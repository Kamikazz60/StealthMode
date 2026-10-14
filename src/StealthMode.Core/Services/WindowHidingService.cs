using StealthMode.Core.Audio;
using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using static StealthMode.Core.Native.NativeMethods;

namespace StealthMode.Core.Services;

/// <summary>
/// Moteur central : masque (SW_HIDE) puis restaure (SW_SHOWNA) des fenêtres sans toucher aux processus.
/// Option : coupe le son des applications masquées (par exécutable, même si une autre de leurs fenêtres reste visible).
/// Fenêtres et sessions audio coupées sont consignées dans hidden-state.json pour la récupération après incident.
/// </summary>
public sealed class WindowHidingService : IDisposable
{
    static readonly TimeSpan AudioSweepInterval = TimeSpan.FromSeconds(1);

    readonly object _gate = new();
    readonly List<HiddenWindowRecord> _hidden = new();
    readonly List<MutedSessionRecord> _muted = new();
    readonly List<HeldItemRecord> _held = new();
    readonly string _holdRoot;
    readonly string _journalPath;
    readonly string _pendingPath;
    Timer? _audioTimer, _pendingTimer;

    public WindowHidingService(string? journalPath = null)
    {
        _journalPath = journalPath ?? AppPaths.HiddenStateFile;
        _pendingPath = Path.Combine(Path.GetDirectoryName(_journalPath)!, "audio-pending.json");
        // Production : dossier local de l'ordinateur (même disque que les icônes, jamais la clé USB). Tests : à côté du journal.
        _holdRoot = journalPath is null ? AppPaths.HoldRoot : Path.Combine(Path.GetDirectoryName(_journalPath)!, "hold");
    }

    /// <summary>Coupe le son des applications masquées pendant le mode secret.</summary>
    public bool MuteAudio { get; set; }

    public bool IsSecretActive { get { lock (_gate) return ActiveLocked; } }
    public int HeldItemCount { get { lock (_gate) return _held.Count; } }
    bool ActiveLocked => _hidden.Count > 0 || _held.Count > 0;
    public int HiddenCount { get { lock (_gate) return _hidden.Count; } }
    public int MutedSessionCount { get { lock (_gate) return _muted.Count; } }
    public event Action? StateChanged;

    /// <summary>Masque les fenêtres données. Sans effet si le mode secret est déjà actif.</summary>
    public HideResult Hide(IEnumerable<WindowInfo> windows, IEnumerable<string>? items = null)
    {
        var failures = new List<string>();
        lock (_gate)
        {
            if (ActiveLocked) return new HideResult(0, failures);

            foreach (var w in windows)
            {
                var h = w.Handle;
                if (!IsWindow(h) || !IsWindowVisible(h)) { failures.Add($"{w.Title} : fenêtre indisponible"); continue; }

                var record = Capture(h, w);
                // Le journal est écrit AVANT le masquage : en cas de crash, la fenêtre reste récupérable.
                _hidden.Add(record);
                PersistJournal();

                ShowWindow(h, SW_HIDE);
                if (IsWindowVisible(h))
                {
                    // La fenêtre refuse le masquage.
                    _hidden.Remove(record);
                    PersistJournal();
                    failures.Add($"{w.Title} : la fenêtre refuse d'être masquée");
                    Logger.Error($"Masquage refusé : {w.Title} ({w.ProcessName})");
                }
            }

            // Icônes (Bureau, dossiers) mises de côté ; le journal est écrit avant chaque déplacement.
            if (items is not null) failures.AddRange(HeldItemsManager.Hold(items, _holdRoot, _held, PersistJournal));

            if (_hidden.Count > 0 && MuteAudio)
            {
                SweepAudioLocked();
                // Les applications peuvent ouvrir de nouvelles sessions audio (nouvelle piste, nouvel onglet) : balayage léger, seulement pendant le mode secret.
                _audioTimer = new Timer(_ => { lock (_gate) { if (_hidden.Count > 0) SweepAudioLocked(); } },
                    null, AudioSweepInterval, AudioSweepInterval);
            }
            Logger.Info($"Mode secret : {_hidden.Count} fenêtre(s) masquée(s), {_held.Count} élément(s) mis de côté, {_muted.Count} session(s) audio coupée(s), {failures.Count} échec(s)");
        }
        StateChanged?.Invoke();
        return new HideResult(_hidden.Count, failures, _held.Count);
    }

