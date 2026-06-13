# v0.1 Manual QA Checklist

Sources:

- `docs/12_v0_1_scope.md`
- `docs/13_next_backlog.md`
- `docs/01_architecture_map.md`
- `docs/02_unity_scene_and_object_construction.md`
- `docs/15_validation_baseline_result.md`
- `README.md`

This checklist covers the currently implemented v0.1 gameplay loop. Hold'em is now a first-pass source-level implementation and must be validated separately from the older dice/mahjong baseline.

## Required EditMode Test Status Before Manual QA

Do not begin manual QA unless one of these is true:

- The current accepted baseline from `docs/15_validation_baseline_result.md` is still valid: full EditMode suite `68` total, `68` passed, `0` failed, `0` skipped.
- A newer isolated validation-worktree run has replaced that baseline and records full EditMode passing status.

Validation requirements:

- Unity validation must run from a separate Git worktree, not the foreground checkout.
- The validation worktree must not use the same checked-out branch as the foreground checkout.
- Logs, test results, and build outputs must be outside the Unity project root.
- Build validation must use explicit `-buildTarget StandaloneWindows64`.
- After validation, run `git status --porcelain` in the validation worktree.
- Treat tracked-file changes caused by validation as a failure unless explicitly allowed.

Known baseline caveat:

- `docs/15_validation_baseline_result.md` reports passing EditMode tests, but Unity dirtied tracked settings in the isolated validation worktree. That is a validation hygiene risk, not an accepted foreground project change.
- That older baseline does not cover the new Hold'em tests. Hold'em validation requires a newer isolated EditMode run.

## QA Run Record

| Field | Value |
|---|---|
| QA date |  |
| Tester |  |
| Unity version | `6000.3.11f1` |
| Foreground project path |  |
| Validation baseline used |  |
| Build target | Windows Standalone x64 |
| Notes |  |

## Checklist

### 1. Main Menu

| Field | Detail |
|---|---|
| Relevant scene | `MainMenu` |
| Relevant scripts | `Assets/Scripts/MainMenu/MainMenuController.cs`, `Assets/Scripts/MainMenu/MainMenuButtonHandler.cs`, `Assets/Scripts/MainMenu/SettingsController.cs`, `Assets/Scripts/Game/AudioManager.cs` |
| Risk level | Medium |
| Steps | 1. Open `MainMenu` and enter Play Mode. 2. Verify the logo and menu buttons are visible. 3. Click Play. 4. Return to `MainMenu` later from exposed return paths and verify the menu works again. 5. Open and close Settings and Credits if visible. |
| Expected result | Main menu renders without missing UI or console errors. Play loads `CharacterSelect`. Settings and credits popups open and close without blocking input. Returning to main menu leaves the menu usable. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 2. Character Select

| Field | Detail |
|---|---|
| Relevant scene | `CharacterSelect` |
| Relevant scripts | `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Game/GameSessionManager.cs` |
| Risk level | Medium |
| Steps | 1. Arrive from `MainMenu`. 2. Advance through each story slide by clicking. 3. Use Skip to reach weapon selection. 4. Use Back where exposed. 5. Select Dice. 6. Repeat from fresh runs and select Mahjong, then Hold'em. |
| Expected result | Slides, subtitle text, fade behavior, Skip, Back, and weapon buttons respond. Dice, Mahjong, and Hold'em each call new-game setup and load `GameExploreScene`. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 3. Dice Character Path

| Field | Detail |
|---|---|
| Relevant scene | `CharacterSelect`, `GameExploreScene`, `DiceBattleScene` |
| Relevant scripts | `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Game/GameSessionManager.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Battle/DiceRollDirector.cs` |
| Risk level | High |
| Steps | 1. Start a new run from `MainMenu`. 2. Select Dice in `CharacterSelect`. 3. Confirm `GameExploreScene` loads. 4. Start the first fight. 5. Confirm the loaded battle scene is `DiceBattleScene`. 6. Complete or debug-complete the battle and return to explore. |
| Expected result | `SelectedCharacter` behaves as Dice. Explore resolves combat to `DiceBattleScene`. Dice battle initializes dice, enemy UI, player hearts, and stage background. Victory returns to `GameExploreScene` with session state intact. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 4. Mahjong Character Path

