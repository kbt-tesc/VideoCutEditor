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

        AddFadeArguments(
            arguments,
            request.Settings.Fade,
            request.Range.Duration,
            MediaHasAudioStream(request.MediaInfo));

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

    private static void AddFadeArguments(
        List<string> arguments,
        FadeSettings fade,
        TimeSpan clipDuration,
        bool mediaHasAudioStream)
    {
        if (!fade.HasAnyFade || clipDuration <= TimeSpan.Zero)
        {
            return;
        }

        double durationSeconds = TruncateToTwoDecimals(Math.Clamp(fade.DurationSeconds, 0, clipDuration.TotalSeconds));
        if (durationSeconds <= 0)
        {
            return;
        }

        if (BuildFadeFilter("fade", fade.VideoFadeIn, fade.VideoFadeOut, durationSeconds, clipDuration) is { Length: > 0 } videoFilter)
        {
            arguments.Add("-vf");
            arguments.Add(videoFilter);
        }

        if (mediaHasAudioStream
            && BuildFadeFilter("afade", fade.AudioFadeIn, fade.AudioFadeOut, durationSeconds, clipDuration) is { Length: > 0 } audioFilter)
        {
            arguments.Add("-af");
            arguments.Add(audioFilter);
            arguments.Add("-c:a");
            arguments.Add("aac");
        }
    }

    private static bool MediaHasAudioStream(MediaInfo? mediaInfo) =>
        mediaInfo is null
        || mediaInfo.Streams.Any(stream => string.Equals(stream.CodecType, "audio", StringComparison.OrdinalIgnoreCase));

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
