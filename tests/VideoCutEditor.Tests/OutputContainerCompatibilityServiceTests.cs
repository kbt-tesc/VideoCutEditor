using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class OutputContainerCompatibilityServiceTests
{
    [Theory]
    [InlineData("vp8", "vorbis")]
    [InlineData("vp9", "opus")]
    [InlineData("av1", "opus")]
    public void CanStreamCopy_accepts_webm_video_and_audio_codecs(string videoCodec, string audioCodec)
    {
        MediaInfo mediaInfo = CreateMediaInfo(videoCodec, audioCodec);

        bool compatible = OutputContainerCompatibilityService.CanStreamCopy(
            mediaInfo,
            OutputContainer.WebM);

        Assert.True(compatible);
    }

    [Theory]
    [InlineData("h264", "aac")]
    [InlineData("hevc", "aac")]
    public void CanStreamCopy_rejects_mp4_codecs_for_webm(string videoCodec, string audioCodec)
    {
        MediaInfo mediaInfo = CreateMediaInfo(videoCodec, audioCodec);

        bool compatible = OutputContainerCompatibilityService.CanStreamCopy(
            mediaInfo,
            OutputContainer.WebM);

        Assert.False(compatible);
    }

    [Fact]
    public void CanStreamCopy_accepts_common_webm_codecs_for_mp4()
    {
        MediaInfo mediaInfo = CreateMediaInfo("vp9", "opus");

        bool compatible = OutputContainerCompatibilityService.CanStreamCopy(
            mediaInfo,
            OutputContainer.Mp4);

        Assert.True(compatible);
    }

    [Fact]
    public void CanStreamCopy_rejects_unknown_or_unsupported_stream_types()
    {
        MediaInfo mediaInfo = CreateMediaInfo(
            "vp9",
            "opus",
            new MediaStreamInfo(2, "subtitle", "ass", null));

        Assert.False(OutputContainerCompatibilityService.CanStreamCopy(mediaInfo, OutputContainer.WebM));
        Assert.False(OutputContainerCompatibilityService.CanStreamCopy(mediaInfo, OutputContainer.Mp4));
    }

    private static MediaInfo CreateMediaInfo(
        string videoCodec,
        string audioCodec,
        params MediaStreamInfo[] additionalStreams) =>
        new(
            "source.webm",
            TimeSpan.FromSeconds(10),
            "matroska,webm",
            2_000_000,
            [
                new MediaStreamInfo(0, "video", videoCodec, 1_800_000),
                new MediaStreamInfo(1, "audio", audioCodec, 128_000),
                .. additionalStreams,
            ]);
}
