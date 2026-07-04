using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class WaveformCachePathServiceTests
{
    [Fact]
    public void CreateCachePath_returns_stable_png_path_for_source()
    {
        string first = WaveformCachePathService.CreateCachePath(@"C:\video\source.mp4");
        string second = WaveformCachePathService.CreateCachePath(@"C:\video\source.mp4");

        Assert.Equal(first, second);
        Assert.EndsWith(".png", first);
        Assert.Contains(Path.Combine("VideoCutEditor", "waveforms"), first);
    }

    [Fact]
    public void CreateCachePath_uses_different_paths_for_different_sources()
    {
        string first = WaveformCachePathService.CreateCachePath(@"C:\video\source-a.mp4");
        string second = WaveformCachePathService.CreateCachePath(@"C:\video\source-b.mp4");

        Assert.NotEqual(first, second);
    }
}
