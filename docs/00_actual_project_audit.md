# 1. Executive summary

Update note, 2026-05-28: this audit predated the first-pass Hold'em implementation. Current docs should treat Hold'em as source-level implemented but Unity-unvalidated until isolated Editor validation completes.

This repository is a Unity 6 project using editor-side scene builders and a runtime loop that currently supports dice, mahjong, and first-pass Hold'em battle scenes. Evidence: `ProjectSettings/ProjectVersion.txt`, `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Editor/DiceBattleSceneBuilder.cs`, `Assets/Editor/MahjongBattleSceneBuilder.cs`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Scripts/Explore/GameExploreController.cs`.

The repository does not contain one class literally named `SceneBuilder`. The actual construction system is split across `SceneBuilderUtility` plus scene-specific builders such as `MainMenuSceneBuilder`, `CharacterSelectSceneBuilder`, `GameExploreSceneBuilder`, `DiceBattleSceneBuilder`, `MahjongBattleSceneBuilder`, and `HoldemBattleSceneBuilder`. Evidence: `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Editor/*SceneBuilder.cs`.

The proven gameplay flow is menu -> character select -> explore -> dice, mahjong, or Hold'em battle -> explore/main menu. The flow is linear stage-round progression, not a visible node-map implementation. Evidence: `README.md`, `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Stages/StageData.cs`, `Assets/Scripts/Stages/Stage1Forest.cs`, `Assets/Scripts/Stages/Stage2Cave.cs`.

Dice battle and mahjong battle are implemented. Texas Hold'em is source-level implemented as a first pass and routes to `HoldemBattleScene`; Unity validation is still pending. Evidence: `Assets/Scripts/CharacterSelect/CharacterType.cs`, `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Holdem/`, `Assets/Editor/HoldemBattleSceneBuilder.cs`.

The dynamic sprite pipeline is partially visible in the repository: PNG frame folders, MP4 source/intermediate files, `_transparent` and `_transparent_clean` folders, and prompt documentation for Grok Imagine image-to-video work. The actual extraction/rembg scripts are not tracked in the inspected repo paths. Evidence: `Assets/Player/Sprites/`, `Assets/Mobs/Sprites/`, `docs/grok-imagine-sprite-prompts.md`, `docs/assets.md`.

Scene files exist in the foreground checkout, but `Assets/Scenes/` is ignored and no `Assets/Scenes/*` files are tracked by Git. `ProjectSettings/EditorBuildSettings.asset` also references `SampleScene`, `YachtDice`, and `DiceTest`, but those scene files were not found in `Assets/Scenes/`. Evidence: `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/DiceBattleScene.unity`, `.gitignore`, `ProjectSettings/EditorBuildSettings.asset`.

# 2. Unity project facts

| Item | Found value | Evidence | Confidence | Notes |
|---|---|---|---|---|
| Unity version | `6000.3.11f1` | `ProjectSettings/ProjectVersion.txt` | High | Revision shown as `6000.3.11f1 (3000ef702840)` in the same file. |
| Render pipeline | Universal Render Pipeline, with 2D renderer assets present | `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset`, `Assets/Settings/UniversalRP.asset`, `Assets/Settings/Renderer2D.asset` | High | `manifest.json` includes `com.unity.render-pipelines.universal: 17.3.0`; `GraphicsSettings.asset` maps URP global settings. |
| Input system | New Input System package present; UI module used by scene builder | `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`, `Assets/InputSystem_Actions.inputactions`, `Assets/Editor/SceneBuilderUtility.cs` | High | `manifest.json` includes `com.unity.inputsystem: 1.19.0`; `SceneBuilderUtility.BuildSceneShell()` adds `InputSystemUIInputModule`. |
| Target platforms | Standalone settings visible; exact Windows x64 build target not proven from repository files | `ProjectSettings/ProjectSettings.asset` | Medium | `ProjectSettings.asset` contains Standalone bundle identifiers. No CI/build script proving x64 was found. |
| Major packages | URP, Input System, Unity Test Framework, UGUI, 2D Animation, Aseprite importer, PSD importer, SpriteShape, Tilemap, Timeline, Visual Scripting | `Packages/manifest.json` | High | Exact package versions are recorded in `Packages/manifest.json`. |
| Package lock | Present | `Packages/packages-lock.json` | High | Required for deterministic package restore. |
| Scenes in build settings | `MainMenu`, `SampleScene`, `CharacterSelect`, `YachtDice`, `DiceTest`, `DiceBattleScene`, `GameExploreScene`, `MahjongBattleScene` | `ProjectSettings/EditorBuildSettings.asset` | High | All are enabled entries in build settings. |
| Scene files found in foreground checkout | `MainMenu`, `CharacterSelect`, `DiceBattleScene`, `GameExploreScene`, `MahjongBattleScene` | `Assets/Scenes/*.unity` | High | `SampleScene`, `YachtDice`, and `DiceTest` were referenced by build settings but not found in `Assets/Scenes/`. |
| Scene Git tracking | `Assets/Scenes/` ignored and not tracked | `.gitignore`, `Assets/Scenes/*.unity` | High | `.gitignore` contains `/[Aa]ssets/[Ss]cenes/`; `git status --ignored Assets/Scenes` showed ignored scene files. |
| Prefabs | Dice prefabs only found in the inspected prefab list | `Assets/Dices/Prefabs/Dice_d4.prefab`, `Dice_d6.prefab`, `Dice_d6_mine.prefab`, `Dice_d8.prefab`, `Dice_d10.prefab`, `Dice_d12.prefab`, `Dice_d20.prefab` | High | No player, mob, or mahjong tile prefab asset was found by the prefab scan. Mahjong builds an in-scene tile prefab object. |
| ScriptableObject assets | Mahjong tile sprite database plus Unity settings assets | `Assets/Mahjong/MahjongTileSprites.asset`, `Assets/Scripts/Mahjong/MahjongTileSpriteDatabase.cs` | High | `MahjongTileSpriteDatabase` is a `ScriptableObject` with `CreateAssetMenu`. |
| Resources | TextMesh Pro resources present; no custom `Assets/Resources` folder found | `Assets/TextMesh Pro/Resources`, `Assets/Editor/SceneBuilderUtility.cs` | Medium | `SceneBuilderUtility` loads TMP font assets by path; only TMP resources folder was found. |
| Addressables | Not found as a configured content system | `.gitignore`, `Packages/manifest.json` | Medium | `.gitignore` has default Addressables ignore patterns, but no Addressables package or `Assets/AddressableAssetsData` was found. |
| Animation clips/controllers | Not found as asset files | `Assets/Scripts/Stages/StageData.cs`, `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Scripts/Battle/EnemySpriteAnimator.cs` | Medium | Code supports `AnimationClip` death clips, but no `*.anim`, `*.controller`, or `*.overrideController` files were found under `Assets/`. |
| Sprite atlases | Not found | Asset scan under `Assets/` | Medium | No `*.spriteatlas` files were found. |
| Tests | EditMode tests for Battle, Mahjong, and Hold'em | `Assets/Editor/Tests/Battle/`, `Assets/Editor/Tests/Mahjong/`, `Assets/Editor/Tests/Holdem/` | High | Test classes include `GameSessionManagerTests`, `SceneBuilderUtilityTests`, `YakuEvaluatorTests`, `TileWallTests`, Hold'em evaluator/state/damage/defense/routing tests, and others. |
| Build/CI files | Not found in repo paths | `AGENTS.md`, `.github` absence, CI config scan excluding `Library/` | Medium | `AGENTS.md` says no CI/CD or linting. Only package documentation YAMLs were found under generated `Library/PackageCache`. |

