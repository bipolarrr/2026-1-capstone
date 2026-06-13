# Scene Build Hygiene Result

## Files Changed

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
- `docs/02_unity_scene_and_object_construction.md`
- `docs/11_project_decisions.md`
- `docs/14_scene_build_hygiene_result.md`

## Final Build Settings Scene List

`ProjectSettings/EditorBuildSettings.asset` now references only these enabled scenes:

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/CharacterSelect.unity`
- `Assets/Scenes/DiceBattleScene.unity`
- `Assets/Scenes/GameExploreScene.unity`
- `Assets/Scenes/MahjongBattleScene.unity`

No explicit placeholder scenes remain in build settings.

## Stable Runtime Scene Policy

The accepted Hybrid policy is implemented by tracking the five stable runtime `.unity` scenes and their `.meta` files for clean checkout safety.

Builders remain the authoring and regeneration mechanism for scene structure and wiring:

- `Tools/Build MainMenu Scene`
- `Tools/Build CharacterSelect Scene`
- `Tools/Build GameExplore Scene`
- `Tools/Build DiceBattle Scene`
- `Tools/Build MahjongBattle Scene`

Other local scene files under `Assets/Scenes/` remain ignored unless explicitly promoted later.

## SampleScene, YachtDice, And DiceTest

- `Assets/Scenes/SampleScene.unity` was removed from build settings.
- `Assets/Scenes/YachtDice.unity` was removed from build settings.
- `Assets/Scenes/DiceTest.unity` was removed from build settings.

These scenes were treated as stale cleanup targets, not intentional placeholders. They were not restored, regenerated, or added as required runtime source of truth.

## Validation Command Run

Validation was run from an isolated detached worktree:

- Validation worktree: `C:\Users\song\desktop\Capstone_scene_build_hygiene_validation`
- Log path: `C:\Users\song\desktop\Capstone_validation_outputs\scene-build-hygiene\unity-build.log`
- Build output path: `C:\Users\song\desktop\Capstone_validation_outputs\scene-build-hygiene\Build\Capstone.exe`

Command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\Users\song\desktop\Capstone_scene_build_hygiene_validation' -buildTarget StandaloneWindows64 -buildWindows64Player 'C:\Users\song\desktop\Capstone_validation_outputs\scene-build-hygiene\Build\Capstone.exe' -logFile 'C:\Users\song\desktop\Capstone_validation_outputs\scene-build-hygiene\unity-build.log'
```

Unity version observed:

- `6000.3.11f1`

## Validation Result

Result: Failed before player build completion.

Failure cause:

```text
Assets\sprite_fix.cs(6,19): error CS0234: The type or namespace name 'U2D' does not exist in the namespace 'UnityEditor' (are you missing an assembly reference?)
Assets\sprite_fix.cs(9,55): error CS0246: The type or namespace name 'EditorWindow' could not be found (are you missing a using directive or an assembly reference?)
Assets\sprite_fix.cs(103,3): error CS0246: The type or namespace name 'MenuItemAttribute' could not be found (are you missing a using directive or an assembly reference?)
Assets\sprite_fix.cs(103,3): error CS0246: The type or namespace name 'MenuItem' could not be found (are you missing a using directive or an assembly reference?)
Error building Player because scripts had compiler errors
```

Post-validation `git status --porcelain` in the validation worktree showed the expected applied patch plus additional Unity-created or Unity-modified files. Additional tracked settings/assets dirtied by validation included:

- `Assets/Settings/UniversalRP.asset`
- `Assets/UniversalRenderPipelineGlobalSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/ProjectSettings.asset`

Unity also created untracked validation-worktree files:

- `Assets/Resources.meta`
- `Assets/Resources/`
- `Capstone_scene_build_hygiene_validation.slnx`

Per `AGENTS.md`, unexpected tracked-file changes from validation are treated as a validation failure unless explicitly allowed. These validation-worktree changes were not copied back to the foreground checkout.

## Remaining Risks

- Player build validation is blocked by existing compile errors in `Assets/sprite_fix.cs`. This task did not modify runtime/editor code to fix that.
- Unity player build validation dirtied tracked settings in the isolated validation worktree. Future validation should either classify those changes or run from a disposable worktree.
- The tracked scene files are clean-checkout artifacts, but builder output drift still needs review if builders are changed later.
- EditMode test failures documented in `docs/10_validation_triage.md` remain unresolved and were not addressed by this scene/build hygiene task.

## Rollback Notes

To roll back this policy implementation:

- Restore the previous broad `Assets/Scenes/` ignore rule in `.gitignore`.
- Remove the five tracked runtime scene files and their `.meta` files from version control.
- Restore previous `ProjectSettings/EditorBuildSettings.asset` entries if a human owner intentionally wants `SampleScene`, `YachtDice`, or `DiceTest` as build scenes.
- Revert the scene policy updates in `docs/02_unity_scene_and_object_construction.md` and `docs/11_project_decisions.md`.

Rollback should not delete local scene assets from disk unless explicitly requested.