| Field | Detail |
|---|---|
| Relevant scene | `CharacterSelect`, `GameExploreScene`, `MahjongBattleScene` |
| Relevant scripts | `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Game/GameSessionManager.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs` |
| Risk level | High |
| Steps | 1. Start a new run from `MainMenu`. 2. Select Mahjong in `CharacterSelect`. 3. Confirm `GameExploreScene` loads. 4. Start the first fight. 5. Confirm the loaded battle scene is `MahjongBattleScene`. 6. Complete or debug-complete the battle and return to explore. |
| Expected result | `SelectedCharacter` behaves as Mahjong. Explore resolves combat to `MahjongBattleScene`. Mahjong battle initializes hand, draw tile area, discard area, dora display, enemy wait UI, hearts, and stage background. Victory returns to `GameExploreScene` with session state intact. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 5. Hold'em Battle Path

| Field | Detail |
|---|---|
| Relevant scene | `CharacterSelect`, `GameExploreScene`, `HoldemBattleScene` |
| Relevant scripts | `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Holdem/HoldemBattleController.cs`, `Assets/Scripts/Holdem/*`, `Assets/Scripts/Game/GameSessionManager.cs` |
| Risk level | High |
| Precondition | In the validation worktree, generate the scene with `Tools/Build HoldemBattle Scene` unless `Assets/Scenes/HoldemBattleScene.unity` and its `.meta` have been intentionally promoted as tracked stable runtime artifacts. |
| Steps | 1. Start a new run from `MainMenu`. 2. Select Hold'em. 3. Confirm `GameExploreScene` loads. 4. Start a fight. 5. Confirm `HoldemBattleScene` loads. 6. Verify reveal stages show exactly 1, then 3, then 5 community cards. 7. Redraw each hole card twice and confirm the third attempt is unavailable or ignored. 8. Use community redraw once and confirm a second attempt is unavailable or ignored. 9. Confirm High Card damages only the selected target. 10. Confirm One Pair or better deals AOE damage. 11. Confirm enemy defense minigame appears and equal/higher defense rank blocks while lower rank fails. 12. Confirm victory, cancel, defeat, and return paths. |
| Expected result | Hold'em initializes from session enemies and hearts, routes from explore to `HoldemBattleScene`, uses the 1/3/5 reveal flow, enforces redraw limits, applies single-target versus AOE damage rules, runs the defense card minigame, and returns through the documented victory/cancel/defeat paths without breaking Dice or Mahjong routing. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 6. Explore Scene

| Field | Detail |
|---|---|
| Relevant scene | `GameExploreScene` |
| Relevant scripts | `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Game/GameSessionManager.cs`, `Assets/Scripts/Stages/StageData.cs`, `Assets/Scripts/Stages/Stage1Forest.cs`, `Assets/Scripts/Stages/Stage2Cave.cs`, `Assets/Scripts/Stages/StageRegistry.cs` |
| Risk level | High |
| Steps | 1. Enter `GameExploreScene` from Dice and from Mahjong runs. 2. Verify player hearts, player name, power-up text, background, and encounter panel. 3. Advance through the visible stage sequence. 4. Confirm normal combat, item box, and boss combat rounds appear in order for the active stage. 5. Use `/stage` and `/nextround` only if debug QA is needed. |
| Expected result | Explore initializes from `GameSessionManager`. Stage rounds follow the linear `StageData` sequence. `CurrentEventIndex` advances through explore behavior after completed encounters and does not visibly skip or repeat rounds incorrectly. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 7. Normal Combat

| Field | Detail |
|---|---|
| Relevant scene | `DiceBattleScene`, `MahjongBattleScene`, then `GameExploreScene` |
| Relevant scripts | `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`, `Assets/Scripts/Battle/BattleControllerBase.cs`, `Assets/Scripts/Game/GameSessionManager.cs` |
| Risk level | High |
| Steps | 1. Start a normal combat from explore with Dice. 2. Roll, hold, unhold, confirm score, and let enemy counterattack occur. 3. Win the battle. 4. Repeat normal combat from explore with Mahjong. 5. Draw, discard, use partial attack, and complete a win or debug-complete enemy defeat. |
| Expected result | Normal combat enemies are prepared from session state. Dice and Mahjong each allow their current attack loop, enemy damage/player damage can occur, and victory records a result consumed by explore. The game returns to `GameExploreScene` after battle victory. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 8. Item Box / Power-Up Selection

