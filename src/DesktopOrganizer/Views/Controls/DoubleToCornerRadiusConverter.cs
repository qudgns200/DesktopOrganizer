using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DesktopOrganizer.Views.Controls;

/// <summary>Converts a single double value to a uniform WPF CornerRadius.</summary>
[ValueConversion(typeof(double), typeof(CornerRadius))]
public sealed class DoubleToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? new CornerRadius(d) : new CornerRadius(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
