using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopOrganizer.Interop;
using DesktopOrganizer.ViewModels;
using DesktopOrganizer.Views.Controls;
using Microsoft.Win32;
// UseWindowsForms=true: resolve ambiguities with WinForms types
using ContainerControl = DesktopOrganizer.Views.Controls.ContainerControl;
using Point             = System.Windows.Point;

namespace DesktopOrganizer.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        FitToScreen();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    // ── Lifecycle ────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // Prevent the overlay from stealing focus or rising above other windows when clicked.
        int exStyle = WindowInterop.GetWindowLong(hwnd, WindowInterop.GWL_EXSTYLE);
        WindowInterop.SetWindowLong(hwnd, WindowInterop.GWL_EXSTYLE,
            exStyle | WindowInterop.WS_EX_NOACTIVATE);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
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
        => Dispatcher.Invoke(FitToScreen);

    // ── Mouse pass-through ───────────────────────────────────────

    /// <summary>
    /// Returns HTTRANSPARENT for empty overlay areas so all mouse events
    /// (left and right clicks) pass through to the desktop.
    /// Container areas return HTCLIENT so WPF handles Container interactions.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowInterop.WM_NCHITTEST)
            return IntPtr.Zero;

        var defaultResult = WindowInterop.DefWindowProc(hwnd, msg, wParam, lParam);
        if (defaultResult.ToInt32() != WindowInterop.HTCLIENT)
            return defaultResult;

        var screenX      = WindowInterop.SignedLoWord(lParam);
        var screenY      = WindowInterop.SignedHiWord(lParam);
        var logicalPoint = PointFromScreen(new Point(screenX, screenY));

        if (!IsOverContainer(logicalPoint))
        {
            handled = true;
            return new IntPtr(WindowInterop.HTTRANSPARENT);
        }

        return defaultResult;
    }

    /// <summary>
    /// Returns true when the cursor is anywhere over a ContainerControl, so the whole
    /// container is interactive: the title bar drags it, resize handles resize it, and
    /// the body receives double-clicks that launch the icon under the cursor.
    /// Points outside every container return false and are made HTTRANSPARENT so
    /// left/right clicks pass through to the desktop.
    /// </summary>
    private bool IsOverContainer(Point logicalPoint)
    {
        var result = VisualTreeHelper.HitTest(this, logicalPoint);
        if (result is null) return false;

        var element = result.VisualHit as DependencyObject;
        while (element is not null)
        {
            if (element is ContainerControl) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}
