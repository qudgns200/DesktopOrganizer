using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class IconSortServiceTests
{
    private readonly IconSortService _sut = new();

    // ── Helpers ──────────────────────────────────────────────────

    private static IconInfo MakeIcon(
        string name,
        string ext          = ".txt",
        FileCategory cat    = FileCategory.Document,
        DateTime? created   = null,
        DateTime? modified  = null,
        int orderIndex      = -1) => new()
    {
        FileName   = name,
        FullPath   = $"C:\\Desktop\\{name}",
        Extension  = ext,
        Category   = cat,
        CreatedAt  = created  ?? DateTime.UtcNow,
        ModifiedAt = modified ?? DateTime.UtcNow,
        OrderIndex = orderIndex
    };

    private static Container MakeContainer(
        double x = 100, double y = 100,
        double w = 300, double h = 200,
        bool showTitle = true,
        SortMode mode = SortMode.NameAsc) => new()
    {
        X = x, Y = y, Width = w, Height = h,
        SortMode = mode,
        Style    = new ContainerStyle { ShowTitle = showTitle }
    };

    // ── Sort: NameAsc ────────────────────────────────────────────

    [Fact]
    public void Sort_ByNameAsc_OrdersAlphabetically()
    {
        var icons = new[] { MakeIcon("Zebra.txt"), MakeIcon("Apple.txt"), MakeIcon("Mango.txt") };
        var result = _sut.Sort(icons, SortMode.NameAsc);
        Assert.Equal(new[] { "Apple.txt", "Mango.txt", "Zebra.txt" }, result.Select(i => i.FileName).ToArray());
    }

    [Fact]
    public void Sort_ByNameAsc_IsCaseInsensitive()
    {
        var icons = new[] { MakeIcon("zebra.txt"), MakeIcon("Apple.txt") };
        var result = _sut.Sort(icons, SortMode.NameAsc);
        Assert.Equal("Apple.txt", result[0].FileName);
    }

    // ── Sort: NameDesc ───────────────────────────────────────────

    [Fact]
    public void Sort_ByNameDesc_OrdersReverseAlphabetically()
    {
        var icons = new[] { MakeIcon("Apple.txt"), MakeIcon("Zebra.txt"), MakeIcon("Mango.txt") };
        var result = _sut.Sort(icons, SortMode.NameDesc);
        Assert.Equal(new[] { "Zebra.txt", "Mango.txt", "Apple.txt" }, result.Select(i => i.FileName).ToArray());
    }

    // ── Sort: Extension ──────────────────────────────────────────

    [Fact]
    public void Sort_ByExtension_GroupsByExtensionThenName()
    {
        var icons = new[]
        {
            MakeIcon("Bravo.png",  ".png"),
            MakeIcon("Alpha.txt",  ".txt"),
            MakeIcon("Charlie.png",".png"),
            MakeIcon("Delta.txt",  ".txt")
        };
        var result = _sut.Sort(icons, SortMode.Extension);
        Assert.Equal(".png", result[0].Extension);
        Assert.Equal(".png", result[1].Extension);
        Assert.Equal("Bravo.png",   result[0].FileName);
        Assert.Equal("Charlie.png", result[1].FileName);
        Assert.Equal(".txt", result[2].Extension);
        Assert.Equal(".txt", result[3].Extension);
    }

    // ── Sort: FileType ───────────────────────────────────────────

    [Fact]
    public void Sort_ByFileType_GroupsByCategory()
    {
        var icons = new[]
        {
            MakeIcon("video.mp4",  cat: FileCategory.Video),
            MakeIcon("doc.pdf",    cat: FileCategory.Document),
            MakeIcon("img.png",    cat: FileCategory.Image)
        };
        var result = _sut.Sort(icons, SortMode.FileType);

        // Document < Image < Video in enum order
        Assert.Equal(FileCategory.Document, result[0].Category);
        Assert.Equal(FileCategory.Image,    result[1].Category);
        Assert.Equal(FileCategory.Video,    result[2].Category);
    }

    // ── Sort: CreatedAsc / CreatedDesc ───────────────────────────

    [Fact]
    public void Sort_ByCreatedAsc_OldestFirst()
    {
        var base_ = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var icons = new[]
        {
            MakeIcon("New.txt",  created: base_.AddDays(2)),
            MakeIcon("Old.txt",  created: base_),
            MakeIcon("Mid.txt",  created: base_.AddDays(1))
        };
        var result = _sut.Sort(icons, SortMode.CreatedAsc);
        Assert.Equal(new[] { "Old.txt", "Mid.txt", "New.txt" }, result.Select(i => i.FileName).ToArray());
    }

    [Fact]
    public void Sort_ByCreatedDesc_NewestFirst()
    {
        var base_ = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var icons = new[]
        {
            MakeIcon("New.txt", created: base_.AddDays(2)),
            MakeIcon("Old.txt", created: base_)
        };
        var result = _sut.Sort(icons, SortMode.CreatedDesc);
        Assert.Equal("New.txt", result[0].FileName);
    }

    // ── Sort: ModifiedAsc / ModifiedDesc ─────────────────────────

    [Fact]
    public void Sort_ByModifiedAsc_LeastRecentlyModifiedFirst()
    {
        var base_ = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var icons = new[]
        {
            MakeIcon("Recent.txt",  modified: base_.AddDays(5)),
            MakeIcon("Ancient.txt", modified: base_)
        };
        var result = _sut.Sort(icons, SortMode.ModifiedAsc);
        Assert.Equal("Ancient.txt", result[0].FileName);
    }

    [Fact]
    public void Sort_ByModifiedDesc_MostRecentlyModifiedFirst()
    {
        var base_ = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var icons = new[]
        {
            MakeIcon("Recent.txt",  modified: base_.AddDays(5)),
            MakeIcon("Ancient.txt", modified: base_)
        };
        var result = _sut.Sort(icons, SortMode.ModifiedDesc);
        Assert.Equal("Recent.txt", result[0].FileName);
    }

    // ── Sort: Manual ─────────────────────────────────────────────

    [Fact]
    public void Sort_Manual_OrdersByExistingOrderIndex()
    {
        var icons = new[]
        {
            MakeIcon("Third.txt",  orderIndex: 2),
            MakeIcon("First.txt",  orderIndex: 0),
            MakeIcon("Second.txt", orderIndex: 1)
        };
        var result = _sut.Sort(icons, SortMode.Manual);
        Assert.Equal(new[] { "First.txt", "Second.txt", "Third.txt" },
            result.Select(i => i.FileName).ToArray());
    }

    [Fact]
    public void Sort_Manual_IconsWithNegativeIndex_GoToEnd()
    {
        var icons = new[]
        {
            MakeIcon("NoIndex.txt",  orderIndex: -1),
            MakeIcon("Indexed.txt",  orderIndex: 0)
        };
        var result = _sut.Sort(icons, SortMode.Manual);
        Assert.Equal("Indexed.txt",  result[0].FileName);
        Assert.Equal("NoIndex.txt",  result[1].FileName);
    }

    // ── Sort: Empty list ─────────────────────────────────────────

    [Fact]
    public void Sort_EmptyList_ReturnsEmpty()
    {
        var result = _sut.Sort([], SortMode.NameAsc);
        Assert.Empty(result);
    }

    // ── ComputePositions: basic ───────────────────────────────────

    [Fact]
    public void ComputePositions_FirstIconStartsAtContainerTopLeft()
    {
        var c     = MakeContainer(x: 100, y: 200, showTitle: false);
        var icons = new List<IconInfo> { MakeIcon("A.txt") };

        _sut.ComputePositions(c, icons);

        Assert.Equal(100 + IconSortService.PaddingX, icons[0].X);
        Assert.Equal(200 + IconSortService.PaddingY, icons[0].Y);
    }

    [Fact]
    public void ComputePositions_WithTitleBar_AddsOffset()
    {
        var c     = MakeContainer(x: 0, y: 0, showTitle: true);
        var icons = new List<IconInfo> { MakeIcon("A.txt") };

        _sut.ComputePositions(c, icons);

        Assert.Equal(IconSortService.PaddingY + IconSortService.TitleBarHeight, icons[0].Y);
    }

    [Fact]
    public void ComputePositions_SetsOrderIndex()
    {
        var c     = MakeContainer();
        var icons = new List<IconInfo> { MakeIcon("A"), MakeIcon("B"), MakeIcon("C") };

        _sut.ComputePositions(c, icons);

        Assert.Equal(0, icons[0].OrderIndex);
        Assert.Equal(1, icons[1].OrderIndex);
        Assert.Equal(2, icons[2].OrderIndex);
    }

    [Fact]
    public void ComputePositions_SetsAssignedContainerId()
    {
        var c     = MakeContainer();
        var icons = new List<IconInfo> { MakeIcon("A.txt") };

        _sut.ComputePositions(c, icons);

        Assert.Equal(c.Id, icons[0].AssignedContainerId);
    }

    [Fact]
    public void ComputePositions_WrapsToNextRow()
    {
        // Container is exactly one icon-cell wide → every icon gets its own row
        var c = MakeContainer(x: 0, y: 0,
            w: IconSortService.PaddingX * 2 + IconSortService.IconCellWidth,
            showTitle: false);

        var icons = new List<IconInfo> { MakeIcon("A"), MakeIcon("B"), MakeIcon("C") };
        _sut.ComputePositions(c, icons);

        Assert.Equal(icons[0].X, icons[1].X);   // same column
        Assert.Equal(icons[0].X, icons[2].X);
        Assert.True(icons[1].Y > icons[0].Y);   // each on its own row
        Assert.True(icons[2].Y > icons[1].Y);
    }

    [Fact]
    public void ComputePositions_MultipleIconsPerRow()
    {
        // Container fits exactly 3 icons per row
        var c = MakeContainer(x: 0, y: 0,
            w: IconSortService.PaddingX * 2 + IconSortService.IconCellWidth * 3,
            showTitle: false);

        var icons = Enumerable.Range(0, 6).Select(i => MakeIcon($"Icon{i}")).ToList();
        _sut.ComputePositions(c, icons);

        // First 3 → row 0 (same Y), last 3 → row 1
        Assert.Equal(icons[0].Y, icons[1].Y);
        Assert.Equal(icons[0].Y, icons[2].Y);
        Assert.True(icons[3].Y > icons[0].Y);
        Assert.Equal(icons[3].Y, icons[4].Y);
        Assert.Equal(icons[3].Y, icons[5].Y);
    }

    // ── SortAndComputePositions ───────────────────────────────────

    [Fact]
    public void SortAndComputePositions_SortsAndAssignsPositions()
    {
        var c = MakeContainer(mode: SortMode.NameAsc, showTitle: false);
        var icons = new[] { MakeIcon("Zebra.txt"), MakeIcon("Apple.txt") };

        var result = _sut.SortAndComputePositions(c, icons);

        Assert.Equal("Apple.txt", result[0].FileName);
        Assert.Equal(0, result[0].OrderIndex);
        Assert.Equal(c.Id, result[0].AssignedContainerId);
    }
}
