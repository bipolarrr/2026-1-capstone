# REFACTOR_BACKLOG.md

Version: v0.1 operational backlog
Audit date: 2026-05-26

This document is for Unity refactor handoff. It records what is known, what still needs verification, and which small tasks are safe to run first. It does not authorize broad rewrites.

Update note, 2026-05-28: first-pass Hold'em source implementation supersedes the original placeholder-routing assumption. Treat Hold'em as source-level implemented but Unity-unvalidated until isolated Editor validation completes.

## Operating Scope

- Work one task at a time.
- Preserve current gameplay behavior unless a task explicitly says otherwise.
- Prefer tests and pure-logic seams before touching controllers, scene builders, prefabs, or serialized fields.
- Do not rename, delete, move, namespace-shift, or reparent files/assets unless the task explicitly includes that action.
- Do not edit `.unity`, `.prefab`, `.asset`, or `.meta` files for Low/Medium tasks.
- If a task would create a new C# file, note that Unity will create a `.meta` file. Prefer existing test files for Low-risk work.
- Codex must not run Unity batchmode or `dotnet build`. Verification that requires Unity must be done in the user's Unity Editor.

## Confirmed Findings

- `Assets/Editor/Tests/` exists and already contains Battle and Mahjong EditMode tests.
- Scene builders under `Assets/Editor/` wire private/protected runtime fields by string name through `SceneBuilderUtility.SetField()`.
- `GameSessionManager` is static mutable cross-scene state and is used by Explore, Dice battle, Mahjong battle, debug commands, and some animation/dice code.
- `GameExploreController` owns `CurrentEventIndex` advancement and battle scene selection.
- `BattleControllerBase`, `DiceRollDirector`, `EnemyCounterAttackDirector`, `GameExploreController`, `MahjongBattleController`, and `SceneBuilderUtility` are high-coupling files.
- Several pure or near-pure logic classes already exist: `DamageCalculator`, `PlayerAttackPipeline`, `DefenseCalculator`, `DiceFaceResolver`, `ComboProximity`, `ComboFortune`, `EnemyAttackPositionResolver`, Mahjong rule classes, and Hold'em rule classes.

## Check Needed

- Confirm in Unity Editor whether the current worktree compiles.
- Confirm in Unity Editor whether all current EditMode tests pass before the first refactor.
- Confirm whether generated scenes and prefabs are in sync with builder output.
- Confirm whether existing untracked Unity assets are intentional before any asset or `.meta` operation.
- Confirm exact line counts before using size as a refactor metric; current notes are static-read estimates.

## Major Systems

| System | Primary Files | Responsibility | Refactor Risk |
|---|---|---|---|
| Session state | `Assets/Scripts/Game/GameSessionManager.cs`, `CharacterSelectionContext.cs` | Character, hearts, power-ups, current stage, battle enemies, battle result | Medium because static mutable state affects scene flow. |
| Explore flow | `Assets/Scripts/Explore/GameExploreController.cs` | Event progression, encounters, item box, battle routing | Medium to High because it owns `CurrentEventIndex`. |
| Stage data | `Assets/Scripts/Stages/*` | Stage registry, mobs, bosses, sprite paths, round order | Medium because data is read by runtime and builders. |
| Dice battle | `BattleSceneController.cs`, `BattleControllerBase.cs`, `DiceRollDirector.cs`, `EnemyCounterAttackDirector.cs` | Dice attack/defense flow, enemy counterattack, HUD/log/VFX | High because controllers are serialized and builder-wired. |
| Dice runtime | `Assets/Scripts/Dice/Dice.cs`, `DiceFaceResolver.cs`, `DiceViewportInteraction.cs` | Physical dice, result orientation, viewport input | Medium; `DiceFaceResolver` is Low when tested alone. |
| Mahjong battle | `Assets/Scripts/Mahjong/*`, `MahjongBattleController.cs` | Tile wall, hand/yaku evaluation, waits, Mahjong battle flow | Medium to High; pure rules are safer than controller flow. |
| Scene builders | `Assets/Editor/*SceneBuilder.cs`, `SceneBuilderUtility.cs`, `DicePrefabBuilder.cs` | Programmatic scene/prefab/material wiring | High because output is generated Unity objects. |
| Debug console | `DebugConsoleController.cs`, `DebugCommandProcessor.cs`, `IBattleDebugTarget.cs` | Runtime command UI and debug execution | Medium; parsing can become Low after side effects are separated. |
| Audio | `AudioManager.cs`, builder audio setup | Clip lookup and playback | Medium because clip names are string contracts. |

