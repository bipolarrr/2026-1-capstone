# Unity Scene And Object Construction

Factual baseline: `docs/00_actual_project_audit.md`. This document records current construction contracts and risks only. It does not propose broad refactors.

## Construction Model

The project does not have one class named `SceneBuilder`. Scene generation is implemented by `SceneBuilderUtility` plus scene-specific editor builders under `Assets/Editor/`.

Builders own:

- Scene object creation
- Canvas/EventSystem/Input System UI setup
- UI layout
- Runtime component attachment
- Serialized field wiring through `SceneBuilderUtility.SetField()`
- Button callback wiring through `UnityEventTools.AddPersistentListener()`
- Saving generated `.unity` scenes

Runtime logic belongs in runtime `MonoBehaviour` classes under `Assets/Scripts/`.

## Scene Builder Menu Items

| Menu item | Builder | Generated scene |
|---|---|---|
| `Tools/Build MainMenu Scene` | `Assets/Editor/MainMenuSceneBuilder.cs` | `Assets/Scenes/MainMenu.unity` |
| `Tools/Build CharacterSelect Scene` | `Assets/Editor/CharacterSelectSceneBuilder.cs` | `Assets/Scenes/CharacterSelect.unity` |
| `Tools/Build GameExplore Scene` | `Assets/Editor/GameExploreSceneBuilder.cs` | `Assets/Scenes/GameExploreScene.unity` |
| `Tools/Build DiceBattle Scene` | `Assets/Editor/DiceBattleSceneBuilder.cs` | `Assets/Scenes/DiceBattleScene.unity` |
| `Tools/Build MahjongBattle Scene` | `Assets/Editor/MahjongBattleSceneBuilder.cs` | `Assets/Scenes/MahjongBattleScene.unity` |
| `Tools/Build HoldemBattle Scene` | `Assets/Editor/HoldemBattleSceneBuilder.cs` | `Assets/Scenes/HoldemBattleScene.unity` |

Related non-scene menu item:

| Menu item | Builder | Output |
|---|---|---|
| `Tools/Build Dice Prefabs/D6 Mine` | `Assets/Editor/DicePrefabBuilder.cs` | `Assets/Dices/Prefabs/Dice_d6_mine.prefab` |

## What Each Builder Creates

| Builder | Creates |
|---|---|
| `MainMenuSceneBuilder` | Main menu scene shell, logo image, play/settings/credits buttons, settings popup, credits popup, `MainMenuController`, `MainMenuButtonHandler`, `SettingsController`, popup close callbacks, audio manager |
| `CharacterSelectSceneBuilder` | Cutscene slide scene shell, story image slides, subtitle UI, click catcher, skip/back buttons, weapon selection slide with mahjong/holdem/dice buttons, `CharacterSelectController`, audio manager |
| `GameExploreSceneBuilder` | Explore scene shell, scrolling/background image, player sprite, heart display, power-up text, combat encounter UI, enemy slots, item-box cards, victory panel, `GameExploreController`, debug console, audio manager |
| `DiceBattleSceneBuilder` | Dice battle scene shell, dice physics arena, render textures/materials/physics materials, player dice, enemy dice arena, dice viewport and held-dice UI, battle root, enemy UI, player rig/animations, buttons, `BattleSceneController`, `DiceRollDirector`, `EnemyDiceRoller`, `EnemyCounterAttackDirector`, debug console, audio manager |
| `MahjongBattleSceneBuilder` | Mahjong battle scene shell, shared battle root, mahjong table/lower UI, dora/discard/hand/draw tile areas, in-scene tile prefab, wait displays, ron bubble, wait info panel, action buttons, `MahjongBattleController`, draw/attack helpers, debug console/audio through shared helpers where used |
| `HoldemBattleSceneBuilder` | Hold'em battle scene shell, shared battle root, card/reveal/redraw UI, defense card panel, action buttons, `HoldemBattleController`, player/enemy animation helpers, debug console, audio manager |

## Scene Source Policy

Accepted policy: Hybrid.

Stable runtime scenes are tracked as `.unity` files for clean checkout safety, while builders remain the authoring/regeneration source for scene structure and wiring. Other experimental, obsolete, or temporary local scenes under `Assets/Scenes/` remain ignored unless explicitly promoted later.

