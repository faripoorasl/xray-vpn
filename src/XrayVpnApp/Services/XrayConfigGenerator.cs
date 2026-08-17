using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XrayVpnApp.Models;

namespace XrayVpnApp.Services;

/// <summary>
/// Generates the full Xray JSON config from a ServerConfig + app settings.
/// Includes: dokodemo-door inbound (for TUN mode), socks+http inbounds (fallback),
/// proxy outbound, freedom outbound (direct), blackhole outbound, DNS, routing rules.
/// </summary>
public static class XrayConfigGenerator
{
    public static string Generate(ServerConfig server, AppSettings settings, int tunMode = 1)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            WriteLog(writer, settings.XrayLogLevel);
            WriteDns(writer, server, settings);
            WriteInbounds(writer, settings, tunMode);
            WriteOutbounds(writer, server, settings);
            WriteRouting(writer, settings);
            WritePolicy(writer, settings);

            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteLog(Utf8JsonWriter w, int level)
    {
        w.WritePropertyName("log");
        w.WriteStartObject();
        var lvl = level switch
        {
            0 => "debug",
            1 => "info",
            2 => "warning",
            _ => "error"
        };
        w.WriteString("loglevel", lvl);
        w.WriteString("access", "");
        w.WriteString("error", "");
        w.WriteEndObject();
    }

    private static void WriteDns(Utf8JsonWriter w, ServerConfig server, AppSettings s)
    {
        w.WritePropertyName("dns");
        w.WriteStartObject();

        if (s.EnableFakeDns)
        {
            w.WritePropertyName("servers");
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("address", s.DnsOverHttps ? s.DohUrl : s.RemoteDns);
            w.WriteNumber("port", 443);
            w.WritePropertyName("domains");
            w.WriteStartArray();
            w.WriteStringValue("geosite:geolocation-!cn");
            w.WriteStringValue("geosite:category-ads-all");
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteStartObject();
            w.WriteString("address", s.LocalDns);
            w.WritePropertyName("domains");
            w.WriteStartArray();
            w.WriteStringValue("geosite:cn");
            w.WriteStringValue("geosite:category-ir");
            w.WriteEndArray();
            w.WritePropertyName("expectIPs");
            w.WriteStartArray();
            w.WriteStringValue("geoip:cn");
            w.WriteStringValue("geoip:private");
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteStringValue("localhost");
            w.WriteEndArray();

            w.WritePropertyName("queryStrategy");
            w.WriteStringValue("UseIP");

            w.WritePropertyName("tag");
            w.WriteStringValue("dns_inbound");
        }
        else
        {
            w.WritePropertyName("servers");
            w.WriteStartArray();
            w.WriteStringValue(s.RemoteDns);
            w.WriteStringValue(s.LocalDns);
            w.WriteStringValue("localhost");
            w.WriteEndArray();
        }

        w.WriteEndObject();
    }

    private static void WriteInbounds(Utf8JsonWriter w, AppSettings s, int tunMode)
    {
        w.WritePropertyName("inbounds");
        w.WriteStartArray();

        // SOCKS inbound (fallback, always available)
        w.WriteStartObject();
        w.WriteString("tag", "socks-in");
        w.WriteString("listen", "127.0.0.1");
        w.WriteNumber("port", s.SocksPort);
        w.WriteString("protocol", "socks");
        w.WritePropertyName("settings");
        w.WriteStartObject();
        w.WriteString("auth", "noauth");
        w.WriteBoolean("udp", true);
        w.WriteEndObject();
        w.WritePropertyName("sniffing");
        w.WriteStartObject();
        w.WriteBoolean("enabled", true);
        w.WritePropertyName("destOverride");
        w.WriteStartArray();
        w.WriteStringValue("http");
        w.WriteStringValue("tls");
        w.WriteStringValue("quic");
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();

        // HTTP inbound (fallback)
        w.WriteStartObject();
        w.WriteString("tag", "http-in");
        w.WriteString("listen", "127.0.0.1");
        w.WriteNumber("port", s.HttpPort);
        w.WriteString("protocol", "http");
        w.WritePropertyName("settings");
        w.WriteStartObject();
        w.WriteEndObject();
        w.WritePropertyName("sniffing");
        w.WriteStartObject();
        w.WriteBoolean("enabled", true);
        w.WritePropertyName("destOverride");
        w.WriteStartArray();
        w.WriteStringValue("http");
        w.WriteStringValue("tls");
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();

                w.WriteEndArray();

    }

