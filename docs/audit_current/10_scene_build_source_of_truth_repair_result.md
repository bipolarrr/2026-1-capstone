# Scene/build Source-of-truth Repair Result

- 작업 날짜: 2026-06-11 KST
- Repository commit/hash: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: current audit docs, `.gitignore`, `ProjectSettings/EditorBuildSettings.asset`, required runtime scene artifacts, validation worktree Unity logs/results.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Current source documents were `docs/audit_current/00_index.md` through `09_risk_register_and_next_actions.md`.

## 1. Pre-state Record

Required commands were run before edits:

- `git status --short`
- `git status --ignored --short`
- `git ls-files Assets/Scenes`

Observed pre-state:

- Foreground worktree was already dirty with many unrelated modified/untracked/ignored files.
- Relevant dirty/tracking state included `M .gitignore`, `M ProjectSettings/EditorBuildSettings.asset`, and no tracked files from `git ls-files Assets/Scenes`.
- Foreground scene files existed under `Assets/Scenes`, but the required runtime scenes were ignored by the then-current scene ignore rule.
- `ProjectSettings/EditorBuildSettings.asset` included the five runtime scenes plus `Assets/Scenes/HoldemBattleScene.unity` in the dirty foreground state.
- Clean audit evidence showed HEAD build settings also had stale/missing entries: `SampleScene`, `YachtDice`, and `DiceTest`.

## 2. Changed Files

Files changed or created by this task:

