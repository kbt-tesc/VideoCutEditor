using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FastCopyExportPlannerTests
{
    [Fact]
    public void CreatePlan_builds_fast_copy_arguments()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var planner = new FastCopyExportPlanner();
            string outputPath = Path.Combine(outputDirectory, "clip_cut.mp4");
            var request = new ExportRequest(
                @"C:\video\source.mp4",
                outputPath,
                new ClipRange(TimeSpan.FromSeconds(12.5), TimeSpan.FromSeconds(42.75)),
                new AppSettings { FfmpegPath = @"C:\tools\ffmpeg.exe" });

            ExportPlan plan = planner.CreatePlan(request);

            Assert.Equal(@"C:\tools\ffmpeg.exe", plan.FfmpegPath);
            Assert.Equal(@"C:\video\source.mp4", plan.SourcePath);
            Assert.Equal(outputPath, plan.FinalOutputPath);
            Assert.EndsWith(".mp4", plan.TemporaryOutputPath);
            Assert.Contains(".partial-", plan.TemporaryOutputPath);
            Assert.Equal(
                [
                    "-hide_banner",
                    "-nostdin",
                    "-y",
                    "-ss",
                    "00:00:12.500",
                    "-i",
                    @"C:\video\source.mp4",
                    "-t",
                    "00:00:30.250",
                    "-map",
                    "0",
                    "-c",
                    "copy",
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
    public void CreatePlan_rejects_invalid_range()
    {
        var planner = new FastCopyExportPlanner();
        var request = new ExportRequest(
            @"C:\video\source.mp4",
            @"C:\output\clip_cut.mp4",
            new ClipRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            new AppSettings { FfmpegPath = @"C:\tools\ffmpeg.exe" });

        Assert.Throws<ArgumentException>(() => planner.CreatePlan(request));
    }
}
