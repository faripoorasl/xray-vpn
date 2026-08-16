using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using XrayVpnApp.Utils;

namespace XrayVpnApp.Services;

/// <summary>
/// Creates & manages a wintun TUN adapter and routes all system traffic through it.
/// Uses native wintun.dll + iphlpapi.dll directly (no third-party wrappers).
/// </summary>
public class TunService
{
    private readonly Logger _logger;
    private IntPtr _adapterHandle = IntPtr.Zero;
    private IntPtr _sessionHandle = IntPtr.Zero;
    private uint _tunInterfaceIndex = 0;
    private bool _routesInstalled = false;

    public bool IsRunning => _adapterHandle != IntPtr.Zero;
    public uint InterfaceIndex => _tunInterfaceIndex;

    public TunService(Logger logger)
    {
        _logger = logger;
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
            _logger.Info($"Creating TUN adapter '{settings.TunAdapterName}'...");

            // Generate a stable GUID for the adapter
            var adapterGuid = Guid.NewGuid();

            _adapterHandle = WintunNative.WintunCreateAdapter(
                settings.TunAdapterName,
                "XrayVpn",
                adapterGuid);

            if (_adapterHandle == IntPtr.Zero)
            {
                _logger.Error($"WintunCreateAdapter failed (err={Marshal.GetLastWin32Error()})");
                return false;
            }

            _logger.Info("TUN adapter created, assigning IP...");

            // Assign IP & netmask using netsh (simplest, reliable on Win11)
            if (!RunNetsh($"interface ip set address name=\"{settings.TunAdapterName}\" " +
                          $"static {settings.TunIp} {settings.TunMask} {settings.TunGateway}"))
            {
                _logger.Error("Failed to assign IP to TUN adapter");
                Stop();
                return false;
            }

            // Set MTU
            RunNetsh($"interface ipv4 set subinterface \"{settings.TunAdapterName}\" " +
                     $"mtu={settings.TunMtu} store=persistent");

            // Get the interface index
            _tunInterfaceIndex = GetInterfaceIndex(settings.TunAdapterName);
            if (_tunInterfaceIndex == 0)
            {
                _logger.Error("Could not determine TUN interface index");
                Stop();
                return false;
            }

            _logger.Info($"TUN interface index: {_tunInterfaceIndex}");

            // Start wintun session (for read/write of packets — Xray handles routing via dokodemo-door)
            _sessionHandle = WintunNative.WintunStartSession(_adapterHandle, 0x400000);
            if (_sessionHandle == IntPtr.Zero)
            {
                _logger.Error($"WintunStartSession failed (err={Marshal.GetLastWin32Error()})");
                Stop();
                return false;
            }

            // Install routes: redirect everything through TUN
            InstallRoutes(settings);

            _logger.Info("TUN service started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"TUN start failed: {ex.Message}");
            Stop();
            return false;
        }
    }

    public void Stop()
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

    private void InstallRoutes(Models.AppSettings settings)
    {
        // Add a host route for the proxy server through the original default gateway
        // (so traffic to the proxy itself doesn't get sucked into the TUN)

        // 1. Add default route through TUN with lower metric
        // route add 0.0.0.0 mask 0.0.0.0 <gateway> metric 5 if <index>
        RunProcess("route", $"add 0.0.0.0 mask 0.0.0.0 {settings.TunGateway} " +
                           $"metric 5 if {_tunInterfaceIndex}");

        _routesInstalled = true;
        _logger.Info("Default route installed through TUN");
    }

    private void RemoveRoutes()
    {
        try
        {
            RunProcess("route", "delete 0.0.0.0");
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
            // Use PowerShell to get the ifIndex
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
            p.WaitForExit(3000);
            return uint.TryParse(output, out var idx) ? idx : 0;
        }
        catch
        {
            return 0;
        }
    }

    private bool RunNetsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Verb = "runas",
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"netsh failed: {ex.Message}");
            return false;
        }
    }

    private bool RunProcess(string fileName, string args)
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
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"{fileName} failed: {ex.Message}");
            return false;
        }
    }
}
