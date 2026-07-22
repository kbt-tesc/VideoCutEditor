using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public static class OutputContainerCompatibilityService
{
    private static readonly IReadOnlySet<string> WebMVideoCodecs = CreateSet("vp8", "vp9", "av1");
    private static readonly IReadOnlySet<string> WebMAudioCodecs = CreateSet("vorbis", "opus");
    private static readonly IReadOnlySet<string> Mp4VideoCodecs = CreateSet("h264", "hevc", "av1", "vp9", "mpeg4");
    private static readonly IReadOnlySet<string> Mp4AudioCodecs = CreateSet("aac", "mp3", "ac3", "eac3", "alac", "opus");

    public static bool CanStreamCopy(MediaInfo mediaInfo, OutputContainer targetContainer)
    {
        ArgumentNullException.ThrowIfNull(mediaInfo);

        return mediaInfo.Streams.Count > 0
            && mediaInfo.Streams.All(stream => IsCompatible(stream, targetContainer));
    }

    private static bool IsCompatible(MediaStreamInfo stream, OutputContainer targetContainer)
    {
        if (string.IsNullOrWhiteSpace(stream.CodecName))
        {
            return false;
        }

        return (stream.CodecType.ToLowerInvariant(), targetContainer) switch
        {
            ("video", OutputContainer.WebM) => WebMVideoCodecs.Contains(stream.CodecName),
            ("audio", OutputContainer.WebM) => WebMAudioCodecs.Contains(stream.CodecName),
            ("video", OutputContainer.Mp4) => Mp4VideoCodecs.Contains(stream.CodecName),
            ("audio", OutputContainer.Mp4) => Mp4AudioCodecs.Contains(stream.CodecName),
            _ => false,
        };
    }

    private static IReadOnlySet<string> CreateSet(params string[] values) =>
        new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}
