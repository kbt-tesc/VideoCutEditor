using System.Text.Json;
using System.Text.RegularExpressions;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

internal static partial class AudioNormalizationArguments
{
    public const string LoudnormFilter = "loudnorm=I=-14:TP=-1.5:LRA=11";
    public const string LoudnormAnalysisFilter = $"{LoudnormFilter}:print_format=json";

    public static void ThrowIfRequestedWithoutAudio(AppSettings settings, MediaInfo? mediaInfo)
    {
        if (settings.NormalizeAudio && mediaInfo is not null && !HasAudioStream(mediaInfo))
        {
            throw new InvalidOperationException("音声ストリームがないため、音量正規化を使用できません");
        }
    }

    public static bool MayHaveAudioStream(MediaInfo? mediaInfo) =>
        mediaInfo is null || HasAudioStream(mediaInfo);

    public static AudioNormalizationAnalysisPlan? CreateAnalysisPlan(ExportRequest request)
    {
        if (!request.Settings.NormalizeAudio || !MayHaveAudioStream(request.MediaInfo))
        {
            return null;
        }

        return new AudioNormalizationAnalysisPlan(
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
            "-vn",
            "-sn",
            "-dn",
            "-af",
            LoudnormAnalysisFilter,
            "-f",
            "null",
            "-",
        ]);
    }

    public static string CreateMeasuredLoudnormFilter(string stderr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stderr);

        Match match = LoudnormJsonRegex().Match(stderr);
        if (!match.Success)
        {
            throw new InvalidOperationException("ffmpeg loudness analysis did not return loudnorm measurement data.");
        }

        using JsonDocument document = JsonDocument.Parse(match.Value);
        JsonElement root = document.RootElement;

        string measuredI = GetRequiredString(root, "input_i");
        string measuredTp = GetRequiredString(root, "input_tp");
        string measuredLra = GetRequiredString(root, "input_lra");
        string measuredThreshold = GetRequiredString(root, "input_thresh");
        string offset = GetRequiredString(root, "target_offset");

        return $"{LoudnormFilter}:measured_I={measuredI}:measured_TP={measuredTp}:measured_LRA={measuredLra}:measured_thresh={measuredThreshold}:offset={offset}:linear=true:print_format=summary";
    }

    private static bool HasAudioStream(MediaInfo mediaInfo) =>
        mediaInfo.Streams.Any(stream => string.Equals(stream.CodecType, "audio", StringComparison.OrdinalIgnoreCase));

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.GetString() is not { Length: > 0 } text)
        {
            throw new InvalidOperationException($"ffmpeg loudness analysis did not return '{propertyName}'.");
        }

        return text;
    }

    [GeneratedRegex(@"\{[\s\S]*?""input_i""[\s\S]*?\}")]
    private static partial Regex LoudnormJsonRegex();
}
