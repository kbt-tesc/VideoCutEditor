using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class AudioNormalizeExportPlannerTests
{
    [Fact]
    public void CreatePlan_builds_loudnorm_arguments_and_copies_video()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var planner = new AudioNormalizeExportPlanner();
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(12)),
                new AppSettings { FfmpegPath = @"C:\tools\ffmpeg.exe" },
                new MediaInfo(
                    @"C:\video\source.mp4",
                    TimeSpan.FromSeconds(30),
                    "mov,mp4,m4a,3gp,3g2,mj2",
                    3_000_000,
                    [
                        new MediaStreamInfo(0, "video", "h264", 2_500_000),
                        new MediaStreamInfo(1, "audio", "aac", 128_000),
                    ]));

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
                    "00:00:02.000",
                    "-i",
                    @"C:\video\source.mp4",
                    "-t",
                    "00:00:10.000",
                    "-map",
                    "0",
                    "-c",
                    "copy",
                    "-af",
                    "loudnorm=I=-14:TP=-1.5:LRA=11",
                    "-c:a",
                    "aac",
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
    public void CreatePlan_rejects_media_without_audio_stream()
    {
        var planner = new AudioNormalizeExportPlanner();
        var request = new ExportRequest(
            @"C:\video\source.mp4",
            @"C:\output\clip_cut.mp4",
            new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
            new AppSettings { FfmpegPath = @"C:\tools\ffmpeg.exe" },
            new MediaInfo(
                @"C:\video\source.mp4",
                TimeSpan.FromSeconds(30),
                "mov,mp4,m4a,3gp,3g2,mj2",
                3_000_000,
                [
                    new MediaStreamInfo(0, "video", "h264", 2_500_000),
                ]));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => planner.CreatePlan(request));

        Assert.Contains("audio stream", exception.Message);
    }
}
