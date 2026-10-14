using System.Diagnostics;
using StealthMode.Core.Audio;
using StealthMode.Core.Services;

namespace StealthMode.Tests;

/// <summary>
/// Test réel de la coupure audio : une fenêtre (PowerShell + WinForms) joue en boucle un WAV silencieux
/// (une vraie session audio Windows, mais aucun bruit). Nécessite un périphérique de sortie audio actif.
/// </summary>
public class AudioMuteTests
{
    static string WriteSilentWav(string path)
    {
        const int rate = 8000, seconds = 30;
        int dataLen = rate * seconds;
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataLen); w.Write("WAVEfmt "u8.ToArray());
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate); w.Write((short)1); w.Write((short)8);
        w.Write("data"u8.ToArray()); w.Write(dataLen);
        w.Write(Enumerable.Repeat((byte)128, dataLen).ToArray()); // 128 = silence en PCM 8 bits
        return path;
    }

    static (Process proc, string title, string instanceId, StealthMode.Core.Models.WindowInfo window) StartAudioWindow(string wav, bool cleanState = true)
    {
        var title = "audiotest-" + Guid.NewGuid().ToString("N");
        var script = "Add-Type -AssemblyName System.Windows.Forms;" +
                     $"$f=New-Object Windows.Forms.Form;$f.Text='{title}';" +
                     $"$p=New-Object Media.SoundPlayer '{wav}';$p.PlayLooping();[Windows.Forms.Application]::Run($f)";
        var proc = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{script}\"") { UseShellExecute = false })!;

        StealthMode.Core.Models.WindowInfo? window = null;
        string? id = null;
        for (int i = 0; i < 100 && (window is null || id is null); i++)
        {
            Thread.Sleep(200);
            window ??= WindowEnumerator.GetOpenWindows().FirstOrDefault(w => w.Title == title);
            id ??= AudioSessionMuter.ListSessions().FirstOrDefault(s => s.Pid == (uint)proc.Id).InstanceId;
        }
        Assert.True(window is not null, "la fenêtre de test n'est pas apparue");
        Assert.True(!string.IsNullOrEmpty(id), "aucune session audio créée (périphérique de sortie audio actif requis)");
        // Windows mémorise l'état muet par application : on repart d'un état propre si un test précédent a été interrompu.
        if (cleanState && AudioSessionMuter.IsMuted(id!) == true)
            AudioSessionMuter.Unmute(new[] { new StealthMode.Core.Models.MutedSessionRecord { InstanceId = id! } });
        return (proc, title, id!, window!);
    }

    // Dossier temporaire dédié : le fichier d'attente audio est placé à côté du journal.
    static string NewJournal() => Path.Combine(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "stealth-a-" + Guid.NewGuid().ToString("N"))).FullName, "hidden-state.json");

    static void Kill(Process? p) { try { if (p is { HasExited: false }) p.Kill(); } catch { } }

    [Fact]
    public void HidingWindow_MutesItsApplicationAudio_AndRestoreUnmutes()
    {
        var wav = WriteSilentWav(Path.Combine(Path.GetTempPath(), $"stealth-{Guid.NewGuid():N}.wav"));
        var journal = NewJournal();
        Process? p = null;
        try
        {
            var t = StartAudioWindow(wav); p = t.proc;
            Assert.False(AudioSessionMuter.IsMuted(t.instanceId));

            using var svc = new WindowHidingService(journal) { MuteAudio = true };
            svc.Hide(new[] { t.window });
            Assert.True(svc.MutedSessionCount >= 1);
            Assert.True(AudioSessionMuter.IsMuted(t.instanceId));          // son coupé
            var state = StealthMode.Core.Configuration.JsonFile.LoadOrDefault<StealthMode.Core.Models.HiddenState>(journal);
            Assert.Contains(state.MutedSessions, m => m.InstanceId == t.instanceId); // consigné pour la récupération
            Assert.False(p.HasExited);                                       // le processus continue

            var r = svc.Restore();
            Assert.True(r.AudioRestored >= 1);
            Assert.False(AudioSessionMuter.IsMuted(t.instanceId));          // son rétabli
        }
        finally { Kill(p); File.Delete(wav); File.Delete(journal); }
    }

    [Fact]
    public void AlreadyMutedSessionIsLeftAlone_AndAudioOptionOffDoesNothing()
    {
        var wav = WriteSilentWav(Path.Combine(Path.GetTempPath(), $"stealth-{Guid.NewGuid():N}.wav"));
        var journal = NewJournal();
        Process? p = null;
        try
        {
            var t = StartAudioWindow(wav); p = t.proc;

            using var off = new WindowHidingService(journal) { MuteAudio = false };
            off.Hide(new[] { t.window });
            Assert.Equal(0, off.MutedSessionCount);
            Assert.False(AudioSessionMuter.IsMuted(t.instanceId));          // option désactivée : le son continue
            off.Restore();
        }
        finally { Kill(p); File.Delete(wav); File.Delete(journal); }
    }

    [Fact]
    public void AppClosedWhileMuted_RelaunchesMuted_ThenIsUnmutedByPendingSweep()
    {
        // Windows mémorise l'état muet par application : sans le fichier d'attente, l'application redémarrerait muette.
        var wav = WriteSilentWav(Path.Combine(Path.GetTempPath(), $"stealth-{Guid.NewGuid():N}.wav"));
        var journal = NewJournal();
        var pending = Path.Combine(Path.GetDirectoryName(journal)!, "audio-pending.json");
        Process? a = null, b = null;
        try
        {
            var t = StartAudioWindow(wav); a = t.proc;
            using var svc = new WindowHidingService(journal) { MuteAudio = true };
            svc.Hide(new[] { t.window });
            Assert.True(AudioSessionMuter.IsMuted(t.instanceId));

            a.Kill(); a.WaitForExit(); Thread.Sleep(1500);             // l'application se ferme pendant la coupure
            svc.Restore();
            Assert.True(AudioPendingUnmute.Any(pending));                // notée : session disparue, rien à rétablir

            var t2 = StartAudioWindow(wav, cleanState: false); b = t2.proc;               // relance : même application
            Assert.True(AudioSessionMuter.IsMuted(t2.instanceId));      // Windows la fait redémarrer muette

            Assert.Equal(1, AudioPendingUnmute.Sweep(pending));
            Assert.False(AudioSessionMuter.IsMuted(t2.instanceId));     // rétablie
            Assert.False(AudioPendingUnmute.Any(pending));              // entrée consommée
        }
        finally { Kill(a); Kill(b); File.Delete(wav); }
    }

    [Fact]
    public void CrashRecovery_FromJournal_UnmutesSessions()
    {
        var wav = WriteSilentWav(Path.Combine(Path.GetTempPath(), $"stealth-{Guid.NewGuid():N}.wav"));
        var journal = NewJournal();
        Process? p = null;
        try
        {
            var t = StartAudioWindow(wav); p = t.proc;
            var crashed = new WindowHidingService(journal) { MuteAudio = true };
            crashed.Hide(new[] { t.window });
            crashed.Dispose(); // « crash » : plus de restauration par ce service
            Assert.True(AudioSessionMuter.IsMuted(t.instanceId));

            var rec = WindowRestorer.RestoreFromJournal(journal);
            Assert.NotNull(rec);
            Assert.True(rec!.AudioRestored >= 1);
            Assert.False(AudioSessionMuter.IsMuted(t.instanceId));
            Assert.True(StealthMode.Core.Native.NativeMethods.IsWindowVisible(t.window.Handle));
        }
        finally { Kill(p); File.Delete(wav); File.Delete(journal); }
    }
}
