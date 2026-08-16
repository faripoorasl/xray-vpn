using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using XrayVpnApp.Services;
using XrayVpnApp.ViewModels;

namespace XrayVpnApp.Views;

public partial class MainWindow : Window
{
    private MainViewModel _vm = null!;
    private TrayService _tray = null!;
    private System.Windows.Threading.DispatcherTimer? _statsTimer;
    private DateTime _connectedSince = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        // Init tray
        _tray = new TrayService(App.Logger);
        try
        {
            var icon = System.Drawing.SystemIcons.Shield;
            _tray.Initialize(icon,
                onShowClick: () => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; }),
                onQuitClick: () => Dispatcher.Invoke(() => { _tray.Dispose(); Application.Current.Shutdown(); }),
                onConnectClick: () => Dispatcher.InvokeAsync(async () => await ToggleConnect()));
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Tray init failed: {ex.Message}");
        }

        // Load pages into frames
        ServersFrame.Content = new ServersPage(_vm);
        SubscriptionsFrame.Content = new SubscriptionsPage(_vm);
        SpeedTestFrame.Content = new SpeedTestPage(_vm);
        SettingsFrame.Content = new SettingsPage(_vm);
        LogsFrame.Content = new LogsPage();
        AboutFrame.Content = new AboutPage();

        // Apply language
        App.Language.Apply();

        // Restore window position
        var s = App.Settings.Current;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        if (!double.IsNaN(s.WindowX) && !double.IsNaN(s.WindowY))
        {
            Left = s.WindowX;
            Top = s.WindowY;
        }

        // Auto-connect
        if (s.AutoConnectOnStart && !string.IsNullOrEmpty(s.LastServerId))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                await Dispatcher.InvokeAsync(async () => await ToggleConnect());
            });
        }

        // Start stats timer
        _statsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += OnStatsTick;
        _statsTimer.Start();
    }

    private void OnStatsTick(object? sender, EventArgs e)
    {
        if (_vm.IsConnected && _connectedSince != DateTime.MinValue)
        {
            var dur = DateTime.Now - _connectedSince;
            _vm.ConnectedDurationDisplay = $"{dur.Hours:D2}:{dur.Minutes:D2}:{dur.Seconds:D2}";
        }
    }

    private async Task ToggleConnect()
    {
        if (_vm.IsConnected)
        {
            await _vm.DisconnectAsync();
            _connectedSince = DateTime.MinValue;
        }
        else
        {
            if (await _vm.ConnectAsync())
            {
                _connectedSince = DateTime.Now;
            }
        }
    }

    private async void ConnectBtn_OnClick(object sender, RoutedEventArgs e)
    {
        await ToggleConnect();
    }

    private void MainWindow_OnStateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && App.Settings.Current.MinimizeToTray)
        {
            Hide();
        }
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (App.Settings.Current.CloseToTray && !_vm.IsClosing)
        {
            e.Cancel = true;
            Hide();
            _tray?.ShowBalloon("Xray VPN",
                App.Language.Current == "fa"
                    ? "برنامه در حال اجراست. برای خروج کامل روی آیکون کلیک راست و Exit را بزنید."
                    : "App is still running. Right-click tray icon and Exit to fully quit.",
                3000);
            return;
        }

        // Save window state
        var s = App.Settings.Current;
        s.WindowWidth = Width;
        s.WindowHeight = Height;
        s.WindowX = Left;
        s.WindowY = Top;
        App.Settings.SaveSettings();
    }
}