# 3. Code inventory

| Path | Class / type | Role | Evidence | Confidence | Notes |
|---|---|---|---|---|---|
| `Assets/Scripts/Game/GameSessionManager.cs` | `GameSessionManager`, `BattleResult` | Static cross-scene session state | Fields `SelectedCharacter`, `PlayerHearts`, `PowerUps`, `CurrentEventIndex`, `BattleEnemies`, `LastBattleResult`; methods `StartNewGame()`, `CompleteBattleWon()`, `CancelBattle()` | High | Central mutable state used by explore and battle scenes. |
| `Assets/Scripts/Game/HeartSystem.cs` | `HeartContainer`, `Heart`, `HeartType` | Player HP container | Runtime heart structs/classes in file | High | Used by `GameSessionManager.PlayerHearts` and battle UI. |
| `Assets/Scripts/Game/EnemyInfo.cs` | `EnemyInfo` | Battle enemy runtime data | Class file and use in stage/explore/battle code | High | Contains enemy identity, HP, attack value, and color data. |
| `Assets/Scripts/Game/PowerUpType.cs` | `PowerUpType` | Upgrade/modifier enum | Enum values include `ReviveOnce`, `OddEvenDouble`, `AllOrNothing` | High | Used by explore item choices and dice damage. |
| `Assets/Scripts/CharacterSelect/CharacterType.cs` | `CharacterType` | Character/combat-mode enum | Values `Mahjong`, `Holdem`, `Dice` | High | Hold'em now has first-pass source-level battle implementation, pending Unity validation. |
| `Assets/Scripts/CharacterSelect/CharacterSelectController.cs` | `CharacterSelectController` | Weapon/character selection flow | Methods `OnWeaponSelected_Mahjong()`, `OnWeaponSelected_Holdem()`, `OnWeaponSelected_Dice()`, `SelectWeaponAndStart()` | High | Calls `GameSessionManager.StartNewGame()` and loads `GameExploreScene`. |
| `Assets/Scripts/Explore/GameExploreController.cs` | `GameExploreController` | Explore/event progression controller | Methods `SetupItemEncounter()`, `OnFightClicked()`, `ShowVictory()`, `ResolveBattleSceneName()` | High | Owns `CurrentEventIndex` advancement and battle scene routing. |
| `Assets/Scripts/Stages/StageData.cs` | `StageData`, `MobDef`, `BossDef`, `StageRoundType`, `EnemyAttackRangeType` | Stage and encounter data model | Enum values `NormalCombat`, `ItemBox`, `BossCombat`; mob/boss fields | High | Used by `Stage1Forest`, `Stage2Cave`, and explore/battle setup. |
| `Assets/Scripts/Stages/StageRegistry.cs` | `StageRegistry` | Stage lookup | Static registry class | High | Provides stage data to controllers/builders. |
| `Assets/Scripts/Stages/Stage1Forest.cs` | `Stage1Forest` | Stage 1 data | Defines `NormalCombat`, `ItemBox`, `BossCombat` round sequence | High | Includes mob/boss sprite path data. |
| `Assets/Scripts/Stages/Stage2Cave.cs` | `Stage2Cave` | Stage 2 data | Defines `NormalCombat`, `NormalCombat`, `ItemBox`, `BossCombat` sequence | High | Includes mob/boss sprite path data. |
| `Assets/Scripts/Stages/StageSpriteBundle.cs` | `StageSpriteBundle`, `EnemySpriteAnimationSet` | Runtime sprite bundle DTO | Classes for player/enemy sprite arrays and optional death clip | High | Populated by `SceneBuilderUtility`. |
| `Assets/Scripts/Battle/BattleControllerBase.cs` | `BattleControllerBase` | Shared battle UI/state base | Abstract `MonoBehaviour` with shared enemy/UI fields | High | Extended by dice and mahjong battle controllers. |
| `Assets/Scripts/Battle/BattleSceneController.cs` | `BattleSceneController` | Dice battle scene controller | Extends `BattleControllerBase`, implements `IBattleDebugTarget`; uses `DamageCalculator`, `DiceRollDirector`, `EnemyCounterAttackDirector` | High | Main dice battle runtime. |
| `Assets/Scripts/Battle/DiceRollDirector.cs` | `DiceRollDirector` | Dice rolling and held-slot state machine | `RollPhase`, `TurnMode`, roll/stop/come-out methods | High | Wired by `DiceBattleSceneBuilder`. |
| `Assets/Scripts/Battle/DamageCalculator.cs` | `DamageCalculator` | Dice hand scoring | `Calculate()` handles Yacht, Four of a Kind, Large Straight, Full House, Small Straight, and power-ups | High | Dice combat scoring model. |
| `Assets/Scripts/Battle/DefenseCalculator.cs` | `DefenseCalculator`, `DefenseResult` | Enemy attack defense scoring | Handles combo defense names and results | High | Tested under Battle and Mahjong test folders. |
| `Assets/Scripts/Battle/ComboProximity.cs` | `ComboProximity` | Dice combo ranking and stop planning | Constants for Small Straight, Full House, Large Straight, Four of a Kind, Yacht | High | Used by dice stop planning and tests. |
| `Assets/Scripts/Battle/ComboFortune.cs` | `ComboFortune` | Dice adjustment helper | Comments and methods for force-nearest-combo policy | Medium | Contains commented examples for possible future power-ups, not implemented systems. |
| `Assets/Scripts/Battle/PlayerAttackPipeline.cs` | `PlayerAttackPipeline` | Pure dice attack result pipeline | Calls `DamageCalculator.Calculate()` and maps combo to attack presentation | High | Tested candidate for safe pure logic. |
| `Assets/Scripts/Battle/EnemyCounterAttackDirector.cs` | `EnemyCounterAttackDirector` | Enemy counterattack flow | Large `MonoBehaviour` director with `EnemyCounterAttackContext` | High | Coupled to battle controller and animation/UI. |
| `Assets/Scripts/Battle/EnemyDiceRoller.cs` | `EnemyDiceRoller` | Enemy dice roll visual/model bridge | Calls `DamageCalculator.Calculate()` for enemy dice result | High | Wired by dice battle scene builder. |
| `Assets/Scripts/Battle/EnemyDiceResult.cs` | `EnemyDiceResult` | Enemy dice result DTO | `GetMultiplier()` returns combo multipliers | High | Generic multiplier, not a Balatro chip/mult model. |
| `Assets/Scripts/Battle/EnemyAttackPositionResolver.cs` | `EnemyAttackPositionResolver` | Enemy attack positioning policy | Static resolver class | High | Has failing validation tests from prior run; current audit did not modify it. |
| `Assets/Scripts/Battle/EnemyProjectileAttachmentFollower.cs` | `EnemyProjectileAttachmentFollower` | Projectile attachment/trajectory helper | `MonoBehaviour` used by enemy attack animation | High | Has failing validation tests from prior run; current audit did not modify it. |
| `Assets/Scripts/Battle/EnemySpriteAnimator.cs` | `EnemySpriteAnimator` | Enemy sprite playback | Supports idle/action/death sprites and optional `AnimationClip` | High | Uses `EnemySpriteAnimationSet`. |
| `Assets/Scripts/Battle/PlayerBodyAnimator.cs` | `PlayerBodyAnimator` | Player body sprite playback | Wired by `SceneBuilderUtility.BuildBattlePlayerRig()` | High | Builder injects idle, low HP, jump, defense, hit, attack, death sprites. |
| `Assets/Scripts/Battle/PlayerAttackAnimator.cs` | `PlayerAttackAnimator` | Player attack visual playback | Wired with body/weapon sprites and projectile offsets by `SceneBuilderUtility` | High | Uses generated player sprite folders. |
| `Assets/Scripts/Battle/PlayerDeathAnimator.cs` | `PlayerDeathAnimator` | Player death visual playback | Added and wired by `DiceBattleSceneBuilder` | High | Uses `SceneBuilderUtility.PlayerDieSpriteFolder`. |
| `Assets/Scripts/Battle/BattleHudPresenter.cs` | `BattleHudPresenter` | Battle HUD presentation | MonoBehaviour class in battle folder | Medium | UI-facing helper. |
| `Assets/Scripts/Battle/BattleLog.cs` | `BattleLog`, `BattleEventPresentation` | Battle log UI | Created/wired by `SceneBuilderUtility` | High | Used by battle controllers. |
| `Assets/Scripts/Battle/HeartDisplay.cs` | `HeartDisplay` | Heart UI renderer | Wired by `SceneBuilderUtility.CreateHeartDisplay()` | High | Uses UI heart sprites. |
| `Assets/Scripts/Mahjong/MahjongBattleController.cs` | `MahjongBattleController` | Mahjong battle scene controller | Extends `BattleControllerBase`; fields for tile roots/buttons; methods for draw/discard, riichi, kan, victory, defeat | High | Main mahjong runtime. |
| `Assets/Scripts/Mahjong/Tile.cs` | `Tile`, `Suit` | Mahjong tile value model | Suit enum and tile type | High | Used by all mahjong rule classes. |
| `Assets/Scripts/Mahjong/TileFactory.cs` | `TileFactory` | Tile creation helper | Static factory class | High | Used by wall and tests. |
| `Assets/Scripts/Mahjong/TileWall.cs` | `TileWall` | Mahjong wall/shuffle/dora model | Uses `System.Random` seed and dora indicators | High | Tested by `TileWallTests`. |
| `Assets/Scripts/Mahjong/Hand.cs` | `Hand`, `MeldKind` | Player hand/meld state | Supports concealed tiles and ankans | High | Tested by `HandTests`. |
| `Assets/Scripts/Mahjong/HandDecomposer.cs` | `HandDecomposer` | Winning hand decomposition | `Enumerate()` returns possible winning hands | High | Tested by `HandDecomposerTests`. |
| `Assets/Scripts/Mahjong/BestHandPicker.cs` | `BestHandPicker`, `BestHandResult` | Best mahjong hand/yaku selection | Calls `HandDecomposer.Enumerate()` and counts dora | High | Used by `MahjongBattleController`. |
| `Assets/Scripts/Mahjong/YakuEvaluator.cs` | `YakuEvaluator`, `YakuId`, `YakuResult` | Mahjong yaku evaluation | Yaku enum includes Riichi, MenzenTsumo, Pinfu, Tanyao, Toitoi, yakuman entries | High | One yaku test failed in prior validation; current audit did not modify it. |
| `Assets/Scripts/Mahjong/MahjongDamageTable.cs` | `MahjongDamageTable` | Mahjong hand-to-damage table | Static damage table and partial damage method | High | Tested by `DamageTableTests`. |
| `Assets/Scripts/Mahjong/PartialHandEvaluator.cs` | `PartialHandEvaluator`, `PartialBreakdown` | Partial mahjong attack value | Counts shuntsu/koutsu/kantsu/pair breakdown | High | Tested by `PartialHandEvaluatorTests`. |
| `Assets/Scripts/Mahjong/EnemyMahjongState.cs` | `EnemyMahjongState`, `EnemyTriggerResult`, `EnemyComboType` | Enemy mahjong wait/trigger state | Enemy wait and trigger classes | High | Tested by `EnemyMahjongStateTests`. |
| `Assets/Scripts/Mahjong/MahjongTileSpriteDatabase.cs` | `MahjongTileSpriteDatabase` | Mahjong tile sprite ScriptableObject | `ScriptableObject` with `CreateAssetMenu` | High | Asset exists at `Assets/Mahjong/MahjongTileSprites.asset`. |
| `Assets/Scripts/Mahjong/MahjongTileVisual.cs` | `MahjongTileVisual` | Tile UI visual/click binding | `MonoBehaviour`, `IPointerClickHandler` | High | Runtime tile prefab instances use this component. |
| `Assets/Scripts/Mahjong/MahjongWaitInfoPanel.cs` | `MahjongWaitInfoPanel` | Wait information UI | Instantiates `tilePrefab` into `tilesRoot` | High | Builder injects tile prefab and sprite DB. |
| `Assets/Scripts/Mahjong/EnemyWaitTilesDisplay.cs` | `EnemyWaitTilesDisplay` | Enemy wait tile UI | Uses `MahjongTileSpriteDatabase` | High | Built by mahjong scene builder. |
| `Assets/Scripts/Audio/AudioManager.cs` | `AudioManager` | Global audio facade/registry | `MonoBehaviour` audio manager | High | Built by `SceneBuilderUtility.BuildAudioManager()`. |
| `Assets/Scripts/Game/DebugConsoleController.cs` | `DebugConsoleController` | Runtime debug console UI | Built by `SceneBuilderUtility.BuildDebugConsole()` | High | Uses `DebugCommandProcessor`. |
| `Assets/Scripts/Game/DebugCommandProcessor.cs` | `DebugCommandProcessor` | Debug command parser/executor | Static processor class | High | Commands touch session, stage, sprite, and battle state. |
| `Assets/Scripts/MainMenu/MainMenuController.cs` | `MainMenuController` | Main menu flow | Main menu runtime controller | High | Built by `MainMenuSceneBuilder`. |
| `Assets/Editor/SceneBuilderUtility.cs` | `SceneBuilderUtility` | Shared editor scene/UI/battle builder utility | `SetField()`, `BuildSceneShell()`, `BuildBattleRootBase()`, `BuildAudioManager()`, `AddSceneToBuildSettings()` | High | Central editor construction dependency. |
| `Assets/Editor/MainMenuSceneBuilder.cs` | `MainMenuSceneBuilder` | Main menu scene generator | `[MenuItem("Tools/Build MainMenu Scene")]` | High | Creates menu UI and wires controller. |
| `Assets/Editor/CharacterSelectSceneBuilder.cs` | `CharacterSelectSceneBuilder` | Character select scene generator | `[MenuItem("Tools/Build CharacterSelect Scene")]` | High | Creates cutscene/weapon select UI and persistent callbacks. |
| `Assets/Editor/GameExploreSceneBuilder.cs` | `GameExploreSceneBuilder` | Explore scene generator | `[MenuItem("Tools/Build GameExplore Scene")]` | High | Creates explore UI/enemy slots and stage sprite bundles. |
| `Assets/Editor/DiceBattleSceneBuilder.cs` | `DiceBattleSceneBuilder` | Dice battle scene generator | `[MenuItem("Tools/Build DiceBattle Scene")]`; loads dice prefabs and builds dice/battle UI | High | High-risk builder hub. |
| `Assets/Editor/MahjongBattleSceneBuilder.cs` | `MahjongBattleSceneBuilder` | Mahjong battle scene generator | `[MenuItem("Tools/Build MahjongBattle Scene")]`; builds in-scene tile prefab and injects tile DB | High | High-risk mahjong UI/builder hub. |
| `Assets/Editor/DicePrefabBuilder.cs` | `DicePrefabBuilder` | Generated dice prefab builder | Saves `Assets/Dices/Prefabs/Dice_d6_mine.prefab` | High | Uses `PrefabUtility.SaveAsPrefabAsset()`. |
| `Assets/Editor/Tests/Battle/` | Battle EditMode tests | Battle/session/builder tests | Test classes include `SceneBuilderUtilityTests`, `GameSessionManagerTests`, `EnemyAttackPositionResolverTests` | High | Existing test coverage for several pure/editor helpers. |
| `Assets/Editor/Tests/Mahjong/` | Mahjong EditMode tests | Mahjong rule/controller tests | Test classes include `YakuEvaluatorTests`, `TileWallTests`, `MahjongBattleControllerTests` | High | Existing test coverage for mahjong rules and controller checks. |

