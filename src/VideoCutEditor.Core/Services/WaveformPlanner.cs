namespace VideoCutEditor.Core.Services;

public sealed class WaveformPlanner
{
    public WaveformPlan CreatePlan(
        string ffmpegPath,
        string sourcePath,
        string outputPath,
        int width,
        int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);

        string[] arguments =
        [
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            sourcePath,
            "-filter_complex",
            $"aformat=channel_layouts=mono,showwavespic=s={width}x{height}:colors=#4cc2ff",
            "-frames:v",
            "1",
            outputPath,
        ];

        return new WaveformPlan(ffmpegPath, sourcePath, outputPath, arguments);
    }
}
