namespace StealthMode.Core.Models;

/// <summary>Instantané d'une fenêtre de premier niveau détectée.</summary>
public sealed record WindowInfo(
    IntPtr Handle,
    string Title,
    string ClassName,
    uint ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string ApplicationName,
    bool IsVisible,
    bool IsMinimized,
    bool IsMaximized)
{
    /// <summary>Clé stable entre sessions pour regrouper par application (chemin d'exécutable, sinon nom de processus).</summary>
    public string AppKey => (ExecutablePath ?? ProcessName).ToLowerInvariant();
}

/// <summary>État d'une fenêtre au moment de son masquage, pour restauration fidèle.</summary>
public sealed class HiddenWindowRecord
{
    public long Handle { get; set; }
    public uint ProcessId { get; set; }
    public string ClassName { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ExecutablePath { get; set; }
    public bool WasMinimized { get; set; }
    public bool WasMaximized { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
}

/// <summary>Session audio que Stealth Mode a coupée (à rétablir à la restauration).</summary>
public sealed class MutedSessionRecord
{
    public string InstanceId { get; set; } = "";
    /// <summary>Identifiant stable de l'application (même valeur pour ses sessions successives) ; Windows y mémorise l'état muet.</summary>
    public string SessionId { get; set; } = "";
    public uint ProcessId { get; set; }
    public string? ExecutablePath { get; set; }
}

/// <summary>Contenu de hidden-state.json : tout ce qu'il faut pour défaire le mode secret après un incident.</summary>
public sealed class HiddenState
{
    public List<HiddenWindowRecord> Windows { get; set; } = new();
    public List<MutedSessionRecord> MutedSessions { get; set; } = new();
    public List<HeldItemRecord> HeldItems { get; set; } = new();
}

/// <summary>Fichier ou dossier (icône du Bureau, d'un dossier…) mis de côté pendant le mode secret.</summary>
public sealed class HeldItemRecord
{
    public string OriginalPath { get; set; } = "";
    public string HeldPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    /// <summary>Ordinateur où l'élément a été mis de côté (le journal peut voyager sur une clé USB en mode portable).</summary>
    public string Machine { get; set; } = Environment.MachineName;
}

public enum SelectionMode { PerWindow, PerApplication }

public sealed record HideResult(int Hidden, IReadOnlyList<string> Failures, int HeldItems = 0);
public sealed record RestoreResult(int Restored, int Gone, IReadOnlyList<string> Failures, int AudioRestored = 0, int ItemsRestored = 0);
