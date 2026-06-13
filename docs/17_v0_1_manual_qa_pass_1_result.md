# v0.1 Manual QA Pass 1 Result

Source checklist: `docs/16_v0_1_manual_qa_checklist.md`

QA date: 2026-05-27

Historical note: this QA pass predates the first-pass Hold'em implementation. Current Hold'em validation expectations are in `docs/16_v0_1_manual_qa_checklist.md`.

## Summary

Manual QA Pass 1 did not reach interactive gameplay. The EditMode gate passed, but the first safe runtime path, a Windows standalone player build from the isolated validation worktree, failed with player compilation errors.

No code, scenes, prefabs, assets, `.meta`, `ProjectSettings/`, `Packages/`, scene builders, serialized field names, or public button callback names were edited for this QA pass.

## Environment

| Field | Value |
|---|---|
| Foreground project | `C:\Users\song\desktop\Capstone` |
| Validation worktree | `C:\Users\song\Desktop\Capstone_editmode_baseline_validation` |
| Validation output root | `C:\Users\song\desktop\Capstone_validation_outputs\manual-qa-pass-1` |
| Standalone build output root | `C:\Users\song\desktop\Capstone_validation_outputs\manual-qa-pass-1-build` |
| Unity version | `6000.3.11f1` |
| Build target | `StandaloneWindows64` |

## Required Order Results

| Order | Item | Status | Notes |
|---:|---|---|---|
| 1 | Confirm current EditMode baseline is still 68/68 passing, or record newer isolated baseline. | PASS | Fresh isolated EditMode run passed: total `68`, passed `68`, failed `0`, skipped `0`. |
| 2 | Smoke-test Dice path through explore. | BLOCKED | Not executed. Runtime player build failed before the game could be launched. |
| 3 | Complete Dice normal combat. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 4 | Test item box and power-up selection. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 5 | Complete Dice boss path. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 6 | Trigger Dice defeat and new-game reset. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 7 | Smoke-test Mahjong path. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 8 | Complete Mahjong normal combat. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 9 | Test Mahjong item box and boss if time allows. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 10 | Verify Hold'em route/behavior. | BLOCKED | Not executed. Blocked by standalone player build failure. |
| 11 | Run transition, UI, sprite/animation, audio, and debug-console regression passes. | BLOCKED | Not executed. Blocked by standalone player build failure. |

## EditMode Baseline

Command run from the foreground shell, with `-projectPath` pointed at the isolated validation worktree:

```powershell
$unity='C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe'
$out='C:\Users\song\desktop\Capstone_validation_outputs\manual-qa-pass-1'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$args=@(
  '-batchmode',
  '-projectPath','C:\Users\song\Desktop\Capstone_editmode_baseline_validation',
  '-runTests',
  '-testPlatform','EditMode',
  '-testResults',"$out\full-editmode-results.xml",
  '-buildTarget','StandaloneWindows64',
  '-logFile',"$out\full-editmode.log"
)
$p=Start-Process -FilePath $unity -ArgumentList $args -Wait -PassThru -WindowStyle Hidden
```

Observed result:

- Unity exit code: `0`
- Result file: `C:\Users\song\desktop\Capstone_validation_outputs\manual-qa-pass-1\full-editmode-results.xml`
- Log file: `C:\Users\song\desktop\Capstone_validation_outputs\manual-qa-pass-1\full-editmode.log`
- Test result XML: `result="Passed" total="68" passed="68" failed="0" skipped="0"`

Post-validation worktree status was dirty before and after the test run. This matches the known validation hygiene caveat, but the worktree is not clean enough to prove Unity made no tracked-file changes during this pass.

## Runtime QA Blocker

### BLOCKER-001: Standalone player build fails because an editor-only script is compiled into the player

Status: FAIL for runtime build readiness, BLOCKED for interactive manual QA.

Classification:

- bug
- validation environment issue

Reproduction steps:

1. Use the isolated validation worktree, not the foreground checkout.
2. Run a Windows standalone build with output outside the project root:

```powershell
$unity='C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe'
$out='C:\Users\song\desktop\Capstone_validation_outputs\manual-qa-pass-1-build'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$args=@(
  '-batchmode',
  '-projectPath','C:\Users\song\Desktop\Capstone_editmode_baseline_validation',
  '-buildTarget','StandaloneWindows64',
  '-buildWindows64Player',"$out\CapstoneQA.exe",
  '-logFile','C:\Users\song\desktop\Capstone_validation_outputs\manual-qa-pass-1\standalone-build.log',
  '-quit'
)
$p=Start-Process -FilePath $unity -ArgumentList $args -Wait -PassThru -WindowStyle Hidden
```