# 4. SceneBuilder audit

## Does SceneBuilder exist?

No single class exactly named `SceneBuilder` was found. The project uses `SceneBuilderUtility` plus multiple scene-specific builder classes. Evidence: `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Editor/MainMenuSceneBuilder.cs`, `Assets/Editor/CharacterSelectSceneBuilder.cs`, `Assets/Editor/GameExploreSceneBuilder.cs`, `Assets/Editor/DiceBattleSceneBuilder.cs`, `Assets/Editor/MahjongBattleSceneBuilder.cs`.

## Where is it?

The builder system is under `Assets/Editor/`. Evidence: `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Editor/*SceneBuilder.cs`, `Assets/Editor/DicePrefabBuilder.cs`.

## What does it create/place?

It creates cameras, canvases, event systems, battle roots, player rigs, enemy slots, bottom-focus battle UI, debug console, audio manager, scene-specific buttons/panels, dice cameras, render textures/raw images, and mahjong tile UI. Evidence: `SceneBuilderUtility.BuildSceneShell()`, `SceneBuilderUtility.BuildBattlePlayerRig()`, `SceneBuilderUtility.BuildBattleRootBase()`, `SceneBuilderUtility.BuildAudioManager()`, `DiceBattleSceneBuilder.BuildScene()`, `MahjongBattleSceneBuilder.BuildScene()`.

