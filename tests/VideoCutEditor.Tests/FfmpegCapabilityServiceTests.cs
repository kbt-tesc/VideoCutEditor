using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FfmpegCapabilityServiceTests
{
    [Fact]
    public void ParseEncoders_extracts_encoder_names_from_ffmpeg_output()
    {
        const string output = """
            Encoders:
             V....D libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10
             V....D h264_nvenc           NVIDIA NVENC H.264 encoder
             V....D hevc_nvenc           NVIDIA NVENC hevc encoder
             A..... aac                  AAC (Advanced Audio Coding)
            """;

        FfmpegCapabilities capabilities = FfmpegCapabilityService.ParseEncoders(output);

        Assert.True(capabilities.SupportsEncoder("libx264"));
        Assert.True(capabilities.SupportsEncoder("h264_nvenc"));
        Assert.True(capabilities.SupportsEncoder("hevc_nvenc"));
        Assert.True(capabilities.SupportsEncoder("aac"));
        Assert.False(capabilities.SupportsEncoder("missing_encoder"));
    }

    [Fact]
    public void SupportsNvenc_reports_support_by_codec_family()
    {
        var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "h264_nvenc",
            "hevc_nvenc",
        });

        Assert.True(capabilities.SupportsNvenc(CodecFamily.H264));
        Assert.True(capabilities.SupportsNvenc(CodecFamily.H265));
        Assert.False(capabilities.SupportsNvenc(CodecFamily.Av1));
    }

    [Fact]
    public void ChooseVideoEncoder_prefers_nvenc_for_auto_when_available()
    {
        var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "libx264",
            "h264_nvenc",
        });

        Assert.Equal("h264_nvenc", capabilities.ChooseVideoEncoder(CodecFamily.H264, EncoderKind.Auto));
        Assert.Equal("h264_nvenc", capabilities.ChooseVideoEncoder(CodecFamily.H264, EncoderKind.Nvenc));
        Assert.Equal("libx264", capabilities.ChooseVideoEncoder(CodecFamily.H264, EncoderKind.Software));
    }

    [Fact]
    public void ChooseVideoEncoder_falls_back_to_software_for_auto_when_nvenc_is_missing()
    {
        var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "libx265",
        });

        Assert.Equal("libx265", capabilities.ChooseVideoEncoder(CodecFamily.H265, EncoderKind.Auto));
        Assert.Null(capabilities.ChooseVideoEncoder(CodecFamily.H265, EncoderKind.Nvenc));
    }
}