Actual observed behavior:

- The build did not produce a runnable player.
- The command timed out at 180 seconds, but Unity had exited by the follow-up process check.
- `manual-qa-pass-1-build` contained no built player output.
- Build log reports `Build Finished, Result: Failure.`
- Build log reports `Error building Player because scripts had compiler errors`.

Exact compile errors from `standalone-build.log`:

```text
Assets\sprite_fix.cs(6,19): error CS0234: The type or namespace name 'U2D' does not exist in the namespace 'UnityEditor' (are you missing an assembly reference?)
Assets\sprite_fix.cs(9,55): error CS0246: The type or namespace name 'EditorWindow' could not be found (are you missing a using directive or an assembly reference?)
Assets\sprite_fix.cs(103,3): error CS0246: The type or namespace name 'MenuItemAttribute' could not be found (are you missing a using directive or an assembly reference?)
Assets\sprite_fix.cs(103,3): error CS0246: The type or namespace name 'MenuItem' could not be found (are you missing a using directive or an assembly reference?)
```

Expected behavior:

- The v0.1 runtime scene set should compile into a Windows Standalone x64 player so the first manual QA pass can run against a player build or equivalent runtime session.

Suspected file/class:

- `Assets/sprite_fix.cs`
- `LooseSpriteSheetRectifierWindow`

Suspected cause:

- `Assets/sprite_fix.cs` references `UnityEditor`, `UnityEditor.U2D.Sprites`, `EditorWindow`, and `[MenuItem]`, but the file is tracked under `Assets/` rather than an editor-only compilation location or preprocessor guard. It compiles for EditMode/editor use but fails player compilation.

Proposed next task:

- Approve a small, scoped build-readiness fix for `Assets/sprite_fix.cs`.
- Lowest-churn option: wrap the editor-only script with `#if UNITY_EDITOR` / `#endif` so it is excluded from player builds without moving assets or creating `.meta` churn.
- Alternative: move it under `Assets/Editor/`, but that touches asset path and `.meta` handling and should be treated as higher risk.

Stop condition:

- Stop before moving the file because that would touch asset paths and likely `.meta`.
- Stop before editing code unless the small build-readiness fix is explicitly approved.
- Stop before changing ProjectSettings, packages, scene builders, serialized fields, public callbacks, scenes, prefabs, assets, or `.meta`.

## Validation Worktree Dirtiness

Post-build `git status --porcelain` in the validation worktree reported tracked changes including:

- `.gitignore`
- `Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs`
- `Assets/Editor/Tests/Battle/EnemyProjectileAttachmentFollowerTests.cs`
- `Assets/Editor/Tests/Mahjong/YakuEvaluatorTests.cs`
- `Assets/Settings/UniversalRP.asset`
- `Assets/UniversalRenderPipelineGlobalSettings.asset`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `ProjectSettings/ShaderGraphSettings.asset`

It also reported untracked generated entries including:

- `Assets/Resources.meta`
- `Assets/Resources/`
- `Assets/Scenes/`
- `Capstone_editmode_baseline_validation.slnx`
- multiple docs from earlier stabilization work

Classification:

- validation environment issue

Actual observed behavior:

- The validation worktree was already dirty from earlier baseline/stabilization work and became dirtier after the failed player build.

Expected behavior:

- A validation worktree should start from a known state and return clean or only contain explicitly allowed validation artifacts outside the project root.

Proposed next task:

- Create a fresh detached validation worktree after the build-readiness fix is approved, then rerun EditMode and player build validation before attempting interactive manual QA again.

Stop condition:

- Do not clean, reset, delete, or revert the validation worktree without explicit approval.

## Checklist Item Detail

### 1. Main Menu

Status: BLOCKED

Actual observed behavior:

- Not launched. Runtime player build failed before the game could be opened for interaction.

Expected behavior if known:

- `MainMenu` should render and Play should load `CharacterSelect`.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`, not by `MainMenuController`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun this checklist item.

Stop condition:

- Stop before code fixes unless approved.

### 2. Character Select

Status: BLOCKED

Actual observed behavior:

- Not launched. Runtime player build failed before the game could be opened for interaction.

Expected behavior if known:

- Story slides and Dice/Mahjong/Hold'em buttons should be interactable.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`, not by `CharacterSelectController`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun this checklist item.

