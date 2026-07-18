# VideoCutEditor Technical Design

## Platform

- Build a WinUI 3 desktop app in C# on .NET 10.
- Use Windows App SDK with unpackaged, self-contained, single-file deployment as the preferred packaging target.
- Build the WinUI app as self-contained by default, including packaged debug launches, so app-local host files and `runtimeconfig.json` stay consistent.
- Store settings under `%AppData%/VideoCutEditor/settings.json`.
- Keep ffmpeg and ffprobe external. The user can configure explicit paths; PATH lookup is a fallback.

## App Structure

Use a small MVVM-style structure:

- View models own UI state and commands.
- `SettingsService` loads and saves app settings.
- `MediaProbeService` runs ffprobe and converts metadata into app models.
- `FfmpegCapabilityService` detects available encoders and encoder options.
- `ExportService` builds export plans and ffmpeg arguments.
- `FfmpegRunner` starts ffmpeg, parses progress, captures logs, and supports cancellation.
- `ExportClip` captures one registered range and its output title. `ClipTitleService` normalizes titles and avoids collisions with the current registration list and existing destination MP4 files.

Prefer testable command-generation code that does not require launching ffmpeg.

Registered clips are kept in memory for the currently opened source video. The view model exports them sequentially using the same export settings and planner selection, creating one independent `<title>.mp4` plan per item. Opening another source clears the registration list. When the list is empty, the existing single-range export path remains available for compatibility.

Explicit clip titles are normalized before matching, including trimming and optional `.mp4` removal. A match is treated as an edit target rather than passed through collision suffixing. `MainPageViewModel` stores the pending existing item and replacement range, raises a confirmation request, and replaces the immutable `ExportClip` in the observable collection only after the main-page `ContentDialog` returns approval. Canceling leaves both the original list item and current edit controls unchanged.

Batch export snapshots the registration list before starting, checks every destination for an existing file before launching ffmpeg, and disables list edits while processing. Progress for each plan is mapped into one overall progress value. Cancellation or the first failed plan stops later items; already completed independent files remain in the output directory.

## Settings Persistence

`JsonSettingsService` stores settings in `%AppData%/VideoCutEditor/settings.json`. Saving first serializes to a unique temporary file in the same directory, flushes and closes it, then replaces the destination with a same-volume move. Cancellation or failure removes the temporary file and leaves the previous settings file unchanged.

Invalid or empty JSON is moved to a timestamped `settings.corrupt.*.json` file before defaults are returned. I/O or access failures return defaults and attempt to write a timestamped `settings.load-error.*.txt` diagnostic without allowing the diagnostic write itself to prevent startup. `MainPageViewModel.InitializeAsync` also has a defensive fallback for unexpected `ISettingsService` failures.

App-layer behavior tests live in `tests/VideoCutEditor.App.Tests`. The project targets the Windows TFM and x64 runtime, references the WinUI app with Windows App SDK bootstrap/deployment initialization disabled, and exercises `MainPageViewModel` without creating XAML windows. Core-only tests remain in `tests/VideoCutEditor.Tests`.

## Media Probing

Use ffprobe to collect:

- Duration
- Container format
- Stream list
- Video codec, dimensions, frame rate, bitrate
- Video color metadata (`color_space`, `color_transfer`, `color_primaries`) for HDR detection
- Audio codec, channel layout, sample rate, bitrate
- Existing metadata where practical

If ffprobe cannot return a source bitrate, fall back to stream-level bitrate or a resolution-based default for UI initialization.

The initial media probing implementation runs `ffprobe -v error -show_format -show_streams -print_format json <source>` through `ProcessStartInfo.ArgumentList`, parses format and stream metadata in testable code, and uses the primary video stream frame rate for one-frame keyboard stepping when available.

