using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using XrayVpnApp.Utils;

namespace XrayVpnApp.Services;

/// <summary>
/// Creates &amp; manages a wintun TUN adapter + tun2socks to route ALL system
/// traffic through Xray's SOCKS proxy.
///
/// Architecture:
///   1. Create wintun adapter (via wintun.dll)
///   2. Assign IP 10.10.0.2/24 + gateway 10.10.0.1 via netsh
///   3. Start tun2socks.exe to bridge TUN packets to Xray's SOCKS port
///   4. Add default route 0.0.0.0/0 via 10.10.0.1
/// </summary>
public class TunService
{
    private readonly Logger _logger;
    private IntPtr _adapterHandle = IntPtr.Zero;
    private Process? _tun2socksProcess;
    private bool _routesInstalled = false;
    private bool _wintunLoaded = false;
    private int _tunInterfaceIndex = 0;

    public bool IsRunning => _adapterHandle != IntPtr.Zero || _tun2socksProcess != null;
    public int InterfaceIndex => _tunInterfaceIndex;

    public TunService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Find and load wintun.dll (needed for adapter creation).
    /// </summary>
    private bool EnsureWintunLoaded()
    {
        if (_wintunLoaded) return true;

        try
        {
            var candidates = new List<string>();
            var resDir = App.AppResourceDir;
            candidates.Add(Path.Combine(resDir, "wintun.dll"));

            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var exeDir = Path.GetDirectoryName(exePath)!;
                    candidates.Add(Path.Combine(exeDir, "wintun.dll"));
                    candidates.Add(Path.Combine(exeDir, "Resources", "wintun.dll"));
                }
            }
            catch { }

            candidates.Add(Path.Combine(AppContext.BaseDirectory, "wintun.dll"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Resources", "wintun.dll"));

            _logger.Info("Searching for wintun.dll:");
            foreach (var c in candidates.Distinct())
            {
                var exists = File.Exists(c);
                _logger.Info($"  {(exists ? "[FOUND]" : "[miss]")} {c}");
            }

            foreach (var path in candidates.Distinct())
            {
                if (File.Exists(path))
                {
                    _logger.Info($"Loading wintun.dll from: {path}");
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        SetDllDirectory(dir);
                    }

                    var hModule = LoadLibrary(path);
                    if (hModule != IntPtr.Zero)
                    {
                        _logger.Info($"wintun.dll loaded (handle=0x{hModule.ToInt64():X})");
                        _wintunLoaded = true;
                        return true;
                    }
                    else
                    {
                        var err = Marshal.GetLastWin32Error();
                        _logger.Error($"LoadLibrary failed (Win32 error {err})");
                    }
                }
            }

            _logger.Error("wintun.dll not found!");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"EnsureWintunLoaded: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Path to tun2socks.exe (bundled in Resources folder).
    /// </summary>
    private string Tun2socksExePath => Path.Combine(App.AppResourceDir, "tun2socks.exe");

    /// <summary>
    /// Start the full TUN pipeline: wintun adapter + tun2socks + routes.
    /// </summary>
    public bool Start(Models.AppSettings settings)
    {
        if (IsRunning)
        {
            _logger.Warn("TUN already running, stopping first");
            Stop();
        }

        try
        {
            // Step 1: Load wintun.dll
            _logger.Info("=== TUN Service: Step 1/5 - Loading wintun.dll ===");
            if (!EnsureWintunLoaded())
            {
                _logger.Error("Cannot start TUN: wintun.dll could not be loaded");
                return false;
            }

            // Step 2: Create wintun adapter
            _logger.Info($"=== TUN Service: Step 2/5 - Creating adapter '{settings.TunAdapterName}' ===");
            _adapterHandle = WintunNative.WintunCreateAdapter(
                settings.TunAdapterName,
                "XrayVpn",
                IntPtr.Zero);

            if (_adapterHandle == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                _logger.Error($"WintunCreateAdapter failed (Win32 error {err})");
                _logger.Error("Possible causes: app not running as Admin, antivirus blocking driver install, architecture mismatch");
                return false;
            }
            _logger.Info($"TUN adapter created (handle=0x{_adapterHandle.ToInt64():X})");

            // Step 3: Assign IP + gateway via netsh
            _logger.Info($"=== TUN Service: Step 3/5 - Assigning IP {settings.TunIp} ===");
            var netshArgs = $"interface ip set address name=\"{settings.TunAdapterName}\" " +
                            $"static {settings.TunIp} {settings.TunMask} {settings.TunGateway}";
            var (netshOk, netshOut, netshErr) = RunProcessWithOutput("netsh", netshArgs);
            _logger.Info($"netsh output: {netshOut}");
            if (!string.IsNullOrEmpty(netshErr)) _logger.Warn($"netsh stderr: {netshErr}");

            if (!netshOk)
            {
                _logger.Error("Failed to assign IP to TUN adapter");
                Stop();
                return false;
            }

            // Set MTU
            RunProcessWithOutput("netsh",
                $"interface ipv4 set subinterface \"{settings.TunAdapterName}\" mtu={settings.TunMtu} store=persistent");

            // Get interface index
            _tunInterfaceIndex = (int)GetInterfaceIndex(settings.TunAdapterName);
            if (_tunInterfaceIndex == 0)
            {
                _logger.Error("Could not determine TUN interface index");
                Stop();
                return false;
            }
            _logger.Info($"TUN interface index: {_tunInterfaceIndex}");

            // Step 4: Start tun2socks.exe to bridge TUN packets to Xray's SOCKS port
            _logger.Info($"=== TUN Service: Step 4/5 - Starting tun2socks ===");
            if (!File.Exists(Tun2socksExePath))
            {
                _logger.Error($"tun2socks.exe not found at {Tun2socksExePath}");
                Stop();
                return false;
            }

            var tun2socksArgs = $"--device \"{settings.TunAdapterName}\" " +
                                $"--proxy socks5://127.0.0.1:{settings.SocksPort} " +
                                $"--mtu {settings.TunMtu} " +
                                $"--loglevel silent";

            _logger.Info($"tun2socks.exe {tun2socksArgs}");

            var psi = new ProcessStartInfo
            {
                FileName = Tun2socksExePath,
                Arguments = tun2socksArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = App.AppResourceDir,
            };

            _tun2socksProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _tun2socksProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.Info($"[tun2socks] {e.Data}");
            };
            _tun2socksProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.Error($"[tun2socks] {e.Data}");
            };
            _tun2socksProcess.Exited += (_, _) =>
            {
                _logger.Info($"tun2socks exited (code={_tun2socksProcess?.ExitCode})");
            };

            _tun2socksProcess.Start();
            _tun2socksProcess.BeginOutputReadLine();
            _tun2socksProcess.BeginErrorReadLine();

            _logger.Info($"tun2socks started (PID={_tun2socksProcess.Id})");

            // Give tun2socks time to initialize
            System.Threading.Thread.Sleep(2000);

            if (_tun2socksProcess.HasExited)
            {
                _logger.Error($"tun2socks exited prematurely (code={_tun2socksProcess.ExitCode})");
                Stop();
                return false;
            }

            // Step 5: Install routes
            _logger.Info("=== TUN Service: Step 5/5 - Installing routes ===");
            InstallRoutes(settings);

            _logger.Info("=== TUN service started successfully ===");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"TUN start failed: {ex.Message}");
            _logger.Error($"Stack trace: {ex.StackTrace}");
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            if (_routesInstalled)
            {
                RemoveRoutes();
                _routesInstalled = false;
            }

