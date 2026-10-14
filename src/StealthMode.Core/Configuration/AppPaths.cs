namespace StealthMode.Core.Configuration;

/// <summary>
/// Emplacements des fichiers applicatifs.
/// Mode normal : %LOCALAPPDATA%\StealthMode.
/// Mode portable : si un fichier nommé « portable » est posé à côté de l'exécutable, les données sont écrites dans le
/// dossier « StealthModeData » à côté de l'exécutable (clé USB : profils et réglages suivent la clé).
/// </summary>
public static class AppPaths
{
    public const string PortableMarkerName = "portable";
    public const string PortableDataFolderName = "StealthModeData";

    static readonly string DefaultExeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>Dossier de données du mode normal (profil Windows de l'utilisateur).</summary>
    public static string LocalRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StealthMode");

    public static string ExecutableDirectory => DefaultExeDir;
    public static string PortableMarker => Path.Combine(DefaultExeDir, PortableMarkerName);
    public static string PortableRoot => Path.Combine(DefaultExeDir, PortableDataFolderName);

    /// <summary>Dossier de données en cours d'utilisation (surchargeable pour les tests).</summary>
    public static string Root { get; set; } = ComputeRoot(DefaultExeDir);

    public static bool IsPortableActive =>
        string.Equals(Root, PortableRoot, StringComparison.OrdinalIgnoreCase);

    /// <summary>Le fichier « portable » est présent à côté de l'exécutable (le mode sera actif au prochain démarrage).</summary>
    public static bool IsPortableMarkerPresent => File.Exists(PortableMarker);

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string ProfilesFile => Path.Combine(Root, "profiles.json");
    public static string HiddenStateFile => Path.Combine(Root, "hidden-state.json");
    public static string LogDirectory => Path.Combine(Root, "logs");

    /// <summary>
    /// Dossier où sont mises de côté les icônes : toujours sur le disque de l'ordinateur (jamais sur une clé USB),
    /// car un déplacement n'est possible que sur le même volume que l'élément.
    /// </summary>
    public static string HoldRoot => Path.Combine(LocalRoot, "hold");

    /// <summary>Dossier de données pour un dossier d'exécutable donné.</summary>
    public static string ComputeRoot(string exeDir) =>
        File.Exists(Path.Combine(exeDir, PortableMarkerName))
            ? Path.Combine(exeDir, PortableDataFolderName)
            : LocalRoot;

    static readonly string[] DataFiles = { "settings.json", "profiles.json" };

    /// <summary>
    /// Active le mode portable : copie les profils et réglages actuels à côté de l'exécutable puis crée le fichier « portable ».
    /// Les données déjà présentes à destination ne sont jamais écrasées. Retourne un message d'erreur, ou null si tout va bien.
    /// Le changement s'applique au prochain démarrage.
    /// </summary>
    public static string? EnablePortable(string? exeDir = null, string? fromRoot = null)
    {
        exeDir ??= DefaultExeDir;
        fromRoot ??= Root;
        try
        {
            var target = Path.Combine(exeDir, PortableDataFolderName);
            Directory.CreateDirectory(target);
            foreach (var f in DataFiles)
            {
                var src = Path.Combine(fromRoot, f);
                var dst = Path.Combine(target, f);
                if (File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
            }
            File.WriteAllText(Path.Combine(exeDir, PortableMarkerName), "Mode portable : les données sont dans le dossier StealthModeData.");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("Activation du mode portable", ex);
            return "Impossible d'écrire à côté du programme (dossier protégé ou clé en lecture seule ?) : " + ex.Message;
        }
    }

    /// <summary>Quitte le mode portable : recopie les données dans le profil de l'utilisateur (elles y remplacent les anciennes) puis supprime le fichier « portable ».</summary>
    public static string? DisablePortable(string? exeDir = null, string? toRoot = null)
    {
        exeDir ??= DefaultExeDir;
        toRoot ??= LocalRoot;
        try
        {
            var source = Path.Combine(exeDir, PortableDataFolderName);
            Directory.CreateDirectory(toRoot);
            foreach (var f in DataFiles)
            {
                var src = Path.Combine(source, f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(toRoot, f), overwrite: true);
            }
            var marker = Path.Combine(exeDir, PortableMarkerName);
            if (File.Exists(marker)) File.Delete(marker);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("Désactivation du mode portable", ex);
            return "Impossible de quitter le mode portable : " + ex.Message;
        }
    }
}