Tracked stable runtime scenes:

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/CharacterSelect.unity`
- `Assets/Scenes/GameExploreScene.unity`
- `Assets/Scenes/DiceBattleScene.unity`
- `Assets/Scenes/MahjongBattleScene.unity`

Each tracked scene must include its `.unity.meta` file to preserve the scene GUID used by build settings.

`HoldemBattleScene` is now required by source routing but has not yet been added to the tracked stable scene list in this document. If it is promoted, generate `Assets/Scenes/HoldemBattleScene.unity` and `Assets/Scenes/HoldemBattleScene.unity.meta` through Unity and review them as generated artifacts before commit. If it remains generated-only, regenerate it before play/build validation.

Builder regeneration entry points remain:

- `Tools/Build MainMenu Scene`
- `Tools/Build CharacterSelect Scene`
- `Tools/Build GameExplore Scene`
- `Tools/Build DiceBattle Scene`
- `Tools/Build MahjongBattle Scene`
- `Tools/Build HoldemBattle Scene`

Do not hand-edit tracked scene YAML as a gameplay or UI authoring mechanism. Scene changes should still be made through builders, then regenerated and reviewed as generated artifact changes.

## Scenes Referenced By Build Settings

`ProjectSettings/EditorBuildSettings.asset` currently references these enabled runtime scenes:

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/CharacterSelect.unity`
- `Assets/Scenes/DiceBattleScene.unity`
- `Assets/Scenes/GameExploreScene.unity`
- `Assets/Scenes/MahjongBattleScene.unity`

The inspected foreground build settings do not yet list `Assets/Scenes/HoldemBattleScene.unity`. `HoldemBattleSceneBuilder` saves that scene through `SceneBuilderUtility.SaveSceneAndShowDialog()`, whose helper appends the scene to `EditorBuildSettings`; run and review that in an isolated validation worktree or promote the generated scene and `.meta` intentionally.

## Scene Tracking Rules

`.gitignore` ignores other local `Assets/Scenes/*` files but explicitly allows these tracked runtime scenes and matching `.meta` files:

- `MainMenu.unity`
- `MainMenu.unity.meta`
- `CharacterSelect.unity`
- `CharacterSelect.unity.meta`
- `GameExploreScene.unity`
- `GameExploreScene.unity.meta`
- `DiceBattleScene.unity`
- `DiceBattleScene.unity.meta`
- `MahjongBattleScene.unity`
- `MahjongBattleScene.unity.meta`

If a new runtime scene becomes required, either add it to the tracked stable scene set with its `.meta` file, or document a reliable builder regeneration procedure and build validation step before treating it as required. For Hold'em, the current generated-only procedure is `Tools/Build HoldemBattle Scene` before play/build validation.

## Missing Or Stale Scene References

These stale build settings references were removed from the required runtime build settings list:

- `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/YachtDice.unity`
- `Assets/Scenes/DiceTest.unity`

They have no matching current builder and were not present locally under `Assets/Scenes/`. Do not treat them as v0.1 runtime source of truth unless a human owner restores one intentionally.

Runtime scene strings observed in code:

- `MainMenu`
- `CharacterSelect`
- `GameExploreScene`
- `DiceBattleScene`
- `MahjongBattleScene`
- `HoldemBattleScene`

Because scene names are hard-coded in runtime flow and generated scene paths are in build settings, renaming scene assets or leaving stale build-settings entries can break navigation/build validation.

## `SceneBuilderUtility.SetField()` Contracts

`SceneBuilderUtility.SetField(object target, string fieldName, object value)` uses reflection to set private/protected runtime fields by name. Field names are case-sensitive. The string names below are scene construction contracts.

### Shared Battle Root Contracts

`SceneBuilderUtility.BuildBattleRootBase()` wires fields on `BattleControllerBase` subclasses:

| Target | Field names |
|---|---|
| `BattleControllerBase` / subclass | `fightBackgroundImage`, `stageBundles`, `playerBody`, `playerBodyAnimator`, `enemyPanels`, `enemyBodies`, `enemyIdleProjectiles`, `enemyAnimators`, `enemyNames`, `enemyHpFills`, `enemyHpTexts`, `targetMarkers`, `deadOverlays`, `enemyDeathGroundY`, `heartDisplay`, `battleLog`, `bottomFocus`, `vfx`, `battleAnims` |
| `BattleDamageVFX` | `damageSpawnParent`, `enemyPanels` |
| `BattleLog` | `logText`, `scrollRect` |
| `BattleBottomFocusController` | `inputGroup`, `messageGroup`, `messageText`, `messageAdvanceButton`, `historyGroup`, `historyText`, `historyScroll`, `logButton`, `closeHistoryButton` |
| `HeartDisplay` | `slotRoot`, `heartImages`, `emptyHeartSprite`, `halfHeartSprite`, `fullHeartSprite`, `slotSize` |
| `PlayerBodyAnimator` | `playerBody`, `frameRate`, `idleFrameRate`, `idleSprites`, `lowHpSprites`, `jumpSprites`, `defenseSprites`, `smallHitSprites`, `strongHitSprites`, `debuffSprites`, `attackDisplaySprites`, `deathDisplaySprites`, `smallHitFrameStep`, `strongHitFrameStep`, `lowHpIntroEndFrame`, `lowHpLoopStartFrame`, `lowHpLoopEndFrame` |
| `PlayerAttackAnimator` | `playerBody`, `weaponProjectile`, `bodyAnimator`, `frameRate`, `frameStep`, `attackBodyScaleMultiplier`, `weaponVisibleRatio`, `weaponLaunchRatio`, `projectileEndRatio`, `impactNormalizedTime`, `handAttachStartOffset`, `handAttachEndOffset`, `projectileTargetOffset`, `attackSprites` |
| `EnemySpriteAnimator` | `targetImage`, `idleFrameRate`, `actionFrameRate`, `deathFrameRate` |
| `EnemyProjectileAttachmentFollower` | `projectileImage`, `animator`, `size`, `normalizedX`, `normalizedY`, `rotation`, `scaleX`, `releasePointOnArrow` |
| `AudioManager` | `clips`, `source`, `drumRollSource`, `drumRollClip` |

### Main Menu Contracts

| Target | Field names |
|---|---|
| `MainMenuController` | `logoGroup`, `menuButtonsGroup` |
| `MainMenuButtonHandler` | `menuController`, `settingsPopup`, `creditsPopup` |
| `SimplePopup` | `dimmer` |
| `SettingsController` | `bgmSlider`, `sfxSlider`, `bgmValueLabel`, `sfxValueLabel` |

### Character Select Contracts

| Target | Field names |
|---|---|
| `CharacterSelectController` | `slides`, `subtitleText`, `clickCatcher`, `skipButton`, `fadeGroup` |

Weapon button callbacks are found by child object paths under `WeaponButtonsRow/Btn_Mahjong`, `WeaponButtonsRow/Btn_Holdem`, and `WeaponButtonsRow/Btn_Dice`.

### Explore Contracts

| Target | Field names |
|---|---|
| `SpriteAnimator` | `amplitude`, `speed` |
| `UIHoverEffect` | `targetText`, `targetImage`, `outlineColor` |
| `GameExploreController` | `heartDisplay`, `powerUpText`, `playerBody`, `playerNameText`, `encounterPanel`, `encounterTitle`, `itemEncounterTitle`, `combatGroup`, `enemySlots`, `enemyBodies`, `enemyIdleProjectiles`, `enemyNames`, `enemyHpFills`, `enemyHpTexts`, `stageBundles`, `backgroundImage`, `fightButton`, `fleeButton`, `itemGroup`, `itemButtons`, `itemTitles`, `itemDescs`, `victoryPanel` |

### Dice Battle Contracts

| Target | Field names |
|---|---|
| `BattleSceneController` | `rollButton`, `confirmButton`, `cancelButton`, `nextRoundButton`, `diceDirector`, `deathAnimator`, `attackAnimator`, `hud`, `counterAttackDirector` |
| `DiceRollDirector` | `dice`, `viewportInteraction`, `rollButton`, `vaultCenter`, `slotCenter`, `slotSpacing`, `slotRowSpacing`, `heldDiceImages`, `diceFaceSprites` |
| `DiceViewportInteraction` | `viewport`, `diceCamera`, `diceLayerIndex` |
| `EnemyDiceRoller` | `enemyDice`, `vaultCenter`, `diceCamera`, `profileCatalog`, `diceSpacing` |
| `PlayerDeathAnimator` | `playerBody`, `bodyAnimator`, `frameRate`, `deathSprites`, `screenDimmer` |
| `PlayerJumpAnimator` | `playerBody`, `belowEffect`, `bodyAnimator`, `belowSprites`, `jumpDuration` |
| `BattleHudPresenter` | `rollDotsText`, `damagePreviewText`, `comboLabel` |
| `EnemyCounterAttackDirector` | `enemyDiceRoller`, `enemyDicePopup`, `enemyDiceOverlay`, `enemyDiceResultTexts`, `jumpAnimator`, `enemyProjectile` |
| `Dice` | `outlineBaseMaterial` |
| Held dice slot `UIHoverEffect` | `targetImage`, `fontSizeBoost`, `scaleFactor`, `transitionDuration`, `outlineColor`, `outlineDistance`, `shadowColor`, `shadowDistance` |

### Mahjong Battle Contracts

