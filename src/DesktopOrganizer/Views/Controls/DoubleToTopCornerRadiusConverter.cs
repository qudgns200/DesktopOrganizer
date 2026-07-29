using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DesktopOrganizer.Views.Controls;

/// <summary>
/// Converts a single double to a CornerRadius rounded on the TOP corners only
/// (top-left, top-right) with square bottom corners — used for the frosted-card header
/// so it hugs the card's rounded top while sitting flush against the body below.
/// </summary>
[ValueConversion(typeof(double), typeof(CornerRadius))]
public sealed class DoubleToTopCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? new CornerRadius(d, d, 0, 0) : new CornerRadius(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