| Field | Detail |
|---|---|
| Relevant scene | `GameExploreScene` |
| Relevant scripts | `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Game/GameSessionManager.cs`, `Assets/Scripts/Game/PowerUpType.cs`, `Assets/Scripts/Battle/DamageCalculator.cs` |
| Risk level | Medium |
| Steps | 1. Advance to an item-box round. 2. Verify item-card UI appears. 3. Select each visible power-up option across repeated runs if possible. 4. Confirm the selected power-up appears in explore UI. 5. Enter the next combat and observe that combat remains playable. |
| Expected result | Item-box selection updates `GameSessionManager.PowerUps`, advances the explore flow, and does not corrupt hearts, selected character, current event index, or battle scene selection. Expected v0.1 power-ups are `OddEvenDouble`, `AllOrNothing`, and `ReviveOnce`. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 9. Boss Combat

| Field | Detail |
|---|---|
| Relevant scene | `GameExploreScene`, `DiceBattleScene`, `MahjongBattleScene` |
| Relevant scripts | `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Game/GameSessionManager.cs`, `Assets/Scripts/Stages/StageData.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs` |
| Risk level | High |
| Steps | 1. Advance to a boss round with Dice. 2. Verify boss encounter UI appears in explore. 3. Start combat and verify boss enemy data appears in the battle scene. 4. Win the boss fight. 5. Repeat with Mahjong if time allows. |
| Expected result | `IsBossBattle` is set for boss encounters. Battle scene uses boss data rather than a normal enemy set. Winning the boss combat advances stage or triggers final victory behavior according to the current stage progression. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 10. Victory Path

| Field | Detail |
|---|---|
| Relevant scene | `GameExploreScene`, `MainMenu`, battle scenes |
| Relevant scripts | `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Game/GameSessionManager.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs` |
| Risk level | High |
| Steps | 1. Complete normal combat and confirm return to explore. 2. Complete an item-box round and continue. 3. Complete boss combat for the current stage. 4. Continue until final stage victory if reachable. 5. Observe whether the game routes to a victory panel, next stage, or `MainMenu`. |
| Expected result | Battle victory returns to explore for non-final rounds. Boss victory advances the intended stage or final victory route. Final stage victory must not depend on missing scenes and should route to `MainMenu` or the documented intended victory flow. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 11. Defeat Path

| Field | Detail |
|---|---|
| Relevant scene | `DiceBattleScene`, `MahjongBattleScene`, `MainMenu` |
| Relevant scripts | `Assets/Scripts/Game/GameSessionManager.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`, `Assets/Scripts/Battle/HeartDisplay.cs`, `Assets/Scripts/Game/DebugConsole.cs` |
| Risk level | High |
| Steps | 1. Enter Dice battle. 2. Take damage until defeat or use `/kill player` if debug QA is enabled. 3. Confirm defeat routing. 4. Repeat in Mahjong battle if possible. 5. Start a new game after defeat. |
| Expected result | Player hearts reach defeat cleanly. Defeat routes to `MainMenu` or the intended restart path without missing-scene errors. Starting a new game resets hearts, power-ups, current event index, stage state, battle enemies, boss flag, and last battle result. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 12. Return To Main Menu

| Field | Detail |
|---|---|
| Relevant scene | `GameExploreScene`, `MainMenu`, battle scenes |
| Relevant scripts | `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`, `Assets/Scripts/MainMenu/MainMenuController.cs` |
| Risk level | Medium |
| Steps | 1. Use any exposed return-to-main-menu control in explore. 2. Use battle cancel where exposed. 3. Trigger defeat. 4. Trigger final victory if reachable. 5. From `MainMenu`, start a new Dice or Mahjong run after each return path. |
| Expected result | Every exposed return path loads `MainMenu` or the documented intended destination. A new run after returning starts from clean session state and does not reuse stale enemies, power-ups, stage index, or hearts. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 13. Scene Transition Regressions

