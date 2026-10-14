using System.Text.Json;

namespace StealthMode.Core.Configuration;

/// <summary>Lecture/écriture JSON. L'écriture est atomique (fichier temporaire puis remplacement), avec repli pour les disques FAT/exFAT.</summary>
public static class JsonFile
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static T LoadOrDefault<T>(string path) where T : new()
    {
        try
        {
            if (!File.Exists(path)) return new T();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch (Exception ex)
        {
            Logger.Error($"Lecture de {path} impossible, valeurs par défaut utilisées", ex);
            return new T();
        }
    }

    public static void SaveAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        if (!File.Exists(path)) { File.Move(tmp, path); return; }

        try { File.Replace(tmp, path, null); }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Certains systèmes de fichiers (clés USB en FAT32/exFAT) refusent File.Replace : repli par copie.
            // Le fichier temporaire complet existe déjà ; la copie écrase ensuite l'ancien en une opération.
            File.Copy(tmp, path, overwrite: true);
            File.Delete(tmp);
        }
    }
}
