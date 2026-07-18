# VideoCutEditor Product Spec

## Purpose

VideoCutEditor is a lightweight Windows desktop tool for extracting one or more desired segments from one video. It is for users who want a fast "keep these parts" workflow without opening a full nonlinear editor.

## Target User

- Windows users who already have ffmpeg available or can provide an ffmpeg build.
- Users who usually want fast lossless-ish container-level cutting, but sometimes need controlled re-encoding, fades, or hardware encoding.
- Users who prefer a small local app over a full editing suite.

## Main Workflow

1. Open a video file.
2. Preview it when Windows can play the format.
3. Set a start point and end point with the timeline controls or direct time inputs.
4. Optionally enter a title and add the current range to the export list. Repeat for other ranges in the same source video.
5. Confirm the output mode and optional fade/encode settings.
6. Export the registered ranges, or the current range when none are registered, to the configured output folder.

Each registered range is exported as an independent MP4 file. Merging ranges into one file is not supported.

## UI Behavior

- Show a video preview using Windows standard media playback.
- Show a custom editor-style timeline below the preview with the current playhead position, start/end markers, timeline zoom controls, and an audio waveform when ffmpeg can generate one.
- The play/pause button shows a play icon while not playing and switches to a pause icon while playback is active.
- The timeline ruler shows frequent minor tick lines plus longer labeled major tick lines. Time labels appear above their major tick lines with enough ruler height to avoid overlapping the tick marks, and whole-second labels must not display the preceding second because of floating-point accumulation.
- At timeline zoom `1.00x`, the timeline content fills the available parent width. Higher zoom levels expand the content horizontally inside the parent and allow horizontal scrolling. Timeline zoom changes in `0.01x` steps through the zoom slider and zoom buttons. Ctrl + mouse wheel over the timeline/waveform area changes zoom in `0.10x` steps.
- Provide a control near playback and range-marking controls that scrolls the timeline/waveform to the current playhead position.
- Ctrl + mouse wheel timeline zoom preserves the waveform time under the mouse cursor. Zoom-in and zoom-out buttons preserve the current playhead position by keeping it centered after zooming.
- Provide direct time inputs for start and end.
- Allow setting the start marker from the current playhead with `[` and the end marker with `]`, using both on-screen buttons and keyboard shortcuts.
- Support left/right arrow frame stepping while preview is focused. When ffprobe reports a valid video frame rate, use that frame rate; otherwise fall back to 30 fps.
- Support slow preview playback rates.
- Show export mode controls:
  - Fast copy
  - Re-encode
  - Normalize audio option
  - HDR to SDR conversion option, shown only for HDR media in Re-encode mode
  - Fade controls
  - Codec and encoder controls
  - Bitrate, target size, or quality controls
