# Architecture Map

Factual baseline: `docs/00_actual_project_audit.md`.

## Runtime Scene Flow

```text
MainMenu
  -> CharacterSelect
  -> GameExploreScene
     -> DiceBattleScene, MahjongBattleScene, or HoldemBattleScene
     -> GameExploreScene after battle victory/cancel
     -> MainMenu after player defeat or final stage victory
```

Evidence:

- `Assets/Scripts/MainMenu/MainMenuController.cs`
- `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`
- `Assets/Scripts/Explore/GameExploreController.cs`
- `Assets/Scripts/Battle/BattleSceneController.cs`
- `Assets/Scripts/Mahjong/MahjongBattleController.cs`
- `Assets/Scripts/Holdem/HoldemBattleController.cs`

`CharacterSelectController.SelectWeaponAndStart()` calls `GameSessionManager.StartNewGame(type)` and loads `GameExploreScene`. `GameExploreController.OnFightClicked()` prepares session battle state and loads the battle scene returned by `ResolveBattleSceneName()`.

## Static Session State Ownership

`Assets/Scripts/Game/GameSessionManager.cs` owns mutable cross-scene state:

| State | Role |
|---|---|
| `SelectedCharacter` | Character/combat-mode choice from character select |
| `PlayerHearts` | Player HP container |
| `PowerUps` | Current item-box modifiers |
| `CurrentEventIndex` | Current linear stage-round index |
| `CurrentStageId` / `CurrentStage` | Active stage selection |
| `BattleEnemies` | Prepared enemy snapshot for battle scenes |
| `IsBossBattle` | Boss encounter flag |
| `LastBattleResult` | Battle result consumed by explore |

Key invariants from `AGENTS.md` and `.claude/specs/game-flow.md`:

- `GameSessionManager` owns mutable cross-scene runtime state.
- `CurrentEventIndex` advances only in `GameExploreController`.
- Battle enemy lists should be cloned before mutation.
- New session fields must be reset in `StartNewGame()`.

## Stage Progression Model

Current progression is linear, not a proven node-map system.

Evidence:

- `Assets/Scripts/Stages/StageData.cs` defines `StageRoundType.NormalCombat`, `ItemBox`, and `BossCombat`.
- `Assets/Scripts/Stages/Stage1Forest.cs` defines `NormalCombat -> ItemBox -> BossCombat`.
- `Assets/Scripts/Stages/Stage2Cave.cs` defines `NormalCombat -> NormalCombat -> ItemBox -> BossCombat`.
- `Assets/Scripts/Stages/StageRegistry.cs` registers `Stage1Forest.Build()` and `Stage2Cave.Build()`.
- `Assets/Scripts/Explore/GameExploreController.cs` reads `GameSessionManager.CurrentEventIndex` and the active stage's `rounds`.

## Dice Battle Architecture

| Area | Files / classes | Role |
|---|---|---|
| Scene controller | `Assets/Scripts/Battle/BattleSceneController.cs` | Main dice battle flow, victory/cancel/defeat, debug target |
| Shared battle base | `Assets/Scripts/Battle/BattleControllerBase.cs` | Shared enemy/UI/session setup for battle scenes |
| Dice flow | `Assets/Scripts/Battle/DiceRollDirector.cs` | Player dice rolling, holds, vault, roll state |
| Dice scoring | `Assets/Scripts/Battle/DamageCalculator.cs`, `PlayerAttackPipeline.cs` | Combo damage, splash, power-up application, attack presentation |
| Defense | `Assets/Scripts/Battle/DefenseCalculator.cs` | Player defense success and enemy damage formula |
| Enemy dice | `Assets/Scripts/Battle/EnemyDiceRoller.cs`, `EnemyDiceResult.cs`, `EnemyDiceProfile*.cs` | Enemy dice rolling, combo result, profile/catalog data |
| Counterattack | `Assets/Scripts/Battle/EnemyCounterAttackDirector.cs` | Sequential enemy attack/defense presentation and player damage |
| Dice objects | `Assets/Scripts/Dice/Dice.cs`, `DiceFaceResolver.cs`, `DiceViewportInteraction.cs` | Physical dice, face resolution, UI viewport interaction |

