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

The WinUI shell presents Fast copy and Re-encode as direct mode choices. Re-encode-only settings are hidden while Fast copy is selected. Hidden fade controls are not applied in Fast copy mode, so invisible settings cannot silently force a re-encode.

Expose a simple settings surface:

- Codec family
- Encoder type, with NVEnc preferred where available
- Bitrate mode as the default
- Target size mode
- Quality mode
- Additional ffmpeg arguments for advanced users

Additional ffmpeg arguments are stored as a user-entered string for Re-encode mode, parsed into an argument list with whitespace splitting and quote handling, validated against app-managed options, and appended after generated encode/filter/metadata/timestamp options but before the output path. The app must continue passing ffmpeg arguments through `ProcessStartInfo.ArgumentList`; it must not concatenate the advanced field into a shell command. Malformed quotes and blocked options fail before process launch with a recoverable error.

Blocked additional options include app-managed input/range/output-control options (`-i`, `-ss`, `-t`, `-to`, `-y`, `-n`, `-nostdin`, `-map`, `-map_metadata`, `-avoid_negative_ts`) and generated codec/rate/filter options (`-c`, `-codec`, `-c:v`, `-codec:v`, `-c:a`, `-codec:a`, `-b:v`, `-crf`, `-cq`, `-vf`, `-filter:v`, `-af`, `-filter:a`). Future work can add more nuanced option validation if the advanced field expands beyond Re-encode tuning.

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

Audio normalization is an export setting available in both Fast Copy and Re-encode for changing loudness. The initial implementation uses a single-pass `loudnorm` filter with the requested loudness target:

`loudnorm=I=-14:TP=-1.5:LRA=11`

Fast Copy command policy when normalize audio is enabled:

- Select the requested time range with `-ss` and duration.
- Map all streams where practical with `-map 0`.
- Default to `-c copy` so video and untouched streams are stream-copied.
- Apply `-af loudnorm=I=-14:TP=-1.5:LRA=11`.
- Override audio with `-c:a aac` because audio filters require decoding and encoding.
- Preserve metadata with `-map_metadata 0` where supported.
- Use temporary output promotion and cancellation handling through the common `FfmpegRunner` path.

Re-encode command policy when normalize audio is enabled:

- Keep the selected video codec, encoder, and rate-control arguments.
- Add the same `loudnorm` audio filter and override audio with `-c:a aac`.
- If clip-edge audio fades are also enabled, combine `loudnorm` and `afade` in a single `-af` chain.

If media metadata confirms there is no audio stream, the planner rejects the export with a clear error. If metadata is unavailable, the planner assumes audio may exist and lets ffmpeg report any process-level failure. Future work may add two-pass loudnorm analysis, configurable true peak/loudness range, and codec-preserving audio encode choices.

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

Waveform previews are generated by invoking the configured or detected ffmpeg with the `showwavespic` filter into a temporary PNG cache. `WaveformPlanner` builds the ffmpeg argument list in testable core code, `WaveformGenerator` runs the process with cancellation support, and the WinUI timeline binds the generated PNG into the waveform image slot. This preview is visual-only and does not add audio waveform editing.

## Packaging

- Prefer unpackaged Windows App SDK deployment.
- Publish as a self-contained single-file EXE for Windows.
- Do not bundle ffmpeg in the first implementation.
- Document any runtime prerequisites discovered during WinUI 3 packaging work.
