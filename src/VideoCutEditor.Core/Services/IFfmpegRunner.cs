using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public interface IFfmpegRunner
{
    Task<ExportResult> RunAsync(
        ExportPlan plan,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
