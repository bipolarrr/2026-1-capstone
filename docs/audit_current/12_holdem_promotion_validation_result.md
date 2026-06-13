# Hold'em Promotion Validation Result

Date: 2026-06-12

## Commit

- Validated content commit before this result note: `e1337d16` (`Promote holdem battle mode`).

## Scene Builder

- Worktree: `C:\Users\song\Desktop\Capstone_holdem_commit_validation_20260612_033316`
- Output root: `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_commit_20260612_033316`
- Command: Unity batchmode `-executeMethod HoldemBattleSceneBuilder.BuildForIncremental` with `-buildTarget StandaloneWindows64` and explicit `-logFile`.
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_commit_20260612_033316\holdem-scene-builder.log`
- Result: Unity exit code `0`; log reports `HoldemBattleScene` generation completed.
- Risk: builder regeneration dirtied `Assets/Scenes/HoldemBattleScene.unity` every run with YAML file ID/order churn. This is treated as a validation source-of-truth risk, so tests/build were run in a separate clean worktree from the commit.

## EditMode

- Worktree: `C:\Users\song\Desktop\Capstone_holdem_testbuild_validation_20260612_034113`
- Output root: `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113`
- Result XML: `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\editmode\full-editmode-results.xml`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\editmode\full-editmode.log`
- Result: Passed, `130` total, `130` passed, `0` failed, `0` skipped.
- Required routing check: `HoldemRoutesToHoldemBattleScene` passed.

## Windows Standalone Build

- Build output: `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\player-build\CapstoneHoldemPromotion.exe`
- Log: `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\player-build\standalone-build.log`
- Result: Unity exit code `0`; executable produced; build log opened and loaded `Assets/Scenes/HoldemBattleScene.unity`.

## Validation Worktree Status

- Builder worktree status after builder: tracked `Assets/Scenes/HoldemBattleScene.unity`, `ProjectSettings/EditorBuildSettings.asset`, and `ProjectSettings/ShaderGraphSettings.asset` dirty, plus generated `.slnx`.
- Test/build worktree status after EditMode/build: tracked render/build settings dirty (`Assets/Settings/UniversalRP.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`), plus generated `.slnx`.
- No validation-generated tracked changes were copied into the foreground checkout after the final test/build run.
