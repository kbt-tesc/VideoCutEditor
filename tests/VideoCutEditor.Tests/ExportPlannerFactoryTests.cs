using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class ExportPlannerFactoryTests
{
    [Fact]
    public void CreatePlanner_returns_fast_copy_planner_for_fast_copy_mode()
    {
        var factory = new ExportPlannerFactory(new FfmpegCapabilities(new HashSet<string>()));

        IExportPlanner planner = factory.CreatePlanner(ExportMode.FastCopy);

        Assert.IsType<FastCopyExportPlanner>(planner);
    }

    [Fact]
    public void CreatePlanner_returns_reencode_planner_for_reencode_mode()
    {
        var factory = new ExportPlannerFactory(new FfmpegCapabilities(new HashSet<string>
        {
            "libx264",
        }));

        IExportPlanner planner = factory.CreatePlanner(ExportMode.Reencode);

        Assert.IsType<ReencodeExportPlanner>(planner);
    }
}
