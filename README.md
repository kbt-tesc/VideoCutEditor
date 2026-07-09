# VideoCutEditor

VideoCutEditor is a lightweight Windows desktop app for extracting one selected range from a video. The app uses WinUI 3, C#, .NET 10, and external ffmpeg/ffprobe paths.

Implementation has started with a small WinUI app skeleton, a testable core library, and an xUnit test project.

## Start Here

- `AGENTS.md` contains always-on repository instructions for Codex.
- `docs/product-spec.md` is the product behavior source of truth.
- `docs/technical-design.md` is the architecture and ffmpeg export source of truth.
- `docs/codex-workflow.md` describes how Codex should work in this repo.
- `docs/implementation-kickoff.md` is the handoff checklist for the implementation session.
- `docs/project-file-notes.md` records VS Code/C# Dev Kit project-file decisions, including the design-time reference fallback for `VideoCutEditor.Core` diagnostics.

The repo-local skills in `.agents/skills` include the VideoCutEditor workflow skill and Microsoft WinUI skills from `microsoft/win-dev-skills`.

## Current Projects

- `src/VideoCutEditor` - WinUI 3 desktop app shell.
- `src/VideoCutEditor.Core` - testable settings, models, and service contracts.
- `tests/VideoCutEditor.Tests` - xUnit coverage for deterministic core behavior.

## Verify

```powershell
dotnet test VideoCutEditor.slnx
dotnet build src/VideoCutEditor/VideoCutEditor.csproj -p:Platform=x64
```

## Debug In VS Code

Open the repository root, then choose `VideoCutEditor: Debug x64` from Run and Debug before pressing F5. VS Code is pinned to `VideoCutEditor.slnx` through `.vscode/settings.json` so the C# language service opens the repo-standard solution. The WinUI app project also exposes a design-time-only `VideoCutEditor.Core` reference to keep Roslyn LSP diagnostics aligned with the normal project reference. This configuration runs the `build VideoCutEditor x64` task first, builds with `WindowsPackageType=None`, and launches the x64 Debug output directly so VS Code can attach breakpoints. The C# Dev Kit generated profile is kept as a fallback, but it can choose a mismatched platform folder on some machines.

If the build reports that `VideoCutEditor.exe` is locked, close the previous debugged app instance and press F5 again.

If VS Code still shows stale `VideoCutEditor.Core` or generated MVVM warnings after pulling changes, run `Developer: Reload Window` or restart the C# language server.

## Publish

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Publish-Portable.ps1 -Platform x64 -Configuration Release
powershell -ExecutionPolicy Bypass -File scripts\Publish-AllPortable.ps1 -Configuration Release
```

The portable publish output is written under `src\VideoCutEditor\bin\Release\...\publish`.
The WinUI app is built self-contained so packaged debug launches and portable output do not depend on a machine-wide .NET runtime probe.
`Publish-Portable.ps1` also runs `scripts\Test-PortablePublish.ps1` to verify that the output contains `VideoCutEditor.exe`, does not include sidecar runtime files, and does not bundle ffmpeg or ffprobe.
`Publish-AllPortable.ps1` runs the same publish and validation flow for x64, x86, and arm64.
