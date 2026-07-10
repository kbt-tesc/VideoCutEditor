using VideoCutEditor.Core.Services;

namespace VideoCutEditor.Tests;

public sealed class WaveformGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_reports_missing_ffmpeg()
    {
        var generator = new WaveformGenerator();
        var plan = new WaveformPlan(
            @"C:\missing\ffmpeg.exe",
            @"C:\video\source.mp4",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "waveform.png"),
            ["-version"]);

        WaveformResult result = await generator.GenerateAsync(plan);

        Assert.False(result.Succeeded);
        Assert.Contains("ffmpeg was not found", result.ErrorMessage);
    }

    [SkippableFact]
    public async Task GenerateAsync_succeeds_when_process_creates_output()
    {
        await RunFakeProcessTestAsync("success", async (plan, outputPath) =>
        {
            WaveformResult result = await new WaveformGenerator().GenerateAsync(plan);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.True(File.Exists(outputPath));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(outputPath)!, "*.partial"));
        });
    }

    [SkippableFact]
    public async Task GenerateAsync_reports_process_failure_and_removes_partial_output()
    {
        await RunFakeProcessTestAsync("fail", async (plan, outputPath) =>
        {
            WaveformResult result = await new WaveformGenerator().GenerateAsync(plan);

            Assert.False(result.Succeeded);
            Assert.Contains("exit code 7", result.ErrorMessage);
            Assert.Contains("fake ffmpeg failed", result.ErrorMessage);
            Assert.False(File.Exists(outputPath));
        });
    }

    [SkippableFact]
    public async Task GenerateAsync_reports_successful_process_that_created_no_output()
    {
        await RunFakeProcessTestAsync("no-output", async (plan, outputPath) =>
        {
            WaveformResult result = await new WaveformGenerator().GenerateAsync(plan);

            Assert.False(result.Succeeded);
            Assert.Contains("without creating", result.ErrorMessage);
            Assert.False(File.Exists(outputPath));
        });
    }

    [SkippableFact]
    public async Task GenerateAsync_cancels_process_and_removes_partial_output()
    {
        await RunFakeProcessTestAsync("cancel", async (plan, outputPath) =>
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            WaveformResult result = await new WaveformGenerator().GenerateAsync(plan, cancellation.Token);

            Assert.False(result.Succeeded);
            Assert.Contains("canceled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(outputPath));
        });
    }

    private static async Task RunFakeProcessTestAsync(
        string mode,
        Func<WaveformPlan, string, Task> assertion)
    {
        string powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");
        IntegrationTestRequirements.RequireFile(powershellPath, "Windows PowerShell is required for fake waveform process tests.");

        string directory = Path.Combine(Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string scriptPath = Path.Combine(directory, "fake-waveform.ps1");
            string outputPath = Path.Combine(directory, "waveform.png");
            await File.WriteAllTextAsync(
                scriptPath,
                """
                param([string]$Mode, [string]$OutputPath)
                if ($Mode -eq "success") {
                    Set-Content -LiteralPath $OutputPath -Value "png"
                    exit 0
                }
                if ($Mode -eq "fail") {
                    Set-Content -LiteralPath $OutputPath -Value "partial"
                    [Console]::Error.WriteLine("fake ffmpeg failed")
                    exit 7
                }
                if ($Mode -eq "cancel") {
                    Set-Content -LiteralPath $OutputPath -Value "partial"
                    Start-Sleep -Seconds 30
                    exit 0
                }
                exit 0
                """);
            var plan = new WaveformPlan(
                powershellPath,
                "source.mp4",
                outputPath,
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, mode, outputPath]);

            await assertion(plan, outputPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
