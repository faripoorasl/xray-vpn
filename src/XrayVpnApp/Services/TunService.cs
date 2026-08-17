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

            // Try multiple approaches to find the adapter
            _tunInterfaceIndex = FindTunAdapterIndex(settings.TunAdapterName);
            if (_tunInterfaceIndex == 0)
            {
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

            // Get the ACTUAL adapter name from the index (more reliable)
            _adapterName = GetAdapterNameByIndex(_tunInterfaceIndex);
            _logger.Info($"Found TUN adapter: index={_tunInterfaceIndex}, actual name='{_adapterName}'");

            // Step 4: Assign IP address using PowerShell (more reliable than netsh)
            _logger.Info($"=== TUN Service: Step 4/5 - Assigning IP {settings.TunIp} ===");
            AssignIpViaPowerShell(_tunInterfaceIndex, settings.TunIp, "24");

            // Verify IP was assigned
            VerifyAdapterIp(_adapterName);

            // Step 5: Install routes using PowerShell (more reliable than route add)
            _logger.Info("=== TUN Service: Step 5/5 - Installing routes ===");
            InstallRoutesViaPowerShell(_tunInterfaceIndex, settings);

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
    /// Get the actual adapter name from its interface index.
    /// </summary>
    private string GetAdapterNameByIndex(int ifIndex)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"(Get-NetAdapter -InterfaceIndex {ifIndex} -ErrorAction SilentlyContinue).Name\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            return string.IsNullOrEmpty(output) ? $"ifIndex{ifIndex}" : output;
        }
        catch
        {
            return $"ifIndex{ifIndex}";
        }
    }

    /// <summary>
    /// Assign IP address using PowerShell's New-NetIPAddress (more reliable than netsh).
    /// Uses interface INDEX instead of name, so adapter name doesn't matter.
    /// </summary>
    private void AssignIpViaPowerShell(int ifIndex, string ip, string prefixLength)
    {
        try
        {
            // First remove any existing IP addresses on the interface
            var clearArgs = $"-NoProfile -Command \"Get-NetIPAddress -InterfaceIndex {ifIndex} -ErrorAction SilentlyContinue | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue\"";
            var (clearOk, clearOut, clearErr) = RunProcessWithOutput("powershell", clearArgs);
            _logger.Info($"Clear existing IPs: ok={clearOk}");

            // Now assign the new IP using the interface INDEX (not name)
            var args = $"-NoProfile -Command \"New-NetIPAddress -InterfaceIndex {ifIndex} -IPAddress {ip} -PrefixLength {prefixLength} -ErrorAction Stop | Out-Null\"";
            var (ok, output, err) = RunProcessWithOutput("powershell", args);
            _logger.Info($"New-NetIPAddress: ok={ok}, output='{output}', err='{err}'");

            if (!ok)
            {
                // Fallback: try netsh with the actual adapter name
                _logger.Info("PowerShell failed, trying netsh with actual name...");
                var netshArgs = $"interface ip set address name=\"{_adapterName}\" static {ip} 255.255.255.0";
                var (ok2, out2, err2) = RunProcessWithOutput("netsh", netshArgs);
                _logger.Info($"netsh fallback: ok={ok2}, output='{out2}', err='{err2}'");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"AssignIpViaPowerShell: {ex.Message}");
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

    /// <summary>
    /// Install default route using PowerShell's New-NetRoute.
    /// Uses interface INDEX, so adapter name doesn't matter.
    /// </summary>
    private void InstallRoutesViaPowerShell(int ifIndex, Models.AppSettings settings)
    {
        try
        {
            // Use New-NetRoute with the interface index
            // We DON'T specify -NextHop because the gateway doesn't exist
            // This creates a route that sends traffic to the interface directly
            var args = $"-NoProfile -Command \"New-NetRoute -InterfaceIndex {ifIndex} -DestinationPrefix '0.0.0.0/0' -RouteMetric 5 -ErrorAction Stop | Out-Null\"";
            var (ok, output, err) = RunProcessWithOutput("powershell", args);
            _logger.Info($"New-NetRoute: ok={ok}, output='{output}', err='{err}'");

            if (!ok)
            {
                // Fallback: try with NextHop (the gateway IP)
                _logger.Info("Trying with NextHop (gateway)...");
                var args2 = $"-NoProfile -Command \"New-NetRoute -InterfaceIndex {ifIndex} -DestinationPrefix '0.0.0.0/0' -NextHop {settings.TunGateway} -RouteMetric 5 -ErrorAction Stop | Out-Null\"";
                var (ok2, output2, err2) = RunProcessWithOutput("powershell", args2);
                _logger.Info($"New-NetRoute with NextHop: ok={ok2}, output='{output2}', err='{err2}'");

                if (!ok2)
                {
                    // Last fallback: route add command
                    _logger.Info("PowerShell failed, trying route add...");
                    var routeArgs = $"add 0.0.0.0 mask 0.0.0.0 {settings.TunGateway} metric 5 if {ifIndex}";
                    var (ok3, output3, err3) = RunProcessWithOutput("route", routeArgs);
                    _logger.Info($"route add: ok={ok3}, output='{output3}', err='{err3}'");
                }
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
        catch (Exception ex)
        {
            _logger.Error($"InstallRoutesViaPowerShell: {ex.Message}");
        }
    }

    private void RemoveRoutes()
    {
        try
        {
            // Remove via PowerShell using interface index
            if (_tunInterfaceIndex > 0)
            {
                var args = $"-NoProfile -Command \"Get-NetRoute -InterfaceIndex {_tunInterfaceIndex} -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue\"";
                var (ok, output, err) = RunProcessWithOutput("powershell", args);
                _logger.Info($"PowerShell route delete: ok={ok}, output='{output}', err='{err}'");
            }

            // Also try route delete as fallback
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
