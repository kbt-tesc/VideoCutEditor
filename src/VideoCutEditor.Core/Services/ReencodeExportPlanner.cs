using System.Globalization;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class ReencodeExportPlanner : IExportPlanner
{
    private const int DefaultVideoBitrateKbps = 2500;

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

        int videoBitrateKbps = request.Settings.LastVideoBitrateKbps.GetValueOrDefault(DefaultVideoBitrateKbps);
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
            "-b:v",
            $"{videoBitrateKbps}k",
        };

        AddFadeArguments(arguments, request.Settings.Fade, request.Range.Duration);

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

    private static void AddFadeArguments(List<string> arguments, FadeSettings fade, TimeSpan clipDuration)
    {
        if (!fade.HasAnyFade || clipDuration <= TimeSpan.Zero)
        {
            return;
        }

        double durationSeconds = Math.Clamp(fade.DurationSeconds, 0, clipDuration.TotalSeconds);
        if (durationSeconds <= 0)
        {
            return;
        }

        if (BuildFadeFilter("fade", fade.VideoFadeIn, fade.VideoFadeOut, durationSeconds, clipDuration) is { Length: > 0 } videoFilter)
        {
            arguments.Add("-vf");
            arguments.Add(videoFilter);
        }

        if (BuildFadeFilter("afade", fade.AudioFadeIn, fade.AudioFadeOut, durationSeconds, clipDuration) is { Length: > 0 } audioFilter)
        {
            arguments.Add("-af");
            arguments.Add(audioFilter);
            arguments.Add("-c:a");
            arguments.Add("aac");
        }
    }

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
        seconds.ToString("0.###", CultureInfo.InvariantCulture);
}
