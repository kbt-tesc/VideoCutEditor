using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public interface IExportPlanner
{
    ExportPlan CreatePlan(ExportRequest request);
}
