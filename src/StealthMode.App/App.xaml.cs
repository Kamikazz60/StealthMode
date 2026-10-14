using System.Windows;
using StealthMode.App.Services;
using StealthMode.App.ViewModels;
using StealthMode.App.Views;
using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using StealthMode.Core.Services;

namespace StealthMode.App;

public partial class App : Application
{
    Mutex? _singleInstance;
    WindowHidingService? _hiding;
    HotkeyService? _hotkeys;
    TrayService? _tray;
    MainWindowHider? _mainHider;
    MainWindow? _window;
    AppSettings _settings = new();
    ProfilesData _profiles = new();
    MainViewModel? _vm;
    bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Après un redémarrage demandé (changement de mode portable), on laisse jusqu'à 10 s à l'ancienne instance pour se fermer.
        bool restarting = e.Args.Contains("--restart");
        bool first;
        for (int attempt = 0; ; attempt++)
        {
            _singleInstance = new Mutex(true, @"Local\StealthMode.SingleInstance", out first);
            if (first || !restarting || attempt >= 40) break;
            _singleInstance.Dispose();
            Thread.Sleep(250);
        }
        if (!first)
        {
            MessageBox.Show("Stealth Mode est déjà en cours d'exécution.", "Stealth Mode");
            Shutdown();
            return;
        }

        // Filets de sécurité : toute sortie anormale tente de réafficher les fenêtres.
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Exception non gérée (UI)", args.Exception);
            SafeRestore();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Error("Exception non gérée", args.ExceptionObject as Exception);
            SafeRestore();
        };
        SessionEnding += (_, _) => { _exiting = true; SafeRestore(); };

        // Fenêtres restées masquées par une exécution précédente interrompue.
        var leftover = WindowRestorer.RestoreFromJournal();
        if (leftover is not null) Logger.Info($"Récupération au démarrage : {leftover.Restored} fenêtre(s) restaurée(s)");

        _settings = AppSettings.Load();
        _profiles = ProfilesData.Load();
        _hiding = new WindowHidingService();
        _hiding.MuteAudio = _settings.MuteHiddenApps;
        _hiding.StartPendingWatcherIfNeeded(); // applications coupées puis fermées lors d'une session précédente
        _vm = new MainViewModel(_hiding, _settings, _profiles);
        _window = new MainWindow(_vm);

        _hotkeys = new HotkeyService();
        var errors = RegisterHotkeys();
        // Un seul raccourci : il bascule (active, ou restaure si le mode secret est déjà actif). Sinon deux raccourcis distincts.
        _hotkeys.ActivatePressed += () => { if (_settings.SingleHotkeyToggle && _vm.IsSecret) _vm.Restore(); else _vm.Activate(); };
        _hotkeys.RestorePressed += _vm.Restore;

        _tray = new TrayService(
            open: ShowMainWindow,
            activate: _vm.Activate,
            restore: _vm.Restore,
            settings: () => { ShowMainWindow(); OpenSettings(); },
            exit: ExitApplication);

        _mainHider = new MainWindowHider(_window);
        _vm.HideMainWindow = () => { _mainHider.Hide(); TrimLater(); };
        // Retour du mode secret : la fenêtre se comporte comme n'importe quelle fenêtre (état conservé, pas de premier plan).
        _vm.RestoreMainWindow = () => _mainHider.Restore(_settings.ShowMainWindowAfterRestore);
        _vm.OpenProfilesRequested = OpenProfiles;
        _vm.OpenSettingsRequested = OpenSettings;
        _vm.CreateRuleRequested = CreateRuleFromWindow;

        // Fermer la fenêtre réduit dans la zone de notification si le paramètre est actif.
        _window.Closing += (_, args) =>
        {
            if (_exiting || !_settings.MinimizeToTray) return;
            args.Cancel = true;
            _window.Hide();
            TrimLater();
        };

        // Sécurité : l'icône ne disparaît que si le raccourci de restauration est actif.
        _hiding.StateChanged += UpdateTrayVisibility;
        UpdateTrayVisibility();

        _window.Show();
        if (errors.Count > 0) _vm.Status = string.Join(" ", errors);
    }

    /// <summary>Libère la mémoire une fois l'interface au repos (pas de boucle : un seul passage par masquage).</summary>
    void TrimLater() =>
        Dispatcher.BeginInvoke(MemoryTrimmer.Trim, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    void UpdateTrayVisibility()
    {
        bool hide = _hiding!.IsSecretActive && _settings.HideTrayIconInSecretMode && _hotkeys!.RestoreRegistered;
        _tray?.SetVisible(!hide);
    }

    /// <summary>Ouverture demandée par l'utilisateur (icône de notification) : au premier plan.</summary>
    void ShowMainWindow() => _mainHider!.ShowAndActivate();

    List<string> RegisterHotkeys() =>
        _hotkeys!.Register(_settings.GetActivateHotkey(), _settings.SingleHotkeyToggle ? null : _settings.GetRestoreHotkey());

    void ExitApplication()
    {
        _exiting = true;
        Shutdown();
    }

    void OpenProfiles()
    {
        var pvm = new ProfilesViewModel(_profiles);
        pvm.Changed += _vm!.ReloadProfiles;
        new ProfilesWindow(pvm) { Owner = _window }.ShowDialog();
        _vm.ReloadProfiles();
    }

    void OpenSettings()
    {
        var svm = new SettingsViewModel(_settings)
        {
            // Si un raccourci est refusé, on rétablit immédiatement les précédents.
            ApplyHotkeys = (activate, restore) =>
            {
                var errs = _hotkeys!.Register(activate, restore);
                if (errs.Count > 0) RegisterHotkeys();
                return errs;
            }
        };
        svm.Saved += () => { _hiding!.MuteAudio = _settings.MuteHiddenApps; _vm!.RaiseHotkeyTexts(); UpdateTrayVisibility(); };

        // Libère les raccourcis pendant la saisie, sinon la combinaison déclencherait l'action au lieu d'être lue.
        _hotkeys!.Unregister();
        svm.IsSecretActive = () => _hiding!.IsSecretActive;
        try { new SettingsWindow(svm) { Owner = _window }.ShowDialog(); }
        finally
        {
            var errs = RegisterHotkeys();
            if (errs.Count > 0) _vm!.Status = string.Join(" ", errs);
            UpdateTrayVisibility();
        }
        if (svm.NeedsRestart) RestartApplication();
    }

    /// <summary>Relance l'application (les chemins de données changent avec le mode portable) puis quitte celle-ci.</summary>
    void RestartApplication()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--restart") { UseShellExecute = false });
            ExitApplication();
        }
        catch (Exception ex)
        {
            Logger.Error("Redémarrage", ex);
            _vm!.Status = "Mode portable modifié : fermez puis relancez Stealth Mode pour l'appliquer.";
        }
    }

    void CreateRuleFromWindow(WindowInfo w)
    {
        var rule = RuleEditorWindow.Edit(_window, new HideRule { Name = w.ApplicationName, ProcessName = w.ProcessName });
        if (rule is null) return;
        _profiles.Active.Rules.Add(rule);
        try
        {
            _profiles.Save();
            _vm!.ReloadProfiles();
            _vm.Status = $"Règle « {rule.Name} » ajoutée au profil « {_profiles.Active.Name} ».";
        }
        catch (Exception ex)
        {
            Logger.Error("Sauvegarde des profils", ex);
            _vm!.Status = "Règle non enregistrée : " + ex.Message;
        }
    }

    void SafeRestore()
    {
        try { _hiding?.Restore(); } catch (Exception ex) { Logger.Error("Restauration de secours", ex); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SafeRestore();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
