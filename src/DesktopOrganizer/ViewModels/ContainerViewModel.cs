using System.Windows.Input;
using System.Windows.Media;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels.Base;

namespace DesktopOrganizer.ViewModels;

/// <summary>
/// Wraps a single <see cref="Container"/> model for XAML binding.
/// Manages inline-edit (F-005), drag (F-007), resize (F-008), and style (F-009) state.
/// </summary>
public class ContainerViewModel : ObservableObject
{
    private readonly Container _model;
    private readonly ContainerService _service;
    private bool _isEditing;
    private string _editName = string.Empty;

    public ContainerViewModel(Container model, ContainerService service)
    {
        _model              = model;
        _service            = service;
        BeginRenameCommand  = new RelayCommand(_ => BeginRename());
        CommitRenameCommand = new RelayCommand(_ => CommitRename());
        CancelRenameCommand = new RelayCommand(_ => CancelRename());
        DeleteCommand       = new RelayCommand(_ => RequestDelete());
        EditStyleCommand    = new RelayCommand(_ => RequestEditStyle());
    }

    public Guid Id => _model.Id;

    public string Name
    {
        get => _model.Name;
        private set { _model.Name = value; OnPropertyChanged(); }
    }

    // ── Geometry (writable for drag/resize without persisting) ───

    public double X
    {
        get => _model.X;
        set { _model.X = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _model.Y;
        set { _model.Y = value; OnPropertyChanged(); }
    }

    public double Width
    {
        get => _model.Width;
        set { _model.Width = value; OnPropertyChanged(); }
    }

    public double Height
    {
        get => _model.Height;
        set { _model.Height = value; OnPropertyChanged(); }
    }

    // ── Style-derived properties (F-009) ────────────────────────

    public ContainerStyle Style => _model.Style;

    public string StyleBackgroundColor  => _model.Style.BackgroundColor;
    public double StyleBackgroundOpacity => _model.Style.BackgroundOpacity;
    public string StyleBorderColor       => _model.Style.BorderColor;
    public double StyleBorderThickness   => _model.Style.BorderThickness;
    public bool   StyleShowTitle         => _model.Style.ShowTitle;
    public double StyleTitleFontSize     => _model.Style.TitleFontSize;
    public string StyleTitleFontColor    => _model.Style.TitleFontColor;
    public double StyleCornerRadius      => _model.Style.CornerRadius;

    // ── Inline-edit state ────────────────────────────────────────

    public bool IsEditing
    {
        get => _isEditing;
        private set => SetField(ref _isEditing, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetField(ref _editName, value);
    }

    // ── Commands ─────────────────────────────────────────────────

    public ICommand BeginRenameCommand  { get; }
    public ICommand CommitRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand DeleteCommand       { get; }
    public ICommand EditStyleCommand    { get; }

    // ── Events ───────────────────────────────────────────────────

    public event EventHandler? DeleteRequested;
    public event EventHandler? EditStyleRequested;

    // ── F-005 Rename ─────────────────────────────────────────────

    public void BeginRename()
    {
        EditName  = Name;
        IsEditing = true;
    }

    public void CommitRename()
    {
        if (_service.Rename(Id, EditName))
        {
            OnPropertyChanged(nameof(Name));
            IsEditing = false;
        }
    }

    public void CancelRename() => IsEditing = false;

    // ── F-007 Move ───────────────────────────────────────────────

    /// <summary>Updates position in memory only (called frequently during drag).</summary>
    public void MoveWithoutSave(double x, double y)
    {
        _model.X = x;
        _model.Y = y;
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
    }

    /// <summary>Persists the current position (call on drag-end).</summary>
    public void CommitPosition() => _service.UpdatePosition(Id, _model.X, _model.Y);

    // ── F-008 Resize ─────────────────────────────────────────────

    /// <summary>Updates bounds in memory only (called frequently during resize).</summary>
    public void ResizeWithoutSave(double x, double y, double w, double h)
    {
        _model.X      = x; _model.Y      = y;
        _model.Width  = w; _model.Height = h;
        OnPropertyChanged(nameof(X));     OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(Width)); OnPropertyChanged(nameof(Height));
    }

    /// <summary>Persists the current size (call on resize-end).</summary>
    public void CommitSize() =>
        _service.Resize(Id, _model.X, _model.Y, _model.Width, _model.Height);

    // ── F-009 Style ──────────────────────────────────────────────

    /// <summary>Applies a new style and persists it immediately.</summary>
    public void ApplyStyle(ContainerStyle newStyle)
    {
        _model.Style = newStyle;
        _service.UpdateStyle(Id, newStyle);
        RefreshStyleProperties();
    }

    private void RefreshStyleProperties()
    {
        OnPropertyChanged(nameof(Style));
        OnPropertyChanged(nameof(StyleBackgroundColor));
        OnPropertyChanged(nameof(StyleBackgroundOpacity));
        OnPropertyChanged(nameof(StyleBorderColor));
        OnPropertyChanged(nameof(StyleBorderThickness));
        OnPropertyChanged(nameof(StyleShowTitle));
        OnPropertyChanged(nameof(StyleTitleFontSize));
        OnPropertyChanged(nameof(StyleTitleFontColor));
        OnPropertyChanged(nameof(StyleCornerRadius));
    }

    // ── Private helpers ───────────────────────────────────────────

    private void RequestDelete()     => DeleteRequested?.Invoke(this, EventArgs.Empty);
    private void RequestEditStyle()  => EditStyleRequested?.Invoke(this, EventArgs.Empty);
}
