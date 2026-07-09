# Codex Workflow

## Working Model

- Start by reading `AGENTS.md`, this file, `docs/product-spec.md`, `docs/technical-design.md`, `docs/implementation-kickoff.md`, and `docs/implementation-status.md`.
- Before implementing a feature, check whether it changes product behavior or export semantics. If it does, update the relevant doc in the same change.
- Before choosing the next slice after a fresh session or context compression, read `docs/implementation-status.md` and update it when a slice is completed or when remaining work changes.
- Documentation updates are mandatory after every completed implementation slice. Do not commit implementation work without updating the matching docs and `docs/implementation-status.md`.
- When work reveals a bug, missing requirement, validation gap, technical debt item, or additional follow-up, document it immediately in the appropriate source of truth instead of leaving it only in chat context.
- Prefer small, verifiable changes over broad rewrites.
- Keep product behavior, technical design, and Codex workflow guidance in separate files.

## Planning Expectations

Plan before implementing when:

- Adding a new user-facing workflow.
- Changing ffmpeg command policy.
- Changing codec, encoder, or NVEnc behavior.
- Changing packaging or deployment.
- Adding dependencies or replacing the UI framework.

For small fixes that already fit the docs, implement directly and keep changes scoped.

## Implementation Expectations

- Follow WinUI 3 and C# conventions.
- Prefer t-wada style test-driven development for new behavior: write or update a failing behavior-focused test first, make the smallest production change to pass it, then refactor while keeping tests green. If a behavior cannot be tested at the current layer, extract a smaller testable core unit or explicitly defer it to the WinUI UI testing slice.
- Treat docs as part of the implementation. When code, tests, manual verification, or user feedback changes the known state of the project, update docs in the same slice.
- Use the repo-local Microsoft WinUI skills when they match the task:
  - `winui-setup` for environment and project setup.
  - `winui-dev-workflow` for build/run workflow.
  - `winui-design` for WinUI UI and XAML design decisions.
  - `winui-packaging` for Windows App SDK packaging.
  - `winui-ui-testing` for UI testing strategy.
  - `winui-code-review` for review passes.
- These WinUI skills were installed from `microsoft/win-dev-skills` at commit `c98bc78c427910838e97c70bdcd7678a0331fad3` under `plugins/winui/skills`. The local `winui-setup` copy removes the upstream `disable-model-invocation` frontmatter key so the current Codex skill validator accepts it.
- Keep ffmpeg command construction testable without requiring actual video files.
- Treat file paths as user data and avoid shell interpolation.
- Prefer explicit process arguments over command strings.
- Keep advanced ffmpeg controls available without making the default UI complex.
- Do not silently re-encode in fast-copy mode.

## Validation Expectations

Use automated tests for deterministic behavior:

- ffmpeg argument generation
- ffprobe metadata parsing
- settings persistence
- output filename collision handling
- time range and duration calculation
- bitrate and predicted file size calculation
- cancellation state handling

Use `tests/ui-tests.ps1` with `winapp ui` for WinUI surface behavior:

- expected editor controls and AutomationIds exist
- key controls expose correct enabled/default state
- screenshots are captured for visual inspection
- extend this script before or alongside user-facing UI changes

For VS Code breakpoint debugging, keep `.vscode/launch.json` and `.vscode/tasks.json` aligned so F5 uses the `VideoCutEditor: Debug x64` launch configuration and the `build VideoCutEditor x64` pre-launch task. The task must build with `-p:Platform=x64` and `-p:WindowsPackageType=None`, then launch the x64 Debug output directly. This uses the app's custom unpackaged entry point for breakpoint debugging and avoids C# Dev Kit generating mixed paths such as `bin\ARM64\...\win-x64` on machines where host architecture detection does not match the desired debug RID. Use `BuildAndRun.ps1` or `winapp run` when validating normal packaged WinUI launch behavior and debug-output logs.

Use manual tests for environment-dependent behavior:

- Windows preview playback
- ffmpeg/ffprobe path selection
- real export success
- unavailable preview fallback
- NVEnc encoder detection on compatible hardware
- packaged EXE startup

Use `scripts/New-SampleMedia.ps1` to create repeatable local sample inputs before manual preview/export checks when representative user media is not available. Its default output is `artifacts/verification-media`, which is intentionally ignored by git. It creates short `video-with-audio.mp4`, `video-only.mp4`, and `quiet-audio.mp4` files for waveform, no-audio, fade, and loudness-normalization checks. The script supports `-WhatIf` for dry-run verification and `-FfmpegPath` when PATH discovery is not enough.

Record manual test results, skipped checks, and newly discovered validation needs in `docs/implementation-status.md`.

## Handling Uncertainty

- For ffmpeg behavior, prefer local `ffmpeg -version`, `ffmpeg -encoders`, and `ffmpeg -h encoder=<name>` over assumptions.
- For WinUI 3 or Windows App SDK behavior, verify against the installed SDK and official Microsoft documentation when details could have changed.
- For NVEnc settings, inspect the actual ffmpeg build before using optional high-quality arguments.
- If a capability depends on hardware, driver, or ffmpeg build support, detect it at runtime and show a clear fallback.

## Rules Policy

Do not add active `.codex/rules` files in the first guidance pass. Codex Rules are experimental and this workspace currently allows full access.

If future approval-control work needs Rules, document and test candidate rules before enabling them. Reasonable future candidates could include:

- Prompt before package installation commands.
- Allow read-only inspection commands.
- Prompt before destructive filesystem or git commands.
- Prompt before networked package restore or installer commands.

Never use Rules as the only safety layer for destructive behavior. Keep implementation safeguards and tests in the codebase.
