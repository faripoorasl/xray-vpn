using System.Windows;
using System.Windows.Controls;
using XrayVpnApp.ViewModels;

namespace XrayVpnApp.Views;

public partial class SpeedTestPage : UserControl
{
    private readonly MainViewModel _vm;

    public SpeedTestPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void TestLatency_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerCombo.SelectedItem as Models.ServerConfig;
        if (server == null)
        {
            StatusMsg.Text = App.Language.Current == "fa" ? "ابتدا یک سرور انتخاب کنید" : "Select a server first";
            return;
        }

        TestProgress.IsIndeterminate = true;
        StatusMsg.Text = App.Language.Current == "fa" ? "در حال تست پینگ..." : "Testing latency...";
        LatencyLabel.Text = "—";

        var latency = await App.SpeedTest.TestLatencyAsync(server, App.Settings.Current);
        LatencyLabel.Text = latency < 0 ? "Timeout" : $"{latency} ms";

        TestProgress.IsIndeterminate = false;
        StatusMsg.Text = App.Language.Current == "fa" ? "تکمیل شد" : "Done";
    }

    private async void TestDownload_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerCombo.SelectedItem as Models.ServerConfig;
        if (server == null)
        {
            StatusMsg.Text = App.Language.Current == "fa" ? "ابتدا یک سرور انتخاب کنید" : "Select a server first";
            return;
        }

        TestProgress.IsIndeterminate = true;
        StatusMsg.Text = App.Language.Current == "fa" ? "در حال تست دانلود..." : "Testing download speed...";
        SpeedLabel.Text = "—";

        var speed = await App.SpeedTest.TestDownloadSpeedAsync(server, App.Settings.Current);
        SpeedLabel.Text = speed <= 0 ? "Failed" : $"{speed:F2} Mbps";

        TestProgress.IsIndeterminate = false;
        StatusMsg.Text = App.Language.Current == "fa" ? "تکمیل شد" : "Done";
    }

    private async void TestActive_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsConnected)
        {
            StatusMsg.Text = App.Language.Current == "fa" ? "ابتدا به VPN متصل شوید" : "Connect to VPN first";
            return;
        }

        TestProgress.IsIndeterminate = true;
        StatusMsg.Text = App.Language.Current == "fa" ? "در حال تست سرعت فعلی..." : "Testing active speed...";
        SpeedLabel.Text = "—";

        var speed = await App.SpeedTest.TestActiveSpeedAsync();
        SpeedLabel.Text = speed <= 0 ? "Failed" : $"{speed:F2} Mbps";

        TestProgress.IsIndeterminate = false;
        StatusMsg.Text = App.Language.Current == "fa" ? "تکمیل شد" : "Done";
    }
}
