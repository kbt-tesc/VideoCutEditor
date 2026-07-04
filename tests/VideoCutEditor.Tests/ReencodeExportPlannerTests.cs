using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class ReencodeExportPlannerTests
{
    [Fact]
    public void CreatePlan_uses_nvenc_for_auto_when_available()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "h264_nvenc",
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Auto,
                    LastVideoBitrateKbps = 4500,
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Equal(@"C:\tools\ffmpeg.exe", plan.FfmpegPath);
            Assert.Equal(outputPath, plan.FinalOutputPath);
            Assert.Contains(".partial-", plan.TemporaryOutputPath);
            Assert.Equal(
                [
                    "-hide_banner",
                    "-nostdin",
                    "-y",
                    "-ss",
                    "00:00:05.000",
                    "-i",
                    @"C:\video\source.mp4",
                    "-t",
                    "00:00:15.000",
                    "-map",
                    "0",
                    "-c",
                    "copy",
                    "-c:v",
                    "h264_nvenc",
                    "-b:v",
                    "4500k",
                    "-map_metadata",
                    "0",
                    "-avoid_negative_ts",
                    "make_zero",
                    plan.TemporaryOutputPath,
                ],
                plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_falls_back_to_software_for_auto_when_nvenc_is_missing()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx265",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H265,
                    LastEncoderKind = EncoderKind.Auto,
                    LastVideoBitrateKbps = 3000,
                });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Contains("libx265", plan.Arguments);
            Assert.Contains("3000k", plan.Arguments);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatePlan_rejects_explicit_nvenc_when_encoder_is_unavailable()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var capabilities = new FfmpegCapabilities(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "libx264",
            });
            var planner = new ReencodeExportPlanner(capabilities);
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new AppSettings
                {
                    FfmpegPath = @"C:\tools\ffmpeg.exe",
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Nvenc,
                    LastVideoBitrateKbps = 2500,
                });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.CreatePlan(request));
            Assert.Contains("No supported video encoder", exception.Message);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
