using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using XrayVpnApp.Models;

namespace XrayVpnApp.Services;

/// <summary>
/// Manages the xray.exe child process lifecycle.
/// Generates config, starts/stops the process, captures logs.
/// </summary>
public class XrayCoreService
{
    private readonly Logger _logger;
    private Process? _xrayProcess;
    private string _configPath = string.Empty;

    public bool IsRunning => _xrayProcess != null && !_xrayProcess.HasExited;
    public int ProcessId => _xrayProcess?.Id ?? -1;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler? Exited;

    public XrayCoreService(Logger logger)
    {
        _logger = logger;
    }

    private string XrayExePath =>
        Path.Combine(App.AppResourceDir, "xray.exe");

    /// <summary>
    /// Generate config & start xray.exe.
    /// </summary>
    public bool Start(ServerConfig server, AppSettings settings, int tunMode = 1)
    {
        Stop();

        if (!File.Exists(XrayExePath))
        {
            _logger.Error($"xray.exe not found at {XrayExePath}");
            return false;
        }

        try
        {
            var configJson = XrayConfigGenerator.Generate(server, settings, tunMode);
            _configPath = Path.Combine(App.AppConfigDir, "config.json");
            File.WriteAllText(_configPath, configJson);

            _logger.Info("Xray config generated. Starting core...");

            var psi = new ProcessStartInfo
            {
                FileName = XrayExePath,
                Arguments = $"run -c \"{_configPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = App.AppResourceDir,
            };

            _xrayProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _xrayProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.Info($"[xray] {e.Data}");
                    OutputReceived?.Invoke(this, e.Data);
                }
            };
            _xrayProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.Error($"[xray] {e.Data}");
                    OutputReceived?.Invoke(this, e.Data);
                }
            };
            _xrayProcess.Exited += (_, _) =>
            {
                _logger.Info($"Xray exited (code={_xrayProcess.ExitCode})");
                Exited?.Invoke(this, EventArgs.Empty);
            };

            _xrayProcess.Start();
            _xrayProcess.BeginOutputReadLine();
            _xrayProcess.BeginErrorReadLine();

            _logger.Info($"Xray started, PID={_xrayProcess.Id}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to start Xray: {ex.Message}");
            _xrayProcess = null;
            return false;
        }
    }

    public void Stop()
    {
        if (_xrayProcess == null) return;
        try
        {
            if (!_xrayProcess.HasExited)
            {
                _logger.Info("Stopping Xray...");
                _xrayProcess.Kill(entireProcessTree: true);
                _xrayProcess.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error stopping Xray: {ex.Message}");
        }
        finally
        {
            _xrayProcess.Dispose();
            _xrayProcess = null;
        }
    }

    /// <summary>
    /// Quick TCP connect test through SOCKS port.
    /// Returns latency in ms, -1 on failure.
    /// </summary>
    public async Task<int> TestLatencyAsync(ServerConfig server, AppSettings settings,
        int timeoutMs = 5000)
    {
        // Start a temp xray instance on a different port if main is busy
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
        if (!IsRunning)
        {
            if (!Start(server, testSettings, tunMode: 0)) return -1;
            startedHere = true;
        }

        try
        {
            var socksPort = startedHere ? 10810 : settings.SocksPort;
            var proxy = new System.Net.WebProxy("socks5://127.0.0.1:" + socksPort);

            using var handler = new System.Net.Http.HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = true,
            };
            using var client = new System.Net.Http.HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await client.GetStringAsync("https://www.gstatic.com/generate_204");
            sw.Stop();
            return (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
        finally
        {
            if (startedHere) Stop();
        }
    }
}
