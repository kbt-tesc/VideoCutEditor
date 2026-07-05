using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class AudioNormalizeExportPlanner : IExportPlanner
{
    private const string LoudnormFilter = "loudnorm=I=-14:TP=-1.5:LRA=11";

    public ExportPlan CreatePlan(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Range.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Settings.FfmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);

        if (request.MediaInfo is not null && !HasAudioStream(request.MediaInfo))
        {
            throw new InvalidOperationException("Audio normalization requires an audio stream.");
        }

        string temporaryOutputPath = ExportPlanPathHelper.CreateTemporaryOutputPath(request.OutputPath);

        string[] arguments =
        [
            "-hide_banner",
            "-nostdin",
            "-y",
            "-ss",
            FastCopyExportPlanner.FormatTimestamp(request.Range.Start),
            "-i",
            request.SourcePath,
            "-t",
            FastCopyExportPlanner.FormatTimestamp(request.Range.Duration),
            "-map",
            "0",
            "-c",
            "copy",
            "-af",
            LoudnormFilter,
            "-c:a",
            "aac",
            "-map_metadata",
            "0",
            "-avoid_negative_ts",
            "make_zero",
            temporaryOutputPath,
        ];

        return new ExportPlan(
            request.Settings.FfmpegPath!,
            request.SourcePath,
            temporaryOutputPath,
            request.OutputPath,
            arguments);
    }

    private static bool HasAudioStream(MediaInfo mediaInfo) =>
        mediaInfo.Streams.Any(stream => string.Equals(stream.CodecType, "audio", StringComparison.OrdinalIgnoreCase));
}
