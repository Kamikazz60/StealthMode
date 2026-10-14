namespace StealthMode.Core.Configuration;

/// <summary>Paramètres persistés dans settings.json. Les valeurs invalides retombent sur les défauts.</summary>
public sealed class AppSettings
{
    public string ActivateHotkey { get; set; } = HotkeyBinding.DefaultActivate.ToString();
    public string RestoreHotkey { get; set; } = HotkeyBinding.DefaultRestore.ToString();
    public bool SelectByApplication { get; set; }

    /// <summary>Un seul raccourci (ActivateHotkey) bascule : il active le mode secret, ou le désactive s'il est déjà actif.</summary>
    public bool SingleHotkeyToggle { get; set; } = true;

    /// <summary>La croix de la fenêtre réduit dans la zone de notification au lieu de quitter.</summary>
    public bool MinimizeToTray { get; set; } = true;
    /// <summary>Masque l'icône de notification pendant le mode secret (seulement si le raccourci de restauration est actif).</summary>
    public bool HideTrayIconInSecretMode { get; set; }
    /// <summary>
    /// Réaffiche la fenêtre principale après la restauration (sinon elle reste cachée dans la zone de notification).
    /// Quand elle revient, c'est dans son état d'avant (réduite reste réduite) et sans prendre le premier plan.
    /// </summary>
    public bool ShowMainWindowAfterRestore { get; set; } = true;

    /// <summary>Coupe le son des applications masquées (par exécutable : tout le son de l'application, même si une autre de ses fenêtres reste visible).</summary>
    public bool MuteHiddenApps { get; set; } = true;

    public HotkeyBinding GetActivateHotkey() =>
        HotkeyBinding.TryParse(ActivateHotkey, out var b) ? b : HotkeyBinding.DefaultActivate;

    public HotkeyBinding GetRestoreHotkey() =>
        HotkeyBinding.TryParse(RestoreHotkey, out var b) ? b : HotkeyBinding.DefaultRestore;

    public static AppSettings Load() => JsonFile.LoadOrDefault<AppSettings>(AppPaths.SettingsFile);
    public void Save() => JsonFile.SaveAtomic(AppPaths.SettingsFile, this);
}
