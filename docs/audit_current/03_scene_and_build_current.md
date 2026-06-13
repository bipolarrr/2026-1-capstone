# Scene And Build Current

- Post-audit closure date: 2026-06-12 KST
- Current HEAD at closure: `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`

## Latest Scene And Build Delta

This section updates the initial 2026-06-11 baseline after `e1337d16`, `d007d473`, and `311ae2e6`.

Latest `HEAD` build settings scene list:

| Order | Scene path | Scene file tracked in latest HEAD? | Latest status |
|---:|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | yes | buildable |
| 1 | `Assets/Scenes/CharacterSelect.unity` | yes | buildable |
| 2 | `Assets/Scenes/GameExploreScene.unity` | yes | buildable |
| 3 | `Assets/Scenes/DiceBattleScene.unity` | yes | buildable |
| 4 | `Assets/Scenes/MahjongBattleScene.unity` | yes | buildable |
| 5 | `Assets/Scenes/HoldemBattleScene.unity` | yes | buildable |

Latest scene file evidence:

- `git ls-files Assets/Scenes` reports 12 tracked paths: six `.unity` files and six `.meta` files.
- `ProjectSettings/EditorBuildSettings.asset` in latest `HEAD` lists the six scenes above.
- Runtime asset validation player build exited 0 and produced `C:\Users\song\Desktop\Capstone_validation_outputs\runtime_assets_20260612_231046\player-build\CapstoneRuntimeAssets.exe`.
- Hold'em validation player build exited 0 and produced `C:\Users\song\Desktop\Capstone_validation_outputs\holdem_testbuild_20260612_034113\player-build\CapstoneHoldemPromotion.exe`.

Latest classification:

- Player build readiness: Partially mitigated. Latest builds pass and no invalid empty scene path was observed in the runtime build log.
- Scene tracking/source-of-truth: Partially mitigated. Runtime scenes are now tracked, but generated scene churn remains open.
- Builder validation: Implemented with hygiene risk. `12_holdem_promotion_validation_result.md` records that `HoldemBattleSceneBuilder` regeneration dirtied `Assets/Scenes/HoldemBattleScene.unity` and settings.
- Validation hygiene: Open. `13_runtime_asset_source_of_truth_validation_result.md` records tracked URP/project settings dirtied by Unity validation in a disposable worktree.

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: `ProjectSettings/EditorBuildSettings.asset`, `Assets/Scenes/**`, `Assets/Editor/*SceneBuilder.cs`, `SceneBuilderUtility`, scene transition strings, Unity build log.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Build Settings Scene List

Initial audit baseline at `e6de7c9`; use Latest Scene And Build Delta above for current `master`.

Clean HEAD `ProjectSettings/EditorBuildSettings.asset` enabled scene list:

| Order | Scene path | Scene file tracked in HEAD? | Foreground file exists? | Foreground status |
|---:|---|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | no | yes | ignored |
| 1 | `Assets/Scenes/SampleScene.unity` | no | no | missing |
| 2 | `Assets/Scenes/CharacterSelect.unity` | no | yes | ignored |
| 3 | `Assets/Scenes/YachtDice.unity` | no | no | missing |
| 4 | `Assets/Scenes/DiceTest.unity` | no | no | missing |
| 5 | `Assets/Scenes/DiceBattleScene.unity` | no | yes | ignored |
| 6 | `Assets/Scenes/GameExploreScene.unity` | no | yes | ignored |
| 7 | `Assets/Scenes/MahjongBattleScene.unity` | no | yes | ignored |

Dirty foreground `ProjectSettings/EditorBuildSettings.asset` enabled scene list:

| Order | Scene path | Foreground file exists? | Foreground status |
|---:|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | yes | ignored |
| 1 | `Assets/Scenes/CharacterSelect.unity` | yes | ignored |
| 2 | `Assets/Scenes/DiceBattleScene.unity` | yes | ignored |
| 3 | `Assets/Scenes/GameExploreScene.unity` | yes | ignored |
| 4 | `Assets/Scenes/MahjongBattleScene.unity` | yes | ignored |
| 5 | `Assets/Scenes/HoldemBattleScene.unity` | yes | untracked |

Finding: clean HEAD build settings and foreground build settings disagree. Clean HEAD references missing scenes and no scene files are tracked under `Assets/Scenes`.

## Scene File Existence

Foreground `Assets/Scenes` contains:

