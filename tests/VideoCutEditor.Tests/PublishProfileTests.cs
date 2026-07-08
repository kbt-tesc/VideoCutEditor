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
}
