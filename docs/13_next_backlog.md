# Next Backlog

Factual baseline:

- `docs/00_actual_project_audit.md`
- `docs/10_validation_triage.md`
- `docs/11_project_decisions.md`
- `docs/12_v0_1_scope.md`
- `REFACTOR_BACKLOG.md`

This backlog ranks the next operating tasks. It does not authorize broad refactors or feature expansion.

## P0

### 1. Scene/build hygiene

| Field | Detail |
|---|---|
| Task | Establish the exact v0.1 runtime scene list and classify each required scene as tracked stable or generated with documented regeneration. |
| Why | Current `Assets/Scenes/` is ignored while local scene files exist, and clean checkout behavior is ambiguous. |
| Files likely touched | `docs/02_unity_scene_and_object_construction.md`, `docs/11_project_decisions.md`, `docs/12_v0_1_scope.md`, possibly `.gitignore`, `ProjectSettings/EditorBuildSettings.asset`, and selected `Assets/Scenes/*.unity` only in a later explicit high-risk task. |
| Owner | Human + Codex |
| Acceptance criteria | Scene ownership table exists; required runtime scenes are listed; each scene has tracked/generated status; generation procedure exists for generated scenes; no obsolete scene is required for v0.1 runtime validation. |
| Risk level | High |
| Stop condition | Stop before editing `.gitignore`, scenes, `.meta`, or ProjectSettings unless the task explicitly authorizes those files. |

### 2. Validation baseline

| Field | Detail |
|---|---|
| Task | Create a reliable EditMode validation baseline from an isolated worktree. |
| Why | Current focused tests fail, and validation command/worktree hygiene has known issues. |
| Files likely touched | `docs/10_validation_triage.md`, validation docs, existing test files only when fixes are explicitly approved. |
| Owner | Codex |
| Acceptance criteria | Unity command is documented; `-runTests` behavior is verified; focused failing tests are fixed or waived with classifications; full EditMode suite result is recorded; validation worktree status is checked after run. |
| Risk level | Medium |
| Stop condition | Stop if Unity modifies tracked files unexpectedly or if failures indicate runtime behavior ambiguity requiring human decision. |

### 3. Stale build settings cleanup

| Field | Detail |
|---|---|
| Task | Remove or intentionally document stale enabled build settings entries for `SampleScene`, `YachtDice`, and `DiceTest`. |
| Why | They are enabled in build settings but missing locally and not produced by current scene builders. |
| Files likely touched | `ProjectSettings/EditorBuildSettings.asset`, `docs/02_unity_scene_and_object_construction.md`, `docs/12_v0_1_scope.md`. |
| Owner | Human + Codex |
| Acceptance criteria | Build settings contain only intentional v0.1 scene entries, or every stale-looking entry has owner, purpose, and validation behavior documented. |
| Risk level | High |
| Stop condition | Stop if any stale scene is claimed as intentional but lacks owner or regeneration/tracking plan. |

### 4. Tracked/generated scene policy enforcement

| Field | Detail |
|---|---|
| Task | Enforce the accepted Hybrid policy after scene ownership is decided. |
| Why | Policy without enforcement still leaves clean checkout and Codex behavior ambiguous. |
| Files likely touched | `.gitignore`, `Assets/Scenes/*.unity`, `Assets/Scenes/*.unity.meta`, `docs/02_unity_scene_and_object_construction.md`, `docs/12_v0_1_scope.md`, possibly builder docs. |
| Owner | Human + Codex |
| Acceptance criteria | Stable tracked scenes, if any, are committed with `.meta`; generated scenes, if any, have documented builder regeneration; Codex rules say which source is authoritative per scene; validation confirms required scenes exist before build. |
| Risk level | High |
| Stop condition | Stop if enforcing policy would require manual scene edits or unapproved `.meta` churn. |

## P1

### 5. Dice battle loop stabilization

