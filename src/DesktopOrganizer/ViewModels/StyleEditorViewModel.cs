using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels.Base;
// UseWindowsForms=true: resolve BorderStyle ambiguity with System.Windows.Forms.BorderStyle
using BorderStyle = DesktopOrganizer.Models.BorderStyle;

namespace DesktopOrganizer.ViewModels;

/// <summary>
/// Working-copy VM for the style editor dialog (F-009).
/// Initialised from a source ContainerStyle; call ToContainerStyle() to get the result.
/// </summary>
public class StyleEditorViewModel : ObservableObject
{
    private string      _backgroundColor;
    private double      _backgroundOpacity;
    private string      _borderColor;
    private double      _borderThickness;
    private BorderStyle _borderStyle;
    private bool        _showTitle;
    private double      _titleFontSize;
    private string      _titleFontColor;
    private double      _cornerRadius;
    private string      _accentColor;

    public StyleEditorViewModel(ContainerStyle source)
    {
        _backgroundColor   = source.BackgroundColor;
        _backgroundOpacity = source.BackgroundOpacity;
        _borderColor       = source.BorderColor;
        _borderThickness   = source.BorderThickness;
        _borderStyle       = source.BorderStyle;
        _showTitle         = source.ShowTitle;
        _titleFontSize     = source.TitleFontSize;
        _titleFontColor    = source.TitleFontColor;
        _cornerRadius      = source.CornerRadius;
        _accentColor       = source.AccentColor;
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set => SetField(ref _backgroundColor, value);
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set => SetField(ref _backgroundOpacity, Math.Clamp(value, 0.0, 1.0));
    }

    public string BorderColor
    {
        get => _borderColor;
        set => SetField(ref _borderColor, value);
    }

    public double BorderThickness
    {
        get => _borderThickness;
        set => SetField(ref _borderThickness, Math.Max(0, value));
    }

    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set => SetField(ref _borderStyle, value);
    }

    public bool ShowTitle
    {
        get => _showTitle;
        set => SetField(ref _showTitle, value);
    }

    public double TitleFontSize
    {
        get => _titleFontSize;
        set => SetField(ref _titleFontSize, Math.Clamp(value, 8.0, 24.0));
    }

    public string TitleFontColor
    {
        get => _titleFontColor;
        set => SetField(ref _titleFontColor, value);
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set => SetField(ref _cornerRadius, Math.Clamp(value, 0.0, 40.0));
    }

    public string AccentColor
    {
        get => _accentColor;
        set => SetField(ref _accentColor, value);
    }

    /// <summary>All available border styles for ComboBox binding.</summary>
    public IReadOnlyList<BorderStyle> BorderStyleOptions { get; } = Enum.GetValues<BorderStyle>();

    /// <summary>
    /// F-009: click-to-pick accent colours. Typing raw ARGB hex is still supported as the
    /// advanced path, but the palette is the primary way to set a colour.
    /// </summary>
    public IReadOnlyList<string> AccentPalette { get; } = new[]
    {
        "#FF4C8DFF", // blue
        "#FF00B0FF", // sky
        "#FF00BFA5", // teal
        "#FF00C853", // green
        "#FFAEEA00", // lime
        "#FFFFC400", // amber
        "#FFFF8A00", // orange
        "#FFFF5252", // red
        "#FFFF4081", // pink
        "#FFAB47BC", // purple
        "#FF7C4DFF", // violet
        "#FF90A4AE"  // slate
    };

    /// <summary>Builds a new ContainerStyle from the current editor state.</summary>
    public ContainerStyle ToContainerStyle() => new()
    {
        BackgroundColor   = _backgroundColor,
        BackgroundOpacity = _backgroundOpacity,
        BorderColor       = _borderColor,
        BorderThickness   = _borderThickness,
        BorderStyle       = _borderStyle,
        ShowTitle         = _showTitle,
        TitleFontSize     = _titleFontSize,
        TitleFontColor    = _titleFontColor,
        CornerRadius      = _cornerRadius,
        AccentColor       = _accentColor
    };
}
