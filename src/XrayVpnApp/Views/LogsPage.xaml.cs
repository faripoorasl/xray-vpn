using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace XrayVpnApp.Views;

public partial class LogsPage : UserControl
{
    public LogsPage()
    {
        InitializeComponent();
        RefreshLogs();
        LogPathLabel.Text = Path.Combine(App.AppLogDir, $"xrayvpn-{DateTime.Now:yyyy-MM-dd}.log");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshLogs();

    private void RefreshLogs()
    {
        try
        {
            var file = Path.Combine(App.AppLogDir, $"xrayvpn-{DateTime.Now:yyyy-MM-dd}.log");
            if (!File.Exists(file))
            {
                LogBox.Text = "(no logs yet)";
                return;
            }
            var lines = File.ReadAllLines(file);
            // Show last 1000 lines
            var take = Math.Min(1000, lines.Length);
            var content = string.Join("\n", lines.Skip(lines.Length - take));
            LogBox.Text = content;
            LogBox.ScrollToEnd();
        }
        catch (Exception ex)
        {
            LogBox.Text = "Error: " + ex.Message;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var file = Path.Combine(App.AppLogDir, $"xrayvpn-{DateTime.Now:yyyy-MM-dd}.log");
        if (File.Exists(file)) File.WriteAllText(file, "");
        LogBox.Text = "";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = App.AppLogDir,
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