| Field | Detail |
|---|---|
| Task | Stabilize the existing dice battle loop without adding new mechanics. |
| Why | Dice is an in-scope v0.1 combat mode and should complete attack, defense, victory, cancel, and defeat paths reliably. |
| Files likely touched | Existing Battle EditMode tests, `Assets/Scripts/Battle/*`, possibly builder docs if scene wiring assumptions are clarified. |
| Owner | Codex |
| Acceptance criteria | Dice battle focused tests pass or are classified; manual QA covers roll, hold, attack, enemy counterattack, victory return, cancel, and defeat; no scene files are hand-edited. |
| Risk level | Medium to High |
| Stop condition | Stop if a fix requires serialized field renames, builder rewiring, or gameplay balance changes outside the stated bug. |

### 6. Mahjong battle loop stabilization

| Field | Detail |
|---|---|
| Task | Stabilize the existing Mahjong battle loop without adding unimplemented rank 4-5 design or new rules systems. |
| Why | Mahjong is an in-scope v0.1 combat mode with one known yaku test ambiguity. |
| Files likely touched | Existing Mahjong tests, `Assets/Scripts/Mahjong/*`, documentation for accepted yaku behavior. |
| Owner | Codex + Human for design ambiguity |
| Acceptance criteria | `Toitoi`/`Suuankou` ambiguity is resolved; Mahjong focused tests pass or have accepted waivers; manual QA covers draw/discard, partial attack, full win path, enemy trigger, victory return, cancel, and defeat. |
| Risk level | Medium to High |
| Stop condition | Stop if resolving a test requires open-meld modeling, yakuman scoring policy change, or new enemy rank design without human decision. |

### 7. Explore -> battle -> return loop QA

| Field | Detail |
|---|---|
| Task | Validate the implemented `MainMenu -> CharacterSelect -> GameExploreScene -> battle -> GameExploreScene` loop for Dice and Mahjong. |
| Why | The v0.1 product is the playable loop, not isolated battle scenes. |
| Files likely touched | QA notes, docs, possibly small runtime/test fixes if bugs are found and approved. |
| Owner | Human + Codex |
| Acceptance criteria | Dice and Mahjong can each enter battle from explore and return after victory; item-box and boss rounds remain reachable; session state is not corrupted between transitions. |
| Risk level | Medium |
| Stop condition | Stop if scene/build policy is unresolved or required runtime scenes are missing. |

### 8. Victory/defeat/restart QA

| Field | Detail |
|---|---|
| Task | Validate final victory, battle defeat, and new-game restart behavior. |
| Why | Cross-scene session reset and end states are high-risk in a static session-state architecture. |
| Files likely touched | QA docs, existing session/battle tests, small runtime fixes only if explicitly approved. |
| Owner | Human + Codex |
| Acceptance criteria | Player defeat routes correctly; final victory routes correctly; starting a new game resets hearts, power-ups, current event index, stage state, battle enemies, boss flag, and last battle result. |
| Risk level | Medium |
| Stop condition | Stop if behavior is not designed or if a fix would move `CurrentEventIndex` ownership out of `GameExploreController`. |

## P2

### 9. Asset pipeline manifest

| Field | Detail |
|---|---|
| Task | Create a manifest that classifies source images, Unity-ready PNGs, MP4s, raw frames, transparent outputs, and provenance docs. |
| Why | The repo has many generated and intermediate assets, but deletion/tracking policy is not formal. |
| Files likely touched | New or existing asset docs, `docs/assets.md`, `docs/grok-imagine-sprite-prompts.md`. |
| Owner | ChatGPT Project + Codex |
| Acceptance criteria | Asset categories are defined; source-of-truth candidates are listed; intermediate categories are named; no deletion is performed; unknowns are explicitly marked. |
| Risk level | Low |
| Stop condition | Stop before moving, deleting, renaming, or reimporting assets. |

### 10. Generated sprite import policy

| Field | Detail |
|---|---|
| Task | Document how generated sprite frames should be imported, named, and referenced by builders/runtime. |
| Why | Builders and stage data depend on hard-coded sprite paths and suffix conventions such as `_transparent_clean`. |
| Files likely touched | `docs/assets.md`, possible new content pipeline doc, `docs/grok-imagine-sprite-prompts.md`. |
| Owner | ChatGPT Project + Codex |
| Acceptance criteria | Naming conventions are documented as current observed policy; Unity-ready transparent PNGs are distinguished from raw/intermediate frames; `.meta` handling rules are stated; no import settings are changed. |
| Risk level | Low |
| Stop condition | Stop if policy requires asset renames, folder moves, or `.meta` regeneration. |

