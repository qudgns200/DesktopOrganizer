using System.Collections.ObjectModel;
using System.Windows.Input;
using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels.Base;

namespace DesktopOrganizer.ViewModels;

/// <summary>
/// VM for the Rule create / edit dialog (F-012, F-013).
/// Validates required fields before the dialog can be confirmed.
/// </summary>
public class RuleEditorViewModel : ObservableObject
{
    private string         _name                 = string.Empty;
    private bool           _isEnabled            = true;
    private ConditionLogic _conditionCombination = ConditionLogic.And;
    private Guid?          _targetContainerId;

    public RuleEditorViewModel(IReadOnlyList<Container> containers, Rule? existing = null)
    {
        Containers             = containers;
        AddConditionCommand    = new RelayCommand(_ => AddCondition());
        RemoveConditionCommand = new RelayCommand(p => RemoveCondition(p as RuleConditionViewModel));

        if (existing is not null)
        {
            _name                 = existing.Name;
            _isEnabled            = existing.IsEnabled;
            _conditionCombination = existing.ConditionCombination;
            _targetContainerId    = existing.TargetContainerId;

            foreach (var c in existing.Conditions)
                Conditions.Add(RuleConditionViewModel.FromModel(c));
        }
        else
        {
            Conditions.Add(new RuleConditionViewModel());
        }
    }

    // ── Basic rule fields ─────────────────────────────────────────

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    public ConditionLogic ConditionCombination
    {
        get => _conditionCombination;
        set => SetField(ref _conditionCombination, value);
    }

    public Guid? TargetContainerId
    {
        get => _targetContainerId;
        set => SetField(ref _targetContainerId, value);
    }

    // ── Lists ─────────────────────────────────────────────────────

    public IReadOnlyList<Container>     Containers           { get; }
    public IReadOnlyList<ConditionLogic> ConditionLogicOptions { get; } = Enum.GetValues<ConditionLogic>();

    public ObservableCollection<RuleConditionViewModel> Conditions { get; } = new();

    // ── Commands ──────────────────────────────────────────────────

    public ICommand AddConditionCommand    { get; }
    public ICommand RemoveConditionCommand { get; }

    // ── Validation ────────────────────────────────────────────────

    public (bool IsValid, string Error) Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return (false, "Rule 이름을 입력하세요.");
        if (Conditions.Count == 0)
            return (false, "조건을 하나 이상 추가하세요.");

        foreach (var c in Conditions)
        {
            if (c.Type == ConditionType.FileNamePattern && string.IsNullOrWhiteSpace(c.Value))
                return (false, "파일명 패턴 조건의 패턴 값을 입력하세요.");
            if (c.Type == ConditionType.Extension && string.IsNullOrWhiteSpace(c.Value))
                return (false, "확장자 조건의 확장자 값을 입력하세요. (예: .pdf, .docx)");
        }

        if (TargetContainerId is null)
            return (false, "대상 Container를 선택하세요.");
        return (true, string.Empty);
    }

    // ── Extract result ────────────────────────────────────────────

    public (string Name, List<RuleCondition> Conditions, ConditionLogic Logic,
            Guid TargetContainerId, bool IsEnabled) ToParams() =>
        (
            Name.Trim(),
            Conditions.Select(c => c.ToModel()).ToList(),
            ConditionCombination,
            TargetContainerId!.Value,
            IsEnabled
        );

    // ── Private helpers ───────────────────────────────────────────

    private void AddCondition() => Conditions.Add(new RuleConditionViewModel());

    private void RemoveCondition(RuleConditionViewModel? vm)
    {
        if (vm is not null)
            Conditions.Remove(vm);
    }
}
