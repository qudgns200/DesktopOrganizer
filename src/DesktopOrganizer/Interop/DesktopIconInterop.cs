using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DesktopOrganizer.Services;

namespace DesktopOrganizer.Interop;

/// <summary>
/// Reads desktop icon positions from the Shell's SysListView32 control
/// using cross-process memory access (standard approach for all desktop organizer apps).
/// Only reads positions — never writes or modifies any file system entries.
/// </summary>
internal static class DesktopIconInterop
{
    // ListView messages
    private const int LVM_FIRST            = 0x1000;
    private const int LVM_GETITEMCOUNT     = LVM_FIRST + 4;
    private const int LVM_GETITEMPOSITION  = LVM_FIRST + 16;
    private const int LVM_SETITEMPOSITION  = LVM_FIRST + 15;   // MAKELONG(x,y) — works cross-process
    private const int LVM_GETITEMW         = LVM_FIRST + 75;
    private const uint LVIF_TEXT           = 0x0001;

    // Window style constants for auto-arrange detection
    private const int GWL_STYLE        = -16;
    private const int LVS_AUTOARRANGE  = 0x0100;

    // Process access rights
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    // Virtual memory flags
    private const uint MEM_COMMIT = 0x00001000;
    private const uint MEM_RELEASE = 0x00008000;
    private const uint PAGE_READWRITE = 0x04;

    private const int MAX_ICON_NAME = 260;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    // Must match the shell process layout (both x64 on Windows 10/11)
    [StructLayout(LayoutKind.Sequential)]
    private struct LVITEM
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;   // 8 bytes on x64
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;    // 8 bytes on x64
        public int iIndent;
        public int iGroupId;
        public uint cColumns;
        public IntPtr puColumns; // 8 bytes on x64
        public IntPtr piColFmt;  // 8 bytes on x64
        public int iGroup;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindow(string? cls, string? wnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? wnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();

    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAllocEx(IntPtr proc, IntPtr addr, nint size, uint type, uint protect);
    [DllImport("kernel32.dll")] private static extern bool VirtualFreeEx(IntPtr proc, IntPtr addr, nint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool WriteProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, nint size, out nint written);
    [DllImport("kernel32.dll")] private static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, nint size, out nint read);

    /// <summary>
    /// Moves desktop icons to the coordinates in <paramref name="positions"/> (display name → position).
    /// Coordinates are in WPF DIPs; this method converts them to physical pixels using system DPI.
    /// Uses LVM_SETITEMPOSITION with MAKELONG encoding — works reliably across all Windows versions.
    /// Does NOT move, copy, or delete any file system entries.
    /// Returns the number of icons whose positions were actually sent to the desktop ListView.
    /// </summary>
    public static int WriteIconPositions(Dictionary<string, (int X, int Y)> positions)
    {
        if (positions.Count == 0) return 0;

        var listView = FindDesktopListView();
        if (listView == IntPtr.Zero)
        {
            LogService.Instance.Warn("Interop", "WriteIconPositions: desktop ListView handle not found");
            return 0;
        }

        int count = (int)SendMessage(listView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0) return 0;

        // Warn if auto-arrange is enabled — positions will snap back immediately
        int lvStyle = GetWindowLong(listView, GWL_STYLE);
        if ((lvStyle & LVS_AUTOARRANGE) != 0)
        {
            LogService.Instance.Warn("Interop",
                "WriteIconPositions: LVS_AUTOARRANGE is set on the desktop — " +
                "icons will snap back. Disable 'Auto arrange icons' on the desktop.");
        }

        GetWindowThreadProcessId(listView, out uint pid);
        var hProcess = OpenProcess(
            PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
            false, pid);

        if (hProcess == IntPtr.Zero)
        {
            LogService.Instance.Error("Interop",
                $"WriteIconPositions: OpenProcess failed (PID={pid}, " +
                $"LastError={Marshal.GetLastWin32Error()})");
            return 0;
        }

        // DPI scale: container positions are in WPF DIPs; listview expects physical pixels
        double dpiScale = GetDpiForSystem() / 96.0;

        // Pre-normalize positions keys to NFC so Korean/CJK names match regardless of
        // whether the filesystem or SysListView32 returned NFC vs NFD Unicode form.
        // Value = (original key in positions dict, the position).
        var nfcLookup = new Dictionary<string, (string OriginalKey, (int X, int Y) Pos)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in positions)
            nfcLookup[kvp.Key.Normalize(NormalizationForm.FormC)] = (kvp.Key, kvp.Value);

