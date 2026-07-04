using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class BitrateSuggestionService
{
    private const int SparseMetadataDefaultKbps = 2500;
    private const int MinimumSuggestionKbps = 300;

    public int SuggestVideoBitrateKbps(MediaInfo mediaInfo, CodecFamily codecFamily)
    {
        ArgumentNullException.ThrowIfNull(mediaInfo);

        int baseBitrateKbps = GetSourceVideoBitrateKbps(mediaInfo)
            ?? GetContainerBitrateKbps(mediaInfo)
            ?? GetResolutionDefaultKbps(mediaInfo);

        return Math.Max(MinimumSuggestionKbps, (int)Math.Round(baseBitrateKbps * GetCodecMultiplier(codecFamily), MidpointRounding.AwayFromZero));
    }

    private static int? GetSourceVideoBitrateKbps(MediaInfo mediaInfo)
    {
        long? bitrate = mediaInfo.Streams
            .FirstOrDefault(stream => stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase))
            ?.Bitrate;

        return ToKbps(bitrate);
    }

    private static int? GetContainerBitrateKbps(MediaInfo mediaInfo) => ToKbps(mediaInfo.Bitrate);

    private static int? ToKbps(long? bitsPerSecond)
    {
        if (bitsPerSecond is null or <= 0)
        {
            return null;
        }

        return Math.Max(1, (int)Math.Round(bitsPerSecond.Value / 1000.0, MidpointRounding.AwayFromZero));
    }

    private static int GetResolutionDefaultKbps(MediaInfo mediaInfo)
    {
        MediaStreamInfo? videoStream = mediaInfo.Streams
            .FirstOrDefault(stream => stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase));
        int width = Math.Max(videoStream?.Width ?? 0, videoStream?.Height ?? 0);
        int height = Math.Min(videoStream?.Width ?? 0, videoStream?.Height ?? 0);

        return (width, height) switch
        {
            (>= 3840, >= 2160) => 12000,
            (>= 2560, >= 1440) => 8000,
            (>= 1920, >= 1080) => 5000,
            (>= 1280, >= 720) => 2500,
            (> 0, > 0) => 1200,
            _ => SparseMetadataDefaultKbps,
        };
    }

    private static double GetCodecMultiplier(CodecFamily codecFamily) => codecFamily switch
    {
        CodecFamily.H265 => 0.70,
        CodecFamily.Av1 => 0.55,
        _ => 1.0,
    };
}
