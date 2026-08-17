using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private string _connectButtonIcon = "\u25B6"; // play symbol

    [ObservableProperty]
    private string _connectedServerName = "\u2014"; // em dash

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
            string msg1 = App.Language.Current == "fa"
                ? "\u0627\u0628\u062A\u062F\u0627 \u06CC\u06A9 \u0633\u0631\u0648\u0631 \u0627\u0646\u062A\u062E\u0627\u0628 \u06A9\u0646\u06CC\u062F"
                : "Please select a server first";
            string title1 = App.Language.Current == "fa" ? "\u062A\u0648\u062C\u0647" : "Notice";
            MessageBox.Show(msg1, title1);
            return false;
        }

        IsConnecting = true;
        string connectingText = App.Language.Current == "fa"
            ? "\u062F\u0631 \u062D\u0627\u0644 \u0627\u062A\u0635\u0627\u0644..."
            : "Connecting...";
        StatusText = connectingText;

        try
        {
            // 1. Start Xray in SOCKS+HTTP proxy mode (no TUN needed)
            //    This is the most reliable mode - works for all browsers and most apps
            if (!App.XrayCore.Start(SelectedServer, App.Settings.Current, tunMode: 0))
            {
                string msg = App.Language.Current == "fa"
                    ? "\u062E\u0637\u0627 \u062F\u0631 \u0631\u0627\u0645\u200C\u0627\u0646\u062F\u0627\u0632\u06CC \u0647\u0633\u062A\u0647 Xray"
                    : "Failed to start Xray core";
                MessageBox.Show(msg);
                IsConnecting = false;
                ResetStatus();
                return false;
            }

            await Task.Delay(1000);

            // 2. Set Windows system proxy to Xray's HTTP inbound (127.0.0.1:httpPort)
            //    This routes all HTTP/HTTPS traffic through Xray
            if (!App.SystemProxy.Enable(App.Settings.Current.HttpPort, App.Settings.Current.SocksPort))
            {
                App.XrayCore.Stop();
                string msg = App.Language.Current == "fa"
                    ? "\u062E\u0637\u0627 \u062F\u0631 \u062A\u0646\u0638\u06CC\u0645 \u067E\u0631\u0648\u06A9\u0633\u06CC \u0633\u06CC\u0633\u062A\u0645"
                    : "Failed to set system proxy";
                MessageBox.Show(msg);
                IsConnecting = false;
                ResetStatus();
                return false;
            }

            await Task.Delay(200);

            // 4. State
            _activeServer = SelectedServer;
            IsConnected = true;
            IsConnecting = false;
            StatusText = App.Language.Current == "fa" ? "\u0645\u062A\u0635\u0644" : "Connected";
            StatusBackground = "#6BB700";
            ConnectButtonText = App.Language.Current == "fa"
                ? "\u0642\u0637\u0639 \u0627\u062A\u0635\u0627\u0644"
                : "Disconnect";
            ConnectButtonIcon = "\u25A0"; // square symbol
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
        StatusText = App.Language.Current == "fa"
            ? "\u062F\u0631 \u062D\u0627\u0644 \u0642\u0637\u0639..."
            : "Disconnecting...";

        await Task.Run(() =>
        {
            try
            {
                // Stop in reverse order: SystemProxy -> Xray
                App.SystemProxy.Disable();
                App.XrayCore.Stop();
                App.TunAdapter.Stop();  // in case TUN was used
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
        StatusText = App.Language.Current == "fa" ? "\u0642\u0637\u0639" : "Disconnected";
        StatusBackground = "#C50F1F";
        ConnectButtonText = App.Language.Current == "fa" ? "\u0627\u062A\u0635\u0627\u0644" : "Connect";
        ConnectButtonIcon = "\u25B6";
        ConnectedServerName = "\u2014";
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
            "\u2713",
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
