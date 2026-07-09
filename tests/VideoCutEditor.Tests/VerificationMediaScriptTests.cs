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
