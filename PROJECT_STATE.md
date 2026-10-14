# PROJECT_STATE — Stealth Mode

_Dernière mise à jour : 2026-10-09 (Phases 1 à 4 réalisées ; reste surtout des tests manuels)_

## Objectif
Utilitaire Windows (C# / .NET 8 / WPF / MVVM / Win32) qui masque instantanément des fenêtres choisies (SW_HIDE) sans fermer ni suspendre leurs processus, avec restauration par raccourci. Hors ligne, sans droits admin, interface en français. Utilitaire de confidentialité visuelle, **pas** un outil de sécurité.

## Architecture
```
StealthMode.sln
src/StealthMode.Core       (net8.0-windows, sans WPF)
    Native/         NativeMethods (P/Invoke)
    Models/         WindowInfo, HiddenWindowRecord, Profiles.cs (HideRule, Profile, ProfilesData)
    Configuration/  AppPaths, JsonFile (écriture atomique), Logger, HotkeyBinding, AppSettings, AutoStart
    Services/       WindowEnumerator, WindowHidingService, WindowRestorer, SelectionResolver, RuleMatcher
src/StealthMode.App        (WPF, MVVM)
    ViewModels/     MainViewModel, WindowItemViewModel, ProfilesViewModel, SettingsViewModel
    Views/          MainWindow, ProfilesWindow, SettingsWindow, RuleEditorWindow, InputDialog
    Services/       HotkeyService (RegisterHotKey), TrayService (NotifyIcon)
tools/StealthMode.Recovery (WinExe)  — restaure depuis hidden-state.json, réutilise Core
tests/StealthMode.Tests    (xunit)
```
Écart par rapport au plan initial : code métier dans un projet `Core` séparé (partagé avec l'outil de récupération, testable sans UI).

## Terminé
**Phase 1** : détection des fenêtres ; masquage/restauration réels ; raccourcis globaux ; filets de sécurité (restauration à la sortie, SessionEnding, exceptions non gérées, journal `hidden-state.json` écrit avant masquage, récupération au démarrage, `StealthMode.Recovery.exe`) ; JSON atomique ; logs ; instance unique.

**Phase 2** :
- Interface : thème sombre, profil actif (liste déroulante), boutons Profils / Paramètres / Actualiser, aperçu « Sera masquée » (sélection manuelle **et** règles), bouton « Règle… » par fenêtre pour créer une règle préremplie.
- Règles automatiques : critères nom de processus, chemin d'exécutable, titre contenant (ET entre critères renseignés ; une règle vide ne correspond à rien). Jamais de PID/HWND.
- Profils (profiles.json) : créer, renommer, supprimer (jamais le dernier), choisir l'actif, ajouter/modifier/supprimer des règles. Résultat masqué = sélection manuelle ∪ règles du profil actif.
- Masquage par application ou par fenêtre (case à cocher persistée).
- Paramètres : éditeur de raccourcis par capture de touches (validation, doublons, conflit avec un autre logiciel → anciens raccourcis rétablis), démarrage avec Windows (HKCU\...\Run), réduction dans la zone de notification à la fermeture, masquage de l'icône pendant le mode secret, réaffichage de la fenêtre après restauration.
- Zone de notification : Ouvrir / Activer / Restaurer / Quitter. L'icône n'est masquée en mode secret que si le raccourci de restauration est enregistré.
- Pendant la saisie des raccourcis, les raccourcis globaux sont libérés (sinon la combinaison déclencherait l'action).

**Phase 3** :
- Comportement voulu et conservé (demandé par l'utilisateur) : si le processus est tué (Gestionnaire des tâches), les fenêtres **restent masquées** jusqu'au prochain lancement de Stealth Mode, qui les restaure au démarrage via le journal. `StealthMode.Recovery.exe` fait la même chose sans l'application. Ne pas « corriger » ce comportement.
- Fenêtre dont l'écran a disparu pendant le mode secret : ramenée sur l'écran principal à la restauration (taille inchangée).
- Menu de l'icône : entrée « Paramètres ».
- Mémoire : GC non concurrent, TieredPGO désactivé, `MemoryTrimmer` (un passage quand la fenêtre se masque). Mesuré : ~128 Mo fenêtre ouverte, **~25 Mo en veille**, CPU 0 ms sur 5 s.

**Coupure du son des applications masquées** (demandée par l'utilisateur, option « Couper le son des applications masquées », **activée par défaut**) :
- À l'activation, toutes les sessions audio des **exécutables** des fenêtres masquées sont coupées (Core Audio, `ISimpleAudioVolume`, COM natif sans dépendance), **même si une autre fenêtre de la même application reste visible** (ex. Brave/Chrome : tout le son du navigateur est coupé ; il suffit de rétablir pour récupérer le son). Correspondance par chemin d'exécutable ou PID, car le son d'un navigateur sort d'un autre processus que ses fenêtres.
- Balayage toutes les 1 s **uniquement pendant le mode secret** (nouvelles pistes/sessions). Sessions déjà muettes (choix de l'utilisateur) jamais touchées ni « rétablies ».
- Sessions coupées consignées dans `hidden-state.json` (format `{Windows, MutedSessions}`) → récupération après arrêt forcé / `Recovery.exe`.
- **Découverte importante** : Windows mémorise l'état muet par application. Une application qui se ferme pendant la coupure redémarrerait muette. Parade : `audio-pending.json` (+ `AudioPendingUnmute`) ; ces applications sont rétablies dès qu'une de leurs sessions réapparaît (balayage 3 s, seulement tant que ce fichier n'est pas vide, jamais pendant le mode secret, expiration 7 jours). Vérifié en réel.

**Raccourci unique + fenêtre principale discrète** (demandés par l'utilisateur) :
- Option `SingleHotkeyToggle` (**activée par défaut**) : le raccourci d'activation (`ActivateHotkey`) bascule — il active le mode secret, ou le désactive s'il est déjà actif. Un seul `RegisterHotKey`. Décochée : deux raccourcis distincts comme avant. L'éditeur de raccourcis masque le champ « restauration » en mode bascule.
- Fenêtre principale (`MainWindowHider`) : traitée comme n'importe quelle fenêtre masquée. Au masquage on mémorise si elle était visible et réduite ; au retour : `SW_SHOWMINNOACTIVE` si réduite, sinon `SW_SHOWNA` (jamais d'activation, jamais de premier plan). Si elle était déjà cachée (zone de notification) avant, elle le reste. Distinct de l'option « Réafficher cette fenêtre après la restauration » (`ShowMainWindowAfterRestore`) qui décide **seulement** si elle réapparaît ; décochée, elle reste cachée dans la zone de notification. L'ouverture explicite (icône de notification) active toujours la fenêtre au premier plan.

## Restant
- **Phase 3 (reste)** : arrêt/redémarrage réel de Windows (SessionEnding codé, non testable ici) ; tests Windows 11 (machine de dev = Windows 10 19045) ; « préférences d'interface » au-delà du thème sombre (aucune pour l'instant).
- **Phase 4 : faite** (voir ci-dessous). Reste éventuel : signature numérique (SmartScreen), installateur, version Windows 11 testée.
- Idée : défauts de raccourcis sans touches F (la Surface exige Fn).

## Décisions techniques
- SW_HIDE ; restauration par SW_SHOWNA (SW_SHOWMINNOACTIVE si réduite) : état/position conservés, pas de vol de focus. HWND vérifié (PID + classe) avant toute action.
- Pas de surveillance en boucle : scan à l'actualisation et juste avant l'activation. Fenêtres ouvertes après l'activation jamais masquées (V1).
- Icônes via ExtractIconEx ; aucune dépendance NuGet hors tests. WinForms référencé uniquement pour `NotifyIcon`.
- Par défaut la croix de la fenêtre réduit dans la zone de notification (paramétrable) ; « Quitter » de l'icône ferme vraiment.
- Build : SDK 8.0.425 installé le 2026-10-09. **Livrable : `publish.ps1` -> `dist/StealthMode-autonome/` (StealthMode.exe ~69 Mo, StealthMode.Recovery.exe ~34 Mo), autonomes, fichier unique, runtime inclus.** L'ancien `dist/StealthMode/` (build de développement dépendant du runtime) a été **supprimé le 2026-10-09** à la demande de l'utilisateur ; `dist/StealthMode-portable/` (exécutables + marqueur + `StealthModeData`) est la version à copier sur une clé.

## Bugs / limites connus
- Les applications peuvent réagir différemment masquées (minuteries, rendu) : à vérifier au cas par cas (test 3).
- Aucune garantie de récupération après arrêt brutal / coupure / crash système (le journal aide seulement si le système a gardé les fenêtres).
- `Recovery` ne restaure que les fenêtres du journal.
- Fenêtres UWP/« cloaked » exclues. Titres changeants (ex. onglet actif de Chrome) : une règle sur le titre ne suit que le titre courant au moment de l'activation.
- Audio : seuls les périphériques de sortie actifs sont gérés ; une session créée entre deux balayages (≤ 1 s) peut sonner brièvement ; non testé avec Brave/Chrome réels (testé avec PowerShell/WAV silencieux) ; un périphérique audio branché pendant le mode secret n'est pris en compte qu'au balayage suivant.
- Non testés en réel : éditeur de raccourcis (saisie + conflit), démarrage avec Windows (n'a pas été activé pour ne pas modifier le registre), masquage de l'icône en mode secret, suppression/renommage de profil via l'interface.

## Tests
Automatisés (`dotnet test -c Release`) : **22 réussis le 2026-10-09** (dont fenêtre hors écran ramenée à l'écran ; audio réel avec une fenêtre PowerShell qui joue un WAV silencieux : coupé au masquage / rétabli à la restauration / option désactivée / récupération par journal / application fermée pendant la coupure puis relancée muette puis rétablie). Les tests audio exigent un périphérique de sortie audio actif. Note : le banc d'essai doit utiliser `Application.Run` (un `ShowDialog` se ferme quand on le masque).

Bout en bout réel (UI Automation + touches, 2026-10-09) du raccourci unique et de la fenêtre discrète, avec Ctrl+Alt+W et un notepad d'un autre processus au premier plan : (S1/S2) fenêtre normale → 1re pression masque la cible et l'app, 2e pression **de la même touche** restaure ; l'app revient visible, le premier plan reste notepad ; (S3) app réduite avant → reste réduite après ; (S4) app cachée dans la zone de notification avant → reste cachée. Pas de test automatisé xunit pour cela (interface).

Bout en bout réel avec l'application (raccourcis de l'utilisateur) : fenêtre sonore ciblée par une règle → Ctrl+Alt+W : fenêtre masquée + 1 session audio coupée, journal contenant la session → Ctrl+Maj+W : restaurée + son rétabli, journal supprimé. — raccourcis, résolution fenêtre/application/règles, RuleMatcher (processus, chemin, titre, règle vide), profils (normalisation, aller-retour JSON, fichier corrompu), JSON atomique, masquage/restauration réels de notepad (une fenêtre masquée, l'autre visible, processus vivant, position identique, idempotence), fenêtre fermée pendant le mode secret, récupération par journal.

Réels hors xunit (UI Automation + touches simulées) : coche manuelle + F11/F12 (Phase 1) ; **Phase 2** : profil avec règle « notepad + titre contient ruleA », rien coché → F11 masque seulement A, B reste visible → F12 restaure ; fenêtres Profils et Paramètres s'ouvrent (la règle apparaît dans la liste) ; la croix réduit dans la zone de notification (processus vivant).

Validés par l'utilisateur sur sa machine : tests 1 (Chrome), 4, 5, 6, 8 ; raccourcis OK avec Fn.

| # | Test | État |
|---|------|------|
| 1 | Chrome, une fenêtre sans l'autre | Validé (utilisateur) |
| 2 | VS Code sans fermer ses processus | À faire (manuel) |
| 3 | Claude Code, tâche qui continue | Validé (utilisateur, 2026-10-09) |
| 4 | Ctrl+Alt+F11 | Validé |
| 5 | Ctrl+Alt+F12 | Validé |
| 6 | Bouton | Validé |
| 7 | Restauration depuis l'icône de notification | Validé (utilisateur, 2026-10-09) |
| 8 | Nouvelle application pendant le mode secret | Validé (utilisateur) |
| 9 | Fermer une fenêtre masquée puis restaurer | Réussi (xunit) |
| 10 | Positions conservées | Réussi sur notepad ; multi-écrans à tester |
| 11 | Raccourci déjà utilisé | Code présent (message + retour aux anciens), **non testé en réel** |
| 12 | Fermeture normale / arrêt forcé | **Réussi en réel (2026-10-09)** : processus tué pendant le mode secret → fenêtre reste masquée, processus du programme vivant ; relance de l'app → restaurée ; `Recovery.exe` seul → restaurée |

## Phase 4 (2026-10-09)
- Icône : `ange demon.jpg` (racine, fourni par l'utilisateur) converti en `src/StealthMode.App/Resources/StealthMode.ico` (7 tailles, fond rendu transparent) ; utilisée pour l'exécutable, la fenêtre, l'icône de notification (ressource WPF) et `StealthMode.Recovery.exe`.
- Publication autonome : `publish.ps1` (PublishSingleFile + SelfContained + compression). Testée en réel : démarrage dans un environnement **sans .NET** (DOTNET_ROOT invalide, `dotnet` retiré du PATH), raccourci bascule, masquage, coupure audio, restauration, journal supprimé. Mémoire mesurée sur cette build : ~246 Mo fenêtre ouverte (démarrage), **18 Mo en veille** (zone de notification), CPU 0 ms/5 s. Extraction de ~8 Mo de natifs dans `%TEMP%\.net\StealthMode` au 1er lancement.
- Documentation : `README.md`, `docs/DOCUMENTATION_UTILISATEUR.md`, `docs/DOCUMENTATION_TECHNIQUE.md`.

## Prochaine étape recommandée
1. Tests manuels restants avec les vraies applications (2 VS Code, 3 Claude Code, 7 icône de notification, 11 raccourci déjà pris, Brave pour le son).
2. Optionnel : signature numérique ou installateur ; test sous Windows 11 ; réduire la mémoire au démarrage de la build autonome (~246 Mo).

## Validations utilisateur finales (2026-10-09)
Brave : son coupé au masquage, rétabli à la restauration. Restauration depuis la zone de notification OK. Claude masqué : la tâche continue. Restent non validés : test 2 (VS Code), test 11 (raccourci déjà pris), Windows 11, arrêt/redémarrage réel de Windows.

## Icônes du Bureau / d'un dossier (2026-10-09)
Demande : pouvoir masquer le raccourci de Stealth Mode du Bureau (et d'autres icônes, ou fichiers d'un dossier) pendant le mode secret. Choix de l'utilisateur : **icônes choisies, mises de côté**.
- Pourquoi pas l'attribut « masqué » : l'Explorateur de l'utilisateur affiche les fichiers masqués (Hidden=1), les icônes resteraient visibles. Pourquoi pas « tout le Bureau » : non sélectif (proposé, non retenu).
- `HeldItemsManager` : les éléments choisis (par profil : `Profile.HiddenItems`) sont **déplacés** (renommage, jamais copiés/supprimés) dans `%LOCALAPPDATA%\StealthMode\hold\` puis remis exactement à leur chemin d'origine. Journal (`HiddenState.HeldItems`) écrit **avant** chaque déplacement ; arrêt forcé → les éléments restent de côté jusqu'au prochain lancement (comme les fenêtres), puis `RestoreFromJournal` les remet. Collision de nom pendant le mode secret → rien n'est écrasé, l'original revient sous « nom (restauré).ext ». Élément qui ne peut pas être remis → conservé dans `hold`, reste au journal, nouvelle tentative au démarrage suivant.
- Gardes (`Validate`) : refus des racines de disque, dossiers Windows/Program Files/profil/dossier Bureau lui-même/AppData\Local, du dossier interne, et de tout dossier contenant le programme en cours d'exécution. Même volume que `%LOCALAPPDATA%` obligatoire (sinon un déplacement serait une copie) → message d'échec. Fichier verrouillé → échec signalé, les autres continuent.
- Le mode secret peut être activé avec **uniquement** des icônes (aucune fenêtre).
- UI : `Profils…` → section « Icônes à mettre de côté » (ajouter des fichiers/raccourcis [dialogue sans déréférencer les .lnk], ajouter un dossier, retirer).
- Tests : 28 xunit réussis (dont 6 pour les éléments mis de côté : fichiers+dossiers, collision, fichier verrouillé, récupération après crash, chemins dangereux, élément manquant). Bout en bout réel avec `StealthMode.exe` autonome : fichier et dossier du vrai Bureau disparaissent au raccourci (aucune fenêtre cochée), reviennent intacts ; arrêt forcé → restent de côté ; relance → remis ; Bureau nettoyé.
- **Non vérifié** : que chaque icône retrouve exactement sa position sur le Bureau (Windows la mémorise normalement par nom, mais mon test automatique n'a pas pu le confirmer car le Bureau était recouvert). À vérifier visuellement par l'utilisateur ; Bureau redirigé vers OneDrive non testé (la synchro verrait un déplacement).

## Bulle d'aide « ? » (2026-10-09)
Bouton « ? » à côté du nom dans la fenêtre principale ; ouvre une bulle (Popup, `StaysOpen=False`, se ferme au clic ailleurs ou Échap) avec la description de l'application et un mini mode d'emploi (texte validé par l'utilisateur). Le ou les **raccourcis affichés sont les raccourcis réellement configurés** (`MainViewModel.ActivateHint` / `RestoreHint`, mis à jour après modification dans les Paramètres ; un seul raccourci ou deux selon l'option). Correction associée : `Profile.ToString()` renvoie le nom (la case des listes déroulantes affichait « StealthMode.Core.Models.Profile »). Vérifié par UI Automation : la bulle s'ouvre, contient le raccourci Ctrl+Alt+W de l'utilisateur, se ferme avec Échap et se rouvre. Non vérifié visuellement (mise en page).

## Mode portable (2026-10-09)
Demandé par l'utilisateur (clé USB, profils qui suivent, ex. « masquer Chrome » sur un autre PC). Fichier `portable` à côté de l'exe → données dans `StealthModeData\` à côté de l'exe (`AppPaths.ComputeRoot`). Interrupteur dans les Paramètres (`EnablePortable`/`DisablePortable` + redémarrage `--restart`), refusé en mode secret ; démarrage automatique désactivé en portable ; `HoldRoot` toujours local (même volume que les icônes) ; `HeldItemRecord.Machine` pour qu'un journal d'un autre PC ne soit pas consommé ; `JsonFile` avec repli FAT ; `publish.ps1` produit aussi `dist\StealthMode-portable`. `dist\StealthMode-portable\StealthModeData` contient une **copie** des profils/réglages de l'utilisateur du 2026-10-09 (originaux inchangés).
Tests : 33 xunit réussis (5 nouveaux : choix du dossier, activation sans écrasement, retour au mode normal, erreur sur emplacement non inscriptible, journal d'un autre ordinateur). Réel : exe depuis un dossier « clé » simulé → profil lu depuis la clé, règle appliquée (cible masquée, autre fenêtre visible), même touche restaure, rien écrit dans le profil Windows ; interrupteur des Paramètres → redémarrage automatique, marqueur créé, profils copiés, nouvelle instance en données portables. **Non testé** : vraie clé USB (FAT32/exFAT), clé retirée en mode secret, autre ordinateur, clé en lecture seule.

## Idée gardée de côté : « clé USB = badge » (fonction premium potentielle, NON implémentée)
Idée de l'utilisateur (2026-10-09) : retirer la clé USB active automatiquement le mode secret (profil actif), la remettre restaure. **Rien de cela n'existe aujourd'hui** : retirer la clé ne masque rien, et si le mode secret était actif le programme (qui tourne depuis la clé) s'arrête ou plante (non testé) sans restauration automatique au retour de la clé.
Pistes si on la construit : détecter le retrait/retour du volume (`WM_DEVICECHANGE`, `DBT_DEVICEREMOVECOMPLETE`, volume de l'exécutable) ; faire survivre le programme au retrait (copie de l'exe dans `%TEMP%` + relance) ; garder le journal de secours sur le disque du PC tant que la clé est absente ; vérifier masquage / audio / icônes mises de côté quand le volume disparaît en cours de route ; gérer le retrait pendant un mode secret déjà actif ; option dans les Paramètres, désactivée par défaut ; décider si le retour de la clé restaure automatiquement ou seulement au raccourci. À présenter comme une commodité, pas une protection (le masquage n'est pas un verrouillage ; Win+L reste la bonne réponse pour une vraie absence).

## Clic droit sur une fenêtre + bouton « Terminer » (2026-10-09)
- Fenêtre principale : **clic droit sur une ligne** → menu (sombre, `ContextMenu`/`MenuItem` stylés dans `App.xaml`) : « Toujours masquer {application} » (règle = nom de processus seul, toutes les fenêtres), « Toujours masquer cette fenêtre : titre contenant … » (processus + titre actuel ; peut changer, ex. onglet de navigateur), « Créer une règle personnalisée… » (éditeur existant). Ajout **direct** au profil actif, sans boîte de dialogue ; libellés citant le profil ; doublon / règle plus large déjà présente détectés (« Déjà couvert par la règle… ») ; aperçu « Sera masquée » mis à jour ; message d'état (`MainViewModel.AddQuickRule`).
- Fenêtre « Profils et règles » : bouton **« Terminer »** (Échap fonctionne aussi ; pas de touche Entrée par défaut) + texte « Les modifications sont enregistrées automatiquement. Fermer cette fenêtre ne quitte pas Stealth Mode. ».
- Vérifié hors écran (banc d'essai jetable avec dossier de données temporaire, vrais profils non touchés) : 9 contrôles de logique OK (libellés, règles application/fenêtre, doublon, persistance, aperçu) et rendu du menu sombre/lisible ; 33 xunit OK. **Non vérifié en réel** : l'ouverture effective du menu par un vrai clic droit et la mise en page à l'écran.
