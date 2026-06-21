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
using Point            = System.Windows.Point;

namespace DesktopOrganizer.Views;

public partial class OverlayWindow : Window
{
    private Point _lastRightClickPosition;

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
        => Dispatcher.Invoke(FitToScreen);

    // ── Mouse pass-through ───────────────────────────────────────

    /// <summary>
    /// Returns HTTRANSPARENT so left-clicks on empty overlay areas pass through
    /// to the desktop.  Right-clicks are NOT passed through so WPF can show the
    /// context menu for F-004 (new Container).
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WindowInterop.WM_NCHITTEST)
            return IntPtr.Zero;

        var defaultResult = WindowInterop.DefWindowProc(hwnd, msg, wParam, lParam);
        if (defaultResult.ToInt32() != WindowInterop.HTCLIENT)
            return defaultResult;

        // Keep HTCLIENT during right-clicks so WPF can open the context menu
        if (WindowInterop.GetAsyncKeyState(WindowInterop.VK_RBUTTON) < 0)
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

    /// <summary>Walks the visual tree to find whether a ContainerControl is under the cursor.</summary>
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

    // ── Context menu handlers (F-004) ────────────────────────────

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _lastRightClickPosition = e.GetPosition(OverlayRoot);
    }

    private void OnCreateContainerMenuClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.CreateContainerAt(_lastRightClickPosition.X, _lastRightClickPosition.Y);
    }
}
