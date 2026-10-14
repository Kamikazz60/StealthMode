using StealthMode.Core.Models;
using StealthMode.Core.Services;

namespace StealthMode.Tests;

/// <summary>Tests réels sur le système de fichiers (dossiers temporaires dédiés, même volume).</summary>
public class HeldItemsTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "stealth-held-" + Guid.NewGuid().ToString("N"));
    string Desk => Path.Combine(_root, "desk");
    string Journal => Path.Combine(_root, "state", "hidden-state.json");

    public HeldItemsTests() => Directory.CreateDirectory(Desk);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    string MakeFile(string name, string content = "contenu")
    {
        var p = Path.Combine(Desk, name); File.WriteAllText(p, content); return p;
    }

    [Fact]
    public void SecretModeWithOnlyItems_HidesFilesAndFolders_ThenRestoresThemIntact()
    {
        var file = MakeFile("raccourci.lnk", "abc");
        var dir = Path.Combine(Desk, "Dossier perso"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "note.txt"), "secret");

        using var svc = new WindowHidingService(Journal);
        var r = svc.Hide(Array.Empty<WindowInfo>(), new[] { file, dir });

        Assert.True(svc.IsSecretActive);                       // actif même sans aucune fenêtre
        Assert.Equal(2, r.HeldItems);
        Assert.False(File.Exists(file)); Assert.False(Directory.Exists(dir));   // ils ont quitté le Bureau
        Assert.True(File.Exists(Journal));                     // journal présent

        var back = svc.Restore();
        Assert.Equal(2, back.ItemsRestored);
        Assert.Empty(back.Failures);
        Assert.Equal("abc", File.ReadAllText(file));
        Assert.Equal("secret", File.ReadAllText(Path.Combine(dir, "note.txt")));
        Assert.False(File.Exists(Journal));
        Assert.False(Directory.Exists(Path.Combine(_root, "state", "hold")));    // dossier de mise de côté nettoyé
        Assert.False(svc.IsSecretActive);
    }

    [Fact]
    public void NameCollisionDuringSecretMode_NeverOverwrites()
    {
        var file = MakeFile("a.txt", "original");
        using var svc = new WindowHidingService(Journal);
        svc.Hide(Array.Empty<WindowInfo>(), new[] { file });

        File.WriteAllText(file, "nouveau fichier créé entre-temps");   // même nom pendant le mode secret
        var back = svc.Restore();

        Assert.Equal("nouveau fichier créé entre-temps", File.ReadAllText(file));       // intact
        var restored = Path.Combine(Desk, "a (restauré).txt");
        Assert.Equal("original", File.ReadAllText(restored));                           // l'original n'est pas perdu
        Assert.Single(back.Failures);
    }

    [Fact]
    public void LockedFile_IsReportedAndSkipped_OthersStillHidden_NoJournalEntryLeft()
    {
        var ok = MakeFile("ok.txt");
        var locked = MakeFile("verrouille.txt");
        using var keep = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        using var svc = new WindowHidingService(Journal);
        var r = svc.Hide(Array.Empty<WindowInfo>(), new[] { locked, ok });

        Assert.Equal(1, r.HeldItems);
        Assert.Single(r.Failures);
        Assert.True(File.Exists(locked));          // resté en place
        Assert.False(File.Exists(ok));
        var state = StealthMode.Core.Configuration.JsonFile.LoadOrDefault<HiddenState>(Journal);
        Assert.Single(state.HeldItems);            // seule la mise de côté réussie est consignée
        svc.Restore();
        Assert.True(File.Exists(ok));
    }

    [Fact]
    public void CrashRecovery_ReplaysJournal_AndPutsItemsBack()
    {
        var file = MakeFile("x.txt", "123");
        var crashed = new WindowHidingService(Journal);
        crashed.Hide(Array.Empty<WindowInfo>(), new[] { file });
        crashed.Dispose();                         // « crash » : plus de restauration par ce service
        Assert.False(File.Exists(file));

        var rec = WindowRestorer.RestoreFromJournal(Journal);
        Assert.NotNull(rec);
        Assert.Equal(1, rec!.ItemsRestored);
        Assert.Equal("123", File.ReadAllText(file));
        Assert.False(File.Exists(Journal));
    }

    [Fact]
    public void Validate_RefusesDangerousPaths()
    {
        var hold = Path.Combine(_root, "hold");
        Assert.NotNull(HeldItemsManager.Validate(@"C:\", hold));
        Assert.NotNull(HeldItemsManager.Validate(Environment.GetFolderPath(Environment.SpecialFolder.Windows), hold));
        Assert.NotNull(HeldItemsManager.Validate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"), hold));
        Assert.NotNull(HeldItemsManager.Validate(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), hold));
        Assert.NotNull(HeldItemsManager.Validate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), hold));
        Assert.NotNull(HeldItemsManager.Validate(Path.Combine(hold, "1_x"), hold));
        Assert.NotNull(HeldItemsManager.Validate("relatif.txt", hold));
        Assert.NotNull(HeldItemsManager.Validate(Path.GetDirectoryName(Environment.ProcessPath)!, hold)); // dossier contenant le programme en cours
        Assert.Null(HeldItemsManager.Validate(MakeFile("ok.txt"), hold));
    }

    [Fact]
    public void MissingItem_IsReportedNotFound()
    {
        using var svc = new WindowHidingService(Journal);
        var r = svc.Hide(Array.Empty<WindowInfo>(), new[] { Path.Combine(Desk, "inexistant.txt") });
        Assert.Equal(0, r.HeldItems);
        Assert.Single(r.Failures);
        Assert.False(svc.IsSecretActive);
    }
}
