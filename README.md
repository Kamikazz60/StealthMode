# Stealth Mode

<img src="src/StealthMode.App/Resources/StealthMode.ico" width="48" align="right">

Masque instantanément des fenêtres Windows choisies, sans fermer leurs processus, pour laisser l'ordinateur utilisable par quelqu'un d'autre. Outil de confidentialité visuelle : il ne cache ni les processus ni les fichiers.

## Lancer
Windows 10/11 x64. **Aucune installation, aucun runtime requis** :
1. Générer ou récupérer le dossier `dist\StealthMode-autonome` (voir plus bas).
2. Double-cliquer sur `StealthMode.exe`.

1. Cochez les fenêtres à masquer.
2. **Ctrl+Alt+F11** (ou le bouton) active le mode secret ; **le même raccourci** restaure. Les raccourcis se changent dans les Paramètres.
3. Sur Surface/portable, utilisez **Fn** avec les touches F, ou choisissez une autre combinaison.

Le son des applications masquées est coupé par défaut (option). Règles automatiques par processus/titre et profils : voir la documentation.

## Secours
Si l'application s'arrête alors que des fenêtres sont masquées (par exemple arrêt forcé), elles **restent masquées** : relancer `StealthMode.exe` ou `StealthMode.Recovery.exe`. Aucune garantie après coupure électrique ou plantage du système.

## Clé USB
Dossier `dist\StealthMode-portable` : à copier sur une clé. Les profils et réglages (`StealthModeData`) restent avec la clé. Voir la documentation utilisateur, section « Mode portable ».

## Documentation
- [Documentation utilisateur](docs/DOCUMENTATION_UTILISATEUR.md)
- [Documentation technique](docs/DOCUMENTATION_TECHNIQUE.md)
- [État du projet](PROJECT_STATE.md) (reprise du développement)

## Développer
```
dotnet build -c Release
dotnet test -c Release
powershell -ExecutionPolicy Bypass -File .\publish.ps1     # -> dist\StealthMode-autonome
```

## Licence
Code sous licence [MIT](LICENSE). L'icône de l'application (`src/StealthMode.App/Resources/StealthMode.ico`) n'est **pas** couverte par cette licence : elle est tirée d'une image tierce dont les droits ne sont pas garantis. Remplacez-la par la vôtre si vous réutilisez ce projet.

## Téléchargement
Les exécutables autonomes (aucun runtime requis) sont dans les [Releases](../../releases) : `StealthMode-autonome.zip` (installation normale) et `StealthMode-portable.zip` (clé USB). Programme non signé numériquement : Windows SmartScreen peut afficher un avertissement.
