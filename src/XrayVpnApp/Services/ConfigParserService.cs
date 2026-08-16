using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Text.Json;
using XrayVpnApp.Models;

namespace XrayVpnApp.Services;

/// <summary>
/// Parses V2Ray/Xray share-link protocols and full JSON configs.
/// Supports: vmess://, vless://, trojan://, ss://, Subscription URLs, JSON file.
/// </summary>
public class ConfigParserService
{
    private readonly Logger _logger;

    public ConfigParserService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Auto-detect format and parse a single config or list of configs.
    /// </summary>
    public List<ServerConfig> ParseAuto(string input)
    {
        var results = new List<ServerConfig>();
        if (string.IsNullOrWhiteSpace(input)) return results;

        input = input.Trim();

        // Multi-line content (subscription body)
        if (input.Contains('\n'))
        {
            foreach (var line in input.Split(new[] { '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var l = line.Trim();
                if (l.StartsWith("#") || l.StartsWith("//")) continue;
                var parsed = ParseSingle(l);
                if (parsed != null) results.Add(parsed);
            }
            return results;
        }

        // Subscription URL
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Treat as subscription URL — caller should fetch first.
            return results;
        }

        // JSON file path or JSON content
        if (input.StartsWith("{") || input.StartsWith("["))
        {
            return ParseJson(input);
        }

        var single = ParseSingle(input);
        if (single != null) results.Add(single);
        return results;
    }

    /// <summary>
    /// Parse a single share-link.
    /// </summary>
    public ServerConfig? ParseSingle(string link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        link = link.Trim();

        try
        {
            if (link.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                return ParseVmess(link);
            if (link.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                return ParseVless(link);
            if (link.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase))
                return ParseTrojan(link);
            if (link.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
                return ParseShadowsocks(link);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to parse link '{link.Substring(0, Math.Min(50, link.Length))}...': {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// VMess: vmess://&lt;base64-json&gt;
    /// JSON: { "v":"2","ps":"remark","add":"host","port":"443","id":"uuid",
    ///         "aid":"0","net":"tcp","type":"none","host":"","path":"",
    ///         "tls":"tls","sni":"","scy":"auto" }
    /// </summary>
    private ServerConfig ParseVmess(string link)
    {
        var base64 = link.Substring("vmess://".Length);
        var json = Base64Decode(base64);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var server = new ServerConfig
        {
            Protocol = "vmess",
            OriginalUrl = link,
            Remark = GetString(root, "ps", "VMess"),
            Address = GetString(root, "add", ""),
            Port = GetInt(root, "port", 443),
            Uuid = GetString(root, "id", ""),
            AlterId = GetString(root, "aid", "0"),
            Network = GetString(root, "net", "tcp"),
            Type = GetString(root, "type", "none"),
            Host = GetString(root, "host", ""),
            Path = GetString(root, "path", ""),
            Security = GetString(root, "scy", "auto"),
            TlsEnabled = GetString(root, "tls", "") == "tls",
            Sni = GetString(root, "sni", ""),
        };

        if (string.IsNullOrEmpty(server.Sni) && server.TlsEnabled)
            server.Sni = server.Address;

        return server;
    }

    /// <summary>
    /// VLESS: vless://&lt;uuid&gt;@&lt;host&gt;:&lt;port&gt;?param=value&amp;...#&lt;remark&gt;
    /// Params: encryption, security(tls/reality/none), type(ws/grpc/tcp),
    ///         host, path, sni, fp, pbk, sid, flow, alpn.
    /// </summary>
    private ServerConfig ParseVless(string link)
    {
        var uri = new Uri(link);
        var query = ParseQueryString(uri.Query);

        var server = new ServerConfig
        {
            Protocol = "vless",
            OriginalUrl = link,
            Remark = string.IsNullOrEmpty(uri.Fragment)
                ? $"{uri.Host}:{uri.Port}"
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Address = uri.Host,
            Port = uri.Port,
            Uuid = uri.UserInfo,
            Encryption = DictGet(query, "encryption") ?? "none",
            Network = DictGet(query, "type") ?? "tcp",
            Security = DictGet(query, "security") ?? "tls",
            Host = DictGet(query, "host") ?? "",
            Path = DictGet(query, "path") ?? "",
            Sni = DictGet(query, "sni") ?? DictGet(query, "peer") ?? "",
            Flow = DictGet(query, "flow") ?? "",
            Fingerprint = DictGet(query, "fp") ?? "chrome",
            PublicKey = DictGet(query, "pbk") ?? "",
            ShortId = DictGet(query, "sid") ?? "",
            SpiderX = DictGet(query, "spx") ?? "",
            ServerName = DictGet(query, "sni") ?? "",
        };

        server.TlsEnabled = server.Security == "tls";
        server.RealityEnabled = server.Security == "reality";

        if (string.IsNullOrEmpty(server.Sni) && server.TlsEnabled)
            server.Sni = server.Address;

        // gRPC path is actually serviceName
        if (server.Network == "grpc" && !string.IsNullOrEmpty(server.Path))
            server.Path = Uri.UnescapeDataString(server.Path);

        return server;
    }

    /// <summary>
    /// Trojan: trojan://&lt;password&gt;@&lt;host&gt;:&lt;port&gt;?param=value#&lt;remark&gt;
    /// </summary>
    private ServerConfig ParseTrojan(string link)
    {
        var uri = new Uri(link);
        var query = ParseQueryString(uri.Query);

        var server = new ServerConfig
        {
            Protocol = "trojan",
            OriginalUrl = link,
            Remark = string.IsNullOrEmpty(uri.Fragment)
                ? $"{uri.Host}:{uri.Port}"
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Address = uri.Host,
            Port = uri.Port,
            Uuid = Uri.UnescapeDataString(uri.UserInfo), // password
            Network = DictGet(query, "type") ?? "tcp",
            Security = "tls",
            Host = DictGet(query, "host") ?? "",
            Path = DictGet(query, "path") ?? "",
            Sni = DictGet(query, "sni") ?? DictGet(query, "peer") ?? "",
            Flow = DictGet(query, "flow") ?? "",
            Fingerprint = DictGet(query, "fp") ?? "chrome",
            AllowInsecure = (DictGet(query, "allowInsecure") ?? "0") == "1",
            TlsEnabled = true,
        };

        if (string.IsNullOrEmpty(server.Sni))
            server.Sni = server.Address;

        return server;
    }

    /// <summary>
    /// Shadowsocks: ss://&lt;base64(method:password)&gt;@&lt;host&gt;:&lt;port&gt;#&lt;remark&gt;
    ///          or: ss://&lt;base64(method:password@host:port)&gt;#&lt;remark&gt;
    /// </summary>
    private ServerConfig ParseShadowsocks(string link)
    {
        // Strip remark
        string remark = "";
        int hash = link.IndexOf('#');
        if (hash >= 0)
        {
            remark = Uri.UnescapeDataString(link.Substring(hash + 1));
            link = link.Substring(0, hash);
        }

        var body = link.Substring("ss://".Length);

        // New format: base64@host:port
        int at = body.LastIndexOf('@');
        string methodPassword;
        string hostPort;

        if (at >= 0)
        {
            methodPassword = body.Substring(0, at);
            hostPort = body.Substring(at + 1);
        }
        else
        {
            // Old format: full base64
            var decoded = Base64Decode(body);
            at = decoded.LastIndexOf('@');
            methodPassword = decoded.Substring(0, at);
            hostPort = decoded.Substring(at + 1);
        }

        // methodPassword may itself be base64
        try
        {
            methodPassword = Base64Decode(methodPassword);
        }
        catch { /* not base64, treat as plain */ }

        var colon = methodPassword.IndexOf(':');
        var method = colon > 0 ? methodPassword.Substring(0, colon) : "aes-256-gcm";
        var password = colon > 0 ? methodPassword.Substring(colon + 1) : "";

        var lastColon = hostPort.LastIndexOf(':');
        var host = hostPort.Substring(0, lastColon);
        var port = int.Parse(hostPort.Substring(lastColon + 1));

        return new ServerConfig
        {
            Protocol = "shadowsocks",
            OriginalUrl = "ss://" + link.Substring("ss://".Length),
            Remark = string.IsNullOrEmpty(remark) ? $"{host}:{port}" : remark,
            Address = host,
            Port = port,
            Uuid = password,
            Encryption = method,
            Network = "tcp",
            TlsEnabled = false,
        };
    }

    /// <summary>
    /// Parse a full Xray JSON config (inbounds/outbounds).
    /// Extracts the proxy outbound as a ServerConfig.
    /// </summary>
    public List<ServerConfig> ParseJson(string json)
    {
        var results = new List<ServerConfig>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("outbounds", out var outbounds))
            {
                foreach (var ob in outbounds.EnumerateArray())
                {
                    var protocol = GetString(ob, "protocol", "");
                    if (protocol is "vmess" or "vless" or "trojan" or "shadowsocks")
                    {
                        var server = ParseOutboundElement(ob);
                        if (server != null) results.Add(server);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"JSON parse error: {ex.Message}");
        }
        return results;
    }

    private ServerConfig? ParseOutboundElement(JsonElement ob)
    {
        var protocol = GetString(ob, "protocol", "");
        var settings = ob.GetProperty("settings");
        var vnext = settings.TryGetProperty("vnext", out var vn) ? vn[0] :
                    settings.TryGetProperty("servers", out var ss) ? ss[0] :
                    (JsonElement?)null;

        if (vnext == null) return null;

        var user = vnext.Value.TryGetProperty("users", out var users) && users.GetArrayLength() > 0
            ? users[0]
            : (JsonElement?)null;

        var server = new ServerConfig
        {
            Protocol = protocol,
            Address = GetString(vnext.Value, "address", ""),
            Port = GetInt(vnext.Value, "port", 443),
            Uuid = user.HasValue ? GetString(user.Value, "id", "") : "",
            AlterId = user.HasValue ? GetString(user.Value, "alterId", "0") : "0",
            Encryption = user.HasValue ? GetString(user.Value, "encryption", "none") : "none",
            Flow = user.HasValue ? GetString(user.Value, "flow", "") : "",
            Remark = $"{GetString(vnext.Value, "address", "")}:{GetInt(vnext.Value, "port", 0)}",
        };

        // streamSettings
        if (ob.TryGetProperty("streamSettings", out var ss2))
        {
            server.Network = GetString(ss2, "network", "tcp");
            server.Security = GetString(ss2, "security", "none");
            server.TlsEnabled = server.Security == "tls";
            server.RealityEnabled = server.Security == "reality";

            if (ss2.TryGetProperty("tlsSettings", out var tls))
            {
                server.Sni = GetString(tls, "serverName", "");
                server.AllowInsecure = GetBool(tls, "allowInsecure", false);
                server.Fingerprint = GetString(tls, "fingerprint", "chrome");
            }
            if (ss2.TryGetProperty("realitySettings", out var reality))
            {
                server.PublicKey = GetString(reality, "publicKey", "");
                server.ShortId = GetString(reality, "shortId", "");
                server.Sni = GetString(reality, "serverName", "");
                server.Fingerprint = GetString(reality, "fingerprint", "chrome");
            }
            if (ss2.TryGetProperty("wsSettings", out var ws))
            {
                server.Path = GetString(ws, "path", "/");
                if (ws.TryGetProperty("headers", out var headers) && headers.TryGetProperty("Host", out var host))
                    server.Host = host.GetString() ?? "";
            }
            if (ss2.TryGetProperty("grpcSettings", out var grpc))
            {
                server.Path = GetString(grpc, "serviceName", "");
            }
        }

        return server;
    }

    #region Helpers

    private static string GetString(JsonElement el, string name, string def = "")
    {
        if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString() ?? def;
        if (el.TryGetProperty(name, out var p2))
        {
            return p2.ValueKind switch
            {
                JsonValueKind.Number => p2.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => def
            };
        }
        return def;
    }

    private static int GetInt(JsonElement el, string name, int def = 0)
    {
        if (el.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number) return p.GetInt32();
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var v)) return v;
        }
        return def;
    }

    private static bool GetBool(JsonElement el, string name, bool def = false)
    {
        if (el.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.True) return true;
            if (p.ValueKind == JsonValueKind.False) return false;
            if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var v)) return v;
        }
        return def;
    }

    private static string Base64Decode(string s)
    {
        // Pad
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        var bytes = Convert.FromBase64String(s);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Manual query-string parser (avoid System.Web dependency).
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;

        // Strip leading '?'
        if (query.StartsWith("?")) query = query.Substring(1);

        foreach (var pair in query.Split('&'))
        {
            if (string.IsNullOrEmpty(pair)) continue;
            var eq = pair.IndexOf('=');
            string key, value;
            if (eq < 0)
            {
                key = Uri.UnescapeDataString(pair);
                value = "";
            }
            else
            {
                key = Uri.UnescapeDataString(pair.Substring(0, eq));
                value = Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            result[key] = value;
        }
        return result;
    }

    /// <summary>
    /// Safe indexer — returns null if key not found.
    /// </summary>
    private static string? DictGet(Dictionary<string, string> dict, string key) =>
        dict.TryGetValue(key, out var val) ? val : null;

    #endregion
}
