using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class WaveformPlannerTests
{
    [Fact]
    public void CreatePlan_builds_showwavespic_arguments()
    {
        var planner = new WaveformPlanner();

        WaveformPlan plan = planner.CreatePlan(
            @"C:\tools\ffmpeg.exe",
            @"C:\video\source.mp4",
            @"C:\cache\waveform.png",
            2400,
            160);

        Assert.Equal(@"C:\tools\ffmpeg.exe", plan.FfmpegPath);
        Assert.Equal(@"C:\video\source.mp4", plan.SourcePath);
        Assert.Equal(@"C:\cache\waveform.png", plan.OutputPath);
        Assert.Equal(
            [
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-i",
                @"C:\video\source.mp4",
                "-filter_complex",
                "aformat=channel_layouts=mono,showwavespic=s=2400x160:colors=#4cc2ff",
                "-frames:v",
                "1",
                @"C:\cache\waveform.png",
            ],
            plan.Arguments);
    }

    [Fact]
    public void CreatePlan_rejects_invalid_dimensions()
    {
        var planner = new WaveformPlanner();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            planner.CreatePlan(
                @"C:\tools\ffmpeg.exe",
                @"C:\video\source.mp4",
                @"C:\cache\waveform.png",
                0,
                160));
    }
}
