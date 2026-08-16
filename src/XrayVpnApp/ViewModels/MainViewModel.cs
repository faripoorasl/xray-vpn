using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XrayVpnApp.Models;

namespace XrayVpnApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    #region State

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private bool _isConnecting = false;

    [ObservableProperty]
    private string _statusText = "Disconnected";

    [ObservableProperty]
    private string _statusBackground = "#C50F1F";

    [ObservableProperty]
    private string _statusDotColor = "#FFFFFF";

    [ObservableProperty]
    private string _connectButtonText = "Connect";

    [ObservableProperty]
    private string _connectButtonIcon = "▶";

    [ObservableProperty]
    private string _connectedServerName = "—";

    [ObservableProperty]
    private string _downloadSpeedDisplay = "0.0 Mbps";

    [ObservableProperty]
    private string _uploadSpeedDisplay = "0.0 Mbps";

    [ObservableProperty]
    private string _connectedDurationDisplay = "00:00:00";

    [ObservableProperty]
    private ServerConfig? _selectedServer;

    public bool IsClosing = false;
    public ObservableCollection<ServerConfig> Servers => App.Settings.Store.Servers;
    public ObservableCollection<SubscriptionSource> Subscriptions => 
        new(App.Settings.Store.Subscriptions);

    private ServerConfig? _activeServer;

    #endregion

    #region Connect / Disconnect

    [RelayCommand]
    public async Task<bool> ConnectAsync()
    {
        if (SelectedServer == null)
        {
            MessageBox.Show(App.Language.Current == "fa"
                ? "ابتدا یک سرور انتخاب کنید"
                : "Please select a server first",
                App.Language.Current == "fa" ? "توجه" : "Notice");
            return false;
        }

        IsConnecting = true;
        StatusText = App.Language.Current == "fa" ? "در حال اتصال..." : "Connecting...";

        try
        {
            // 1. Start Xray
            if (!App.XrayCore.Start(SelectedServer, App.Settings.Current, tunMode: 1))
            {
                MessageBox.Show(App.Language.Current == "fa"
                    ? "خطا در راه‌اندازی هسته Xray"
                    : "Failed to start Xray core");
                IsConnecting = false;
                ResetStatus();
                return false;
            }

            await Task.Delay(800);

            // 2. Start TUN adapter
            if (!App.TunAdapter.Start(App.Settings.Current))
            {
                App.XrayCore.Stop();
                MessageBox.Show(App.Language.Current == "fa"
                    ? "خطا در ایجاد اداپتور TUN. مطمئن شوید برنامه به صورت ادمین اجرا شده است."
                    : "Failed to create TUN adapter. Make sure the app is running as Administrator.");
                IsConnecting = false;
                ResetStatus();
                return false;
            }

            // 3. Set DNS
            App.Dns.SetSystemDns(App.Settings.Current.RemoteDns, App.Settings.Current.LocalDns);

            // 4. State
            _activeServer = SelectedServer;
            IsConnected = true;
            IsConnecting = false;
            StatusText = App.Language.Current == "fa" ? "متصل" : "Connected";
            StatusBackground = "#6BB700";
            ConnectButtonText = App.Language.Current == "fa" ? "قطع اتصال" : "Disconnect";
            ConnectButtonIcon = "■";
            ConnectedServerName = SelectedServer.Remark;

            App.Settings.Current.LastServerId = SelectedServer.Id;
            App.Settings.SaveSettings();
            return true;
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Connect failed: {ex.Message}");
            IsConnecting = false;
            ResetStatus();
            return false;
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        IsConnecting = true;
        StatusText = App.Language.Current == "fa" ? "در حال قطع..." : "Disconnecting...";

        await Task.Run(() =>
        {
            try
            {
                App.TunAdapter.Stop();
                App.XrayCore.Stop();
                App.Dns.ResetSystemDns();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Disconnect error: {ex.Message}");
            }
        });

        _activeServer = null;
        IsConnected = false;
        IsConnecting = false;
        ResetStatus();
    }

    private void ResetStatus()
    {
        StatusText = App.Language.Current == "fa" ? "قطع" : "Disconnected";
        StatusBackground = "#C50F1F";
        ConnectButtonText = App.Language.Current == "fa" ? "اتصال" : "Connect";
        ConnectButtonIcon = "▶";
        ConnectedServerName = "—";
        DownloadSpeedDisplay = "0.0 Mbps";
        UploadSpeedDisplay = "0.0 Mbps";
        ConnectedDurationDisplay = "00:00:00";
    }

    #endregion

    #region Server management

    [RelayCommand]
    public void AddServerFromClipboard()
    {
        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show(
                (string)Application.Current.FindResource("MsgEmptyClipboard"),
                (string)Application.Current.FindResource("MsgError"));
            return;
        }

        var parsed = App.ConfigParser.ParseAuto(text);
        if (parsed.Count == 0)
        {
            MessageBox.Show(
                (string)Application.Current.FindResource("MsgConfigAddFailed"),
                (string)Application.Current.FindResource("MsgError"));
            return;
        }

        foreach (var s in parsed)
            Servers.Add(s);

        App.Settings.SaveStore();
        MessageBox.Show(
            (string)Application.Current.FindResource("MsgConfigAdded"),
            "✓",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    public void ImportFromFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var parsed = App.ConfigParser.ParseAuto(content);
            foreach (var s in parsed)
                Servers.Add(s);
            App.Settings.SaveStore();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error");
        }
    }

    [RelayCommand]
    public void DeleteServer(ServerConfig server)
    {
        var msg = (string)Application.Current.FindResource("MsgDeleteConfirm");
        if (MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        Servers.Remove(server);
        App.Settings.SaveStore();
    }

    [RelayCommand]
    public async Task TestLatencyAsync(ServerConfig server)
    {
        server.LastTested = DateTime.Now;
        server.LatencyMs = -1;

        var latency = await App.SpeedTest.TestLatencyAsync(server, App.Settings.Current);
        server.LatencyMs = latency;
    }

    [RelayCommand]
    public async Task TestAllLatencyAsync()
    {
        foreach (var s in Servers.ToList())
        {
            await TestLatencyAsync(s);
        }
    }

    [RelayCommand]
    public async Task TestSpeedAsync(ServerConfig server)
    {
        server.DownloadSpeedMbps = 0;
        var speed = await App.SpeedTest.TestDownloadSpeedAsync(server, App.Settings.Current);
        server.DownloadSpeedMbps = speed;
    }

    [RelayCommand]
    public void CopyServerLink(ServerConfig server)
    {
        if (!string.IsNullOrEmpty(server.OriginalUrl))
            Clipboard.SetText(server.OriginalUrl);
    }

    #endregion
}
