using System.Runtime.InteropServices;

namespace DesktopOrganizer.Interop;

/// <summary>
/// Win32 helpers for overlay window behaviour: hit-test pass-through and
/// DPI-aware screen geometry queries.
/// </summary>
internal static class WindowInterop
{
    // WM_NCHITTEST return values
    public const int WM_NCHITTEST = 0x0084;
    public const int HTTRANSPARENT = -1;
    public const int HTCLIENT = 1;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Signed coordinate extraction from LPARAM (handles negative screen coords)
    public static int SignedLoWord(IntPtr lParam) =>
        unchecked((short)(long)lParam);

    public static int SignedHiWord(IntPtr lParam) =>
        unchecked((short)((long)lParam >> 16));
}
