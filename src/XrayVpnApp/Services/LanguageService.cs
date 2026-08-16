using System.Windows;

namespace XrayVpnApp.Services;

/// <summary>
/// Switches UI language at runtime by swapping ResourceDictionary.
/// </summary>
public class LanguageService
{
    public const string Fa = "fa";
    public const string En = "en";

    public string Current { get; private set; } = Fa;

    public LanguageService(string initial)
    {
        Current = initial;
        Apply();
    }

    public void Set(string lang)
    {
        Current = lang;
        Apply();
    }

    public void Apply()
    {
        var dictPath = $"Resources/Strings.{Current}.xaml";
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        // Remove existing language dictionary
        for (int i = dictionaries.Count - 1; i >= 0; i--)
        {
            var d = dictionaries[i];
            if (d.Source != null && d.Source.OriginalString.StartsWith("Resources/Strings."))
            {
                dictionaries.RemoveAt(i);
            }
        }

        // Add new one
        var newDict = new ResourceDictionary
        {
            Source = new Uri(dictPath, UriKind.Relative)
        };
        dictionaries.Add(newDict);

        // Set FlowDirection for RTL
        Application.Current.MainWindow?.SetValue(FrameworkElement.FlowDirectionProperty,
            Current == Fa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);
    }
}
