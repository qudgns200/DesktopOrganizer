using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesktopOrganizer.Models;
// UseWindowsForms=true: resolve BorderStyle ambiguity with System.Windows.Forms.BorderStyle
using BorderStyle = DesktopOrganizer.Models.BorderStyle;

namespace DesktopOrganizer.Views.Controls;

/// <summary>
/// Maps <see cref="BorderStyle"/> to a Shape.StrokeDashArray so the container's border can
/// finally render Dashed/Dotted (F-009) — a plain WPF Border only ever draws solid.
/// Solid → null (an unbroken stroke); Dashed → 4-on/2-off; Dotted → 1-on/2-off.
/// Values are in stroke-thickness units (WPF scales the dash pattern by StrokeThickness).
/// </summary>
[ValueConversion(typeof(BorderStyle), typeof(DoubleCollection))]
public sealed class BorderStyleToDashArrayConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is BorderStyle style ? style switch
        {
            BorderStyle.Dashed => new DoubleCollection(new[] { 4.0, 2.0 }),
            BorderStyle.Dotted => new DoubleCollection(new[] { 1.0, 2.0 }),
            _                  => null   // Solid
        } : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
