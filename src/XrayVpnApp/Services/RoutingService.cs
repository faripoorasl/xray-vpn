using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using XrayVpnApp.Utils;

namespace XrayVpnApp.Services;

/// <summary>
/// Manages Windows routing table & system DNS settings.
/// </summary>
public class RoutingService
{
    private readonly Logger _logger;
    private uint _originalDnsIfIndex = 0;
    private string? _savedDns;

    public RoutingService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Set the system DNS for the TUN interface to point to the local Xray DNS.
    /// </summary>
    public bool SetDns(uint tunIfIndex, string dns)
    {
        try
        {
            // netsh interface ip set dns name="XrayVpn" static 1.1.1.1 primary
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"interface ip set dns name=\"XrayVpn\" static {dns} primary",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                Verb = "runas",
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(5000);

            // Flush DNS cache
            IpHelperNative.DnsFlushResolverCache();

            _logger.Info($"DNS set to {dns} on TUN interface");
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"SetDns failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restore routes (called on shutdown to clean up).
    /// </summary>
    public void RestoreRoutes()
    {
        try
        {
            // Remove the TUN default route
            var psi = new ProcessStartInfo
            {
                FileName = "route",
                Arguments = "delete 0.0.0.0",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(3000);
            _logger.Info("Routes restored");
        }
        catch { }
    }

    public void RestoreDns()
    {
        try
        {
            // Restore to DHCP
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface ip set dns name=\"XrayVpn\" source=dhcp",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(3000);
            IpHelperNative.DnsFlushResolverCache();
            _logger.Info("DNS restored");
        }
        catch { }
    }
}
