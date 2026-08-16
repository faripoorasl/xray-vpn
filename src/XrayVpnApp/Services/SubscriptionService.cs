using System;
using System.Net.Http;
using System.Threading.Tasks;
using XrayVpnApp.Models;

namespace XrayVpnApp.Services;

/// <summary>
/// Fetches & parses subscription URLs.
/// </summary>
public class SubscriptionService
{
    private readonly ConfigParserService _parser;
    private readonly Logger _logger;
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public SubscriptionService(ConfigParserService parser, Logger logger)
    {
        _parser = parser;
        _logger = logger;
    }

    /// <summary>
    /// Fetch & parse a subscription URL. Returns parsed servers.
    /// </summary>
    public async Task<(bool success, string message, System.Collections.Generic.List<ServerConfig> servers)>
        FetchAsync(string url, string subscriptionId = "")
    {
        try
        {
            _logger.Info($"Fetching subscription: {url}");

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("XrayVpn/1.0");
            request.Headers.Add("Accept", "text/plain, application/json, */*");

            var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.Info($"Subscription returned {content.Length} bytes");

            // Subscriptions can be base64-encoded as a whole
            if (!content.Contains("://") && !content.TrimStart().StartsWith("{"))
            {
                try
                {
                    content = DecodeBase64(content);
                }
                catch { /* not base64 */ }
            }

            var servers = _parser.ParseAuto(content);
            foreach (var s in servers)
                s.SubscriptionId = subscriptionId;

            _logger.Info($"Parsed {servers.Count} servers from subscription");
            return (true, $"OK ({servers.Count})", servers);
        }
        catch (Exception ex)
        {
            _logger.Error($"Subscription fetch failed: {ex.Message}");
            return (false, ex.Message, new System.Collections.Generic.List<ServerConfig>());
        }
    }

    private static string DecodeBase64(string s)
    {
        s = s.Replace("-", "+").Replace("_", "/").Trim();
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        var bytes = Convert.FromBase64String(s);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
