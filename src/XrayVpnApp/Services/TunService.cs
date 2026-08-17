using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using XrayVpnApp.Utils;

namespace XrayVpnApp.Services;

/// <summary>
/// Manages TUN adapter creation via tun2socks.exe.
///
/// Architecture:
///   1. Start Xray (SOCKS+HTTP inbounds)
///   2. Start tun2socks.exe - it CREATES the TUN adapter
///   3. Wait for adapter to appear
///   4. Assign IP 10.10.0.2/24 to the adapter
///   5. Install default route via the adapter
///
/// Note: tun2socks creates its own adapter; we don't need wintun.dll
/// in our code anymore. tun2socks bundles wintun internally.
/// </summary>
public class TunService
{
    private readonly Logger _logger;
    private Process? _tun2socksProcess;
    private bool _routesInstalled = false;
    private int _tunInterfaceIndex = 0;
    private string _adapterName = string.Empty;

    public bool IsRunning => _tun2socksProcess != null;
    public int InterfaceIndex => _tunInterfaceIndex;

    public TunService(Logger logger)
    {
        _logger = logger;
    }

    private string Tun2socksExePath => Path.Combine(App.AppResourceDir, "tun2socks.exe");

    /// <summary>
    /// Start tun2socks which creates TUN adapter, then assign IP + routes.
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
            // Step 1: Cleanup any existing adapters from previous runs
            _logger.Info("=== TUN Service: Step 1/5 - Cleaning up existing adapters ===");
            CleanupExistingAdapters(settings.TunAdapterName);

            // Step 2: Start tun2socks (it creates the adapter)
            _logger.Info($"=== TUN Service: Step 2/5 - Starting tun2socks ===");
            if (!File.Exists(Tun2socksExePath))
            {
                _logger.Error($"tun2socks.exe not found at {Tun2socksExePath}");
                return false;
            }

