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
        AudioNormalizationArguments.ThrowIfRequestedWithoutAudio(request.Settings, request.MediaInfo);
        ValidateContainerSwitch(request);

        string temporaryOutputPath = ExportPlanPathHelper.CreateTemporaryOutputPath(request.OutputPath);

        var arguments = new List<string>
        {
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
        };

        if (request.Settings.NormalizeAudio)
        {
            string audioEncoder = OutputContainerExtensions.TryFromPath(request.OutputPath, out OutputContainer outputContainer)
                && outputContainer == OutputContainer.WebM
                    ? "libopus"
                    : "aac";
            arguments.AddRange(
            [
                "-af",
                AudioNormalizationArguments.LoudnormFilter,
                "-c:a",
                audioEncoder,
            ]);
        }

        arguments.AddRange(
        [
            "-map_metadata",
            "0",
            "-avoid_negative_ts",
            "make_zero",
            temporaryOutputPath,
        ]);

        return new ExportPlan(
            request.Settings.FfmpegPath!,
            request.SourcePath,
            temporaryOutputPath,
            request.OutputPath,
            arguments,
            AudioNormalizationArguments.CreateAnalysisPlan(request));
    }

    private static void ValidateContainerSwitch(ExportRequest request)
    {
        if (!OutputContainerExtensions.TryFromPath(request.SourcePath, out OutputContainer sourceContainer)
            || !OutputContainerExtensions.TryFromPath(request.OutputPath, out OutputContainer outputContainer)
            || sourceContainer == outputContainer)
        {
            return;
        }

        if (request.MediaInfo is null
            || !OutputContainerCompatibilityService.CanStreamCopy(request.MediaInfo, outputContainer))
        {
            throw new InvalidOperationException(
                $"{outputContainer.GetDisplayName()}へFast copyできないストリームが含まれています。Re-encodeを選択してください");
        }
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
