using System.IO;

namespace XrayVpnApp.Services;

/// <summary>
/// Simple file+console logger. Thread-safe.
/// </summary>
public class Logger
{
    private readonly string _logDir;
    private readonly object _lock = new();
    private string CurrentLogFile =>
        Path.Combine(_logDir, $"xrayvpn-{DateTime.Now:yyyy-MM-dd}.log");

    public Logger(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(_logDir);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);
    public void Debug(string message) => Write("DEBUG", message);

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        lock (_lock)
        {
            try
            {
                File.AppendAllText(CurrentLogFile, line + Environment.NewLine);
            }
            catch { /* ignore */ }
        }
#if DEBUG
        System.Diagnostics.Debug.WriteLine(line);
#endif
    }
}
