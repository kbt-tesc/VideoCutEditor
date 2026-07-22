using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class OutputPathServiceTests
{
    [Fact]
    public void CreateAvailableCutPath_uses_cut_suffix_and_source_extension()
    {
        string directory = CreateTempDirectory();
        var service = new OutputPathService();

        string outputPath = service.CreateAvailableCutPath(@"C:\Input\sample.mkv", directory);

        Assert.Equal(Path.Combine(directory, "sample_cut.mkv"), outputPath);
    }

    [Fact]
    public void CreateAvailableCutPath_appends_one_based_number_when_default_exists()
    {
        string directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "sample_cut.mp4"), string.Empty);
        var service = new OutputPathService();

        string outputPath = service.CreateAvailableCutPath(@"C:\Input\sample.mp4", directory);

        Assert.Equal(Path.Combine(directory, "sample_cut_1.mp4"), outputPath);
    }

    [Fact]
    public void CreateAvailableCutPath_continues_after_one_based_suffix_exists()
    {
        string directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "sample_cut.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "sample_cut_1.mp4"), string.Empty);
        var service = new OutputPathService();

        string outputPath = service.CreateAvailableCutPath(@"C:\Input\sample.mp4", directory);

        Assert.Equal(Path.Combine(directory, "sample_cut_2.mp4"), outputPath);
    }

    [Fact]
    public void CreateAvailableCutPath_uses_requested_output_container_extension()
    {
        string directory = CreateTempDirectory();
        var service = new OutputPathService();

        string outputPath = service.CreateAvailableCutPath(
            @"C:\Input\sample.mp4",
            directory,
            OutputContainer.WebM);

        Assert.Equal(Path.Combine(directory, "sample_cut.webm"), outputPath);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