    /// <summary>Restaure toutes les fenêtres masquées et rétablit le son. Sans effet si rien n'est masqué.</summary>
    public RestoreResult Restore()
    {
        RestoreResult result;
        lock (_gate)
        {
            if (!ActiveLocked) return new RestoreResult(0, 0, Array.Empty<string>());
            _audioTimer?.Dispose();
            _audioTimer = null;

            var (audio, orphans) = AudioSessionMuter.Unmute(_muted);
            // Application fermée pendant la coupure : Windows la relancera muette, on la rétablira à sa réapparition.
            if (orphans.Count > 0) AudioPendingUnmute.Add(orphans, _pendingPath);
            result = WindowRestorer.RestoreAll(_hidden) with { AudioRestored = audio };
            var (items, _, itemFailures, kept) = HeldItemsManager.Release(_held);
            result = result with { ItemsRestored = items, Failures = result.Failures.Concat(itemFailures).ToList() };
            _hidden.Clear();
            _muted.Clear();
            // Un élément non remis (échec, ou mis de côté sur un autre ordinateur) reste consigné : le journal n'est alors pas supprimé.
            _held.Clear();
            if (kept.Count == 0) DeleteJournal();
            else { _held.AddRange(kept); PersistJournal(); _held.Clear(); }
            Logger.Info($"Restauration : {result.Restored} restaurée(s), {result.Gone} disparue(s), {items} élément(s) remis, {audio} session(s) audio rétablie(s), {result.Failures.Count} échec(s)");
            StartPendingWatcherIfNeeded();
        }
        StateChanged?.Invoke();
        return result;
    }

    /// <summary>
    /// Surveille (toutes les 3 s) la réapparition d'applications coupées puis fermées, tant que le fichier d'attente n'est pas vide.
    /// À appeler au démarrage ; sans effet si rien n'est en attente.
    /// </summary>
    public void StartPendingWatcherIfNeeded()
    {
        lock (_gate)
        {
            if (_pendingTimer is not null || !AudioPendingUnmute.Any(_pendingPath)) return;
            _pendingTimer = new Timer(_ =>
            {
                lock (_gate)
                {
                    if (ActiveLocked) return; // jamais pendant le mode secret
                    AudioPendingUnmute.Sweep(_pendingPath);
                    if (!AudioPendingUnmute.Any(_pendingPath)) { _pendingTimer?.Dispose(); _pendingTimer = null; }
                }
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _audioTimer?.Dispose(); _audioTimer = null;
            _pendingTimer?.Dispose(); _pendingTimer = null;
        }
    }

    /// <summary>Coupe les sessions audio des applications masquées (appelé avec _gate pris).</summary>
    void SweepAudioLocked()
    {
        var paths = _hidden.Where(r => r.ExecutablePath is not null).Select(r => r.ExecutablePath!.ToLowerInvariant()).ToHashSet();
        var pids = _hidden.Select(r => r.ProcessId).ToHashSet();
        var known = _muted.Select(m => m.InstanceId).ToHashSet();

        var added = AudioSessionMuter.MuteMatching(paths, pids, known);
        if (added.Count == 0) return;
        _muted.AddRange(added);
        AudioPendingUnmute.Remove(added.Select(a => a.SessionId), _pendingPath); // de nouveau gérée activement
        PersistJournal();
    }

    static HiddenWindowRecord Capture(IntPtr h, WindowInfo w)
    {
        var wp = new WINDOWPLACEMENT { length = System.Runtime.InteropServices.Marshal.SizeOf<WINDOWPLACEMENT>() };
        GetWindowPlacement(h, ref wp);
        GetWindowRect(h, out var rect);
        return new HiddenWindowRecord
        {
            Handle = h.ToInt64(),
            ProcessId = w.ProcessId,
            ClassName = w.ClassName,
            Title = w.Title,
            ExecutablePath = w.ExecutablePath,
            WasMinimized = IsIconic(h),
            WasMaximized = IsZoomed(h),
            Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom
        };
    }

    void PersistJournal()
    {
        try { JsonFile.SaveAtomic(_journalPath, new HiddenState { Windows = _hidden, MutedSessions = _muted, HeldItems = _held }); }
        catch (Exception ex) { Logger.Error("Écriture du journal de masquage impossible", ex); }
    }

    void DeleteJournal()
    {
        try { if (File.Exists(_journalPath)) File.Delete(_journalPath); }
        catch (Exception ex) { Logger.Error("Suppression du journal impossible", ex); }
    }
}