## God Objects And Strong Coupling

| Finding | Evidence | Operational Rule |
|---|---|---|
| `SceneBuilderUtility` is an editor-side god object | Owns generic UI helpers, battle UI, asset loading, audio manager setup, build settings, and reflection wiring | Do not split broadly. Extract one responsibility at a time only after tests or a builder compile check exist. |
| `DiceBattleSceneBuilder` is a scene/prefab wiring hub | Creates physics arenas, render textures, dice, UI, persistent callbacks, controller wiring, materials | Treat any edit as High risk. No Low/Medium task should touch it. |
| `BattleControllerBase` owns many common battle concerns | Stage background, fallback enemy generation, enemy UI, death poses, debug sprite playback, session reads | Extract pure helpers first. Do not rename protected serialized fields. |
| `EnemyCounterAttackDirector` combines flow, UI, animation, and damage | Counterattack sequence, dice overlay, defense phase, player damage, logging, projectile logic | Extract calculation-only helpers before splitting coroutines. |
| `DiceRollDirector` mixes state machine, layout, hold/vault UI, audio, and physics coordination | Roll state, slot/vault placement, stop profile, button labels, events | Extract deterministic math before touching coroutine flow. |
| `GameExploreController` mixes event flow, generation, UI, session mutation, audio, and scene loading | Direct `GameSessionManager` writes and `SceneManager.LoadScene()` calls | Do not move `CurrentEventIndex` ownership. Extract pure routing/catalog/generation first. |
| `MahjongBattleController` mixes rules, UI, animation, and debug hooks | Discard flow, Ron/Tsumo, wait reveal, attack animation, victory/defeat | Extract one tested policy at a time; do not touch tile prefab wiring first. |
| `GameSessionManager` is global mutable state | Used across scene controllers and debug commands | Tests must cover reset/snapshot invariants before adding fields. |

## Scene, Prefab, Serialized Field, And `.meta` Risk

| Risk | Where | Classification | Rule |
|---|---|---|---|
| String-based serialized field wiring | `SceneBuilderUtility.SetField(...)` | High | Any field rename must update matching builder strings and have a Unity Editor scene-builder verification step. |
| Persistent listener callback names | `UnityEventTools.AddPersistentListener(...)` | High | Do not rename public UI callback methods unless the task explicitly includes builder updates. |
| Generated scenes | `Assets/Scenes/*.unity` or scene files generated by builders | High | Do not hand-edit. Builder scripts are the source of truth. |
| Prefab generation | `DicePrefabBuilder.cs`, generated dice prefabs | High | Do not edit prefab output in Low/Medium tasks. |
| `.meta` files | Any Unity asset or C# script metadata | High unless explicitly expected | Do not delete/regenerate. New files imply `.meta`; call out before creating them. |
| Asset path strings | `SceneBuilderUtility` constants, `StageData` definitions | Medium to High | Moving assets is not a refactor task unless explicitly scoped. |
| Scene name strings | `SceneManager.LoadScene(...)` | Medium to High | Constants/resolvers are allowed in small tasks; scene asset renames are not. |
| Project settings and packages | `ProjectSettings/`, `Packages/` | High | Do not touch during code refactors. |

## First Three Tasks: Fixed Execution Order

