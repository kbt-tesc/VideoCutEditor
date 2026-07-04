using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public interface ISettingsService
{
    string SettingsFilePath { get; }

    ValueTask<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
