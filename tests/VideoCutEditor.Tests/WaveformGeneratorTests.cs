using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class WaveformGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_reports_missing_ffmpeg()
    {
        var generator = new WaveformGenerator();
        var plan = new WaveformPlan(
            @"C:\missing\ffmpeg.exe",
            @"C:\video\source.mp4",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "waveform.png"),
            ["-version"]);

        WaveformResult result = await generator.GenerateAsync(plan);

        Assert.False(result.Succeeded);
        Assert.Contains("ffmpeg was not found", result.ErrorMessage);
    }
}