Dice combat uses Yacht-style five-dice combo scoring. It is not a Balatro chips/mult scoring model.

## Mahjong Battle Architecture

| Area | Files / classes | Role |
|---|---|---|
| Scene controller | `Assets/Scripts/Mahjong/MahjongBattleController.cs` | Main mahjong battle flow, draw/discard, attack, cancel/victory/defeat |
| Match state | `Assets/Scripts/Mahjong/MahjongMatchState.cs` | Battle-local mahjong state container |
| Tile model | `Tile.cs`, `TileFactory.cs`, `TileIndex.cs` | Tile values and helpers |
| Wall/hand | `TileWall.cs`, `Hand.cs` | 136-tile wall, draw/dora model, player hand/meld state |
| Hand solving | `HandDecomposer.cs`, `WinningHand.cs`, `BestHandPicker.cs` | Winning hand decomposition and best yaku selection |
| Yaku/damage | `YakuEvaluator.cs`, `MahjongDamageTable.cs`, `PartialHandEvaluator.cs` | Yaku evaluation, full/partial attack damage |
| Enemy waits | `EnemyMahjongState.cs`, `EnemyWaitTilesDisplay.cs`, `MahjongWaitInfoPanel.cs` | Enemy trigger state and wait-info UI |
| Tile visuals | `MahjongTileSpriteDatabase.cs`, `MahjongTileVisual.cs`, `MahjongTileHoverEffect.cs` | Tile sprite mapping and UI interaction |

Mahjong combat is implemented as a 1-player riichi-mahjong-based battle mode. Rank 4-5 mahjong enemy design is still documented as future/uncertain in `.claude/specs/mahjong-battle.md`.

## Hold'em Battle Architecture

| Area | Files / classes | Role |
|---|---|---|
| Scene controller | `Assets/Scripts/Holdem/HoldemBattleController.cs` | Main Hold'em battle flow, reveal/redraw, attack, defense, cancel/victory/defeat |
| Round state | `Assets/Scripts/Holdem/HoldemRoundState.cs`, `HoldemDeck.cs` | Hole/community cards, reveal stages, redraw limits, no-duplicate deck draws |
| Hand evaluation | `HoldemHandEvaluator.cs`, `HoldemPartialHandEvaluator.cs`, `HoldemHandResult.cs` | Full 5-7 card hand ranking and Stage 1 partial ranking |
| Damage | `HoldemDamageTable.cs` | Stage multipliers, 80 damage cap, single-target vs AOE target mode |
| Defense | `HoldemDefenseResolver.cs` | Enemy attack card, five defense cards, equal-or-higher rank block rule |

Hold'em is first-pass source-level implemented and routes through `HoldemBattleScene`. Unity scene generation and play/build validation remain required.

## Editor Scene Builder Architecture

There is no single `SceneBuilder` class. The construction system is:

| Builder | Menu item | Generated scene |
|---|---|---|
| `Assets/Editor/MainMenuSceneBuilder.cs` | `Tools/Build MainMenu Scene` | `Assets/Scenes/MainMenu.unity` |
| `Assets/Editor/CharacterSelectSceneBuilder.cs` | `Tools/Build CharacterSelect Scene` | `Assets/Scenes/CharacterSelect.unity` |
| `Assets/Editor/GameExploreSceneBuilder.cs` | `Tools/Build GameExplore Scene` | `Assets/Scenes/GameExploreScene.unity` |
| `Assets/Editor/DiceBattleSceneBuilder.cs` | `Tools/Build DiceBattle Scene` | `Assets/Scenes/DiceBattleScene.unity` |
| `Assets/Editor/MahjongBattleSceneBuilder.cs` | `Tools/Build MahjongBattle Scene` | `Assets/Scenes/MahjongBattleScene.unity` |
| `Assets/Editor/HoldemBattleSceneBuilder.cs` | `Tools/Build HoldemBattle Scene` | `Assets/Scenes/HoldemBattleScene.unity` |

