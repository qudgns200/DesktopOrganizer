using DesktopOrganizer.Interop;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-010: Sorts icons by 9 criteria and computes their absolute grid positions
/// inside a Container.  Pure logic — no I/O, no Win32 calls.
/// </summary>
public class IconSortService
{
    // Base grid cell size (pixels at 100% DPI), used as a FLOOR. The real desktop grid
    // cell (measured via LVM_GETITEMSPACING) is used when it is larger, so that with the
    // shell's "Align icons to grid" active no two icons snap into the same cell (which the
    // shell would otherwise resolve by scattering them — the reported move-scatter bug).
    // The vertical cell is taller than the horizontal one because a desktop icon reserves
    // space for its (often two-line) label.
    public const int IconCellWidth  = 75;
    public const int IconCellHeight = 90;

    // Inset from the container edge
    public const int PaddingX = 10;
    public const int PaddingY = 10;

    // Space reserved for the title bar when ShowTitle=true
    public const int TitleBarHeight = 26;

    // Optional: supplies the user-configured icon spacing (F-010). Null in unit tests.
    private readonly SettingsService? _settings;

    // Optional: measures the real desktop grid cell size in DIPs. Null in unit tests
    // (→ base constants only). Wired to DesktopIconInterop.GetDesktopGridCellSize in production.
    private readonly Func<(double Cx, double Cy)?>? _gridCellProvider;

    /// <summary>Parameterless ctor — spacing 0, no grid measurement (used by unit tests).</summary>
    public IconSortService() { }

    /// <summary>Production ctor — reads spacing from settings and measures the real desktop grid.</summary>
    public IconSortService(SettingsService settings)
        : this(settings, DesktopIconInterop.GetDesktopGridCellSize) { }

    /// <summary>Full ctor — the grid-cell provider is injectable for unit testing.</summary>
    internal IconSortService(SettingsService? settings, Func<(double Cx, double Cy)?>? gridCellProvider)
    {
        _settings         = settings;
        _gridCellProvider = gridCellProvider;
    }

    /// <summary>User-configured spacing added to each base cell (F-010 "정렬 간격").</summary>
    private int Spacing => _settings?.Config.Settings.IconSpacingPx ?? 0;

    /// <summary>
    /// Resolves the icon grid for <paramref name="container"/> — the single source of truth for
    /// placement (F-010 item 7). Re-measured each call so it stays correct across DPI /
    /// resolution changes (the underlying query is one cheap window message).
    ///
    /// Two properties make the result safe against the shell's "Align icons to grid", which
    /// re-quantizes whatever coordinates we send to its own cell:
    ///   1. The pitch is an exact INTEGER MULTIPLE of the measured cell, so consecutive icons
    ///      land in consecutive cells instead of colliding and being scattered by the shell.
    ///      (The previous fix added the user spacing on top of the measured cell, making the
    ///      pitch a non-multiple — which is what re-introduced the scatter.)
    ///   2. The origin is snapped to a cell boundary, so the whole block cannot sit half a cell
    ///      off the container it belongs to.
    /// </summary>
    public IconGridLayout ComputeGrid(Container container)
    {
        var (cellX, cellY) = MeasureShellCell();

        double pitchX = QuantizePitch(IconCellWidth  + Spacing, cellX);
        double pitchY = QuantizePitch(IconCellHeight + Spacing, cellY);

        double rawX = container.X + PaddingX;
        double rawY = container.Y + PaddingY + (container.Style.ShowTitle ? TitleBarHeight : 0);

        // Ceiling (not Round) so the first row can never land above the content box.
        double originX = AlignToCell(rawX, cellX);
        double originY = AlignToCell(rawY, cellY);

        // Derive icons-per-row from the ALIGNED origin, otherwise the alignment inset can push
        // the last column past the container's right edge.
        double availableWidth = (container.X + container.Width - PaddingX) - originX;
        int iconsPerRow = Math.Max(1, (int)(availableWidth / pitchX));

        return new IconGridLayout(originX, originY, pitchX, pitchY, iconsPerRow, cellX, cellY);
    }

    /// <summary>
    /// Rounds <paramref name="desiredPitch"/> to the nearest whole number of shell cells
    /// (minimum one cell). Returns the desired pitch unchanged when the cell is unknown.
    /// Round rather than Ceiling: at the defaults (base 75 + spacing 8 = 83, cell 80) Ceiling
    /// would jump to 2 cells = 160 and halve icons-per-row for every existing user.
    /// A consequence is that IconSpacingPx becomes a coarse, cell-granular control.
    /// </summary>
    private static double QuantizePitch(double desiredPitch, double? shellCell)
    {
        if (shellCell is not > 0) return desiredPitch;
        double cell  = shellCell.Value;
        int    cells = Math.Max(1, (int)Math.Round(desiredPitch / cell, MidpointRounding.AwayFromZero));
        return cells * cell;
    }