## Does it instantiate prefabs?

Yes for dice. `DiceBattleSceneBuilder` loads `Assets/Dices/Prefabs/Dice_d6_mine.prefab`, falls back to `Assets/Dices/Prefabs/Dice_d6.prefab`, and uses `PrefabUtility.InstantiatePrefab()`. Evidence: `Assets/Editor/DiceBattleSceneBuilder.cs`.

For mahjong tiles, no prefab asset was found. `MahjongBattleSceneBuilder.BuildTilePrefab()` creates a `GameObject` named `MahjongTile` in the generated scene, and `MahjongBattleController.SpawnTileVisual()` instantiates the injected `tilePrefab` at runtime. Evidence: `Assets/Editor/MahjongBattleSceneBuilder.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`.

## Does it hard-code object names, positions, tags, layers, sorting layers, cameras, canvases, or coordinates?

Yes. It hard-codes names such as `MainCamera`, `Canvas`, `EventSystem`, `BattleRoot`, `DebugConsole`, `DiceCamera`, `EnemyDiceCamera`, `EnemyDiceRoller`, `DiceViewport`, and `MahjongTile`. Evidence: `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Editor/DiceBattleSceneBuilder.cs`, `Assets/Editor/MahjongBattleSceneBuilder.cs`.

It hard-codes UI anchors and coordinates through `AnchorBox()`, direct `RectTransform` anchors, and `Vector2`/`Vector3` placement values. Evidence: `SceneBuilderUtility.AnchorBox()`, `DiceBattleSceneBuilder.BuildScene()`, `MahjongBattleSceneBuilder.AnchorBox()`.

It hard-codes asset paths for sprites/audio/materials/prefabs. Evidence: `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Editor/DiceBattleSceneBuilder.cs`, `Assets/Editor/GameExploreSceneBuilder.cs`, `Assets/Editor/MahjongBattleSceneBuilder.cs`, `Assets/Scripts/Stages/Stage1Forest.cs`, `Assets/Scripts/Stages/Stage2Cave.cs`.

## Does it own UI construction?

Yes. The builders create menu UI, character select UI, explore UI, dice battle UI, mahjong battle UI, battle logs, buttons, panels, TextMeshPro text, and layouts. Evidence: `Assets/Editor/MainMenuSceneBuilder.cs`, `Assets/Editor/CharacterSelectSceneBuilder.cs`, `Assets/Editor/GameExploreSceneBuilder.cs`, `Assets/Editor/DiceBattleSceneBuilder.cs`, `Assets/Editor/MahjongBattleSceneBuilder.cs`, `Assets/Editor/SceneBuilderUtility.cs`.

## What other scripts depend on it?

Runtime controllers depend on serialized fields that builders inject by string name through `SceneBuilderUtility.SetField()`. Evidence: `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Battle/BattleControllerBase.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`.

Scene callbacks depend on method names used by persistent listeners. Evidence: `DiceBattleSceneBuilder` uses `UnityEventTools.AddPersistentListener()` for `BattleSceneController.RollDice`, `ConfirmScore`, `CancelBattle`, and `NextRound`; `SceneBuilderUtility.BindBattleEnemyPanelButtons()` wires `BattleControllerBase.OnEnemyPanel0Clicked()` through `OnEnemyPanel3Clicked()`.