        int moved = 0;
        try
        {
            int  textBytes = MAX_ICON_NAME * sizeof(char);
            nint lvSize    = Marshal.SizeOf<LVITEM>();
            nint total     = lvSize + textBytes;   // no remote POINT needed for MAKELONG encoding

            var remote = VirtualAllocEx(hProcess, IntPtr.Zero, total, MEM_COMMIT, PAGE_READWRITE);
            if (remote == IntPtr.Zero)
            {
                LogService.Instance.Error("Interop", "WriteIconPositions: VirtualAllocEx failed");
                return 0;
            }

            try
            {
                var remoteLv  = remote;
                var remoteTxt = remote + (int)lvSize;

                // Track which original position keys were matched (for unmatched logging)
                var matched   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                // Collect all ListView names for diagnostic logging when items are unmatched
                var lvNames   = new List<string>(count);

                for (int i = 0; i < count; i++)
                {
                    var raw = ReadItemText(hProcess, listView, i, remoteLv, remoteTxt, textBytes);
                    if (string.IsNullOrEmpty(raw)) continue;

                    lvNames.Add(raw);

                    // Normalise to NFC: handles Korean NFC/NFD differences between the
                    // filesystem name and the SysListView32 display string.
                    var name = raw.Normalize(NormalizationForm.FormC);

                    string? matchedKey  = null;
                    (int X, int Y) pos  = default;

                    // 1) Exact NFC match (folders and full-name files land here)
                    if (nfcLookup.TryGetValue(name, out var exact))
                    {
                        pos        = exact.Pos;
                        matchedKey = exact.OriginalKey;
                    }
                    else
                    {
                        // 2) Extension-insensitive fallback. Windows may hide known-type
                        //    extensions in the ListView, so compare both sides with the
                        //    extension stripped (handles ".exe"/".pdf" hidden or shown).
                        var nameNoExt = Path.GetFileNameWithoutExtension(name);
                        var fallback = nfcLookup.FirstOrDefault(kvp =>
                            Path.GetFileNameWithoutExtension(kvp.Key)
                                .Equals(name,      StringComparison.OrdinalIgnoreCase) ||
                            Path.GetFileNameWithoutExtension(kvp.Key)
                                .Equals(nameNoExt, StringComparison.OrdinalIgnoreCase));
                        if (fallback.Key is not null)
                        {
                            pos        = fallback.Value.Pos;
                            matchedKey = fallback.Value.OriginalKey;
                        }
                    }

                    if (matchedKey is null) continue;
                    matched.Add(matchedKey);

                    // Convert WPF DIP → physical pixels, then pack into MAKELONG(x, y)
                    int physX = (int)Math.Round(pos.X * dpiScale);
                    int physY = (int)Math.Round(pos.Y * dpiScale);
                    // Cast via uint to avoid CS0675 sign-extension warning
                    long lp = (long)(uint)(physX & 0xFFFF) | ((long)(uint)(physY & 0xFFFF) << 16);

                    SendMessage(listView, LVM_SETITEMPOSITION, new IntPtr(i), new IntPtr(lp));
                    moved++;
                }

                // Report unmatched items. When any remain, also dump the exact ListView
                // names (at Info level so it appears in default logs) so the mismatch
                // between our keys and the shell's display strings can be diagnosed.
                var unmatched = positions.Keys.Where(k => !matched.Contains(k)).ToList();
                if (unmatched.Count > 0)
                {
                    LogService.Instance.Info("Interop",
                        $"WriteIconPositions: {unmatched.Count} unmatched key(s): " +
                        string.Join(", ", unmatched.Select(k => $"'{k}'")));
                    LogService.Instance.Info("Interop",
                        $"WriteIconPositions: SysListView32 returned {lvNames.Count} name(s): " +
                        string.Join(", ", lvNames.Select(n => $"'{n}'")));
                }
            }
            finally
            {
                VirtualFreeEx(hProcess, remote, 0, MEM_RELEASE);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }

        LogService.Instance.Info("Interop",
            $"WriteIconPositions: repositioned {moved}/{positions.Count} icon(s) (DPI scale {dpiScale:F2})");
        return moved;
    }

    /// <summary>
    /// Returns a map of icon display name → desktop pixel position (X, Y).
    /// Returns an empty dictionary if the desktop ListView cannot be accessed.
    /// </summary>
    public static Dictionary<string, (int X, int Y)> ReadIconPositions()
    {
        var result = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);

        var listView = FindDesktopListView();
        if (listView == IntPtr.Zero)
        {
            Debug.WriteLine("[DesktopIconInterop] SysListView32 handle not found");
            return result;
        }

        int count = (int)SendMessage(listView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0) return result;

        GetWindowThreadProcessId(listView, out uint pid);
        var hProcess = OpenProcess(
            PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
            false, pid);

