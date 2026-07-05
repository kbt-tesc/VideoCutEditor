using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class ExportPlannerFactory
{
    private readonly FfmpegCapabilities capabilities;

    public ExportPlannerFactory(FfmpegCapabilities capabilities)
    {
        this.capabilities = capabilities;
    }

    public IExportPlanner CreatePlanner(ExportMode exportMode) =>
        exportMode switch
        {
            ExportMode.FastCopy => new FastCopyExportPlanner(),
            ExportMode.Reencode => new ReencodeExportPlanner(capabilities),
            _ => throw new ArgumentOutOfRangeException(nameof(exportMode)),
        };
}
