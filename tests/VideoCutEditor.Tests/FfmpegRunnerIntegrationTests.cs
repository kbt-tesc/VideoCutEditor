using System.Diagnostics;
using VideoCutEditor.Core.Models;
using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class FfmpegRunnerIntegrationTests
{
    [SkippableFact]
    public async Task RunAsync_applies_two_pass_loudnorm_measurements_before_export()
    {
        string? powershellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        IntegrationTestRequirements.RequireFile(powershellPath, "Windows PowerShell is not available.");

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string fakeScriptPath = Path.Combine(workingDirectory, "fake-ffmpeg.ps1");
            string argumentsLogPath = Path.Combine(workingDirectory, "final-arguments.txt");
            string temporaryOutputPath = Path.Combine(workingDirectory, "output.partial.mp4");
            string finalOutputPath = Path.Combine(workingDirectory, "output.mp4");
            File.WriteAllText(
                fakeScriptPath,
                """
                param(
                    [Parameter(ValueFromRemainingArguments = $true)]
                    [string[]]$Remaining
                )

                if ($Remaining[0] -eq "analysis") {
                    [Console]::Error.WriteLine('{')
                    [Console]::Error.WriteLine('  "input_i" : "-20.00",')
                    [Console]::Error.WriteLine('  "input_tp" : "-1.00",')
                    [Console]::Error.WriteLine('  "input_lra" : "5.00",')
                    [Console]::Error.WriteLine('  "input_thresh" : "-30.00",')
                    [Console]::Error.WriteLine('  "target_offset" : "1.25"')
                    [Console]::Error.WriteLine('}')
                    exit 0
                }

                $logPath = $Remaining[1]
                $outputPath = $Remaining[$Remaining.Length - 1]
                Set-Content -LiteralPath $logPath -Value ($Remaining -join "`n")
                Set-Content -LiteralPath $outputPath -Value "output"
                exit 0
                """);

            var plan = new ExportPlan(
                powershellPath,
                "source.mp4",
                temporaryOutputPath,
                finalOutputPath,
                [
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    fakeScriptPath,
                    "final",
                    argumentsLogPath,
                    "-af",
                    "loudnorm=I=-14:TP=-1.5:LRA=11",
                    temporaryOutputPath,
                ],
                new AudioNormalizationAnalysisPlan(
                [
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    fakeScriptPath,
                    "analysis",
                ]));

            ExportResult result = await new FfmpegRunner().RunAsync(plan);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.True(File.Exists(finalOutputPath));
            Assert.False(File.Exists(temporaryOutputPath));

            string finalArguments = File.ReadAllText(argumentsLogPath);
            Assert.Contains("measured_I=-20.00", finalArguments);
            Assert.Contains("measured_TP=-1.00", finalArguments);
            Assert.Contains("measured_LRA=5.00", finalArguments);
            Assert.Contains("measured_thresh=-30.00", finalArguments);
            Assert.Contains("offset=1.25", finalArguments);
            Assert.Contains("linear=true", finalArguments);
            Assert.DoesNotContain("print_format=json", finalArguments);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [SkippableFact]
    public async Task RunAsync_creates_fast_copy_output_when_ffmpeg_is_available()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");

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

    [SkippableFact]
    public async Task RunAsync_creates_reencode_output_with_fades_when_tools_are_available()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");
        IntegrationTestRequirements.RequireFile(paths.FfprobePath, "ffprobe is not available.");

        FfmpegCapabilities capabilities = await new FfmpegCapabilityService().DetectAsync(paths.FfmpegPath);
        IntegrationTestRequirements.Require(capabilities.SupportsEncoder("libx264"), "ffmpeg does not provide the libx264 encoder.");

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string sourcePath = Path.Combine(workingDirectory, "source-with-audio.mp4");
            string outputPath = Path.Combine(workingDirectory, "source-with-audio_cut.mp4");
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
                    "-f",
                    "lavfi",
                    "-i",
                    "sine=frequency=1000:sample_rate=44100",
                    "-t",
                    "2",
                    "-c:v",
                    "mpeg4",
                    "-q:v",
                    "5",
                    "-c:a",
                    "aac",
                    "-shortest",
                    sourcePath,
                ]);

            var planner = new ReencodeExportPlanner(capabilities);
            ExportPlan plan = planner.CreatePlan(new ExportRequest(
                sourcePath,
                outputPath,
                new ClipRange(TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(1.75)),
                new AppSettings
                {
                    FfmpegPath = paths.FfmpegPath,
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 500,
                    Fade = new FadeSettings
                    {
                        VideoFadeIn = true,
                        VideoFadeOut = true,
                        AudioFadeIn = true,
                        AudioFadeOut = true,
                        DurationSeconds = 0.25,
                    },
                }));

            var progressEvents = new List<ExportProgress>();
            ExportResult result = await new FfmpegRunner().RunAsync(
                plan,
                new Progress<ExportProgress>(progressEvents.Add));

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            Assert.True(new FileInfo(outputPath).Length > 0);
            Assert.False(File.Exists(plan.TemporaryOutputPath));
            Assert.Contains(progressEvents, progress => progress is not null && progress.Percent == 1);

            MediaInfo outputInfo = await new MediaProbeService().ProbeAsync(paths.FfprobePath, outputPath);
            Assert.Contains(outputInfo.Streams, stream => stream.CodecType == "video");
            Assert.Contains(outputInfo.Streams, stream => stream.CodecType == "audio");
            Assert.InRange(outputInfo.Duration.TotalSeconds, 1.0, 2.1);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [SkippableFact]
    public async Task RunAsync_reencodes_video_only_source_when_audio_fade_is_enabled()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");
        IntegrationTestRequirements.RequireFile(paths.FfprobePath, "ffprobe is not available.");

        FfmpegCapabilities capabilities = await new FfmpegCapabilityService().DetectAsync(paths.FfmpegPath);
        IntegrationTestRequirements.Require(capabilities.SupportsEncoder("libx264"), "ffmpeg does not provide the libx264 encoder.");

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string sourcePath = Path.Combine(workingDirectory, "source-video-only.mp4");
            string outputPath = Path.Combine(workingDirectory, "source-video-only_cut.mp4");
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

            MediaInfo sourceInfo = await new MediaProbeService().ProbeAsync(paths.FfprobePath, sourcePath);
            var planner = new ReencodeExportPlanner(capabilities);
            ExportPlan plan = planner.CreatePlan(new ExportRequest(
                sourcePath,
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(1.5)),
                new AppSettings
                {
                    FfmpegPath = paths.FfmpegPath,
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastVideoBitrateKbps = 500,
                    Fade = new FadeSettings
                    {
                        AudioFadeIn = true,
                        AudioFadeOut = true,
                        DurationSeconds = 0.25,
                    },
                },
                sourceInfo));

            ExportResult result = await new FfmpegRunner().RunAsync(plan);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            Assert.False(File.Exists(plan.TemporaryOutputPath));

            MediaInfo outputInfo = await new MediaProbeService().ProbeAsync(paths.FfprobePath, outputPath);
            Assert.Contains(outputInfo.Streams, stream => stream.CodecType == "video");
            Assert.DoesNotContain(outputInfo.Streams, stream => stream.CodecType == "audio");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [SkippableFact]
    public async Task RunAsync_creates_reencode_output_with_quality_mode_when_tools_are_available()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");
        IntegrationTestRequirements.RequireFile(paths.FfprobePath, "ffprobe is not available.");

        FfmpegCapabilities capabilities = await new FfmpegCapabilityService().DetectAsync(paths.FfmpegPath);
        IntegrationTestRequirements.Require(capabilities.SupportsEncoder("libx264"), "ffmpeg does not provide the libx264 encoder.");

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string sourcePath = Path.Combine(workingDirectory, "source-quality.mp4");
            string outputPath = Path.Combine(workingDirectory, "source-quality_cut.mp4");
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

            var planner = new ReencodeExportPlanner(capabilities);
            ExportPlan plan = planner.CreatePlan(new ExportRequest(
                sourcePath,
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(1.5)),
                new AppSettings
                {
                    FfmpegPath = paths.FfmpegPath,
                    LastExportMode = ExportMode.Reencode,
                    LastCodecFamily = CodecFamily.H264,
                    LastEncoderKind = EncoderKind.Software,
                    LastBitrateMode = BitrateMode.Quality,
                    LastQualityValue = 23,
                }));

            ExportResult result = await new FfmpegRunner().RunAsync(plan);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            Assert.False(File.Exists(plan.TemporaryOutputPath));

            MediaInfo outputInfo = await new MediaProbeService().ProbeAsync(paths.FfprobePath, outputPath);
            Assert.Contains(outputInfo.Streams, stream => stream.CodecType == "video");
            Assert.InRange(outputInfo.Duration.TotalSeconds, 1.0, 2.1);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [SkippableFact]
    public async Task RunAsync_creates_audio_normalized_output_when_tools_are_available()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");
        IntegrationTestRequirements.RequireFile(paths.FfprobePath, "ffprobe is not available.");

        string workingDirectory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string sourcePath = Path.Combine(workingDirectory, "source-normalize.mp4");
            string outputPath = Path.Combine(workingDirectory, "source-normalize_cut.mp4");
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
                    "-f",
                    "lavfi",
                    "-i",
                    "sine=frequency=1000:sample_rate=44100",
                    "-t",
                    "2",
                    "-c:v",
                    "mpeg4",
                    "-q:v",
                    "5",
                    "-c:a",
                    "aac",
                    "-shortest",
                    sourcePath,
                ]);

            MediaInfo sourceInfo = await new MediaProbeService().ProbeAsync(paths.FfprobePath, sourcePath);
            var planner = new FastCopyExportPlanner();
            ExportPlan plan = planner.CreatePlan(new ExportRequest(
                sourcePath,
                outputPath,
                new ClipRange(TimeSpan.Zero, TimeSpan.FromSeconds(1.5)),
                new AppSettings
                {
                    FfmpegPath = paths.FfmpegPath,
                    LastExportMode = ExportMode.FastCopy,
                    NormalizeAudio = true,
                },
                sourceInfo));

            ExportResult result = await new FfmpegRunner().RunAsync(plan);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            Assert.False(File.Exists(plan.TemporaryOutputPath));

            MediaInfo outputInfo = await new MediaProbeService().ProbeAsync(paths.FfprobePath, outputPath);
            Assert.Contains(outputInfo.Streams, stream => stream.CodecType == "video");
            Assert.Contains(outputInfo.Streams, stream => stream.CodecType == "audio");
            Assert.Contains(outputInfo.Streams, stream => stream.CodecType == "audio" && stream.CodecName == "aac");
            Assert.InRange(outputInfo.Duration.TotalSeconds, 1.0, 2.1);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [SkippableFact]
    public async Task RunAsync_cleans_temporary_output_when_export_is_canceled()
    {
        FfmpegToolPaths paths = new FfmpegToolPathService().Resolve(new AppSettings());
        IntegrationTestRequirements.RequireFile(paths.FfmpegPath, "ffmpeg is not available.");

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
