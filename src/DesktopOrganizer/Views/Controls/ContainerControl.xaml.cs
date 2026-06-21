using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopOrganizer.ViewModels;
// UseWindowsForms=true: resolve ambiguities with WinForms types
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using UserControl  = System.Windows.Controls.UserControl;

namespace DesktopOrganizer.Views.Controls;

public partial class ContainerControl : UserControl
{
    private ContainerViewModel? _vm;

    public ContainerControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ── DataContext wiring ───────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as ContainerViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ContainerViewModel.IsEditing) || _vm?.IsEditing != true)
            return;

        // Auto-focus + select-all when edit mode starts
        Dispatcher.BeginInvoke(() =>
        {
            NameEditor.Focus();
            NameEditor.SelectAll();
        });
    }

    // ── Title bar interaction ────────────────────────────────────

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _vm?.BeginRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Name editor keyboard handling ────────────────────────────

    private void OnNameEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter)
        {
            _vm?.CommitRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm?.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnNameEditorLostFocus(object sender, RoutedEventArgs e)
    {
        // Commit on focus loss only when still in edit mode
        // (Enter/Escape already set IsEditing=false before LostFocus fires)
        if (_vm?.IsEditing == true)
            _vm.CommitRenameCommand.Execute(null);
    }
}
