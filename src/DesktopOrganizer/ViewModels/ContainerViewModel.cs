using System.Windows.Input;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels.Base;

namespace DesktopOrganizer.ViewModels;

/// <summary>
/// Wraps a single <see cref="Container"/> model for XAML binding.
/// Manages the inline-edit state required by F-004 (creation) and F-005 (rename).
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
    }

    public Guid Id => _model.Id;

    public string Name
    {
        get => _model.Name;
        private set { _model.Name = value; OnPropertyChanged(); }
    }

    public double X      => _model.X;
    public double Y      => _model.Y;
    public double Width  => _model.Width;
    public double Height => _model.Height;

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

    public ICommand BeginRenameCommand  { get; }
    public ICommand CommitRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand DeleteCommand       { get; }

    public event EventHandler? DeleteRequested;

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

    public void CancelRename()
    {
        IsEditing = false;
    }

    private void RequestDelete() => DeleteRequested?.Invoke(this, EventArgs.Empty);
}
