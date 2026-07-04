# Codex Workflow

## Working Model

- Start by reading `AGENTS.md`, this file, `docs/product-spec.md`, and `docs/technical-design.md`.
- Before implementing a feature, check whether it changes product behavior or export semantics. If it does, update the relevant doc in the same change.
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

Use manual tests for environment-dependent behavior:

- Windows preview playback
- ffmpeg/ffprobe path selection
- real export success
- unavailable preview fallback
- NVEnc encoder detection on compatible hardware
- packaged EXE startup

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
