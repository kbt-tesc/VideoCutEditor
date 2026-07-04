using System.Diagnostics;
using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FfmpegRunnerIntegrationTests
{
    [Fact]
    public async Task RunAsync_creates_fast_copy_output_when_ffmpeg_is_available()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        if (string.IsNullOrWhiteSpace(paths.FfmpegPath) || !File.Exists(paths.FfmpegPath))
        {
            return;
        }

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string sourcePath = Path.Combine(workingDirectory, "source.mp4");
            string outputPath = Path.Combine(workingDirectory, "source_cut.mp4");
            await RunProcessAsync(
                paths.FfmpegPath,
                [
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-y",
                    "-f",
                    "lavfi",
                    "-i",
                    "testsrc=size=128x72:rate=24",
                    "-t",
                    "2",
                    "-c:v",
                    "mpeg4",
                    "-q:v",
                    "5",
                    sourcePath,
                ]);

            var planner = new FastCopyExportPlanner();
            ExportPlan plan = planner.CreatePlan(new ExportRequest(
                sourcePath,
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(1)),
                new AppSettings { FfmpegPath = paths.FfmpegPath }));

            ExportResult result = await new FfmpegRunner().RunAsync(plan);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
            Assert.False(File.Exists(plan.TemporaryOutputPath));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_cleans_temporary_output_when_export_is_canceled()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        if (string.IsNullOrWhiteSpace(paths.FfmpegPath) || !File.Exists(paths.FfmpegPath))
        {
            return;
        }

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string temporaryOutputPath = Path.Combine(workingDirectory, "cancel.partial.mp4");
            string finalOutputPath = Path.Combine(workingDirectory, "cancel.mp4");
            var plan = new ExportPlan(
                paths.FfmpegPath,
                "generated-test-source",
                temporaryOutputPath,
                finalOutputPath,
                [
                    "-hide_banner",
                    "-y",
                    "-f",
                    "lavfi",
                    "-re",
                    "-i",
                    "testsrc=size=128x72:rate=30",
                    "-t",
                    "30",
                    "-c:v",
                    "mpeg4",
                    "-q:v",
                    "5",
                    temporaryOutputPath,
                ]);

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));

            ExportResult result = await new FfmpegRunner().RunAsync(plan, cancellationToken: cancellation.Token);

            Assert.False(result.Succeeded);
            Assert.Equal("Export canceled.", result.ErrorMessage);
            Assert.False(File.Exists(finalOutputPath));
            Assert.False(File.Exists(temporaryOutputPath));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static async Task RunProcessAsync(string executablePath, IReadOnlyList<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(stderr);
        }
    }
}
