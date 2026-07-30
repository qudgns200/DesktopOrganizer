using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

/// <summary>
/// F-010 item 9: verification classification. This is what makes the log honest — the old code
/// counted every sent message as a success, so "N/N repositioned" was reported even while the
/// shell was relocating every icon.
/// </summary>
public class IconPlacementVerifierTests
{
    private const double CellX = 80;
    private const double CellY = 80;

    [Fact]
    public void Classify_SamePosition_IsExact()
    {
        var outcome = IconPlacementVerifier.Classify((100, 200), (100, 200), CellX, CellY);

        Assert.Equal(PlacementOutcome.Exact, outcome);
        Assert.True(IconPlacementVerifier.IsLanded(outcome));
    }

    [Fact]
    public void Classify_WithinHalfCell_IsSnapAdjusted_AndCountsAsLanded()
    {
        // A snap nudge of 30px with an 80px cell (tolerance 40) is expected and harmless.
        var outcome = IconPlacementVerifier.Classify((100, 200), (130, 200), CellX, CellY);

        Assert.Equal(PlacementOutcome.SnapAdjusted, outcome);
        Assert.True(IconPlacementVerifier.IsLanded(outcome));
    }

    [Fact]
    public void Classify_BeyondHalfCell_IsRelocated_AndDoesNotCount()
    {
        // 200px away with an 80px cell — the shell moved it elsewhere. This is the scatter.
        var outcome = IconPlacementVerifier.Classify((100, 200), (300, 200), CellX, CellY);

        Assert.Equal(PlacementOutcome.Relocated, outcome);
        Assert.False(IconPlacementVerifier.IsLanded(outcome));
    }

    [Fact]
    public void Classify_BeyondHalfCellVertically_IsRelocated()
    {
        var outcome = IconPlacementVerifier.Classify((100, 200), (100, 400), CellX, CellY);

        Assert.Equal(PlacementOutcome.Relocated, outcome);
    }

    [Fact]
    public void Classify_SharedPosition_IsCollided_EvenWhenExact()
    {
        // Two icons stacked in one cell is a failure regardless of matching our request.
        var outcome = IconPlacementVerifier.Classify(
            (100, 200), (100, 200), CellX, CellY, sharesPositionWithOther: true);

        Assert.Equal(PlacementOutcome.Collided, outcome);
        Assert.False(IconPlacementVerifier.IsLanded(outcome));
    }

    [Fact]
    public void Classify_MissingFromReadback_IsUnverified()
    {
        var outcome = IconPlacementVerifier.Classify((100, 200), null, CellX, CellY);

        Assert.Equal(PlacementOutcome.Unverified, outcome);
        Assert.False(IconPlacementVerifier.IsLanded(outcome));
    }

    [Fact]
    public void Classify_UnknownCell_UsesMinimumPixelTolerance()
    {
        // Cell 0 (measurement unavailable) → 2px floor, so 1px off is still fine…
        Assert.Equal(PlacementOutcome.SnapAdjusted,
            IconPlacementVerifier.Classify((100, 200), (101, 200), 0, 0));

        // …but 10px is a relocation.
        Assert.Equal(PlacementOutcome.Relocated,
            IconPlacementVerifier.Classify((100, 200), (110, 200), 0, 0));
    }
}
