using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public static class AudioEncodingService
{
    public const int DefaultBitrateKbps = 128;
    public const int MinimumBitrateKbps = 32;
    public const int MaximumBitrateKbps = 512;

    public static bool HasAudioStream(MediaInfo? mediaInfo) =>
        mediaInfo?.Streams.Any(IsAudioStream) == true;

    public static bool MayHaveAudioStream(MediaInfo? mediaInfo) =>
        mediaInfo is null || HasAudioStream(mediaInfo);

    public static int GetSuggestedBitrateKbps(MediaInfo? mediaInfo)
    {
        MediaStreamInfo? audioStream = mediaInfo?.Streams.FirstOrDefault(IsAudioStream);
        if (audioStream?.Bitrate is not > 0
            || !IsSupportedSourceCodec(audioStream.CodecName))
        {
            return DefaultBitrateKbps;
        }

        int bitrateKbps = (int)Math.Round(audioStream.Bitrate.Value / 1000d, MidpointRounding.AwayFromZero);
        return NormalizeBitrateKbps(bitrateKbps);
    }

    public static bool RequiresReencode(
        AppSettings settings,
        MediaInfo? mediaInfo,
        OutputContainer targetContainer)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (mediaInfo is not null && !HasAudioStream(mediaInfo))
        {
            return false;
        }

        if (settings.ReencodeAudio
            || settings.NormalizeAudio
            || settings.Fade.AudioFadeIn
            || settings.Fade.AudioFadeOut)
        {
            return true;
        }

        if (mediaInfo is null)
        {
            return targetContainer == OutputContainer.WebM;
        }

        return mediaInfo.Streams
            .Where(IsAudioStream)
            .Any(stream => !OutputContainerCompatibilityService.IsAudioCodecCompatible(
                stream.CodecName,
                targetContainer));
    }

    public static IReadOnlyList<string> CreateArguments(AppSettings settings, OutputContainer targetContainer)
    {
        ArgumentNullException.ThrowIfNull(settings);

        int bitrateKbps = NormalizeBitrateKbps(settings.AudioBitrateKbps);
        if (targetContainer == OutputContainer.WebM)
        {
            return
            [
                "-c:a",
                "libopus",
                "-b:a",
                $"{bitrateKbps}k",
                "-vbr",
                settings.AudioRateMode == AudioRateMode.Vbr ? "on" : "off",
            ];
        }

        return ["-c:a", "aac", "-b:a", $"{bitrateKbps}k"];
    }

    public static int NormalizeBitrateKbps(int bitrateKbps) =>
        Math.Clamp(bitrateKbps, MinimumBitrateKbps, MaximumBitrateKbps);

    private static bool IsAudioStream(MediaStreamInfo stream) =>
        string.Equals(stream.CodecType, "audio", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedSourceCodec(string? codecName) =>
        string.Equals(codecName, "aac", StringComparison.OrdinalIgnoreCase)
        || string.Equals(codecName, "opus", StringComparison.OrdinalIgnoreCase);
}
