using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using DesktopOrganizer.Interop;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-020 / F-021: Saves and restores named Layout snapshots.
/// Each Layout is stored as a separate JSON file under
/// %APPDATA%\DesktopOrganizer\layouts\{id}.json.
/// </summary>
public class LayoutService
{
    private const int MaxLayouts = 10;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    private readonly string             _layoutsDir;
    private readonly IconSortService    _sortService;
    private readonly DesktopGridService? _desktopGrid;

    /// <summary>Convenience ctor — builds its own IconSortService (kept for existing callers/tests).</summary>
    public LayoutService(SettingsService settings)
        : this(settings, new IconSortService(settings)) { }

    /// <summary>
    /// Production ctor. Takes the SAME <see cref="IconSortService"/> instance as
    /// <see cref="AutoOrganizeService"/> so restore uses one grid policy for the whole app
    /// (F-021 previously duplicated the layout math and drifted to the raw constants).
    /// </summary>
    public LayoutService(SettingsService settings, IconSortService sortService,
        DesktopGridService? desktopGrid = null)
    {
        _layoutsDir  = settings.LayoutsDir;
        _sortService = sortService;
        _desktopGrid = desktopGrid;
    }

    // ── F-020: Capture & Save ────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Layout"/> snapshot from the current containers and
    /// their saved icon orders.  Does NOT persist yet — call <see cref="Save"/> after.
    /// </summary>
    public Layout Capture(string name, SettingsService settings)
    {
        var screenW = (int)SystemParameters.PrimaryScreenWidth;
        var screenH = (int)SystemParameters.PrimaryScreenHeight;

        var layout = new Layout
        {
            Name         = name.Trim(),
            SavedAt      = DateTime.UtcNow,
            ScreenWidth  = screenW,
            ScreenHeight = screenH
        };

        foreach (var c in settings.Config.Containers)
        {
            var snap = new LayoutContainerSnapshot
            {
                ContainerId   = c.Id,
                ContainerName = c.Name,
                X             = c.X,
                Y             = c.Y,
                Width         = c.Width,
                Height        = c.Height,
                Style         = c.Style,
                SortMode      = c.SortMode,
                Icons         = c.IconOrder.Select(e => new LayoutIconPlacement
                {
                    IconPath   = e.IconPath,
                    OrderIndex = e.OrderIndex,
                    X          = 0,
                    Y          = 0
                }).ToList()
            };
            layout.Containers.Add(snap);
        }

        return layout;
    }

    /// <summary>Persists a layout to <c>layouts/{id}.json</c>.</summary>
    public void Save(Layout layout)
    {
        Directory.CreateDirectory(_layoutsDir);
        var path = LayoutPath(layout.Id);
        var json = JsonSerializer.Serialize(layout, JsonOpts);
        var tmp  = path + ".tmp";
        File.WriteAllText(tmp, json, Encoding.UTF8);
        File.Move(tmp, path, overwrite: true);
        LogService.Instance.Info("F-020", $"Layout saved: '{layout.Name}' ({layout.Id})");
    }

    // ── F-021: List & Load ────────────────────────────────────────

    /// <summary>
    /// Returns all saved layouts ordered by <see cref="Layout.SavedAt"/> descending.
    /// Files that cannot be parsed are silently skipped.
    /// </summary>
    public IReadOnlyList<Layout> GetAll()
    {
        if (!Directory.Exists(_layoutsDir)) return Array.Empty<Layout>();

        var result = new List<Layout>();
        foreach (var file in Directory.EnumerateFiles(_layoutsDir, "*.json"))
        {
            if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var json = File.ReadAllText(file, Encoding.UTF8);
                var layout = JsonSerializer.Deserialize<Layout>(json, JsonOpts);
                if (layout is not null) result.Add(layout);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("F-021", $"Cannot read layout '{file}': {ex.Message}");
            }
        }

