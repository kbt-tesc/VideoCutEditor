# Implementation Status

Last updated: 2026-07-09

This document is the handoff ledger for future Codex sessions. Read it after `AGENTS.md`, `docs/product-spec.md`, `docs/technical-design.md`, `docs/codex-workflow.md`, and `docs/implementation-kickoff.md` before choosing the next implementation slice.

## Current State

VideoCutEditor has a working WinUI 3/.NET 10 editor shell with a single-range video extraction workflow. The app can detect externally installed `ffmpeg` and `ffprobe`, probe media metadata, preview videos through Windows media playback when possible, set start/end markers, suggest re-encode bitrates from metadata, configure clip-edge fades, and export Fast copy or Re-encode through bitrate/target-size/quality rate control. Two-pass audio normalization is available as an option within both export modes, and Re-encode exposes an advanced additional ffmpeg arguments field with focused guardrails.

The project is being developed in small TDD slices. Keep using behavior-focused tests first, then implement the smallest production change, verify, and commit each slice.

## Completed Slices

- `9ac5d75 chore: add WinUI solution scaffold and repo guidance`
  - Created the WinUI 3/.NET 10 solution, app project, core project, and test project.
  - Added the initial product, technical, workflow, and kickoff docs.
- `f73ca5d feat(core): add media probing and export planning services`
  - Added settings persistence, ffmpeg/ffprobe path discovery, ffprobe parsing, media models, output naming, Fast copy planning, Re-encode planning, capability detection, and core tests.
- `e42f6de feat(app): add WinUI editor shell and export workflow`
  - Added the editor UI with video opening, ffmpeg/ffprobe pickers, output folder selection, media metadata display, export progress/log/cancel controls, timeline shell, drag/drop, preview loading, marker controls, keyboard shortcuts, and export execution wiring.
- `39a4f21 test: add core integration and WinUI UI coverage`
  - Added scripted WinUI UI tests and integration coverage around local ffmpeg/ffprobe-dependent flows.
- `092ff3b feat: enable re-encode mode selection`
  - Added Re-encode mode selection UI, codec/encoder/bitrate controls, export planner factory routing, settings persistence for last-used encode choices, and UI tests.
- `2b52887 fix: add right padding to export pane`
  - Fixed right-pane spacing so input controls do not overlap the vertical scrollbar.
- `d1aa517 feat: show predicted output size`
  - Added predicted output size calculation for bitrate-based Re-encode and exposed the read-only UI field.
- `fbcb7ca feat: add waveform generation service`
  - Extracted waveform command construction, cache path generation, and ffmpeg execution into tested core services.
  - Wired the WinUI timeline waveform image slot to the generated PNG cache.
- `4667a55 feat: suggest re-encode bitrate from media`
  - Added a tested core bitrate suggestion service using video stream bitrate, container bitrate, and resolution fallback defaults.
  - Wired suggestions into media load and codec-family changes while preserving saved or manually edited bitrate values.
- `891208a feat: add fade controls and filters`
  - Added video/audio fade-in and fade-out settings with duration persistence.
  - Added ffmpeg argument generation for `fade`/`afade`, AAC audio re-encode when audio fades are enabled, and automatic Re-encode routing whenever fades are enabled.
- `7b0eeb4 fix: normalize fade duration precision`
  - Set fade duration input changes to 0.25 second steps.
  - Truncated fade duration values to two decimal places before settings/export use.
- `8836e1d feat: add target size mode`
  - Added target-size rate control with MB persistence.
  - Added tested bitrate derivation from target size, duration, detected audio bitrate, and container overhead.
  - Wired target-size mode to pass the derived bitrate into the existing Re-encode planner.
- `ad06fe6 test: cover re-encode fade export integration`
  - Added an ffmpeg-backed integration test that generates a temporary audio/video source, runs Re-encode with video and audio fades through `FfmpegRunner`, probes the output, and verifies temporary output cleanup.
- `17f7cd7 fix: skip audio fade filters without audio`
  - Added metadata-aware Re-encode planning so audio fade filters and AAC audio re-encode are emitted only when media metadata shows an audio stream.
  - Passed probed media metadata from the WinUI export flow into the export planner.
  - Added planner and ffmpeg-backed integration coverage for video-only inputs with audio fade controls enabled.
- `45b54c2 feat: add quality rate control`
  - Added Quality rate control with a persisted numeric quality value.
  - Added Re-encode planner support for software `-crf` and NVEnc `-cq` quality arguments.
  - Added UI and UI smoke coverage for the Quality control.
  - Added an ffmpeg-backed integration test for software quality-mode Re-encode.
- `618cdd5 ui: simplify export mode controls`
  - Replaced the two-option export mode drop-down with direct Fast copy/Re-encode choices.
  - Hid codec, encoder, rate-control, predicted-size, and fade controls while Fast copy is selected.
  - Prevented hidden Fast copy fade settings from silently forcing Re-encode.
