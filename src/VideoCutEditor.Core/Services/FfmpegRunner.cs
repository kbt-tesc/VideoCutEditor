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

            IReadOnlyList<string> exportArguments = plan.Arguments;
            if (plan.AudioNormalizationAnalysis is not null)
            {
                progress?.Report(new ExportProgress(null, null, "Analyzing audio loudness..."));
                FfmpegProcessResult analysisResult = await RunProcessAsync(
                    plan.FfmpegPath,
                    plan.AudioNormalizationAnalysis.Arguments,
                    progress,
                    cancellationToken);

                if (analysisResult.ExitCode != 0)
                {
                    DeleteIfExists(plan.TemporaryOutputPath);
                    return new ExportResult(false, CreateFailureMessage(analysisResult.ExitCode, analysisResult.Stderr));
                }

                string measuredLoudnormFilter = AudioNormalizationArguments.CreateMeasuredLoudnormFilter(analysisResult.Stderr);
                exportArguments = ReplaceLoudnormFilter(plan.Arguments, measuredLoudnormFilter);
                progress?.Report(new ExportProgress(null, null, "Applying audio normalization..."));
            }

            FfmpegProcessResult exportResult = await RunProcessAsync(
                plan.FfmpegPath,
                exportArguments,
                progress,
                cancellationToken);

            if (!exportResult.Started)
            {
                return new ExportResult(false, "ffmpeg failed to start.");
            }

            if (exportResult.ExitCode != 0)
            {
                DeleteIfExists(plan.TemporaryOutputPath);
                return new ExportResult(false, CreateFailureMessage(exportResult.ExitCode, exportResult.Stderr));
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

    private static async Task<FfmpegProcessResult> RunProcessAsync(
        string ffmpegPath,
        IReadOnlyList<string> arguments,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
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
            EnableRaisingEvents = true,
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            return new FfmpegProcessResult(false, -1, string.Empty);
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

        return new FfmpegProcessResult(true, process.ExitCode, stderr);
    }

    private static IReadOnlyList<string> ReplaceLoudnormFilter(
        IReadOnlyList<string> arguments,
        string measuredLoudnormFilter)
    {
        var replacedArguments = new List<string>(arguments.Count);
        bool replaced = false;

        foreach (string argument in arguments)
        {
            if (argument.Contains(AudioNormalizationArguments.LoudnormFilter, StringComparison.Ordinal))
            {
                replacedArguments.Add(argument.Replace(
                    AudioNormalizationArguments.LoudnormFilter,
                    measuredLoudnormFilter,
                    StringComparison.Ordinal));
                replaced = true;
                continue;
            }

            replacedArguments.Add(argument);
        }

        if (!replaced)
        {
            throw new InvalidOperationException("Could not apply measured loudness normalization arguments.");
        }

        return replacedArguments;
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

    private sealed record FfmpegProcessResult(bool Started, int ExitCode, string Stderr);
}
