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
    string? ChannelLayout = null);