Tests depend on builder utilities. Evidence: `Assets/Editor/Tests/Battle/SceneBuilderUtilityTests.cs`.

Project rules explicitly treat builder-wired serialized field names as public contracts. Evidence: `AGENTS.md`, `.claude/rules/scene-builder.md`, `REFACTOR_BACKLOG.md`.

## What would likely break if SceneBuilder changed?

Field-name changes would break scene wiring if a `SetField()` string no longer matches the target serialized field. Evidence: `SceneBuilderUtility.SetField()`, `Assets/Editor/DiceBattleSceneBuilder.cs`, `Assets/Editor/MahjongBattleSceneBuilder.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`.

Persistent callback method changes would break generated button behavior. Evidence: `UnityEventTools.AddPersistentListener()` calls in `Assets/Editor/DiceBattleSceneBuilder.cs` and `Assets/Editor/SceneBuilderUtility.cs`.

Asset path changes would break sprite/audio/prefab loading. Evidence: `AssetDatabase.LoadAssetAtPath<>()` calls in `Assets/Editor/SceneBuilderUtility.cs`, `Assets/Editor/DiceBattleSceneBuilder.cs`, `Assets/Editor/MahjongBattleSceneBuilder.cs`.

Scene generation changes could desynchronize ignored/generated scene files from runtime expectations. Evidence: `.gitignore` ignores `Assets/Scenes/`; scene builders save scenes through `SceneBuilderUtility.SaveAndRegisterScene()`/`EditorSceneManager.SaveScene()`; `ProjectSettings/EditorBuildSettings.asset` references scene paths.

# 5. Gameplay loop audit

| Item | Classification | Evidence | Notes |
|---|---|---|---|
| Run loop | Implemented | `README.md`; `Assets/Scripts/MainMenu/MainMenuController.cs`; `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`; `Assets/Scripts/Explore/GameExploreController.cs`; `Assets/Scripts/Battle/BattleSceneController.cs`; `Assets/Scripts/Mahjong/MahjongBattleController.cs` | Proven flow is menu/select/explore/battle/return. |
| Map/node/progression | Partially implemented | `Assets/Scripts/Stages/StageData.cs`; `Assets/Scripts/Stages/Stage1Forest.cs`; `Assets/Scripts/Stages/Stage2Cave.cs`; `Assets/Scripts/Explore/GameExploreController.cs` | Stage `rounds` are linear arrays of `NormalCombat`, `ItemBox`, and `BossCombat`. No node-map class or graph model was found. |
| Encounter/combat loop | Implemented | `GameExploreController.OnFightClicked()`, `GameExploreController.ResolveBattleSceneName()`, `BattleSceneController`, `MahjongBattleController` | Dice and mahjong battle scenes exist as runtime controllers and builder targets. |
| Rewards | Partially implemented | `GameExploreController.SetupItemEncounter()`, `GameExploreController.OnItemSelected()`, `PowerUpType` | Item box rewards exist. No post-combat reward screen/system beyond event progression was found. |
| Upgrades | Partially implemented | `PowerUpType`, `GameSessionManager.PowerUps`, `DamageCalculator.ApplyPowerUps()`, `GameExploreController.SetupItemEncounter()` | Three power-up types are visible. No full relic/joker/card upgrade framework was found. |
| Shop | Not found | Search over `Assets/Scripts` found no shop controller/model; no `Shop` scene/prefab was found | Unknown if intended outside repo. |
| Player state | Implemented | `GameSessionManager`, `HeartContainer`, `CharacterSelectionContext`, `CharacterType` | Static session stores selected character, hearts, power-ups, stage, battle state. |
| Enemy/opponent state | Implemented | `EnemyInfo`, `StageData.MobDef`, `StageData.BossDef`, `EnemyMahjongState`, `MahjongMatchState` | Dice battle uses `EnemyInfo`; mahjong has per-enemy wait/trigger state. |
| Win/loss/restart | Partially implemented | `BattleSceneController.PlayerDefeatedRoutine()`, `BattleSceneController.BattleWonRoutine()`, `MahjongBattleController.VictoryRoutine()`, `MahjongBattleController.Defeat()`, `GameExploreController.ShowVictory()` | Victory/defeat paths return to explore or main menu. A distinct restart flow beyond starting from main/select was not found. |
| Boss progression | Implemented | `StageRoundType.BossCombat`, `StageData.boss`, `GameExploreController.PrepareBossEncounter()` | Boss combat is represented in stage data and explore logic. |
| Multi-stage progression | Partially implemented | `StageRegistry`, `Stage1Forest`, `Stage2Cave`, `GameSessionManager.CurrentStageId` | Multiple stages exist, but a map/roadmap UI for choosing stages was not found. |

# 6. Combat/rules audit

| Area | Classification | Evidence | Notes |
|---|---|---|---|
| Dice mode | Implemented | `Assets/Scripts/Battle/BattleSceneController.cs`, `DiceRollDirector.cs`, `DamageCalculator.cs`, `DefenseCalculator.cs`, `EnemyDiceRoller.cs`, `Assets/Editor/DiceBattleSceneBuilder.cs` | Main dice battle controller, dice roll director, scoring, defense, enemy dice, and scene builder exist. |
| Dice hand evaluation | Implemented | `DamageCalculator.Calculate()`, `ComboProximity.GetComboRank()`, `Assets/Editor/Tests/Battle/DefenseRulesTests.cs` | Yacht-style combos include Yacht, Four of a Kind, Large Straight, Full House, Small Straight. |
| Dice power-up scoring | Partially implemented | `PowerUpType`, `DamageCalculator.ApplyPowerUps()`, `GameExploreController.SetupItemEncounter()` | `OddEvenDouble` and `AllOrNothing` affect dice damage; no broader modifier/relic/joker system was found. |
| Dice randomness | Implemented | `Assets/Scripts/Dice/DiceRandomizer.cs`, `Assets/Scripts/Explore/GameExploreController.cs` | Dice and encounter generation use Unity random APIs. Deterministic seed handling for dice was not found. |
| Mahjong tile mode | Implemented | `MahjongBattleController`, `TileWall`, `Hand`, `HandDecomposer`, `BestHandPicker`, `YakuEvaluator`, `MahjongDamageTable`, `MahjongBattleSceneBuilder` | 1-player mahjong battle loop and rules classes exist. |
| Mahjong hand evaluation | Implemented | `HandDecomposer.Enumerate()`, `BestHandPicker.Pick()`, `YakuEvaluator.Evaluate()` | Yaku enum and evaluator include many riichi mahjong yaku and yakuman entries. |
| Mahjong damage model | Implemented | `MahjongDamageTable`, `PartialHandEvaluator`, `Assets/Editor/Tests/Mahjong/DamageTableTests.cs` | Damage table and partial-hand damage exist. |
| Mahjong randomness/seeding | Implemented | `TileWall`, `EnemyMahjongState`, `MahjongBattleController` | `TileWall` uses `System.Random` with seed; controller creates seeds from runtime time. |
| Texas Hold'em mode | Source-level implemented, Unity-unvalidated | `Assets/Scripts/Holdem/`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Editor/Tests/Holdem/`, `GameExploreController.ResolveBattleSceneName()` | Hold'em routes to `HoldemBattleScene` and has first-pass rules/controller/tests. Scene generation, compile, EditMode, play, and build validation are still required. |
| Shared scoring model | Not found | `DamageCalculator`, `MahjongDamageTable`, `YakuEvaluator` | Dice and mahjong scoring are separate systems. No shared score abstraction was found. |
| Balatro-like chips/multiplier/scoring | Not found | `DamageCalculator`, `EnemyDiceResult.GetMultiplier()`, `PowerUpType` | Generic combo damage and enemy multipliers exist, but no chip/mult scoring model or Balatro-specific system was found. |
| Modifiers/relics/jokers/equivalent systems | Partially implemented | `PowerUpType`, `GameSessionManager.PowerUps`, `GameExploreController.SetupItemEncounter()` | Power-ups exist. No relic, joker, deck, shop, or card-modifier model was found. |
| Tests for rules | Implemented | `Assets/Editor/Tests/Battle/`, `Assets/Editor/Tests/Mahjong/`, `Assets/Editor/Tests/Holdem/` | Existing tests cover dice face/defense/session/builder, mahjong wall/hand/yaku/damage/controller pieces, and Hold'em hand/damage/round/defense/routing rules. |

