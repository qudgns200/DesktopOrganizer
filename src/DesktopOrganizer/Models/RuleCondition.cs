namespace DesktopOrganizer.Models;

public enum ConditionType
{
    FileNamePattern,
    Extension,
    FileCategory,
    CreatedDateRange,
    ModifiedDateRange
}

public enum PatternMatchType
{
    Contains,
    StartsWith,
    EndsWith,
    Regex
}

public enum ConditionLogic
{
    And,
    Or
}

public class RuleCondition
{
    public ConditionType Type { get; set; }

    // FileNamePattern: the pattern string
    // Extension: comma-separated list (e.g. ".pdf,.docx")
    // FileCategory: category name
    // DateRange: "yyyy-MM-dd|yyyy-MM-dd" (from|to)
    public string Value { get; set; } = string.Empty;

    public PatternMatchType PatternMatchType { get; set; } = PatternMatchType.Contains;

    // Operator to combine with the NEXT condition in the list
    public ConditionLogic Operator { get; set; } = ConditionLogic.And;
}
