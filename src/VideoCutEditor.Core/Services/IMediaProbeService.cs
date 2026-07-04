using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public interface IMediaProbeService
{
    Task<MediaInfo> ProbeAsync(string ffprobePath, string sourcePath, CancellationToken cancellationToken = default);
}
