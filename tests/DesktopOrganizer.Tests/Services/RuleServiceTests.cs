using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class RuleServiceTests : IDisposable
{
    private readonly string          _tempDir;
    private readonly SettingsService _settings;
    private readonly RuleService     _sut;
    private readonly Container       _container;

    public RuleServiceTests()
    {
        _tempDir   = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _settings  = new SettingsService(_tempDir);
        _settings.Load();
        _sut       = new RuleService(_settings);

        _container = new Container { Id = Guid.NewGuid(), Name = "TestContainer" };
        _settings.Config.Containers.Add(_container);
        _settings.Save();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private RuleCondition MakePatternCondition(string pattern,
        PatternMatchType match = PatternMatchType.Contains) => new()
    {
        Type             = ConditionType.FileNamePattern,
        Value            = pattern,
        PatternMatchType = match
    };

    private RuleCondition MakeExtensionCondition(string extensions) => new()
    {
        Type  = ConditionType.Extension,
        Value = extensions
    };

    private RuleCondition MakeCategoryCondition(FileCategory cat) => new()
    {
        Type  = ConditionType.FileCategory,
        Value = cat.ToString()
    };

    private RuleCondition MakeDateRangeCondition(ConditionType type, string from, string to) => new()
    {
        Type  = type,
        Value = $"{from}|{to}"
    };

    private static IconInfo MakeIcon(string fileName, string ext = ".txt",
        FileCategory cat = FileCategory.Document,
        DateTime? created = null, DateTime? modified = null) => new()
    {
        FileName   = fileName,
        FullPath   = $"C:\\Desktop\\{fileName}",
        Extension  = ext,
        Category   = cat,
        CreatedAt  = created  ?? new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        ModifiedAt = modified ?? new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc)
    };

    private Rule CreateRule(List<RuleCondition> conditions,
        ConditionLogic logic = ConditionLogic.And, string name = "Test Rule") =>
        _sut.Create(name, conditions, logic, _container.Id);

    // ── F-012: Create ─────────────────────────────────────────────

    [Fact]
    public void Create_AddsRuleWithAutoIncPriority()
    {
        var r1 = CreateRule(new() { MakePatternCondition("A") });
        var r2 = CreateRule(new() { MakePatternCondition("B") });

        Assert.Equal(1, r1.Priority);
        Assert.Equal(2, r2.Priority);
    }

    [Fact]
    public void Create_PersistsToDisk()
    {
        CreateRule(new() { MakePatternCondition("test") }, name: "Persist Rule");

        var s2 = new SettingsService(_tempDir);
        s2.Load();
        Assert.Single(s2.Config.Rules);
        Assert.Equal("Persist Rule", s2.Config.Rules[0].Name);
    }

    [Fact]
    public void Create_SetsIsEnabledTrue_ByDefault()
    {
        var rule = CreateRule(new() { MakePatternCondition("x") });
        Assert.True(rule.IsEnabled);
    }

    // ── F-013: Update ─────────────────────────────────────────────

    [Fact]
    public void Update_ChangesName()
    {
        var rule = CreateRule(new() { MakePatternCondition("old") }, name: "Old");
        bool ok  = _sut.Update(rule.Id, "New", rule.Conditions,
            rule.ConditionCombination, rule.TargetContainerId, rule.IsEnabled);

        Assert.True(ok);
        Assert.Equal("New", rule.Name);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        bool ok = _sut.Update(Guid.NewGuid(), "x", new(), ConditionLogic.And, _container.Id, true);
        Assert.False(ok);
    }

    [Fact]
    public void Update_CanDisableRule()
    {
        var rule = CreateRule(new() { MakePatternCondition("x") });
        _sut.Update(rule.Id, rule.Name, rule.Conditions,
            rule.ConditionCombination, rule.TargetContainerId, isEnabled: false);

        Assert.False(rule.IsEnabled);
    }

    // ── F-014: Delete ─────────────────────────────────────────────

    [Fact]
    public void Delete_RemovesRule()
    {
        var rule = CreateRule(new() { MakePatternCondition("x") });
        bool ok  = _sut.Delete(rule.Id);

        Assert.True(ok);
        Assert.Empty(_sut.GetAll());
    }

    [Fact]
    public void Delete_NormalizesPriorities()
    {
        var r1 = CreateRule(new() { MakePatternCondition("a") }, name: "R1");
        var r2 = CreateRule(new() { MakePatternCondition("b") }, name: "R2");
        var r3 = CreateRule(new() { MakePatternCondition("c") }, name: "R3");

        _sut.Delete(r2.Id);

        var remaining = _sut.GetAll().OrderBy(r => r.Priority).ToList();
        Assert.Equal(1, remaining[0].Priority);
        Assert.Equal(2, remaining[1].Priority);
    }

    [Fact]
    public void Delete_UnknownId_ReturnsFalse()
    {
        Assert.False(_sut.Delete(Guid.NewGuid()));
    }

    // ── F-015: MoveUp / MoveDown ──────────────────────────────────

    [Fact]
    public void MoveUp_SwapsWithPreviousRule()
    {
        var r1 = CreateRule(new() { MakePatternCondition("a") }, name: "R1");
        var r2 = CreateRule(new() { MakePatternCondition("b") }, name: "R2");

        bool ok = _sut.MoveUp(r2.Id);

        Assert.True(ok);
        var ordered = _sut.GetAll().OrderBy(r => r.Priority).ToList();
        Assert.Equal("R2", ordered[0].Name);
        Assert.Equal("R1", ordered[1].Name);
    }

    [Fact]
    public void MoveUp_FirstRule_ReturnsFalse()
    {
        var r1 = CreateRule(new() { MakePatternCondition("a") });
        Assert.False(_sut.MoveUp(r1.Id));
    }

    [Fact]
    public void MoveDown_SwapsWithNextRule()
    {
        var r1 = CreateRule(new() { MakePatternCondition("a") }, name: "R1");
        var r2 = CreateRule(new() { MakePatternCondition("b") }, name: "R2");

        bool ok = _sut.MoveDown(r1.Id);

        Assert.True(ok);
        var ordered = _sut.GetAll().OrderBy(r => r.Priority).ToList();
        Assert.Equal("R2", ordered[0].Name);
        Assert.Equal("R1", ordered[1].Name);
    }

    [Fact]
    public void MoveDown_LastRule_ReturnsFalse()
    {
        var r1 = CreateRule(new() { MakePatternCondition("a") });
        Assert.False(_sut.MoveDown(r1.Id));
    }

    [Fact]
    public void Reorder_MovesSourceBeforeTarget()
    {
        var r1 = CreateRule(new() { MakePatternCondition("a") }, name: "R1");
        var r2 = CreateRule(new() { MakePatternCondition("b") }, name: "R2");
        var r3 = CreateRule(new() { MakePatternCondition("c") }, name: "R3");

        // Move R3 before R1
        bool ok = _sut.Reorder(r3.Id, r1.Id);

        Assert.True(ok);
        var ordered = _sut.GetAll().OrderBy(r => r.Priority).ToList();
        Assert.Equal("R3", ordered[0].Name);
        Assert.Equal("R1", ordered[1].Name);
        Assert.Equal("R2", ordered[2].Name);
    }

    // ── F-015: FindMatchingRule (First-Match) ─────────────────────

    [Fact]
    public void FindMatchingRule_ReturnsFirstMatch_ByPriority()
    {
        var r1 = CreateRule(new() { MakePatternCondition("Report") }, name: "R1");
        var r2 = CreateRule(new() { MakePatternCondition("Report") }, name: "R2");

        var match = _sut.FindMatchingRule(MakeIcon("Report.pdf"));

        Assert.NotNull(match);
        Assert.Equal("R1", match!.Name);
    }

    [Fact]
    public void FindMatchingRule_SkipsDisabledRules()
    {
        var r1 = CreateRule(new() { MakePatternCondition("test") }, name: "R1");
        _sut.Update(r1.Id, r1.Name, r1.Conditions,
            r1.ConditionCombination, r1.TargetContainerId, isEnabled: false);

        var r2 = CreateRule(new() { MakePatternCondition("test") }, name: "R2");

        var match = _sut.FindMatchingRule(MakeIcon("test.pdf"));
        Assert.NotNull(match);
        Assert.Equal("R2", match!.Name);
    }

    [Fact]
    public void FindMatchingRule_NoMatch_ReturnsNull()
    {
        CreateRule(new() { MakePatternCondition("nomatch") });
        var match = _sut.FindMatchingRule(MakeIcon("unrelated.pdf"));
        Assert.Null(match);
    }

    // ── MatchesCondition: FileNamePattern ─────────────────────────

    [Fact]
    public void MatchesCondition_FileNamePattern_Contains()
    {
        var cond = MakePatternCondition("report", PatternMatchType.Contains);
        Assert.True(_sut.MatchesCondition(MakeIcon("Monthly_report.pdf"), cond));
        Assert.False(_sut.MatchesCondition(MakeIcon("invoice.pdf"), cond));
    }

    [Fact]
    public void MatchesCondition_FileNamePattern_StartsWith()
    {
        var cond = MakePatternCondition("2024", PatternMatchType.StartsWith);
        Assert.True(_sut.MatchesCondition(MakeIcon("2024_plan.txt"), cond));
        Assert.False(_sut.MatchesCondition(MakeIcon("plan_2024.txt"), cond));
    }

    [Fact]
    public void MatchesCondition_FileNamePattern_EndsWith()
    {
        var cond = MakePatternCondition("final.docx", PatternMatchType.EndsWith);
        Assert.True(_sut.MatchesCondition(MakeIcon("report_final.docx"), cond));
        Assert.False(_sut.MatchesCondition(MakeIcon("final.docx.bak"), cond));
    }

    [Fact]
    public void MatchesCondition_FileNamePattern_Regex()
    {
        var cond = MakePatternCondition(@"^\d{4}_.*\.pdf$", PatternMatchType.Regex);
        Assert.True(_sut.MatchesCondition(MakeIcon("2024_report.pdf"), cond));
        Assert.False(_sut.MatchesCondition(MakeIcon("report.pdf"), cond));
    }

    [Fact]
    public void MatchesCondition_FileNamePattern_CaseInsensitive()
    {
        var cond = MakePatternCondition("REPORT", PatternMatchType.Contains);
        Assert.True(_sut.MatchesCondition(MakeIcon("Monthly_report.pdf"), cond));
    }

    // ── MatchesCondition: Extension ───────────────────────────────

    [Fact]
    public void MatchesCondition_Extension_SingleMatch()
    {
        var cond = MakeExtensionCondition(".pdf");
        Assert.True(_sut.MatchesCondition(MakeIcon("a.pdf", ".pdf"), cond));
        Assert.False(_sut.MatchesCondition(MakeIcon("a.docx", ".docx"), cond));
    }

    [Fact]
    public void MatchesCondition_Extension_MultipleExtensions()
    {
        var cond = MakeExtensionCondition(".pdf, .docx, .xlsx");
        Assert.True(_sut.MatchesCondition(MakeIcon("a.docx", ".docx"), cond));
        Assert.True(_sut.MatchesCondition(MakeIcon("b.xlsx", ".xlsx"), cond));
        Assert.False(_sut.MatchesCondition(MakeIcon("c.png", ".png"), cond));
    }

    [Fact]
    public void MatchesCondition_Extension_WithoutLeadingDot()
    {
        var cond = MakeExtensionCondition("pdf");
        Assert.True(_sut.MatchesCondition(MakeIcon("a.pdf", ".pdf"), cond));
    }

    // ── MatchesCondition: FileCategory ───────────────────────────

    [Fact]
    public void MatchesCondition_FileCategory_Match()
    {
        var cond = MakeCategoryCondition(FileCategory.Image);
        Assert.True(_sut.MatchesCondition(MakeIcon("photo.png", cat: FileCategory.Image), cond));
        Assert.False(_sut.MatchesCondition(MakeIcon("doc.pdf", cat: FileCategory.Document), cond));
    }

    // ── MatchesCondition: DateRange ───────────────────────────────

    [Fact]
    public void MatchesCondition_CreatedDateRange_WithinRange()
    {
        var cond = MakeDateRangeCondition(ConditionType.CreatedDateRange, "2024-01-01", "2024-12-31");
        var icon = MakeIcon("a.txt", created: new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(_sut.MatchesCondition(icon, cond));
    }

    [Fact]
    public void MatchesCondition_CreatedDateRange_OutsideRange()
    {
        var cond = MakeDateRangeCondition(ConditionType.CreatedDateRange, "2024-01-01", "2024-06-01");
        var icon = MakeIcon("a.txt", created: new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        Assert.False(_sut.MatchesCondition(icon, cond));
    }

    // ── Matches: AND / OR logic ───────────────────────────────────

    [Fact]
    public void Matches_And_AllConditionsMustMatch()
    {
        var rule = new Rule
        {
            ConditionCombination = ConditionLogic.And,
            Conditions = new()
            {
                MakePatternCondition("report"),
                MakeExtensionCondition(".pdf")
            }
        };

        Assert.True(_sut.Matches(MakeIcon("report.pdf",  ".pdf"), rule));
        Assert.False(_sut.Matches(MakeIcon("report.docx", ".docx"), rule));
        Assert.False(_sut.Matches(MakeIcon("invoice.pdf", ".pdf"), rule));
    }

    [Fact]
    public void Matches_Or_AnyConditionSuffices()
    {
        var rule = new Rule
        {
            ConditionCombination = ConditionLogic.Or,
            Conditions = new()
            {
                MakePatternCondition("report"),
                MakeExtensionCondition(".pdf")
            }
        };

        Assert.True(_sut.Matches(MakeIcon("invoice.pdf",  ".pdf"), rule));
        Assert.True(_sut.Matches(MakeIcon("report.docx", ".docx"), rule));
        Assert.False(_sut.Matches(MakeIcon("invoice.docx", ".docx"), rule));
    }

    [Fact]
    public void Matches_EmptyConditions_ReturnsFalse()
    {
        var rule = new Rule { Conditions = new() };
        Assert.False(_sut.Matches(MakeIcon("any.pdf"), rule));
    }
}