- `8f272ae feat: add audio normalization mode`
  - Added Normalize audio export mode with single-pass `loudnorm=I=-14:TP=-1.5:LRA=11`.
  - Stream-copies video and untouched streams while re-encoding filtered audio to AAC.
  - Added planner coverage for loudnorm arguments and no-audio rejection.
  - Added an ffmpeg-backed integration test for generated audio/video media.
  - Added the mode to the WinUI direct export choices.
- `refactor: make audio normalization an export setting`
  - Refactored Normalize audio from a third export mode into a checkbox setting available in both Fast copy and Re-encode.
  - Fast copy keeps video and untouched streams on `-c copy` while adding `loudnorm` and AAC audio re-encode when normalization is enabled.
  - Re-encode combines `loudnorm` with existing audio fade filters when both are enabled.
  - Added settings migration from the legacy `AudioNormalize` mode value to `FastCopy` plus `NormalizeAudio=true`.
- `feat: add re-encode additional ffmpeg arguments`
  - Added a Re-encode-only advanced ffmpeg arguments TextBox.
  - Added quote-aware argument parsing and planner tests so the field is appended as process arguments rather than shell text.
  - Persisted the additional arguments setting.
- `fix: validate additional ffmpeg arguments`
  - Added guardrails that reject app-managed input, range, codec, filter, and output-control ffmpeg options in the advanced field.
  - Added focused validator and planner tests for allowed options, blocked options, and inline option values.
- `chore: add portable publish workflow`
  - Updated Windows publish profiles for unpackaged, Windows App SDK self-contained, single-file portable output.
  - Added `scripts/Publish-Portable.ps1` as the repeatable publish entry point.
  - Added a custom `Program.Main` for unpackaged portable builds to set `MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY` before WinUI starts in single-file builds.
  - Made the WinUI app self-contained by default so packaged debug launch does not fail with a misleading machine-wide .NET runtime prompt.
  - Added tests that lock portable publish profile and self-contained project settings.
- `chore: validate portable publish artifacts`
  - Added `scripts/Test-PortablePublish.ps1` to verify portable publish output after publishing.
  - The validation requires `VideoCutEditor.exe`, rejects sidecar runtime/package files that should be inside the single-file EXE, and rejects bundled `ffmpeg`/`ffprobe`.
  - Added tests for accepted symbol-only sidecars, rejected runtime sidecars, rejected bundled tools, and the publish script validation hook.
- `feat: use two-pass audio normalization`
  - Changed audio normalization from single-pass loudnorm to a fixed-target two-pass loudnorm flow.
  - Added loudnorm analysis planning for Fast copy and Re-encode exports.
  - Added measured loudnorm replacement in `FfmpegRunner` and integration coverage with a fake ffmpeg process.
  - Kept loudness/true peak/LRA settings fixed and not user-configurable.
  - Tightened Re-encode additional ffmpeg argument validation only for obvious syntax mistakes: bare values and missing values for common value-taking options.
- `chore: add all-platform portable publish script`
  - Added `scripts/Publish-AllPortable.ps1` to run the validated portable publish flow for x64, x86, and arm64.
  - Added dry-run coverage for default platform selection and explicit platform selection.
  - Verified x86 and arm64 Release publishes with artifact validation.
- `chore: add verification sample media script`
  - Added `scripts/New-SampleMedia.ps1` to generate short local ffmpeg sample videos for manual waveform, no-audio, fade, and audio-normalization checks.
  - Added dry-run unit coverage so the script's representative output set is locked without requiring ffmpeg during the unit test.
  - Smoke-tested actual generation with the PATH-discovered winget ffmpeg into a temporary directory.
- `chore: add VS Code x64 debug configuration`
  - Added a repository VS Code launch configuration named `VideoCutEditor: Debug x64`.
  - Added a matching pre-launch task that builds `src\VideoCutEditor\VideoCutEditor.csproj` with `-p:Platform=x64`.
  - Kept the C# Dev Kit generated launch profile as a fallback and locked the expected VS Code configuration with a unit test.
- `fix: use unpackaged entry point for VS Code F5`
  - Updated the VS Code x64 pre-launch task to include `-p:WindowsPackageType=None` so direct F5 debugging uses the custom unpackaged `Program.Main`.
  - Documented that normal packaged WinUI launch validation should still use `BuildAndRun.ps1` or `winapp run`.
  - Recorded that stale debugged app instances can lock `VideoCutEditor.exe` and must be closed before rebuilding.
