using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels;
using DesktopOrganizer.Views.Dialogs;
// UseWindowsForms=true: resolve ambiguities with WinForms types
using KeyEventArgs  = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point          = System.Windows.Point;
using UserControl    = System.Windows.Controls.UserControl;

namespace DesktopOrganizer.Views.Controls;

public partial class ContainerControl : UserControl
{
    private ContainerViewModel? _vm;

    // ── Drag state (F-007) ───────────────────────────────────────
    private bool  _isDragging;
    private Point _dragOffset;   // cursor offset from container top-left, in canvas coords

    // ── Resize state (F-008) ─────────────────────────────────────
    private string _resizeDir     = "";
    private Point  _resizeStart;
    private double _rsX, _rsY, _rsW, _rsH;

    private const double MinW = ContainerService.MinContainerWidth;
    private const double MinH = ContainerService.MinContainerHeight;

    public ContainerControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ── DataContext wiring ───────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged    -= OnVmPropertyChanged;
            _vm.EditStyleRequested -= OnEditStyleRequested;
        }

        _vm = e.NewValue as ContainerViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged    += OnVmPropertyChanged;
            _vm.EditStyleRequested += OnEditStyleRequested;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ContainerViewModel.IsEditing) || _vm?.IsEditing != true)
            return;

        Dispatcher.BeginInvoke(() => { NameEditor.Focus(); NameEditor.SelectAll(); });
    }

    // ── Title bar: drag (F-007) + double-click rename (F-005) ───

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Cancel any in-progress drag, then start rename
            if (sender is FrameworkElement el && el.IsMouseCaptured)
                el.ReleaseMouseCapture();
            _isDragging = false;
            _vm?.BeginRenameCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (_vm == null) return;
        var canvas = FindParentCanvas();
        if (canvas == null) return;

        var pos   = e.GetPosition(canvas);
        _dragOffset = new Point(pos.X - _vm.X, pos.Y - _vm.Y);
        _isDragging = true;

        (sender as FrameworkElement)?.CaptureMouse();
        e.Handled = true;
    }

    private void OnTitleBarMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _vm == null) return;
        if (sender is not FrameworkElement el || !el.IsMouseCaptured) return;

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        var pos  = e.GetPosition(canvas);
        var newX = Math.Clamp(pos.X - _dragOffset.X, 0, canvas.ActualWidth  - _vm.Width);
        var newY = Math.Clamp(pos.Y - _dragOffset.Y, 0, canvas.ActualHeight - _vm.Height);
        _vm.MoveWithoutSave(newX, newY);
    }

    private void OnTitleBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        if (sender is not FrameworkElement el || !el.IsMouseCaptured) return;

        el.ReleaseMouseCapture();
        _isDragging = false;
        _vm?.CommitPosition();
        e.Handled = true;
    }

    // ── Resize handles (F-008) ───────────────────────────────────

    private void OnResizeHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement handle || _vm == null) return;
        _resizeDir = handle.Tag as string ?? "";
        if (string.IsNullOrEmpty(_resizeDir)) return;

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        _resizeStart = e.GetPosition(canvas);
        _rsX = _vm.X; _rsY = _vm.Y; _rsW = _vm.Width; _rsH = _vm.Height;

        handle.CaptureMouse();
        e.Handled = true;
    }

    private void OnResizeHandleMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement handle || !handle.IsMouseCaptured || _vm == null) return;

        var canvas = FindParentCanvas();
        if (canvas == null) return;

        var pos = e.GetPosition(canvas);
        ComputeNewBounds(pos.X - _resizeStart.X, pos.Y - _resizeStart.Y,
            out double nx, out double ny, out double nw, out double nh);
        _vm.ResizeWithoutSave(nx, ny, nw, nh);
    }

    private void OnResizeHandleMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement handle || !handle.IsMouseCaptured) return;
        handle.ReleaseMouseCapture();
        _vm?.CommitSize();
        e.Handled = true;
    }

    private void ComputeNewBounds(double dx, double dy,
        out double x, out double y, out double w, out double h)
    {
        x = _rsX; y = _rsY; w = _rsW; h = _rsH;

        if (_resizeDir.Contains('E'))
            w = Math.Max(MinW, _rsW + dx);
        if (_resizeDir.Contains('W'))
        {
            w = Math.Max(MinW, _rsW - dx);
            x = _rsX + _rsW - w;   // keep right edge fixed
        }
        if (_resizeDir.Contains('S'))
            h = Math.Max(MinH, _rsH + dy);
        if (_resizeDir.Contains('N'))
        {
            h = Math.Max(MinH, _rsH - dy);
            y = _rsY + _rsH - h;   // keep bottom edge fixed
        }
    }

    // ── Name editor ──────────────────────────────────────────────

    private void OnNameEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Return or Key.Enter)
        {
            _vm?.CommitRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm?.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnNameEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (_vm?.IsEditing == true)
            _vm.CommitRenameCommand.Execute(null);
    }

    // ── Style editor (F-009) ─────────────────────────────────────

    private void OnEditStyleRequested(object? sender, EventArgs e)
    {
        if (_vm == null) return;
        var dialog = new StyleEditorDialog(_vm.Style) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            _vm.ApplyStyle(dialog.ResultStyle);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private Canvas? FindParentCanvas()
    {
        var element = VisualTreeHelper.GetParent(this) as DependencyObject;
        while (element is not null)
        {
            if (element is Canvas c) return c;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
