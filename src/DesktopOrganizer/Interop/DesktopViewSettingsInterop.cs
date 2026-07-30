using System.Runtime.InteropServices;

namespace DesktopOrganizer.Interop;

/// <summary>
/// Snapshot of the two desktop view settings that relocate our icons (F-010 item 8).
/// Public because <see cref="Services.DesktopGridService"/> exposes it on a protected seam.
/// </summary>
public readonly record struct DesktopViewState(bool SnapToGrid, bool AutoArrange);

/// <summary>
/// Raw P/Invoke for reading and clearing the desktop's "Align icons to grid" and
/// "Auto arrange icons" settings. Policy (when to read, whether to send, verification,
/// logging, throttling) lives in <see cref="Services.DesktopGridService"/> — this type only
/// talks to Win32 and returns what it saw.
///
/// Never moves, copies or deletes files. It does change a desktop VIEW setting, which is a
/// deliberate, user-approved exception documented in F-010 item 8 / F-023.
/// </summary>
internal static class DesktopViewSettingsInterop
{
    private const int LVM_FIRST                       = 0x1000;
    private const int LVM_SETEXTENDEDLISTVIEWSTYLE    = LVM_FIRST + 54;   // 0x1036
    private const int LVM_GETEXTENDEDLISTVIEWSTYLE    = LVM_FIRST + 55;   // 0x1037
    private const int LVS_EX_SNAPTOGRID               = 0x00080000;

    private const int GWL_STYLE       = -16;
    private const int LVS_AUTOARRANGE = 0x0100;

    private const int WM_COMMAND = 0x0111;
    // Undocumented but stable Vista→Win11 shell View-menu command IDs on SHELLDLL_DefView.
    private const int IdmToggleAutoArrange = 0x7041;
    private const int IdmToggleAlignToGrid = 0x7042;

    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint SendMessageTimeoutMs = 2000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeoutMs, out IntPtr result);

    /// <summary>
    /// Reads both settings, or null when the desktop cannot be reached (window missing,
    /// explorer hung, or the message was filtered by UIPI). Null means UNKNOWN — callers must
    /// then send nothing, because the toggle commands would otherwise switch a setting ON.
    /// </summary>
    public static DesktopViewState? TryReadState()
    {
        var (_, listView) = DesktopIconInterop.FindDesktopViewWindows();
        if (listView == IntPtr.Zero) return null;

        if (!TrySend(listView, LVM_GETEXTENDEDLISTVIEWSTYLE, IntPtr.Zero, IntPtr.Zero, out var exStyle))
            return null;

        int style = GetWindowLong(listView, GWL_STYLE);

        return new DesktopViewState(
            SnapToGrid:  (exStyle.ToInt64() & LVS_EX_SNAPTOGRID) != 0,
            AutoArrange: (style & LVS_AUTOARRANGE) != 0);
    }

    /// <summary>Toggles "Auto arrange icons" via the shell's own View-menu command.</summary>
    public static bool ToggleAutoArrange() => SendShellViewCommand(IdmToggleAutoArrange);

    /// <summary>Toggles "Align icons to grid" via the shell's own View-menu command.</summary>
    public static bool ToggleAlignToGrid() => SendShellViewCommand(IdmToggleAlignToGrid);

    /// <summary>
    /// Sends a View-menu command to SHELLDLL_DefView. Going through DefView (rather than poking
    /// the ListView style directly) keeps the shell's own state and its View context-menu
    /// checkmarks consistent, and persists the change to the desktop's shell bag so it survives
    /// an explorer restart. These commands are TOGGLES — read the state first.
    /// </summary>
    private static bool SendShellViewCommand(int commandId)
    {
        var (defView, _) = DesktopIconInterop.FindDesktopViewWindows();
        if (defView == IntPtr.Zero) return false;

        return TrySend(defView, WM_COMMAND, new IntPtr(commandId), IntPtr.Zero, out _);
    }

    /// <summary>
    /// Documented fallback: clears LVS_EX_SNAPTOGRID directly on the ListView. Immediate, but
    /// DefView does not learn about it (its View menu still shows the old checkmark and a view
    /// refresh can re-apply the style), so this is only used when the toggle above did not take.
    /// </summary>
    public static bool ClearSnapToGridExtendedStyle()
    {
        var (_, listView) = DesktopIconInterop.FindDesktopViewWindows();
        if (listView == IntPtr.Zero) return false;

        // wParam = mask of bits to change, lParam = desired values (0 → clear).
        return TrySend(listView, LVM_SETEXTENDEDLISTVIEWSTYLE,
            new IntPtr(LVS_EX_SNAPTOGRID), IntPtr.Zero, out _);
    }

    /// <summary>
    /// SendMessageTimeout wrapper — a hung explorer must never freeze our UI thread,
    /// which a plain cross-process SendMessage would do.
    /// </summary>
    private static bool TrySend(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, out IntPtr result)
    {
        var ok = SendMessageTimeout(hWnd, msg, wParam, lParam,
            SMTO_ABORTIFHUNG, SendMessageTimeoutMs, out result);
        return ok != IntPtr.Zero;
    }
}
