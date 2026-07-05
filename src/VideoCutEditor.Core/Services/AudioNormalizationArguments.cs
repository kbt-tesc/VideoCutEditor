using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

internal static class AudioNormalizationArguments
{
    public const string LoudnormFilter = "loudnorm=I=-14:TP=-1.5:LRA=11";

    public static void ThrowIfRequestedWithoutAudio(AppSettings settings, MediaInfo? mediaInfo)
    {
        if (settings.NormalizeAudio && mediaInfo is not null && !HasAudioStream(mediaInfo))
        {
            throw new InvalidOperationException("Audio normalization requires an audio stream.");
        }
    }

    public static bool MayHaveAudioStream(MediaInfo? mediaInfo) =>
        mediaInfo is null || HasAudioStream(mediaInfo);

    private static bool HasAudioStream(MediaInfo mediaInfo) =>
        mediaInfo.Streams.Any(stream => string.Equals(stream.CodecType, "audio", StringComparison.OrdinalIgnoreCase));
}
