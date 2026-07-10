using System.Diagnostics;
using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class MediaProbeServiceIntegrationTests
{
    [SkippableFact]
    public async Task ProbeAsync_reads_generated_video_when_tools_are_available()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");
        IntegrationTestRequirements.RequireFile(paths.FfprobePath, "ffprobe is not available.");

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string sourcePath = Path.Combine(workingDirectory, "source.mp4");
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
                    "testsrc=size=160x90:rate=25",
                    "-t",
                    "2",
                    "-c:v",
                    "mpeg4",
                    "-q:v",
                    "5",
                    sourcePath,
                ]);

            MediaInfo info = await new MediaProbeService().ProbeAsync(paths.FfprobePath, sourcePath);

            Assert.Equal(sourcePath, info.SourcePath);
            Assert.True(info.Duration.TotalSeconds > 1.5);
            MediaStreamInfo video = Assert.Single(info.Streams, stream => stream.CodecType == "video");
            Assert.Equal(160, video.Width);
            Assert.Equal(90, video.Height);
            Assert.Equal(25, video.FrameRate!.Value, precision: 2);
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