    private static void WriteOutbounds(Utf8JsonWriter w, ServerConfig server, AppSettings s)
    {
        w.WritePropertyName("outbounds");
        w.WriteStartArray();

        // Proxy outbound
        w.WriteStartObject();
        w.WriteString("tag", "proxy");
        w.WriteString("protocol", server.Protocol);

        w.WritePropertyName("settings");
        w.WriteStartObject();

        if (server.Protocol == "vmess" || server.Protocol == "vless")
        {
            w.WritePropertyName("vnext");
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("address", server.Address);
            w.WriteNumber("port", server.Port);
            w.WritePropertyName("users");
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("id", server.Uuid);
            w.WriteString("encryption", server.Encryption);
            if (server.Protocol == "vmess")
                w.WriteString("alterId", server.AlterId);
            if (!string.IsNullOrEmpty(server.Flow))
                w.WriteString("flow", server.Flow);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndArray();
        }
        else if (server.Protocol == "trojan")
        {
            w.WritePropertyName("servers");
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("address", server.Address);
            w.WriteNumber("port", server.Port);
            w.WriteString("password", server.Uuid);
            w.WriteEndObject();
            w.WriteEndArray();
        }
        else if (server.Protocol == "shadowsocks")
        {
            w.WritePropertyName("servers");
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("address", server.Address);
            w.WriteNumber("port", server.Port);
            w.WriteString("method", server.Encryption);
            w.WriteString("password", server.Uuid);
            w.WriteEndObject();
            w.WriteEndArray();
        }

        w.WriteEndObject(); // settings

        // Mux
        if (s.EnableMux)
        {
            w.WritePropertyName("mux");
            w.WriteStartObject();
            w.WriteBoolean("enabled", true);
            w.WriteNumber("concurrency", s.MuxConcurrency);
            w.WriteEndObject();
        }

        // Stream settings
        WriteStreamSettings(w, server);

        w.WriteEndObject();

        // Direct outbound
        w.WriteStartObject();
        w.WriteString("tag", "direct");
        w.WriteString("protocol", "freedom");
        w.WritePropertyName("settings");
        w.WriteStartObject();
        w.WriteString("domainStrategy", "UseIP");
        w.WriteEndObject();
        w.WriteEndObject();

        // Blackhole outbound
        w.WriteStartObject();
        w.WriteString("tag", "block");
        w.WriteString("protocol", "blackhole");
        w.WritePropertyName("settings");
        w.WriteStartObject();
        w.WritePropertyName("response");
        w.WriteStartObject();
        w.WriteString("type", "http");
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteEndObject();

        // DNS outbound
        w.WriteStartObject();
        w.WriteString("tag", "dns-out");
        w.WriteString("protocol", "dns");
        w.WritePropertyName("settings");
        w.WriteStartObject();
        w.WriteEndObject();
        w.WriteEndObject();

        w.WriteEndArray();
    }

