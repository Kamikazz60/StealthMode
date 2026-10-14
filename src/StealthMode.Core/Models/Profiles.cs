using StealthMode.Core.Configuration;

namespace StealthMode.Core.Models;

/// <summary>
/// Règle permanente de masquage. Tous les critères renseignés doivent correspondre (ET).
/// Aucun critère basé sur le PID ou le HWND : ils changent entre les sessions.
/// </summary>
public sealed class HideRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    /// <summary>Nom de processus sans extension (ex. « Code »).</summary>
    public string? ProcessName { get; set; }
    /// <summary>Chemin complet de l'exécutable.</summary>
    public string? ExecutablePath { get; set; }
    /// <summary>Texte contenu dans le titre de la fenêtre (insensible à la casse).</summary>
    public string? TitleContains { get; set; }

    public bool HasCriteria =>
        !string.IsNullOrWhiteSpace(ProcessName) || !string.IsNullOrWhiteSpace(ExecutablePath) || !string.IsNullOrWhiteSpace(TitleContains);

    /// <summary>Texte descriptif pour l'affichage (non sérialisé).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Summary => Describe();

    public string Describe()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ProcessName)) parts.Add($"processus « {ProcessName} »");
        if (!string.IsNullOrWhiteSpace(ExecutablePath)) parts.Add($"exécutable {ExecutablePath}");
        if (!string.IsNullOrWhiteSpace(TitleContains)) parts.Add($"titre contenant « {TitleContains} »");
        return parts.Count == 0 ? "(aucun critère)" : string.Join(" et ", parts);
    }
}

public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Personnel";
    public List<HideRule> Rules { get; set; } = new();
    /// <summary>Fichiers et dossiers (icônes du Bureau ou d'un dossier) mis de côté pendant le mode secret.</summary>
    public List<string> HiddenItems { get; set; } = new();

    /// <summary>Nom affiché par les listes déroulantes (la case de sélection affiche ToString()).</summary>
    public override string ToString() => Name;
}

/// <summary>Contenu de profiles.json.</summary>
public sealed class ProfilesData
{
    public Guid ActiveProfileId { get; set; }
    public List<Profile> Profiles { get; set; } = new();

    /// <summary>Garantit au moins un profil et un profil actif valide.</summary>
    public ProfilesData Normalize()
    {
        Profiles ??= new();
        foreach (var p in Profiles) { p.Rules ??= new(); p.HiddenItems ??= new(); }
        if (Profiles.Count == 0) Profiles.Add(new Profile { Name = "Personnel" });
        if (!Profiles.Any(p => p.Id == ActiveProfileId)) ActiveProfileId = Profiles[0].Id;
        return this;
    }

    public Profile Active => Profiles.First(p => p.Id == ActiveProfileId);

    public static ProfilesData Load() => JsonFile.LoadOrDefault<ProfilesData>(AppPaths.ProfilesFile).Normalize();
    public void Save() => JsonFile.SaveAtomic(AppPaths.ProfilesFile, this);
}