- `.gitignore`
- `ProjectSettings/EditorBuildSettings.asset`
- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/MainMenu.unity.meta`
- `Assets/Scenes/CharacterSelect.unity`
- `Assets/Scenes/CharacterSelect.unity.meta`
- `Assets/Scenes/GameExploreScene.unity`
- `Assets/Scenes/GameExploreScene.unity.meta`
- `Assets/Scenes/DiceBattleScene.unity`
- `Assets/Scenes/DiceBattleScene.unity.meta`
- `Assets/Scenes/MahjongBattleScene.unity`
- `Assets/Scenes/MahjongBattleScene.unity.meta`
- `docs/audit_current/10_scene_build_source_of_truth_repair_result.md`

No gameplay runtime logic was changed. `GameExploreController.ResolveBattleSceneName` was not modified.

Note: `.gitignore` already had unrelated dirty foreground changes before this task, including `!/[Aa]ssets/[Hh]oldem/**`. This task only changed the `Assets/Scenes` ignore policy block.

## 3. `.gitignore` Change Summary

Scene policy implemented:

- `Assets/Scenes/*` remains ignored by default.
- The five required runtime scenes and their `.meta` files are explicitly unignored and Git tracking-capable.
- Experimental/debug/prototype scenes remain ignored, including:
  - `Assets/Scenes/AnimationDebugScene.unity`
  - `Assets/Scenes/HoldemBattleScene.unity`

Final relevant ignore checks:

```text
.gitignore:104:!/[Aa]ssets/[Ss]cenes/MainMenu.unity
.gitignore:105:!/[Aa]ssets/[Ss]cenes/MainMenu.unity.meta
.gitignore:106:!/[Aa]ssets/[Ss]cenes/CharacterSelect.unity
.gitignore:107:!/[Aa]ssets/[Ss]cenes/CharacterSelect.unity.meta
.gitignore:108:!/[Aa]ssets/[Ss]cenes/GameExploreScene.unity
.gitignore:109:!/[Aa]ssets/[Ss]cenes/GameExploreScene.unity.meta
.gitignore:110:!/[Aa]ssets/[Ss]cenes/DiceBattleScene.unity
.gitignore:111:!/[Aa]ssets/[Ss]cenes/DiceBattleScene.unity.meta
.gitignore:112:!/[Aa]ssets/[Ss]cenes/MahjongBattleScene.unity
.gitignore:113:!/[Aa]ssets/[Ss]cenes/MahjongBattleScene.unity.meta
.gitignore:103:/[Aa]ssets/[Ss]cenes/* Assets/Scenes/HoldemBattleScene.unity
.gitignore:103:/[Aa]ssets/[Ss]cenes/* Assets/Scenes/AnimationDebugScene.unity
```

## 4. Final Build Settings Scene List

`ProjectSettings/EditorBuildSettings.asset` now contains only the required runtime scene set:

1. `Assets/Scenes/MainMenu.unity`
2. `Assets/Scenes/CharacterSelect.unity`
3. `Assets/Scenes/GameExploreScene.unity`
4. `Assets/Scenes/DiceBattleScene.unity`
5. `Assets/Scenes/MahjongBattleScene.unity`

There is no empty scene path in the final file.

## 5. Runtime Scene Artifacts

The required scene files and scene `.meta` files are now Git tracking-capable and appear as untracked files until staged:

```text
?? Assets/Scenes/CharacterSelect.unity
?? Assets/Scenes/CharacterSelect.unity.meta
?? Assets/Scenes/DiceBattleScene.unity
?? Assets/Scenes/DiceBattleScene.unity.meta
?? Assets/Scenes/GameExploreScene.unity
?? Assets/Scenes/GameExploreScene.unity.meta
?? Assets/Scenes/MahjongBattleScene.unity
?? Assets/Scenes/MahjongBattleScene.unity.meta
?? Assets/Scenes/MainMenu.unity
?? Assets/Scenes/MainMenu.unity.meta
```

The scene YAML was not hand-edited. The foreground scene files were copied from a validation worktree after Unity scene builders generated them.

Foreground scene SHA-256 hashes matched the generated validation worktree files after copy:

| Scene | Foreground equals validation-generated file |
|---|---|
| `MainMenu.unity` | yes |
| `CharacterSelect.unity` | yes |
| `GameExploreScene.unity` | yes |
| `DiceBattleScene.unity` | yes |
| `MahjongBattleScene.unity` | yes |

## 6. Removed Stale Scene Entries

Removed from build settings:

- `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/YachtDice.unity`
- `Assets/Scenes/DiceTest.unity`
- `Assets/Scenes/HoldemBattleScene.unity`

No empty scene path remains.

## 7. Hold'em Treatment

Hold'em included in build settings: no.

Reason:

- Hold'em is a product feature, but it is not narrowly promoted in this task.
- Current tracked runtime routing was not changed.
- `GameExploreController.ResolveBattleSceneName` still remains outside this task's edits.
- The foreground Hold'em scene/scripts/builder/assets are preserved but remain a follow-up feature promotion path.

Follow-up task:

- Promote Hold'em as a separate scoped feature task only when `Assets/Scripts/Holdem/**`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Holdem/**`, `Assets/Scenes/HoldemBattleScene.unity`, required `.meta` files, and `GameExploreController` routing can be included and validated together.

## 8. Scene Builder Generation

Validation worktree:

- Path: `C:\Users\song\c_sbv_20260611_1`
- Output root: `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500`

Initial long sibling path worktree creation failed because Windows could not create a tracked long filename. The partial directory was safely removed. Final validation used the shorter path above with `git -c core.longpaths=true worktree add --detach`.

Builder execution notes:

- `-executeMethod MainMenuSceneBuilder.BuildForIncremental` failed because Unity `-executeMethod` did not resolve the `bool` returning method.
- The `void` entrypoints were then used successfully:
  - `MainMenuSceneBuilder.Build`
  - `CharacterSelectSceneBuilder.Build`
  - `GameExploreSceneBuilder.BuildScene`
  - `DiceBattleSceneBuilder.BuildScene`
  - `MahjongBattleSceneBuilder.BuildScene`

Builder logs:

- `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\scene-builders\MainMenuSceneBuilder_Build.log`
- `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\scene-builders\CharacterSelectSceneBuilder_Build.log`
- `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\scene-builders\GameExploreSceneBuilder_BuildScene.log`
- `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\scene-builders\DiceBattleSceneBuilder_BuildScene.log`
- `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\scene-builders\MahjongBattleSceneBuilder_BuildScene.log`

## 9. EditMode Validation Result

Command ran in validation worktree with `-batchmode`, `-projectPath C:\Users\song\c_sbv_20260611_1`, `-runTests`, `-testPlatform EditMode`, `-buildTarget StandaloneWindows64`, and `-logFile`.

Result:

- Exit code: 0
- Result: Passed
- Total: 82
- Passed: 82
- Failed: 0
- Skipped: 0
- Inconclusive: 0
- Result XML: `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\editmode\full-editmode-results.xml`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\editmode\full-editmode.log`

Notes:

- Licensing retry/access-token messages appeared but did not fail the run.
- No compile error or failed test was observed.
- A scene builder field-binding warning appeared in logs, but tests still passed.

## 10. Player Build Validation Result

Command ran in validation worktree with `-batchmode`, `-nographics`, `-quit`, `-projectPath C:\Users\song\c_sbv_20260611_1`, `-buildTarget StandaloneWindows64`, `-buildWindows64Player`, and `-logFile`.

Result:

- Exit code: 0
- Build status: success
- Produced executable: yes
- Executable path: `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\player-build\CapstoneSceneBuildRepair.exe`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\scene-build-source-of-truth-repair_20260611_231500\player-build\standalone-build.log`
- Build log includes `DisplayProgressNotification: Build Successful`
- The previous `'' is an incorrect path for a scene file` error did not recur.

## 11. Validation Worktree Post-run Dirty State

Post-run `git status --porcelain --untracked-files=all` in validation worktree:

```text
 M .gitignore
 M Assets/Physics/DiceBouncy.asset
 M Assets/Physics/DiceBouncy.asset.meta
 M Assets/Physics/WallBouncy.asset
 M Assets/Physics/WallBouncy.asset.meta
 M Assets/Settings/UniversalRP.asset
 M Assets/Textures/DiceRenderTexture.renderTexture
 M Assets/Textures/DiceRenderTexture.renderTexture.meta
 M Assets/Textures/EnemyDiceRenderTexture.renderTexture
 M Assets/Textures/EnemyDiceRenderTexture.renderTexture.meta
 M Assets/UniversalRenderPipelineGlobalSettings.asset
 M ProjectSettings/EditorBuildSettings.asset
 M ProjectSettings/GraphicsSettings.asset
 M ProjectSettings/ProjectSettings.asset
?? Assets/Scenes/CharacterSelect.unity
?? Assets/Scenes/CharacterSelect.unity.meta
?? Assets/Scenes/DiceBattleScene.unity
?? Assets/Scenes/DiceBattleScene.unity.meta
?? Assets/Scenes/GameExploreScene.unity
?? Assets/Scenes/GameExploreScene.unity.meta
?? Assets/Scenes/MahjongBattleScene.unity
?? Assets/Scenes/MahjongBattleScene.unity.meta
?? Assets/Scenes/MainMenu.unity
?? Assets/Scenes/MainMenu.unity.meta
?? c_sbv_20260611_1.slnx
```

Post-run `git diff --stat` in validation worktree:

```text
 .gitignore                                         | 15 ++++-
 Assets/Settings/UniversalRP.asset                  | 66 +++++++++++-----------
 Assets/UniversalRenderPipelineGlobalSettings.asset | 16 +++++-
 ProjectSettings/EditorBuildSettings.asset          | 13 +----
 ProjectSettings/ProjectSettings.asset              |  5 +-
 5 files changed, 67 insertions(+), 48 deletions(-)
```

Only required scene files/metas and the intended `.gitignore` / build settings changes were copied back to foreground. Unity-mutated validation settings/assets were not copied back.

Cleanup result:

- `git worktree remove --force C:\Users\song\c_sbv_20260611_1` completed successfully.
- Validation worktree exists after cleanup: no.
- Validation output root exists after cleanup: yes.

## 12. Remaining Risks

- The foreground checkout still contains many unrelated dirty and untracked files.
- Required scenes are untracked until staged/committed.
- Unity validation still dirties settings/assets in the disposable worktree.
- Hold'em remains a foreground prototype/follow-up feature, not part of build settings yet.
- The generated scenes were validated for build safety, not manual visual/playthrough correctness.
- Asset reference risks from `05_runtime_asset_reference_manifest.md` remain separate from this scene/build repair.

## 13. Next Recommended Tasks

1. Stage/commit the 5 required scene artifacts, their `.meta` files, `.gitignore`, and cleaned build settings as the scene/build source-of-truth repair.
2. Run a focused runtime asset reference repair task for missing/untracked asset dependencies.
3. Plan Hold'em feature promotion separately, including scripts, builder, assets, scene, `.meta`, routing, tests, and player build validation.
