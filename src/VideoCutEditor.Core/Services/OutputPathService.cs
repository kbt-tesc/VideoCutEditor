using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class OutputPathService
{
    public string CreateAvailableCutPath(string sourcePath, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return CreateAvailableCutPath(sourcePath, outputDirectory, Path.GetExtension(sourcePath));
    }

    public string CreateAvailableCutPath(
        string sourcePath,
        string outputDirectory,
        OutputContainer outputContainer) =>
        CreateAvailableCutPath(sourcePath, outputDirectory, outputContainer.GetFileExtension());

    private static string CreateAvailableCutPath(
        string sourcePath,
        string outputDirectory,
        string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        string firstCandidate = Path.Combine(outputDirectory, $"{sourceName}_cut{extension}");

        if (!File.Exists(firstCandidate))
        {
            return firstCandidate;
        }

        for (int index = 1; ; index++)
        {
            string candidate = Path.Combine(outputDirectory, $"{sourceName}_cut_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
