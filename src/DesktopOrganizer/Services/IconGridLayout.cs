namespace DesktopOrganizer.Services;

/// <summary>
/// F-010: The resolved icon grid for one Container — the single source of truth for icon
/// placement, shared by <see cref="IconSortService.ComputePositions"/>,
/// <see cref="IconSortService.HitTestIndex"/> and F-021 Layout restore. Previously this math
/// was written out in three places, which is how the restore path silently drifted back to
/// the raw constants.
///
/// All values are in WPF DIPs and deliberately kept fractional: the pitch is an exact integer
/// multiple of the measured shell grid cell, and rounding happens ONCE in
/// <see cref="PositionOf"/>. Pre-truncating the cell to an int drifts about 1px per column
/// cumulatively at fractional DPI (at 150% an 80px cell is 53.333 DIP, not 54).
/// </summary>
public readonly record struct IconGridLayout(
    double OriginX,
    double OriginY,
    double PitchX,
    double PitchY,
    int    IconsPerRow,
    double? ShellCellX,
    double? ShellCellY)
{
    /// <summary>True when the real desktop grid cell was measured (diagnostics/logging).</summary>
    public bool IsGridAligned => ShellCellX is > 0 && ShellCellY is > 0;

    /// <summary>
    /// Absolute desktop position (DIPs, rounded) of the icon at <paramref name="index"/>,
    /// laid out left-to-right then top-to-bottom.
    /// </summary>
    public (int X, int Y) PositionOf(int index)
    {
        int perRow = Math.Max(1, IconsPerRow);
        int col    = index % perRow;
        int row    = index / perRow;

        return ((int)Math.Round(OriginX + col * PitchX),
                (int)Math.Round(OriginY + row * PitchY));
    }
}
