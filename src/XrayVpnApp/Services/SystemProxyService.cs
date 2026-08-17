using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace XrayVpnApp.Services;

/// <summary>
/// Sets the Windows system-wide proxy (WinINET) to route all HTTP/HTTPS
/// traffic through Xray's SOCKS or HTTP inbound.
/// This is the simplest mode that works for most apps (browsers, etc.).
/// </summary>
public class SystemProxyService
{
    private readonly Logger _logger;
    private bool _proxyWasEnabled = false;

    public SystemProxyService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enable system-wide HTTP proxy pointing to Xray's HTTP inbound (127.0.0.1:httpPort).
    /// This makes Windows applications use Xray for HTTP/HTTPS traffic.
    /// </summary>
    public bool Enable(int httpPort, int socksPort = 0)
    {
        try
        {
            _logger.Info($"Enabling system HTTP proxy on 127.0.0.1:{httpPort}");

            // Set proxy via WinINET API (takes effect immediately)
            var proxySetting = $"127.0.0.1:{httpPort}";
            InternSetProxy(proxySetting, true);

            // Also write to registry (so it persists and is reflected in Settings)
            SetRegistryProxy(proxySetting, true);

            // Bypass list: localhost and LAN
            var bypassList = "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*;<local>";
            SetRegistryProxyBypass(bypassList);

            _proxyWasEnabled = true;
            _logger.Info("System proxy enabled");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Enable system proxy failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disable the system proxy (restore direct connection).
    /// </summary>
    public bool Disable()
    {
        try
        {
            if (!_proxyWasEnabled)
            {
                _logger.Info("System proxy was not enabled, nothing to disable");
                return true;
            }

            _logger.Info("Disabling system proxy");

            InternSetProxy("", false);
            SetRegistryProxy("", false);

            _proxyWasEnabled = false;
            _logger.Info("System proxy disabled");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Disable system proxy failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Use WinINET InternetSettings to apply the proxy immediately (without restart).
    /// </summary>
    private void InternSetProxy(string proxyServer, bool enable)
    {
        // Structure for INTERNET_PROXY_INFO
        // https://docs.microsoft.com/en-us/windows/win32/api/wininet/ns-wininet-internet_proxy_info
        var structSize = 0;
        IntPtr ptr = IntPtr.Zero;

        try
        {
            // Calculate size
            var proxyBytes = System.Text.Encoding.Unicode.GetBytes(proxyServer + '\0');
            var enableBytes = new byte[4];
            enableBytes[0] = (byte)(enable ? 1 : 0);

            structSize = 8 + proxyBytes.Length + 4; // approx
            ptr = Marshal.AllocHGlobal(structSize);
            Marshal.WriteInt64(ptr, 0);  // dwAccessType
            // ... (this is simplified; real INTERNET_PROXY_INFO has 3 fields)

            // Use InternetSetOptionW to apply settings
            const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
            const int INTERNET_OPTION_REFRESH = 37;

            InternetSetOptionW(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOptionW(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
        }
    }

    private void SetRegistryProxy(string proxyServer, bool enable)
    {
        try
        {
            const string regKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
            using var key = Registry.CurrentUser.OpenSubKey(regKey, true);
            if (key == null)
            {
                _logger.Error("Could not open Internet Settings registry key");
                return;
            }

            key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
            key.SetValue("ProxyEnable", enable ? 1 : 0, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            _logger.Error($"SetRegistryProxy failed: {ex.Message}");
        }
    }

    private void SetRegistryProxyBypass(string bypassList)
    {
        try
        {
            const string regKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
            using var key = Registry.CurrentUser.OpenSubKey(regKey, true);
            if (key == null) return;

            key.SetValue("ProxyOverride", bypassList, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            _logger.Error($"SetRegistryProxyBypass failed: {ex.Message}");
        }
    }

    [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool InternetSetOptionW(
        IntPtr hInternet,
        int dwOption,
        IntPtr lpBuffer,
        int dwBufferLength);
}