            if (_tun2socksProcess != null)
            {
                try
                {
                    if (!_tun2socksProcess.HasExited)
                    {
                        _logger.Info("Stopping tun2socks...");
                        _tun2socksProcess.Kill(entireProcessTree: true);
                        _tun2socksProcess.WaitForExit(3000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error stopping tun2socks: {ex.Message}");
                }
                finally
                {
                    _tun2socksProcess.Dispose();
                    _tun2socksProcess = null;
                }
            }

            if (_adapterHandle != IntPtr.Zero)
            {
                WintunNative.WintunCloseAdapter(_adapterHandle);
                _adapterHandle = IntPtr.Zero;
            }

            _tunInterfaceIndex = 0;
            _logger.Info("TUN service stopped");
        }
        catch (Exception ex)
        {
            _logger.Error($"TUN Stop error: {ex.Message}");
        }
    }

    private void InstallRoutes(Models.AppSettings settings)
    {
        var args = $"add 0.0.0.0 mask 0.0.0.0 {settings.TunGateway} metric 5 if {_tunInterfaceIndex}";
        var (ok, output, err) = RunProcessWithOutput("route", args);
        _logger.Info($"route {args}: ok={ok}, output={output}, err={err}");

        _routesInstalled = true;
        _logger.Info("Default route installed through TUN");
    }

    private void RemoveRoutes()
    {
        try
        {
            var (ok, output, _) = RunProcessWithOutput("route", "delete 0.0.0.0");
            _logger.Info($"route delete: ok={ok}, output={output}");
            _logger.Info("Routes removed");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Error removing routes: {ex.Message}");
        }
    }

    private uint GetInterfaceIndex(string adapterName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"(Get-NetAdapter -Name '{adapterName}').ifIndex\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            _logger.Info($"GetInterfaceIndex: PowerShell output = '{output}'");
            return uint.TryParse(output, out var idx) ? idx : 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"GetInterfaceIndex failed: {ex.Message}");
            return 0;
        }
    }

    private (bool success, string output, string error) RunProcessWithOutput(string fileName, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd().Trim();
            var error = p.StandardError.ReadToEnd().Trim();
            p.WaitForExit(10000);
            return (p.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.Error($"{fileName} failed: {ex.Message}");
            return (false, "", ex.Message);
        }
    }

    #region Win32 Helpers for DLL loading

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string lpPathName);

    #endregion
}
