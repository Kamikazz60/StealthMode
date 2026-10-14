# Stealth Mode — Documentation technique

## Pile technique
C#, .NET 8, WPF (MVVM), Win32 par P/Invoke, COM natif pour l'audio. JSON local. Hors ligne. Aucune dépendance NuGet dans l'application (xunit uniquement pour les tests). WinForms n'est référencé que pour `NotifyIcon`.

## Structure
```
StealthMode.sln
src/StealthMode.Core        (net8.0-windows, sans WPF)
    Native/NativeMethods.cs       P/Invoke user32, kernel32, dwmapi, shell32
    Audio/                        COM Core Audio, AudioSessionMuter, AudioPendingUnmute
    Models/                       WindowInfo, HiddenWindowRecord, HiddenState, HideRule, Profile, ProfilesData
    Configuration/                AppPaths, JsonFile (atomique), Logger, HotkeyBinding, AppSettings, AutoStart
    Services/                     WindowEnumerator, WindowHidingService, WindowRestorer, SelectionResolver, RuleMatcher
src/StealthMode.App         (WPF)
    ViewModels/  Views/           MainViewModel, ProfilesViewModel, SettingsViewModel, fenêtres XAML
    Services/                     HotkeyService, TrayService, MainWindowHider, MemoryTrimmer
    Resources/StealthMode.ico
tools/StealthMode.Recovery  récupération autonome (réutilise Core)
tests/StealthMode.Tests     xunit (dont tests réels contre Windows)
publish.ps1                 publication autonome
```
Le code métier est dans `Core` pour être partagé avec l'outil de récupération et testé sans interface.

## Fonctionnement
### Détection (`WindowEnumerator`)
`EnumWindows`, puis filtre « fenêtre de barre des tâches » : visible, titre non vide, non « cloaked » (DWM), classes du shell exclues, pas de fenêtre outil ni possédée (sauf `WS_EX_APPWINDOW`), pas de `WS_EX_NOACTIVATE`, pas Stealth Mode lui-même. Le processus donne nom, chemin (`QueryFullProcessImageName`) et description du fichier.

### Masquage et restauration (`WindowHidingService`, `WindowRestorer`)
- Masquage : `ShowWindow(SW_HIDE)` puis contrôle que la fenêtre n'est plus visible. `SW_HIDE` retire la fenêtre de la barre des tâches et d'Alt+Tab sans toucher au processus. Pas de manipulation de styles.
- Avant chaque masquage, la fenêtre est ajoutée au **journal** `hidden-state.json` (écriture atomique).
- Restauration : `SW_SHOWNA` (état, position et ordre conservés, sans focus) ou `SW_SHOWMINNOACTIVE` si elle était réduite. Avant d'agir on vérifie que le HWND correspond toujours au même PID et à la même classe (les HWND sont réutilisés). Si l'écran d'origine n'existe plus (`MonitorFromRect`), la fenêtre est ramenée sur l'écran principal sans changer sa taille.
- Idempotence : activer un mode déjà actif, ou restaurer sans rien de masqué, ne fait rien.

