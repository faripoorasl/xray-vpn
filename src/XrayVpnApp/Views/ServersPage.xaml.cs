using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using XrayVpnApp.ViewModels;

namespace XrayVpnApp.Views;

public partial class ServersPage : UserControl
{
    private readonly MainViewModel _vm;

    public ServersPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Config files|*.json;*.txt;*.yaml;*.yml|All files|*.*",
            Multiselect = false
        };
        if (dlg.ShowDialog() == true)
        {
            _vm.ImportFromFileCommand.Execute(dlg.FileName);
        }
    }

    private void AddFromPasteBox_Click(object sender, RoutedEventArgs e)
    {
        var text = PasteBox.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { Clipboard.SetText(text); } catch { }
            _vm.AddServerFromClipboardCommand.Execute(null);
            PasteBox.Text = "";
        }
    }

    private void PasteBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            AddFromPasteBox_Click(sender, e);
        }
    }
}
