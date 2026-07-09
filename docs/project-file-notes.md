# Project File Notes

This memo records repository-specific project-file decisions that affect VS Code, C# Dev Kit, Roslyn LSP, and WinUI builds.

## Solution File Policy

- `VideoCutEditor.slnx` is the only solution file used by the repository.
- `.vscode/settings.json` must keep `dotnet.defaultSolution` set to `VideoCutEditor.slnx`.
- Do not add or restore a classic `VideoCutEditor.sln` for VS Code diagnostics unless a future tool regression proves it is required.

## VS Code Diagnostic Fallback

VS Code Problems previously reported false `DocumentCompilerSemantic` errors such as:

- `VideoCutEditor.Core` namespace not found.
- Core model/service types such as `MediaInfo`, `ExportMode`, `WaveformPlanner`, and `ISettingsService` not found.

The app still built, ran, and supported breakpoints. The failing surface was VS Code/Roslyn LSP design-time diagnostics.

The confirmed fix is the `DesignTimeBuild`-only `VideoCutEditor.Core` reference in `src/VideoCutEditor/VideoCutEditor.csproj`:

```xml
<ItemGroup Condition="'$(DesignTimeBuild)' == 'true'">
  <Reference Include="VideoCutEditor.Core">
    <HintPath>..\VideoCutEditor.Core\obj\$(Configuration)\net10.0\ref\VideoCutEditor.Core.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Keep the normal `ProjectReference` as the real build reference. The `Reference` above is only an editor diagnostic fallback and should stay scoped to `DesignTimeBuild`.

## What Did Not Fix It

These were tried and did not clear VS Code Problems:

- Adding a classic `VideoCutEditor.sln` and pinning VS Code to it.
- Adding app-level `global using` directives for `VideoCutEditor.Core.Models` and `VideoCutEditor.Core.Services`.

User feedback confirmed Problems remained after the global-usings experiment and disappeared after the `DesignTimeBuild` reference fallback.

## If The Problem Returns

1. Keep `VideoCutEditor.slnx` as the solution file.
2. Confirm `.vscode/settings.json` points to `VideoCutEditor.slnx`.
3. Confirm the app project still contains the `DesignTimeBuild`-only `VideoCutEditor.Core` reference.
4. Run `dotnet test VideoCutEditor.slnx --filter VsCodeDebugConfigurationTests`.
5. Run `dotnet build src\VideoCutEditor\VideoCutEditor.csproj -p:Platform=x64 -p:WindowsPackageType=None`.
6. Reload VS Code or restart the C# language server if diagnostics appear stale.

