using System.Runtime.InteropServices;
using StealthMode.Core.Services;

// Utilitaire de récupération indépendant : réaffiche les fenêtres encore existantes consignées
// dans %LOCALAPPDATA%\StealthMode\hidden-state.json. Ne dépend pas de l'interface principale.
var result = WindowRestorer.RestoreFromJournal();
string message = result is null
    ? "Aucune fenêtre masquée n'est enregistrée : rien à restaurer."
    : $"{result.Restored} fenêtre(s) restaurée(s), {result.Gone} introuvable(s) (déjà fermées)"
      + (result.Failures.Count > 0 ? $", {result.Failures.Count} échec(s)." : ".");

if (!args.Contains("--silent")) MessageBoxW(IntPtr.Zero, message, "Stealth Mode - Récupération", 0x40);
return 0;

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
