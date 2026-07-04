using System.Diagnostics;

namespace VideoCutEditor.Core.Services;

public sealed class WaveformGenerator : IWaveformGenerator
{
    public async Task<WaveformResult> GenerateAsync(WaveformPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!File.Exists(plan.FfmpegPath))
        {
            return new WaveformResult(false, $"ffmpeg was not found: {plan.FfmpegPath}");
        }

        try
        {
            string? outputDirectory = Path.GetDirectoryName(plan.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            DeleteIfExists(plan.OutputPath);

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
                return new WaveformResult(false, "ffmpeg failed to start.");
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

            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            string stderr = await stderrTask;
            _ = await stdoutTask;

            if (process.ExitCode != 0)
            {
                DeleteIfExists(plan.OutputPath);
                return new WaveformResult(false, CreateFailureMessage(process.ExitCode, stderr));
            }

            return File.Exists(plan.OutputPath)
                ? new WaveformResult(true, null)
                : new WaveformResult(false, "ffmpeg completed without creating a waveform image.");
        }
        catch (OperationCanceledException)
        {
            DeleteIfExists(plan.OutputPath);
            return new WaveformResult(false, "Waveform generation canceled.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            DeleteIfExists(plan.OutputPath);
            return new WaveformResult(false, exception.Message);
        }
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
}
