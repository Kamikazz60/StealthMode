namespace StealthMode.Core.Configuration;

/// <summary>Journal technique minimal (un fichier par jour dans logs/). Ne lève jamais d'exception.</summary>
public static class Logger
{
    static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message} : {ex}");

    static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                var file = Path.Combine(AppPaths.LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch { /* le journal ne doit jamais faire échouer l'application */ }
    }
}
