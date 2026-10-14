using StealthMode.Core.Configuration;
using StealthMode.Core.Models;

namespace StealthMode.Core.Audio;

public sealed class PendingUnmute
{
    public string SessionId { get; set; } = "";
    public string? ExecutablePath { get; set; }
    public DateTime Since { get; set; } = DateTime.UtcNow;
}

public sealed class PendingUnmuteFile
{
    public List<PendingUnmute> Entries { get; set; } = new();
}

/// <summary>
/// Windows mémorise l'état muet par application : une application qui se ferme pendant que nous la coupons redémarrerait muette.
/// On note donc ces applications ; dès qu'une de leurs sessions réapparaît, elle est rétablie (une seule fois par entrée).
/// Les entrées expirent après 7 jours. Aucun balayage tant que le fichier est vide.
/// </summary>
public static class AudioPendingUnmute
{
    static readonly TimeSpan Expiry = TimeSpan.FromDays(7);
    static readonly object Gate = new();

    static string DefaultPath => Path.Combine(AppPaths.Root, "audio-pending.json");

    public static void Add(IEnumerable<MutedSessionRecord> orphans, string? path = null)
    {
        path ??= DefaultPath;
        lock (Gate)
        {
            var file = Load(path);
            foreach (var o in orphans.Where(o => o.SessionId.Length > 0))
                if (!file.Entries.Any(e => e.SessionId == o.SessionId))
                    file.Entries.Add(new PendingUnmute { SessionId = o.SessionId, ExecutablePath = o.ExecutablePath });
            Save(path, file);
        }
    }

    /// <summary>Retire des entrées (application de nouveau activement gérée).</summary>
    public static void Remove(IEnumerable<string> sessionIds, string? path = null)
    {
        path ??= DefaultPath;
        var ids = sessionIds.ToHashSet();
        if (ids.Count == 0) return;
        lock (Gate)
        {
            var file = Load(path);
            if (file.Entries.RemoveAll(e => ids.Contains(e.SessionId)) > 0) Save(path, file);
        }
    }

    public static bool Any(string? path = null)
    {
        path ??= DefaultPath;
        lock (Gate) return Load(path).Entries.Count > 0;
    }

    /// <summary>Rétablit les sessions des applications en attente qui sont apparues ; retourne le nombre d'entrées traitées.</summary>
    public static int Sweep(string? path = null)
    {
        path ??= DefaultPath;
        lock (Gate)
        {
            var file = Load(path);
            if (file.Entries.Count == 0) return 0;

            file.Entries.RemoveAll(e => DateTime.UtcNow - e.Since > Expiry);
            var seen = AudioSessionMuter.UnmuteBySessionId(file.Entries.Select(e => e.SessionId).ToHashSet());
            int done = file.Entries.RemoveAll(e => seen.Contains(e.SessionId));
            if (done > 0) Logger.Info($"Son rétabli pour {done} application(s) redémarrée(s) muette(s)");
            Save(path, file);
            return done;
        }
    }

    static PendingUnmuteFile Load(string path) => JsonFile.LoadOrDefault<PendingUnmuteFile>(path);

    static void Save(string path, PendingUnmuteFile file)
    {
        try
        {
            if (file.Entries.Count == 0) { if (File.Exists(path)) File.Delete(path); }
            else JsonFile.SaveAtomic(path, file);
        }
        catch (Exception ex) { Logger.Error("Fichier d'attente audio", ex); }
    }
}