Encoder information, media information, and export logs are displayed by one modeless `InfoWindow`. The main page retains the window instance while it is open, activates that instance on repeated INFO commands, and releases it after `Closed`. The secondary HWND sets the main HWND as its native owner, preserving normal owner stacking and minimize behavior without making the window modal. The window binds to the same `MainPageViewModel` as the editor so export progress and logs continue updating without blocking the main window. `MediaSummaryFormatter` labels `smpte2084` as `HDR10 (PQ)`, labels `arib-std-b67` as `HLG`, and displays raw ffprobe color space, transfer, and primaries values for diagnosis. Unloading the main page closes the secondary window to keep application lifetime predictable.

Registered ranges are displayed by a separate modeless `ExportListWindow` owned by the main HWND. The first registration opens it automatically, and the main page can reopen the retained list while registrations exist. Start and end columns use fixed widths; the title column uses the remaining width with stretched `ListViewItem` content. Each row provides icon edit and delete actions, and the window binds to the same in-memory collection as `MainPageViewModel`. Edit restores the selected item's range/title through the view model and activates the main owner window.

The registration input belongs to the timeline workflow rather than the export-settings panel. The compact title field, add command, and list command sit directly after the start/end marker buttons so selecting a range and registering it remain one local operation.

HDR detection treats a video stream as HDR when ffprobe reports `color_transfer=smpte2084` (HDR10/PQ) or `color_transfer=arib-std-b67` (HLG). `color_space` and `color_primaries` are retained for display/debugging and future refinement, but transfer characteristics drive the current HDR decision to avoid treating BT.2020 SDR as HDR.

## Export Modes

### Fast Copy

Fast copy is the default. It should avoid re-encoding.

Command policy:

- Select the requested time range with `-ss` and duration or end-time derived from the user range.
- Map all streams where practical with `-map 0`.
- Copy all codecs with `-c copy`.
- Preserve metadata with `-map_metadata 0` where supported.
- Use timestamp normalization such as `-avoid_negative_ts make_zero` when needed.

The UI must communicate that stream-copy cuts may align to keyframes and can be slightly imprecise.

The first implemented export path is Fast Copy. It builds the ffmpeg argument list in testable code, writes to a unique temporary output path in the destination folder, and moves the file to the final generated path only after ffmpeg exits successfully.

Fast Copy export cancellation is wired through a `CancellationToken` from the UI to `FfmpegRunner`. Canceling terminates the ffmpeg process tree, deletes the temporary output path, and reports the export as canceled rather than promoting a partial file.

### Re-encode

Re-encode is selected explicitly or forced by filters such as fade.

Supported video codec families:

- H.264
- H.265/HEVC
- AV1

Encoder preference:

1. Prefer NVEnc when a matching ffmpeg encoder is available:
   - `h264_nvenc`
   - `hevc_nvenc`
   - `av1_nvenc`
2. Fall back to software encoders when NVEnc is unavailable.
3. Query `ffmpeg -encoders` and, for NVEnc tuning, `ffmpeg -h encoder=<name>` before using optional encoder-specific arguments.

The initial capability detector runs `ffmpeg -hide_banner -encoders`, parses encoder names, reports NVEnc availability by codec family, and chooses an automatic video encoder by preferring NVEnc before supported software encoders (`libx264`, `libx265`, `libsvtav1`, `libaom-av1`).

Tool setup normally selects one directory. `FfmpegToolPathService.ResolveDirectory` checks that directory directly for `ffmpeg.exe` and `ffprobe.exe`; when both are present, the settings UI stores both full paths and keeps individual selectors hidden. If either is absent, it preserves the path that was found and reveals the existing independent executable pickers. Startup still prefers valid saved full paths and then PATH discovery; saved paths in different directories open in the individual-selector state.

The initial re-encode planner builds command arguments for bitrate-based video re-encode. It selects the user-requested codec family and encoder preference from settings, uses the detected capability list to choose the concrete encoder, maps all streams, defaults non-video streams to copy with `-c copy`, overrides video with `-c:v <encoder> -b:v <kbps>k`, preserves metadata, and writes through a temporary output path before promotion. The WinUI shell exposes Fast copy/Re-encode mode, codec family, encoder preference, and video bitrate controls. The view model stores the last-used choices and routes export planning through `ExportPlannerFactory` so Fast copy remains the default while Re-encode uses the detected capability list.