            // tun2socks uses wintun internally on Windows when no driver:// prefix
            // Just pass the adapter name directly (no prefix)
            var deviceName = settings.TunAdapterName;
            var tun2socksArgs = $"--device \"{deviceName}\" " +
                                $"--proxy socks5://127.0.0.1:{settings.SocksPort} " +
                                $"--mtu {settings.TunMtu} " +
                                $"--loglevel info";

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
                    _logger.Info($"[tun2socks] {e.Data}");  // tun2socks logs to stderr
            };
            _tun2socksProcess.Exited += (_, _) =>
            {
                _logger.Info($"tun2socks exited (code={_tun2socksProcess?.ExitCode})");
            };

            _tun2socksProcess.Start();
            _tun2socksProcess.BeginOutputReadLine();
            _tun2socksProcess.BeginErrorReadLine();

            _logger.Info($"tun2socks started (PID={_tun2socksProcess.Id})");

            // Give tun2socks time to create the adapter
            _logger.Info("Waiting 3 seconds for tun2socks to create adapter...");
            System.Threading.Thread.Sleep(3000);

            if (_tun2socksProcess.HasExited)
            {
                _logger.Error($"tun2socks exited prematurely (code={_tun2socksProcess.ExitCode})");
                return false;
            }

            // Step 3: Find the TUN adapter (tun2socks may name it differently)
            _logger.Info("=== TUN Service: Step 3/5 - Finding TUN adapter ===");
            _adapterName = settings.TunAdapterName;
            _tunInterfaceIndex = FindTunAdapterIndex(settings.TunAdapterName);

            if (_tunInterfaceIndex == 0)
            {
                // Try alternative names
                _logger.Info("Adapter not found by exact name, searching for similar...");
                _tunInterfaceIndex = FindTunAdapterIndexFuzzy(settings.TunAdapterName);
            }

            if (_tunInterfaceIndex == 0)
            {
                _logger.Error("Could not find TUN adapter created by tun2socks");
                _logger.Error("Available adapters:");
                ListAllAdapters();
                Stop();
                return false;
            }
            _logger.Info($"Found TUN adapter: index={_tunInterfaceIndex}");

            // Step 4: Assign IP address
            _logger.Info($"=== TUN Service: Step 4/5 - Assigning IP {settings.TunIp} ===");
            AssignIpToAdapter(_adapterName, settings.TunIp, settings.TunMask);

            // Verify IP was assigned
            VerifyAdapterIp(_adapterName);

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

    /// <summary>
    /// Remove any existing TUN adapters (from previous runs).
    /// </summary>
    private void CleanupExistingAdapters(string adapterName)
    {
        try
        {
            // Use PowerShell to find and remove adapters with our name (or similar)
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"Get-NetAdapter | Where-Object {{ $_.Name -like '*{adapterName}*' -or $_.InterfaceDescription -like '*tun2socks*' -or $_.InterfaceDescription -like '*wintun*' }} | ForEach-Object {{ Write-Host \\\"Removing: $($_.Name) (ifIndex=$($_.ifIndex))\\\"; Remove-NetAdapter -Name $_.Name -Confirm:$false }}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            var error = p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            if (!string.IsNullOrEmpty(output))
            {
                _logger.Info($"Cleanup output: {output}");
            }
            if (!string.IsNullOrEmpty(error))
            {
                _logger.Info($"Cleanup errors: {error}");
            }
            System.Threading.Thread.Sleep(1000);  // Let Windows clean up
        }
        catch (Exception ex)
        {
            _logger.Warn($"CleanupExistingAdapters: {ex.Message}");
        }
    }

    /// <summary>
    /// Find TUN adapter by exact name.
    /// </summary>
    private int FindTunAdapterIndex(string adapterName)
    {
        try
        {
            // Use wildcard match because wintun appends " Tunnel" to the name
            // e.g. "XrayVpn" becomes "XrayVpn Tunnel"
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"(Get-NetAdapter -Name '{adapterName}*' -ErrorAction SilentlyContinue | Select-Object -First 1).ifIndex\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            _logger.Info($"FindTunAdapterIndex('{adapterName}*'): output='{output}'");
            return int.TryParse(output, out var idx) ? idx : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Find TUN adapter by fuzzy match (for when tun2socks renames it).
    /// </summary>
    private int FindTunAdapterIndexFuzzy(string adapterName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"Get-NetAdapter | Where-Object {{ $_.Name -like '*{adapterName}*' -or $_.InterfaceDescription -like '*tun2socks*' -or $_.InterfaceDescription -like '*wintun*' }} | Select-Object Name, ifIndex, InterfaceDescription | Format-Table -AutoSize\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            _logger.Info($"Fuzzy search results:");
            foreach (var line in output.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    _logger.Info($"  {line.TrimEnd()}");

            // Try to extract ifIndex from the first matching adapter
            var idxPsi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"(Get-NetAdapter | Where-Object {{ $_.Name -like '*{adapterName}*' -or $_.InterfaceDescription -like '*tun2socks*' -or $_.InterfaceDescription -like '*wintun*' }} | Select-Object -First 1).ifIndex\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p2 = Process.Start(idxPsi)!;
            var idxOutput = p2.StandardOutput.ReadToEnd().Trim();
            p2.WaitForExit(5000);
            _logger.Info($"First matching ifIndex: '{idxOutput}'");
            return int.TryParse(idxOutput, out var idx) ? idx : 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"FindTunAdapterIndexFuzzy: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// List all network adapters (for debugging).
    /// </summary>
    private void ListAllAdapters()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"Get-NetAdapter | Select-Object Name, ifIndex, InterfaceDescription, Status | Format-Table -AutoSize\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            foreach (var line in output.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    _logger.Info($"  {line.TrimEnd()}");
        }
        catch { }
    }

    /// <summary>
    /// Assign IP address to the TUN adapter.
    /// </summary>
    private void AssignIpToAdapter(string adapterName, string ip, string mask)
    {
        try
        {
            // Try both with the original name and with " Tunnel" suffix
            // wintun appends " Tunnel" to adapter names
            var args = $"interface ip set address name=\"{adapterName}\" static {ip} {mask}";
            var (ok, output, err) = RunProcessWithOutput("netsh", args);
            _logger.Info($"netsh assign IP (try 1): ok={ok}, output='{output}', err='{err}'");

            if (!ok)
            {
                var args2 = $"interface ip set address name=\"{adapterName} Tunnel\" static {ip} {mask}";
                var (ok2, output2, err2) = RunProcessWithOutput("netsh", args2);
                _logger.Info($"netsh assign IP (try 2 with ' Tunnel' suffix): ok={ok2}, output='{output2}', err='{err2}'");

                if (ok2)
                {
                    _adapterName = $"{adapterName} Tunnel";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"AssignIpToAdapter: {ex.Message}");
        }
    }

    /// <summary>
    /// Verify that IP was actually assigned.
    /// </summary>
    private void VerifyAdapterIp(string adapterName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"interface ip show config name=\"{adapterName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            _logger.Info($"Adapter IP config:");
            foreach (var line in output.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    _logger.Info($"  {line.TrimEnd()}");
        }
        catch { }
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

            // Cleanup the adapter
            if (!string.IsNullOrEmpty(_adapterName))
            {
                CleanupExistingAdapters(_adapterName);
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
        // Use PowerShell's New-NetRoute which is more reliable than `route add`
        // We route ALL traffic (0.0.0.0/0) through the TUN adapter
        // The gateway doesn't need to exist - we just need to point to the interface

        // Method 1: Use netsh (more compatible)
        var args = $"interface ipv4 add routeprefix=0.0.0.0/0 interface=\"{_adapterName}\" nexthop={settings.TunGateway} metric=5";
        var (ok, output, err) = RunProcessWithOutput("netsh", args);
        _logger.Info($"netsh route: ok={ok}, output='{output}', err='{err}'");

        // If netsh failed, try `route add` as fallback
        if (!ok)
        {
            _logger.Info("netsh route failed, trying route add...");
            var routeArgs = $"add 0.0.0.0 mask 0.0.0.0 {settings.TunIp} metric 5 if {_tunInterfaceIndex}";
            var (ok2, output2, err2) = RunProcessWithOutput("route", routeArgs);
            _logger.Info($"route add: ok={ok2}, output='{output2}', err='{err2}'");
        }

        // Print current routes for verification
        var (printOk, printOut, _) = RunProcessWithOutput("route", "print 0.0.0.0");
        _logger.Info("Current default routes:");
        foreach (var line in printOut.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line))
                _logger.Info($"  {line.TrimEnd()}");

        _routesInstalled = true;
        _logger.Info("Default route installed through TUN");
    }

    private void RemoveRoutes()
    {
        try
        {
            // Remove via netsh
            var args = $"interface ipv4 delete routeprefix=0.0.0.0/0 interface=\"{_adapterName}\"";
            var (ok, output, err) = RunProcessWithOutput("netsh", args);
            _logger.Info($"netsh route delete: ok={ok}, output='{output}', err='{err}'");

            // Also try route delete
            RunProcessWithOutput("route", "delete 0.0.0.0");
            _logger.Info("Routes removed");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Error removing routes: {ex.Message}");
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
}
