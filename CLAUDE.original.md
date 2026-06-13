# CLAUDE.md

This file is the startup guide for repository work. Keep it short. Put detailed specs in scoped docs instead of growing this file.

## Project Snapshot

- Unity 6 (`6000.3.11f1`) turn-based dice battle prototype
- C# 11+, .NET Standard 2.1, Windows Standalone x64, URP
- No CI/CD, tests, or linting configured
- Scenes (`.unity`) are not tracked in git and are generated from scene builders in `Assets/Editor/`

## Non-Negotiables

- Build or modify scenes through scene builder scripts, not manual scene editing.
- When renaming a `[SerializeField]` used by a builder, update the matching `SceneBuilderUtility.SetField()` call. Matching is case-sensitive and failed wiring only logs a warning.
- Shared builder helpers belong in `Assets/Editor/SceneBuilderUtility.cs`. Do not copy helper logic into individual builders.
- `GameSessionManager` owns mutable cross-scene runtime state. Deep-copy session lists before mutation, clamp stored indices before use, and add every new session field to `StartNewGame()`.
- `CurrentEventIndex` advances only in `GameExploreController`.
- In methods that mutate state, run validation before side effects.
- Do not read physical dice results before the settle loop completes.
- Avoid runtime `Shader.Find()`, obsolete Unity APIs, and per-frame allocating physics queries when a `NonAlloc` API exists.

## Read By Topic

- Coding conventions: `.claude/rules/coding.md`
- Scene builder rules: `.claude/rules/scene-builder.md`
- Cross-scene flow and session invariants: `.claude/specs/game-flow.md`
- Battle rules, dice flow, battle log, debug console: `.claude/specs/battle-system.md`
- Tuning values, UI sizing, and balance tables: `docs/tuning.md`
- Dependencies and required/generated assets: `docs/assets.md`

## Source Of Truth

- `CLAUDE.md` contains only startup rules and document routing.
- Files under `.claude/archive/` and `prompts/` are historical notes, not current rules.
- If a rule changes, update the scoped doc that owns that topic instead of appending detail here.
