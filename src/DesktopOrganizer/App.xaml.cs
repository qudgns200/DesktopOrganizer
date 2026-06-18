using System.Threading;
using System.Windows;

namespace DesktopOrganizer;

public partial class App : Application
{
    private const string MutexName = "DesktopOrganizer_SingleInstance_{A1B2C3D4}";
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Desktop Organizer가 이미 실행 중입니다.", "Desktop Organizer",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var overlay = new Views.OverlayWindow();
        overlay.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
