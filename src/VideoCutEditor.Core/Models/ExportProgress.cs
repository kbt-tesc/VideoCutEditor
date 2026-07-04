namespace VideoCutEditor.Core.Models;

public sealed record ExportProgress(TimeSpan? Position, double? Percent, string Status);
