using System.Diagnostics;
using StealthMode.Core.Models;
using StealthMode.Core.Native;
using static StealthMode.Core.Native.NativeMethods;

namespace StealthMode.Core.Services;

/// <summary>Détection des fenêtres de premier niveau « utilisateur » (celles qui apparaissent dans la barre des tâches / Alt+Tab).</summary>
public static class WindowEnumerator
{
    static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow"
    };

    static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
        "TextInputHost", "LockApp", "dwm", "csrss", "winlogon"
    };

    /// <param name="excludeProcessId">PID à ignorer (typiquement Stealth Mode lui-même).</param>
    public static List<WindowInfo> GetOpenWindows(uint? excludeProcessId = null)
    {
        var handles = new List<IntPtr>();
        EnumWindows((h, _) => { handles.Add(h); return true; }, IntPtr.Zero);

        var result = new List<WindowInfo>();
        var processCache = new Dictionary<uint, (string Name, string? Path, string App)>();

        foreach (var h in handles)
        {
            var info = TryDescribe(h, excludeProcessId, processCache);
            if (info is not null) result.Add(info);
        }
        return result;
    }

    /// <summary>Construit un WindowInfo si la fenêtre est candidate au masquage, sinon null.</summary>
    static WindowInfo? TryDescribe(IntPtr h, uint? excludePid, Dictionary<uint, (string, string?, string)> cache)
    {
        if (!IsWindow(h) || !IsWindowVisible(h) || IsCloaked(h)) return null;

        var title = GetText(h);
        if (title.Length == 0) return null;

        var cls = GetClass(h);
        if (ShellClasses.Contains(cls)) return null;

        long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
        bool toolWindow = (ex & WS_EX_TOOLWINDOW) != 0;
        bool appWindow = (ex & WS_EX_APPWINDOW) != 0;
        bool owned = GetWindow(h, GW_OWNER) != IntPtr.Zero;
        // Même critère que la barre des tâches : pas de fenêtre outil, pas de fenêtre possédée (sauf WS_EX_APPWINDOW).
        if ((toolWindow && !appWindow) || (owned && !appWindow)) return null;
        if ((ex & WS_EX_NOACTIVATE) != 0 && !appWindow) return null;

        var pid = GetPid(h);
        if (pid == 0 || pid == excludePid) return null;

        if (!cache.TryGetValue(pid, out var p))
        {
            p = DescribeProcess(pid);
            cache[pid] = p;
        }
        if (ProtectedProcesses.Contains(p.Item1)) return null;

        return new WindowInfo(h, title, cls, pid, p.Item1, p.Item2, p.Item3,
            IsVisible: true, IsMinimized: IsIconic(h), IsMaximized: IsZoomed(h));
    }

    static (string Name, string? Path, string App) DescribeProcess(uint pid)
    {
        string name = $"pid{pid}";
        string? path = GetProcessPath(pid);
        try { using var proc = Process.GetProcessById((int)pid); name = proc.ProcessName; }
        catch { /* processus terminé entre-temps */ }

        string app = name;
        if (path is not null)
        {
            try
            {
                var desc = FileVersionInfo.GetVersionInfo(path).FileDescription;
                if (!string.IsNullOrWhiteSpace(desc)) app = desc.Trim();
            }
            catch { }
        }
        return (name, path, app);
    }
}
