using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class BitrateSuggestionServiceTests
{
    private readonly BitrateSuggestionService service = new();

    [Fact]
    public void SuggestVideoBitrateKbps_uses_video_stream_bitrate_for_h264()
    {
        MediaInfo mediaInfo = CreateMediaInfo(
            mediaBitrate: 4_700_000,
            videoBitrate: 4_500_000,
            width: 1920,
            height: 1080);

        int suggestion = service.SuggestVideoBitrateKbps(mediaInfo, CodecFamily.H264);

        Assert.Equal(4500, suggestion);
    }

    [Theory]
    [InlineData(CodecFamily.H265, 3150)]
    [InlineData(CodecFamily.Av1, 2475)]
    public void SuggestVideoBitrateKbps_applies_codec_multipliers(CodecFamily codecFamily, int expectedKbps)
    {
        MediaInfo mediaInfo = CreateMediaInfo(
            mediaBitrate: null,
            videoBitrate: 4_500_000,
            width: 1920,
            height: 1080);

        int suggestion = service.SuggestVideoBitrateKbps(mediaInfo, codecFamily);

        Assert.Equal(expectedKbps, suggestion);
    }

    [Fact]
    public void SuggestVideoBitrateKbps_falls_back_to_container_bitrate()
    {
        MediaInfo mediaInfo = CreateMediaInfo(
            mediaBitrate: 3_200_000,
            videoBitrate: null,
            width: 1280,
            height: 720);

        int suggestion = service.SuggestVideoBitrateKbps(mediaInfo, CodecFamily.H264);

        Assert.Equal(3200, suggestion);
    }

    [Theory]
    [InlineData(3840, 2160, CodecFamily.H264, 12000)]
    [InlineData(2560, 1440, CodecFamily.H264, 8000)]
    [InlineData(1920, 1080, CodecFamily.H264, 5000)]
    [InlineData(1280, 720, CodecFamily.H264, 2500)]
    [InlineData(854, 480, CodecFamily.H264, 1200)]
    [InlineData(1920, 1080, CodecFamily.H265, 3500)]
    [InlineData(1920, 1080, CodecFamily.Av1, 2750)]
    public void SuggestVideoBitrateKbps_falls_back_to_resolution_defaults(
        int width,
        int height,
        CodecFamily codecFamily,
        int expectedKbps)
    {
        MediaInfo mediaInfo = CreateMediaInfo(
            mediaBitrate: null,
            videoBitrate: null,
            width: width,
            height: height);

        int suggestion = service.SuggestVideoBitrateKbps(mediaInfo, codecFamily);

        Assert.Equal(expectedKbps, suggestion);
    }

    [Theory]
    [InlineData(CodecFamily.H264, 2500)]
    [InlineData(CodecFamily.H265, 1750)]
    [InlineData(CodecFamily.Av1, 1375)]
    public void SuggestVideoBitrateKbps_uses_720p_default_when_metadata_is_sparse(
        CodecFamily codecFamily,
        int expectedKbps)
    {
        MediaInfo mediaInfo = new(
            SourcePath: "input.mp4",
            Duration: TimeSpan.FromMinutes(1),
            ContainerFormat: "mov,mp4",
            Bitrate: null,
            Streams: []);

        int suggestion = service.SuggestVideoBitrateKbps(mediaInfo, codecFamily);

        Assert.Equal(expectedKbps, suggestion);
    }

    private static MediaInfo CreateMediaInfo(
        long? mediaBitrate,
        long? videoBitrate,
        int? width,
        int? height)
    {
        return new MediaInfo(
            SourcePath: "input.mp4",
            Duration: TimeSpan.FromMinutes(1),
            ContainerFormat: "mov,mp4",
            Bitrate: mediaBitrate,
            Streams:
            [
                new MediaStreamInfo(
                    Index: 0,
                    CodecType: "video",
                    CodecName: "h264",
                    Bitrate: videoBitrate,
                    Width: width,
                    Height: height,
                    FrameRate: 29.97),
            ]);
    }
}
