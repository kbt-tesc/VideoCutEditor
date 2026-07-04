namespace VideoCutEditor.Core.Models;

public sealed record MediaInfo(
    string SourcePath,
    TimeSpan Duration,
    string? ContainerFormat,
    long? Bitrate,
    IReadOnlyList<MediaStreamInfo> Streams);
