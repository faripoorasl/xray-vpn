using System.IO;
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

    public static string AppResourceDir { get; } = Path.Combine(
        AppContext.BaseDirectory, "Resources");

    public static AppSettingsService Settings { get; private set; } = null!;
    public static XrayCoreService XrayCore { get; private set; } = null!;
    public static TunService TunAdapter { get; private set; } = null!;
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
