using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
// UseWindowsForms=true: resolve Brush ambiguity with System.Drawing.Brush
using Brush  = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace DesktopOrganizer.Views.Controls;

/// <summary>Converts a hex colour string (e.g. "#44000000") to a WPF Brush.</summary>
[ValueConversion(typeof(string), typeof(Brush))]
public sealed class StringToBrushConverter : IValueConverter
{
    private static readonly BrushConverter Converter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return (Brush)Converter.ConvertFromString(hex)!; }
            catch { }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
