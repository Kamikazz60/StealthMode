using System.Diagnostics;
using StealthMode.Core.Native;
using StealthMode.Core.Services;

namespace StealthMode.Tests;

/// <summary>Tests réels contre Windows : lancent deux instances de notepad.exe. Nécessitent une session interactive.</summary>
public class HidingIntegrationTests
{
    static Process? StartNotepad(string file)
    {
        File.WriteAllText(file, "test");
        var p = Process.Start(new ProcessStartInfo("notepad.exe", $"\"{file}\"") { UseShellExecute = false });
        if (p is null) return null;
        for (int i = 0; i < 50; i++)
        {
            Thread.Sleep(200);
            p.Refresh();
            if (p.HasExited) return null;
            if (p.MainWindowHandle != IntPtr.Zero) return p;
        }
        return null;
    }

    [Fact]
    public void HideAndRestore_OneWindowOnly_ProcessKeepsRunning_PositionKept()
    {
        var f1 = Path.Combine(Path.GetTempPath(), $"stealth-a-{Guid.NewGuid():N}.txt");
        var f2 = Path.Combine(Path.GetTempPath(), $"stealth-b-{Guid.NewGuid():N}.txt");
        var journal = Path.Combine(Path.GetTempPath(), $"stealth-j-{Guid.NewGuid():N}.json");
        Process? p1 = null, p2 = null;
        try
        {
            p1 = StartNotepad(f1); p2 = StartNotepad(f2);
            Assert.True(p1 is not null && p2 is not null, "notepad.exe n a pas ouvert de fenêtre exploitable");

            var all = WindowEnumerator.GetOpenWindows();
            var w1 = all.FirstOrDefault(w => w.ProcessId == p1!.Id);
            var w2 = all.FirstOrDefault(w => w.ProcessId == p2!.Id);
            Assert.NotNull(w1); Assert.NotNull(w2);

            NativeMethods.GetWindowRect(w1!.Handle, out var before);
            var svc = new WindowHidingService(journal);

            var hide = svc.Hide(new[] { w1 });
            Assert.Equal(1, hide.Hidden);
            Assert.True(svc.IsSecretActive);
            Assert.False(NativeMethods.IsWindowVisible(w1.Handle));       // masquée
            Assert.True(NativeMethods.IsWindowVisible(w2!.Handle));       // l'autre fenêtre du même programme reste visible
            Assert.False(p1!.HasExited);                                   // processus toujours actif
            Assert.True(File.Exists(journal));                            // journal de récupération présent
            Assert.DoesNotContain(WindowEnumerator.GetOpenWindows(), w => w.Handle == w1.Handle);

            Assert.Equal(0, svc.Hide(new[] { w2 }).Hidden);               // activation idempotente
            Assert.True(NativeMethods.IsWindowVisible(w2.Handle));

            var restore = svc.Restore();
            Assert.Equal(1, restore.Restored);
            Assert.True(NativeMethods.IsWindowVisible(w1.Handle));
            NativeMethods.GetWindowRect(w1.Handle, out var after);
            Assert.Equal((before.Left, before.Top, before.Right, before.Bottom), (after.Left, after.Top, after.Right, after.Bottom));
            Assert.False(File.Exists(journal));
            Assert.Equal(0, svc.Restore().Restored);                    // restauration idempotente
        }
        finally { Kill(p1); Kill(p2); File.Delete(f1); File.Delete(f2); File.Delete(journal); }
    }

    [Fact]
    public void Restore_AfterHiddenWindowWasClosed_NoError_AndRecoveryFromJournalWorks()
    {
        var f = Path.Combine(Path.GetTempPath(), $"stealth-c-{Guid.NewGuid():N}.txt");
        var journal = Path.Combine(Path.GetTempPath(), $"stealth-j-{Guid.NewGuid():N}.json");
        Process? p = null, p2 = null;
        try
        {
            p = StartNotepad(f);
            Assert.True(p is not null, "notepad.exe sans fenêtre");
            var w = WindowEnumerator.GetOpenWindows().First(x => x.ProcessId == p!.Id);

            // 1) fenêtre fermée pendant le mode secret -> restauration sans erreur
            var svc = new WindowHidingService(journal);
            svc.Hide(new[] { w });
            p!.Kill(); p.WaitForExit();
            var r = svc.Restore();
            Assert.Empty(r.Failures);
            Assert.Equal(1, r.Gone);

            // 2) simulation de crash : journal laissé sur disque, un autre « processus » restaure
            p2 = StartNotepad(f);
            Assert.True(p2 is not null, "notepad.exe sans fenêtre");
            var w2 = WindowEnumerator.GetOpenWindows().First(x => x.ProcessId == p2!.Id);
            new WindowHidingService(journal).Hide(new[] { w2 });
            Assert.False(NativeMethods.IsWindowVisible(w2.Handle));
            var rec = WindowRestorer.RestoreFromJournal(journal);
            Assert.NotNull(rec);
            Assert.Equal(1, rec!.Restored);
            Assert.True(NativeMethods.IsWindowVisible(w2.Handle));
        }
        finally { Kill(p); Kill(p2); File.Delete(f); File.Delete(journal); }
    }

    [Fact]
    public void Restore_BringsOffscreenWindowBackOnScreen()
    {
        var f = Path.Combine(Path.GetTempPath(), $"stealth-o-{Guid.NewGuid():N}.txt");
        var journal = Path.Combine(Path.GetTempPath(), $"stealth-j-{Guid.NewGuid():N}.json");
        Process? p = null;
        try
        {
            p = StartNotepad(f);
            Assert.True(p is not null, "notepad.exe sans fenêtre");
            var w = WindowEnumerator.GetOpenWindows().First(x => x.ProcessId == p!.Id);
            var svc = new WindowHidingService(journal);
            svc.Hide(new[] { w });
            // Simule un écran débranché pendant le mode secret : la fenêtre masquée est déplacée hors de tout écran.
            NativeMethods.SetWindowPos(w.Handle, IntPtr.Zero, 30000, 30000, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            NativeMethods.GetWindowRect(w.Handle, out var off);
            Assert.Equal(IntPtr.Zero, NativeMethods.MonitorFromRect(ref off, NativeMethods.MONITOR_DEFAULTTONULL));

            svc.Restore();
            NativeMethods.GetWindowRect(w.Handle, out var back);
            Assert.True(NativeMethods.IsWindowVisible(w.Handle));
            Assert.NotEqual(IntPtr.Zero, NativeMethods.MonitorFromRect(ref back, NativeMethods.MONITOR_DEFAULTTONULL));
            Assert.Equal(off.Width, back.Width); // taille inchangée
        }
        finally { Kill(p); File.Delete(f); File.Delete(journal); }
    }

    static void Kill(Process? p) { try { if (p is { HasExited: false }) p.Kill(); } catch { } }
}
