using System.Diagnostics;
using System.Xml.Linq;

namespace VideoCutEditor.Tests;

public sealed class PublishProfileTests
{
    public static TheoryData<string, string> PortableProfiles => new()
    {
        { "win-x64.pubxml", "win-x64" },
        { "win-x86.pubxml", "win-x86" },
        { "win-arm64.pubxml", "win-arm64" },
    };

    [Theory]
    [MemberData(nameof(PortableProfiles))]
    public void Portable_publish_profiles_are_unpacked_self_contained_single_file(string profileName, string runtimeIdentifier)
    {
        string profilePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "VideoCutEditor",
            "Properties",
            "PublishProfiles",
            profileName);
        XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";
        XDocument profile = XDocument.Load(profilePath);

        string? property(string name) => profile.Descendants(msbuild + name).SingleOrDefault()?.Value;

        Assert.Equal(runtimeIdentifier, property("RuntimeIdentifier"));
        Assert.Equal("None", property("WindowsPackageType"));
        Assert.Equal("true", property("WindowsAppSDKSelfContained"), ignoreCase: true);
        Assert.Equal("true", property("SelfContained"), ignoreCase: true);
        Assert.Equal("true", property("PublishSingleFile"), ignoreCase: true);
        Assert.Equal("true", property("IncludeNativeLibrariesForSelfExtract"), ignoreCase: true);
    }

    [Fact]
    public void App_project_uses_self_contained_runtime_by_default()
    {
        string projectPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "VideoCutEditor",
            "VideoCutEditor.csproj");
        XDocument project = XDocument.Load(projectPath);

        string? selfContained = project
            .Descendants("SelfContained")
            .FirstOrDefault()
            ?.Value;

        Assert.Equal("true", selfContained, ignoreCase: true);
    }

    [Fact]
    public void Portable_publish_validation_accepts_single_file_artifact_with_symbols()
    {
        using var publishDirectory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(publishDirectory.Path, "VideoCutEditor.exe"), "stub");
        File.WriteAllText(Path.Combine(publishDirectory.Path, "VideoCutEditor.pdb"), "symbols");

        (int exitCode, string output) = RunPortableValidation(publishDirectory.Path);

        Assert.Equal(0, exitCode);
        Assert.Contains("Portable publish validation passed", output);
    }

    [Theory]
    [InlineData("VideoCutEditor.dll")]
    [InlineData("VideoCutEditor.runtimeconfig.json")]
    [InlineData("ffmpeg.exe")]
    [InlineData("ffprobe.exe")]
    public void Portable_publish_validation_rejects_sidecars_and_bundled_tools(string fileName)
    {
        using var publishDirectory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(publishDirectory.Path, "VideoCutEditor.exe"), "stub");
        File.WriteAllText(Path.Combine(publishDirectory.Path, fileName), "unexpected");

        (int exitCode, string output) = RunPortableValidation(publishDirectory.Path);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(fileName, output);
    }

    [Fact]
    public void Portable_publish_script_runs_artifact_validation()
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "Publish-Portable.ps1");
        string script = File.ReadAllText(scriptPath);

        Assert.Contains("Test-PortablePublish.ps1", script);
    }

    [Fact]
    public void Publish_all_portable_script_dry_run_lists_all_platforms_by_default()
    {
        (int exitCode, string output) = RunPublishAllDryRun();

        Assert.Equal(0, exitCode);
        Assert.Contains("Would publish: x64", output);
        Assert.Contains("Would publish: x86", output);
        Assert.Contains("Would publish: arm64", output);
    }

    [Fact]
    public void Publish_all_portable_script_dry_run_honors_selected_platforms()
    {
        (int exitCode, string output) = RunPublishAllDryRun("-Platforms", "x64,arm64");

        Assert.Equal(0, exitCode);
        Assert.Contains("Would publish: x64", output);
        Assert.DoesNotContain("Would publish: x86", output);
        Assert.Contains("Would publish: arm64", output);
    }

    private static (int ExitCode, string Output) RunPortableValidation(string publishDirectory)
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "Test-PortablePublish.ps1");
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
        startInfo.ArgumentList.Add("-PublishDirectory");
        startInfo.ArgumentList.Add(publishDirectory);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static (int ExitCode, string Output) RunPublishAllDryRun(params string[] arguments)
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "Publish-AllPortable.ps1");
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
        startInfo.ArgumentList.Add("-WhatIf");

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

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