- `ui: refine Japanese messages and timeline controls`
  - Localized app-generated status, warning, information, and primary editor labels for Japanese users while keeping technical ffmpeg details available in logs.
  - Added an output-folder open button next to the output folder picker.
  - Reduced the start/end timeline marker lines to 1px.
  - Fixed `[` and `]` keyboard shortcuts by handling key events even when child controls mark them handled.
  - Added drag tracking on the timeline so the playhead follows pointer movement while dragging.

## Implemented Capabilities

- WinUI 3 editor shell with usable first screen.
- External `ffmpeg` and `ffprobe` path configuration with PATH discovery fallback.
- File picker-based executable and output folder selection.
- Drag and drop video opening, with confirmation when replacing an already opened video.
- Preview area using `MediaPlayerElement`.
- Custom timeline shell with playhead, selected range fill, start/end markers, zoom, horizontal scrolling, and waveform image slot.
- Waveform PNG generation through ffmpeg `showwavespic` into a temporary cache path.
- Start/end controls through `[` and `]` buttons plus keyboard shortcuts.
- Left/right arrow frame stepping using ffprobe frame rate when available, otherwise 30 fps.
- Slow playback rates through the speed selector.
- ffprobe metadata parsing and media summary display.
- Fast copy export planning and execution.
- Direct Fast copy/Re-encode mode selection without a drop-down.
- Re-encode export planning and execution for bitrate, target-size, and quality-based video encoding.
- Re-encode advanced additional ffmpeg arguments with quote-aware argument parsing, app-managed option validation, and obvious syntax-mistake validation.
- Two-pass audio normalization option with fixed `-14 LUFS` loudnorm and AAC audio re-encode in both Fast copy and Re-encode.
- Target-size mode that derives Re-encode video bitrate from desired output size.
- Quality mode that maps a single quality value to `-crf` for software encoders and `-cq` for NVEnc encoders.
- Clip-edge video/audio fade controls that force Re-encode and generate ffmpeg filters.
- Audio fade planning skips audio filters for probed video-only inputs instead of adding synthetic audio.
- Fade duration input with 0.25 second steps and two-decimal truncation.
- NVEnc/software encoder capability detection from `ffmpeg -encoders`.
- Export progress parsing, log display, cancellation, temporary output path, and final promotion after success.
- Output filename collision avoidance.
- Settings persistence for tool paths, output directory, export mode, normalize audio, codec, encoder, bitrate mode, video bitrate, and additional ffmpeg arguments.
- Re-encode video bitrate suggestions from source bitrate or resolution with codec-specific multipliers.
- Predicted output size display for bitrate-based Re-encode when enough information is available.
- Scripted WinUI UI smoke tests with screenshots.
- Portable x64/x86/arm64 publish profiles for unpackaged self-contained single-file output.
- Self-contained WinUI app builds for both packaged debug launch and portable publish.
- Automated portable publish artifact validation for single-file shape and external ffmpeg/ffprobe policy.
- All-platform portable publish script for x64, x86, and arm64.
- Verification sample media generator for repeatable local manual checks.
- VS Code F5 configuration for x64 Debug breakpoint debugging.
- VS Code F5 direct debugging uses the unpackaged entry point while packaged debug launch remains covered by `BuildAndRun.ps1` / `winapp run`.
- Japanese app-generated UI status/notice text for the main editor.
- Output-folder open button.
- Timeline drag seeking and 1px start/end marker lines.

## Current Verification Baseline

Most recent successful checks:

- `dotnet test VideoCutEditor.slnx`
  - 109 tests passed.
- `dotnet test VideoCutEditor.slnx --filter UserInterfaceSourceTests`
  - 2 tests passed after first confirming failures for the missing output-folder open button, drag handlers, 1px marker contract, handled keyboard hook, and Japanese message contract.
- `dotnet test VideoCutEditor.slnx --filter VsCodeDebugConfigurationTests`
  - 1 test passed after first confirming the test failed while the VS Code task lacked `-p:WindowsPackageType=None`.
- `dotnet build src\VideoCutEditor\VideoCutEditor.csproj -p:Platform=x64 -p:WindowsPackageType=None`
  - Build succeeded for the VS Code F5 direct-debug configuration.
- Direct launch smoke of `src\VideoCutEditor\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\VideoCutEditor.exe` after the unpackaged Debug build
  - The app remained running after 5 seconds, so the previous immediate `REGDB_E_CLASSNOTREG` failure was not reproduced; the smoke-test process was then stopped.
- `dotnet test VideoCutEditor.slnx --filter VerificationMediaScriptTests`
  - 1 test passed after first confirming the test failed before `scripts/New-SampleMedia.ps1` existed.
- `powershell -ExecutionPolicy Bypass -File scripts\New-SampleMedia.ps1 -OutputDirectory "$env:TEMP\VideoCutEditor-SampleMedia-Smoke" -DurationSeconds 1 -Force`
  - Generated `video-with-audio.mp4`, `video-only.mp4`, and `quiet-audio.mp4` successfully with the winget-provided ffmpeg found on PATH.
