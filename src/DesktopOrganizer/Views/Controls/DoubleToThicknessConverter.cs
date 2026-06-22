using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DesktopOrganizer.Views.Controls;

/// <summary>Converts a single double value to a uniform WPF Thickness.</summary>
[ValueConversion(typeof(double), typeof(Thickness))]
public sealed class DoubleToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? new Thickness(d) : new Thickness(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
