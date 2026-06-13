# CLAUDE.md

File = startup guide. Keep short. Detailed specs go in scoped docs, not here.

## Project Snapshot

- Unity 6 (`6000.3.11f1`) turn-based battle prototype. Weapons: Dice (Yacht-style), Mahjong (리치마작 기반 1인 조패)
- C# 11+, .NET Standard 2.1, Windows Standalone x64, URP
- No CI/CD, tests, linting
- Scenes (`.unity`) untracked in git. Generated from scene builders in `Assets/Editor/`

## Non-Negotiables

- Build/modify scenes via scene builder scripts. No manual scene editing.
- Rename `[SerializeField]` used by builder → update matching `SceneBuilderUtility.SetField()` call. Case-sensitive. Failed wiring only logs warning.
- Shared builder helpers → `Assets/Editor/SceneBuilderUtility.cs`. No copy-paste into individual builders.
- `GameSessionManager` owns mutable cross-scene runtime state. Deep-copy session lists before mutation. Clamp stored indices before use. Add every new session field to `StartNewGame()`.
- `CurrentEventIndex` advances only in `GameExploreController`.
- Mutating methods: validate before side effects.
- No reading physical dice results before settle loop completes.
- Avoid runtime `Shader.Find()`, obsolete Unity APIs, per-frame allocating physics queries when `NonAlloc` API exists.

## Read By Topic

- Coding conventions: `.claude/rules/coding.md`
- Scene builder rules: `.claude/rules/scene-builder.md`
- Cross-scene flow, session invariants: `.claude/specs/game-flow.md`
- Battle rules, dice flow, battle log, debug console: `.claude/specs/battle-system.md`
- Mahjong battle rules (tile wall, yaku, damage): `.claude/specs/mahjong-battle.md`
- Tuning values, UI sizing, balance tables: `docs/tuning.md`
- Dependencies, required/generated assets: `docs/assets.md`

## Source Of Truth

- `CLAUDE.md` = startup rules + doc routing only.
- `.claude/archive/` and `prompts/` = historical notes. Not current rules.
- Rule change → update scoped doc owning topic. No appending here.