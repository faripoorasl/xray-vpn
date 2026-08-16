using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace XrayVpnApp.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Color.FromRgb(0x6B, 0xB7, 0x00)); // green
        return new SolidColorBrush(Color.FromRgb(0xC5, 0x0F, 0x1F)); // red
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class LatencyToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int ms)
        {
            if (ms < 0) return new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));
            if (ms < 150) return new SolidColorBrush(Color.FromRgb(0x6B, 0xB7, 0x00));
            if (ms < 500) return new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x00));
            return new SolidColorBrush(Color.FromRgb(0xC5, 0x0F, 0x1F));
        }
        return new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
