using System.Text.RegularExpressions;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-012 ~ F-015: Rule CRUD and First-Match condition evaluation engine.
/// Rules are stored in order; lower Priority number = higher priority (1 = first matched).
/// </summary>
public class RuleService
{
    private readonly SettingsService _settings;

    public RuleService(SettingsService settings) => _settings = settings;

    // ── F-012: Create ─────────────────────────────────────────────

    public Rule Create(string name, List<RuleCondition> conditions,
        ConditionLogic logic, Guid targetContainerId, bool isEnabled = true)
    {
        var rule = new Rule
        {
            Name                 = name.Trim(),
            Conditions           = conditions,
            ConditionCombination = logic,
            TargetContainerId    = targetContainerId,
            IsEnabled            = isEnabled,
            Priority             = GetNextPriority()
        };
        _settings.Config.Rules.Add(rule);
        _settings.Save();
        return rule;
    }

    // ── F-013: Update ─────────────────────────────────────────────

    public bool Update(Guid id, string name, List<RuleCondition> conditions,
        ConditionLogic logic, Guid targetContainerId, bool isEnabled)
    {
        var rule = FindRule(id);
        if (rule is null) return false;

        rule.Name                 = name.Trim();
        rule.Conditions           = conditions;
        rule.ConditionCombination = logic;
        rule.TargetContainerId    = targetContainerId;
        rule.IsEnabled            = isEnabled;
        rule.UpdatedAt            = DateTime.UtcNow;
        _settings.Save();
        return true;
    }

    // ── F-014: Delete ─────────────────────────────────────────────

    public bool Delete(Guid id)
    {
        var rule = FindRule(id);
        if (rule is null) return false;

        _settings.Config.Rules.Remove(rule);
        NormalizePriorities();
        _settings.Save();
        return true;
    }

    // ── F-015: Priority reorder ───────────────────────────────────

    public bool MoveUp(Guid id)
    {
        var rules = _settings.Config.Rules;
        int idx   = rules.FindIndex(r => r.Id == id);
        if (idx <= 0) return false;

        (rules[idx].Priority, rules[idx - 1].Priority) =
            (rules[idx - 1].Priority, rules[idx].Priority);

        var item = rules[idx];
        rules.RemoveAt(idx);
        rules.Insert(idx - 1, item);

        _settings.Save();
        return true;
    }

    public bool MoveDown(Guid id)
    {
        var rules = _settings.Config.Rules;
        int idx   = rules.FindIndex(r => r.Id == id);
        if (idx < 0 || idx >= rules.Count - 1) return false;

        (rules[idx].Priority, rules[idx + 1].Priority) =
            (rules[idx + 1].Priority, rules[idx].Priority);

        var item = rules[idx];
        rules.RemoveAt(idx);
        rules.Insert(idx + 1, item);

        _settings.Save();
        return true;
    }

    /// <summary>Moves <paramref name="sourceId"/> so it appears immediately before <paramref name="targetId"/>.</summary>
    public bool Reorder(Guid sourceId, Guid targetId)
    {
        if (sourceId == targetId) return false;

        var rules = _settings.Config.Rules;
        int srcIdx = rules.FindIndex(r => r.Id == sourceId);
        int tgtIdx = rules.FindIndex(r => r.Id == targetId);
        if (srcIdx < 0 || tgtIdx < 0) return false;

        var item = rules[srcIdx];
        rules.RemoveAt(srcIdx);

        int insertAt = rules.FindIndex(r => r.Id == targetId);
        if (insertAt < 0) insertAt = rules.Count;
        rules.Insert(insertAt, item);

        NormalizePriorities();
        _settings.Save();
        return true;
    }

    // ── F-015: First-Match evaluation ─────────────────────────────

    /// <summary>
    /// Returns the first active Rule (ordered by Priority) whose conditions
    /// match <paramref name="icon"/>.  Returns <c>null</c> if none match.
    /// </summary>
    public Rule? FindMatchingRule(IconInfo icon)
    {
        foreach (var rule in _settings.Config.Rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority))
        {
            if (Matches(icon, rule))
                return rule;
        }
        return null;
    }

    public bool Matches(IconInfo icon, Rule rule)
    {
        if (rule.Conditions.Count == 0) return false;

        return rule.ConditionCombination == ConditionLogic.And
            ? rule.Conditions.All(c => MatchesCondition(icon, c))
            : rule.Conditions.Any(c => MatchesCondition(icon, c));
    }

    public bool MatchesCondition(IconInfo icon, RuleCondition condition) =>
        condition.Type switch
        {
            ConditionType.FileNamePattern   => MatchesPattern(icon.FileName,  condition),
            ConditionType.Extension         => MatchesExtension(icon.Extension, condition),
            ConditionType.FileCategory      => MatchesCategory(icon.Category, condition),
            ConditionType.CreatedDateRange  => MatchesDateRange(icon.CreatedAt,  condition),
            ConditionType.ModifiedDateRange => MatchesDateRange(icon.ModifiedAt, condition),
            _ => false
        };

    // ── Queries ───────────────────────────────────────────────────

    public IReadOnlyList<Rule> GetAll() =>
        _settings.Config.Rules.AsReadOnly();

    public Rule? GetById(Guid id) => FindRule(id);

    // ── Helpers ───────────────────────────────────────────────────

    private Rule? FindRule(Guid id) =>
        _settings.Config.Rules.FirstOrDefault(r => r.Id == id);

    private int GetNextPriority()
    {
        var rules = _settings.Config.Rules;
        return rules.Count == 0 ? 1 : rules.Max(r => r.Priority) + 1;
    }

    private void NormalizePriorities()
    {
        int i = 1;
        foreach (var rule in _settings.Config.Rules)
            rule.Priority = i++;
    }

    private static bool MatchesPattern(string fileName, RuleCondition condition)
    {
        var pattern = condition.Value;
        if (string.IsNullOrEmpty(pattern)) return false;

        return condition.PatternMatchType switch
        {
            PatternMatchType.Contains   => fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            PatternMatchType.StartsWith => fileName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
            PatternMatchType.EndsWith   => fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase),
            PatternMatchType.Regex      => TryRegexMatch(fileName, pattern),
            _ => false
        };
    }

    private static bool MatchesExtension(string extension, RuleCondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.Value)) return false;

        return condition.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(ext => extension.Equals(
                ext.StartsWith('.') ? ext : '.' + ext,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCategory(FileCategory category, RuleCondition condition) =>
        Enum.TryParse<FileCategory>(condition.Value, ignoreCase: true, out var cat) && cat == category;

    private static bool MatchesDateRange(DateTime date, RuleCondition condition)
    {
        var parts = condition.Value.Split('|');
        if (parts.Length != 2) return false;

        var hasFrom = DateTime.TryParse(parts[0].Trim(), out var from);
        var hasTo   = DateTime.TryParse(parts[1].Trim(), out var to);

        if (hasFrom && date.Date < from.Date) return false;
        if (hasTo   && date.Date > to.Date)   return false;
        return true;
    }

    private static bool TryRegexMatch(string input, string pattern)
    {
        try   { return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)); }
        catch { return false; }
    }
}
