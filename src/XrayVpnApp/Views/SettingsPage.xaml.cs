using System.Windows;
using System.Windows.Controls;
using XrayVpnApp.ViewModels;

namespace XrayVpnApp.Views;

public partial class SettingsPage : UserControl
{
    private readonly MainViewModel _vm;
    private bool _loaded = false;

    public SettingsPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = App.Settings.Current;

        // General
        SelectComboItem(LangCombo, s.Language);
        StartWithWindows.IsChecked = s.StartWithWindows;
        MinimizeToTray.IsChecked = s.MinimizeToTray;
        CloseToTray.IsChecked = s.CloseToTray;
        AutoConnect.IsChecked = s.AutoConnectOnStart;

        // TUN
        TunName.Text = s.TunAdapterName;
        TunIpBox.Text = s.TunIp;
        TunGwBox.Text = s.TunGateway;
        TunMtuBox.Text = s.TunMtu.ToString();

        // DNS
        RemoteDnsBox.Text = s.RemoteDns;
        LocalDnsBox.Text = s.LocalDns;
        DohUrlBox.Text = s.DohUrl;
        FakeDns.IsChecked = s.EnableFakeDns;
        DohEnabled.IsChecked = s.DnsOverHttps;

        // Routing
        BypassLan.IsChecked = s.BypassLan;
        BypassIran.IsChecked = s.BypassIran;
        BlockAds.IsChecked = s.BlockAds;

        // Xray
        LogLevelCombo.SelectedIndex = s.XrayLogLevel;
        SocksPortBox.Text = s.SocksPort.ToString();
        HttpPortBox.Text = s.HttpPort.ToString();
        MuxEnabled.IsChecked = s.EnableMux;
        MuxConcBox.Text = s.MuxConcurrency.ToString();

        _loaded = true;
    }

    private void SelectComboItem(ComboBox combo, string tag)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (LangCombo.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            App.Language.Set(lang);
            App.Settings.Current.Language = lang;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = App.Settings.Current;

        s.StartWithWindows = StartWithWindows.IsChecked == true;
        s.MinimizeToTray = MinimizeToTray.IsChecked == true;
        s.CloseToTray = CloseToTray.IsChecked == true;
        s.AutoConnectOnStart = AutoConnect.IsChecked == true;

        s.TunAdapterName = TunName.Text;
        s.TunIp = TunIpBox.Text;
        s.TunGateway = TunGwBox.Text;
        if (int.TryParse(TunMtuBox.Text, out var mtu)) s.TunMtu = mtu;

        s.RemoteDns = RemoteDnsBox.Text;
        s.LocalDns = LocalDnsBox.Text;
        s.DohUrl = DohUrlBox.Text;
        s.EnableFakeDns = FakeDns.IsChecked == true;
        s.DnsOverHttps = DohEnabled.IsChecked == true;

        s.BypassLan = BypassLan.IsChecked == true;
        s.BypassIran = BypassIran.IsChecked == true;
        s.BlockAds = BlockAds.IsChecked == true;

        s.XrayLogLevel = LogLevelCombo.SelectedIndex;
        if (int.TryParse(SocksPortBox.Text, out var sp)) s.SocksPort = sp;
        if (int.TryParse(HttpPortBox.Text, out var hp)) s.HttpPort = hp;
        s.EnableMux = MuxEnabled.IsChecked == true;
        if (int.TryParse(MuxConcBox.Text, out var mc)) s.MuxConcurrency = mc;

        // Auto-start registry
        new TrayService(App.Logger).SetAutoStart(s.StartWithWindows);

        App.Settings.SaveSettings();

        MessageBox.Show(
            (string)Application.Current.FindResource("MsgSettingsSaved"),
            "✓", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
