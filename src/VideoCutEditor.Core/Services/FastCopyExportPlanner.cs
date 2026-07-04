using System.Globalization;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class FastCopyExportPlanner : IExportPlanner
{
    public ExportPlan CreatePlan(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Range.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Settings.FfmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);

        string temporaryOutputPath = ExportPlanPathHelper.CreateTemporaryOutputPath(request.OutputPath);

        string[] arguments =
        [
            "-hide_banner",
            "-nostdin",
            "-y",
            "-ss",
            FormatTimestamp(request.Range.Start),
            "-i",
            request.SourcePath,
            "-t",
            FormatTimestamp(request.Range.Duration),
            "-map",
            "0",
            "-c",
            "copy",
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

    internal static string FormatTimestamp(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(time), "Timestamp must be zero or later.");
        }

        int totalHours = (int)Math.Floor(time.TotalHours);
        return string.Create(CultureInfo.InvariantCulture, $"{totalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}");
    }
}
