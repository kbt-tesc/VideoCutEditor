using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class FfmpegToolPathService : IFfmpegToolPathService
{
    private readonly string? pathValue;

    public FfmpegToolPathService(string? pathValue = null)
    {
        this.pathValue = pathValue;
    }

    public FfmpegToolPaths Resolve(AppSettings settings)
    {
        return new FfmpegToolPaths(
            ResolveExecutable(settings.FfmpegPath, "ffmpeg"),
            ResolveExecutable(settings.FfprobePath, "ffprobe"));
    }

    public string? ResolveExecutable(string? configuredPath, string executableName)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        return FindOnPath(executableName);
    }

    public string? FindOnPath(string executableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);

        string? path = pathValue ?? Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (string candidateName in GetCandidateNames(executableName))
            {
                string candidatePath = Path.Combine(directory, candidateName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetCandidateNames(string executableName)
    {
        if (Path.HasExtension(executableName))
        {
            return [executableName];
        }

        if (OperatingSystem.IsWindows())
        {
            return [executableName, $"{executableName}.exe", $"{executableName}.cmd", $"{executableName}.bat"];
        }

        return [executableName];
    }
}