| Order | Task | Risk | Verification Method |
|---:|---|---|---|
| 1 | Add player damage pipeline regression tests | Low | Unity Editor Test Runner -> EditMode -> Battle tests. Also verify diff has no `.unity`, `.prefab`, `.asset`, `.meta`, or `Assets/Scripts/` runtime changes. |
| 2 | Add dice stop planning tests | Low | Unity Editor Test Runner -> EditMode -> Battle tests. Also verify tests do not use physics dice, scene objects, or `UnityEngine.Random.value`. |
| 3 | Expand session and heart invariant tests | Low | Unity Editor Test Runner -> EditMode -> `GameSessionManagerTests` or Battle tests. Also verify only existing test files changed. |

Do not start task 4 until tasks 1-3 are done or explicitly skipped by the user.

## Backlog

### 1. Low - Add Player Damage Pipeline Regression Tests

Goal: Add regression coverage for current player attack calculation behavior.

Context: `DamageCalculator` and `PlayerAttackPipeline` are pure static code. Existing Battle EditMode tests already live under `Assets/Editor/Tests/Battle/`.

Constraints: Modify only existing Battle EditMode test files unless the user approves a new file and its `.meta`. Do not touch runtime code, scene builders, scenes, prefabs, serialized fields, `.asset`, or `.meta` files. Do not change namespaces.

Done When: Unity Editor EditMode tests cover combo damage, splash ratio, `AllOrNothing` then `OddEvenDouble` power-up order, `PlayerAttackPipeline.PadToFive`, and `GetPlayerAttackClipName`. The diff contains no Scene/Prefab/asset/runtime-code changes.

### 2. Low - Add Dice Stop Planning Tests

Goal: Lock down deterministic dice stop and boost-planning decisions.

Context: `ComboProximity` and deterministic parts of `ComboFortune` drive the "나와라!" roll stop flow, while the risky coroutine/physics behavior stays in `DiceRollDirector`.

Constraints: Modify only existing Battle EditMode test files unless the user approves a new file and its `.meta`. Test deterministic methods only: stop profile, combo rank, emphasis/decisive masks, held-mask behavior, and `TryUpgradeOneDie`. Do not use physical dice, scene objects, or random-value assertions.

Done When: Unity Editor EditMode tests cover already-combo, one-away, no-combo, held-mask, and tie-break behavior. The diff contains no Scene/Prefab/asset/runtime-code changes.

### 3. Low - Expand Session And Heart Invariant Tests

Goal: Strengthen regression coverage for global session and heart state invariants.

Context: `GameSessionManager` owns cross-scene mutable state and already has `GameSessionManagerTests`. `HeartContainer` is pure enough to test without scene objects.

Constraints: Prefer extending `Assets/Editor/Tests/Battle/GameSessionManagerTests.cs`. Reset static session state in test setup/teardown. Do not touch runtime code, scenes, prefabs, builders, serialized fields, `.asset`, or `.meta` files.

Done When: Unity Editor EditMode tests prove `StartNewGame()` resets all current session fields, battle enemy snapshots are deep copies, `ReviveOnce` is one-use, and heart damage/heal edge cases match current behavior. The diff contains only existing test-file changes.

### 4. Low - Add Serialized Field Wiring Audit Tests

Goal: Detect broken `SetField` target names before scene builders are run.

Context: Builder wiring is string-based and case-sensitive. `SceneBuilderUtilityTests` already verifies `SetField()` behavior.

Constraints: Use Editor tests only. Do not generate or save scenes. Do not edit builders in this task. Start with curated field-name lists for `BattleControllerBase`, `GameExploreController`, and `MahjongBattleController`; mark full static source parsing as 확인 필요.

Done When: Unity Editor EditMode tests fail with the target type and missing field name if an audited builder field string no longer exists.

### 5. Medium - Extract Battle Scene Routing Resolver

Goal: Move character-to-battle-scene selection into a small tested resolver.

Context: `GameExploreController` currently contains private scene routing logic and string scene names.

Constraints: Preserve current behavior: Mahjong -> `MahjongBattleScene`; Holdem -> `HoldemBattleScene`; Dice/default -> `DiceBattleScene`. Do not rename scene assets. Do not move `CurrentEventIndex` logic.

