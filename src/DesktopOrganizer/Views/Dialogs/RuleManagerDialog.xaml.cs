using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels;

// UseWindowsForms=true: resolve type ambiguities
using Button          = System.Windows.Controls.Button;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs   = System.Windows.DragEventArgs;
using MessageBox      = System.Windows.MessageBox;
using MouseEventArgs  = System.Windows.Input.MouseEventArgs;
using Point           = System.Windows.Point;

namespace DesktopOrganizer.Views.Dialogs;

/// <summary>
/// F-012 ~ F-015: Rule list with drag-to-reorder, create, edit and delete.
/// </summary>
public partial class RuleManagerDialog : Window
{
    private readonly RuleService    _ruleService;
    private readonly SettingsService _settings;

    private RuleListItemViewModel? _dragSource;
    private Point                  _dragStartPoint;
    private bool                   _isDragging;

    public ObservableCollection<RuleListItemViewModel> Rules { get; } = new();

    public RuleManagerDialog(RuleService ruleService, SettingsService settings)
    {
        _ruleService = ruleService;
        _settings    = settings;
        InitializeComponent();
        DataContext  = this;
        LoadRules();
    }

    // ── Rule list helpers ─────────────────────────────────────────

    private void LoadRules()
    {
        Rules.Clear();
        foreach (var rule in _ruleService.GetAll().OrderBy(r => r.Priority))
            Rules.Add(MakeItem(rule));
    }

    private RuleListItemViewModel MakeItem(Rule rule)
    {
        var containerName = _settings.Config.Containers
            .FirstOrDefault(c => c.Id == rule.TargetContainerId)?.Name ?? "(삭제됨)";
        return new RuleListItemViewModel(rule, containerName);
    }

    private IReadOnlyList<Container> GetContainers() =>
        _settings.Config.Containers.AsReadOnly();

    // ── F-012: Add ────────────────────────────────────────────────

    private void OnAddRuleClick(object sender, RoutedEventArgs e)
    {
        var containers = GetContainers();
        if (containers.Count == 0)
        {
            MessageBox.Show("먼저 Container를 하나 이상 생성하세요.",
                "Container 없음", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new RuleEditorDialog(containers) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        var (name, conditions, logic, targetId, isEnabled) = dialog.Result.Value;
        _ruleService.Create(name, conditions, logic, targetId, isEnabled);

        var ask = MessageBox.Show("새 Rule을 기존 바탕화면 아이콘에 즉시 적용하시겠습니까?",
            "즉시 적용", MessageBoxButton.YesNo, MessageBoxImage.Question);
        // Actual re-apply is handled in Phase 7 (AutoOrganizeService). Log intent only.
        _ = ask;

        LoadRules();
    }

    // ── F-013: Edit ───────────────────────────────────────────────

    private void OnEditRuleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id) return;

        var rule = _ruleService.GetById(id);
        if (rule is null) return;

        var containers = GetContainers();
        var dialog = new RuleEditorDialog(containers, rule) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        var (name, conditions, logic, targetId, isEnabled) = dialog.Result.Value;
        _ruleService.Update(id, name, conditions, logic, targetId, isEnabled);

        var ask = MessageBox.Show("수정된 Rule을 기존 바탕화면 아이콘에 재적용하시겠습니까?",
            "재적용", MessageBoxButton.YesNo, MessageBoxImage.Question);
        _ = ask;

        LoadRules();
    }

    // ── F-014: Delete ─────────────────────────────────────────────

    private void OnDeleteRuleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id) return;

        var rule = _ruleService.GetById(id);
        if (rule is null) return;

        var result = MessageBox.Show(
            $"'{rule.Name}' Rule을 삭제하시겠습니까?\n\n이 Rule을 삭제하면 더 이상 자동으로 적용되지 않습니다.\n기존 배치된 아이콘 위치는 변경되지 않습니다.",
            "Rule 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK) return;

        _ruleService.Delete(id);
        LoadRules();
    }

    // ── F-015: Drag-to-reorder ────────────────────────────────────

    private void OnListPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(RuleListBox);
        _isDragging     = false;

        if (e.OriginalSource is FrameworkElement fe &&
            fe.DataContext is RuleListItemViewModel vm)
        {
            _dragSource = vm;
        }
        else
        {
            _dragSource = null;
        }
    }

    private void OnListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSource is null || e.LeftButton != MouseButtonState.Pressed || _isDragging)
            return;

        var pos  = e.GetPosition(RuleListBox);
        var diff = _dragStartPoint - pos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _isDragging = true;
        DragDrop.DoDragDrop(RuleListBox, _dragSource, DragDropEffects.Move);
        _isDragging = false;
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(RuleListItemViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnListDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(RuleListItemViewModel))) return;
        if (_dragSource is null) return;

        var target = (e.OriginalSource as FrameworkElement)?.DataContext as RuleListItemViewModel;
        if (target is null || target.Id == _dragSource.Id)
        {
            _dragSource = null;
            return;
        }

        _ruleService.Reorder(_dragSource.Id, target.Id);
        _dragSource = null;
        LoadRules();
    }

    // ── Close ─────────────────────────────────────────────────────

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
