using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class ClipTitleServiceTests
{
    [Fact]
    public void CreateAvailableTitle_assigns_one_based_placeholder_for_blank_input()
    {
        string directory = CreateTempDirectory();
        var service = new ClipTitleService();

        string title = service.CreateAvailableTitle("  ", directory, []);

        Assert.Equal("クリップ_1", title);
    }

    [Fact]
    public void CreateAvailableTitle_avoids_registered_titles_and_existing_mp4_files()
    {
        string directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "クリップ_2.mp4"), string.Empty);
        var service = new ClipTitleService();

        string title = service.CreateAvailableTitle(null, directory, ["クリップ_1"]);

        Assert.Equal("クリップ_3", title);
    }

    [Fact]
    public void CreateAvailableTitle_trims_mp4_extension_and_suffixes_duplicate_title()
    {
        string directory = CreateTempDirectory();
        var service = new ClipTitleService();

        string title = service.CreateAvailableTitle("  ボス戦.mp4  ", directory, ["ボス戦"]);

        Assert.Equal("ボス戦_1", title);
    }

    [Fact]
    public void CreateAvailableTitle_replaces_invalid_filename_characters()
    {
        string directory = CreateTempDirectory();
        var service = new ClipTitleService();

        string title = service.CreateAvailableTitle("チャプター:1", directory, []);

        Assert.Equal("チャプター_1", title);
    }

    [Fact]
    public void ExportClip_uses_title_as_mp4_filename_and_exposes_fixed_time_text()
    {
        var clip = new ExportClip(
            new ClipRange(TimeSpan.FromSeconds(62.5), TimeSpan.FromSeconds(125.25)),
            "見どころ");

        Assert.Equal("見どころ.mp4", clip.OutputFileName);
        Assert.Equal("00:01:02.500", clip.StartText);
        Assert.Equal("00:02:05.250", clip.EndText);
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
