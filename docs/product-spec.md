# VideoCutEditor Product Spec

## Purpose

VideoCutEditor is a lightweight Windows desktop tool for extracting one desired segment from a video. It is for users who want a fast "keep this part" workflow without opening a full nonlinear editor.

## Target User

- Windows users who already have ffmpeg available or can provide an ffmpeg build.
- Users who usually want fast lossless-ish container-level cutting, but sometimes need controlled re-encoding, fades, or hardware encoding.
- Users who prefer a small local app over a full editing suite.

## Main Workflow

1. Open a video file.
2. Preview it when Windows can play the format.
3. Set a start point and end point with the timeline controls or direct time inputs.
4. Confirm the output mode and optional fade/encode settings.
5. Export the selected range to the configured output folder.

Only one keep range is supported per export.

## UI Behavior

- Show a video preview using Windows standard media playback.
- Show a custom editor-style timeline below the preview with the current playhead position, start/end markers, timeline zoom controls, and an audio waveform when ffmpeg can generate one.
- At timeline zoom `1.0x`, the timeline content fills the available parent width. Higher zoom levels expand the content horizontally inside the parent and allow horizontal scrolling.
- Provide direct time inputs for start and end.
- Allow setting the start marker from the current playhead with `[` and the end marker with `]`, using both on-screen buttons and keyboard shortcuts.
- Support left/right arrow frame stepping while preview is focused. When ffprobe reports a valid video frame rate, use that frame rate; otherwise fall back to 30 fps.
- Support slow preview playback rates.
- Show export mode controls:
  - Fast copy
  - Re-encode
  - Normalize audio option
  - Fade controls
  - Codec and encoder controls
  - Bitrate, target size, or quality controls
- Show export modes as direct choices rather than a drop-down.
- In Fast copy mode, hide Re-encode-only controls such as codec, encoder, rate control, predicted re-encode size, fades, and additional ffmpeg arguments.
- Show ffmpeg progress, current status, log output, and a cancel button during export.
- The first export implementation supports Fast copy with progress/log display and cancellation.
- Re-encode mode supports codec family, encoder preference, bitrate-based export controls, target-size export controls, and clip-edge fade controls.
- Normalize audio is a setting available in both Fast copy and Re-encode. It uses two-pass loudness normalization to `-14 LUFS`, re-encodes audio to AAC, preserves video according to the selected mode where practical, and reports a clear error when probed media has no audio stream.
- Re-encode mode includes an advanced additional ffmpeg arguments field. The app parses the field into explicit process arguments, supports quoted values with spaces, rejects app-managed options such as input, range, codec, filter, and output-control arguments, catches obvious syntax mistakes such as bare values and missing values for common value-taking options, and shows a recoverable error.
- Quality mode uses a single numeric quality value where lower values mean higher quality and output size is not predicted.
- Fade duration is adjusted in 0.25 second steps and is truncated to two decimal places.
- Audio fade controls affect existing audio streams only. They do not synthesize or add audio to inputs that have no audio stream.
- When media metadata is available, Re-encode mode suggests an initial video bitrate from the source bitrate or resolution. The value remains editable and saved user settings take precedence.

The first screen should be the usable editor, not a landing page or marketing screen.

## Preview Behavior

- If Windows can preview the input, enable normal seeking and marker placement from playback.
- If preview is unavailable, show a warning and allow editing to continue through ffprobe metadata and manual time inputs.
- Do not reject a file only because Windows preview cannot play it if ffmpeg can still process it.

## Output Behavior

- The output folder is configured in settings before export.
- The output filename is generated from the source filename with a cut suffix.
- If the generated filename already exists, append a numeric suffix such as `_cut_2` instead of overwriting.
- The default output container follows the input extension.
- Stream-copy cuts may not be frame-accurate because they depend on keyframes. The UI should make that tradeoff clear.
- Enabling any fade forces the export through Re-encode even when the visible export mode is Fast copy, because ffmpeg filters require decoding and encoding.
- The normalize audio option analyzes audio loudness before the final export, then applies an audio filter and therefore re-encodes audio. In Fast copy mode it should not re-encode video when video stream copy is possible; in Re-encode mode it combines with the selected video encode settings.

## Settings

Persist these settings in the user's AppData folder:

- `ffmpegPath`
- `ffprobePath`
- `outputDirectory`
- Last-used export mode
- Last-used codec, encoder, rate control mode, and related encode settings
- Last-used normalize audio setting
- Last-used additional ffmpeg arguments

The app should try configured paths first, then PATH discovery as a fallback.

## Error States

Show clear recoverable errors for:

- Missing ffmpeg path
- Missing ffprobe path
- Invalid output directory
- ffprobe metadata read failure
- Unsupported or unavailable encoder
- Audio normalization requested for media without an audio stream
- Malformed additional ffmpeg arguments
- Additional ffmpeg arguments that conflict with app-managed export options
- Export process failure
- Export cancellation
- Start/end range errors

Errors should not delete source files or overwrite existing output files.

## Out Of Scope

- Multi-range cutting
- Merging clips
- Multi-track editing
- Timeline effects beyond clip-edge fade-in and fade-out
- Subtitle editing
- Audio waveform editing
- Cloud upload or online processing
- Bundling ffmpeg in the first implementation
