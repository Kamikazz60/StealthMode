# Stealth Mode — Documentation utilisateur

Stealth Mode masque instantanément des fenêtres Windows que vous choisissez, **sans fermer ni suspendre** leurs programmes, pour laisser votre ordinateur utilisable par quelqu'un d'autre.

> C'est un outil de **confidentialité visuelle**, pas de sécurité. Les programmes masqués continuent de tourner : ils restent visibles dans le Gestionnaire des tâches, et quelqu'un qui sait chercher peut les retrouver. Stealth Mode ne verrouille pas Windows et ne protège pas vos fichiers.

## Installation et lancement
Aucune installation. Copiez le dossier `StealthMode-autonome` où vous voulez (Bureau, clé USB…) puis double-cliquez sur **`StealthMode.exe`**. Aucun runtime .NET, aucun compte, aucune connexion Internet, aucun droit administrateur.

Windows 10 ou 11, 64 bits. Si Windows SmartScreen signale un éditeur inconnu (le programme n'est pas signé numériquement), choisissez « Informations complémentaires » puis « Exécuter quand même ».

Un bouton **« ? »** à côté du nom, dans la fenêtre principale, affiche à tout moment une aide courte avec votre raccourci actuel.

## Utilisation de base
1. Dans la liste, **cochez** les fenêtres à masquer. La mention « Sera masquée » confirme celles qui le seront réellement.
2. Appuyez sur le raccourci (par défaut **Ctrl+Alt+F11**) ou sur « Activer le mode secret » : les fenêtres cochées disparaissent de l'écran, de la barre des tâches et d'Alt+Tab. L'interface se masque aussi.
3. Appuyez de nouveau sur **le même raccourci** : tout revient. Vous pouvez aussi utiliser le bouton « Restaurer les fenêtres » ou l'icône de la zone de notification (clic droit → « Restaurer les fenêtres »).

Les applications ouvertes **après** l'activation restent visibles. Une fenêtre fermée pendant le mode secret est simplement ignorée à la restauration.

**Portables et Surface :** les touches F11/F12 sont souvent des touches multimédia. Utilisez **Ctrl+Alt+Fn+F11**, activez le verrouillage de la touche Fn, ou choisissez un autre raccourci dans les Paramètres (par exemple Ctrl+Alt+W).

### Par fenêtre ou par application
- Par défaut, seules les fenêtres cochées sont masquées (ex. masquer deux fenêtres Chrome et laisser la troisième).
- Avec « Masquer toutes les fenêtres des applications cochées », cocher une fenêtre masque toutes celles de la même application.
- Les onglets d'un navigateur ne peuvent pas être masqués individuellement : seule la fenêtre entière l'est.

## Règles automatiques et profils
**Raccourci pratique :** faites un **clic droit** sur une fenêtre de la liste pour l'ajouter directement au profil actif (toute l'application, ou seulement cette fenêtre d'après son titre). Dans la fenêtre « Profils… », le bouton **Terminer** ferme la fenêtre sans quitter Stealth Mode ; les modifications sont enregistrées automatiquement.

- Un **profil** regroupe des règles (ex. « Personnel », « Travail »). Le profil actif se choisit en haut de la fenêtre ; **Profils…** permet de créer, renommer, supprimer et de gérer les règles.
- Une **règle** s'appuie sur le nom du processus, le chemin de l'exécutable et/ou un texte contenu dans le titre de la fenêtre (tous les critères renseignés doivent correspondre). Exemple : processus `Code` = toujours masquer VS Code ; processus `chrome` + titre contenant `Gmail`.
- Le bouton **Règle…** d'une ligne crée une règle préremplie à partir de cette fenêtre.
- À chaque activation, les fenêtres cochées **et** celles qui correspondent aux règles du profil actif sont masquées. L'aperçu « Sera masquée » montre le résultat.

## Le son
Par défaut, le son des applications masquées est **coupé** (option dans les Paramètres). C'est le son de toute l'application qui est coupé, même si une autre de ses fenêtres reste visible (par exemple tout Brave ou Chrome). Il est rétabli à la restauration. Le volume général de Windows n'est jamais modifié, et une application que vous aviez déjà coupée vous-même n'est pas touchée.

## Paramètres
- **Raccourcis :** cliquez dans le champ, puis appuyez sur la combinaison. Un raccourci déjà utilisé par un autre logiciel est refusé et l'ancien est conservé. Case « Un seul raccourci » : un même raccourci active puis restaure ; décochée, deux raccourcis distincts.
- **Couper le son des applications masquées.**
- **Démarrer avec Windows.**
- **Réduire dans la zone de notification à la fermeture :** la croix de la fenêtre cache l'application dans la zone de notification au lieu de la quitter. Le menu de l'icône permet de l'ouvrir, d'activer, de restaurer, d'ouvrir les paramètres ou de quitter.
- **Masquer l'icône pendant le mode secret :** n'est appliqué que si le raccourci est bien actif, pour toujours garder un moyen de restaurer.
- **Réafficher cette fenêtre après la restauration :** la fenêtre de Stealth Mode revient comme n'importe quelle fenêtre : réduite si elle l'était, dans la zone de notification si elle y était, et jamais au premier plan toute seule. Décochée, elle reste dans la zone de notification.

Les paramètres sont stockés dans `%LOCALAPPDATA%\StealthMode` (`settings.json`, `profiles.json`, `logs\`). Supprimer ce dossier remet tout à zéro.

## En cas de problème
- **Des fenêtres restent masquées après un arrêt brutal** (par exemple arrêt forcé dans le Gestionnaire des tâches) : elles restent masquées jusqu'à ce que vous **relanciez `StealthMode.exe`**, qui les restaure au démarrage. Sinon lancez **`StealthMode.Recovery.exe`** (même dossier), qui fait la même chose seul, sans interface. Aucune garantie après une coupure de courant ou un plantage de Windows.
- **Un raccourci ne réagit pas :** vérifiez la touche Fn sur portable, ou changez de combinaison dans les Paramètres. Le journal (`logs\`) indique si le raccourci a été enregistré ou refusé.
- **Une application est restée muette :** rouvrez Stealth Mode, il rétablit le son dès que l'application réapparaît ; sinon, utilisez le mélangeur de volume de Windows.
- **Une fenêtre refuse d'être masquée** (certaines fenêtres système ou protégées) : un message l'indique et les autres sont masquées normalement.
- **Une fenêtre restaurée est hors écran** (écran débranché) : elle est ramenée sur l'écran principal.

## Limites connues
- Un programme masqué peut se comporter différemment de quand il est visible (minuteries, rendu graphique). Testez avec vos propres applications avant de compter dessus.
- Stealth Mode n'interagit pas avec les onglets d'un navigateur et ne modifie jamais leur contenu.
- Il ne cache pas les processus, n'installe aucun pilote, n'injecte rien dans d'autres programmes et n'écoute pas votre clavier (seuls les raccourcis déclarés sont reçus).

## Masquer des icônes du Bureau ou d'un dossier
Dans **Profils…**, section « Icônes à mettre de côté » : ajoutez des fichiers, des raccourcis ou des dossiers (par exemple le raccourci de Stealth Mode posé sur votre Bureau). Pendant le mode secret, ils sont déplacés dans un dossier de Stealth Mode, donc leurs icônes disparaissent ; à la restauration ils sont remis exactement à leur place. Les icônes sont propres à chaque profil, et le mode secret peut être activé même si aucune fenêtre n'est cochée.

À savoir :
- Rien n'est copié ni supprimé : c'est un déplacement. Si le programme est arrêté de force, les éléments restent de côté jusqu'au prochain lancement de Stealth Mode (comme les fenêtres), qui les remet.
- Si vous créez un fichier du même nom pendant le mode secret, il n'est jamais écrasé : l'original revient sous « nom (restauré) ».
- Les éléments doivent être sur le même disque que `%LOCALAPPDATA%` (en général C:). Un fichier utilisé par un programme ne peut pas être déplacé : un message l'indique.
- Il est impossible d'ajouter un disque entier, les dossiers Windows ou Program Files, le dossier Bureau lui-même, ni le dossier qui contient Stealth Mode.
- Après la restauration, vérifiez que vos icônes ont retrouvé leur emplacement sur le Bureau ; Windows retient normalement la position par nom, mais ce n'est pas garanti.

## Mode portable (clé USB)
Pour emporter Stealth Mode **et vos profils** sur une clé USB :
1. Copiez le dossier `StealthMode-portable` sur la clé. Il contient `StealthMode.exe`, `StealthMode.Recovery.exe`, un fichier `portable` (c'est lui qui active le mode portable) et un dossier `StealthModeData` avec vos profils et réglages.
2. Sur n'importe quel ordinateur Windows 10/11 64 bits, lancez `StealthMode.exe` depuis la clé. Vos profils, règles et raccourcis sont ceux de la clé : par exemple une règle « processus chrome » masque Chrome sur tous les ordinateurs, et tout ce que vous modifiez est enregistré sur la clé. Rien n'est écrit dans le profil Windows de l'ordinateur (hormis les fichiers temporaires du programme).

Vous pouvez aussi l'activer depuis un exemplaire normal : **Paramètres → Mode portable**. L'application copie vos profils et réglages à côté de `StealthMode.exe`, crée le fichier `portable` et redémarre. Décocher la case fait l'inverse (vos données sont recopiées dans le profil Windows).

À savoir en mode portable :
- **Ne retirez jamais la clé pendant le mode secret** : le programme s'arrête et les fenêtres restent masquées. Rebranchez la clé et relancez `StealthMode.exe` (ou `StealthMode.Recovery.exe`) pour les récupérer.
- Les **règles par nom de processus** (ex. `chrome`, `Code`) fonctionnent partout. Les règles par **chemin d'exécutable** et les **icônes à mettre de côté** dépendent des dossiers de chaque ordinateur : un chemin absent sur un autre PC est simplement ignoré.
- « Démarrer avec Windows » est désactivé (il pointerait vers la clé).
- Les icônes mises de côté sont rangées sur le disque de l'ordinateur, jamais sur la clé. Si vous débranchez la clé en mode secret, elles restent sur cet ordinateur et sont remises quand vous y relancez Stealth Mode depuis la clé.
- Une clé en lecture seule ou verrouillée empêche d'enregistrer les réglages et le journal de secours.
- Certains ordinateurs (professionnels, scolaires) interdisent les programmes lancés depuis une clé, ou affichent un avertissement SmartScreen (programme non signé).
