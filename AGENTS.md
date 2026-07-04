# AGENTS.md

## Repository Rules

- This repository builds VideoCutEditor, a lightweight Windows desktop app for extracting one desired range from a video.
- Before implementation work, read `docs/product-spec.md`, `docs/technical-design.md`, `docs/codex-workflow.md`, and `docs/implementation-kickoff.md`.
- For WinUI-specific setup, implementation, design, packaging, testing, or review work, use the repo-local Microsoft WinUI skills under `.agents/skills/winui-*` when relevant.
- Keep durable product behavior in `docs/product-spec.md`; keep architecture and ffmpeg command policy in `docs/technical-design.md`; keep Codex process guidance in `docs/codex-workflow.md`.
- If implementation changes product behavior, export behavior, architecture, or validation expectations, update the matching document in the same change.

## Product Constraints

- Use WinUI 3, C#, and .NET 10 for the Windows desktop app.
- Target a portable single-file EXE using unpackaged, self-contained Windows App SDK deployment unless a later design update changes this.
- Support only one selected keep range per export. Do not add multi-cut, merge, track editing, or timeline editing features without updating the spec first.
- Default export must use ffmpeg stream copy with no re-encode. Re-encode only when the user chooses it or when requested features require it, such as fades.
- Treat ffmpeg and ffprobe as external tools configured by path, with PATH discovery only as a fallback.
- Preserve streams and metadata where practical, especially in stream-copy exports.

## Verification

- Add or update unit tests for ffmpeg argument generation, settings persistence, output file naming, time calculations, and predicted file size calculations.
- Add integration coverage around stream-copy export, re-encode command construction, fade-triggered re-encode behavior, progress parsing, and cancellation.
- Manually verify preview, export, ffmpeg/ffprobe path handling, and NVEnc encoder detection when relevant hardware and ffmpeg builds are available.
