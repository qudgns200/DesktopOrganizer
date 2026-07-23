using System.Windows;
using System.Windows.Input;
// UseWindowsForms=true
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox   = System.Windows.MessageBox;

namespace DesktopOrganizer.Views.Dialogs;

public partial class LayoutNameDialog : Window
{
    public string? ResultName { get; private set; }

    public LayoutNameDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => TryCommit();

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryCommit();
    }

    private void TryCommit()
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Layout 이름을 입력해주세요.", "이름 필요",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        ResultName   = name;
        DialogResult = true;
        Close();
    }
}
