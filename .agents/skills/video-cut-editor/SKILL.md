---
name: video-cut-editor
description: Use when working on VideoCutEditor, including product design, WinUI 3/C# implementation, ffmpeg or ffprobe integration, stream-copy export, re-encode options, fade handling, NVEnc detection and settings, tests, packaging, or repo guidance updates.
---

# VideoCutEditor

## Overview

Use this repo-scoped skill to keep VideoCutEditor work aligned with the agreed product and technical design. The skill is a routing layer: load the right repo documents, then implement or review against them.

## Required Reading

Before changing behavior, architecture, export logic, tests, or packaging, read:

- `AGENTS.md`
- `docs/product-spec.md`
- `docs/technical-design.md`
- `docs/codex-workflow.md`
- `docs/implementation-kickoff.md`

Use `docs/product-spec.md` as the source of truth for user-facing behavior. Use `docs/technical-design.md` as the source of truth for architecture and ffmpeg command policy. Use `docs/codex-workflow.md` for how Codex should plan, validate, and handle uncertainty in this repository.

## Workflow

1. Classify the request as product behavior, technical implementation, export/ffmpeg behavior, testing, packaging, or repo guidance.
2. Read only the matching source documents plus `AGENTS.md`.
3. Check whether the request changes an existing documented decision.
4. If it changes behavior or architecture, update the relevant doc in the same change.
5. Implement narrowly and verify with the tests or manual checks described in the docs.

## Guardrails

- Keep the app focused on one-range extraction.
- Keep fast stream-copy export as the default.
- Do not add active `.codex/rules` unless the repo guidance is explicitly updated.
- Prefer detecting ffmpeg, ffprobe, and NVEnc capabilities from the local environment over assuming support.
