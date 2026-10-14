using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StealthMode.App.Services;

/// <summary>Réduit la mémoire résidente quand l'application est en veille (fenêtre masquée / zone de notification).</summary>
public static class MemoryTrimmer
{
    [DllImport("kernel32.dll")]
    static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

    public static void Trim()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1));
    }
}