- `AnimationDebugScene.unity` and `.meta`
- `CharacterSelect.unity` and `.meta`
- `DiceBattleScene.unity` and `.meta`
- `GameExploreScene.unity` and `.meta`
- `HoldemBattleScene.unity` and `.meta`
- `MahjongBattleScene.unity` and `.meta`
- `MainMenu.unity` and `.meta`

HEAD contains no tracked files under `Assets/Scenes`.

Classification:

- Tracked/generated scene state: drift risk.
- Foreground scenes: generated scene artifacts, not clean commit source.
- Missing HEAD scenes: player build blocker.

## Scene Builder Inventory

Initial audit baseline at `e6de7c9`; latest `master` tracks `Assets/Editor/HoldemBattleSceneBuilder.cs` and validates Hold'em with the churn caveat described in Latest Scene And Build Delta.

| Builder file | Menu item | Primary build method evidence | Batch callable status | Notes |
|---|---|---|---|---|
| `Assets/Editor/MainMenuSceneBuilder.cs` | `Tools/Build MainMenu Scene` | `Build`, `BuildForIncremental`, `BuildInternal` | Parameterless static method exists, but batch execution was not run | Builds `Assets/Scenes/MainMenu.unity`. |
| `Assets/Editor/CharacterSelectSceneBuilder.cs` | `Tools/Build CharacterSelect Scene` | `Build`, `BuildForIncremental` | Parameterless static method exists, but batch execution was not run | Wires weapon callbacks. |
| `Assets/Editor/GameExploreSceneBuilder.cs` | `Tools/Build GameExplore Scene` | `BuildScene`, `BuildForIncremental` | Parameterless static method exists, but batch execution was not run | Wires explore UI and map assets. |
| `Assets/Editor/DiceBattleSceneBuilder.cs` | `Tools/Build DiceBattle Scene` | `BuildScene`, `BuildForIncremental` | Parameterless static method exists, but batch execution was not run | Uses dice prefab/material/player/mob assets. |
| `Assets/Editor/MahjongBattleSceneBuilder.cs` | `Tools/Build MahjongBattle Scene` | `BuildScene`, `BuildForIncremental` | Parameterless static method exists, but batch execution was not run | Loads `Assets/Mahjong/MahjongTileSprites.asset`. |
| `Assets/Editor/HoldemBattleSceneBuilder.cs` | `Tools/Build HoldemBattle Scene` | foreground untracked builder | Not current tracked evidence | Foreground-only prototype. |
| `Assets/Editor/AnimationDebugSceneBuilder.cs` | `Tools/Build Animation Debug Scene` | foreground untracked builder | Not current tracked evidence | Foreground-only debug scene builder. |
| `Assets/Editor/DicePrefabBuilder.cs` | `Tools/Build Dice Prefabs/D6 Mine` | `BuildD6MinePrefab` | Do not run during audit | Writes generated dice assets/prefabs. |
| `Assets/Editor/SceneBuilderIncrementalBuild.cs` | `Tools/Scene Builders/*` | foreground untracked utility | Not current tracked evidence | Foreground-only incremental builder utility. |

No scene builder batch execution was performed because the audit task did not require mutating validation scenes and the clean build already failed on scene path hygiene.

## Serialized Field Contract

Evidence source: `SceneBuilderUtility.SetField(...)` calls in `Assets/Editor/**`.

Representative contracts:

- `CharacterSelectSceneBuilder` sets UI references and connects weapon selection state to `CharacterSelectController`.
- `GameExploreSceneBuilder` sets explore title, description, stage/event UI, route/map widgets, heart UI, and power-up choice UI on `GameExploreController`.
- `DiceBattleSceneBuilder` sets dice battle UI, dice roll director slots, enemy panels, player/mob animation views, debug console, and battle log references.
- `MahjongBattleSceneBuilder` sets hand/river/enemy UI, tile sprite database, buttons, battle log, debug console, and audio references.
- `SceneBuilderUtility` traverses base classes when assigning fields and records failures.

Risk: renaming serialized fields or changing field types without matching builder updates breaks generated scenes.

## Persistent Callback Contract

Evidence source: `UnityEventTools.AddPersistentListener(...)` calls in builders.

Representative public callback contracts:

