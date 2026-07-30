namespace DesktopOrganizer.Services;

/// <summary>How the shell actually treated one icon we tried to position (F-010 item 9).</summary>
public enum PlacementOutcome
{
    /// <summary>Landed exactly where we asked.</summary>
    Exact,
    /// <summary>Nudged within half a grid cell — expected when align-to-grid is on and our pitch is right.</summary>
    SnapAdjusted,
    /// <summary>Moved more than half a cell away — this is the scatter signature.</summary>
    Relocated,
    /// <summary>Ended up sharing a position with another icon — the "two icons in one cell" symptom.</summary>
    Collided,
    /// <summary>Could not be found in the post-write read-back.</summary>
    Unverified
}

/// <summary>
/// F-010 item 9: compares requested vs actual icon positions after a write so the log can state
/// the truth. Previously <c>WriteIconPositions</c> discarded the Win32 result and counted every
/// message as a success, which made every "N/N repositioned" log line meaningless even while the
/// user watched the icons scatter.
///
/// Pure logic, no interop — fully unit-testable. All coordinates are PHYSICAL pixels.
/// </summary>
public static class IconPlacementVerifier
{
    /// <summary>Icons within this fraction of a cell are treated as harmless snap nudges.</summary>
    private const double ToleranceCellFraction = 0.5;

    /// <summary>Floor tolerance in px, used when the grid cell is unknown or tiny.</summary>
    private const int ToleranceMinPx = 2;

    /// <summary>
    /// Classifies one icon. <paramref name="actual"/> is null when the icon was not found in the
    /// read-back. <paramref name="sharesPositionWithOther"/> marks icons that ended up stacked.
    /// </summary>
    public static PlacementOutcome Classify(
        (int X, int Y) requested,
        (int X, int Y)? actual,
        double cellX,
        double cellY,
        bool sharesPositionWithOther = false)
    {
        if (actual is null) return PlacementOutcome.Unverified;

        var a = actual.Value;
        if (a.X == requested.X && a.Y == requested.Y)
            return sharesPositionWithOther ? PlacementOutcome.Collided : PlacementOutcome.Exact;

        if (sharesPositionWithOther) return PlacementOutcome.Collided;

        double tolX = Math.Max(ToleranceMinPx, cellX * ToleranceCellFraction);
        double tolY = Math.Max(ToleranceMinPx, cellY * ToleranceCellFraction);

        bool within = Math.Abs(a.X - requested.X) <= tolX
                   && Math.Abs(a.Y - requested.Y) <= tolY;

        return within ? PlacementOutcome.SnapAdjusted : PlacementOutcome.Relocated;
    }

    /// <summary>True when the icon is considered successfully placed (counts toward the result).</summary>
    public static bool IsLanded(PlacementOutcome outcome)
        => outcome is PlacementOutcome.Exact or PlacementOutcome.SnapAdjusted;
}
