using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels;

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