# 7. Asset pipeline audit

| Pipeline item | Classification | Evidence | Notes |
|---|---|---|---|
| Source images | Present | `Assets/Mobs/Goblin_for_grok.png`, `Assets/Player/Sprites/Idle/0.png`, `Assets/Mahjong/*.png`, `Assets/Backgrounds/`, `Assets/UI/` | Static image assets exist. Source/original status of every image is Unknown. |
| Generated videos | Present | `Assets/Player/Sprites/Attack/Player_Attack.mp4`, `Assets/Player/Sprites/Defense/Player_Defense.mp4`, `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack.mp4`, `Assets/Mobs/Sprites/Goblin/Attack/Goblin_Attack.mp4` | Asset scan found 15 `.mp4` files under `Assets/`. |
| Extracted frames | Present | `Assets/Player/Sprites/Attack/`, `Assets/Player/Sprites/Idle/`, `Assets/Mobs/Sprites/Bat/Attack/`, `Assets/Mobs/Sprites/Goblin/Dead/` | Asset scan found 6,205 `.png` files under `Assets/`; many are in animation/action folders. |
| rembg/background removal outputs | Partially present | `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean/`, `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png`, `docs/assets.md`, `docs/grok-imagine-sprite-prompts.md` | Output naming and docs mention clean/remove background. A tracked rembg script was not found in inspected repo paths. |
| Unity-ready sprites | Present | `.png` plus `.png.meta` files under `Assets/Player/`, `Assets/Mobs/`, `Assets/Mahjong/`, `Assets/UI/` | Import settings are visible in `.meta` sidecars. This audit did not edit `.meta` files. |
| Mahjong tile sprites | Present | `Assets/Mahjong/MahjongTileSprites.asset`, `Assets/Scripts/Mahjong/MahjongTileSpriteDatabase.cs`, `Assets/Mahjong/*.png` | ScriptableObject database and tile image files exist. |
| Animation clips/controllers | Not found | Asset scan under `Assets/`; `Assets/Scripts/Battle/EnemySpriteAnimator.cs` | Code can use `AnimationClip`, but no `*.anim` or controller files were found. |
| Sprite atlases | Not found | Asset scan under `Assets/` | No `*.spriteatlas` files found. |
| Prefabs using generated sprites | Not found | Prefab scan found dice prefabs under `Assets/Dices/Prefabs/` only | Player/mob/mahjong visual objects are built by scene builders or runtime instantiation, not found as tracked prefab assets. |
| Dice prefabs | Present | `Assets/Dices/Prefabs/Dice_d6.prefab`, `Assets/Dices/Prefabs/Dice_d6_mine.prefab`, other dice prefabs | Used by `DiceBattleSceneBuilder`. |
| Naming conventions | Present | `Assets/Player/Sprites/*`, `Assets/Mobs/Sprites/<Mob>/<Action>/`, `_transparent`, `_transparent_clean`, `_764x640` folders | Naming is visible in folder/file paths. Formal convention doc is partial in `docs/assets.md` and `docs/grok-imagine-sprite-prompts.md`. |
| Import settings visible in `.meta` files | Present | `Assets/Mahjong/MahjongTileSprites.asset.meta`, `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png.meta`, `Assets/Mobs/Goblin_for_grok.png.meta` | `.meta` sidecars are present and should not be edited manually. |
| Audio assets | Present | `Assets/Se/`, asset scan found 1,058 `.wav` files | Audio manager builder loads clips by asset paths in `SceneBuilderUtility`. |
| Addressables pipeline | Not found | `.gitignore`, `Packages/manifest.json`, no `Assets/AddressableAssetsData` found | Only default ignore patterns were visible. |

# 8. Existing documentation audit

| Path | Type | Evidence | Notes |
|---|---|---|---|
| `README.md` | Project overview | Lists game flow, scene table, builders, tests, and notes about generated scenes | Contains useful but partly stale scene-tracking guidance because scene files exist in the foreground checkout while `Assets/Scenes/` remains ignored. |
| `AGENTS.md` | Codex operating rules | Defines Unity validation isolation, builder risk, runtime invariants, and topic docs | Strong operational source of truth for automation. |
| `CLAUDE.md` | Agent/project instructions | Top-level instruction file present in repo | Not inspected deeply for this report. |
| `CLAUDE.original.md` | Historical/alternate instructions | File present in repo | Not inspected deeply for this report. |
| `REFACTOR_BACKLOG.md` | Refactor backlog | Lists risk map, high-coupling files, task order, and constraints | Useful for technical debt and sequencing. |
| `docs/assets.md` | Asset/dependency notes | Lists asset expectations and transparent-clean examples | Useful but not a full asset pipeline manifest. |
| `docs/tuning.md` | Tuning/layout notes | Contains battle/Mahjong UI layout bands and tuning values | Useful for UI/layout references. |
| `docs/grok-imagine-sprite-prompts.md` | Sprite prompt workflow | Documents Grok Imagine prompt patterns and image-to-video workflow | Direct evidence for generated sprite pipeline intent. |
| `docs/task-000c-approved-files.md` | Task file list from prior work | Present in working tree, not tracked by Git status | Relevant to repository hygiene history, not a game design doc. |
| `.claude/rules/coding.md` | Coding rules | Contains runtime/API constraints such as avoiding runtime `Shader.Find()` | Scoped implementation rules. |
| `.claude/rules/scene-builder.md` | Scene builder rules | Documents `SceneBuilderUtility.SetField()` and builder menu items | Important for SceneBuilder risk documentation. |
| `.claude/specs/game-flow.md` | Game flow spec | Documents `MainMenu -> CharacterSelect -> GameExploreScene <-> battle` | Aligns with runtime code. |
| `.claude/specs/battle-system.md` | Battle system spec | Documents dice battle flow, sprites, debug commands | Aligns with battle code but should be audited when used. |
| `.claude/specs/mahjong-battle.md` | Mahjong battle spec | Documents 1-player riichi mahjong battle and tests | Aligns with mahjong code but should be audited when used. |

