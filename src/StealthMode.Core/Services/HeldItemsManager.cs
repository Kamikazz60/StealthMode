using StealthMode.Core.Configuration;
using StealthMode.Core.Models;

namespace StealthMode.Core.Services;

/// <summary>
/// Fait disparaître des icônes (fichiers/dossiers du Bureau ou d'un dossier) en les mettant de côté dans un dossier
/// de Stealth Mode, puis les remet à leur place. Rien n'est copié ni supprimé : c'est un simple déplacement
/// (renommage) sur le même volume. Chaque déplacement est consigné AVANT d'être fait, pour la récupération.
/// </summary>
public static class HeldItemsManager
{
    /// <summary>Refus de sécurité : on ne déplace jamais une racine de disque, un dossier système ou le dossier de l'application.</summary>
    public static string? Validate(string path, string holdRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return "chemin invalide";
        var full = Path.GetFullPath(path).TrimEnd('\\');
        if (full.Length <= 3) return "racine de disque non autorisée";
        if (Path.GetPathRoot(full)!.TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase)) return "racine de disque non autorisée";

        string[] forbidden =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };
        foreach (var f in forbidden)
            if (f.Length > 0 && f.TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase)) return "dossier système ou dossier du Bureau lui-même non autorisé";

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (full.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase)) return "dossier système non autorisé";
        if (full.StartsWith(holdRoot.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return "dossier interne de Stealth Mode";

        // Le programme en cours d'exécution ne doit pas être déplacé (ni un dossier qui le contient).
        var self = Environment.ProcessPath;
        if (self is not null && (self.Equals(full, StringComparison.OrdinalIgnoreCase)
            || self.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase))) return "contient Stealth Mode en cours d'exécution";
        return null;
    }

    /// <summary>
    /// Met les éléments de côté. <paramref name="persist"/> est appelé après avoir ajouté chaque enregistrement à
    /// <paramref name="held"/> et AVANT de déplacer, pour que le journal soit toujours en avance sur le disque.
    /// </summary>
    public static List<string> Hold(IEnumerable<string> paths, string holdRoot, List<HeldItemRecord> held, Action persist)
    {
        var failures = new List<string>();
        int n = held.Count;
        foreach (var raw in paths)
        {
            var name = Path.GetFileName(raw.TrimEnd('\\'));
            var why = Validate(raw, holdRoot);
            if (why is not null) { failures.Add($"{name} : {why}"); continue; }

            var full = Path.GetFullPath(raw).TrimEnd('\\');
            bool isDir = Directory.Exists(full), isFile = File.Exists(full);
            if (!isDir && !isFile) { failures.Add($"{name} : introuvable"); continue; }

            // Même volume obligatoire : un déplacement entre volumes serait une copie (lente, risquée).
            if (!string.Equals(Path.GetPathRoot(full), Path.GetPathRoot(Path.GetFullPath(holdRoot)), StringComparison.OrdinalIgnoreCase))
            { failures.Add($"{name} : sur un autre disque que le dossier de Stealth Mode, non géré"); continue; }

            var held1 = Path.Combine(holdRoot, $"{++n}_{name}");
            var rec = new HeldItemRecord { OriginalPath = full, HeldPath = held1, IsDirectory = isDir };
            held.Add(rec);
            persist(); // journal d'abord
            try
            {
                Directory.CreateDirectory(holdRoot);
                if (isDir) Directory.Move(full, held1); else File.Move(full, held1);
            }
            catch (Exception ex)
            {
                held.Remove(rec); persist();
                Logger.Error($"Mise de côté de {full}", ex);
                failures.Add($"{name} : {(ex is IOException ? "utilisé par un programme ou protégé" : ex.Message)}");
            }
        }
        return failures;
    }

    /// <summary>Remet les éléments à leur place. Rien n'est jamais écrasé : en cas de collision, l'élément est remis sous un nom « (restauré) ».</summary>
    public static (int Restored, int Gone, List<string> Failures, List<HeldItemRecord> Kept) Release(IEnumerable<HeldItemRecord> records)
    {
        int restored = 0, gone = 0;
        var failures = new List<string>();
        var kept = new List<HeldItemRecord>();
        string? holdRoot = null;
        foreach (var r in records)
        {
            // Journal venu d'un autre ordinateur (clé USB en mode portable) : ces éléments sont sur l'autre machine.
            // On les conserve au journal sans y toucher ; ils seront remis quand la clé reviendra sur leur ordinateur.
            if (!string.IsNullOrEmpty(r.Machine) && !r.Machine.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                kept.Add(r);
                failures.Add($"{Path.GetFileName(r.OriginalPath)} : mis de côté sur l'ordinateur « {r.Machine} », il sera remis quand vous y relancerez Stealth Mode");
                continue;
            }
            holdRoot ??= Path.GetDirectoryName(r.HeldPath);
            var name = Path.GetFileName(r.OriginalPath);
            bool heldExists = r.IsDirectory ? Directory.Exists(r.HeldPath) : File.Exists(r.HeldPath);
            if (!heldExists)
            {
                // Déjà remis (ou supprimé par l'utilisateur) : s'il est à sa place, tout va bien.
                if (File.Exists(r.OriginalPath) || Directory.Exists(r.OriginalPath)) restored++; else gone++;
                continue;
            }
            try
            {
                var target = r.OriginalPath;
                if (File.Exists(target) || Directory.Exists(target))
                {
                    target = UniqueName(target);
                    failures.Add($"{name} : un élément du même nom existe déjà, remis sous « {Path.GetFileName(target)} »");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); // le dossier d'origine a pu être supprimé
                if (r.IsDirectory) Directory.Move(r.HeldPath, target); else File.Move(r.HeldPath, target);
                restored++;
            }
            catch (Exception ex)
            {
                Logger.Error($"Remise en place de {r.OriginalPath}", ex);
                failures.Add($"{name} : {ex.Message} (conservé dans {r.HeldPath})");
                kept.Add(r); // non remis : reste au journal pour une nouvelle tentative
            }
        }
        // Nettoie le dossier de mise de côté seulement s'il est vide (jamais de suppression de contenu).
        try { if (holdRoot is not null && Directory.Exists(holdRoot) && !Directory.EnumerateFileSystemEntries(holdRoot).Any()) Directory.Delete(holdRoot); }
        catch { }
        return (restored, gone, failures, kept);
    }

    static string UniqueName(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, i == 1 ? $"{stem} (restauré){ext}" : $"{stem} (restauré {i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}
