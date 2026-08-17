using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using XrayVpnApp.Utils;

namespace XrayVpnApp.Services;

/// <summary>
/// Creates &amp; manages a wintun TUN adapter and routes all system traffic through it.
/// Uses native wintun.dll + iphlpapi.dll directly (no third-party wrappers).
/// </summary>
public class TunService
{
    private readonly Logger _logger;
    private IntPtr _adapterHandle = IntPtr.Zero;
    private IntPtr _sessionHandle = IntPtr.Zero;
    private uint _tunInterfaceIndex = 0;
    private bool _routesInstalled = false;
    private bool _wintunLoaded = false;

    public bool IsRunning => _adapterHandle != IntPtr.Zero;
    public uint InterfaceIndex => _tunInterfaceIndex;

    public TunService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Find and load wintun.dll from the Resources folder (or next to EXE).
    /// In single-file publish mode, the standard DLL search path doesn't find it.
    /// </summary>
    private bool EnsureWintunLoaded()
    {
        if (_wintunLoaded) return true;

        try
        {
            // Try multiple paths for wintun.dll
            var candidates = new System.Collections.Generic.List<string>();

            // 1. From AppResourceDir (where xray.exe also lives)
            var resDir = App.AppResourceDir;
            candidates.Add(Path.Combine(resDir, "wintun.dll"));

            // 2. Next to EXE
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

            // 3. Base directory
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "wintun.dll"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Resources", "wintun.dll"));

            _logger.Info("Searching for wintun.dll in:");
            foreach (var c in candidates)
            {
                var exists = File.Exists(c);
                _logger.Info($"  {(exists ? "[FOUND]" : "[miss]")} {c}");
            }

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    _logger.Info($"Loading wintun.dll from: {path}");
                    // Set DLL directory to the folder containing wintun.dll
                    // This helps the P/Invoke find it
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        // Use SetDllDirectory to add to search path
                        SetDllDirectory(dir);
                        _logger.Info($"SetDllDirectory: {dir}");
                    }

                    // Pre-load the DLL so subsequent P/Invoke calls find it
                    var hModule = LoadLibrary(path);
                    if (hModule != IntPtr.Zero)
                    {
                        _logger.Info($"wintun.dll loaded successfully (handle=0x{hModule.ToInt64():X})");
                        _wintunLoaded = true;
                        return true;
                    }
                    else
                    {
                        var err = Marshal.GetLastWin32Error();
                        _logger.Error($"LoadLibrary failed for {path} (Win32 error {err})");
                        // Continue trying other paths
                    }
                }
            }

            _logger.Error("wintun.dll not found in any candidate location!");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"EnsureWintunLoaded exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Create the wintun adapter, assign IP, set gateway, install default route.
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
            // Step 1: Ensure wintun.dll is loaded
            _logger.Info("=== TUN Service: Step 1/4 - Loading wintun.dll ===");
            if (!EnsureWintunLoaded())
            {
                _logger.Error("Cannot start TUN: wintun.dll could not be loaded");
                return false;
            }

            // Step 2: Create the adapter
            _logger.Info($"=== TUN Service: Step 2/4 - Creating adapter '{settings.TunAdapterName}' ===");
            var adapterGuid = Guid.NewGuid();
            _logger.Info($"Adapter GUID: {adapterGuid}");

            _adapterHandle = WintunNative.WintunCreateAdapter(
                settings.TunAdapterName,
                "XrayVpn",
                adapterGuid);

            if (_adapterHandle == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                _logger.Error($"WintunCreateAdapter returned NULL (Win32 error {err})");
                _logger.Error($"This usually means:");
                _logger.Error($"  - wintun.dll failed to load (check file exists next to XrayVpn.exe)");
                _logger.Error($"  - The driver installation was blocked by antivirus");
                _logger.Error($"  - The app is not running as Administrator");
                _logger.Error($"  - Architecture mismatch (x64 EXE needs x64 wintun.dll)");
                return false;
            }

            _logger.Info($"TUN adapter created successfully (handle=0x{_adapterHandle.ToInt64():X})");

            // Step 3: Assign IP via netsh
            _logger.Info($"=== TUN Service: Step 3/4 - Assigning IP {settings.TunIp} ===");
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
            var mtuArgs = $"interface ipv4 set subinterface \"{settings.TunAdapterName}\" " +
                         $"mtu={settings.TunMtu} store=persistent";
            RunProcessWithOutput("netsh", mtuArgs);

            // Get interface index
            _tunInterfaceIndex = GetInterfaceIndex(settings.TunAdapterName);
            if (_tunInterfaceIndex == 0)
            {
                _logger.Error("Could not determine TUN interface index");
                Stop();
                return false;
            }
            _logger.Info($"TUN interface index: {_tunInterfaceIndex}");

            // Step 4: Start wintun session + install routes
            _logger.Info("=== TUN Service: Step 4/4 - Starting session & installing routes ===");
            _sessionHandle = WintunNative.WintunStartSession(_adapterHandle, 0x400000);
            if (_sessionHandle == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                _logger.Error($"WintunStartSession failed (Win32 error {err})");
                Stop();
                return false;
            }
            _logger.Info($"Wintun session started (handle=0x{_sessionHandle.ToInt64():X})");

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

            if (_sessionHandle != IntPtr.Zero)
            {
                WintunNative.WintunEndSession(_sessionHandle);
                _sessionHandle = IntPtr.Zero;
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
        var args = $"add 0.0.0.0 mask 0.0.0.0 {settings.TunGateway} " +
                   $"metric 5 if {_tunInterfaceIndex}";
        var (ok, output, err) = RunProcessWithOutput("route", args);
        _logger.Info($"route {args}: ok={ok}, output={output}, err={err}");

        _routesInstalled = true;
        _logger.Info("Default route installed through TUN");
    }

    private void RemoveRoutes()
    {
        try
        {
            var (ok, output, err) = RunProcessWithOutput("route", "delete 0.0.0.0");
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
