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
            OnPropertyChanged(nameof(TypeHint));
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
        set
        {
            if (!SetField(ref _patternMatchType, value)) return;
            OnPropertyChanged(nameof(PatternHint));
        }
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

    // ── Hint text (for UX descriptions) ──────────────────────────

    public string TypeHint => Type switch
    {
        ConditionType.FileNamePattern   => "파일 이름 전체(확장자 포함)를 검사합니다.",
        ConditionType.Extension         => "확장자로 분류합니다. 쉼표(,)로 여러 개 입력 가능  예: .pdf, .docx",
        ConditionType.FileCategory      => "파일 종류(대분류)로 분류합니다. 문서·이미지·동영상 등",
        ConditionType.CreatedDateRange  => "생성일 기준으로 분류합니다. 시작/종료 중 하나만 입력해도 됩니다.",
        ConditionType.ModifiedDateRange => "수정일 기준으로 분류합니다. 시작/종료 중 하나만 입력해도 됩니다.",
        _ => string.Empty
    };

    public string PatternHint => PatternMatchType switch
    {
        PatternMatchType.Contains   => "예: 보고서  →  '보고서'가 포함된 파일명 매칭 (보고서_2024.docx, 월별보고서.xlsx 등)",
        PatternMatchType.StartsWith => "예: 프로젝트  →  '프로젝트'로 시작하는 파일명 매칭 (프로젝트_계획.docx 등)",
        PatternMatchType.EndsWith   => "예: _최종.docx  →  '_최종.docx'로 끝나는 파일명 매칭",
        PatternMatchType.Regex      => "예: ^회의.*\\.pptx$  →  정규식으로 파일명 매칭 (회의록.pptx, 회의자료.pptx 등)",
        _ => string.Empty
    };

    // ── Conversion ────────────────────────────────────────────────

    public RuleCondition ToModel() => new()
    {
        Type             = Type,
        // FileCategory: always derive from SelectedCategory so the default "Document"
        // is captured even when the user never changes the ComboBox selection.
        Value            = Type == ConditionType.FileCategory
                           ? SelectedCategory.ToString()
                           : Value,
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
