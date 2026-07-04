using System.Diagnostics;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed class FfmpegCapabilityService : IFfmpegCapabilityService
{
    public async Task<FfmpegCapabilities> DetectAsync(
        string ffmpegPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);

        if (!File.Exists(ffmpegPath))
        {
            throw new FileNotFoundException("ffmpeg was not found.", ffmpegPath);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        };

        process.StartInfo.ArgumentList.Add("-hide_banner");
        process.StartInfo.ArgumentList.Add("-encoders");

        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            string error = string.IsNullOrWhiteSpace(stderr)
                ? $"ffmpeg -encoders failed with exit code {process.ExitCode}."
                : stderr.Trim();
            throw new InvalidOperationException(error);
        }

        return ParseEncoders(string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
    }

    public static FfmpegCapabilities ParseEncoders(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var encoders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (trimmed.Length < 8)
            {
                continue;
            }

            string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string flags = parts[0];
            if (flags.Length >= 6 && flags.Any(char.IsLetterOrDigit))
            {
                encoders.Add(parts[1]);
            }
        }

        return new FfmpegCapabilities(encoders);
    }
}