Shared builder utilities live in `Assets/Editor/SceneBuilderUtility.cs`. That utility owns scene shell creation, UI helpers, battle root construction, stage sprite bundles, audio manager setup, build-settings insertion, and reflection wiring through `SetField()`.

`Assets/Editor/DicePrefabBuilder.cs` is separate from the scene builders. Its menu item is `Tools/Build Dice Prefabs/D6 Mine`, and it writes `Assets/Dices/Prefabs/Dice_d6_mine.prefab`.

## Test Coverage Areas

EditMode tests exist under `Assets/Editor/Tests/`.

| Folder | Coverage examples |
|---|---|
| `Assets/Editor/Tests/Battle/` | `GameSessionManagerTests`, `SceneBuilderUtilityTests`, `DefenseRulesTests`, `DiceFaceResolverTests`, `EnemyDiceProfileCatalogTests`, `EnemyAttackPositionResolverTests`, `EnemyProjectileAttachmentFollowerTests` |
| `Assets/Editor/Tests/Mahjong/` | `TileWallTests`, `HandTests`, `HandDecomposerTests`, `YakuEvaluatorTests`, `DamageTableTests`, `PartialHandEvaluatorTests`, `EnemyMahjongStateTests`, `MahjongBattleControllerTests`, `DefenseCalculatorTests` |
| `Assets/Editor/Tests/Holdem/` | Hand evaluator, partial hand evaluator, round state, damage table, defense resolver, routing tests |

The audit notes known validation uncertainty: current compile/test status must be checked in Unity using the required isolated validation worktree.

## Known Missing Architecture Pieces

| Missing or partial piece | Evidence |
|---|---|
| Hold'em Unity validation | Source-level implementation exists, but `HoldemBattleScene` still needs isolated Unity generation, compile, EditMode, play, and build validation. |
| Node-map progression | Current stage data is a linear `List<StageRoundType>`. No graph/node-map runtime model was found. |
| Shop/relic/joker systems | Search evidence in the audit found only `PowerUpType` item-box modifiers, not full shop/relic/joker/card-modifier frameworks. |
| Shared scoring model | Dice uses `DamageCalculator`; mahjong uses `YakuEvaluator`/`MahjongDamageTable`. No shared chips/mult scoring abstraction was found. |
| Repeatable generated sprite pipeline scripts | Generated assets and prompt docs exist, but extraction/rembg scripts were not found as tracked repo files. |

## Dependency And Risk Table

| Dependency / contract | Evidence | Risk |
|---|---|---|
| `SceneBuilderUtility.SetField()` string names | `Assets/Editor/*SceneBuilder.cs`, `SceneBuilderUtility.cs` | Serialized field renames break generated scenes unless builder strings are updated case-sensitively. |
| Persistent listener methods | `UnityEventTools.AddPersistentListener()` in builders | Public callback renames break buttons unless builders and scenes are regenerated. |
| Scene name strings | `SceneManager.LoadScene("GameExploreScene")`, `"MainMenu"`, battle scene names | Scene renames or stale build settings can break navigation. |
| Ignored generated scenes | `.gitignore`, `Assets/Scenes/*.unity` | Clean checkout may lack generated scenes even though build settings reference them. |
| Hold'em build-settings alignment | Current source routing targets `HoldemBattleScene`; current foreground build settings inspection did not list it | Build/play validation can fail until the builder regenerates the scene and appends it, or the scene is promoted as a tracked stable artifact with `.meta`. |
| Hard-coded asset paths | `SceneBuilderUtility`, scene builders, stage definitions | Asset moves can break sprite/audio/prefab loading. |
| Static session state | `GameSessionManager` | Cross-scene bugs are easy if state is mutated by shared reference or not reset. |