Done When: `GameExploreController` delegates only routing to the resolver, Unity Editor EditMode tests cover all `CharacterType` values, and no scene/prefab/serialized-field files change.

### 6. Medium - Extract Power-Up Option Catalog

Goal: Move item-box option data out of `GameExploreController.SetupItemEncounter`.

Context: Titles, descriptions, and `PowerUpType` values are inline arrays that must stay index-aligned.

Constraints: Preserve current text, order, and balance. Keep existing UI and serialized fields. Do not add new power-ups or item behavior.

Done When: A pure catalog returns the current three options, `GameExploreController` consumes it, and EditMode tests verify option count, order, labels, descriptions, and types.

### 7. Medium - Extract Seedable Encounter Generation

Goal: Make normal enemy generation deterministic and testable.

Context: Explore normal combat generation currently uses `Random.Range` directly and is conceptually duplicated by battle direct-run fallback generation.

Constraints: Preserve count clamping, no-duplicate mob selection, HP range inclusivity, sprite lookup by existing bundle index, and session deep-copy behavior. Do not change stage data.

Done When: A helper can generate deterministic encounters from a seed, `GameExploreController` delegates generation, and EditMode tests cover count/range/duplicate behavior without scene objects.

### 8. Medium - Extract Shared Stage Visual Lookup

Goal: Remove duplicated stage bundle, background, and projectile lookup logic.

Context: `GameExploreController` and `BattleControllerBase` both find `StageSpriteBundle` data and apply fallback visuals.

Constraints: Keep existing serialized `StageSpriteBundle[]` fields to avoid builder changes. Do not move assets or change sprite paths.

Done When: Both controllers call a shared helper for lookup decisions, fallback behavior is unchanged, and tests cover null bundle, missing sprite, and projectile lookup cases where practical.

### 9. Medium - Extract Dice Layout Math

Goal: Move slot and vault position calculations out of `DiceRollDirector`.

Context: Dice layout math is deterministic but currently mixed with coroutines, UI button state, physics, and hold state.

Constraints: Do not change `DiceRollDirector` public API, serialized fields, hold behavior, or physics flow. Preserve current 5-dice 3-over-2 layout and line layout for other counts.

Done When: A tested helper covers 0-5 dice slot positions, held/unheld sorting inputs, and vault positions. Dice battle runtime behavior is unchanged in code review and Unity play check is requested.

### 10. Medium - Extract Enemy Defense Resolution

Goal: Separate defense outcome calculation from enemy counterattack presentation.

Context: `EnemyCounterAttackDirector.ConfirmDefense()` currently reads dice, evaluates defense, computes damage, writes logs/UI, and schedules animation.

Constraints: First pass extracts calculation only: player dice values + enemy result + rank -> blocked/reduction/base damage/final damage/description. Leave animation, logging, and UI flow in place.

Done When: `EnemyCounterAttackDirector` delegates to a tested resolver, existing `DefenseCalculator` behavior remains unchanged, and EditMode tests cover duplicate subset defense, combo defense, and damage multiplier cases.

### 11. Medium - Extract Mahjong Wait Reveal Policy

Goal: Move enemy wait display reveal decisions out of `MahjongBattleController`.

Context: Reveal behavior depends on enemy rank, forced intuition, and rank-3 per-turn chance. That is gameplay policy, not tile UI construction.

Constraints: Preserve current rules: rank 1-2 always reveal, rank 3 chance or forced reveal, rank 4-5 hidden. Do not change Mahjong tile prefabs, scene builders, or serialized fields.

Done When: A pure policy is covered by EditMode tests, and `MahjongBattleController` only applies the returned reveal state.

### 12. Medium - Split Debug Command Parsing From Execution

Goal: Make debug command parsing testable without scene lookup or global state mutation.

Context: `DebugCommandProcessor` parses commands, finds scene controllers, mutates `GameSessionManager`, and loads scenes in one static class.

