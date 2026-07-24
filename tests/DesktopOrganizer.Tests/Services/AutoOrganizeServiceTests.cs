using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

/// <summary>
/// Tests for F-017 (AutoOrganizeService) core logic.
/// Win32 position writes are suppressed via TestableAutoOrganizeService.
/// Desktop file-system events are injected directly via ProcessChangeEvent().
/// </summary>
public class AutoOrganizeServiceTests : IDisposable
{
    private readonly string          _tempDir;
    private readonly SettingsService _settings;
    private readonly RuleService     _ruleService;
    private readonly Container       _container;

    // The service under test (and the watcher it wraps)
    private readonly DesktopWatcherService       _watcher;
    private readonly TestableAutoOrganizeService _sut;

    public AutoOrganizeServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _settings = new SettingsService(_tempDir);
        _settings.Load();
        _ruleService = new RuleService(_settings);

        _container = new Container { Id = Guid.NewGuid(), Name = "TestContainer" };
        _settings.Config.Containers.Add(_container);
        _settings.Save();

        _watcher = new DesktopWatcherService();
        _sut = new TestableAutoOrganizeService(
            new DesktopReaderService(),
            new ExclusionService(_settings.Config.Settings),
            new FileClassifierService(),
            _ruleService,
            _settings,
            _watcher,
            new IconSortService(),
            new IconOrderService(_settings));
    }

    public void Dispose()
    {
        _sut.Dispose();
        _watcher.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>Creates a real temp file and returns a seeded IconInfo for it.</summary>
    private string CreateTempFile(string fileName)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, "test");
        return path;
    }

    private IconInfo MakeIcon(string fullPath, string ext = ".txt",
        FileCategory cat = FileCategory.Document) => new()
    {
        FileName   = Path.GetFileName(fullPath),
        FullPath   = fullPath,
        Extension  = ext,
        Category   = cat,
        IconType   = IconType.File,
        CreatedAt  = DateTime.UtcNow,
        ModifiedAt = DateTime.UtcNow
    };

    private Rule CreatePdfRule() => _ruleService.Create(
        "PDF Rule",
        new List<RuleCondition>
        {
            new() { Type = ConditionType.Extension, Value = ".pdf" }
        },
        ConditionLogic.And,
        _container.Id);

    private Rule CreateCategoryRule(FileCategory category) => _ruleService.Create(
        $"{category} Rule",
        new List<RuleCondition>
        {
            new() { Type = ConditionType.FileCategory, Value = category.ToString() }
        },
        ConditionLogic.And,
        _container.Id);

    private DesktopChangeEventArgs CreatedEvent(string fullPath) => new()
    {
        ChangeType = DesktopChangeType.Created,
        FullPath   = fullPath
    };

    private DesktopChangeEventArgs DeletedEvent(string fullPath) => new()
    {
        ChangeType = DesktopChangeType.Deleted,
        FullPath   = fullPath
    };

    private DesktopChangeEventArgs RenamedEvent(string oldPath, string newPath) => new()
    {
        ChangeType  = DesktopChangeType.Renamed,
        FullPath    = newPath,
        OldFullPath = oldPath
    };

    // ── F-017: Created — matching rule ───────────────────────────

    [Fact]
    public void Created_MatchingRule_PlacesIconInContainer()
    {
        CreatePdfRule();
        var path = CreateTempFile("report.pdf");
        var icon = MakeIcon(path, ".pdf", FileCategory.Document);
        _sut.SeedIcons(new[] { icon });

        _sut.ProcessChangeEvent(CreatedEvent(path));

        var containerIcons = _sut.GetContainerIcons(_container.Id);
        Assert.Contains(containerIcons, i => i.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Created_MatchingRule_WritesIconPositions()
    {
        CreatePdfRule();
        var path = CreateTempFile("doc.pdf");
        var icon = MakeIcon(path, ".pdf");
        _sut.SeedIcons(new[] { icon });

        _sut.ProcessChangeEvent(CreatedEvent(path));

        Assert.NotEmpty(_sut.PositionWrites);
    }

    [Fact]
    public void Created_MatchingRule_SetsAssignedContainerId()
    {
        CreatePdfRule();
        var path = CreateTempFile("x.pdf");
        var icon = MakeIcon(path, ".pdf");
        _sut.SeedIcons(new[] { icon });

        _sut.ProcessChangeEvent(CreatedEvent(path));

        var icons = _sut.Icons;
        Assert.True(icons.ContainsKey(path));
        Assert.Equal(_container.Id, icons[path].AssignedContainerId);
    }

    // ── F-017: Created — FileCategory rule (Folder / Executable) ─

    [Fact]
    public void Created_FolderMatchingCategoryRule_PlacesInContainer()
    {
        CreateCategoryRule(FileCategory.Folder);
        // A real directory on the desktop is read as IconType.Folder → FileCategory.Folder
        var dirPath = Path.Combine(_tempDir, "MyFolder");
        Directory.CreateDirectory(dirPath);

        _sut.ProcessChangeEvent(CreatedEvent(dirPath));

        var containerIcons = _sut.GetContainerIcons(_container.Id);
        Assert.Contains(containerIcons, i => i.FullPath.Equals(dirPath, StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(_sut.PositionWrites);
    }

    [Fact]
    public void Created_ExeMatchingCategoryRule_PlacesInContainer()
    {
        CreateCategoryRule(FileCategory.Executable);
        var exePath = CreateTempFile("tool.exe");

        _sut.ProcessChangeEvent(CreatedEvent(exePath));

        var containerIcons = _sut.GetContainerIcons(_container.Id);
        Assert.Contains(containerIcons, i => i.FullPath.Equals(exePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Created_FolderCategoryRule_PositionKeyIsFolderName()
    {
        CreateCategoryRule(FileCategory.Folder);
        var dirPath = Path.Combine(_tempDir, "보고서폴더");
        Directory.CreateDirectory(dirPath);

        _sut.ProcessChangeEvent(CreatedEvent(dirPath));

        // The Win32 position key for a folder must be its exact name (no extension logic)
        Assert.Contains(_sut.PositionWrites, dict => dict.ContainsKey("보고서폴더"));
    }

    // ── F-017: Created — no matching rule ────────────────────────

    [Fact]
    public void Created_NoMatchingRule_IconNotPlacedInContainer()
    {
        // No rules registered
        var path = CreateTempFile("readme.txt");
        var icon = MakeIcon(path, ".txt");
        _sut.SeedIcons(new[] { icon });

        _sut.ProcessChangeEvent(CreatedEvent(path));

        var containerIcons = _sut.GetContainerIcons(_container.Id);
        Assert.Empty(containerIcons);
    }

    [Fact]
    public void Created_NoMatchingRule_NoPositionWritten()
    {
        var path = CreateTempFile("data.csv");
        _sut.SeedIcons(new[] { MakeIcon(path, ".csv") });

        _sut.ProcessChangeEvent(CreatedEvent(path));

        Assert.Empty(_sut.PositionWrites);
    }

    // ── F-017: Created — excluded (system icon name) ─────────────

    [Fact]
    public void Created_SystemIconName_NotPlaced()
    {
        CreatePdfRule();
        // ExclusionService treats "휴지통" display name as a system icon
        var path = Path.Combine(_tempDir, "휴지통");

        _sut.ProcessChangeEvent(CreatedEvent(path));

        Assert.Empty(_sut.GetContainerIcons(_container.Id));
        Assert.Empty(_sut.PositionWrites);
    }

    // ── F-017: Deleted ───────────────────────────────────────────

    [Fact]
    public void Deleted_KnownIcon_RemovedFromRegistry()
    {
        var path = CreateTempFile("old.txt");
        var icon = MakeIcon(path);
        _sut.SeedIcons(new[] { icon });

        _sut.ProcessChangeEvent(DeletedEvent(path));

        Assert.DoesNotContain(path, _sut.Icons.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deleted_AssignedIcon_RemovedFromContainer()
    {
        CreatePdfRule();
        var path = CreateTempFile("assigned.pdf");
        var icon = MakeIcon(path, ".pdf");
        icon.AssignedContainerId = _container.Id;
        _sut.SeedIcons(new[] { icon });

        // Manually seed container icons list
        _sut.SeedContainerIcons(_container.Id, new[] { icon });

        _sut.ProcessChangeEvent(DeletedEvent(path));

        Assert.DoesNotContain(
            _sut.GetContainerIcons(_container.Id),
            i => i.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Deleted_UnknownIcon_NoError()
    {
        // Should not throw for an icon we don't know about
        var ex = Record.Exception(() =>
            _sut.ProcessChangeEvent(DeletedEvent(@"C:\Desktop\ghost.txt")));

        Assert.Null(ex);
    }

    // ── F-017: Renamed ───────────────────────────────────────────

    [Fact]
    public void Renamed_UpdatesIconMetadata()
    {
        var oldPath = Path.Combine(_tempDir, "old.txt");
        var newPath = CreateTempFile("new.pdf");
        var icon = MakeIcon(oldPath, ".txt");
        _sut.SeedIcons(new[] { icon });

        _sut.ProcessChangeEvent(RenamedEvent(oldPath, newPath));

        var icons = _sut.Icons;
        Assert.DoesNotContain(oldPath, icons.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.True(icons.ContainsKey(newPath));
        Assert.Equal(".pdf", icons[newPath].Extension);
    }

    [Fact]
    public void Renamed_NewNameMatchesRule_PlacesInContainer()
    {
        CreatePdfRule();
        var oldPath = Path.Combine(_tempDir, "draft.docx");
        var newPath = CreateTempFile("final.pdf");
        var icon = MakeIcon(oldPath, ".docx");
        _sut.SeedIcons(new[] { icon });

        _sut.ProcessChangeEvent(RenamedEvent(oldPath, newPath));

        var containerIcons = _sut.GetContainerIcons(_container.Id);
        Assert.Contains(containerIcons, i => i.FullPath.Equals(newPath, StringComparison.OrdinalIgnoreCase));
    }

    // ── Double-click launch ──────────────────────────────────────

    [Fact]
    public void LaunchIconInContainer_FirstCell_LaunchesFirstSortedIcon()
    {
        // Default container: 220×160, NameAsc, ShowTitle=true.
        // Test SUT uses IconSortService() with spacing 0 → cell 75×90, top offset 36.
        var a = MakeIcon(CreateTempFile("A.txt"));
        var b = MakeIcon(CreateTempFile("B.txt"));
        _sut.SeedContainerIcons(_container.Id, new[] { b, a }); // unsorted on purpose

        _sut.LaunchIconInContainer(_container.Id, localX: 40, localY: 60);

        Assert.Single(_sut.LaunchedPaths);
        Assert.EndsWith("A.txt", _sut.LaunchedPaths[0]); // NameAsc → A is index 0
    }

    [Fact]
    public void LaunchIconInContainer_SecondColumn_LaunchesSecondIcon()
    {
        var a = MakeIcon(CreateTempFile("A.txt"));
        var b = MakeIcon(CreateTempFile("B.txt"));
        _sut.SeedContainerIcons(_container.Id, new[] { a, b });

        // col 1 (localX in [85,160)), row 0 → index 1 → B
        _sut.LaunchIconInContainer(_container.Id, localX: 120, localY: 60);

        Assert.Single(_sut.LaunchedPaths);
        Assert.EndsWith("B.txt", _sut.LaunchedPaths[0]);
    }

    [Fact]
    public void LaunchIconInContainer_AboveGrid_LaunchesNothing()
    {
        _sut.SeedContainerIcons(_container.Id, new[] { MakeIcon(CreateTempFile("A.txt")) });

        // localY 20 is inside the title-bar band (top offset = 36) → no icon
        _sut.LaunchIconInContainer(_container.Id, localX: 40, localY: 20);

        Assert.Empty(_sut.LaunchedPaths);
    }

    [Fact]
    public void LaunchIconInContainer_EmptyCell_LaunchesNothing()
    {
        _sut.SeedContainerIcons(_container.Id, new[] { MakeIcon(CreateTempFile("A.txt")) });

        // Only 1 icon (index 0); clicking the second column (index 1) hits an empty cell
        _sut.LaunchIconInContainer(_container.Id, localX: 120, localY: 60);

        Assert.Empty(_sut.LaunchedPaths);
    }

    // ── First-Match: only first rule applied ─────────────────────

    [Fact]
    public void Created_MultipleMatchingRules_OnlyFirstRuleApplied()
    {
        var container2 = new Container { Id = Guid.NewGuid(), Name = "C2" };
        _settings.Config.Containers.Add(container2);
        _settings.Save();

        var r1 = _ruleService.Create("R1", new() { new() { Type = ConditionType.Extension, Value = ".pdf" } },
            ConditionLogic.And, _container.Id);
        var r2 = _ruleService.Create("R2", new() { new() { Type = ConditionType.Extension, Value = ".pdf" } },
            ConditionLogic.And, container2.Id);

        var path = CreateTempFile("both.pdf");
        _sut.SeedIcons(new[] { MakeIcon(path, ".pdf") });

        _sut.ProcessChangeEvent(CreatedEvent(path));

        // R1 has lower priority number (1) → applied first
        Assert.Contains(_sut.GetContainerIcons(_container.Id), i => i.FullPath == path);
        Assert.Empty(_sut.GetContainerIcons(container2.Id));
    }
}

// ── Test double ───────────────────────────────────────────────────────────

/// <summary>
/// Subclass that suppresses Win32 position writes and exposes additional
/// seeding / inspection APIs for unit tests.
/// </summary>
internal class TestableAutoOrganizeService : AutoOrganizeService
{
    public List<Dictionary<string, (int X, int Y)>> PositionWrites { get; } = new();

    public TestableAutoOrganizeService(
        DesktopReaderService   reader,
        ExclusionService       exclusion,
        FileClassifierService  classifier,
        RuleService            rules,
        SettingsService        settings,
        DesktopWatcherService  watcher,
        IconSortService        sortService,
        IconOrderService       orderService)
        : base(reader, exclusion, classifier, rules, settings, watcher, sortService, orderService) { }

    public List<string> LaunchedPaths { get; } = new();

    protected override int WritePositions(Dictionary<string, (int X, int Y)> positions)
    {
        PositionWrites.Add(new Dictionary<string, (int X, int Y)>(positions, StringComparer.OrdinalIgnoreCase));
        return positions.Count;
    }

    protected override void LaunchFile(string fullPath) => LaunchedPaths.Add(fullPath);

    public new void SeedContainerIcons(Guid containerId, IEnumerable<IconInfo> icons)
        => base.SeedContainerIcons(containerId, icons);
}
