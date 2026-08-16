using System.Diagnostics;

namespace XrayVpnApp.Services;

/// <summary>
/// Sets Windows DNS servers (global + per-interface).
/// </summary>
public class DnsService
{
    private readonly Logger _logger;

    public DnsService(Logger logger)
    {
        _logger = logger;
    }

    public bool SetSystemDns(string primaryDns, string? secondaryDns = null, string? interfaceName = "XrayVpn")
    {
        try
        {
            // Set primary DNS on TUN interface
            RunNetsh($"interface ip set dns name=\"{interfaceName}\" static {primaryDns} primary");

            // Set secondary if provided
            if (!string.IsNullOrEmpty(secondaryDns))
            {
                RunNetsh($"interface ip add dns name=\"{interfaceName}\" {secondaryDns} index=2");
            }

            // Flush DNS cache
            FlushDnsCache();
            _logger.Info($"System DNS set: {primaryDns} / {secondaryDns}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"SetSystemDns failed: {ex.Message}");
            return false;
        }
    }

    public bool ResetSystemDns(string? interfaceName = "XrayVpn")
    {
        try
        {
            RunNetsh($"interface ip set dns name=\"{interfaceName}\" source=dhcp");
            FlushDnsCache();
            _logger.Info("System DNS reset to DHCP");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"ResetSystemDns failed: {ex.Message}");
            return false;
        }
    }

    public void FlushDnsCache()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(3000);
        }
        catch { }
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
        catch
        {
            return false;
        }
    }
}
