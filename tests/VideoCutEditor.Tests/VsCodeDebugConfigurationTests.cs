using System.Text.Json;

namespace VideoCutEditor.Tests;

public sealed class VsCodeDebugConfigurationTests
{
    [Fact]
    public void VsCode_launch_configuration_builds_and_launches_x64_debug_output()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument launchJson = ReadJsonWithComments(Path.Combine(repositoryRoot, ".vscode", "launch.json"));
        using JsonDocument tasksJson = ReadJsonWithComments(Path.Combine(repositoryRoot, ".vscode", "tasks.json"));

        JsonElement launchConfiguration = launchJson
            .RootElement
            .GetProperty("configurations")
            .EnumerateArray()
            .Single(configuration => configuration.GetProperty("name").GetString() == "VideoCutEditor: Debug x64");

        Assert.Equal("coreclr", launchConfiguration.GetProperty("type").GetString());
        Assert.Equal("launch", launchConfiguration.GetProperty("request").GetString());
        Assert.Equal("build VideoCutEditor x64", launchConfiguration.GetProperty("preLaunchTask").GetString());
        Assert.Contains(@"bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\VideoCutEditor.exe", launchConfiguration.GetProperty("program").GetString());

        JsonElement buildTask = tasksJson
            .RootElement
            .GetProperty("tasks")
            .EnumerateArray()
            .Single(task => task.GetProperty("label").GetString() == "build VideoCutEditor x64");

        Assert.Equal("process", buildTask.GetProperty("type").GetString());
        Assert.Equal("dotnet", buildTask.GetProperty("command").GetString());
        string[] arguments = buildTask.GetProperty("args").EnumerateArray().Select(argument => argument.GetString() ?? string.Empty).ToArray();
        Assert.Contains(@"${workspaceFolder}\src\VideoCutEditor\VideoCutEditor.csproj", arguments);
        Assert.Contains("-p:Platform=x64", arguments);
        Assert.Contains("-p:WindowsPackageType=None", arguments);
    }

    [Fact]
    public void VsCode_settings_pin_slnx_solution_for_language_service()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument settingsJson = ReadJsonWithComments(Path.Combine(repositoryRoot, ".vscode", "settings.json"));

        Assert.Equal("VideoCutEditor.slnx", settingsJson.RootElement.GetProperty("dotnet.defaultSolution").GetString());
    }

    [Fact]
    public void Slnx_solution_includes_app_core_and_test_projects()
    {
        string repositoryRoot = FindRepositoryRoot();
        string solution = File.ReadAllText(Path.Combine(repositoryRoot, "VideoCutEditor.slnx"));

        Assert.Contains(@"src/VideoCutEditor/VideoCutEditor.csproj", solution);
        Assert.Contains(@"src/VideoCutEditor.Core/VideoCutEditor.Core.csproj", solution);
        Assert.Contains(@"tests/VideoCutEditor.Tests/VideoCutEditor.Tests.csproj", solution);
    }

    private static JsonDocument ReadJsonWithComments(string path)
    {
        return JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
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
