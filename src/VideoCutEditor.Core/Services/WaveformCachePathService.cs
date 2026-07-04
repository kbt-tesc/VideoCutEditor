using System.Security.Cryptography;
using System.Text;

namespace VideoCutEditor.Core.Services;

public static class WaveformCachePathService
{
    public static string CreateCachePath(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath));
        string name = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "VideoCutEditor", "waveforms", $"{name}.png");
    }
}