        result.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));
        return result;
    }

    /// <summary>Loads a single layout by ID; returns null if not found or corrupt.</summary>
    public Layout? Load(Guid id)
    {
        var path = LayoutPath(id);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<Layout>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn("F-021", $"Cannot load layout {id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Deletes the layout file for the given ID.</summary>
    public bool Delete(Guid id)
    {
        var path = LayoutPath(id);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        LogService.Instance.Info("F-021", $"Layout deleted: {id}");
        return true;
    }

    // ── F-021: Restore ────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="layout"/> to <paramref name="settings"/> in-memory.
    /// Container positions are scaled if the current screen resolution differs from the saved one.
    /// Icon placements are moved on the physical desktop; missing icons are skipped.
    /// </summary>
    /// <returns>List of icon paths that were in the layout but not found on the current desktop.</returns>
    public IReadOnlyList<string> Restore(Layout layout, SettingsService settings,
        IEnumerable<IconInfo> currentDesktopIcons)
    {
        var currentW = (int)SystemParameters.PrimaryScreenWidth;
        var currentH = (int)SystemParameters.PrimaryScreenHeight;
        double scaleX = layout.ScreenWidth  > 0 ? (double)currentW / layout.ScreenWidth  : 1.0;
        double scaleY = layout.ScreenHeight > 0 ? (double)currentH / layout.ScreenHeight : 1.0;

        bool resolutionMismatch = Math.Abs(scaleX - 1.0) > 0.01 || Math.Abs(scaleY - 1.0) > 0.01;
        if (resolutionMismatch)
            LogService.Instance.Warn("F-021", $"Resolution mismatch — scaling ×{scaleX:F2}/×{scaleY:F2}");

        // Build lookup: fullPath (case-insensitive) → IconInfo
        var iconLookup = currentDesktopIcons.ToDictionary(
            i => i.FullPath, i => i, StringComparer.OrdinalIgnoreCase);

        // Replace containers in settings
        settings.Config.Containers.Clear();

        var missing = new List<string>();
        var positionMap = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);

        foreach (var snap in layout.Containers)
        {
            var c = new Container
            {
                Id       = snap.ContainerId,
                Name     = snap.ContainerName,
                X        = snap.X * scaleX,
                Y        = snap.Y * scaleY,
                Width    = snap.Width  * scaleX,
                Height   = snap.Height * scaleY,
                Style    = snap.Style,
                SortMode = snap.SortMode
            };

            // One grid per container, computed from the SAME source of truth as F-010's live
            // layout — hoisted out of the loop (it used to be recomputed per icon).
            var grid = _sortService.ComputeGrid(c);

            // Restore icon order entries; track positions for Win32 call
            c.IconOrder.Clear();
            foreach (var placement in snap.Icons.OrderBy(p => p.OrderIndex))
            {
                c.IconOrder.Add(new IconOrderEntry
                {
                    IconPath   = placement.IconPath,
                    OrderIndex = placement.OrderIndex,
                    SavedAt    = layout.SavedAt
                });

                if (!iconLookup.ContainsKey(placement.IconPath))
                {
                    missing.Add(placement.IconPath);
                    continue;
                }

                var icon       = iconLookup[placement.IconPath];
                var displayName = icon.Extension == ".lnk"
                    ? Path.GetFileNameWithoutExtension(icon.FileName)
                    : icon.FileName;

                positionMap[displayName] = grid.PositionOf(placement.OrderIndex);
            }

            settings.Config.Containers.Add(c);
        }

        settings.Save();

        // Move desktop icons to restored positions
        if (positionMap.Count > 0)
            WritePositions(positionMap);

        if (missing.Count > 0)
            LogService.Instance.Warn("F-021", $"Restore: {missing.Count} icon(s) not found on current desktop");

        return missing;
    }

    /// <summary>Extracted for testability — calls the Win32 position writer.</summary>
    protected virtual void WritePositions(Dictionary<string, (int X, int Y)> positions)
    {
        _desktopGrid?.EnsureDisabled();   // F-010 item 8, same guard as the live layout path
        DesktopIconInterop.WriteIconPositions(positions);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private string LayoutPath(Guid id) => Path.Combine(_layoutsDir, $"{id}.json");

    public int GetCount()
    {
        if (!Directory.Exists(_layoutsDir)) return 0;
        return Directory.EnumerateFiles(_layoutsDir, "*.json")
            .Count(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }
}
