# Runtime Architecture Current

- Post-audit closure date: 2026-06-12 KST
- Current HEAD at closure: `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`

## Latest Runtime Architecture Delta

This section updates the initial 2026-06-11 baseline after `e1337d16`, `d007d473`, and `311ae2e6`.

Current scene flow in latest `HEAD`:

| Step | Latest evidence | Classification | Confidence |
|---|---|---|---|
| Main menu start | `MainMenuController` route and tracked `Assets/Scenes/MainMenu.unity`. | Implemented and validated | High |
| Character select | `CharacterSelectController`, tracked `Assets/Scenes/CharacterSelect.unity`, current build settings. | Implemented and validated | High |
| Explore loop | `GameExploreController`, tracked `Assets/Scenes/GameExploreScene.unity`, latest player build success. | Implemented and validated | High |
| Dice battle route | `ResolveBattleSceneName` returns `DiceBattleScene` for Dice/default. | Implemented and validated | High |
| Mahjong battle route | `ResolveBattleSceneName` returns `MahjongBattleScene` for Mahjong. | Implemented and validated | High |
| Holdem battle route | `ResolveBattleSceneName` returns `HoldemBattleScene`; Hold'em scripts/builder/assets/tests/scene are tracked. | Implemented and validated with builder churn risk | High |
| Victory/defeat return | Dice, Mahjong, and Hold'em battle controllers return to `GameExploreScene` or `MainMenu`. | Implemented | High |

Latest actual route:

```text
MainMenu -> CharacterSelect -> GameExploreScene -> DiceBattleScene/MahjongBattleScene/HoldemBattleScene -> GameExploreScene or MainMenu
```

Validation evidence:

- Hold'em EditMode 130/130 and player build passed; `HoldemRoutesToHoldemBattleScene` passed.
- Mahjong focused 43/43 and full 131/131 passed.
- Runtime assets full EditMode 132/132 and Windows Standalone x64 build passed.

Remaining architecture caveats:

- Manual visual/playthrough QA was not newly performed.
- Validation hygiene/settings churn remains open.
- Hold'em scene-builder regeneration churn remains open.

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: runtime C# under `Assets/Scripts/**`, editor builders when they wire runtime contracts, build settings, direct scene transition strings, validation results.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Current Scene Flow

Initial audit baseline at `e6de7c9`; use Latest Runtime Architecture Delta above for current `master`.

| Step | Current evidence | Classification | Confidence |
|---|---|---|---|
| Main menu start | `Assets/Scripts/MainMenu/MainMenuController.cs` loads `CharacterSelect` from `OnPlayClicked`. | Implemented | High |
| Character select | `Assets/Scripts/CharacterSelect/CharacterSelectController.cs` selects `CharacterType`, calls `GameSessionManager.StartNewGame(type)`, then loads `GameExploreScene`. | Implemented | High |
| Explore loop | `Assets/Scripts/Explore/GameExploreController.cs` renders current event, configures combat/item/boss, and advances `GameSessionManager.CurrentEventIndex`. | Implemented | High |
| Dice battle route | `ResolveBattleSceneName` returns `DiceBattleScene` for Dice/default. | Implemented | High |
| Mahjong battle route | `ResolveBattleSceneName` returns `MahjongBattleScene` for Mahjong. | Implemented | High |
| Holdem battle route | `ResolveBattleSceneName` returns `DiceBattleScene` for Holdem fallback in tracked code; foreground untracked Holdem files exist but were not validated. | Not current implemented runtime route | High for tracked route, Medium for foreground prototype |
| Victory return | Dice and Mahjong controllers load `GameExploreScene` on victory/return. | Implemented | High |
| Defeat return | Dice and Mahjong controllers load `MainMenu` on defeat. | Implemented | High |

Actual tracked route:

```text
MainMenu -> CharacterSelect -> GameExploreScene -> DiceBattleScene/MahjongBattleScene -> GameExploreScene or MainMenu
```

## Session State Ownership

`Assets/Scripts/Session/GameSessionManager.cs` owns mutable cross-scene state:

- `SelectedCharacter`
- `PlayerHearts`
- `PowerUps`
- `CurrentEventIndex`
- `ExploreMapSeed`
- `CurrentExploreMapNodeId`
- `PendingExploreMapNodeId`
- `CurrentStageId`
- `BattleEnemies`
- `IsBossBattle`
- `LastBattleResult`
- `BossHp`

`StartNewGame(CharacterType)` resets session state, hearts, power-ups, current event index, map route fields, battle enemies, boss state, last battle result, and current stage id to `Stage1Forest.Id`.

Classification: Implemented. Confidence: High.

## Stage Progression Model

Evidence:

- `Assets/Scripts/Stages/StageRegistry.cs`
- `Assets/Scripts/Stages/StageData.cs`
- `Assets/Scripts/Stages/Stage1Forest.cs`
- `Assets/Scripts/Stages/Stage2Cave.cs`
- `Assets/Scripts/Explore/GameExploreController.cs`

Current model:

- `StageRegistry` registers Stage 1 Forest and Stage 2 Cave.
- `StageData.Rounds` is a linear list of `StageRoundType`.
- Round types are `NormalCombat`, `ItemBox`, and `BossCombat`.
- `GameExploreController` consumes `CurrentEventIndex`, advances after battle/item resolution, and moves to next registered stage when available.

Classification: Implemented linear stage progression. Confidence: High.

## Battle Routing

Initial audit baseline at `e6de7c9`; latest `master` routes Holdem to `HoldemBattleScene`.

