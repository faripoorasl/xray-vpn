using System.Text.Json.Serialization;

namespace XrayVpnApp.Models;

/// <summary>
/// Application-wide settings persisted to JSON.
/// </summary>
public class AppSettings
{
    public string Language { get; set; } = "fa"; // fa or en
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool AutoConnectOnStart { get; set; } = false;
    public string LastServerId { get; set; } = string.Empty;

    // TUN settings
    public string TunIp { get; set; } = "10.10.0.2";
    public string TunGateway { get; set; } = "10.10.0.1";
    public string TunMask { get; set; } = "255.255.255.0";
    public int TunMtu { get; set; } = 1500;
    public string TunAdapterName { get; set; } = "XrayVpn";

    // SOCKS / HTTP inbound ports (for fallback mode)
    public int SocksPort { get; set; } = 10808;
    public int HttpPort { get; set; } = 10809;

    // DNS settings
    public string LocalDns { get; set; } = "223.5.5.5"; // Chinese-friendly default; override per-region
    public string RemoteDns { get; set; } = "8.8.8.8";
    public bool EnableFakeDns { get; set; } = true;
    public bool DnsOverHttps { get; set; } = true;
    public string DohUrl { get; set; } = "https://1.1.1.1/dns-query";

    // Routing
    public bool BypassLan { get; set; } = true;
    public bool BypassIran { get; set; } = true;
    public bool BlockAds { get; set; } = false;
    public bool BlockAdult { get; set; } = false;

    // Xray core
    public int XrayLogLevel { get; set; } = 1; // 0=debug,1=info,2=warning,3=error
    public int XraySniffingTimeout { get; set; } = 300;
    public bool EnableMux { get; set; } = false;
    public int MuxConcurrency { get; set; } = 8;

    // UI
    public string Theme { get; set; } = "dark"; // dark or light
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public double WindowX { get; set; } = double.NaN;
    public double WindowY { get; set; } = double.NaN;
}

public class SubscriptionSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.MinValue;
    public int ServerCount { get; set; } = 0;
    public bool AutoUpdate { get; set; } = false;
    public int UpdateIntervalHours { get; set; } = 24;
}
