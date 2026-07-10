using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FfmpegCapabilityServiceIntegrationTests
{
    [SkippableFact]
    public async Task DetectAsync_reads_installed_ffmpeg_encoders_when_ffmpeg_is_available()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");

        FfmpegCapabilities capabilities = await new FfmpegCapabilityService().DetectAsync(paths.FfmpegPath);

        Assert.NotEmpty(capabilities.Encoders);
        Assert.True(
            capabilities.SupportsEncoder("libx264")
            || capabilities.SupportsEncoder("h264_nvenc")
            || capabilities.SupportsEncoder("mpeg4"));
    }
}
