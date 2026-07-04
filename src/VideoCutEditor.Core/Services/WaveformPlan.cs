namespace VideoCutEditor.Core.Services;

public sealed record WaveformPlan(
    string FfmpegPath,
    string SourcePath,
    string OutputPath,
    IReadOnlyList<string> Arguments);
