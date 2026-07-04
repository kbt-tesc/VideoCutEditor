using System.Diagnostics;
using System.Text.RegularExpressions;
using VideoCutEditor.Core.Models;

namespace VideoCutEditor.Core.Services;

public sealed partial class FfmpegRunner : IFfmpegRunner
{
    public async Task<ExportResult> RunAsync(
        ExportPlan plan,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!File.Exists(plan.FfmpegPath))
        {
            return new ExportResult(false, $"ffmpeg was not found: {plan.FfmpegPath}");
        }

        try
        {
            DeleteIfExists(plan.TemporaryOutputPath);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = plan.FfmpegPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            foreach (string argument in plan.Arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            if (!process.Start())
            {
                return new ExportResult(false, "ffmpeg failed to start.");
            }

            await using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                }
            });

            Task<string> stderrTask = ReadStreamAsync(process.StandardError, progress, cancellationToken);
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            string stderr = await stderrTask;
            _ = await stdoutTask;

            if (process.ExitCode != 0)
            {
                DeleteIfExists(plan.TemporaryOutputPath);
                return new ExportResult(false, CreateFailureMessage(process.ExitCode, stderr));
            }

            if (!File.Exists(plan.TemporaryOutputPath))
            {
                return new ExportResult(false, "ffmpeg completed without creating an output file.");
            }

            if (File.Exists(plan.FinalOutputPath))
            {
                DeleteIfExists(plan.TemporaryOutputPath);
                return new ExportResult(false, $"Output file already exists: {plan.FinalOutputPath}");
            }

            File.Move(plan.TemporaryOutputPath, plan.FinalOutputPath);
            progress?.Report(new ExportProgress(null, 1, "Export complete"));
            return new ExportResult(true, null);
        }
        catch (OperationCanceledException)
        {
            DeleteIfExists(plan.TemporaryOutputPath);
            return new ExportResult(false, "Export canceled.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            DeleteIfExists(plan.TemporaryOutputPath);
            return new ExportResult(false, exception.Message);
        }
    }

    private static async Task<string> ReadStreamAsync(
        StreamReader reader,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var log = new List<string>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            log.Add(line);
            TimeSpan? position = ParseProgressPosition(line);
            progress?.Report(new ExportProgress(position, null, line));
        }

        return string.Join(Environment.NewLine, log);
    }

    private static TimeSpan? ParseProgressPosition(string line)
    {
        Match match = FfmpegTimeRegex().Match(line);
        return match.Success && TimeSpan.TryParse(match.Groups["time"].Value, out TimeSpan value)
            ? value
            : null;
    }

    private static string CreateFailureMessage(int exitCode, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return $"ffmpeg failed with exit code {exitCode}.";
        }

        string lastLine = stderr
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? stderr;
        return $"ffmpeg failed with exit code {exitCode}: {lastLine}";
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    [GeneratedRegex(@"time=(?<time>\d{2}:\d{2}:\d{2}\.\d{2,3})")]
    private static partial Regex FfmpegTimeRegex();
}