### Sélection et règles
`SelectionResolver` : sélection manuelle (par fenêtre ou par application via `AppKey` = chemin d'exécutable) ∪ règles du profil actif (`RuleMatcher` : nom de processus, chemin, titre contient — ET entre critères renseignés ; une règle vide ne correspond à rien). Jamais de PID/HWND dans les règles.

### Raccourcis (`HotkeyService`)
`RegisterHotKey` sur une fenêtre de messages (`HWND_MESSAGE`) avec `MOD_NOREPEAT`. Pas de hook clavier global. Erreur 1409 = déjà utilisé par un autre logiciel. Mode bascule : un seul identifiant enregistré, qui active ou restaure selon l'état. Pendant la saisie d'un raccourci dans les Paramètres les raccourcis sont libérés, et en cas de refus les anciens sont rétablis.

### Fenêtre principale (`MainWindowHider`)
Traitée comme une fenêtre masquée ordinaire : on mémorise visible/réduite, `SW_HIDE`, puis `SW_SHOWNA` / `SW_SHOWMINNOACTIVE` au retour. Si elle était déjà cachée, elle le reste. L'ouverture explicite par l'icône de notification active la fenêtre.

### Audio (`AudioSessionMuter`)
COM minimal (`IMMDeviceEnumerator`, `IAudioSessionManager2`, `IAudioSessionControl2`, `ISimpleAudioVolume`) sur tous les périphériques de sortie actifs. Les sessions sont rattachées à une application par chemin d'exécutable ou PID, car le son d'un navigateur sort d'un autre processus que ses fenêtres. Seules les sessions non déjà muettes sont coupées. Pendant le mode secret, un balayage d'une seconde capte les nouvelles sessions.

**Point d'attention : Windows mémorise l'état muet par application.** Une application qui se ferme alors qu'elle est coupée redémarre muette. `AudioPendingUnmute` consigne ces applications dans `audio-pending.json` ; leur première session suivante est rétablie (balayage de 3 s uniquement tant que le fichier existe, jamais pendant le mode secret, expiration à 7 jours).

### Icônes mises de côté (`HeldItemsManager`)
Déplacement (File.Move / Directory.Move, même volume) vers `%LOCALAPPDATA%\StealthMode\hold\`, consigné dans `hidden-state.json` (`HeldItems`) avant chaque déplacement ; remise à `OriginalPath` sans jamais écraser (suffixe « (restauré) » en cas de collision) ; dossier `hold` supprimé seulement s'il est vide ; élément non remis = conservé et reconsigné. `Validate` refuse racines de disque, dossiers système, Bureau lui-même, dossier interne et dossier contenant le programme en cours. Choisi plutôt que l'attribut « masqué », inopérant quand l'Explorateur affiche les fichiers masqués.

### Fiabilité
- Restauration à la fermeture normale, à `SessionEnding` et sur exception non gérée.
- Après un arrêt brutal, les fenêtres restent masquées (comportement voulu) jusqu'au prochain lancement qui rejoue le journal, ou jusqu'à `StealthMode.Recovery.exe`. Le journal contient fenêtres et sessions audio coupées.
- L'icône de notification n'est masquée en mode secret que si le raccourci est enregistré.
- Instance unique (mutex `Local\StealthMode.SingleInstance`).
- Mémoire : GC non concurrent, un `MemoryTrimmer` après chaque masquage de la fenêtre ; aucune boucle permanente.

### Fichiers (`%LOCALAPPDATA%\StealthMode`)
| Fichier | Rôle |
|---|---|
| `settings.json` | raccourcis, options |
| `profiles.json` | profils et règles |
| `hidden-state.json` | journal de récupération : fenêtres, sessions audio coupées, éléments mis de côté (présent seulement en mode secret) |
| `hold\` | éléments mis de côté pendant le mode secret (supprimé quand vide) |
| `audio-pending.json` | applications à rétablir au prochain démarrage |
| `logs\AAAA-MM-JJ.log` | journal technique |
Écriture atomique (`.tmp` puis remplacement) ; valeurs par défaut si absent ou invalide.

## Compilation, tests, publication
Prérequis : SDK .NET 8.
```
dotnet build -c Release
dotnet test -c Release
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```
`publish.ps1` produit dans `dist\StealthMode-autonome` deux fichiers uniques, autonomes (runtime inclus, compressés) : `StealthMode.exe` (~69 Mo) et `StealthMode.Recovery.exe` (~34 Mo). Au premier lancement, quelques bibliothèques natives (~8 Mo) sont extraites dans `%TEMP%\.net\StealthMode` ; le reste du programme est décompressé en mémoire. Le dossier de l'exécutable n'est jamais modifié.

## Tests
- Logique pure : raccourcis, sélection, règles, profils, JSON atomique.
- **Tests réels contre Windows** (notepad, et une fenêtre PowerShell jouant un WAV silencieux pour avoir une vraie session audio) : masquage/restauration, position, fenêtre fermée pendant le mode secret, récupération par journal, fenêtre hors écran, coupure et rétablissement du son, application fermée pendant la coupure. Ils exigent une session interactive et, pour l'audio, un périphérique de sortie actif. Les tests qui modifient `AppPaths.Root` sont dans la collection xunit `AppPaths` (non parallèles).
- Non automatisés (manuels) : applications réelles (Chrome/Brave, VS Code, Claude Code), arrêt/redémarrage de Windows, Windows 11, multi-écrans réels.

## Limites techniques
- `SW_HIDE` ne garantit pas que toutes les applications se comportent comme si elles étaient visibles.
- Les fenêtres UWP/« cloaked » sont exclues volontairement.
- WPF ne supporte pas le « trimming » : l'exécutable autonome est volumineux.
- Le programme n'est pas signé numériquement (SmartScreen peut avertir).

## Mode portable
`AppPaths` choisit le dossier de données au démarrage : si un fichier `portable` existe à côté de l'exécutable (`Environment.ProcessPath`), les données vont dans `StealthModeData\` à côté de l'exécutable ; sinon dans `%LOCALAPPDATA%\StealthMode`. `StealthMode.Recovery.exe` applique la même règle (il est dans le même dossier). `AppPaths.EnablePortable()` copie `settings.json`/`profiles.json` sans jamais écraser des données déjà présentes à destination puis crée le marqueur ; `DisablePortable()` recopie vers le profil Windows et supprime le marqueur ; le changement s'applique par redémarrage (`--restart` : la nouvelle instance attend jusqu'à 10 s le verrou d'instance unique). Refusé pendant le mode secret.

Adaptations pour un support amovible :
- `JsonFile.SaveAtomic` : repli par copie si `File.Replace` échoue (FAT32/exFAT) ; non testé sur un vrai volume FAT.
- `AppPaths.HoldRoot` est toujours sur le disque de l'ordinateur (`%LOCALAPPDATA%\StealthMode\hold`), car un déplacement n'est possible que sur le même volume que l'icône.
- `HeldItemRecord.Machine` : un journal venu d'un autre ordinateur est conservé sans toucher aux éléments (ils sont sur l'autre machine) et ils sont remis quand la clé revient sur leur ordinateur.
- Le démarrage automatique est désactivé en mode portable.
- `publish.ps1` produit `dist\StealthMode-portable` (exécutables + marqueur) ; le dossier `StealthModeData` n'est jamais écrasé par le script.