# 9. Gap analysis against proposed documentation plan

| Proposed document | Necessary now? Yes / Later / No | Reason | Evidence | Minimum useful scope |
|---|---|---|---|---|
| `00_project_brief.md` | Yes | A concise factual brief is needed because user-provided genre intent is broader than what the repo proves. | `README.md`, `ProjectSettings/ProjectVersion.txt`, `GameExploreController`, `DamageCalculator`, `MahjongBattleController` | One page: proven current scope, modes implemented, modes absent, validation status, scene/builder source of truth. |
| `01_architecture_map.md` | Yes | Runtime state, scene routing, builders, and tests are coupled across many files. | `GameSessionManager`, `GameExploreController`, `BattleControllerBase`, `BattleSceneController`, `MahjongBattleController`, `SceneBuilderUtility` | Diagram/table of scene flow, session state ownership, builder-to-controller wiring, test coverage map. |
| `02_unity_scene_and_object_construction.md` | Yes | Scene construction is high-risk and mostly editor-generated through string-based field wiring. | `Assets/Editor/*SceneBuilder.cs`, `SceneBuilderUtility.SetField()`, `.claude/rules/scene-builder.md`, `AGENTS.md` | Builder menu list, generated scene ownership, field-name contracts, prefab/asset path dependencies, verification procedure. |
| `03_gameplay_rules_and_combat.md` | Yes | Dice, mahjong, and first-pass Hold'em rules are implemented separately. | `DamageCalculator`, `ComboProximity`, `YakuEvaluator`, `MahjongDamageTable`, `Assets/Scripts/Holdem/`, `GameExploreController.ResolveBattleSceneName()` | Current dice rules, mahjong rules, Hold'em first-pass rules, power-ups, validation gaps, unknown design questions. |
| `04_content_and_asset_pipeline.md` | Yes | Large generated sprite/audio content exists, and pipeline evidence is spread across folders/docs. | `Assets/Player/Sprites/`, `Assets/Mobs/Sprites/`, `docs/grok-imagine-sprite-prompts.md`, `docs/assets.md`, `.gitignore` | Source/intermediate/final asset policy, naming convention, what to track, what to ignore, import/meta rules. |
| `05_code_index.md` | Later | This audit already provides an initial inventory; a maintained index is useful after architecture docs stabilize. | Section 3 of this report, `Assets/Scripts/`, `Assets/Editor/` | Compact module index grouped by runtime/editor/test, with owner/risk level. |
| `06_technical_debt_and_risks.md` | Yes | High-coupling files and validation/test failures are known risk areas. | `REFACTOR_BACKLOG.md`, `SceneBuilderUtility`, `GameExploreController`, `MahjongBattleController`, prior EditMode validation results | Risk register: builder strings, ignored scenes, validation hygiene, failing tests, asset path coupling. |
| `07_roadmap_and_backlog.md` | Later | `REFACTOR_BACKLOG.md` already exists; roadmap should wait until remaining implemented-vs-intended scope is clarified. | `REFACTOR_BACKLOG.md`, `CharacterType.Holdem`, `GameExploreController.ResolveBattleSceneName()` | Update after Hold'em validation and remaining map/shop/rewards scope decisions. |
| `08_open_questions.md` | Yes | Several questions cannot be answered from repository files. | Hold'em validation status, absent shop/node map classes, asset pipeline docs and generated folders | Short list of game design, technical, asset, and roadmap questions with evidence links. |

# 10. Recommended immediate next actions

| Priority | Task | Why it matters | Evidence | Suggested owner | Safe acceptance criteria |
|---|---|---|---|---|---|
| 1 | Decide the source-of-truth policy for generated scenes and build settings | `Assets/Scenes/` is ignored while scene files exist locally and build settings reference missing scene paths. This affects clean checkout validation. | `.gitignore`, `Assets/Scenes/*.unity`, `ProjectSettings/EditorBuildSettings.asset`, `README.md` | Human + Codex | A written policy states whether scenes are generated-only or tracked, and `EditorBuildSettings.asset` references only expected scene paths. |
| 2 | Preserve this audit as the factual baseline | The repo has implemented, partial, and absent systems that should not be conflated in future docs. | `docs/00_actual_project_audit.md` | Codex | This file is reviewed and either committed or explicitly superseded. |
| 3 | Fix validation hygiene before feature/refactor work | Prior isolated validation showed Unity-generated foreground-independent changes and untracked solution output; those can obscure real code/test changes. | Prior Task 000C validation worktree output; `.gitignore`; `ProjectSettings/EditorBuildSettings.asset`; `ProjectSettings/ShaderGraphSettings.asset` | Codex | Isolated validation worktree ends with no tracked file modifications and no disallowed generated files inside the repo. |
| 4 | Triage the current failing EditMode tests without changing gameplay yet | There are known failing tests in battle positioning/projectile/yaku areas, and future refactors need a stable baseline. | `Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs`, `Assets/Editor/Tests/Battle/EnemyProjectileAttachmentFollowerTests.cs`, `Assets/Editor/Tests/Mahjong/YakuEvaluatorTests.cs` | Codex | A report classifies each failure as test drift, implementation bug, or design ambiguity, without broad refactor. |
| 5 | Write `02_unity_scene_and_object_construction.md` next | Scene builders are the highest-risk project-organization area. | `SceneBuilderUtility.SetField()`, `DiceBattleSceneBuilder`, `MahjongBattleSceneBuilder`, `.claude/rules/scene-builder.md` | Codex | Doc lists builder menus, generated outputs, field wiring contracts, asset path dependencies, and validation steps. |
| 6 | Validate Hold'em first pass | Hold'em now has source-level rules/scene builder/routing, but no isolated Unity validation has been run. | `Assets/Scripts/Holdem/`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `GameExploreController.ResolveBattleSceneName()` | Human + Codex | Isolated validation records compile/test/build/play status and scene/build-settings policy. |
| 7 | Create a tracked asset pipeline manifest | Generated sprite/video/frame assets are numerous and easy to lose or duplicate. | `Assets/Player/Sprites/`, `Assets/Mobs/Sprites/`, `docs/grok-imagine-sprite-prompts.md`, `docs/assets.md` | ChatGPT Project + Codex | A doc states source/intermediate/final asset categories, naming rules, and what should be tracked or ignored. |
| 8 | Decide whether `docs/task-000c-approved-files.md` should be tracked | It currently exists as an untracked doc and may be useful repository hygiene history. | `docs/task-000c-approved-files.md`, Git status | Human | Human chooses track, archive, or discard; Codex then follows that decision in a separate task. |
| 9 | Add a compact architecture map | Current architecture depends on static session state plus scene routing plus editor-generated wiring. | `GameSessionManager`, `GameExploreController`, `BattleControllerBase`, `SceneBuilderUtility` | Codex | A short doc or diagram makes state ownership, scene transitions, and builder injection explicit. |
| 10 | Confirm product roadmap before broad docs | Roadmap docs should not invent map/shop/Balatro-style details not visible in code or treat Unity-unvalidated Hold'em source as fully validated. | Absent `Shop`/node-map systems, Hold'em validation pending, separate dice/mahjong/Hold'em scoring | Human | Human-approved scope states required modes, macro-loop, asset pipeline policy, and v0.1 acceptance target. |

