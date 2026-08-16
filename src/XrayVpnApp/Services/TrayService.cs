using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace XrayVpnApp.Services;

/// <summary>
/// Manages system tray icon and Windows startup registration.
/// </summary>
public class TrayService : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private readonly Logger _logger;

    public TrayService(Logger logger)
    {
        _logger = logger;
    }

    public void Initialize(System.Drawing.Icon icon,
        Action onShowClick, Action onQuitClick, Action onConnectClick)
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Visible = true,
            Text = "Xray VPN"
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show / نمایش", null, (_, _) => onShowClick());
        menu.Items.Add("Connect / اتصال", null, (_, _) => onConnectClick());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit / خروج", null, (_, _) => onQuitClick());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => onShowClick();
    }

    public void UpdateStatus(string text, bool connected)
    {
        if (_notifyIcon == null) return;
        _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        _notifyIcon.Visible = true;
    }

    public void ShowBalloon(string title, string body, int timeoutMs = 2000)
    {
        if (_notifyIcon == null) return;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = body;
        _notifyIcon.ShowBalloonTip(timeoutMs);
    }

    /// <summary>
    /// Register the app to auto-start with Windows.
    /// </summary>
    public bool SetAutoStart(bool enabled)
    {
        try
        {
            const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key == null) return false;

            const string valueName = "XrayVpn";
            if (enabled)
            {
                var exePath = Process.GetCurrentProcess().MainModule!.FileName!;
                key.SetValue(valueName, $"\"{exePath}\" --minimized");
                _logger.Info("Auto-start enabled");
            }
            else
            {
                key.DeleteValue(valueName, false);
                _logger.Info("Auto-start disabled");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"SetAutoStart failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
