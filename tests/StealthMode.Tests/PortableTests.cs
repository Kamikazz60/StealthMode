using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using StealthMode.Core.Services;

namespace StealthMode.Tests;

/// <summary>Mode portable : dossiers temporaires simulant « le dossier de l'exécutable » (clé USB) et le profil Windows.</summary>
public class PortableTests : IDisposable
{
    readonly string _base = Path.Combine(Path.GetTempPath(), "stealth-portable-" + Guid.NewGuid().ToString("N"));
    string Usb => Path.Combine(_base, "usb");
    string Local => Path.Combine(_base, "local");

    public PortableTests() { Directory.CreateDirectory(Usb); Directory.CreateDirectory(Local); }
    public void Dispose() { try { Directory.Delete(_base, true); } catch { } }

    [Fact]
    public void WithoutMarker_DataGoesToLocalProfile_WithMarker_NextToExecutable()
    {
        Assert.Equal(AppPaths.LocalRoot, AppPaths.ComputeRoot(Usb));

        File.WriteAllText(Path.Combine(Usb, AppPaths.PortableMarkerName), "");
        Assert.Equal(Path.Combine(Usb, AppPaths.PortableDataFolderName), AppPaths.ComputeRoot(Usb));
    }

    [Fact]
    public void EnablePortable_CopiesProfilesAndSettings_CreatesMarker_NeverOverwritesExistingPortableData()
    {
        File.WriteAllText(Path.Combine(Local, "profiles.json"), "{\"v\":\"local\"}");
        File.WriteAllText(Path.Combine(Local, "settings.json"), "{\"s\":\"local\"}");

        Assert.Null(AppPaths.EnablePortable(Usb, Local));
        var data = Path.Combine(Usb, AppPaths.PortableDataFolderName);
        Assert.True(File.Exists(Path.Combine(Usb, AppPaths.PortableMarkerName)));
        Assert.Equal("{\"v\":\"local\"}", File.ReadAllText(Path.Combine(data, "profiles.json")));
        Assert.Equal("{\"s\":\"local\"}", File.ReadAllText(Path.Combine(data, "settings.json")));

        // Données déjà présentes sur la clé (autre ordinateur) : elles priment, on ne les écrase pas.
        File.WriteAllText(Path.Combine(data, "profiles.json"), "{\"v\":\"cle\"}");
        File.WriteAllText(Path.Combine(Local, "profiles.json"), "{\"v\":\"autre\"}");
        Assert.Null(AppPaths.EnablePortable(Usb, Local));
        Assert.Equal("{\"v\":\"cle\"}", File.ReadAllText(Path.Combine(data, "profiles.json")));
    }

    [Fact]
    public void DisablePortable_CopiesDataBackToLocal_AndRemovesMarker_KeepsPortableFolder()
    {
        AppPaths.EnablePortable(Usb, Local);
        var data = Path.Combine(Usb, AppPaths.PortableDataFolderName);
        File.WriteAllText(Path.Combine(data, "profiles.json"), "{\"v\":\"derniere version\"}");

        Assert.Null(AppPaths.DisablePortable(Usb, Local));
        Assert.False(File.Exists(Path.Combine(Usb, AppPaths.PortableMarkerName)));
        Assert.Equal("{\"v\":\"derniere version\"}", File.ReadAllText(Path.Combine(Local, "profiles.json")));
        Assert.True(Directory.Exists(data));   // rien n'est supprimé sur la clé
        Assert.Equal(AppPaths.LocalRoot, AppPaths.ComputeRoot(Usb));
    }

    [Fact]
    public void EnablePortable_OnUnwritableLocation_ReturnsErrorInsteadOfThrowing()
    {
        // Un fichier à la place du dossier cible : la création échoue, l'erreur est renvoyée (clé en lecture seule, etc.).
        var blocker = Path.Combine(_base, "bloque");
        Directory.CreateDirectory(blocker);
        File.WriteAllText(Path.Combine(blocker, AppPaths.PortableDataFolderName), "je suis un fichier");
        var err = AppPaths.EnablePortable(blocker, Local);
        Assert.NotNull(err);
        Assert.False(File.Exists(Path.Combine(blocker, AppPaths.PortableMarkerName)));
    }

    [Fact]
    public void JournalFromAnotherComputer_IsKeptUntouched_AndReportedNotLost()
    {
        var journal = Path.Combine(Usb, AppPaths.PortableDataFolderName, "hidden-state.json");
        var held = Path.Combine(_base, "hold", "1_a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(held)!); File.WriteAllText(held, "a");
        JsonFile.SaveAtomic(journal, new HiddenState
        {
            HeldItems = { new HeldItemRecord { OriginalPath = Path.Combine(_base, "desk", "a.txt"), HeldPath = held, Machine = "AUTRE-PC" } }
        });

        var r = WindowRestorer.RestoreFromJournal(journal);

        Assert.NotNull(r);
        Assert.Equal(0, r!.ItemsRestored);
        Assert.Single(r.Failures);                         // signalé à l'utilisateur
        Assert.True(File.Exists(held));                    // fichier non touché
        Assert.True(File.Exists(journal));                 // l'entrée reste au journal
        var state = JsonFile.LoadOrDefault<HiddenState>(journal);
        Assert.Equal("AUTRE-PC", Assert.Single(state.HeldItems).Machine);
    }
}
