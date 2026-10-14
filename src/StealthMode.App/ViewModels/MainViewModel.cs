using System.IO;
using System.Collections.ObjectModel;
using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using StealthMode.Core.Services;

namespace StealthMode.App.ViewModels;

public sealed class MainViewModel : ObservableBase
{
    readonly WindowHidingService _hiding;
    readonly AppSettings _settings;
    readonly ProfilesData _profiles;
    readonly HashSet<IntPtr> _selected = new();
    List<WindowInfo> _lastScan = new();
    string _status = "";
    bool _byApplication;
    Profile? _activeProfile;
    bool _updatingProfiles;

    /// <summary>Renseignés par l'application : masquer/réafficher la fenêtre principale pendant le mode secret.</summary>
    public Action? HideMainWindow { get; set; }
    /// <summary>Réaffiche la fenêtre principale dans son état d'avant, sans focus ; retourne vrai si elle est de nouveau visible.</summary>
    public Func<bool>? RestoreMainWindow { get; set; }
    public Action? OpenProfilesRequested { get; set; }
    public Action? OpenSettingsRequested { get; set; }
    /// <summary>Appelé avec une fenêtre pour proposer la création d'une règle à partir d'elle.</summary>
    public Action<WindowInfo>? CreateRuleRequested { get; set; }