- Keep ffmpeg, ffprobe, and output-folder path configuration in a settings surface rather than the main editing surface. Tool configuration first asks for one folder containing both `ffmpeg.exe` and `ffprobe.exe`. When either executable is missing from that folder, reveal separate file selectors so the missing or nonstandard path can be configured individually.
- The main export surface shows an editable output file name initialized from the generated output name, plus the read-only full planned output path.
- The timeline control row places a compact clip-title text box and add button immediately to the right of the start/end marker buttons. Adding captures the current start/end range and title without changing the source video.
- The first registered clip opens a modeless export-list window. The list shows fixed-width start and end columns and a title column that expands or contracts with the window.
- A blank title is assigned a one-based placeholder such as `クリップ_1`. Registered titles and existing destination MP4 files are not reused; a numeric suffix is added when needed.
- The main export surface provides an output-folder open button beside the planned output controls.
- Show encoder information, media information, and export logs in a non-modal INFO window owned by the main window, with its own scrolling area so logs do not expand or block the main editor layout. Media information identifies HDR10/PQ or HLG and shows the probed color space, transfer characteristic, and color primaries. Reuse the open INFO window instead of opening duplicates, keep it associated with the main window for normal stacking/minimize behavior, and keep its displayed values synchronized while export continues.
- User-facing warnings, status messages, and informational notices should be localized for Japanese users where they are generated by the app. External ffmpeg/ffprobe error details may still appear inside logs when useful for troubleshooting.
- Show export modes as direct choices rather than a drop-down.
- In Fast copy mode, hide Re-encode-only controls such as codec, encoder, rate control, predicted re-encode size, fades, and additional ffmpeg arguments.
- Show ffmpeg progress, current status, log output, and a cancel button during export.
- The first export implementation supports Fast copy with progress/log display and cancellation.
- Re-encode mode supports codec family, encoder preference, bitrate-based export controls, target-size export controls, and clip-edge fade controls.
- When ffprobe identifies the selected video as HDR, show an informational notice. In Fast copy mode the notice explains that the video will remain HDR because no video re-encode occurs. In Re-encode mode show an `HDRをSDRに変換` checkbox only for HDR media, checked by default for newly opened HDR media.
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
- The settings surface provides both a picker for the output folder and a button to open the configured output folder in the system file explorer.
- The output filename is generated from the source filename with a cut suffix.
- Registered clips are always exported as `<title>.mp4` in registration order.
- If the generated filename already exists, append a numeric suffix starting at `_cut_1`, then `_cut_2`, instead of overwriting.
- If the user manually edits the output filename to an existing file, keep the typed filename and show a non-blocking inline warning rather than silently changing the name. Export still must not overwrite an existing output file.
- The default output container follows the input extension.
- Stream-copy cuts may not be frame-accurate because they depend on keyframes. The UI should make that tradeoff clear.
- Enabling any fade forces the export through Re-encode even when the visible export mode is Fast copy, because ffmpeg filters require decoding and encoding.
- The normalize audio option analyzes audio loudness before the final export, then applies an audio filter and therefore re-encodes audio. In Fast copy mode it should not re-encode video when video stream copy is possible; in Re-encode mode it combines with the selected video encode settings.
- HDR to SDR conversion requires video filtering and is available only in Re-encode mode. Fast copy exports preserve HDR video unchanged and only show an informational notice.

## Distribution

- Provide an x64 installer that installs for the current Windows user without requesting administrator privileges.
- The default install location is `%LocalAppData%/Programs/VideoCutEditor` rather than `Program Files`.
- Register a Start menu shortcut and a standard per-user uninstall entry.
- Continue providing the portable ZIP for users who do not want installation.
- Include the VideoCutEditor license and applicable official third-party license/notice files in both installer and portable distributions.
- ffmpeg and ffprobe remain external and are not bundled.
- Until production code signing is available, clearly disclose that Windows may show an unknown-publisher or SmartScreen warning.

## Settings

Persist these settings in the user's AppData folder:

- `ffmpegPath`
- `ffprobePath`
- `outputDirectory`
- Last-used export mode
- Last-used codec, encoder, rate control mode, and related encode settings
- Last-used normalize audio setting
- Last-used HDR to SDR conversion setting
- Last-used additional ffmpeg arguments

The app should try configured paths first, then PATH discovery as a fallback.

If the settings file is empty or contains invalid JSON, preserve the unreadable file for diagnosis and continue startup with default settings. A settings read failure must not prevent the editor from opening. Settings writes should replace the previous file only after a complete new JSON file has been written successfully.

## Error States

Show clear recoverable errors for:

- Missing ffmpeg path
- Missing ffprobe path
- Invalid output directory
- File, folder, or executable picker failure
- ffprobe metadata read failure
- Unsupported or unavailable encoder
- Audio normalization requested for media without an audio stream
- Malformed additional ffmpeg arguments
- Additional ffmpeg arguments that conflict with app-managed export options
- Export process failure
- Export cancellation
- Registered clip title or destination collision
- Start/end range errors
- Unreadable or damaged settings data

Errors should not delete source files or overwrite existing output files.

## Out Of Scope

- Merging clips
- Multi-track editing
- Timeline effects beyond clip-edge fade-in and fade-out
- Subtitle editing
- Audio waveform editing
- Cloud upload or online processing
- Bundling ffmpeg in the first implementation
