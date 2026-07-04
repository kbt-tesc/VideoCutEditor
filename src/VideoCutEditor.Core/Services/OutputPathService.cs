namespace VideoCutEditor.Core.Services;

public sealed class OutputPathService
{
    public string CreateAvailableCutPath(string sourcePath, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = Path.GetExtension(sourcePath);
        string firstCandidate = Path.Combine(outputDirectory, $"{sourceName}_cut{extension}");

        if (!File.Exists(firstCandidate))
        {
            return firstCandidate;
        }

        for (int index = 2; ; index++)
        {
            string candidate = Path.Combine(outputDirectory, $"{sourceName}_cut_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
