using System.Windows;
using DesktopOrganizer.ViewModels;

namespace DesktopOrganizer.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
