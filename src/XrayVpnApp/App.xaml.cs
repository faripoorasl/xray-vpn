using System.IO;
using System.Linq;
using System.Windows;
using XrayVpnApp.Services;

namespace XrayVpnApp;

public partial class App : Application
{
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XrayVpn");

    public static string AppConfigDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XrayVpn", "configs");

    public static string AppLogDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XrayVpn", "logs");

    public static string AppResourceDir { get; } = GetResourceDir();

    /// <summary>
    /// In single-file publish mode, AppContext.BaseDirectory points to the
    /// extraction temp folder, not the actual EXE folder. We need to use
    /// the EXE's real directory to find xray.exe and wintun.dll.
    /// Also checks both with and without 'Resources' subfolder.
    /// </summary>
    private static string GetResourceDir()
    {
        // Try multiple sources in order of preference
        var candidates = new List<string>();

        // 1. EXE directory (most reliable for single-file publish)
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var exeDir = Path.GetDirectoryName(exePath)!;
                candidates.Add(Path.Combine(exeDir, "Resources"));
                candidates.Add(exeDir); // fallback: deps next to EXE
            }
        }
        catch { }

        // 2. AppDomain base directory
        try
        {
            var adDir = AppDomain.CurrentDomain.BaseDirectory;
            candidates.Add(Path.Combine(adDir, "Resources"));
            candidates.Add(adDir);
        }
        catch { }

        // 3. AppContext.BaseDirectory (works for normal builds)
        var acDir = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(acDir, "Resources"));
        candidates.Add(acDir);

        // Return the first one that contains xray.exe
        foreach (var c in candidates.Where(Directory.Exists).Distinct())
        {
            if (File.Exists(Path.Combine(c, "xray.exe")))
            {
                return c;
            }
        }

        // Fallback: return the first candidate
        return candidates.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "Resources");
    }

    public static AppSettingsService Settings { get; private set; } = null!;
    public static XrayCoreService XrayCore { get; private set; } = null!;
    public static TunService TunAdapter { get; private set; } = null!;
    public static SystemProxyService SystemProxy { get; private set; } = null!;
    public static RoutingService Routing { get; private set; } = null!;
    public static DnsService Dns { get; private set; } = null!;
    public static ConfigParserService ConfigParser { get; private set; } = null!;
    public static SubscriptionService Subscription { get; private set; } = null!;
    public static SpeedTestService SpeedTest { get; private set; } = null!;
    public static LanguageService Language { get; private set; } = null!;
    public static Logger Logger { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(AppConfigDir);
        Directory.CreateDirectory(AppLogDir);

        Logger = new Logger(AppLogDir);
        Logger.Info("=== Xray VPN starting ===");

        Settings = new AppSettingsService();
        Language = new LanguageService(Settings.Current.Language);
        ConfigParser = new ConfigParserService(Logger);
        XrayCore = new XrayCoreService(Logger);
        TunAdapter = new TunService(Logger);
        SystemProxy = new SystemProxyService(Logger);
        Routing = new RoutingService(Logger);
        Dns = new DnsService(Logger);
        Subscription = new SubscriptionService(ConfigParser, Logger);
        SpeedTest = new SpeedTestService(Logger);

        Logger.Info($"Working dir: {AppDataDir}");
        Logger.Info($"Language: {Settings.Current.Language}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            XrayCore?.Stop();
            TunAdapter?.Stop();
            SystemProxy?.Disable();
            Routing?.RestoreRoutes();
            Dns?.RestoreDns();
            Logger?.Info("=== Xray VPN exited ===");
        }
        catch (Exception ex)
        {
            Logger?.Error($"Error during shutdown: {ex.Message}");
        }
        base.OnExit(e);
    }
}
