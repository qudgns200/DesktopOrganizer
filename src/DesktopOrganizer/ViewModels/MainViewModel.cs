using System.Collections.ObjectModel;
using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels.Base;

namespace DesktopOrganizer.ViewModels;

public class MainViewModel : ObservableObject
{
    public ObservableCollection<Container> Containers { get; } = new();

    private bool _watcherEnabled = true;
    public bool WatcherEnabled
    {
        get => _watcherEnabled;
        set => SetField(ref _watcherEnabled, value);
    }
}