Constraints: Preserve command text and behavior. First introduce a parse result or execution plan. Do not remove existing commands or change console UI.

Done When: Parse tests cover `/setdice`, `/kill`, `/sprite`, `/stage`, `/nextround`, and `/help`, while runtime execution still routes through the current console.

### 13. High - Replace Builder Field Strings With Safer Wiring Contracts

Goal: Reduce serialized-field rename risk in scene builders.

Context: `SceneBuilderUtility.SetField()` string names are a central fragility point.

Constraints: Do this incrementally by one target component. Avoid an all-builder rewrite. Do not hand-edit generated scenes. Do not rename serialized fields as part of the first pass.

Done When: One high-risk target has centralized field-name constants or a typed builder helper, matching Editor tests, and a Unity Editor scene-builder verification step is documented.

### 14. High - Split `SceneBuilderUtility` Into Focused Editor Helpers

Goal: Reduce editor-side god object size and blast radius.

Context: `SceneBuilderUtility` owns generic UI helpers, battle-specific construction, asset loading, audio setup, and build settings.

Constraints: Move one responsibility at a time. Keep public behavior and menu builders compiling. Shared helpers remain under `Assets/Editor/`. Do not change generated scene output intentionally.

Done When: One extraction, such as battle UI builders or audio setup, lands with no behavior change, Editor tests pass, and the user verifies scene builder menu compilation in Unity.

### 15. High - Split Enemy Counterattack Flow Into Smaller Collaborators

Goal: Separate counterattack flow, defense state, dice overlay, and attack animation concerns.

Context: `EnemyCounterAttackDirector` is difficult to test because logic, coroutines, UI, animation, and session damage are intertwined.

Constraints: Start only after task 10. Preserve animation timing, battle log messages, serialized fields, and builder wiring unless explicitly scoped.

Done When: One collaborator is extracted with tests or a clear Unity play-check path, and Dice battle still completes a full enemy counterattack.

### 16. High - Split Mahjong Battle Controller By Policy And Presentation

Goal: Move one Mahjong battle policy out of `MahjongBattleController` without changing UI wiring.

Context: Mahjong core rules have tests, but the scene controller owns discard flow, enemy attacks, wait reveal policy, win/partial attack, animation, and debug hooks.

Constraints: Start after task 11. Extract one tested policy/service per PR. Do not change tile prefab wiring, scene builders, or serialized field names.

Done When: One controller responsibility is delegated to a tested non-MonoBehaviour class and the Mahjong battle scene still reaches win/defeat in Unity play verification.

### 17. High - Normalize Scene Transition Constants And Session Writes

Goal: Centralize scene names and transition-side session writes.

Context: Scene names and session mutations appear in Explore, battle controllers, player death animation, and debug commands.

Constraints: `CurrentEventIndex` must remain owned by `GameExploreController`. Do not introduce an async scene framework. Do not rename scene assets.

Done When: Scene-name constants have one runtime source, transition helpers preserve victory/cancel/defeat behavior, and verification covers normal victory, cancel, player defeat, `/stage`, and `/nextround`.

## Do Not Touch Yet

- `.unity` scene files and `Assets/Scenes/`.
- `.prefab` files and generated dice prefab output.
- `.meta` files, unless the user explicitly approves a task that creates/moves/renames a Unity asset.
- `ProjectSettings/`, `Packages/`, `Library/`, `Temp/`, `UserSettings/`.
- Sprite/audio/texture asset folders such as `Assets/Player/`, `Assets/Mobs/`, `Assets/Mahjong/`, `Assets/UI/`, `Assets/Textures/`, and `Assets/Se/`.
- Scene builder output behavior, except in High-risk builder tasks.
- Serialized field names and public Unity callback method names.
- Namespace structure, assembly definitions, file/class names, asset paths, and scene names.
- Broad rewrites of `SceneBuilderUtility`, `EnemyCounterAttackDirector`, `MahjongBattleController`, or `GameExploreController`.
