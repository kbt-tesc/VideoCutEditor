using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class AudioEncodingServiceTests
{
    [Theory]
    [InlineData("aac", 192_000L, 192)]
    [InlineData("opus", 160_000L, 160)]
    public void GetSuggestedBitrateKbps_uses_source_bitrate_for_aac_or_opus(
        string codecName,
        long sourceBitrate,
        int expectedBitrate)
    {
        MediaInfo mediaInfo = CreateMediaInfo(codecName, sourceBitrate);

        int bitrate = AudioEncodingService.GetSuggestedBitrateKbps(mediaInfo);

        Assert.Equal(expectedBitrate, bitrate);
    }

    [Theory]
    [InlineData("mp3", 256_000L)]
    [InlineData("aac", null)]
    public void GetSuggestedBitrateKbps_uses_default_when_source_is_not_aac_or_opus(
        string codecName,
        long? sourceBitrate)
    {
        MediaInfo mediaInfo = CreateMediaInfo(codecName, sourceBitrate);

        int bitrate = AudioEncodingService.GetSuggestedBitrateKbps(mediaInfo);

        Assert.Equal(128, bitrate);
    }

    [Fact]
    public void RequiresReencode_returns_true_for_incompatible_target_audio()
    {
        MediaInfo mediaInfo = CreateMediaInfo("aac", 128_000);

        bool requiresReencode = AudioEncodingService.RequiresReencode(
            new AppSettings(),
            mediaInfo,
            OutputContainer.WebM);

        Assert.True(requiresReencode);
    }

    [Fact]
    public void RequiresReencode_returns_false_for_compatible_audio_without_processing()
    {
        MediaInfo mediaInfo = CreateMediaInfo("opus", 128_000);

        bool requiresReencode = AudioEncodingService.RequiresReencode(
            new AppSettings(),
            mediaInfo,
            OutputContainer.WebM);

        Assert.False(requiresReencode);
    }

    [Fact]
    public void RequiresReencode_conservatively_encodes_unknown_audio_for_webm()
    {
        bool requiresReencode = AudioEncodingService.RequiresReencode(
            new AppSettings(),
            mediaInfo: null,
            OutputContainer.WebM);

        Assert.True(requiresReencode);
    }

    [Fact]
    public void CreateArguments_uses_aac_bitrate_for_mp4_regardless_of_saved_rate_mode()
    {
        var settings = new AppSettings
        {
            AudioBitrateKbps = 192,
            AudioRateMode = AudioRateMode.Vbr,
        };

        IReadOnlyList<string> arguments = AudioEncodingService.CreateArguments(settings, OutputContainer.Mp4);

        Assert.Equal(["-c:a", "aac", "-b:a", "192k"], arguments);
    }

    private static MediaInfo CreateMediaInfo(string codecName, long? bitrate) =>
        new(
            @"C:\video\source.mp4",
            TimeSpan.FromSeconds(10),
            "mov,mp4",
            null,
            [
                new MediaStreamInfo(0, "video", "vp9", null),
                new MediaStreamInfo(1, "audio", codecName, bitrate),
            ]);
}
