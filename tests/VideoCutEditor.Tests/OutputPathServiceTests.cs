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
    public void CreateAvailableCutPath_appends_number_when_candidate_exists()
    {
        string directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "sample_cut.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(directory, "sample_cut_2.mp4"), string.Empty);
        var service = new OutputPathService();

        string outputPath = service.CreateAvailableCutPath(@"C:\Input\sample.mp4", directory);

        Assert.Equal(Path.Combine(directory, "sample_cut_3.mp4"), outputPath);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
