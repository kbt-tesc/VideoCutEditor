# Implementation Status

Last updated: 2026-07-05

This document is the handoff ledger for future Codex sessions. Read it after `AGENTS.md`, `docs/product-spec.md`, `docs/technical-design.md`, `docs/codex-workflow.md`, and `docs/implementation-kickoff.md` before choosing the next implementation slice.

## Current State

VideoCutEditor has a working WinUI 3/.NET 10 editor shell with a single-range video extraction workflow. The app can detect externally installed `ffmpeg` and `ffprobe`, probe media metadata, preview videos through Windows media playback when possible, set start/end markers, suggest re-encode bitrates from metadata, configure clip-edge fades, and export Fast copy or Re-encode through bitrate, target-size, or quality rate control.

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
- `feat: add quality rate control`
  - Added Quality rate control with a persisted numeric quality value.
  - Added Re-encode planner support for software `-crf` and NVEnc `-cq` quality arguments.
  - Added UI and UI smoke coverage for the Quality control.
  - Added an ffmpeg-backed integration test for software quality-mode Re-encode.

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
- Re-encode export planning and execution for bitrate, target-size, and quality-based video encoding.
- Target-size mode that derives Re-encode video bitrate from desired output size.
- Quality mode that maps a single quality value to `-crf` for software encoders and `-cq` for NVEnc encoders.
- Clip-edge video/audio fade controls that force Re-encode and generate ffmpeg filters.
- Audio fade planning skips audio filters for probed video-only inputs instead of adding synthetic audio.
- Fade duration input with 0.25 second steps and two-decimal truncation.
- NVEnc/software encoder capability detection from `ffmpeg -encoders`.
- Export progress parsing, log display, cancellation, temporary output path, and final promotion after success.
- Output filename collision avoidance.
- Settings persistence for tool paths, output directory, export mode, codec, encoder, bitrate mode, and video bitrate.
- Re-encode video bitrate suggestions from source bitrate or resolution with codec-specific multipliers.
- Predicted output size display for bitrate-based Re-encode when enough information is available.
- Scripted WinUI UI smoke tests with screenshots.

## Current Verification Baseline

Most recent successful checks:

- `dotnet test VideoCutEditor.slnx`
  - 61 tests passed.
- `dotnet build src/VideoCutEditor/VideoCutEditor.csproj -p:Platform=x64`
  - Build succeeded.
- `powershell -ExecutionPolicy Bypass -File tests\ui-tests.ps1 -AppPid <pid>`
  - 57 UI tests passed.

When resuming in a new session, rerun the relevant subset before making assumptions if files have changed.

## Known Gaps

- Waveform generation is implemented in code, but should still be manually verified with real videos that have audio streams.
- Fade controls and fade-triggered Re-encode are covered by generated audio/video integration tests, but should still be manually verified on representative real media.
- Audio fade behavior for generated video-only inputs is covered; still verify representative real video-only media manually.
- Advanced ffmpeg arguments are not implemented.
- Quality mode is covered for generated software-encoded media; NVEnc quality-mode execution still needs manual hardware-backed verification.
- Real media export should still be manually verified for Fast copy and Re-encode on local sample files.
- NVEnc behavior should be manually verified on hardware and ffmpeg builds that expose NVEnc.
- Preview-unavailable fallback behavior needs more manual and/or UI coverage.
- Packaging, signing, portable/self-contained publish, and installer validation are not done.
- UI tests currently cover presence and defaults more than full user workflows with real picker interactions and export completion.

## Recommended Next Slices

1. Continue end-to-end verification.
   - Add scripted picker workflow coverage where reliable.
   - Add manual verification notes for real preview/export/NVEnc/package runs.
2. Packaging and release preparation.
   - Use the repo-local `winui-packaging` skill.
   - Verify unpackaged/self-contained/single-file assumptions against the current Windows App SDK behavior.

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
