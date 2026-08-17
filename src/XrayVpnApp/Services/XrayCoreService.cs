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
        KillOrphanXrayProcesses();  // Kill any orphan xray.exe from previous runs

        if (!File.Exists(XrayExePath))
        {
            _logger.Error($"xray.exe not found at {XrayExePath}");
            _logger.Error($"AppResourceDir = {App.AppResourceDir}");
            _logger.Error($"EXE path = {Environment.ProcessPath}");
            _logger.Error($"BaseDirectory = {AppContext.BaseDirectory}");
            return false;
        }

        // Check if ports are available before starting
        if (!ArePortsAvailable(settings.SocksPort, settings.HttpPort))
        {
            _logger.Error($"Ports {settings.SocksPort} (SOCKS) and/or {settings.HttpPort} (HTTP) are in use.");
            _logger.Error("Attempting to kill processes using these ports...");
            KillProcessesOnPort(settings.SocksPort);
            KillProcessesOnPort(settings.HttpPort);
            System.Threading.Thread.Sleep(500);
            if (!ArePortsAvailable(settings.SocksPort, settings.HttpPort))
            {
                _logger.Error("Ports still in use. Cannot start Xray.");
                return false;
            }
        }

        try
        {
            var configJson = XrayConfigGenerator.Generate(server, settings, tunMode);
            _configPath = Path.Combine(App.AppConfigDir, "config.json");
            File.WriteAllText(_configPath, configJson);

            _logger.Info($"Xray config generated at: {_configPath}");
            _logger.Info($"Starting xray.exe from: {XrayExePath}");
            _logger.Info($"Working directory: {App.AppResourceDir}");

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

            // Give xray 2 seconds to start (or fail)
            System.Threading.Thread.Sleep(2000);

            // Check if process exited prematurely
            if (_xrayProcess.HasExited)
            {
                _logger.Error($"Xray exited prematurely with code {_xrayProcess.ExitCode}");
                _logger.Error("This usually means the config is invalid or xray.exe cannot run.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to start Xray: {ex.Message}");
            _xrayProcess = null;
            return false;
        }
    }

    /// <summary>
    /// Kill any orphan xray.exe processes (from previous app crashes or hangs).
    /// </summary>
    private void KillOrphanXrayProcesses()
    {
        try
        {
            var selfId = Environment.ProcessId;
            var xrayProcs = Process.GetProcessesByName("xray");
            int killed = 0;
            foreach (var p in xrayProcs)
            {
                if (p.Id == selfId) continue;
                try
                {
                    _logger.Info($"Killing orphan xray.exe process (PID={p.Id})");
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(2000);
                    killed++;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to kill PID {p.Id}: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }
            if (killed > 0)
            {
                _logger.Info($"Killed {killed} orphan xray.exe process(es)");
                System.Threading.Thread.Sleep(500);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"KillOrphanXrayProcesses: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if both SOCKS and HTTP ports are free.
    /// </summary>
    private bool ArePortsAvailable(int socksPort, int httpPort)
    {
        try
        {
            var socksTest = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, socksPort);
            socksTest.Start();
            socksTest.Stop();

            var httpTest = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, httpPort);
            httpTest.Start();
            httpTest.Stop();

            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Port check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Find and kill processes listening on a specific port (Windows only).
    /// </summary>
    private void KillProcessesOnPort(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c netstat -ano | findstr :{port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var pidsToKill = new System.Collections.Generic.HashSet<int>();

            foreach (var line in lines)
            {
                var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                if (int.TryParse(parts[parts.Length - 1], out var pid) && pid > 0)
                {
                    pidsToKill.Add(pid);
                }
            }

            foreach (var pid in pidsToKill)
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    var name = proc.ProcessName;
                    _logger.Info($"Killing PID {pid} ({name}) holding port {port}");
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(2000);
                    proc.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to kill PID {pid}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"KillProcessesOnPort({port}): {ex.Message}");
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
