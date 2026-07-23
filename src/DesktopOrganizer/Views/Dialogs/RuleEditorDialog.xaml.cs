using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels;

// UseWindowsForms=true: resolve type ambiguities
using MessageBox = System.Windows.MessageBox;

namespace DesktopOrganizer.Views.Dialogs;

public partial class RuleEditorDialog : Window
{
    private readonly RuleEditorViewModel _vm;

    public string DialogTitle { get; }

    /// <summary>Opens in "create" mode when <paramref name="existing"/> is null.</summary>
    public RuleEditorDialog(IReadOnlyList<Container> containers, Rule? existing = null)
    {
        DialogTitle  = existing is null ? "Rule 생성" : "Rule 수정";
        Title        = DialogTitle;
        _vm          = new RuleEditorViewModel(containers, existing);
        InitializeComponent();
        DataContext = _vm;
    }

    // ── F-012 / F-013: Confirmation ───────────────────────────────

    /// <summary>Non-null only when <see cref="DialogResult"/> is <c>true</c>.</summary>
    public (string Name, List<RuleCondition> Conditions, ConditionLogic Logic,
            Guid TargetContainerId, bool IsEnabled)? Result { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var (isValid, error) = _vm.Validate();
        if (!isValid)
        {
            MessageBox.Show(error, "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result       = _vm.ToParams();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
