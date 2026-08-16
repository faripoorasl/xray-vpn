using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XrayVpnApp.Models;

/// <summary>
/// Represents a single V2Ray/Xray server configuration.
/// </summary>
public class ServerConfig : ObservableObject
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _remark = string.Empty;
    private string _protocol = "vless";
    private string _address = string.Empty;
    private int _port = 443;
    private string _uuid = string.Empty;
    private string _alterId = "0";
    private string _network = "tcp";
    private string _security = "tls";
    private string _sni = string.Empty;
    private string _host = string.Empty;
    private string _path = string.Empty;
    private string _flow = string.Empty;
    private string _encryption = "none";
    private string _type = "none";
    private bool _tlsEnabled = true;
    private bool _realityEnabled = false;
    private string _publicKey = string.Empty;
    private string _shortId = string.Empty;
    private string _spiderX = string.Empty;
    private string _fingerprint = "chrome";
    private string _serverName = string.Empty;
    private bool _allowInsecure = false;
    private string _subscriptionId = string.Empty;
    private DateTime _lastTested = DateTime.MinValue;
    private int _latencyMs = -1;
    private double _downloadSpeedMbps = 0;
    private string _originalUrl = string.Empty;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    public string Protocol
    {
        get => _protocol;
        set => SetProperty(ref _protocol, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Uuid
    {
        get => _uuid;
        set => SetProperty(ref _uuid, value);
    }

    public string AlterId
    {
        get => _alterId;
        set => SetProperty(ref _alterId, value);
    }

    public string Network
    {
        get => _network;
        set => SetProperty(ref _network, value);
    }

    public string Security
    {
        get => _security;
        set => SetProperty(ref _security, value);
    }

    public string Sni
    {
        get => _sni;
        set => SetProperty(ref _sni, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public string Flow
    {
        get => _flow;
        set => SetProperty(ref _flow, value);
    }

    public string Encryption
    {
        get => _encryption;
        set => SetProperty(ref _encryption, value);
    }

    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public bool TlsEnabled
    {
        get => _tlsEnabled;
        set => SetProperty(ref _tlsEnabled, value);
    }

    public bool RealityEnabled
    {
        get => _realityEnabled;
        set => SetProperty(ref _realityEnabled, value);
    }

    public string PublicKey
    {
        get => _publicKey;
        set => SetProperty(ref _publicKey, value);
    }

    public string ShortId
    {
        get => _shortId;
        set => SetProperty(ref _shortId, value);
    }

    public string SpiderX
    {
        get => _spiderX;
        set => SetProperty(ref _spiderX, value);
    }

    public string Fingerprint
    {
        get => _fingerprint;
        set => SetProperty(ref _fingerprint, value);
    }

    public string ServerName
    {
        get => _serverName;
        set => SetProperty(ref _serverName, value);
    }

    public bool AllowInsecure
    {
        get => _allowInsecure;
        set => SetProperty(ref _allowInsecure, value);
    }

    public string SubscriptionId
    {
        get => _subscriptionId;
        set => SetProperty(ref _subscriptionId, value);
    }

    public DateTime LastTested
    {
        get => _lastTested;
        set => SetProperty(ref _lastTested, value);
    }

    public int LatencyMs
    {
        get => _latencyMs;
        set => SetProperty(ref _latencyMs, value);
    }

    public double DownloadSpeedMbps
    {
        get => _downloadSpeedMbps;
        set => SetProperty(ref _downloadSpeedMbps, value);
    }

    [JsonIgnore]
    public string OriginalUrl
    {
        get => _originalUrl;
        set => SetProperty(ref _originalUrl, value);
    }

    [JsonIgnore]
    public string DisplayLatency =>
        LatencyMs < 0 ? "—" :
        LatencyMs < 100 ? $"{LatencyMs}ms" :
        LatencyMs < 500 ? $"{LatencyMs}ms" :
        $"{LatencyMs}ms";

    [JsonIgnore]
    public string DisplaySpeed =>
        DownloadSpeedMbps <= 0 ? "—" : $"{DownloadSpeedMbps:F2} Mbps";

    [JsonIgnore]
    public string DisplayAddress => $"{Address}:{Port}";

    public ServerConfig Clone()
    {
        return new ServerConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Remark = Remark,
            Protocol = Protocol,
            Address = Address,
            Port = Port,
            Uuid = Uuid,
            AlterId = AlterId,
            Network = Network,
            Security = Security,
            Sni = Sni,
            Host = Host,
            Path = Path,
            Flow = Flow,
            Encryption = Encryption,
            Type = Type,
            TlsEnabled = TlsEnabled,
            RealityEnabled = RealityEnabled,
            PublicKey = PublicKey,
            ShortId = ShortId,
            SpiderX = SpiderX,
            Fingerprint = Fingerprint,
            ServerName = ServerName,
            AllowInsecure = AllowInsecure,
            OriginalUrl = OriginalUrl,
        };
    }
}
