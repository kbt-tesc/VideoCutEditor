# Implementation Kickoff

Use this document to start the implementation in a fresh Codex session.

## Fresh Session Checklist

1. Start Codex from the cloned repository root (`<repository-root>`).
2. Confirm Codex loaded `AGENTS.md`.
3. Confirm repo-local skills are visible, especially:
   - `video-cut-editor`
   - `winui-setup`
   - `winui-dev-workflow`
   - `winui-design`
   - `winui-packaging`
   - `winui-ui-testing`
   - `winui-code-review`
4. Read the product, technical, workflow, and implementation status docs before editing code.
5. Use `winui-setup` first to verify or install prerequisites.
6. Check `docs/implementation-status.md` for completed slices, current verification baseline, and recommended next work.

## Recommended First Implementation Slice

Implement the smallest useful skeleton before export logic:

1. Create the WinUI 3/.NET 10 solution and app project.
2. Add a test project for command-generation and settings logic.
3. Implement settings storage for ffmpeg path, ffprobe path, and output directory.
4. Add the main editor shell with file selection, settings access, and empty preview/timeline regions.
5. Add service interfaces for ffprobe, ffmpeg capability detection, export planning, and process execution.

Do not implement full export in the first slice unless the scaffold and tests are already stable.

## Suggested Follow-up Slices

For the current live status, use `docs/implementation-status.md`. This historical list is kept as the original kickoff guidance.

- Media probing and metadata display.
- Preview loading and start/end marker state.
- Fast stream-copy export command generation.
- Export runner with progress and cancellation.
- Re-encode command generation with bitrate estimate.
- Fade filters and audio re-encode behavior.
- NVEnc detection and fallback.
- Packaging and manual verification.

## Ready Prompt For New Session

```text
Use the repo-local VideoCutEditor and WinUI skills. Read AGENTS.md, docs/product-spec.md, docs/technical-design.md, docs/codex-workflow.md, and docs/implementation-kickoff.md. Then start the first implementation slice: create the WinUI 3/.NET 10 solution and app/test project skeleton, verify prerequisites with winui-setup, and keep changes aligned with the docs.
```
