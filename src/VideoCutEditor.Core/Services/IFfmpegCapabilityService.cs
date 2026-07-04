using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public interface IFfmpegCapabilityService
{
    Task<FfmpegCapabilities> DetectAsync(string ffmpegPath, CancellationToken cancellationToken = default);
}