- `MainMenuController.OnPlayClicked`
- `MainMenuController.OnSettingsClicked`
- `MainMenuController.OnCreditsClicked`
- `CharacterSelectController.OnBackClicked`
- `CharacterSelectController.AdvanceSlide`
- `CharacterSelectController.SkipToWeaponSelect`
- `CharacterSelectController.OnWeaponSelected_Mahjong`
- `CharacterSelectController.OnWeaponSelected_Holdem`
- `CharacterSelectController.OnWeaponSelected_Dice`
- `GameExploreController.OnFightClicked`
- `GameExploreController.OnFleeClicked`
- `GameExploreController.OnReturnToMainMenu`
- `BattleSceneController.RollDice`
- `BattleSceneController.ConfirmScore`
- `BattleSceneController.CancelBattle`
- `BattleSceneController.NextRound`
- `DiceRollDirector.UnholdSlot0` through `UnholdSlot4`
- `MahjongBattleController.OnDeclareKan`
- `MahjongBattleController.OnDeclareRiichi`
- `MahjongBattleController.OnPartialAttack`
- `MahjongBattleController.CancelBattle`

Risk: renaming these methods requires builder updates and scene regeneration.

## Scene Name String Contract

Initial audit baseline at `e6de7c9`; latest `master` adds `HoldemBattleScene` as the tracked Hold'em battle route and build-settings scene.

Runtime scene string references:

| Owner | Scene string | Role |
|---|---|---|
| `MainMenuController` | `CharacterSelect` | Play button route |
| `CharacterSelectController` | `MainMenu` | Back/cancel route |
| `CharacterSelectController` | `GameExploreScene` | Start game route |
| `GameExploreController` | `DiceBattleScene` | Dice/default battle route |
| `GameExploreController` | `MahjongBattleScene` | Mahjong battle route |
| `BattleSceneController` | `GameExploreScene` | Return after battle/cancel |
| `BattleSceneController` | `MainMenu` | Defeat route |
| `MahjongBattleController` | `GameExploreScene` | Return after Mahjong battle |
| `MahjongBattleController` | `MainMenu` | Defeat route |
| foreground untracked `HoldemBattleController` | `GameExploreScene`, `MainMenu` | Foreground-only prototype |

Risk: build settings must contain valid scenes matching these names, and tracked scene policy must match runtime strings.

## Asset Path String Contract

Direct builder/runtime asset path strings include:

- `Assets/Scenes/*.unity`
- `Assets/Player/Sprites/**`
- `Assets/UI/UI_Heart.png`
- `Assets/UI/UI_Background.png`
- `Assets/UI/MainScreen_Logo.png`
- `Assets/UI/UI_Map.png`
- `Assets/UI/MapIcons/**`
- `Assets/Story/Story_CutScene_*.png`
- `Assets/Mahjong/MahjongTileSprites.asset`
- `Assets/Mahjong/Table.png`
- `Assets/Dices/Prefabs/Dice_d6_mine.prefab`
- `Assets/Dices/Prefabs/Dice_d6.prefab`
- `Assets/Dices/D6_mine.png`
- `Assets/Textures/DiceFaces/face*.png`
- `Assets/Textures/DiceRenderTexture.renderTexture`
- `Assets/Textures/EnemyDiceRenderTexture.renderTexture`
- `Assets/Physics/DiceBouncy.asset`
- `Assets/Physics/WallBouncy.asset`
- `Assets/Materials/DiceOutline.mat`
- `Assets/Materials/SlimeDiceJelly.mat`
- `Assets/Se/True 8-bit Sound Effect Collection - Lite`
- `Assets/Se/DiceRoll_WakuWaku.wav`
- Stage mob/background sprite paths listed in `Stage1Forest` and `Stage2Cave`

Detailed asset status is in `05_runtime_asset_reference_manifest.md`.

## Generated Scene Drift Risk

Drift indicators:

- HEAD has build settings but no tracked scene files.
- Foreground has generated scene files that are untracked or ignored.
- Foreground build settings were modified outside clean validation.
- Scene builders and generated scene artifacts may not match.
- Unity validation mutated build/settings files in the validation worktree.

Classification: High scene/build drift risk.

## Player Build Readiness

Initial audit baseline at `e6de7c9`; latest runtime and Hold'em player builds pass with validation hygiene caveats.

Player build status at clean commit:

- Failed with exit code 1.
- No executable produced.
- Failure: empty/invalid scene path passed to BuildPlayer.
- No compile error was the observed blocker.

Classification: Blocked.
