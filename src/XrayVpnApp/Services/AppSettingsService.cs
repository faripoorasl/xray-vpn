using System.IO;
using System.Text.Json;
using XrayVpnApp.Models;

namespace XrayVpnApp.Services;

public class AppSettingsService
{
    private static string SettingsFile => Path.Combine(App.AppDataDir, "settings.json");
    private static string StoreFile => Path.Combine(App.AppDataDir, "servers.json");

    public AppSettings Current { get; private set; } = new();
    public ServerStore Store { get; private set; } = new();

    public AppSettingsService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            if (File.Exists(StoreFile))
            {
                var json = File.ReadAllText(StoreFile);
                Store = JsonSerializer.Deserialize<ServerStore>(json) ?? new ServerStore();
            }
        }
        catch
        {
            Current = new AppSettings();
            Store = new ServerStore();
        }
    }

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }

    public void SaveStore()
    {
        try
        {
            var json = JsonSerializer.Serialize(Store, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(StoreFile, json);
        }
        catch { }
    }
}