    private static double AlignToCell(double value, double? shellCell)
        => shellCell is > 0 ? Math.Ceiling(value / shellCell.Value) * shellCell.Value : value;

    /// <summary>Measures the real desktop grid cell in DIPs; (null, null) when unavailable.</summary>
    private (double? Cx, double? Cy) MeasureShellCell()
    {
        (double Cx, double Cy)? grid;
        try { grid = _gridCellProvider?.Invoke(); }
        catch (Exception ex)
        {
            LogService.Instance.Warn("F-010", $"Desktop grid measurement threw: {ex.Message}");
            grid = null;
        }

        if (grid is not { } g) return (null, null);
        return (g.Cx > 0 ? g.Cx : null, g.Cy > 0 ? g.Cy : null);
    }

    // ── F-010: Sort ───────────────────────────────────────────────

    /// <summary>
    /// Returns a new list sorted by <paramref name="mode"/>.
    /// Manual mode orders by <see cref="IconInfo.OrderIndex"/> (unset items go last).
    /// </summary>
    public IList<IconInfo> Sort(IEnumerable<IconInfo> icons, SortMode mode)
    {
        var list = icons.ToList();
        return mode switch
        {
            SortMode.NameAsc      => [.. list.OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.NameDesc     => [.. list.OrderByDescending(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.Extension    => [.. list.OrderBy(i => i.Extension, StringComparer.OrdinalIgnoreCase)
                                           .ThenBy(i => i.FileName,    StringComparer.OrdinalIgnoreCase)],
            SortMode.FileType     => [.. list.OrderBy(i => i.Category)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.CreatedAsc   => [.. list.OrderBy(i => i.CreatedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.CreatedDesc  => [.. list.OrderByDescending(i => i.CreatedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.ModifiedAsc  => [.. list.OrderBy(i => i.ModifiedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.ModifiedDesc => [.. list.OrderByDescending(i => i.ModifiedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.Manual       => [.. list.OrderBy(i => i.OrderIndex >= 0 ? i.OrderIndex : int.MaxValue)],
            _                     => list
        };
    }

    // ── F-010: Grid layout ────────────────────────────────────────

    /// <summary>
    /// Assigns absolute screen (X, Y) coordinates and <see cref="IconInfo.OrderIndex"/> to
    /// every icon in <paramref name="sortedIcons"/>, starting from the container's top-left
    /// corner with padding.  Wraps to the next row when container width is exhausted.
    /// Also sets <see cref="IconInfo.AssignedContainerId"/>.
    /// </summary>
    public void ComputePositions(Container container, IList<IconInfo> sortedIcons)
    {
        var grid = ComputeGrid(container);

        for (int i = 0; i < sortedIcons.Count; i++)
        {
            var (x, y) = grid.PositionOf(i);
            sortedIcons[i].X                   = x;
            sortedIcons[i].Y                   = y;
            sortedIcons[i].OrderIndex          = i;
            sortedIcons[i].AssignedContainerId = container.Id;
        }
    }

    /// <summary>Convenience: sort then compute positions in one call.</summary>
    public IList<IconInfo> SortAndComputePositions(Container container, IEnumerable<IconInfo> icons)
    {
        var sorted = Sort(icons, container.SortMode);
        ComputePositions(container, sorted);
        return sorted;
    }

    // ── Hit testing (for double-click launch) ─────────────────────

    /// <summary>
    /// Maps a point in container-local coordinates (0,0 = container top-left) to the
    /// index of the icon occupying that grid cell, using the same layout as
    /// <see cref="ComputePositions"/>.  Returns null when the point is outside the
    /// icon grid or lands on a cell with no icon.
    /// </summary>
    public int? HitTestIndex(Container container, double localX, double localY, int iconCount)
    {
        if (iconCount <= 0) return null;

        // Consume the same grid as ComputePositions so the two can never disagree.
        // The grid origin is absolute, so convert the container-local point to absolute.
        var grid = ComputeGrid(container);
        double absX = container.X + localX;
        double absY = container.Y + localY;

        // Points left of / above the aligned origin (inside the padding, or the title bar)
        // belong to no cell.
        if (absX < grid.OriginX || absY < grid.OriginY) return null;

        int col = (int)((absX - grid.OriginX) / grid.PitchX);
        int row = (int)((absY - grid.OriginY) / grid.PitchY);
        if (col < 0 || col >= grid.IconsPerRow) return null;

        int index = row * grid.IconsPerRow + col;
        return index >= 0 && index < iconCount ? index : null;
    }
}
