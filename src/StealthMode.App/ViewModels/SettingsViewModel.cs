using StealthMode.Core.Configuration;

namespace StealthMode.App.ViewModels;

public sealed class SettingsViewModel : ObservableBase
{
    readonly AppSettings _settings;
    string _activateText, _restoreText, _message = "";
    bool _startWithWindows, _minimizeToTray, _hideTray, _showAfterRestore, _muteHidden, _single, _portable;
    readonly bool _portableInitial = AppPaths.IsPortableMarkerPresent;

    /// <summary>
    /// Applique de nouveaux raccourcis (enregistrement réel auprès de Windows) ; retourne les erreurs.
    /// En cas d'erreur l'implémentation doit rétablir les anciens raccourcis.
    /// </summary>
    public Func<HotkeyBinding, HotkeyBinding?, List<string>> ApplyHotkeys { get; set; } = (_, _) => new();
    /// <summary>Déclenché après un enregistrement réussi.</summary>
    public event Action? Saved;

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        _activateText = settings.GetActivateHotkey().ToString();
        _restoreText = settings.GetRestoreHotkey().ToString();
        _minimizeToTray = settings.MinimizeToTray;
        _hideTray = settings.HideTrayIconInSecretMode;
        _showAfterRestore = settings.ShowMainWindowAfterRestore;
        _muteHidden = settings.MuteHiddenApps;
        _single = settings.SingleHotkeyToggle;
        _portable = _portableInitial;
        try { _startWithWindows = AutoStart.IsEnabled(); } catch (Exception ex) { Logger.Error("Lecture du démarrage automatique", ex); }
        SaveCommand = new RelayCommand(Save);
        ResetHotkeysCommand = new RelayCommand(() =>
        {
            ActivateText = HotkeyBinding.DefaultActivate.ToString();
            RestoreText = HotkeyBinding.DefaultRestore.ToString();
        });
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetHotkeysCommand { get; }

    public string ActivateText { get => _activateText; set => Set(ref _activateText, value); }
    public string RestoreText { get => _restoreText; set => Set(ref _restoreText, value); }
    /// <summary>Un seul raccourci bascule entre activation et restauration.</summary>
    public bool SingleHotkeyToggle
    {
        get => _single;
        set { if (Set(ref _single, value)) { Raise(nameof(SeparateHotkeys)); Raise(nameof(ActivateLabel)); } }
    }
    public bool SeparateHotkeys => !_single;
    public string ActivateLabel => _single ? "Raccourci (active, puis restaure)" : "Activer le mode secret";
    /// <summary>Enregistre profils et réglages à côté du programme (clé USB) au lieu du profil Windows.</summary>
    public bool PortableMode { get => _portable; set { if (Set(ref _portable, value)) Raise(nameof(AutostartAvailable)); } }
    public bool AutostartAvailable => !_portable;
    public string DataLocation => AppPaths.Root;
    /// <summary>Fourni par l'application : vrai si le mode secret est actif (le changement de mode portable est alors refusé).</summary>
    public Func<bool> IsSecretActive { get; set; } = () => false;
    /// <summary>Vrai si un changement de mode portable exige de relancer l'application.</summary>
    public bool NeedsRestart { get; private set; }
    public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    public bool MinimizeToTray { get => _minimizeToTray; set => Set(ref _minimizeToTray, value); }
    public bool HideTrayIconInSecretMode { get => _hideTray; set => Set(ref _hideTray, value); }
    public bool ShowMainWindowAfterRestore { get => _showAfterRestore; set => Set(ref _showAfterRestore, value); }
    public bool MuteHiddenApps { get => _muteHidden; set => Set(ref _muteHidden, value); }
    public string Message { get => _message; set => Set(ref _message, value); }

    /// <summary>Vrai si l'enregistrement a réussi (la fenêtre peut se fermer).</summary>
    public bool Succeeded { get; private set; }

    void Save()
    {
        Succeeded = false;
        var restore = _settings.GetRestoreHotkey(); // inchangé si un seul raccourci bascule
        bool valid = HotkeyBinding.TryParse(ActivateText, out var activate)
                     && (SingleHotkeyToggle || HotkeyBinding.TryParse(RestoreText, out restore));
        if (!valid)
        {
            Message = "Raccourci invalide : utilisez au moins un modificateur (Ctrl, Alt, Maj, Win) et une touche F1-F24, lettre ou chiffre.";
            return;
        }
        if (!SingleHotkeyToggle && activate == restore)
        {
            Message = "Les raccourcis d'activation et de restauration doivent être différents.";
            return;
        }

        var errors = ApplyHotkeys(activate, SingleHotkeyToggle ? null : restore);
        if (errors.Count > 0)
        {
            Message = string.Join(" ", errors) + " Les raccourcis précédents sont conservés.";
            return;
        }

        _settings.ActivateHotkey = activate.ToString();
        _settings.RestoreHotkey = restore.ToString();
        _settings.SingleHotkeyToggle = SingleHotkeyToggle;
        _settings.MinimizeToTray = MinimizeToTray;
        _settings.HideTrayIconInSecretMode = HideTrayIconInSecretMode;
        _settings.ShowMainWindowAfterRestore = ShowMainWindowAfterRestore;
        _settings.MuteHiddenApps = MuteHiddenApps;

        var notes = new List<string>();
        // En mode portable, le démarrage automatique pointerait vers la clé USB : on le désactive.
        try { AutoStart.SetEnabled(StartWithWindows && !PortableMode); }
        catch (Exception ex) { Logger.Error("Démarrage automatique", ex); notes.Add("Démarrage automatique non modifié : " + ex.Message); }

        try { _settings.Save(); }
        catch (Exception ex) { Logger.Error("Sauvegarde des paramètres", ex); Message = "Enregistrement impossible : " + ex.Message; return; }

        // Mode portable : appliqué EN DERNIER, une fois les réglages enregistrés, pour que la copie les contienne.
        if (PortableMode != _portableInitial)
        {
            if (IsSecretActive())
            {
                PortableMode = _portableInitial;
                Message = "Restaurez d'abord les fenêtres avant de changer le mode portable.";
                return;
            }
            var err = PortableMode ? AppPaths.EnablePortable() : AppPaths.DisablePortable();
            if (err is not null) { PortableMode = _portableInitial; Message = err; return; }
            NeedsRestart = true;
        }

        Saved?.Invoke();
        if (notes.Count > 0) { Message = string.Join(" ", notes); return; }
        Succeeded = true;
    }
}
