namespace VideoCutEditor.Core.Models;

public enum OutputContainer
{
    Mp4,
    WebM,
}

public static class OutputContainerExtensions
{
    public static string GetFileExtension(this OutputContainer container) =>
        container switch
        {
            OutputContainer.WebM => ".webm",
            _ => ".mp4",
        };

    public static string GetDisplayName(this OutputContainer container) =>
        container switch
        {
            OutputContainer.WebM => "WebM",
            _ => "MP4",
        };

    public static bool TryFromPath(string? path, out OutputContainer container)
    {
        string? extension = Path.GetExtension(path);
        if (string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase))
        {
            container = OutputContainer.WebM;
            return true;
        }

        if (string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            container = OutputContainer.Mp4;
            return true;
        }

        container = default;
        return false;
    }
}