The WinUI shell presents Fast copy and Re-encode as direct mode choices. Re-encode-only settings are hidden while Fast copy is selected. Hidden fade controls are not applied in Fast copy mode, so invisible settings cannot silently force a re-encode.

Expose a simple settings surface:

- Codec family
- Encoder type, with NVEnc preferred where available
- Bitrate mode as the default
- Target size mode
- Quality mode
- Additional ffmpeg arguments for advanced users

Additional ffmpeg arguments are stored as a user-entered string for Re-encode mode, parsed into an argument list with whitespace splitting and quote handling, validated against app-managed options, and appended after generated encode/filter/metadata/timestamp options but before the output path. The app must continue passing ffmpeg arguments through `ProcessStartInfo.ArgumentList`; it must not concatenate the advanced field into a shell command. Malformed quotes, blocked options, bare values that are not attached to an option, and missing values for common value-taking options fail before process launch with a recoverable error. The validator intentionally avoids complete ffmpeg option compatibility validation because option support varies by ffmpeg build, encoder, and muxer.

Blocked additional options include app-managed input/range/output-control options (`-i`, `-ss`, `-t`, `-to`, `-y`, `-n`, `-nostdin`, `-map`, `-map_metadata`, `-avoid_negative_ts`) and generated codec/rate/filter options (`-c`, `-codec`, `-c:v`, `-codec:v`, `-c:a`, `-codec:a`, `-b:v`, `-crf`, `-cq`, `-vf`, `-filter:v`, `-af`, `-filter:a`). Future work can add more nuanced option validation if the advanced field expands beyond Re-encode tuning.

## HDR To SDR Conversion

HDR to SDR conversion is a Re-encode-only video filter option. Fast Copy never applies this conversion because stream copy must not decode or re-encode video; when HDR media is selected in Fast Copy mode, the UI only shows an informational notice that the output will remain HDR.

When Re-encode mode is selected for HDR media, the WinUI shell shows an `HDRをSDRに変換` checkbox and defaults it to checked for the newly opened HDR media. The setting is included in `AppSettings` so command planning remains testable and settings JSON can round-trip it, but the UI only applies it when the current export mode is Re-encode and the current probed media has an HDR video stream.

The initial conversion filter is appended to the generated `-vf` chain before clip-edge fade filters:

`zscale=t=linear:npl=100,format=gbrpf32le,tonemap=tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=tv,format=yuv420p`

This produces BT.709 SDR output suitable for common SDR displays. The first implementation relies on the user's ffmpeg build having the required `zscale` and `tonemap` filters; if export fails because those filters are unavailable, the existing ffmpeg failure path surfaces the stderr log. Future work can add explicit filter capability detection and tone-mapping presets if real media verification shows a need.

## Bitrate And Predicted Size

The default re-encode mode is bitrate-based so the app can estimate output size.

Initial bitrate suggestions:

- H.264: source video bitrate when available.
- H.265: about 70% of source video bitrate.
- AV1: about 55% of source video bitrate.

`BitrateSuggestionService` owns this logic in core code. It uses the primary video stream bitrate first, then the container bitrate, then a resolution fallback. Resolution fallback uses H.264-equivalent defaults before applying codec multipliers:

| Resolution floor | H.264 base |
| --- | ---: |
| 3840x2160 | 12000 kbps |
| 2560x1440 | 8000 kbps |
| 1920x1080 | 5000 kbps |
| 1280x720 | 2500 kbps |
| Lower known resolution | 1200 kbps |
| Sparse metadata | 2500 kbps |

The WinUI view model applies suggestions when media metadata is loaded or the codec family changes, until the user manually edits or saves a bitrate. A saved bitrate is treated as an explicit user preference and is not overwritten by metadata suggestions.