    public MainViewModel(WindowHidingService hiding, AppSettings settings, ProfilesData profiles)
    {
        _hiding = hiding;
        _settings = settings;
        _profiles = profiles;
        _byApplication = settings.SelectByApplication;

        RefreshCommand = new RelayCommand(Refresh);
        ActivateCommand = new RelayCommand(Activate, () => !IsSecret);
        RestoreCommand = new RelayCommand(Restore, () => IsSecret);
        OpenProfilesCommand = new RelayCommand(() => OpenProfilesRequested?.Invoke());
        OpenSettingsCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke());
        _hiding.StateChanged += () =>
        {
            Raise(nameof(IsSecret)); Raise(nameof(StateText));
            ActivateCommand.RaiseCanExecuteChanged(); RestoreCommand.RaiseCanExecuteChanged();
        };
        ReloadProfiles();
    }

    public ObservableCollection<WindowItemViewModel> Windows { get; } = new();
    public ObservableCollection<Profile> Profiles { get; } = new();
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ActivateCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand OpenProfilesCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    public bool IsSecret => _hiding.IsSecretActive;
    public string StateText => IsSecret ? $"Mode secret ({_hiding.HiddenCount} fenêtre(s) masquée(s))" : "Mode normal";
    public string HotkeyHelpText => _settings.SingleHotkeyToggle
        ? $"Raccourci (active / restaure) : {_settings.GetActivateHotkey()}"
        : $"Activer : {_settings.GetActivateHotkey()}   Restaurer : {_settings.GetRestoreHotkey()}";

    public string Status { get => _status; set => Set(ref _status, value); }

    public Profile? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (_updatingProfiles || value is null || !Set(ref _activeProfile, value)) return;
            _profiles.ActiveProfileId = value.Id;
            try { _profiles.Save(); } catch (Exception ex) { Logger.Error("Sauvegarde des profils", ex); }
            UpdatePreview();
        }
    }

    public bool ByApplication
    {
        get => _byApplication;
        set
        {
            if (!Set(ref _byApplication, value)) return;
            _settings.SelectByApplication = value;
            try { _settings.Save(); } catch (Exception ex) { Logger.Error("Sauvegarde des paramètres", ex); }
            UpdatePreview();
        }
    }

    SelectionMode Mode => _byApplication ? SelectionMode.PerApplication : SelectionMode.PerWindow;
    IEnumerable<HideRule> ActiveRules => _activeProfile?.Rules ?? Enumerable.Empty<HideRule>();

    /// <summary>À appeler après modification des profils ou des règles (fenêtre Profils) ou des paramètres.</summary>
    public void ReloadProfiles()
    {
        _updatingProfiles = true;
        Profiles.Clear();
        foreach (var p in _profiles.Profiles) Profiles.Add(p);
        _activeProfile = _profiles.Active;
        _updatingProfiles = false;
        Raise(nameof(ActiveProfile));
        UpdatePreview();
    }

    /// <summary>Formulations de l'aide (bulle « ? ») : toujours le ou les raccourcis réellement configurés.</summary>
    public string ActivateHint => _settings.SingleHotkeyToggle
        ? $"le raccourci ({_settings.GetActivateHotkey()})"
        : $"le raccourci d'activation ({_settings.GetActivateHotkey()})";

    public string RestoreHint => _settings.SingleHotkeyToggle
        ? $"de nouveau sur le même raccourci ({_settings.GetActivateHotkey()})"
        : $"sur le raccourci de restauration ({_settings.GetRestoreHotkey()})";

    public void RaiseHotkeyTexts()
    {
        Raise(nameof(HotkeyHelpText)); Raise(nameof(ActivateHint)); Raise(nameof(RestoreHint));
    }

    public void Refresh()
    {
        if (IsSecret) return;
        _lastScan = WindowEnumerator.GetOpenWindows((uint)Environment.ProcessId)
            .OrderBy(w => w.ApplicationName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

        _selected.IntersectWith(_lastScan.Select(w => w.Handle)); // oublie les fenêtres fermées
        Windows.Clear();
        foreach (var w in _lastScan)
            Windows.Add(new WindowItemViewModel(w, _selected.Contains(w.Handle), OnItemSelectionChanged,
                new RelayCommand(() => CreateRuleRequested?.Invoke(w)), AddQuickRule));
        UpdatePreview();
        Status = $"{Windows.Count} fenêtre(s) détectée(s).";
    }

    /// <summary>
    /// Clic droit sur une fenêtre : ajoute directement une règle au profil actif, sans boîte de dialogue.
    /// Règle « application » : nom de processus seul (toutes ses fenêtres, sur n'importe quel ordinateur).
    /// Règle « fenêtre » : nom de processus + titre actuel (le titre peut changer, ex. onglet d'un navigateur).
    /// </summary>
    void AddQuickRule(WindowInfo w, bool withTitle)
    {
        var profile = _activeProfile;
        if (profile is null) return;

        var rule = new HideRule
        {
            Name = withTitle ? $"{w.ApplicationName} — {w.Title}" : w.ApplicationName,
            ProcessName = w.ProcessName,
            TitleContains = withTitle ? w.Title : null
        };

        // Déjà couvert ? (même règle, ou règle plus large sur l'application)
        var covering = profile.Rules.FirstOrDefault(r =>
            string.Equals(r.ProcessName, rule.ProcessName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(r.TitleContains)
                || (withTitle && string.Equals(r.TitleContains, rule.TitleContains, StringComparison.OrdinalIgnoreCase))));
        if (covering is not null)
        {
            Status = $"Déjà couvert par la règle « {covering.Name} » du profil « {profile.Name} ».";
            return;
        }

        profile.Rules.Add(rule);
        try
        {
            _profiles.Save();
            ReloadProfiles(); // met à jour l'aperçu « Sera masquée »
            Status = $"Règle ajoutée au profil « {profile.Name} » : {rule.Describe()}.";
        }
        catch (Exception ex)
        {
            profile.Rules.Remove(rule);
            Logger.Error("Sauvegarde des profils", ex);
            Status = "Règle non enregistrée : " + ex.Message;
        }
    }

    void OnItemSelectionChanged(WindowItemViewModel item)
    {
        if (item.IsSelected) _selected.Add(item.Info.Handle); else _selected.Remove(item.Info.Handle);
        UpdatePreview();
    }

    void UpdatePreview()
    {
        var toHide = SelectionResolver.Resolve(_lastScan, _selected, Mode, ActiveRules).Select(w => w.Handle).ToHashSet();
        foreach (var item in Windows)
        {
            item.WillBeHidden = toHide.Contains(item.Info.Handle);
            item.ProfileName = _activeProfile?.Name ?? "";
        }
    }

    public void Activate()
    {
        if (IsSecret) return; // activation idempotente

        // Nouveau scan juste avant le masquage : les poignées doivent être à jour.
        _lastScan = WindowEnumerator.GetOpenWindows((uint)Environment.ProcessId);
        var targets = SelectionResolver.Resolve(_lastScan, _selected, Mode, ActiveRules);
        var items = (_activeProfile?.HiddenItems ?? new List<string>()).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (targets.Count == 0 && items.Count == 0) { Status = "Aucune fenêtre sélectionnée, aucune règle ni icône à mettre de côté : rien à masquer."; return; }

        var result = _hiding.Hide(targets, items);
        if (!_hiding.IsSecretActive)
        {
            Status = "Rien n'a pu être masqué. " + string.Join(" ; ", result.Failures);
            return;
        }

        Status = result.Failures.Count == 0
            ? $"{result.Hidden} fenêtre(s) masquée(s)" + (result.HeldItems > 0 ? $", {result.HeldItems} icône(s) mise(s) de côté." : ".")
            : $"{result.Hidden} fenêtre(s) masquée(s), {result.HeldItems} icône(s) mise(s) de côté, {result.Failures.Count} échec(s) : {string.Join(" ; ", result.Failures)}";
        if (_settings.MuteHiddenApps) Status += $" Son coupé : {_hiding.MutedSessionCount} session(s) audio.";

        HideMainWindow?.Invoke();
    }

    public void Restore()
    {
        if (!IsSecret) return;
        var r = _hiding.Restore();
        bool shown = RestoreMainWindow?.Invoke() ?? false;
        if (shown) Refresh(); // inutile si la fenêtre reste cachée
        Status = $"{r.Restored} fenêtre(s) restaurée(s)"
                 + (r.Gone > 0 ? $", {r.Gone} déjà fermée(s)" : "")
                 + (r.Failures.Count > 0 ? $", {r.Failures.Count} échec(s)" : "") + ".";
    }
}