| Target | Field names |
|---|---|
| `MahjongBattleController` | `enemyPanelButtons`, `doraIndicatorRoot`, `handTilesRoot`, `drawTileSlot`, `discardRoot`, `tilePrefab`, `tileSprites`, `kanButton`, `riichiButton`, `tempButton1`, `cancelButton`, `drawAnimator`, `waitInfoPanel`, `waitDisplays`, `ronBubble`, `intuitionConfig`, `enemyProjectile`, `attackAnimator`, `enemyTsumoChancePerTurn`, `rank3WaitRevealChancePerTurn` |
| `MahjongTileVisual` | `background`, `label`, `redMarker`, `skullOverlay`, `hoverEffect`, `discardTooltip` |
| `MahjongTileHoverEffect` | `content`, `highlightBorder`, `nameLabel`, `tileVisual`, `hitboxExtender` |
| `EnemyWaitTilesDisplay` | `slotA`, `slotB`, `slotNeed`, `imgA`, `imgB`, `imgNeed`, `markA`, `markB`, `markNeed`, `group` |
| `RonSpeechBubble` | `root`, `label` |
| `MahjongWaitInfoPanel` | `root`, `headerText`, `damageText`, `tilesRoot`, `arrowText`, `tilePrefab`, `tileSprites` |

### Hold'em Battle Contracts

| Target | Field names |
|---|---|
| `HoldemBattleController` | `holeCardImages`, `holeCardLabels`, `holeRedrawCountLabels`, `holeCardViews`, `communityCardImages`, `communityCardLabels`, `communityCardViews`, `cardFaceSprite`, `cardBackSprite`, `cardFrontSprites`, `stageLabel`, `handResultLabel`, `damagePreviewLabel`, `battleMessageView`, `enemyAttackCardViews`, `attackButton`, `redrawHole0Button`, `redrawHole1Button`, `redrawCommunityButton`, `cancelButton`, `defensePanel`, `defensePanelGroup`, `defensePanelRect`, `defenseEnemyCardImage`, `defenseEnemyCardLabel`, `defenseEnemyCardView`, `defenseCardImages`, `defenseCardLabels`, `defenseCardViews`, `defenseButtons`, `defenseResultLabel`, `defenseBackSprite`, `deathAnimator`, `attackAnimator`, `playerBodyAnimator`, `enemyProjectile` |

## Persistent Button Callback Contracts

Public methods wired by builders are scene callback contracts. Renaming them requires builder updates and scene regeneration.

| Builder | Persistent callbacks |
|---|---|
| `MainMenuSceneBuilder` | `MainMenuButtonHandler.OnPlayClicked`, `OnSettingsClicked`, `OnCreditsClicked`; `SimplePopup.Close` for popup close buttons |
| `CharacterSelectSceneBuilder` | `CharacterSelectController.AdvanceSlide`, `SkipToWeaponSelect`, `OnBackClicked`, `OnWeaponSelected_Mahjong`, `OnWeaponSelected_Holdem`, `OnWeaponSelected_Dice` |
| `GameExploreSceneBuilder` | `GameExploreController.OnFightClicked`, `OnFleeClicked`, `OnReturnToMainMenu` |
| `SceneBuilderUtility.BuildBattleRootBase()` | `BattleControllerBase.OnEnemyPanel0Clicked`, `OnEnemyPanel1Clicked`, `OnEnemyPanel2Clicked`, `OnEnemyPanel3Clicked` |
| `DiceBattleSceneBuilder` | `DiceRollDirector.UnholdSlot0` through `UnholdSlot4`; `BattleSceneController.RollDice`, `ConfirmScore`, `CancelBattle`, `NextRound` |
| `MahjongBattleSceneBuilder` | `MahjongBattleController.OnDeclareKan`, `OnDeclareRiichi`, `OnPartialAttack`, `CancelBattle` |
| `HoldemBattleSceneBuilder` | `HoldemBattleController.ConfirmAttack`, `RedrawHoleCard0`, `RedrawHoleCard1`, `RedrawCommunity`, `CancelBattle`, `DefensePick0` through `DefensePick4` |

## Hard-Coded Asset Paths

Important hard-coded paths include:

