# VideoCutEditor Technical Design

## Platform

- Build a WinUI 3 desktop app in C# on .NET 10.
- Use Windows App SDK with unpackaged, self-contained, single-file deployment as the preferred packaging target.
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

Prefer testable command-generation code that does not require launching ffmpeg.

## Media Probing

Use ffprobe to collect:

- Duration
- Container format
- Stream list
- Video codec, dimensions, frame rate, bitrate
- Audio codec, channel layout, sample rate, bitrate
- Existing metadata where practical

If ffprobe cannot return a source bitrate, fall back to stream-level bitrate or a resolution-based default for UI initialization.

The initial media probing implementation runs `ffprobe -v error -show_format -show_streams -print_format json <source>` through `ProcessStartInfo.ArgumentList`, parses format and stream metadata in testable code, and uses the primary video stream frame rate for one-frame keyboard stepping when available.

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

The initial re-encode planner builds command arguments for bitrate-based video re-encode. It selects the user-requested codec family and encoder preference from settings, uses the detected capability list to choose the concrete encoder, maps all streams, defaults non-video streams to copy with `-c copy`, overrides video with `-c:v <encoder> -b:v <kbps>k`, preserves metadata, and writes through a temporary output path before promotion. The WinUI shell exposes Fast copy/Re-encode mode, codec family, encoder preference, and video bitrate controls. The view model stores the last-used choices and routes export planning through `ExportPlannerFactory` so Fast copy remains the default while Re-encode uses the detected capability list.

Expose a simple settings surface:

- Codec family
- Encoder type, with NVEnc preferred where available
- Bitrate mode as the default
- Target size mode
- Quality mode
- Additional ffmpeg arguments for advanced users

## Bitrate And Predicted Size

The default re-encode mode is bitrate-based so the app can estimate output size.

Initial bitrate suggestions:

- H.264: source video bitrate when available.
- H.265: about 70% of source video bitrate.
- AV1: about 55% of source video bitrate.

Predicted size:

`estimated bytes = ((video bitrate + audio bitrate) * selected duration seconds / 8) + container overhead`

Use a small overhead allowance for display. Treat the value as an estimate, not a guarantee.

Target size mode should invert this calculation to suggest a video bitrate after reserving expected audio bitrate and overhead.

The initial UI displays a read-only predicted output size for bitrate-based re-encode settings when media metadata, selected range duration, and a valid video bitrate are available. The calculation includes selected video bitrate, detected audio bitrate when present, and a small container overhead allowance.

## Fade Policy

Fade controls apply only to the clip edges:

- Video fade-in from black at clip start.
- Video fade-out to black at clip end.
- Audio fade-in from silence at clip start.
- Audio fade-out to silence at clip end.

Any fade requiring filters forces re-encoding of the affected stream.

Audio policy:

- If no audio processing is needed, preserve audio with stream copy where practical.
- If audio fade is enabled, re-encode audio to AAC.
- Preserve channel layout and sample rate where practical.

## Stream And Metadata Preservation

- Preserve all input streams and metadata where practical in fast-copy mode.
- In re-encode mode, preserve non-processed streams where compatible with the output container.
- If a stream cannot be preserved, surface the reason in the export summary or log.

## Process Execution

- Run ffmpeg as a child process with arguments passed as an argument list, not string-concatenated shell commands.
- Capture stderr/stdout logs.
- Use ffmpeg progress reporting when practical, or parse timestamp progress from stderr as a fallback.
- Cancellation must terminate the ffmpeg process and leave no partial file promoted as a successful export.
- Write to a temporary output path first, then move/rename to the final output path after successful completion.

Waveform previews are generated by invoking the configured or detected ffmpeg with the `showwavespic` filter into a temporary PNG cache. This preview is visual-only and does not add audio waveform editing.

## Packaging

- Prefer unpackaged Windows App SDK deployment.
- Publish as a self-contained single-file EXE for Windows.
- Do not bundle ffmpeg in the first implementation.
- Document any runtime prerequisites discovered during WinUI 3 packaging work.
