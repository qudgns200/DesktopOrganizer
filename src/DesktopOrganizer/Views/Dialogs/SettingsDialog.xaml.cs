using System.IO;
using System.Windows;
using System.Windows.Controls;
using DesktopOrganizer.Models;
using DesktopOrganizer.Resources;
using DesktopOrganizer.ViewModels;
// UseWindowsForms=true: resolve ambiguities with WinForms types
using Button     = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace DesktopOrganizer.Views.Dialogs;

/// <summary>F-023: Edits AppSettings (watcher, icon spacing, max containers, log level, excluded paths).</summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsViewModel _vm;
    private readonly AppSettings       _target;

    public SettingsDialog(AppSettings source)
    {
        _target = source;
        _vm     = new SettingsViewModel(source);
        InitializeComponent();
        DataContext = _vm;
    }

    private void OnAddExcludedPathClick(object sender, RoutedEventArgs e)
    {
        var added = _vm.AddExcludedPath();
        if (added is not null && !File.Exists(added) && !Directory.Exists(added))
        {
            MessageBox.Show(
                Strings.Settings_PathNotExistMessage,
                Strings.Settings_PathAddedTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnRemoveExcludedPathClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
            _vm.RemoveExcludedPath(path);
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!_vm.Validate(out var error))
        {
            MessageBox.Show(error, Strings.Settings_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _vm.ApplyTo(_target);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