    private static void WriteStreamSettings(Utf8JsonWriter w, ServerConfig server)
    {
        w.WritePropertyName("streamSettings");
        w.WriteStartObject();
        w.WriteString("network", server.Network);

        var security = server.RealityEnabled ? "reality" :
                       server.TlsEnabled ? "tls" : "none";
        w.WriteString("security", security);

        if (server.TlsEnabled && !server.RealityEnabled)
        {
            w.WritePropertyName("tlsSettings");
            w.WriteStartObject();
            w.WriteString("serverName", string.IsNullOrEmpty(server.Sni) ? server.Address : server.Sni);
            w.WriteBoolean("allowInsecure", server.AllowInsecure);
            w.WriteString("fingerprint", server.Fingerprint);
            w.WriteEndObject();
        }

        if (server.RealityEnabled)
        {
            w.WritePropertyName("realitySettings");
            w.WriteStartObject();
            w.WriteString("serverName", server.Sni);
            w.WriteString("fingerprint", server.Fingerprint);
            w.WriteString("publicKey", server.PublicKey);
            w.WriteString("shortId", server.ShortId);
            if (!string.IsNullOrEmpty(server.SpiderX))
                w.WriteString("spiderX", server.SpiderX);
            w.WriteEndObject();
        }

        if (server.Network == "ws")
        {
            w.WritePropertyName("wsSettings");
            w.WriteStartObject();
            w.WriteString("path", string.IsNullOrEmpty(server.Path) ? "/" : server.Path);
            if (!string.IsNullOrEmpty(server.Host))
            {
                w.WritePropertyName("headers");
                w.WriteStartObject();
                w.WriteString("Host", server.Host);
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        else if (server.Network == "grpc")
        {
            w.WritePropertyName("grpcSettings");
            w.WriteStartObject();
            w.WriteString("serviceName", server.Path);
            w.WriteBoolean("multiMode", false);
            w.WriteEndObject();
        }
        else if (server.Network == "tcp" && server.Type == "http")
        {
            w.WritePropertyName("tcpSettings");
            w.WriteStartObject();
            w.WritePropertyName("header");
            w.WriteStartObject();
            w.WriteString("type", "http");
            w.WritePropertyName("request");
            w.WriteStartObject();
            w.WriteString("version", "1.1");
            w.WriteString("method", "GET");
            w.WritePropertyName("path");
            w.WriteStartArray();
            w.WriteStringValue(string.IsNullOrEmpty(server.Path) ? "/" : server.Path);
            w.WriteEndArray();
            w.WritePropertyName("headers");
            w.WriteStartObject();
            w.WriteString("Host", string.IsNullOrEmpty(server.Host) ? server.Address : server.Host);
            w.WriteString("User-Agent", "Mozilla/5.0");
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        }

        w.WriteEndObject();
    }

    private static void WriteRouting(Utf8JsonWriter w, AppSettings s)
    {
        w.WritePropertyName("routing");
        w.WriteStartObject();
        w.WriteString("domainStrategy", "IPIfNonMatch");

        w.WritePropertyName("rules");
        w.WriteStartArray();

        // Block ads
        if (s.BlockAds)
        {
            w.WriteStartObject();
            w.WriteString("type", "field");
            w.WritePropertyName("outboundTag");
            w.WriteStringValue("block");
            w.WritePropertyName("domain");
            w.WriteStartArray();
            w.WriteStringValue("geosite:category-ads-all");
            w.WriteEndArray();
            w.WriteEndObject();
        }

        // Bypass LAN
        if (s.BypassLan)
        {
            w.WriteStartObject();
            w.WriteString("type", "field");
            w.WriteString("outboundTag", "direct");
            w.WritePropertyName("ip");
            w.WriteStartArray();
            w.WriteStringValue("geoip:private");
            w.WriteEndArray();
            w.WriteEndObject();
        }

        // Bypass Iran
        if (s.BypassIran)
        {
            w.WriteStartObject();
            w.WriteString("type", "field");
            w.WriteString("outboundTag", "direct");
            w.WritePropertyName("domain");
            w.WriteStartArray();
            w.WriteStringValue("geosite:category-ir");
            w.WriteStringValue("domain:ir");
            w.WriteStringValue("domain:ایران");
            w.WriteEndArray();
            w.WriteEndObject();

            w.WriteStartObject();
            w.WriteString("type", "field");
            w.WriteString("outboundTag", "direct");
            w.WritePropertyName("ip");
            w.WriteStartArray();
            w.WriteStringValue("geoip:ir");
            w.WriteEndArray();
            w.WriteEndObject();
        }

        // Everything else -> proxy
        w.WriteStartObject();
        w.WriteString("type", "field");
        w.WriteString("outboundTag", "proxy");
        w.WriteString("network", "tcp,udp");
        w.WriteEndObject();

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WritePolicy(Utf8JsonWriter w, AppSettings s)
    {
        w.WritePropertyName("policy");
        w.WriteStartObject();
        w.WritePropertyName("levels");
        w.WriteStartObject();
        w.WritePropertyName("0");
        w.WriteStartObject();
        w.WriteNumber("bufferSize", 1024);
        w.WriteEndObject();
        w.WriteEndObject();
        w.WritePropertyName("system");
        w.WriteStartObject();
        w.WriteBoolean("statsInboundUplink", false);
        w.WriteBoolean("statsOutboundUplink", false);
        w.WriteEndObject();
        w.WriteEndObject();
    }
}
