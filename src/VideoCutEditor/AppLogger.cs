using System.Diagnostics;

namespace VideoCutEditor;

internal static class AppLogger
{
    private static readonly object SyncRoot = new();

    static AppLogger()
    {
        try
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VideoCutEditor",
                "logs");

            Directory.CreateDirectory(logDirectory);
            LogFilePath = Path.Combine(logDirectory, $"app-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
        }
        catch
        {
            LogFilePath = Path.Combine(Path.GetTempPath(), $"VideoCutEditor-app-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
        }
    }

    public static string LogFilePath { get; }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception exception) =>
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            string line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(LogFilePath, line);
            }

            Debug.WriteLine(line);
        }
        catch
        {
            // Logging must never become the reason the app fails to start.
        }
    }
}
