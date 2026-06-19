using System.Windows;
using System.Windows.Interop;
using DesktopOrganizer.Interop;
using DesktopOrganizer.ViewModels;
using Microsoft.Win32;
// UseWindowsForms=true: resolve ambiguity with System.Drawing.Point
using Point = System.Windows.Point;

namespace DesktopOrganizer.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        FitToScreen();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    // ── Lifecycle ────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Hook into the window message pump to implement mouse pass-through
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }

    // ── Screen sizing ────────────────────────────────────────────

    private void FitToScreen()
    {
        Left   = SystemParameters.VirtualScreenLeft;
        Top    = SystemParameters.VirtualScreenTop;
        Width  = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(FitToScreen);
    }

    // ── Mouse pass-through ───────────────────────────────────────

    /// <summary>
    /// Returns HTTRANSPARENT for areas where no child control is present,
    /// so mouse messages fall through to the desktop shell underneath.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowInterop.WM_NCHITTEST)
            return IntPtr.Zero;

        // Let Windows compute the default hit result first
        var defaultResult = WindowInterop.DefWindowProc(hwnd, msg, wParam, lParam);

        // Only override HTCLIENT hits (the interior client area)
        if (defaultResult.ToInt32() != WindowInterop.HTCLIENT)
            return defaultResult;

        // Convert screen coordinates to WPF logical coordinates
        var screenX = WindowInterop.SignedLoWord(lParam);
        var screenY = WindowInterop.SignedHiWord(lParam);
        var logicalPoint = PointFromScreen(new Point(screenX, screenY));

        // If no visible child element is under the cursor → pass through
        var hitElement = InputHitTest(logicalPoint);
        if (hitElement is null || ReferenceEquals(hitElement, OverlayCanvas))
        {
            handled = true;
            return new IntPtr(WindowInterop.HTTRANSPARENT);
        }

        return defaultResult;
    }
}
