# AGENTS.md

Codex startup guide for this Unity project. Keep this file short and operational. Put detailed behavior specs in scoped docs, not here.

## Project Snapshot

- Unity 6 (`6000.3.11f1`) turn-based battle prototype.
- Weapons: Dice (Yacht-style), Mahjong (리치마작 기반 1인 조패).
- C# 11+, .NET Standard 2.1, Windows Standalone x64, URP.
- No CI/CD or linting. EditMode tests exist under `Assets/Editor/Tests/`.
- Scenes (`.unity`) are generated from builders in `Assets/Editor/` and are not the source of truth.

## Confirmed Vs Check Needed

- Confirmed by repo scan: runtime code is under `Assets/Scripts/`, builders are under `Assets/Editor/`, tests exist under `Assets/Editor/Tests/`, scene wiring uses `SceneBuilderUtility.SetField()`.
- Check needed in Unity Editor: whether the current worktree compiles, whether all EditMode tests pass, and whether generated scenes/prefabs match builder output.

## Operating Rules

- Work one backlog item at a time. Do not broaden scope while editing.
- Prefer small, testable extractions over rewrites.
- Do not add new gameplay features while doing refactor work.
- Do not rename, delete, move, or namespace-shift classes/files/assets unless the task explicitly says so.
- Do not edit generated scenes or prefabs manually. Scene changes must be made through scene builders.
- Do not edit `.meta` files manually. If a new/moved/renamed asset would create or change `.meta`, call it out before doing it.
- Do not edit `.unity`, `.prefab`, `.asset`, `ProjectSettings/`, `Packages/`, `Library/`, `Temp/`, or `UserSettings/` unless the task explicitly targets them.
- Do not run `dotnet build`. Unity validation must follow the isolation rules below.
- Before each refactor: read the relevant scoped docs, inspect current code, identify serialized-field and asset risks, then make the smallest change that satisfies the task.

## Unity validation isolation

Unity validation must never run against the foreground local checkout.

Definitions:
- Foreground project: the Unity project path currently opened by the user.
- Validation project: a separate Git worktree path used only for automated validation.

Hard rules:
- Do not run Unity batchmode with `-projectPath` equal to the foreground project path.
- Always run Unity tests/builds from a separate Git worktree.
- Do not use the same branch checked out in both the foreground checkout and validation worktree.
- Prefer detached HEAD or a dedicated validation branch.
- Write logs, test results, and build outputs outside the Unity project root.
- Never write build output under `Assets/`.
- Never use `-ignorecompilererrors`.
- Never use `-rebuildLibrary` on the foreground checkout.
- Never use `-accept-apiupdate` unless the task explicitly authorizes API migration.
- Always pass `-logFile`.
- Always pass an explicit `-buildTarget` for build validation.
- After validation, run `git status --porcelain` in the validation worktree.
- If validation changes tracked files, treat that as a failure unless the task explicitly allowed it.
- Treat validation worktrees as disposable automated-validation workspaces.
- Before cleanup, report the result summary, log path, test result path, and build output path to the user, or record them in the relevant result document.
- Then run `git status --porcelain` inside the validation worktree.
- If tracked files changed, classify the run as failure/risk per the rule above and do not copy those changes into the foreground checkout.
- After required results are recorded, delete the validation worktree.
- Prefer `git worktree remove <path>` for deletion.
- If Unity processes or file locks block deletion, close Unity and retry.
- Use forced worktree deletion only after confirming the path is not the foreground project, is not a human working checkout, and has no unrecorded result documents, logs, test results, build outputs, or explicitly preserved artifacts.
- If a temporary validation branch was created, report it as a deletion candidate after removing the validation worktree.
- Keep external validation output folders, such as `C:\Users\song\desktop\Capstone_validation_outputs`, separate from worktrees; they preserve results, but list stale output folders for separate cleanup when the user asks.
- Never delete the foreground checkout, the project currently open in Unity Editor, tracked runtime scenes, `.meta` files, or validation artifacts the user explicitly asked to preserve.

## Unity Safety Rules

- `[SerializeField]` names wired by builders are part of the public contract. Renaming one requires updating every matching `SceneBuilderUtility.SetField()` call, case-sensitively.
- Public methods used by `UnityEventTools.AddPersistentListener()` are scene callback contracts. Do not rename them without builder updates and scene regeneration.
- Shared builder helpers belong in `Assets/Editor/SceneBuilderUtility.cs`; do not copy-paste helper code into individual builders.
- New C# files create Unity `.meta` files. For Low-risk refactor tasks, prefer extending existing test files to avoid new `.meta` churn.
- Preserve asset GUIDs. Never delete and recreate assets or `.meta` files as a cleanup shortcut.

## Runtime Invariants

- `GameSessionManager` owns mutable cross-scene runtime state.
- Deep-copy session lists before mutation.
- Clamp stored indices before use.
- Add every new session field to `StartNewGame()`.
- `CurrentEventIndex` advances only in `GameExploreController`.
- Mutating methods validate before side effects.
- No physical dice result is read before the settle loop completes.
- Avoid runtime `Shader.Find()`, obsolete Unity APIs, and per-frame allocating physics queries when a `NonAlloc` API exists.

## Risk Levels

- Low: tests or pure logic only; no scene, prefab, serialized field, `.meta`, asset, builder, or project-setting changes.
- Medium: small runtime refactor with tests; no scene/prefab/manual asset changes; no renames or public API changes unless explicitly scoped.
- High: scene builders, generated scenes, prefabs, serialized fields, `.meta`, asset moves/deletes, scene names, project settings, public callback names, broad controller splits.

High-risk work needs an explicit plan, a narrow file list, and user-visible verification steps before implementation.

## Read By Topic

- Coding conventions: `.claude/rules/coding.md`
- Scene builder rules: `.claude/rules/scene-builder.md`
- Cross-scene flow, session invariants: `.claude/specs/game-flow.md`
- Battle rules, dice flow, battle log, debug console: `.claude/specs/battle-system.md`
- Mahjong battle rules: `.claude/specs/mahjong-battle.md`
- Tuning values, UI sizing, balance tables: `docs/tuning.md`
- Dependencies and required/generated assets: `docs/assets.md`
- Refactor operating backlog: `REFACTOR_BACKLOG.md`

## Source Of Truth

- `AGENTS.md` = startup rules and safety constraints.
- `REFACTOR_BACKLOG.md` = approved v0.1 cleanup backlog and task order.
- `.claude/archive/` and `prompts/` = historical notes, not current rules.
- Rule changes go in the scoped doc that owns the topic.
