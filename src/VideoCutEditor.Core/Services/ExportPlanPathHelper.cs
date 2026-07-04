namespace VideoCutEditor.Core.Services;

internal static class ExportPlanPathHelper
{
    public static string GetExistingOutputDirectory(string outputPath)
    {
        string outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("Output path must include a directory.", nameof(outputPath));

        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Output directory does not exist: {outputDirectory}");
        }

        return outputDirectory;
    }

    public static string CreateTemporaryOutputPath(string outputPath)
    {
        string outputDirectory = GetExistingOutputDirectory(outputPath);
        string extension = Path.GetExtension(outputPath);
        return Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(outputPath)}.partial-{Guid.NewGuid():N}{extension}");
    }
}