Predicted size:

`estimated bytes = ((video bitrate + audio bitrate) * selected duration seconds / 8) + container overhead`

Use a small overhead allowance for display. Treat the value as an estimate, not a guarantee.

Target size mode inverts this calculation to derive a video bitrate after reserving detected audio bitrate and overhead. The UI stores target size in MB, updates the derived video bitrate when enough media/range information is available, and passes the derived bitrate to the existing re-encode planner.

Quality mode stores a single numeric quality value. Lower values mean higher quality. Software encoders use `-crf <value>` and NVEnc encoders use `-cq <value>`. The initial UI range is 0 to 51 with a default of 23. Because quality mode does not target a specific bitrate, predicted output size remains unavailable in this mode.

The initial UI displays a read-only predicted output size for bitrate-based re-encode settings when media metadata, selected range duration, and a valid video bitrate are available. The calculation includes selected video bitrate, detected audio bitrate when present, and a small container overhead allowance.

## Fade Policy

Fade controls apply only to the clip edges:

- Video fade-in from black at clip start.
- Video fade-out to black at clip end.
- Audio fade-in from silence at clip start.
- Audio fade-out to silence at clip end.

Any fade requiring filters forces re-encoding of the affected stream.

The implemented planner keeps the selected clip range first, then applies fade filters relative to the trimmed clip timeline. Video fades use `-vf fade=t=in/out`, audio fades use `-af afade=t=in/out`, and audio fades override audio copying with `-c:a aac`. Fade duration is adjusted in 0.25 second UI steps, stored in settings, truncated to two decimal places, and clamped to the selected clip duration during command construction.

Audio policy:

- If no audio processing is needed, preserve audio with stream copy where practical.
- If audio fade is enabled and media metadata confirms an audio stream exists, re-encode audio to AAC.
- If media metadata confirms there is no audio stream, omit audio fade filters and do not add an audio stream. If metadata is unavailable, keep the previous conservative behavior and assume audio may exist.
- Preserve channel layout and sample rate where practical.

## Audio Normalization

Audio normalization is an export setting available in both Fast Copy and Re-encode for changing loudness. The implementation uses a fixed-target two-pass `loudnorm` workflow:

`loudnorm=I=-14:TP=-1.5:LRA=11`

The first pass runs against the selected time range with `loudnorm=I=-14:TP=-1.5:LRA=11:print_format=json`, no video/subtitle/data output, and a null muxer. `FfmpegRunner` parses the loudnorm JSON from stderr, then replaces the generated loudnorm filter in the final export with measured values (`measured_I`, `measured_TP`, `measured_LRA`, `measured_thresh`, `offset`, `linear=true`). The loudness target, true peak, and LRA values are not user-configurable in this implementation.

Fast Copy command policy when normalize audio is enabled:

- Analyze the requested time range first, then run the final export with measured loudnorm values.
- Select the requested time range with `-ss` and duration.
- Map all streams where practical with `-map 0`.
- Default to `-c copy` so video and untouched streams are stream-copied.
- Apply `-af loudnorm=I=-14:TP=-1.5:LRA=11`.
- Override audio with `-c:a aac` because audio filters require decoding and encoding.
- Preserve metadata with `-map_metadata 0` where supported.
- Use temporary output promotion and cancellation handling through the common `FfmpegRunner` path.

Re-encode command policy when normalize audio is enabled:

- Analyze the requested time range first, then run the final export with measured loudnorm values.
- Keep the selected video codec, encoder, and rate-control arguments.
- Add the measured `loudnorm` audio filter and override audio with `-c:a aac`.
- If clip-edge audio fades are also enabled, combine `loudnorm` and `afade` in a single `-af` chain.

If media metadata confirms there is no audio stream, the planner rejects the export before starting ffmpeg with the Japanese error `音声ストリームがないため、音量正規化を使用できません`. If metadata is unavailable, the planner assumes audio may exist and lets the loudnorm analysis pass report any process-level failure. Future work may add configurable true peak/loudness range and codec-preserving audio encode choices.

