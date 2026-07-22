using System.Globalization;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class FastCopyExportPlanner : IExportPlanner
{
    private readonly FfmpegCapabilities? capabilities;

    public FastCopyExportPlanner(FfmpegCapabilities? capabilities = null)
    {
        this.capabilities = capabilities;
    }

    public ExportPlan CreatePlan(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Range.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Settings.FfmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
        AudioNormalizationArguments.ThrowIfRequestedWithoutAudio(request.Settings, request.MediaInfo);
        ValidateContainerSwitch(request);

        OutputContainer outputContainer = OutputContainerExtensions.TryFromPath(
            request.OutputPath,
            out OutputContainer parsedOutputContainer)
                ? parsedOutputContainer
                : request.Settings.LastOutputContainer;
        bool reencodeAudio = AudioEncodingService.RequiresReencode(
            request.Settings,
            request.MediaInfo,
            outputContainer);
        if (reencodeAudio)
        {
            ValidateAudioEncoder(outputContainer);
        }

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
            arguments.AddRange(
            [
                "-af",
                AudioNormalizationArguments.LoudnormFilter,
            ]);
        }

        if (reencodeAudio)
        {
            arguments.AddRange(AudioEncodingService.CreateArguments(request.Settings, outputContainer));
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
            || !OutputContainerCompatibilityService.CanExportWithVideoCopy(request.MediaInfo, outputContainer))
        {
            throw new InvalidOperationException(
                $"{outputContainer.GetDisplayName()}へFast copyできないストリームが含まれています。Re-encodeを選択してください");
        }
    }

    private void ValidateAudioEncoder(OutputContainer outputContainer)
    {
        string requiredEncoder = outputContainer == OutputContainer.WebM ? "libopus" : "aac";
        if (capabilities is not null && !capabilities.SupportsEncoder(requiredEncoder))
        {
            throw new InvalidOperationException(
                $"{outputContainer.GetDisplayName()}音声の書き出しに必要な{requiredEncoder}エンコーダーが利用できません");
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
