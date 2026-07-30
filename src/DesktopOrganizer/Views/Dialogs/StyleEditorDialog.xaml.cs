using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels;
// UseWindowsForms=true: resolve Button ambiguity with System.Windows.Forms.Button
using Button = System.Windows.Controls.Button;

namespace DesktopOrganizer.Views.Dialogs;

public partial class StyleEditorDialog : Window
{
    private readonly StyleEditorViewModel _vm;

    /// <summary>The style built from the editor state when OK was clicked.</summary>
    public ContainerStyle ResultStyle { get; private set; } = new();

    public StyleEditorDialog(ContainerStyle source)
    {
        _vm = new StyleEditorViewModel(source);
        InitializeComponent();
        DataContext = _vm;
    }

    /// <summary>F-009: palette swatch click sets the accent colour (live preview updates).</summary>
    private void OnAccentSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
            _vm.AccentColor = hex;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        ResultStyle  = _vm.ToContainerStyle();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
