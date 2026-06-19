using System.Runtime.InteropServices;
using System.Text;

namespace DesktopOrganizer.Interop;

internal static class ShellApi
{
    private const int CSIDL_DESKTOPDIRECTORY = 0x0010;
    private const int CSIDL_COMMON_DESKTOPDIRECTORY = 0x0019;
    private const int MAX_PATH = 260;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SHGetSpecialFolderPath(
        IntPtr hwndOwner,
        StringBuilder lpszPath,
        int nFolder,
        bool fCreate);

    public static string GetUserDesktopPath()
    {
        var sb = new StringBuilder(MAX_PATH);
        return SHGetSpecialFolderPath(IntPtr.Zero, sb, CSIDL_DESKTOPDIRECTORY, false)
            ? sb.ToString()
            : string.Empty;
    }

    public static string GetPublicDesktopPath()
    {
        var sb = new StringBuilder(MAX_PATH);
        return SHGetSpecialFolderPath(IntPtr.Zero, sb, CSIDL_COMMON_DESKTOPDIRECTORY, false)
            ? sb.ToString()
            : string.Empty;
    }
}
