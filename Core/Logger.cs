namespace GitHubStats;

/// <summary>Writes a line to the log file in the data folder (and to the console when there is one).</summary>
public static class Logger
{
    private static string? _file;
    private static readonly object Gate = new();

    /// <summary>Raised for every log line (the UI can show the latest ones).</summary>
    public static event Action<string>? Line;

    public static void SetFolder(string dataDir) => _file = Path.Combine(dataDir, "log.txt");

    public static void Write(string message)
    {
        string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message;
        try { Console.WriteLine(line); } catch { /* no console */ }
        try { Line?.Invoke(line); } catch { /* ignore */ }
        string? f = _file;
        if (f == null) return;
        lock (Gate)
        {
            try
            {
                if (File.Exists(f) && new FileInfo(f).Length > 1_000_000) File.Move(f, f + ".old", true);
                File.AppendAllText(f, line + Environment.NewLine);
            }
            catch { /* ignore */ }
        }
    }
}
