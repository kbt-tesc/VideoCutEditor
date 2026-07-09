namespace VideoCutEditor.Core.Models;

public sealed record MediaStreamInfo(
    int Index,
    string CodecType,
    string? CodecName,
    long? Bitrate,
    int? Width = null,
    int? Height = null,
    double? FrameRate = null,
    int? SampleRate = null,
    int? Channels = null,
    string? ChannelLayout = null,
    string? ColorSpace = null,
    string? ColorTransfer = null,
    string? ColorPrimaries = null)
{
    public bool IsHighDynamicRange =>
        string.Equals(CodecType, "video", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(ColorTransfer, "smpte2084", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ColorTransfer, "arib-std-b67", StringComparison.OrdinalIgnoreCase));
}