| Area | Paths |
|---|---|
| Fonts/UI | `Assets/TextMesh Pro/Fonts/Mona12.asset`, `Assets/UI/UI_Heart.png`, `Assets/UI/MainScreen_Logo.png`, `Assets/UI/UI_Background.png` |
| Player sprites | `Assets/Player/Sprites/Idle/0.png`, `LowHp`, `Jump`, `JumpBelow`, `Defense`, `SmallHit`, `StrongHit`, `Debuff`, `Die/Player_Die_1000x1000`, `DiceRoll`, `Attack`, `Weapon/Player_Weapon.png` |
| Story slides | `Assets/Story/Story_CutScene_0.png`, `_1.png`, `_2.png` |
| Battle backgrounds | `Assets/Backgrounds/Fight_Background_0_Forest.png`, `Assets/Backgrounds/Fight_Background_1_Cave.png` |
| Enemy projectile | `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png` |
| Dice prefabs | `Assets/Dices/Prefabs/Dice_d6_mine.prefab`, fallback `Assets/Dices/Prefabs/Dice_d6.prefab` |
| Dice generated assets | `Assets/Textures/DiceRenderTexture.renderTexture`, `Assets/Textures/VaultRenderTexture.renderTexture`, `Assets/Physics/DiceBouncy.asset`, `Assets/Physics/WallBouncy.asset`, `Assets/Materials/DiceOutline.mat` |
| Mahjong | `Assets/Mahjong/MahjongTileSprites.asset`, `Assets/Mahjong/*.png` through the tile sprite database |
| Hold'em optional card art | `Assets/Holdem/Sprites/holdem_card_back.png`, `holdem_blank_card_face.png`, `holdem_community_mat.png`, `holdem_defense_card_back.png`; builder uses colored fallbacks when these PNGs are absent |
| Audio | `Assets/Se/True 8-bit Sound Effect Collection - Lite/...`, `Assets/Se/DiceRoll_WakuWaku.wav` |
| Fallback generated sprites | `Assets/Generated/Fallback_{sanitizedKey}.png` |

Stage data also stores sprite/background paths in `Stage1Forest`, `Stage2Cave`, `MobDef`, and `BossDef`.

## Prefab Dependencies

Tracked/local prefab evidence:

- `Assets/Dices/Prefabs/Dice_d4.prefab`
- `Assets/Dices/Prefabs/Dice_d6.prefab`
- `Assets/Dices/Prefabs/Dice_d6_mine.prefab`
- `Assets/Dices/Prefabs/Dice_d8.prefab`
- `Assets/Dices/Prefabs/Dice_d10.prefab`
- `Assets/Dices/Prefabs/Dice_d12.prefab`
- `Assets/Dices/Prefabs/Dice_d20.prefab`

`DiceBattleSceneBuilder` loads `Dice_d6_mine.prefab` first, then falls back to `Dice_d6.prefab`. The audit did not find player, mob, or mahjong tile prefab assets; the mahjong tile prefab is built inside the generated scene.

## Why Names And Paths Are Fragile

- `SetField()` resolves field names by string. A private `[SerializeField]` rename does not update builder strings automatically.
- `UnityEventTools.AddPersistentListener()` stores method references in generated scene data. Public method renames break button callbacks unless builders are updated and scenes are regenerated.
- Builders load assets by exact path using `AssetDatabase.LoadAssetAtPath()`. Moving or renaming assets can produce null sprites/audio/prefabs or fallback visuals.
- Runtime scene transitions use string scene names. Build settings and generated scene names must stay aligned.
- `Assets/Scenes/` is ignored, so local scene files can drift from builder output unless regenerated in validation.

## Recommended Validation Steps After Changing Builders

Follow `AGENTS.md` Unity validation isolation rules:

1. Create or use a separate Git validation worktree, not the foreground checkout.
2. Do not use the same branch in the foreground checkout and validation worktree; prefer detached HEAD or a dedicated validation branch.
3. Run the relevant `Tools/Build ... Scene` menu action in Unity from the validation worktree, or use approved Unity editor validation that invokes the builder.
4. Run EditMode tests from the validation worktree if the change affects runtime wiring, battle behavior, or builder tests.
5. For build validation, pass an explicit `-buildTarget`, always pass `-logFile`, and write logs/results/build outputs outside the Unity project root.
6. Never use `-ignorecompilererrors`.
7. Never use `-rebuildLibrary` on the foreground checkout.
8. Never write build output under `Assets/`.
9. After validation, run `git status --porcelain` in the validation worktree.
10. Treat tracked-file changes from validation as a failure unless the task explicitly allowed generated outputs.

For builder edits specifically, verify:

- All five generated scene builders still compile.
- Generated scenes contain expected controller components.
- `SetField()` warnings/errors do not appear for renamed or missing fields.
- Buttons invoke the expected persistent callback methods.
- Build settings contain only expected scene paths for the current policy.
- Asset load warnings are understood and either expected fallbacks or defects.
