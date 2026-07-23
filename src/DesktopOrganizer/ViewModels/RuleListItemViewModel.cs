using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels.Base;

namespace DesktopOrganizer.ViewModels;

/// <summary>
/// Lightweight VM for displaying a single Rule row in the Rule manager list.
/// </summary>
public class RuleListItemViewModel : ObservableObject
{
    private readonly Rule _model;

    public RuleListItemViewModel(Rule model, string targetContainerName)
    {
        _model              = model;
        TargetContainerName = targetContainerName;
    }

    public Guid   Id       => _model.Id;
    public string Name     => _model.Name;
    public int    Priority => _model.Priority;
    public bool   IsEnabled => _model.IsEnabled;
    public int    ConditionCount => _model.Conditions.Count;
    public string TargetContainerName { get; }

    public string ConditionCombination =>
        _model.ConditionCombination == ConditionLogic.And ? "AND" : "OR";

    public string StatusText  => _model.IsEnabled ? "활성" : "비활성";
    public string StatusColor => _model.IsEnabled ? "#2196F3" : "#999999";

    public Rule Model => _model;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(ConditionCount));
        OnPropertyChanged(nameof(TargetContainerName));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
    }
}
