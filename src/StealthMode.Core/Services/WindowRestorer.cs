using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using static StealthMode.Core.Native.NativeMethods;

namespace StealthMode.Core.Services;

/// <summary>
/// Restauration de fenêtres masquées. Partagée par l'application et par l'utilitaire de récupération,
/// qui ne dépend donc pas de l'interface principale.
/// </summary>
public static class WindowRestorer
{
    public static RestoreResult RestoreAll(IEnumerable<HiddenWindowRecord> records)
    {
        int restored = 0, gone = 0;
        var failures = new List<string>();

        foreach (var r in records)
        {
            var h = new IntPtr(r.Handle);
            // Un HWND peut être réattribué : on vérifie processus et classe avant d'agir.
            if (!IsWindow(h) || GetPid(h) != r.ProcessId || GetClass(h) != r.ClassName) { gone++; continue; }

            try
            {
                if (r.WasMinimized) ShowWindow(h, SW_SHOWMINNOACTIVE);
                else ShowWindow(h, SW_SHOWNA); // réaffiche à l'état et à la position conservés, sans voler le focus

                if (!IsWindowVisible(h)) { failures.Add($"{r.Title} : n'a pas pu être réaffichée"); continue; }
                if (!r.WasMinimized && !r.WasMaximized) EnsureOnScreen(h);
                restored++;
            }
            catch (Exception ex)
            {
                failures.Add($"{r.Title} : {ex.Message}");
                Logger.Error($"Restauration de {r.Title}", ex);
            }
        }
        return new RestoreResult(restored, gone, failures);
    }

    /// <summary>
    /// Si l'écran d'origine a disparu pendant le mode secret (écran débranché), la fenêtre est ramenée
    /// sur l'écran principal sans changer sa taille ; sinon on ne touche à rien.
    /// </summary>
    static void EnsureOnScreen(IntPtr h)
    {
        if (!GetWindowRect(h, out var rect)) return;
        if (MonitorFromRect(ref rect, MONITOR_DEFAULTTONULL) != IntPtr.Zero) return;

        var primary = MonitorFromRect(ref rect, MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(primary, ref info)) return;

        int x = Math.Clamp(rect.Left, info.rcWork.Left, Math.Max(info.rcWork.Left, info.rcWork.Right - rect.Width));
        int y = Math.Clamp(rect.Top, info.rcWork.Top, Math.Max(info.rcWork.Top, info.rcWork.Bottom - rect.Height));
        SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        Logger.Info($"Fenêtre hors écran ramenée sur l'écran principal ({x},{y})");
    }

    /// <summary>Restaure d'après le journal sur disque (récupération après crash). Retourne null si aucun journal.</summary>
    public static RestoreResult? RestoreFromJournal(string? journalPath = null)
    {
        journalPath ??= AppPaths.HiddenStateFile;
        if (!File.Exists(journalPath)) return null;
        var state = JsonFile.LoadOrDefault<HiddenState>(journalPath);
        var (audio, orphans) = Audio.AudioSessionMuter.Unmute(state.MutedSessions);
        if (orphans.Count > 0)
            Audio.AudioPendingUnmute.Add(orphans, Path.Combine(Path.GetDirectoryName(journalPath)!, "audio-pending.json"));
        var result = RestoreAll(state.Windows) with { AudioRestored = audio };
        var (items, _, itemFailures, kept) = HeldItemsManager.Release(state.HeldItems);
        result = result with { ItemsRestored = items, Failures = result.Failures.Concat(itemFailures).ToList() };
        // Un élément non remis (échec, ou mis de côté sur un autre ordinateur) reste dans le journal (rien n'est perdu).
        try
        {
            if (kept.Count == 0) File.Delete(journalPath);
            else JsonFile.SaveAtomic(journalPath, new HiddenState { HeldItems = kept });
        }
        catch { }
        return result;
    }
}
