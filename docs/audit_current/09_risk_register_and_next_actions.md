# Risk Register And Next Actions

- Post-audit closure date: 2026-06-12 KST
- Current HEAD at closure: `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Delta commits covered: `e1337d16`, `d007d473`, `311ae2e6`

## Current Status Summary After Master Delta

| Risk | Current status after master delta | Closure note |
|---|---|---|
| R-001 | Partially mitigated | Latest runtime and Hold'em player builds pass, but validation/settings churn remains open. |
| R-002 | Partially mitigated | Latest `HEAD` tracks six scene files/metas and build settings are buildable, but generated scene churn remains open. |
| R-003 | Open | Foreground remains broadly dirty outside this docs-only closure. |
| R-004 | Open | Latest runtime validation still dirtied tracked URP/project settings. |
| R-005 | Partially mitigated | Hold'em is promoted and validated, but builder scene/settings churn remains open. |
| R-006 | Partially mitigated | Promoted runtime asset references are tracked and tested, but manual visual/provenance risks remain. |
| R-007 | Partially mitigated | Primary D6 mine chain is tracked; broad imported fallback `Dice_d6.prefab` remains ignored by design. |
| R-008 | Open | Sprite pipeline promotion remains potentially destructive and still needs explicit approval per task. |
| R-009 | Partially mitigated | This audit closure reduces drift, but older non-audit docs are not all rewritten. |
| R-010 | Open | Node-map/shop/relic terminology still needs scope clarity and manual QA. |
| R-011 | Partially mitigated | Selected formerly dirty areas were promoted/validated; unrelated dirty/untracked foreground files remain. |

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: current audit findings from code, assets, build settings, validation, and pipeline scans.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## R-001

- Title: Clean commit player build is blocked by invalid scene path
- Current status after master delta: Partially mitigated
- Current evidence: latest Hold'em and runtime asset Windows Standalone x64 builds exited 0 and produced executables; runtime build log contains `Build Finished, Result: Success.` Initial invalid-scene-path failure remains historical baseline for `e6de7c9`.
- Remaining caveat: not fully closed because latest validation still dirtied tracked URP/project settings, and Hold'em builder validation still reports scene/settings churn.
- Classification: scene/build
- Evidence: Unity player build log reports `'' is an incorrect path for a scene file. BuildPlayer expects paths relative to the project folder.`
- Impact: No Windows Standalone executable can be produced from clean validation.
- Risk level: Critical
- Recommended next task: Audit and repair build settings/scene source-of-truth in a dedicated high-risk scene/build task.
- Allowed files for that future task: `ProjectSettings/EditorBuildSettings.asset`, `Assets/Editor/*SceneBuilder.cs`, `Assets/Editor/SceneBuilderUtility.cs`, generated scene outputs only if explicitly approved.
- Forbidden files: unrelated runtime scripts, unrelated assets, `.meta` manual edits.
- Required validation: detached validation worktree, scene builder regeneration if approved, EditMode tests, Windows player build.
- Stop condition: Unity changes tracked files unexpectedly outside the approved file list, or build still receives missing/empty scene paths.
- Human decision needed? yes

## R-002

- Title: HEAD tracks no scene files while runtime/build settings require scenes
- Current status after master delta: Partially mitigated
- Current evidence: latest `HEAD` tracks `Assets/Scenes/{MainMenu,CharacterSelect,GameExploreScene,DiceBattleScene,MahjongBattleScene,HoldemBattleScene}.unity` and corresponding `.meta` files; current build settings list those six scenes.
- Remaining caveat: scene source-of-truth is not fully closed because builder regeneration can still dirty generated scene/settings output.
- Classification: scene/build
- Evidence: `git ls-files Assets/Scenes` is empty; build settings and runtime scene strings reference `Assets/Scenes/*.unity`.
- Impact: Clean checkouts cannot reliably build or run scenes without local generated artifacts.
- Risk level: Critical
- Recommended next task: Decide whether generated scenes remain untracked or selected scenes become tracked artifacts.
- Allowed files for that future task: scene builder docs/build settings/scene builder code/generated scene files if explicitly approved.
- Forbidden files: gameplay code changes unrelated to scene policy.
- Required validation: regenerate scenes in validation, compare builder output, run EditMode and player build.
- Stop condition: generated scenes differ from builders without documented reason.
- Human decision needed? yes

## R-003

- Title: Foreground checkout is heavily dirty
- Current status after master delta: Open
- Current evidence: `git status --porcelain` still reports many modified tracked files and untracked paths outside `docs/audit_current/**`.
- Remaining caveat: this closure did not triage or revert foreground source/assets.
- Classification: scope ambiguity
- Evidence: 105 modified tracked files, 2893 untracked paths, 59690 ignored paths.
- Impact: Current foreground behavior may not match clean commit validation; audits and fixes can mix unrelated work if not scoped tightly.
- Risk level: High
- Recommended next task: Establish a foreground change triage plan before implementation work.
- Allowed files for that future task: status report docs or explicitly selected source files per task.
- Forbidden files: broad revert/delete/reset operations without explicit approval.
- Required validation: status snapshot before and after each task.
- Stop condition: unrelated dirty files overlap with the task's target files and intent is unclear.
- Human decision needed? yes

## R-004

- Title: Validation worktree becomes dirty during Unity validation
- Current status after master delta: Open
- Current evidence: `13_runtime_asset_source_of_truth_validation_result.md` records tracked dirty URP/project settings after runtime validation, including `Assets/Settings/UniversalRP.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset`, and `ProjectSettings/ProjectSettings.asset`.
- Remaining caveat: latest tests/build passed, but validation is classified as `pass-with-validation-hygiene-risk`.
- Classification: validation
- Evidence: Unity modified URP/project settings and created `Assets/Resources/**` performance test files in validation worktree.
- Impact: Validation has hygiene risk; tracked changes must not be copied back accidentally.
- Risk level: High
- Recommended next task: Identify why Unity/Performance Testing writes these files and decide whether settings should be normalized.
- Allowed files for that future task: validation scripts/docs, Unity settings only if explicitly targeted.
- Forbidden files: copying validation-generated settings into foreground without review.
- Required validation: repeat validation in disposable worktree and record post-run `git status --porcelain`.
- Stop condition: tracked files change outside approved list.
- Human decision needed? yes

## R-005

- Title: Holdem documentation and foreground prototype conflict with tracked runtime route
- Current status after master delta: Partially mitigated
- Current evidence: `e1337d16` promotes Hold'em scripts, builder, assets, tests, scene, build settings, and route; `GameExploreController.ResolveBattleSceneName(CharacterType.Holdem)` returns `HoldemBattleScene`; Hold'em EditMode 130/130 and player build passed.
- Remaining caveat: builder validation still dirties `Assets/Scenes/HoldemBattleScene.unity` and settings, so source-of-truth hygiene remains open.
- Classification: runtime / documentation
- Evidence: `GameExploreController.ResolveBattleSceneName` routes Holdem to `DiceBattleScene`; Holdem files/scene/builder are foreground untracked; old docs claim Holdem implementation.
- Impact: Planning may treat Holdem as implemented when clean runtime does not.
- Risk level: High
- Recommended next task: Decide whether Holdem is in scope; either promote and validate it intentionally or mark it historical/prototype.
- Allowed files for that future task: `Assets/Scripts/Holdem/**`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Holdem/**`, `Assets/Scripts/Explore/GameExploreController.cs`, scene/build settings if explicitly approved.
- Forbidden files: unrelated battle systems and unrelated art promotion.
- Required validation: EditMode tests, scene generation, player build, manual battle smoke test.
- Stop condition: Holdem route changes without tracked scene/assets readiness.
- Human decision needed? yes

## R-006

- Title: Runtime asset references are missing, untracked, ignored, or count-mismatched
- Current status after master delta: Partially mitigated
- Current evidence: `311ae2e6` tracks the runtime asset source-of-truth groups and adds `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks`; latest runtime full EditMode passed 132/132 and player build succeeded.
- Remaining caveat: visual QA, provenance review, source media exclusion policy, and broad unrelated dirty assets remain outside this closure.
- Classification: asset reference
- Evidence: Skeleton Attack/Hit folders have 0 direct PNGs; Goblin Hit has 29 direct PNGs while code references 69; dice prefabs are ignored; Stage2 Golem/WaterCannon and Mahjong red-five assets are untracked.
- Impact: Generated scenes or runtime presentation can fail visually or load fallbacks.
- Risk level: High
- Recommended next task: Run an approved asset reference repair task from `05_runtime_asset_reference_manifest.md`.
- Allowed files for that future task: explicitly listed asset paths and corresponding `.meta` files if Unity-generated and approved, stage data only if changing references is approved.
- Forbidden files: broad asset folder moves/deletes, manual `.meta` edits, unapproved image processing.
- Required validation: read-only manifest rerun, Unity import check in validation, EditMode tests, targeted visual/manual QA.
- Stop condition: required fix needs asset generation/promotion not approved for that task.
- Human decision needed? yes

## R-007

- Title: Dice builder inputs are ignored/untracked
- Current status after master delta: Partially mitigated
- Current evidence: `311ae2e6` tracks `Assets/Dices/D6_mine.png`, `Dice_Tray.png`, `Generated/D6Mine*`, and `Prefabs/Dice_d6_mine.prefab` with `.gitignore` exceptions; `git check-ignore -v -n` shows `Dice_d6_mine.prefab` is not ignored.
- Remaining caveat: broad imported fallback `Assets/Dices/Prefabs/Dice_d6.prefab` remains ignored by `.gitignore` and is not part of the runtime source-of-truth commit.
- Classification: asset reference / scene/build
- Evidence: `Assets/Dices/D6_mine.png`, `Assets/Dices/Prefabs/Dice_d6_mine.prefab`, and fallback dice prefab exist only as ignored foreground assets.
- Impact: Clean checkout cannot rebuild dice battle scene/prefab inputs reliably.
- Risk level: High
- Recommended next task: Decide dice asset tracking/import policy.
- Allowed files for that future task: `Assets/Dices/**`, `Assets/Editor/DicePrefabBuilder.cs`, relevant generated prefab paths if approved.
- Forbidden files: runtime battle logic unless needed by an approved task.
- Required validation: builder run in validation worktree, EditMode tests, player build after scene fixes.
- Stop condition: builder needs to create or delete `.meta` files without approval.
- Human decision needed? yes

## R-008

- Title: Sprite pipeline promotion can mutate runtime assets and `.meta` files
- Current status after master delta: Open
- Current evidence: no sprite generation, image processing, or promotion command was run during this closure; existing promotion scripts remain capable of writing into `Assets/**` when explicitly invoked.
- Remaining caveat: future asset promotion still needs explicit scope, target asset ID, review, and validation.
- Classification: Grok pipeline
- Evidence: `promote_selected_asset.py --commit` copies selected frames into `Assets/**` and can remove stale PNG/meta files; batch 480p script can write missing runtime assets with explicit flag.
- Impact: Asset promotion can create large diffs and break GUID/import consistency if run casually.
- Risk level: High
- Recommended next task: Require explicit promotion task with target asset id, runtime owner, backup policy, and validation.
- Allowed files for that future task: selected `SpritePipelineWork/<asset_id>/**`, exact runtime destination folder, Unity-generated `.meta` files if approved.
- Forbidden files: unrelated asset folders, scene/project settings unless promotion requires import validation.
- Required validation: dry run, review packet approval, commit-mode run only if approved, Unity import validation, manifest update.
- Stop condition: approval manifest is missing or target path cannot be tied to a runtime owner.
- Human decision needed? yes

## R-009

- Title: Old markdown can mislead implementation planning
- Current status after master delta: Partially mitigated
- Current evidence: `14_post_audit_master_delta_closure.md` and updated audit docs now distinguish initial baseline from latest master state.
- Remaining caveat: older non-audit docs were not all rewritten, so they can still mislead if read without this closure.
- Classification: documentation
- Evidence: `docs/00_actual_project_audit.md`, `docs/holdem_battle.md`, `REFACTOR_BACKLOG.md`, and validation result docs contain claims contradicted or superseded by current code/assets/validation.
- Impact: Future tasks may start from stale assumptions.
- Risk level: Medium
- Recommended next task: Mark or rewrite stale docs after this audit is reviewed.
- Allowed files for that future task: existing `.md` files only.
- Forbidden files: source/assets/settings.
- Required validation: none for docs-only, but cite current audit evidence.
- Stop condition: proposed doc edit attempts to assert unvalidated implementation state.
- Human decision needed? no

## R-010

- Title: Node map and shop-like terminology can blur implemented scope
- Current status after master delta: Open
- Current evidence: `311ae2e6` tracks explore map UI PNG assets, but no delta commit implements shop/relic/joker/Balatro chips-mult systems, and manual map UX QA was not performed.
- Remaining caveat: map presentation should remain separate from claims about shop/relic/joker systems.
- Classification: scope ambiguity / runtime
- Evidence: Foreground `GameExploreController` contains map-node presentation and untracked map icons; no implemented shop/relic/joker/Balatro chips/mult systems were found.
- Impact: Feature descriptions can overstate the current game loop and confuse backlog priority.
- Risk level: Medium
- Recommended next task: Define terms for current explore UI versus future node-map/shop/relic systems.
- Allowed files for that future task: docs first; runtime only if a separately approved feature task follows.
- Forbidden files: implementing shop/relic/joker as part of documentation cleanup.
- Required validation: docs-only none; runtime changes require EditMode and manual QA.
- Stop condition: task scope shifts from classification to feature implementation.
- Human decision needed? yes

## R-011

- Title: Clean validation does not cover dirty foreground additions
- Current status after master delta: Partially mitigated
- Current evidence: selected formerly foreground areas were promoted and validated in narrow commits: Hold'em, Mahjong back tiles, and runtime asset source-of-truth.
- Remaining caveat: `git status --porcelain` still shows unrelated dirty/untracked foreground files that are not covered by latest validation.
- Classification: validation / scope ambiguity
- Evidence: validation worktree was detached at clean commit; foreground untracked Holdem, scene, asset, and pipeline script files were not part of clean validation.
- Impact: Passing EditMode tests do not prove foreground prototype files compile or behave.
- Risk level: Medium
- Recommended next task: For any foreground addition selected for promotion, create an isolated validation branch/worktree containing only that change set.
- Allowed files for that future task: selected files for one feature/asset task.
- Forbidden files: broad sweep of all dirty/untracked files.
- Required validation: compile/EditMode, player build where relevant, post-run status.
- Stop condition: selected file set cannot be isolated from unrelated dirty work.
- Human decision needed? yes

## Suggested Approval Queue

Closed or moved to monitor:

- Hold'em scope decision: promoted and validated in `e1337d16`; monitor builder churn.
- Runtime asset reference repair using the manifest: implemented and validated in `311ae2e6`; monitor visual QA/provenance and validation hygiene.
- Initial invalid scene path player build blocker: latest player builds pass; keep hygiene caveat.

Current recommended next actions:

1. Validation hygiene/settings churn: identify why Unity validation dirties URP/project settings and decide whether settings should be normalized in a targeted task.
2. Hold'em scene builder determinism: investigate `HoldemBattleScene` YAML/file-ID/order churn from builder regeneration.
3. Manual QA: run player-build launch and visual smoke tests for Dice, Mahjong, Hold'em, scene transitions, stage sprites, map presentation, audio, and debug UI.
4. Stale markdown cleanup: update or mark historical non-audit docs that still contain old Hold'em fallback, build failure, scene, or asset claims.
5. Foreground triage: isolate or discard unrelated dirty/untracked foreground work before further implementation.
