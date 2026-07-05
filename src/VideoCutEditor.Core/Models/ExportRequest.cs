namespace VideoCutEditor.Core.Models;

public sealed record ExportRequest(
    string SourcePath,
    string OutputPath,
    ClipRange Range,
    AppSettings Settings,
    MediaInfo? MediaInfo = null);
