namespace VideoCutEditor.Core.Models;

public sealed record AudioNormalizationAnalysisPlan(IReadOnlyList<string> Arguments);

public sealed record ExportPlan(
    string FfmpegPath,
    string SourcePath,
    string TemporaryOutputPath,
    string FinalOutputPath,
    IReadOnlyList<string> Arguments,
    AudioNormalizationAnalysisPlan? AudioNormalizationAnalysis = null);
