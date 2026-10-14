using Microsoft.Win32;

namespace StealthMode.Core.Configuration;

/// <summary>Démarrage avec Windows via HKCU\...\Run (aucun droit administrateur).</summary>
public static class AutoStart
{
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "StealthMode";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Chemin de l'exécutable introuvable.");
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
