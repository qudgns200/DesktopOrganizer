using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels.Base;

namespace DesktopOrganizer.ViewModels;

/// <summary>
/// View-model wrapper for a single <see cref="RuleCondition"/> inside the Rule editor.
/// Exposes type-specific helpers so the XAML can show/hide the right input controls.
/// </summary>
public class RuleConditionViewModel : ObservableObject
{
    private ConditionType    _type             = ConditionType.FileNamePattern;
    private string           _value            = string.Empty;
    private PatternMatchType _patternMatchType = PatternMatchType.Contains;
    private FileCategory     _selectedCategory = FileCategory.Document;
    private string           _dateFrom         = string.Empty;
    private string           _dateTo           = string.Empty;

    // ── Available option lists ────────────────────────────────────

    public IReadOnlyList<ConditionType>    ConditionTypes    { get; } = Enum.GetValues<ConditionType>();
    public IReadOnlyList<PatternMatchType> PatternMatchTypes { get; } = Enum.GetValues<PatternMatchType>();
    public IReadOnlyList<FileCategory>     FileCategories    { get; } = Enum.GetValues<FileCategory>();

    // ── Bound properties ──────────────────────────────────────────

    public ConditionType Type
    {
        get => _type;
        set
        {
            if (!SetField(ref _type, value)) return;
            OnPropertyChanged(nameof(IsFileNamePattern));
            OnPropertyChanged(nameof(IsExtension));
            OnPropertyChanged(nameof(IsFileCategory));
            OnPropertyChanged(nameof(IsDateRange));
        }
    }

    public string Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }

    public PatternMatchType PatternMatchType
    {
        get => _patternMatchType;
        set => SetField(ref _patternMatchType, value);
    }

    public FileCategory SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetField(ref _selectedCategory, value)) return;
            Value = value.ToString();
        }
    }

    // DateRange helpers — Value is stored as "from|to"
    public string DateFrom
    {
        get => _dateFrom;
        set { SetField(ref _dateFrom, value); SyncDateValue(); }
    }

    public string DateTo
    {
        get => _dateTo;
        set { SetField(ref _dateTo, value); SyncDateValue(); }
    }

    // ── Visibility flags (for DataTrigger-free XAML) ──────────────

    public bool IsFileNamePattern => Type == ConditionType.FileNamePattern;
    public bool IsExtension       => Type == ConditionType.Extension;
    public bool IsFileCategory    => Type == ConditionType.FileCategory;
    public bool IsDateRange       => Type is ConditionType.CreatedDateRange or ConditionType.ModifiedDateRange;

    // ── Conversion ────────────────────────────────────────────────

    public RuleCondition ToModel() => new()
    {
        Type             = Type,
        Value            = Value,
        PatternMatchType = PatternMatchType
    };

    public static RuleConditionViewModel FromModel(RuleCondition c)
    {
        var vm = new RuleConditionViewModel
        {
            _type             = c.Type,
            _patternMatchType = c.PatternMatchType,
            _value            = c.Value
        };

        if (c.Type == ConditionType.FileCategory &&
            Enum.TryParse<FileCategory>(c.Value, ignoreCase: true, out var cat))
        {
            vm._selectedCategory = cat;
        }

        if (c.Type is ConditionType.CreatedDateRange or ConditionType.ModifiedDateRange)
        {
            var parts = c.Value.Split('|');
            if (parts.Length == 2) { vm._dateFrom = parts[0]; vm._dateTo = parts[1]; }
        }

        return vm;
    }

    // ── Private helpers ───────────────────────────────────────────

    private void SyncDateValue() => Value = $"{_dateFrom}|{_dateTo}";
}
