using System.Globalization;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class ReencodeExportPlanner : IExportPlanner
{
    private const int DefaultVideoBitrateKbps = 2500;
    private const int DefaultQualityValue = 23;
    private const int MinQualityValue = 0;
    private const int MaxQualityValue = 51;

    private readonly FfmpegCapabilities capabilities;

    public ReencodeExportPlanner(FfmpegCapabilities capabilities)
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

        string? videoEncoder = capabilities.ChooseVideoEncoder(
            request.Settings.LastCodecFamily,
            request.Settings.LastEncoderKind);

        if (videoEncoder is null)
        {
            throw new InvalidOperationException(
                $"No supported video encoder is available for {request.Settings.LastCodecFamily} with {request.Settings.LastEncoderKind}.");
        }

        string temporaryOutputPath = ExportPlanPathHelper.CreateTemporaryOutputPath(request.OutputPath);

        var arguments = new List<string>
        {
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
            "-c:v",
            videoEncoder,
        };

        AddRateControlArguments(arguments, request.Settings, videoEncoder);

        AddFilterArguments(
            arguments,
            request.Settings,
            request.Range.Duration,
            AudioNormalizationArguments.MayHaveAudioStream(request.MediaInfo));

        arguments.AddRange(
        [
            "-map_metadata",
            "0",
            "-avoid_negative_ts",
            "make_zero",
        ]);

        arguments.AddRange(FfmpegArgumentParser.Parse(request.Settings.AdditionalFfmpegArguments));
        arguments.Add(temporaryOutputPath);

        return new ExportPlan(
            request.Settings.FfmpegPath!,
            request.SourcePath,
            temporaryOutputPath,
            request.OutputPath,
            arguments);
    }

    private static void AddRateControlArguments(List<string> arguments, AppSettings settings, string videoEncoder)
    {
        if (settings.LastBitrateMode == BitrateMode.Quality)
        {
            arguments.Add(IsNvencEncoder(videoEncoder) ? "-cq" : "-crf");
            arguments.Add(NormalizeQualityValue(settings.LastQualityValue).ToString(CultureInfo.InvariantCulture));
            return;
        }

        int videoBitrateKbps = settings.LastVideoBitrateKbps.GetValueOrDefault(DefaultVideoBitrateKbps);
        arguments.Add("-b:v");
        arguments.Add($"{videoBitrateKbps}k");
    }

    private static void AddFilterArguments(
        List<string> arguments,
        AppSettings settings,
        TimeSpan clipDuration,
        bool mediaHasAudioStream)
    {
        FadeSettings fade = settings.Fade;
        bool hasFade = fade.HasAnyFade && clipDuration > TimeSpan.Zero;
        bool hasAudioProcessing = settings.NormalizeAudio || hasFade;

        if (!hasAudioProcessing)
        {
            return;
        }

        double durationSeconds = hasFade
            ? TruncateToTwoDecimals(Math.Clamp(fade.DurationSeconds, 0, clipDuration.TotalSeconds))
            : 0;

        if (durationSeconds > 0
            && BuildFadeFilter("fade", fade.VideoFadeIn, fade.VideoFadeOut, durationSeconds, clipDuration) is { Length: > 0 } videoFilter)
        {
            arguments.Add("-vf");
            arguments.Add(videoFilter);
        }

        if (!mediaHasAudioStream)
        {
            return;
        }

        var audioFilters = new List<string>(capacity: 2);
        if (settings.NormalizeAudio)
        {
            audioFilters.Add(AudioNormalizationArguments.LoudnormFilter);
        }

        if (durationSeconds > 0
            && BuildFadeFilter("afade", fade.AudioFadeIn, fade.AudioFadeOut, durationSeconds, clipDuration) is { Length: > 0 } audioFadeFilter)
        {
            audioFilters.Add(audioFadeFilter);
        }

        if (audioFilters.Count == 0)
        {
            return;
        }

        arguments.Add("-af");
        arguments.Add(string.Join(",", audioFilters));
        arguments.Add("-c:a");
        arguments.Add("aac");
    }

    private static bool IsNvencEncoder(string videoEncoder) =>
        videoEncoder.EndsWith("_nvenc", StringComparison.OrdinalIgnoreCase);

    private static int NormalizeQualityValue(int? qualityValue) =>
        Math.Clamp(qualityValue.GetValueOrDefault(DefaultQualityValue), MinQualityValue, MaxQualityValue);

    private static string? BuildFadeFilter(
        string filterName,
        bool fadeIn,
        bool fadeOut,
        double durationSeconds,
        TimeSpan clipDuration)
    {
        var filters = new List<string>(capacity: 2);
        string duration = FormatFilterSeconds(durationSeconds);

        if (fadeIn)
        {
            filters.Add($"{filterName}=t=in:st=0:d={duration}");
        }

        if (fadeOut)
        {
            double startSeconds = Math.Max(0, clipDuration.TotalSeconds - durationSeconds);
            filters.Add($"{filterName}=t=out:st={FormatFilterSeconds(startSeconds)}:d={duration}");
        }

        return filters.Count == 0 ? null : string.Join(",", filters);
    }

    private static string FormatFilterSeconds(double seconds) =>
        seconds.ToString("0.##", CultureInfo.InvariantCulture);

    private static double TruncateToTwoDecimals(double value) =>
        Math.Truncate(value * 100) / 100;
}