Stop condition:

- Stop before code fixes unless approved.

### 3. Dice Character Path

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Dice selection should start a new game, load `GameExploreScene`, and route combat to `DiceBattleScene`.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun Dice smoke path.

Stop condition:

- Stop before code fixes unless approved.

### 4. Mahjong Character Path

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Mahjong selection should start a new game, load `GameExploreScene`, and route combat to `MahjongBattleScene`.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun Mahjong smoke path.

Stop condition:

- Stop before code fixes unless approved.

### 5. Hold'em Behavior

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Historical expectation at the time was placeholder behavior. Current expectation is first-pass `HoldemBattleScene` routing after scene generation.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then verify actual Hold'em route.

Stop condition:

- Do not add new Hold'em mechanics during validation.

### 6. Explore Scene

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Explore should initialize hearts, power-up UI, player, encounter panel, and stage sequence.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun explore smoke path.

Stop condition:

- Stop before scene, builder, serialized-field, or ProjectSettings changes.

### 7. Normal Combat

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Dice and Mahjong normal combat should be playable and return to explore after victory.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun normal combat checks.

Stop condition:

- Stop before gameplay code fixes unless an observed combat bug is reported and approved.

### 8. Item Box / Power-Up Selection

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Item-box selection should add a `PowerUpType` and advance the explore loop.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun item-box path.

Stop condition:

- Do not add shop, relic, joker, or new power-up systems.

### 9. Boss Combat

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Boss encounter should set boss battle state and route to the selected character's battle scene.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun boss path.

Stop condition:

- Stop before scene or stage-data changes unless a specific boss routing bug is observed and approved.

### 10. Victory Path

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Non-final victory returns to explore. Final victory should route to main menu or the documented intended victory flow without missing scene dependencies.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun victory path checks.

Stop condition:

- Stop if exact final victory design is ambiguous.

### 11. Defeat Path

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Defeat should route to main menu or intended restart path, and a new game should reset session state.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun defeat and new-game reset checks.

Stop condition:

- Stop before changing session state behavior unless a concrete defect is observed and approved.

### 12. Return To Main Menu

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Exposed return, cancel, defeat, and final-victory paths should load `MainMenu` or the documented intended destination.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun return-to-main-menu checks.

Stop condition:

- Stop before changing hard-coded scene names, build settings, scenes, or builders.

### 13. Scene Transition Regressions

Status: BLOCKED

Actual observed behavior:

- Interactive scene traversal was not executed.
- Player build failed before runtime traversal.

Expected behavior if known:

- Runtime scene names and build settings should align for `MainMenu`, `CharacterSelect`, `GameExploreScene`, `DiceBattleScene`, and `MahjongBattleScene`.

Suspected file/class if obvious:

- `Assets/sprite_fix.cs` blocks player build. No scene transition defect was observed yet.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun transition traversal.

Stop condition:

- Stop before modifying `.unity`, `.meta`, ProjectSettings, scene builders, serialized fields, or public callback names.

### 14. UI Interaction Regressions

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Buttons, hover effects, dice slots, mahjong tiles, and enemy selection should respond.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun UI interaction pass.

Stop condition:

- Stop before scene, builder, serialized-field, or public callback changes.

### 15. Sprite / Animation Visibility Regressions

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Main menu, story, explore, battle, dice, player, enemy, projectile, and mahjong tile visuals should be visible.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun sprite and animation visibility pass.

Stop condition:

- Stop before modifying sprite/audio assets, prefabs, `.meta`, scenes, or builders.

### 16. Audio / Debug Console Regressions

Status: BLOCKED

Actual observed behavior:

- Not executed.

Expected behavior if known:

- Audio sources should initialize and debug console commands should work where supported without corrupting state beyond the requested command.

Suspected file/class if obvious:

- Blocked upstream by `Assets/sprite_fix.cs`.

Issue type:

- validation environment issue
- bug

Proposed next task:

- Fix player compilation blocker, then rerun audio and debug-console checks.

Stop condition:

- Stop before audio asset, scene, builder, or ProjectSettings changes.

## Recommended Next Task

Approve a small build-readiness fix for `Assets/sprite_fix.cs`, preferably by excluding the editor-only window from player compilation with `#if UNITY_EDITOR`. After that:

1. Create or refresh a clean detached validation worktree.
2. Rerun full EditMode validation.
3. Rerun Windows Standalone x64 player build validation.
4. If the player build succeeds, rerun Manual QA Pass 1 from checklist order item 2.