| Field | Detail |
|---|---|
| Relevant scene | `MainMenu`, `CharacterSelect`, `GameExploreScene`, `DiceBattleScene`, `MahjongBattleScene`, `HoldemBattleScene` |
| Relevant scripts | `Assets/Scripts/MainMenu/MainMenuButtonHandler.cs`, `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`, `Assets/Scripts/Holdem/HoldemBattleController.cs`, `Assets/Scripts/Game/GameSessionManager.cs` |
| Risk level | High |
| Steps | 1. Traverse `MainMenu -> CharacterSelect -> GameExploreScene -> DiceBattleScene -> GameExploreScene`. 2. Traverse `MainMenu -> CharacterSelect -> GameExploreScene -> MahjongBattleScene -> GameExploreScene`. 3. Traverse `MainMenu -> CharacterSelect -> GameExploreScene -> HoldemBattleScene -> GameExploreScene`. 4. Confirm all required runtime scenes are present in build settings or generated by the documented validation step. 5. Watch for missing-scene, missing-reference, or additive duplicate-object errors. |
| Expected result | Runtime scene names and build settings stay aligned: `MainMenu`, `CharacterSelect`, `GameExploreScene`, `DiceBattleScene`, `MahjongBattleScene`, and `HoldemBattleScene`. No stale `SampleScene`, `YachtDice`, or `DiceTest` dependency is required for v0.1 play validation. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 14. UI Interaction Regressions

| Field | Detail |
|---|---|
| Relevant scene | All runtime scenes |
| Relevant scripts | `Assets/Scripts/MainMenu/MainMenuButtonHandler.cs`, `Assets/Scripts/CharacterSelect/CharacterSelectController.cs`, `Assets/Scripts/Explore/GameExploreController.cs`, `Assets/Scripts/Battle/DiceRollDirector.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`, `Assets/Scripts/Mahjong/MahjongTileVisual.cs`, `Assets/Scripts/Game/UIHoverEffect.cs` |
| Risk level | High |
| Steps | 1. Click every visible primary button in each scene. 2. Verify disabled buttons cannot trigger invalid actions. 3. Hover/click held dice slots and mahjong tiles. 4. Select enemy panels in battle. 5. Open and close any battle log/history/wait-info UI that is visible. |
| Expected result | Buttons invoke their intended callbacks. UI hover effects do not block clicks. Dice hold/unhold and mahjong tile selection remain responsive. Enemy targeting works. No UI gets stuck with blocked raycasts or hidden modal state. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 15. Sprite / Animation Visibility Regressions

| Field | Detail |
|---|---|
| Relevant scene | `GameExploreScene`, `DiceBattleScene`, `MahjongBattleScene`, `HoldemBattleScene`, `CharacterSelect`, `MainMenu` |
| Relevant scripts | `Assets/Scripts/Game/SpriteAnimator.cs`, `Assets/Scripts/Battle/PlayerBodyAnimator.cs`, `Assets/Scripts/Battle/PlayerAttackAnimator.cs`, `Assets/Scripts/Battle/PlayerDeathAnimator.cs`, `Assets/Scripts/Battle/EnemySpriteAnimator.cs`, `Assets/Scripts/Battle/EnemyProjectileAttachmentFollower.cs`, `Assets/Scripts/Mahjong/MahjongTileSpriteDatabase.cs`, `Assets/Scripts/Mahjong/MahjongTileVisual.cs` |
| Risk level | High |
| Steps | 1. Verify main menu logo and story slide images are visible. 2. Verify explore background, player sprite, enemy bodies, and projectile previews are visible. 3. In Dice battle, roll dice and observe player attack, enemy attack, defense, hit, death, and projectile animations. 4. In Mahjong battle, verify tile faces, dora indicator, discard tiles, wait displays, ron bubble, attack visuals, and enemy bodies. 5. Watch for fallback blank sprites, wrong scale, invisible projectiles, or offscreen animation anchors. |
| Expected result | Required sprites and animations are visible and anchored plausibly. Missing assets produce understood fallback behavior only where documented. Player, enemy, dice, projectile, and mahjong tile visuals do not disappear during normal play. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