### 11. Docs cleanup

| Field | Detail |
|---|---|
| Task | Align README and scoped docs with accepted v0.1 decisions after policy tasks are implemented. |
| Why | Some older docs say generated-only scenes, while accepted baseline now uses Hybrid. README includes useful facts but needs policy alignment after enforcement. |
| Files likely touched | `README.md`, `AGENTS.md`, `.claude/rules/scene-builder.md`, docs under `docs/`. |
| Owner | ChatGPT Project + Codex |
| Acceptance criteria | Docs separate implemented facts, accepted decisions, deferred ideas, and rejected v0.1 features; no unimplemented feature is described as implemented. |
| Risk level | Low to Medium |
| Stop condition | Stop before changing operational rules if the source-of-truth owner is unclear. |

## P3

### 12. Hold'em validation and scene policy

| Field | Detail |
|---|---|
| Task | Validate the first-pass Hold'em implementation and decide whether `HoldemBattleScene` is tracked stable or generated-only. |
| Why | Hold'em now has source-level rules, controller, tests, builder, and routing, but still needs isolated Unity validation and scene/build-settings alignment. |
| Files likely touched | Product docs first; later `.gitignore`, `ProjectSettings/EditorBuildSettings.asset`, and `Assets/Scenes/HoldemBattleScene.unity` plus `.meta` only if a high-risk scene-policy task explicitly approves generated artifacts. |
| Owner | Human + Codex |
| Acceptance criteria | Isolated validation records compile/test/build/play status; scene policy states tracked artifact with `.meta` or generated-only regeneration; Dice and Mahjong routing remain unchanged. |
| Risk level | High |
| Stop condition | Stop before editing ProjectSettings, generated scene YAML, or `.meta` files unless the task explicitly authorizes those files. |

### 13. Node map decision

| Field | Detail |
|---|---|
| Task | Decide whether the current linear stage-round structure should remain or later become a node-map macro loop. |
| Why | Node maps require new data, UI, routing, session state, and validation. |
| Files likely touched | Product docs first; later stage/explore runtime and scene builders only if approved. |
| Owner | Human |
| Acceptance criteria | Decision states whether node map is rejected, deferred, or accepted for a post-v0.1 milestone. |
| Risk level | High if implemented |
| Stop condition | Stop if the request would replace current linear progression before v0.1 is stable. |

### 14. Shop/relic/joker decision

| Field | Detail |
|---|---|
| Task | Decide whether reward systems expand beyond current item-box `PowerUpType`. |
| Why | Shop/relic/joker systems would change reward, persistence, UI, and combat modifiers. |
| Files likely touched | Product docs first; later `PowerUpType`, `GameSessionManager`, `GameExploreController`, builders, and tests if approved. |
| Owner | Human |
| Acceptance criteria | Decision states whether current item boxes remain the only v0.1 reward layer and what post-v0.1 system is desired. |
| Risk level | High if implemented |
| Stop condition | Stop if implementation would introduce new economy or modifier architecture during v0.1 stabilization. |

### 15. Balatro-style scoring decision

| Field | Detail |
|---|---|
| Task | Decide whether future combat should use a shared chips/mult scoring model or keep separate dice/mahjong scoring. |
| Why | A shared scoring model would be a major cross-combat architecture change. |
| Files likely touched | Product docs first; later `DamageCalculator`, `YakuEvaluator`, `MahjongDamageTable`, combat UI, and tests if approved. |
| Owner | Human |
| Acceptance criteria | Decision states whether chips/mult is rejected, deferred, or accepted for a post-v0.1 milestone. No v0.1 code changes result from this decision alone. |
| Risk level | High if implemented |
| Stop condition | Stop if implementation would rewrite scoring before dice/mahjong loops and tests are stable. |