- `dotnet build src/VideoCutEditor/VideoCutEditor.csproj -p:Platform=x64`
  - Build succeeded.
- `powershell -ExecutionPolicy Bypass -File tests\ui-tests.ps1 -AppPid <pid>`
  - 56 UI tests passed for packaged Debug launch after adding `OpenOutputDirectoryButton` coverage.
- `powershell -ExecutionPolicy Bypass -File scripts\Publish-Portable.ps1 -Platform x64 -Configuration Release`
  - Publish succeeded, artifact validation passed, and `VideoCutEditor.exe` was produced under `src\VideoCutEditor\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish`.
- `powershell -ExecutionPolicy Bypass -File scripts\Publish-AllPortable.ps1 -Platforms x86,arm64 -Configuration Release`
  - x86 and arm64 Release publishes succeeded with artifact validation.
- `powershell -ExecutionPolicy Bypass -File tests\ui-tests.ps1 -AppPid <pid>`
  - 55 UI tests passed for packaged Debug launch.
  - 55 UI tests passed for the published unpackaged x64 EXE.

When resuming in a new session, rerun the relevant subset before making assumptions if files have changed.

## Known Gaps

- Waveform generation is implemented in code, and repeatable generated sample media is now available. It should still be manually verified with representative real videos that have audio streams.
- Fade controls and fade-triggered Re-encode are covered by generated audio/video integration tests and can be exercised with generated sample media, but should still be manually verified on representative real media.
- Audio fade behavior for generated video-only inputs is covered, and `scripts/New-SampleMedia.ps1` can generate a local `video-only.mp4`; still verify representative real video-only media manually.
- Advanced ffmpeg arguments are implemented for Re-encode mode only. They are quote-parsed, reject app-managed options, catch obvious syntax mistakes, and are passed as argument-list entries. Full ffmpeg option compatibility validation is intentionally not implemented because support varies by ffmpeg build, encoder, and muxer.
- Audio normalization now uses fixed-target two-pass loudnorm. Configurable loudness/true peak/LRA values are intentionally not implemented.
- Audio normalization is covered for generated audio/video media and fake-process two-pass argument replacement; the sample generator can produce quiet and no-audio inputs for manual checks, but representative real media and no-audio UI error handling still need manual verification.
- Quality mode is covered for generated software-encoded media; NVEnc quality-mode execution still needs manual hardware-backed verification.
- Real media export should still be manually verified for Fast copy and Re-encode on local sample files.
- NVEnc behavior should be manually verified on hardware and ffmpeg builds that expose NVEnc.
- Preview-unavailable fallback behavior needs more manual and/or UI coverage.
- Portable x64 publish, x86 publish, arm64 publish, artifact validation, and published x64 EXE startup smoke testing now succeed. Signing, MSIX packaging, installer validation, distribution packaging, and x86/arm64 runtime startup on matching devices still need verification.
- UI tests currently cover presence and defaults more than full user workflows with real picker interactions and export completion.
- VS Code F5 now has an explicit x64 unpackaged launch path, but the user should manually confirm breakpoint attachment from VS Code because automated tests can only validate the configuration files and build output.
- The `[` and `]` shortcut fix is covered by source-level UI contract tests and should still be manually confirmed in the running app with a loaded preview.
- Timeline drag seeking is covered by source-level UI contract tests and should still be manually confirmed visually with real media.

## Recommended Next Slices

1. Continue end-to-end verification.
   - Generate local verification inputs with `scripts/New-SampleMedia.ps1` when real sample media is not available.
   - Add scripted picker workflow coverage where reliable.
   - Add manual verification notes for real preview/export/NVEnc/package runs.
2. Deepen audio normalization verification.
   - Manually verify representative real media and no-audio media.
   - Consider configurable loudness/true peak/LRA only if real users need targets other than `-14 LUFS`.
3. Continue packaging and release preparation.
   - Use the repo-local `winui-packaging` skill.
   - Verify x86/ARM64 published EXE startup on matching devices when available.
   - Add signing/MSIX packaging when distribution format is chosen.

## Resume Checklist

1. Start from `C:\Users\owner\Documents\VideoCutEditor`.
2. Read `AGENTS.md` and all docs it lists, including this file.
3. Check `git status --short`; do not overwrite uncommitted user changes.
4. Check the latest commits with `git log --oneline --max-count=10`.
5. Pick one small slice from `Recommended Next Slices` or the user's newest request.
6. Write or update a failing test first where practical.
7. Implement narrowly.
8. Run relevant tests/build/UI checks.
9. Always update product/design/workflow/status docs before committing. Record completed work, changed verification baseline, remaining tasks, discovered bugs, validation gaps, technical debt, and required follow-ups.
10. Commit the completed slice with a focused message.
