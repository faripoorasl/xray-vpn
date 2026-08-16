using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace XrayVpnApp.Views;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        LoadXrayVersion();
    }

    private void LoadXrayVersion()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(App.AppResourceDir, "xray.exe"),
                Arguments = "version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = App.AppResourceDir,
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            // First line: "Xray 1.8.x (Xray, Penetrates Everything.) Custom (go1.21.4 windows/amd64)"
            var firstLine = output.Split('\n')[0].Trim();
            XrayVersionLabel.Text = firstLine;
        }
        catch (Exception ex)
        {
            XrayVersionLabel.Text = $"(xray.exe not found: {ex.Message})";
        }
    }

    private void GitHub_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