### 16. Audio / Debug Console Regressions

| Field | Detail |
|---|---|
| Relevant scene | All runtime scenes, especially battle scenes and explore |
| Relevant scripts | `Assets/Scripts/Game/AudioManager.cs`, `Assets/Scripts/Game/DebugConsole.cs`, `Assets/Scripts/Battle/BattleSceneController.cs`, `Assets/Scripts/Mahjong/MahjongBattleController.cs`, `Assets/Scripts/Explore/GameExploreController.cs` |
| Risk level | Medium |
| Steps | 1. Verify expected BGM/SFX play where currently implemented. 2. Adjust settings sliders if available and confirm values change without errors. 3. Enter the debug console with the Konami command if debug QA is enabled. 4. Run `/help`, `/stage`, `/nextround`, `/kill mob @a`, and `/kill player` only in a controlled QA run. 5. Close the console with `Esc`. |
| Expected result | Audio sources initialize without missing-reference errors. Settings do not break audio playback. Debug console opens, accepts supported commands, closes, and does not corrupt session state beyond the requested command effect. Commands unavailable in a given scene fail gracefully. |
| Pass/fail | [ ] Pass [ ] Fail |
| Notes |  |

## Known Out-Of-Scope Features

- Slay the Spire-style node map.
- Shop system.
- Relic system.
- Joker system.
- Explicit Balatro-style shared chips/mult scoring model.
- Broad `SceneBuilderUtility` or scene-builder refactor.
- Broad controller rewrites.
- Asset deletion, asset moves, or `.meta` churn.
- Manual edits to generated `.unity` scene files.
- Rank 4-5 Mahjong enemy design beyond the currently implemented behavior.
- Open-meld Mahjong modeling for normal `Toitoi` behavior.

## Known Fragile Areas

- Runtime scene transitions use hard-coded scene names.
- Build settings must contain only intentional v0.1 runtime scene paths.
- `SceneBuilderUtility.SetField()` uses case-sensitive private field names as scene contracts.
- Persistent button callbacks are public method contracts wired by builders.
- Hard-coded asset paths can produce missing sprites, missing audio, or fallback visuals after asset moves.
- Static `GameSessionManager` state can leak across scene transitions if new-game reset paths are missed.
- `CurrentEventIndex` must advance only through `GameExploreController`.
- Battle enemy lists should be deep-copied before mutation.
- Unity validation may dirty tracked settings in an isolated worktree.
- README still contains an older statement that scene files are not included in Git; the stabilized scene policy in `docs/02_unity_scene_and_object_construction.md` supersedes that with the accepted Hybrid policy.

## Missing Information To Capture During QA

- Exact final victory presentation after the last boss: main menu route, victory panel, or another intended flow.
- Hold'em scene/build-settings policy: tracked stable artifact with `.meta`, or generated-only with mandatory regeneration before validation.
- Whether all expected audio cues are currently assigned or whether some scenes intentionally run silent.
- Whether debug console should remain accessible in player-facing v0.1 builds or only in QA/editor runs.
- Any reproducible route where Unity dirties tracked files during validation or scene opening.

## Recommended First QA Run Order

1. Confirm required EditMode status from `docs/15_validation_baseline_result.md` or a newer isolated validation baseline.
2. Smoke-test `MainMenu -> CharacterSelect -> Dice -> GameExploreScene`.
3. Complete one Dice normal combat and verify return to explore.
4. Advance to item box, choose a power-up, and verify the next encounter remains playable.
5. Complete Dice boss combat and record victory or stage-advance behavior.
6. Trigger Dice defeat and verify return/new-game reset.
7. Smoke-test `MainMenu -> CharacterSelect -> Mahjong -> GameExploreScene`.
8. Complete one Mahjong normal combat and verify return to explore.
9. Advance Mahjong through item box and boss if time allows.
10. Generate `HoldemBattleScene` in the validation worktree if needed.
11. Smoke-test `MainMenu -> CharacterSelect -> Hold'em -> GameExploreScene`.
12. Complete one Hold'em normal combat path or debug-complete enough to verify victory, cancel, and defeat routes.
13. Run cross-scene UI, sprite/animation, audio, and debug-console regression passes.
