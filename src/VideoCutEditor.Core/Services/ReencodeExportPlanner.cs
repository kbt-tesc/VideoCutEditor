using System.Globalization;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class ReencodeExportPlanner : IExportPlanner
{
    private const int DefaultVideoBitrateKbps = 2500;
    private const int DefaultQualityValue = 23;
    private const int MinQualityValue = 0;
    private const int MaxQualityValue = 51;
    private const string HdrToSdrVideoFilter = "zscale=t=linear:npl=100,format=gbrpf32le,tonemap=tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=tv,format=yuv420p";

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
        IReadOnlyList<string> additionalArguments = FfmpegArgumentParser.Parse(request.Settings.AdditionalFfmpegArguments);
        FfmpegAdditionalArgumentValidator.Validate(additionalArguments);

        bool isWebM = OutputContainerExtensions.TryFromPath(request.OutputPath, out OutputContainer outputContainer)
            && outputContainer == OutputContainer.WebM;
        bool mediaHasAudioStream = AudioNormalizationArguments.MayHaveAudioStream(request.MediaInfo);
        CodecFamily videoCodecFamily = isWebM ? CodecFamily.Av1 : request.Settings.LastCodecFamily;
        string? videoEncoder = capabilities.ChooseVideoEncoder(
            videoCodecFamily,
            request.Settings.LastEncoderKind);

        if (videoEncoder is null)
        {
            throw new InvalidOperationException(
                $"No supported video encoder is available for {videoCodecFamily} with {request.Settings.LastEncoderKind}.");
        }

        if (isWebM && mediaHasAudioStream && !capabilities.SupportsEncoder("libopus"))
        {
            throw new InvalidOperationException("WebM音声の書き出しに必要なlibopusエンコーダーが利用できません");
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
        };

        if (isWebM)
        {
            arguments.AddRange(["-map", "0:v?", "-map", "0:a?"]);
        }
        else
        {
            arguments.AddRange(["-map", "0"]);
        }

        arguments.AddRange(["-c", "copy", "-c:v", videoEncoder]);

        AddRateControlArguments(arguments, request.Settings, videoEncoder);

        bool hasAudioFilters = AddFilterArguments(
            arguments,
            request.Settings,
            request.MediaInfo,
            request.Range.Duration,
            mediaHasAudioStream);

        if (mediaHasAudioStream && (hasAudioFilters || isWebM))
        {
            arguments.Add("-c:a");
            arguments.Add(isWebM ? "libopus" : "aac");
        }

        arguments.AddRange(
        [
            "-map_metadata",
            "0",
            "-avoid_negative_ts",
            "make_zero",
        ]);

        arguments.AddRange(additionalArguments);
        arguments.Add(temporaryOutputPath);

        return new ExportPlan(
            request.Settings.FfmpegPath!,
            request.SourcePath,
            temporaryOutputPath,
            request.OutputPath,
            arguments,
            AudioNormalizationArguments.CreateAnalysisPlan(request));
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

    private static bool AddFilterArguments(
        List<string> arguments,
        AppSettings settings,
        MediaInfo? mediaInfo,
        TimeSpan clipDuration,
        bool mediaHasAudioStream)
    {
        FadeSettings fade = settings.Fade;
        bool hasFade = fade.HasAnyFade && clipDuration > TimeSpan.Zero;
        bool hasHdrToSdr = settings.ConvertHdrToSdr && HasHdrVideoStream(mediaInfo);
        bool hasAudioProcessing = settings.NormalizeAudio || hasFade;

        if (!hasAudioProcessing && !hasHdrToSdr)
        {
            return false;
        }

        double durationSeconds = hasFade
            ? TruncateToTwoDecimals(Math.Clamp(fade.DurationSeconds, 0, clipDuration.TotalSeconds))
            : 0;

        var videoFilters = new List<string>(capacity: 2);
        if (hasHdrToSdr)
        {
            videoFilters.Add(HdrToSdrVideoFilter);
        }

        if (durationSeconds > 0
            && BuildFadeFilter("fade", fade.VideoFadeIn, fade.VideoFadeOut, durationSeconds, clipDuration) is { Length: > 0 } videoFadeFilter)
        {
            videoFilters.Add(videoFadeFilter);
        }

        if (videoFilters.Count > 0)
        {
            arguments.Add("-vf");
            arguments.Add(string.Join(",", videoFilters));
        }

        if (!mediaHasAudioStream)
        {
            return false;
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
            return false;
        }

        arguments.Add("-af");
        arguments.Add(string.Join(",", audioFilters));
        return true;
    }

    private static bool IsNvencEncoder(string videoEncoder) =>
        videoEncoder.EndsWith("_nvenc", StringComparison.OrdinalIgnoreCase);

    private static bool HasHdrVideoStream(MediaInfo? mediaInfo) =>
        mediaInfo?.Streams.Any(stream => stream.IsHighDynamicRange) == true;

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
