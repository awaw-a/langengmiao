using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lanmian;

public static class Cs2WindowChecker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static bool IsCs2Foreground()
    {
        if (!OperatingSystem.IsWindows()) return false;

        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out var pid) == 0 || pid == 0) return false;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName.Equals("cs2", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