| Character type | Current route | Evidence | Classification |
|---|---|---|---|
| `CharacterType.Dice` | `DiceBattleScene` | `GameExploreController.ResolveBattleSceneName` | Implemented |
| `CharacterType.Mahjong` | `MahjongBattleScene` | `GameExploreController.ResolveBattleSceneName` | Implemented |
| `CharacterType.Holdem` | `DiceBattleScene` fallback | `GameExploreController.ResolveBattleSceneName` comment and return value | Placeholder/fallback, not implemented as current battle route |

Foreground untracked files under `Assets/Scripts/Holdem/**`, `Assets/Editor/HoldemBattleSceneBuilder.cs`, `Assets/Holdem/**`, and `Assets/Scenes/HoldemBattleScene.unity` show a local Holdem prototype may exist. They are not part of clean validation and do not override the tracked route.

## Dice Battle

Evidence:

- `Assets/Scripts/Battle/BattleSceneController.cs`
- `Assets/Scripts/Battle/DiceRollDirector.cs`
- `Assets/Scripts/Battle/DamageCalculator.cs`
- `Assets/Scripts/Battle/DefenseCalculator.cs`
- `Assets/Scripts/Battle/EnemyDiceRoller.cs`
- `Assets/Scripts/Battle/EnemyCounterAttackDirector.cs`
- `Assets/Scripts/Battle/PlayerAttackPipeline.cs`
- `Assets/Editor/Tests/**` dice-related tests in clean validation

Current behavior:

- Player rolls dice, settles physical dice through `DiceRollDirector`, confirms score, resolves attack/defense/counter logic, advances round, and returns to explore or main menu depending on result.
- Score and damage helpers are covered by EditMode tests in the clean validation run.

Classification: Implemented. Confidence: High for source/test validation; manual visual QA still unknown.

## Mahjong Battle

Evidence:

- `Assets/Scripts/Mahjong/MahjongBattleController.cs`
- `Assets/Scripts/Mahjong/Tile.cs`
- `Assets/Scripts/Mahjong/TileFactory.cs`
- `Assets/Scripts/Mahjong/TileWall.cs`
- `Assets/Scripts/Mahjong/Hand.cs`
- `Assets/Scripts/Mahjong/HandDecomposer.cs`
- `Assets/Scripts/Mahjong/BestHandPicker.cs`
- `Assets/Scripts/Mahjong/YakuEvaluator.cs`
- `Assets/Scripts/Mahjong/PartialHandEvaluator.cs`
- `Assets/Scripts/Mahjong/MahjongDamageTable.cs`
- `Assets/Editor/Tests/**` Mahjong tests in clean validation

Current behavior:

- The controller builds a hand/wall flow, supports discard, riichi, kan, partial attacks, enemy state, yaku evaluation, damage, victory/defeat handling, and scene return.

Classification: Implemented. Confidence: High for source/test validation; manual visual QA still unknown.

## Item / Power-Up System

Evidence:

- `Assets/Scripts/PowerUps/PowerUpType.cs`
- `Assets/Scripts/PowerUps/PowerUpRewardCatalog.cs`
- `Assets/Scripts/Explore/GameExploreController.cs`
- `Assets/Scripts/Session/GameSessionManager.cs`

Current behavior:

- Item-box rounds present power-up choices.
- `GameSessionManager.PowerUps` persists selected power-ups.
- Known power-up values are dice and Mahjong scoped: `OddEvenDouble`, `AllOrNothing`, `ReviveOnce`, `MahjongPartialFocus`, `MahjongYakuFocus`, and `MahjongSafetyCharm`.

Classification: Implemented item-box power-ups. Confidence: High.

Not found as implemented runtime systems:

- Relic economy
- Joker inventory
- Shop purchase system
- Balatro chips/mult scoring

## Node Map / Shop / Relic / Joker / Balatro Scoring

Evidence:

- `GameExploreController` contains map-node presentation and node-kind code in the dirty foreground source.
- `Assets/UI/MapIcons/**` exists as foreground untracked assets.
- No runtime classes named `Relic`, `Joker`, `Chips`, `Mult`, or Balatro-style shared scoring system were found in the audited runtime code.
- Current stage data is still linear `StageData.Rounds`.

Classification:

- Node-map presentation: Partial/foreground-only and requires manual QA.
- Shop/relic/joker/Balatro chips-mult: Not found as implemented runtime systems.

Confidence: High for absence of shared systems by source scan; Medium for foreground map presentation because it was not validated in clean Unity run.

## Debug Console

Evidence:

- `Assets/Scripts/Debug/DebugConsoleController.cs`
- `Assets/Scripts/Debug/DebugCommandProcessor.cs`
- `Assets/Scripts/Debug/IBattleDebugTarget.cs`
- Dice and Mahjong battle controllers implement debug target behavior.

Classification: Implemented debug tooling. Confidence: High for source presence; manual input QA unknown.

## Audio

Evidence:

- `Assets/Scripts/Audio/AudioManager.cs`
- `Assets/Editor/SceneBuilderUtility.cs` audio clip path references
- `Assets/Se/**`

Classification: Implemented basic audio manager/reference layer. Confidence: High for source/reference; audio mix correctness unknown.

## Unknown / Requires Manual QA

- Foreground generated scenes visually match builders.
- Player build can launch after scene/build settings are fixed.
- Runtime visual quality of untracked or generated sprite folders.
- Foreground Holdem prototype behavior, if intentionally in scope later.
- Foreground map presentation and map node interaction behavior.
