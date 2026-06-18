namespace DesktopOrganizer.Models;

public class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // Lower number = higher priority (1 = highest)
    public int Priority { get; set; }

    public List<RuleCondition> Conditions { get; set; } = new();

    // Global combination mode when Conditions list logic isn't sufficient
    public ConditionLogic ConditionCombination { get; set; } = ConditionLogic.And;

    public Guid TargetContainerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
