using System.Diagnostics;

namespace VideoCutEditor.Tests;

public sealed class VerificationMediaScriptTests
{
    [Fact]
    public void Sample_media_script_dry_run_lists_representative_outputs()
    {
        using var outputDirectory = new TemporaryDirectory();

        (int exitCode, string output) = RunSampleMediaDryRun(outputDirectory.Path);

        Assert.Equal(0, exitCode);
        Assert.Contains("video-with-audio.mp4", output);
        Assert.Contains("video-only.mp4", output);
        Assert.Contains("quiet-audio.mp4", output);
    }

    [Fact]
    public void Ui_test_script_can_open_a_generated_sample_through_the_system_picker()
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "tests", "ui-tests.ps1");
        string script = File.ReadAllText(scriptPath);

        Assert.Contains("[string]$SampleVideoPath", script);
        Assert.Contains("Get-FilePickerWindow", script);
        Assert.Contains("FileNameControlHost", script);
        Assert.Contains("0x52D5, 0x753B, 0x3092", script);
        Assert.Contains("RangeEndTextBox", script);
    }

    [Fact]
    public void Export_ui_verification_supports_success_and_no_audio_normalization_modes()
    {
        string root = FindRepositoryRoot();
        string viewModel = File.ReadAllText(Path.Combine(root, "src", "VideoCutEditor", "ViewModels", "MainPageViewModel.cs"));
        string uiScript = File.ReadAllText(Path.Combine(root, "tests", "ui-tests.ps1"));
        string runnerPath = Path.Combine(root, "scripts", "Test-ExportUi.ps1");

        Assert.Contains("VIDEOCUTEDITOR_TEST_SETTINGS_DIRECTORY", viewModel);
        Assert.Contains("VerifyExportMode", uiScript);
        Assert.Contains("Reencode", uiScript);
        Assert.Contains("ExpectedOutputDirectory", uiScript);
        Assert.Contains("ExportButton", uiScript);
        Assert.Contains("[System.IO.File]::Exists", uiScript);
        Assert.True(File.Exists(runnerPath));
        string runner = File.ReadAllText(runnerPath);
        Assert.Contains("VIDEOCUTEDITOR_TEST_SETTINGS_DIRECTORY", runner);
        Assert.Contains("ValidateSet(\"FastCopy\", \"Reencode\", \"NormalizeAudio\", \"NormalizeNoAudio\")", runner);
        Assert.Contains("$encoderKind = if ($Mode -eq \"Reencode\") { \"Software\" }", runner);
        Assert.Contains("normalizeAudio = $normalizeAudio", runner);
        Assert.Contains("NormalizeAudioCheckBox", uiScript);
        Assert.Contains("Analyzing audio loudness...", uiScript);
        Assert.Contains("Applying audio normalization...", uiScript);
        Assert.Contains("NormalizeNoAudio", uiScript);
    }

    private static (int ExitCode, string Output) RunSampleMediaDryRun(string outputDirectory)
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "New-SampleMedia.ps1");
        var startInfo = new ProcessStartInfo("powershell")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-OutputDirectory");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add("-WhatIf");

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VideoCutEditor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VideoCutEditor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
