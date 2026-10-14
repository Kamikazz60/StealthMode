using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using StealthMode.Core.Native;

namespace StealthMode.Core.Audio;

/// <summary>
/// Coupe/rétablit le son par session audio Windows (mixeur de volume), sans toucher au volume général.
/// Les sessions sont reconnues par l'exécutable : le son d'un navigateur sort d'un processus différent de celui de ses fenêtres.
/// Attention : Windows mémorise l'état muet par application. Une application qui se termine pendant la coupure
/// redémarrerait muette ; voir <see cref="AudioPendingUnmute"/>.
/// </summary>
public static class AudioSessionMuter
{
    static readonly Guid IidSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    const int EDataFlowRender = 0, DeviceStateActive = 1, ClsctxAll = 23;

    sealed record SessionView(string InstanceId, string SessionId, uint Pid, ISimpleAudioVolume Volume);

    /// <summary>
    /// Coupe les sessions appartenant aux exécutables/PID donnés et non déjà muettes (état utilisateur respecté).
    /// <paramref name="alreadyMuted"/> évite de retraiter les sessions déjà coupées par nous.
    /// </summary>
    public static List<MutedSessionRecord> MuteMatching(IReadOnlySet<string> exePaths, IReadOnlySet<uint> pids,
        ISet<string> alreadyMuted)
    {
        var result = new List<MutedSessionRecord>();
        try
        {
            foreach (var s in EnumerateSessions())
            {
                if (s.Pid == 0 || s.Pid == (uint)Environment.ProcessId || alreadyMuted.Contains(s.InstanceId)) continue;

                var path = NativeMethods.GetProcessPath(s.Pid);
                bool match = pids.Contains(s.Pid) || (path is not null && exePaths.Contains(path.ToLowerInvariant()));
                if (!match) continue;

                var ctx = Guid.Empty;
                if (s.Volume.GetMute(out bool muted) != 0 || muted) continue; // déjà muet : on ne touche pas
                if (s.Volume.SetMute(true, ref ctx) == 0)
                    result.Add(new MutedSessionRecord
                    {
                        InstanceId = s.InstanceId, SessionId = s.SessionId, ProcessId = s.Pid, ExecutablePath = path
                    });
            }
        }
        catch (Exception ex) { Logger.Error("Coupure audio", ex); }
        return result;
    }

    /// <summary>
    /// Rétablit le son des sessions que nous avons coupées. Si une session d'origine n'existe plus, les sessions actuelles
    /// de la même application sont rétablies à sa place (l'application a redémarré, muette). Les enregistrements sans aucune
    /// session vivante sont retournés dans <c>Orphans</c> : Windows les rendra muets au prochain démarrage de l'application.
    /// </summary>
    public static (int Unmuted, List<MutedSessionRecord> Orphans) Unmute(IEnumerable<MutedSessionRecord> records)
    {
        var list = records.ToList();
        var orphans = new List<MutedSessionRecord>();
        int count = 0;
        if (list.Count == 0) return (0, orphans);
        try
        {
            var sessions = EnumerateSessions().ToList();
            foreach (var r in list)
            {
                var targets = sessions.Where(s => s.InstanceId == r.InstanceId).ToList();
                if (targets.Count == 0 && r.SessionId.Length > 0)
                    targets = sessions.Where(s => s.SessionId == r.SessionId).ToList();
                if (targets.Count == 0) { orphans.Add(r); continue; }
                foreach (var s in targets)
                {
                    var ctx = Guid.Empty;
                    if (s.Volume.SetMute(false, ref ctx) == 0) count++;
                }
            }
        }
        catch (Exception ex) { Logger.Error("Rétablissement audio", ex); }
        return (count, orphans);
    }

    /// <summary>Retourne l'état muet d'une session (pour les tests) ; null si la session n'existe pas.</summary>
    public static bool? IsMuted(string instanceId)
    {
        foreach (var s in EnumerateSessions())
            if (s.InstanceId == instanceId && s.Volume.GetMute(out bool m) == 0) return m;
        return null;
    }

    /// <summary>Sessions audio (id d'instance, PID) sur tous les périphériques de sortie actifs.</summary>
    public static List<(string InstanceId, uint Pid)> ListSessions() =>
        EnumerateSessions().Select(s => (s.InstanceId, s.Pid)).ToList();

    /// <summary>Rétablit toute session vivante dont l'identifiant d'application figure dans <paramref name="sessionIds"/>.
    /// Retourne les identifiants rencontrés (qu'ils aient été muets ou non).</summary>
    internal static HashSet<string> UnmuteBySessionId(ISet<string> sessionIds)
    {
        var seen = new HashSet<string>();
        try
        {
            foreach (var s in EnumerateSessions())
            {
                if (!sessionIds.Contains(s.SessionId)) continue;
                seen.Add(s.SessionId);
                var ctx = Guid.Empty;
                if (s.Volume.GetMute(out bool m) == 0 && m) s.Volume.SetMute(false, ref ctx);
            }
        }
        catch (Exception ex) { Logger.Error("Rétablissement audio différé", ex); }
        return seen;
    }

    static IEnumerable<SessionView> EnumerateSessions()
    {
        var views = new List<SessionView>();
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        if (enumerator.EnumAudioEndpoints(EDataFlowRender, DeviceStateActive, out var devices) != 0) return views;
        devices.GetCount(out uint deviceCount);

        for (uint d = 0; d < deviceCount; d++)
        {
            if (devices.Item(d, out var device) != 0) continue;
            var iid = IidSessionManager2;
            if (device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out var obj) != 0) continue;
            var manager = (IAudioSessionManager2)obj;
            if (manager.GetSessionEnumerator(out var sessions) != 0) continue;
            sessions.GetCount(out int n);

            for (int i = 0; i < n; i++)
            {
                if (sessions.GetSession(i, out var ctl) != 0) continue;
                if (ctl.GetSessionInstanceIdentifier(out var id) != 0 || id is null) continue;
                ctl.GetSessionIdentifier(out var sid);
                ctl.GetProcessId(out uint pid);
                if (ctl is ISimpleAudioVolume vol) views.Add(new SessionView(id, sid ?? "", pid, vol)); // QueryInterface sur l'objet COM
            }
        }
        return views;
    }
}
