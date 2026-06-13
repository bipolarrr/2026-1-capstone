# Current State Audit Index

- Post-audit closure date: 2026-06-12 KST
- Current branch/head at closure: `master` / `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Delta commits covered by this closure: `e1337d16`, `d007d473`, `311ae2e6`
- Closure source: current git state, current `HEAD` source inspection, existing validation docs, and raw validation XML/log/exe artifacts where available.

## Current Status After Post-Audit Closure

Latest local `master` includes the three requested delta commits. `git merge-base --is-ancestor` verified that `e1337d16`, `d007d473`, and `311ae2e6` are all in current `HEAD` ancestry.

Validation status by latest evidence:

- Hold'em: promoted into tracked runtime/builder/assets/scene/build settings. Raw validation artifact exists; full EditMode passed 130/130, `HoldemRoutesToHoldemBattleScene` passed, and Windows player build produced `CapstoneHoldemPromotion.exe`. Classification: implemented and validated with builder scene/settings churn risk.
- Mahjong: `tile_back_acorn` is the intended database-backed hidden tile back. Raw validation artifacts exist; focused Mahjong EditMode passed 43/43 and full EditMode passed 131/131. Classification: implemented and validated.
- Runtime assets: source-of-truth asset tracking and the runtime asset reference test are in `311ae2e6`. Raw validation artifacts exist; full EditMode passed 132/132 including `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks`, and Windows player build produced `CapstoneRuntimeAssets.exe`. Classification: implemented and validated with validation hygiene risk.
- Player build: the initial audit player build failure is historical baseline. Latest runtime asset validation player build exited 0 and the build log contains `Build Finished, Result: Success.`
- Validation hygiene: still open. Latest runtime validation dirtied tracked URP/project settings in the disposable worktree. Those settings changes were not copied back.

Latest runtime loop summary from current `HEAD`:

`MainMenu` -> `CharacterSelect` -> `GameExploreScene` -> `DiceBattleScene` / `MahjongBattleScene` / `HoldemBattleScene` -> `GameExploreScene` on victory or `MainMenu` on defeat.

Battle routing from `GameExploreController.ResolveBattleSceneName`:

- Dice/default -> `DiceBattleScene`
- Mahjong -> `MahjongBattleScene`
- Holdem -> `HoldemBattleScene`

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: actual runtime code, editor scene/build generators, tracked and foreground asset references, Unity validation logs, sprite pipeline scripts/manifests/provenance docs.
- Unity validation run: yes, in `C:\Users\song\Desktop\Capstone_audit_validation_20260611_204534`
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Documents

1. `00_audit_run_record.md` - run metadata, dirty state, validation paths, command results.
2. `01_repo_inventory_current.md` - tracked/untracked/ignored inventory and evidence inclusion rules.
3. `02_runtime_architecture_current.md` - actual runtime flow and implemented/partial/not found classification.
4. `03_scene_and_build_current.md` - build settings, scene files, builders, contracts, build readiness.
5. `04_validation_current.md` - Unity EditMode and Windows player build validation results.
6. `05_runtime_asset_reference_manifest.md` - runtime/builder/stage asset reference manifest.
7. `06_grok_and_sprite_pipeline_current.md` - Grok provenance and sprite pipeline state.
8. `07_existing_markdown_drift_matrix.md` - stale documentation risk matrix.
9. `08_implemented_vs_documented_current.md` - feature implementation matrix.
10. `09_risk_register_and_next_actions.md` - risk register and proposed follow-up tasks.
11. `11_runtime_asset_reference_repair_result.md` - historical dirty validation/repair note superseded by the narrower runtime source-of-truth commit.
12. `12_holdem_promotion_validation_result.md` - Hold'em promotion validation details and builder churn caveat.
13. `13_runtime_asset_source_of_truth_validation_result.md` - runtime asset source-of-truth validation details for `311ae2e6` (currently untracked at closure).
14. `14_post_audit_master_delta_closure.md` - current closure summary for post-audit master delta commits.

## Initial Audit Baseline At `e6de7c9`

The following conclusions are preserved as the 2026-06-11 initial audit baseline. They are not the latest master state after `e1337d16`, `d007d473`, and `311ae2e6`.

1. The foreground checkout is dirty: 105 modified tracked files, 2893 untracked paths, and 59690 ignored paths were observed by `git status --porcelain` and `git status --ignored --short` summary commands.
2. Unity EditMode tests pass on a clean detached validation worktree at commit `e6de7c9`: 82 total, 82 passed, 0 failed.
3. Windows Standalone x64 player build fails on the clean validation worktree with `'' is an incorrect path for a scene file. BuildPlayer expects paths relative to the project folder.`
4. Clean commit build settings reference scene paths under `Assets/Scenes`, but no `Assets/Scenes/*.unity` files are tracked in HEAD.
5. Foreground build settings and scene files differ from HEAD: foreground has untracked/ignored scene files and a modified build settings asset that includes `HoldemBattleScene`.
6. The current tracked runtime scene loop is MainMenu -> CharacterSelect -> GameExploreScene -> DiceBattleScene or MahjongBattleScene -> GameExploreScene/MainMenu.
7. Hold'em is not a current validated implemented battle loop: tracked `GameExploreController.ResolveBattleSceneName` routes `CharacterType.Holdem` to `DiceBattleScene`; Holdem controller/builder/scene files are foreground untracked.
8. Item-box power-ups exist through `PowerUpType` and `PowerUpRewardCatalog`; shop, relic, joker, and Balatro-style chips/mult systems were not found as implemented runtime systems.
9. Runtime asset references include several missing, ignored, or untracked dependencies, especially generated scenes, dice prefab inputs, Stage2 cave mob assets, Mahjong red-five tiles, and map icon assets.
10. The sprite pipeline is currently a mostly review/candidate pipeline unless explicit promotion or write flags are used; promotion to `Assets/**` is destructive and remains a separate approval task.

## Initial Validation Status

- EditMode: passed, 82/82, exit code 0.
- Player build: failed, exit code 1, no executable produced.
- Validation hygiene: risky. Unity modified tracked settings/assets and created untracked files inside the validation worktree.
- Validation output root: `C:\Users\song\Desktop\Capstone_validation_outputs\audit_current_20260611_204534`

## Latest Validation Status

| Area | Status | Evidence |
|---|---|---|
| Hold'em EditMode | Passed, 130/130 | `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\editmode\full-editmode-results.xml`; `12_holdem_promotion_validation_result.md` |
| Hold'em player build | Passed, executable produced | `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\player-build\standalone-build.log`; `CapstoneHoldemPromotion.exe` |
| Mahjong focused EditMode | Passed, 43/43 | `C:\Users\song\Desktop\Capstone_validation_outputs\mahjong_back_20260612_040025\editmode-mahjong\mahjong-editmode-results.xml` |
| Mahjong full EditMode | Passed, 131/131 | `C:\Users\song\Desktop\Capstone_validation_outputs\mahjong_back_20260612_040025\editmode-full\full-editmode-results.xml` |
| Runtime assets full EditMode | Passed, 132/132 | `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\editmode\full-editmode-results.xml`; `13_runtime_asset_source_of_truth_validation_result.md` |
| Runtime assets player build | Passed, executable produced | `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\player-build\standalone-build.log`; `CapstoneRuntimeAssets.exe` |
| Validation hygiene | Open risk | Runtime validation dirtied tracked URP/project settings in disposable worktree |

## Current Runtime Loop

Evidence from `Assets/Scripts/MainMenu/MainMenuController.cs`, `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, and `Assets/Scripts/Mahjong/MahjongBattleController.cs` shows this active loop:

`MainMenu` -> `CharacterSelect` -> `GameExploreScene` -> selected battle scene -> `GameExploreScene` on victory or `MainMenu` on defeat.

Battle routing is character-based:

- Mahjong -> `MahjongBattleScene`
- Dice -> `DiceBattleScene`
- Holdem -> `HoldemBattleScene` in current `HEAD`

## Current Scene And Build State

- Initial audit baseline: HEAD `ProjectSettings/EditorBuildSettings.asset` referenced missing scenes and no tracked `Assets/Scenes/*.unity` files existed.
- Latest master: `ProjectSettings/EditorBuildSettings.asset` references `MainMenu`, `CharacterSelect`, `GameExploreScene`, `DiceBattleScene`, `MahjongBattleScene`, and `HoldemBattleScene`.
- Latest master tracks the six runtime scene files and their `.meta` files under `Assets/Scenes`.
- Latest player builds passed for Hold'em and runtime asset validation.
- Remaining risk: builder validation still causes scene/settings churn, and Unity validation still dirties URP/project settings in disposable worktrees.

## Current Runtime Asset Reference State

Initial audit baseline: the project had direct runtime/builder/stage references to player sprites, mob sprites, backgrounds, dice prefab inputs, Mahjong tile assets, UI images, audio, and generated scenes, with several important references untracked, ignored, or missing.

Latest master: `311ae2e6` tracks selected runtime source-of-truth assets and adds a passing runtime asset reference test. `d007d473` tracks Mahjong red-five/honor/back tile assets and aligns hidden tile tests with `tile_back_acorn`. Use `05_runtime_asset_reference_manifest.md` for the detailed baseline and delta classifications.

## Current Grok / Sprite Pipeline State

Grok prompt/provenance docs and `SpritePipelineWork/**` artifacts show a manual/review-heavy asset generation process. Pipeline scripts default to writing under `SpritePipelineWork/**` for extraction, cleanup, review, selection, and upscaling. `promote_selected_asset.py --commit` and `batch_ai_assetize_480p_videos.py --write-missing-assets` can write into `Assets/**`; neither was run during this audit.

## Stale Documentation Risk

Several existing documents claim or imply implemented Hold'em routing, historical validation baselines, or asset completeness that no longer matches current code/assets. The highest-risk conflicts are documented in `07_existing_markdown_drift_matrix.md` and should be superseded by this audit before planning implementation work.

## Immediate Next Actions

1. Investigate and normalize Unity validation hygiene/settings churn in a dedicated task. Do not copy disposable validation-worktree settings changes into the foreground checkout without review.
2. Investigate Hold'em scene-builder regeneration churn for `Assets/Scenes/HoldemBattleScene.unity` and document whether generated scene YAML should be stabilized or accepted.
3. Run manual visual/playthrough QA for Dice, Mahjong, Hold'em, scene transitions, stage sprite presentation, and player build launch behavior.
4. Mark or rewrite stale non-audit markdown so old Hold'em fallback/build-failure/asset-risk claims do not override this closure.
5. Triage remaining broad foreground dirty/untracked files before any further implementation work.