## Stream And Metadata Preservation

- Preserve all input streams and metadata where practical in fast-copy mode.
- In re-encode mode, preserve non-processed streams where compatible with the output container.
- If a stream cannot be preserved, surface the reason in the export summary or log.

## Output Path Selection

`OutputPathService` creates automatic default output names from the source stem using `<source>_cut<extension>`. When that default path already exists, it probes one-based suffixes in order: `<source>_cut_1<extension>`, `<source>_cut_2<extension>`, and so on.

Manual output filename edits are owned by `MainPageViewModel`. The view model keeps the typed filename, rebuilds the planned output path from the configured output directory and source extension, and exposes `IsManualOutputFileNameCollision` when the manual destination already exists. The warning is informational and non-blocking; `FfmpegRunner` remains the final overwrite guard by refusing to promote the temporary export output when the final path exists.

Registered clip paths are built from the configured output directory and the collision-safe title as `<title>.mp4`. A second preflight immediately before a batch starts catches files created after registration so no batch begins with a known destination collision.

## Process Execution

- Run ffmpeg as a child process with arguments passed as an argument list, not string-concatenated shell commands.
- Capture stderr/stdout logs.
- Use ffmpeg progress reporting when practical, or parse timestamp progress from stderr as a fallback.
- Cancellation must terminate the ffmpeg process and leave no partial file promoted as a successful export.
- Write to a temporary output path first, then move/rename to the final output path after successful completion.
- The view model accepts progress only while the active runner operation is pending. Queued progress callbacks that arrive after success, failure, or cancellation are ignored so they cannot overwrite the final status or completion value.

Waveform previews are generated by invoking the configured or detected ffmpeg with the `showwavespic` filter into a temporary PNG cache. `WaveformPlanner` builds the ffmpeg argument list in testable core code, `WaveformGenerator` runs the process with cancellation support, and the WinUI timeline binds the generated PNG into the waveform image slot. This preview is visual-only and does not add audio waveform editing.

The timeline ruler accumulates fractional minor-tick intervals for positioning. Before formatting a major-tick label, round its elapsed seconds to six decimal places so values such as `1.999999999` display as `0:02` instead of being truncated to `0:01`. Keep the unrounded value for pixel positioning.

## Test Architecture

Deterministic Core tests run without external tools. Environment-dependent ffmpeg, ffprobe, encoder, and Windows PowerShell tests use `SkippableFact`; an unavailable prerequisite must be reported as an explicit skipped test with a reason, never as a passing test reached through an early `return`.

End-to-end export UI verification uses `scripts/Test-ExportUi.ps1` with `-Mode FastCopy`, `-Mode Reencode`, `-Mode ReencodeHdrToSdr`, the NVEnc bitrate/quality modes, `-Mode NormalizeAudio`, or `-Mode NormalizeNoAudio`. The script generates temporary sample media, writes temporary settings and output directories, launches the unpackaged Debug x64 app, runs the picker and export UI workflow, and removes the process and temporary tree in `finally`. Re-encode verification fixes H.264 Software encoding at 1500 kbps so it does not depend on GPU availability. `ReencodeHdrToSdr` uses generated 10-bit BT.2020/PQ media, verifies the contextual conversion option is checked, completes the real `zscale`/`tonemap` export, and requires ffprobe to report BT.709 color space, transfer, and primaries on the output. NVEnc modes first require the matching `h264_nvenc`, `hevc_nvenc`, or `av1_nvenc` encoder in the selected ffmpeg build. Bitrate modes verify the matching codec with NVEnc at 1500 kbps; Quality modes use the isolated setting fixed at 23 and verify the visible mode, enabled quality control, completed export, and non-empty output. NormalizeAudio uses the quiet audio sample, keeps Fast copy selected, and verifies that the live export log records both loudness analysis and normalization application passes. NormalizeNoAudio uses video-only media and verifies the localized rejection plus absence of an output file. The app honors `VIDEOCUTEDITOR_TEST_SETTINGS_DIRECTORY` only in Debug builds; Release builds always use the normal AppData settings location.

