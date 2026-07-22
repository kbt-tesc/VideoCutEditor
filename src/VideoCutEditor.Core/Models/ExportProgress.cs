namespace VideoCutEditor.Core.Models;

public enum ExportProgressPhase
{
    Video,
    Audio,
}

public sealed record ExportProgress(
    TimeSpan? Position,
    double? Percent,
    string Status,
    ExportProgressPhase Phase = ExportProgressPhase.Video,
    long? ProcessedFrames = null);
