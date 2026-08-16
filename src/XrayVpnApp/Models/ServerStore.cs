using System.Collections.ObjectModel;

namespace XrayVpnApp.Models;

/// <summary>
/// In-memory store of all servers & subscriptions.
/// Persisted as a single JSON file.
/// </summary>
public class ServerStore
{
    public ObservableCollection<ServerConfig> Servers { get; set; } = new();
    public List<SubscriptionSource> Subscriptions { get; set; } = new();
}