        if (hProcess == IntPtr.Zero)
        {
            Debug.WriteLine($"[DesktopIconInterop] OpenProcess failed (PID={pid})");
            return result;
        }

        try
        {
            int textBytes = MAX_ICON_NAME * sizeof(char);
            nint lvSize = Marshal.SizeOf<LVITEM>();
            nint ptSize = Marshal.SizeOf<POINT>();
            nint totalSize = lvSize + ptSize + textBytes;

            var remote = VirtualAllocEx(hProcess, IntPtr.Zero, totalSize, MEM_COMMIT, PAGE_READWRITE);
            if (remote == IntPtr.Zero) return result;

            try
            {
                var remoteLv = remote;
                var remotePt = remote + (int)lvSize;
                var remoteTxt = remote + (int)lvSize + (int)ptSize;

                for (int i = 0; i < count; i++)
                {
                    string name = ReadItemText(hProcess, listView, i, remoteLv, remoteTxt, textBytes);
                    (int x, int y) = ReadItemPosition(hProcess, listView, i, remotePt);

                    if (!string.IsNullOrWhiteSpace(name))
                        result[name] = (x, y);
                }
            }
            finally
            {
                VirtualFreeEx(hProcess, remote, 0, MEM_RELEASE);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }

        Debug.WriteLine($"[DesktopIconInterop] Read {result.Count} icon positions");
        return result;
    }

    private static IntPtr FindDesktopListView()
    {
        // Primary path: Progman → SHELLDLL_DefView → SysListView32
        var progman = FindWindow("Progman", null);
        var lv = FindListViewUnder(progman);
        if (lv != IntPtr.Zero) return lv;

        // Fallback: WorkerW windows (used when a video wallpaper or some customisations are active)
        var workerW = IntPtr.Zero;
        while (true)
        {
            workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
            if (workerW == IntPtr.Zero) break;
            lv = FindListViewUnder(workerW);
            if (lv != IntPtr.Zero) return lv;
        }

        return IntPtr.Zero;
    }

    private static IntPtr FindListViewUnder(IntPtr parent)
    {
        if (parent == IntPtr.Zero) return IntPtr.Zero;
        var shellView = FindWindowEx(parent, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellView == IntPtr.Zero) return IntPtr.Zero;
        return FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
    }

    private static string ReadItemText(
        IntPtr hProcess, IntPtr listView, int index,
        IntPtr remoteLv, IntPtr remoteTxt, int textByteLen)
    {
        var item = new LVITEM
        {
            mask = LVIF_TEXT,
            iItem = index,
            iSubItem = 0,
            pszText = remoteTxt,
            cchTextMax = MAX_ICON_NAME
        };

        var itemBytes = ToBytes(item);
        WriteProcessMemory(hProcess, remoteLv, itemBytes, itemBytes.Length, out _);
        SendMessage(listView, LVM_GETITEMW, new IntPtr(index), remoteLv);

        var buf = new byte[textByteLen];
        ReadProcessMemory(hProcess, remoteTxt, buf, buf.Length, out _);

        // The remote text buffer is allocated once and reused for every item.
        // The shell writes each item's name NUL-terminated, but a shorter name
        // leaves trailing bytes from a previous (longer) read. We must therefore
        // truncate at the FIRST NUL — TrimEnd('\0') is insufficient because it
        // keeps leftover text that sits AFTER an embedded NUL ("name\0garbage").
        var s   = Encoding.Unicode.GetString(buf);
        int nul = s.IndexOf('\0');
        return nul >= 0 ? s[..nul] : s;
    }

    private static (int X, int Y) ReadItemPosition(IntPtr hProcess, IntPtr listView, int index, IntPtr remotePt)
    {
        SendMessage(listView, LVM_GETITEMPOSITION, new IntPtr(index), remotePt);

        var buf = new byte[Marshal.SizeOf<POINT>()];
        ReadProcessMemory(hProcess, remotePt, buf, buf.Length, out _);
        var pt = FromBytes<POINT>(buf);
        return (pt.X, pt.Y);
    }

    private static byte[] ToBytes<T>(T s) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        var buf = new byte[size];
        var ptr = Marshal.AllocHGlobal(size);
        try { Marshal.StructureToPtr(s, ptr, false); Marshal.Copy(ptr, buf, 0, size); }
        finally { Marshal.FreeHGlobal(ptr); }
        return buf;
    }

    private static T FromBytes<T>(byte[] buf) where T : struct
    {
        var ptr = Marshal.AllocHGlobal(buf.Length);
        try { Marshal.Copy(buf, 0, ptr, buf.Length); return Marshal.PtrToStructure<T>(ptr)!; }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}
