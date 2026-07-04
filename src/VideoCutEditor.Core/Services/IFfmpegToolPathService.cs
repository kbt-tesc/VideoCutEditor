using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public interface IFfmpegToolPathService
{
    FfmpegToolPaths Resolve(AppSettings settings);
}
