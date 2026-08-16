using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XrayVpnApp.Models;

namespace XrayVpnApp.Services;

/// <summary>
/// Tests server latency & download speed.
/// </summary>
public class SpeedTestService
{
    private readonly Logger _logger;

    public SpeedTestService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Test latency (TCP connect through SOCKS) — non-VPN-mode quick test.
    /// </summary>
    public async Task<int> TestLatencyAsync(ServerConfig server, AppSettings settings,
        int timeoutMs = 5000)
    {
        return await App.XrayCore.TestLatencyAsync(server, settings, timeoutMs);
    }

    /// <summary>
    /// Test download speed through SOCKS proxy. Returns Mbps.
    /// </summary>
    public async Task<double> TestDownloadSpeedAsync(ServerConfig server, AppSettings settings,
        int timeoutMs = 15000)
    {
        // Spin up temp xray on test port
        var testSettings = new AppSettings
        {
            SocksPort = 10810,
            HttpPort = 10811,
            EnableMux = false,
            XrayLogLevel = 3,
            EnableFakeDns = false,
            BypassLan = true,
            BypassIran = false,
        };

        bool startedHere = false;
        if (!App.XrayCore.IsRunning)
        {
            if (!App.XrayCore.Start(server, testSettings, tunMode: 0)) return 0;
            startedHere = true;
            await Task.Delay(800); // give core time to start
        }

        try
        {
            var socksPort = startedHere ? 10810 : settings.SocksPort;
            var proxy = new WebProxy($"socks5://127.0.0.1:{socksPort}");

            using var handler = new HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = true,
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XrayVpn-SpeedTest/1.0");

            // Download 25MB test file from Cloudflare
            const string testUrl = "https://speed.cloudflare.com/__down?bytes=25000000";
            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[81920];
            long totalBytes = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalBytes += read;
                if (sw.ElapsedMilliseconds > timeoutMs - 1000) break;
            }
            sw.Stop();

            if (sw.Elapsed.TotalSeconds <= 0 || totalBytes == 0) return 0;

            var mbps = (totalBytes * 8.0) / sw.Elapsed.TotalSeconds / 1_000_000;
            _logger.Info($"Speed test: {totalBytes / 1024 / 1024} MB in {sw.Elapsed.TotalSeconds:F2}s = {mbps:F2} Mbps");
            return mbps;
        }
        catch (Exception ex)
        {
            _logger.Error($"Speed test failed: {ex.Message}");
            return 0;
        }
        finally
        {
            if (startedHere) App.XrayCore.Stop();
        }
    }

    /// <summary>
    /// Test post-connect speed (when VPN is already active).
    /// </summary>
    public async Task<double> TestActiveSpeedAsync(int timeoutMs = 15000)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            };

            const string testUrl = "https://speed.cloudflare.com/__down?bytes=25000000";
            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[81920];
            long totalBytes = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalBytes += read;
                if (sw.ElapsedMilliseconds > timeoutMs - 1000) break;
            }
            sw.Stop();

            if (sw.Elapsed.TotalSeconds <= 0 || totalBytes == 0) return 0;
            return (totalBytes * 8.0) / sw.Elapsed.TotalSeconds / 1_000_000;
        }
        catch (Exception ex)
        {
            _logger.Error($"Active speed test failed: {ex.Message}");
            return 0;
        }
    }
}