# 11. Questions for the human developer

## Game design questions

- What exact Unity validation result is required before the first-pass Hold'em implementation is accepted for the current milestone? Evidence: `Assets/Scripts/Holdem/`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Scripts/Explore/GameExploreController.cs`.
- Should the macro structure remain a linear `StageRoundType` sequence, or should a visible node-map/route system be added later? Evidence: `Assets/Scripts/Stages/StageData.cs`, `Assets/Scripts/Explore/GameExploreController.cs`.
- Are shop, relic/joker, and deeper upgrade systems part of the intended design, or should the current `PowerUpType` item-box system remain the only modifier layer for now? Evidence: `Assets/Scripts/Game/PowerUpType.cs`, `GameExploreController.SetupItemEncounter()`.
- Should Balatro-like scoring mean an explicit chips/multiplier model, or only combo-based combat inspiration? Evidence: `DamageCalculator`, `EnemyDiceResult.GetMultiplier()`, absence of chip/mult classes.

## Technical architecture questions

- Are generated `.unity` scenes meant to stay ignored, or should some scene files be intentionally tracked now that clean checkout validation is required? Evidence: `.gitignore`, `Assets/Scenes/*.unity`, `ProjectSettings/EditorBuildSettings.asset`.
- Should `ProjectSettings/EditorBuildSettings.asset` keep references to missing `SampleScene`, `YachtDice`, and `DiceTest` scene paths? Evidence: `ProjectSettings/EditorBuildSettings.asset`, missing `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/YachtDice.unity`, `Assets/Scenes/DiceTest.unity`.
- Should `SceneBuilderUtility` remain a single shared utility for now, or should future tasks extract only one focused helper at a time? Evidence: `Assets/Editor/SceneBuilderUtility.cs`, `REFACTOR_BACKLOG.md`.
- Should validation failures be fixed before documentation expansion beyond this audit? Evidence: `Assets/Editor/Tests/Battle/EnemyAttackPositionResolverTests.cs`, `Assets/Editor/Tests/Battle/EnemyProjectileAttachmentFollowerTests.cs`, `Assets/Editor/Tests/Mahjong/YakuEvaluatorTests.cs`.

## Asset pipeline questions

- Which assets are source of truth: source images, MP4s, extracted frames, `_transparent` outputs, `_transparent_clean` outputs, or Unity-imported sprites? Evidence: `Assets/Player/Sprites/`, `Assets/Mobs/Sprites/`, `docs/grok-imagine-sprite-prompts.md`.
- Are MP4 files under `Assets/` intentional tracked assets or intermediate generation artifacts? Evidence: `Assets/Player/Sprites/*.mp4`, `Assets/Mobs/Sprites/*/*.mp4`.
- Where should extraction and background-removal scripts live if they are part of the repeatable pipeline? Evidence: `docs/grok-imagine-sprite-prompts.md`, absence of tracked `extract_frames`/`rembg` scripts in inspected repo paths.
- Should sprite frame folders use one canonical naming suffix such as `_transparent_clean`, or are action-specific variants acceptable? Evidence: `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean/`, `Assets/Player/Sprites/*_764x640`, `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png`.

## Production/roadmap questions

- What is the v0.1 acceptance target after Hold'em source implementation: dice plus mahjong plus validated first-pass Hold'em, or dice/mahjong with Hold'em behind a validation gate? Evidence: `CharacterType`, `BattleSceneController`, `MahjongBattleController`, `HoldemBattleController`.
- Is CI expected later, or will validation remain local Unity batchmode only? Evidence: `AGENTS.md`, absence of `.github` and CI config files.
- Should documentation be written as human-facing design docs, Codex operating docs, or both? Evidence: `README.md`, `AGENTS.md`, `.claude/specs/*`, `REFACTOR_BACKLOG.md`.
- Who owns final design decisions for undocumented intent: Human, ChatGPT Project, or Codex after repo inspection? Evidence: unknown from repository files.

# 12. Files inspected

Important inspected files and folders:

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/URPProjectSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `.gitignore`
- `.git/info/exclude`
- `AGENTS.md`
- `README.md`
- `REFACTOR_BACKLOG.md`
- `docs/assets.md`
- `docs/tuning.md`
- `docs/grok-imagine-sprite-prompts.md`
- `docs/task-000c-approved-files.md`
- `.claude/rules/coding.md`
- `.claude/rules/scene-builder.md`
- `.claude/specs/game-flow.md`
- `.claude/specs/battle-system.md`
- `.claude/specs/mahjong-battle.md`
- `Assets/Editor/SceneBuilderUtility.cs`
- `Assets/Editor/MainMenuSceneBuilder.cs`
- `Assets/Editor/CharacterSelectSceneBuilder.cs`
- `Assets/Editor/GameExploreSceneBuilder.cs`
- `Assets/Editor/DiceBattleSceneBuilder.cs`
- `Assets/Editor/MahjongBattleSceneBuilder.cs`
- `Assets/Editor/DicePrefabBuilder.cs`
- `Assets/Editor/Tests/Battle/`
- `Assets/Editor/Tests/Mahjong/`
- `Assets/Scripts/Audio/`
- `Assets/Scripts/Battle/`
- `Assets/Scripts/CharacterSelect/`
- `Assets/Scripts/Dice/`
- `Assets/Scripts/Explore/`
- `Assets/Scripts/Game/`
- `Assets/Scripts/Mahjong/`
- `Assets/Scripts/MainMenu/`
- `Assets/Scripts/Stages/`
- `Assets/Scenes/`
- `Assets/Dices/Prefabs/`
- `Assets/Dices/Generated/`
- `Assets/Mahjong/`
- `Assets/Player/Sprites/`
- `Assets/Mobs/Sprites/`
- `Assets/Backgrounds/`
- `Assets/UI/`
- `Assets/Story/`
- `Assets/Textures/`
- `Assets/Se/`
- `Assets/TextMesh Pro/Resources/`
- `Assets/UniversalRenderPipelineGlobalSettings.asset`
- `Assets/Settings/UniversalRP.asset`
- `Assets/Settings/Renderer2D.asset`