`WaveformGenerator` process behavior is exercised with a fake Windows PowerShell process for success, nonzero exit, successful exit without output, cancellation, and partial-output cleanup. Real ffmpeg integration tests continue covering generated media and export/probe behavior when the detected local build provides the required tools and encoders.

## Packaging

- Prefer unpackaged Windows App SDK deployment.
- Publish as a self-contained single-file EXE for Windows.
- Build the public x64 installer with NSIS using `RequestExecutionLevel user` and `%LocalAppData%/Programs/VideoCutEditor` so installation and removal are scoped to the current user. NSIS is selected over Inno Setup because the NSIS zlib/libpng license explicitly permits commercial use and better matches the permissive MIT source distribution.
- Do not bundle ffmpeg in the first implementation.
- Document any runtime prerequisites discovered during WinUI 3 packaging work.

Portable publish uses the `win-x64`, `win-x86`, and `win-arm64` publish profiles plus `scripts/Publish-Portable.ps1`. These profiles set `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `SelfContained=true`, `PublishSingleFile=true`, and `IncludeNativeLibrariesForSelfExtract=true`. The app defines its own `Program.Main` only for unpackaged portable builds so single-file output can set `MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY` before WinUI starts, as required by the Windows App SDK. Normal packaged debug launches keep the generated WinUI entry point.

`scripts/Test-PortablePublish.ps1` validates portable publish output after publish. It requires `VideoCutEditor.exe`, rejects sidecar runtime/package files such as `.dll`, `.json`, `.xbf`, `.pri`, `.winmd`, `.appx`, and `.msix`, rejects unexpected extra executables, and rejects bundled `ffmpeg` or `ffprobe` so external-tool configuration remains explicit.

`scripts/Publish-AllPortable.ps1` runs the validated portable publish flow across x64, x86, and arm64. It supports a `-WhatIf` dry run for CI/script checks and accepts either multiple platform arguments or comma-separated platform names.

`scripts/New-PortableRelease.ps1` creates an installer-free distribution ZIP from a validated portable publish. Release names follow `VideoCutEditor-<version>-win-<platform>.zip`; each ZIP contains `VideoCutEditor.exe`, a user-only Japanese `README.md`, the app MIT license, and applicable official dependency licenses/notices, and is accompanied by a `.zip.sha256` file.

`scripts/New-InstallerRelease.ps1` reuses the validated x64 portable publish and compiles `installer/VideoCutEditor.nsi` with NSIS 3. Release names follow `VideoCutEditor-<version>-win-x64-setup.exe` with a matching SHA-256 file. The installer uses stable per-user registry keys for upgrades, presents the official Windows App SDK terms, installs the EXE/readme/licenses under the current user's LocalAppData, registers a Start menu shortcut and uninstall entry, and does not bundle ffmpeg/ffprobe. The current release line is `0.3.0`. Public artifacts remain unsigned until a trusted production code-signing certificate is available.

The repository root `LICENSE` uses the standard SPDX MIT text for VideoCutEditor-owned source. Unmodified official license and notice documents for the self-contained .NET runtime, Windows App SDK, CommunityToolkit.Mvvm, and vendored Microsoft WinUI development skills are retained under `third-party`. Runtime-related files are copied into application distributions; development-skill notices remain source-repository-only.

The owner-supplied ChatGPT Image icon source is retained under `design`. `scripts/New-AppIconAssets.ps1` uses ImageMagick to remove the source's rendered checkerboard with a rounded alpha mask, restore transparent padding, create the multi-size ICO, and generate every PNG size referenced by the WinUI package manifest. The app project sets `ApplicationIcon` so the unpackaged single-file EXE also embeds the product icon.
