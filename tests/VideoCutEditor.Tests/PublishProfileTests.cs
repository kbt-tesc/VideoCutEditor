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
    public void App_project_embeds_the_product_icon_in_the_executable()
    {
        string projectPath = Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "VideoCutEditor.csproj");
        XDocument project = XDocument.Load(projectPath);

        Assert.Equal("Assets\\AppIcon.ico", project.Descendants("ApplicationIcon").Single().Value);
    }

    [Fact]
    public void App_project_version_matches_current_portable_release()
    {
        string projectPath = Path.Combine(FindRepositoryRoot(), "src", "VideoCutEditor", "VideoCutEditor.csproj");
        XDocument project = XDocument.Load(projectPath);

        Assert.Equal("0.3.0", project.Descendants("Version").Single().Value);
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

    [Fact]
    public void Portable_release_script_packages_zip_checksum_and_readme()
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "New-PortableRelease.ps1");
        string script = File.ReadAllText(scriptPath);

        Assert.Contains("Publish-Portable.ps1", script);
        Assert.Contains("-Version $Version", script);
        Assert.Contains("[string]$Version = \"0.3.0\"", script);
        Assert.Contains("Compress-Archive", script);
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("README.md", script);
        Assert.Contains("LICENSE", script);
        Assert.Contains("licenses", script);
        Assert.DoesNotContain("README.txt", script);
    }

    [Fact]
    public void Portable_release_readme_contains_only_user_guidance()
    {
        string readmePath = Path.Combine(FindRepositoryRoot(), "distribution", "README.md");
        string readme = File.ReadAllText(readmePath);

        Assert.Contains("## 初回起動", readme);
        Assert.Contains("## おすすめポイント", readme);
        Assert.Contains("再エンコードなしで高速にカット", readme);
        Assert.Contains("映像はそのままで音量だけ正規化", readme);
        Assert.Contains("目標ファイルサイズ", readme);
        Assert.Contains("HDR動画をSDRへ変換", readme);
        Assert.Contains("## Fast copy（エンコードなし）", readme);
        Assert.Contains("## Re-encode（エンコードあり）", readme);
        Assert.DoesNotContain("開発者向け情報", readme);
        Assert.DoesNotContain("dotnet test", readme);
    }

    [Fact]
    public void Portable_release_script_dry_run_reports_versioned_x64_artifacts()
    {
        (int exitCode, string output) = RunPortableReleaseDryRun("0.2.0", "x64");

        Assert.Equal(0, exitCode);
        Assert.Contains("VideoCutEditor-0.2.0-win-x64.zip", output);
        Assert.Contains("VideoCutEditor-0.2.0-win-x64.zip.sha256", output);
    }

    [Fact]
    public void Repository_contains_official_license_and_dependency_notices()
    {
        string root = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(root, "LICENSE")));
        Assert.True(File.Exists(Path.Combine(root, "third-party", "Microsoft.WindowsAppSDK.txt")));
        Assert.True(File.Exists(Path.Combine(root, "third-party", "CommunityToolkit.Mvvm.txt")));
        Assert.True(File.Exists(Path.Combine(root, "third-party", "dotnet-Windows.txt")));
        Assert.True(File.Exists(Path.Combine(root, "third-party", "dotnet-ThirdPartyNotices.txt")));
        Assert.True(File.Exists(Path.Combine(root, "third-party", "NSIS.txt")));
        Assert.True(File.Exists(Path.Combine(root, "third-party", "microsoft-win-dev-skills.txt")));
        Assert.True(File.Exists(Path.Combine(root, "third-party", "microsoft-win-dev-skills-ThirdPartyNotices.md")));
    }

    [Fact]
    public void Installer_is_per_user_and_supports_normal_uninstall()
    {
        string root = FindRepositoryRoot();
        string installerDefinition = File.ReadAllText(Path.Combine(root, "installer", "VideoCutEditor.nsi"));
        string releaseScript = File.ReadAllText(Path.Combine(root, "scripts", "New-InstallerRelease.ps1"));

        Assert.Contains("RequestExecutionLevel user", installerDefinition);
        Assert.Contains("InstallDir \"$LOCALAPPDATA\\Programs\\VideoCutEditor\"", installerDefinition);
        Assert.Contains("WriteUninstaller", installerDefinition);
        Assert.Contains("$SMPROGRAMS\\VideoCutEditor", installerDefinition);
        Assert.Contains("MUI_PAGE_LICENSE", installerDefinition);
        Assert.Contains("Microsoft.WindowsAppSDK.txt", installerDefinition);
        Assert.Contains("Publish-Portable.ps1", releaseScript);
        Assert.Contains("makensis", releaseScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get-FileHash", releaseScript);
        Assert.Contains("[string]$Version = \"0.3.0\"", releaseScript);
    }

    public static TheoryData<string, int, int> AppIconPngAssets => new()
    {
        { "design/VideoCutEditor-icon-master.png", 1024, 1024 },
        { "src/VideoCutEditor/Assets/LockScreenLogo.scale-200.png", 48, 48 },
        { "src/VideoCutEditor/Assets/SplashScreen.scale-200.png", 1240, 600 },
        { "src/VideoCutEditor/Assets/Square150x150Logo.scale-200.png", 300, 300 },
        { "src/VideoCutEditor/Assets/Square44x44Logo.scale-200.png", 88, 88 },
        { "src/VideoCutEditor/Assets/Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24 },
        { "src/VideoCutEditor/Assets/Square44x44Logo.targetsize-48_altform-lightunplated.png", 48, 48 },
        { "src/VideoCutEditor/Assets/StoreLogo.png", 50, 50 },
        { "src/VideoCutEditor/Assets/Wide310x150Logo.scale-200.png", 620, 300 },
    };

    [Theory]
    [MemberData(nameof(AppIconPngAssets))]
    public void App_icon_png_assets_have_expected_dimensions_and_alpha(string relativePath, int width, int height)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), relativePath));

        Assert.Equal(width, ReadBigEndianInt32(bytes, 16));
        Assert.Equal(height, ReadBigEndianInt32(bytes, 20));
        Assert.Contains(bytes[25], new byte[] { 4, 6 });
    }

    [Fact]
    public void App_icon_ico_contains_standard_windows_sizes()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "VideoCutEditor",
            "Assets",
            "AppIcon.ico"));
        int imageCount = BitConverter.ToUInt16(bytes, 4);
        var sizes = new HashSet<int>();

        for (int index = 0; index < imageCount; index++)
        {
            int entryOffset = 6 + (index * 16);
            sizes.Add(bytes[entryOffset] == 0 ? 256 : bytes[entryOffset]);
        }

        Assert.Subset(sizes, new HashSet<int> { 16, 20, 24, 32, 40, 48, 64, 128, 256 });
        Assert.True(imageCount >= 9);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];

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

    private static (int ExitCode, string Output) RunPortableReleaseDryRun(string version, string platform)
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "New-PortableRelease.ps1");
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
        startInfo.ArgumentList.Add("-Version");
        startInfo.ArgumentList.Add(version);
        startInfo.ArgumentList.Add("-Platform");
        startInfo.ArgumentList.Add(platform);
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
