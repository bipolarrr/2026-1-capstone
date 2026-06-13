# Grok Imagine Generation Queue

Audit date: 2026-05-28

This is the manual Grok Imagine queue for v0.1 character sprites and combat/explore VFX. Humans run generation manually and keep generated outputs outside `Assets/` until a separate import/postprocess task is approved.

## Quick Copy-Paste Prompts — Character

### `SLIME_ATTACK`

Reference:
- `Assets/Mobs/Sprites/Slime/Idle/0.png`

Prompt:

```text
Use the uploaded slime Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same eyes, mouth, colors, outline, and proportions.
Motion only: compress, small hop up, drop straight down, heavy squash-crush impact, return to Idle/0. Keep the slime fully inside the frame, centered side-view, solid background.
```

### `SLIME_HIT`

Reference:
- `Assets/Mobs/Sprites/Slime/Idle/0.png`

Prompt:

```text
Use the uploaded slime Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same colors, outline, mouth, and proportions.
Motion only: quick hit jolt, tiny sideways squash, eyes briefly close in pain, return to Idle/0. No dancing or rhythmic wobble. Keep the slime fully inside the frame, centered side-view, solid background.
```

### `SKELETON_ATTACK`

Reference:
- `Assets/Mobs/Sprites/Skeleton/Idle/0.png`

Prompt:

```text
Use the uploaded skeleton Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same skull, bones, bow, colors, outline, and proportions.
Motion only: raise bow, pull string, empty string snaps back, return to Idle/0. Empty bow only. No projectile, VFX, sparks, or stars. Keep the full skeleton inside the frame, centered side-view, solid background.
```

### `SKELETON_HIT`

Reference:
- `Assets/Mobs/Sprites/Skeleton/Idle/0.png`

Prompt:

```text
Use the uploaded skeleton archer Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same skull, bones, bow, quiver, colors, outline, and proportions.
Motion only: small hit jolt, brief bone shake, bow arm dips, return to Idle/0. Keep the full skeleton inside the frame, centered side-view, solid background.
```

### `SKELETON_DEATH`

Reference:
- `Assets/Mobs/Sprites/Skeleton/Idle/0.png`

Prompt:

```text
Use the uploaded skeleton archer Idle/0 as the exact character reference.
Frame 1 matches Idle/0. Keep the same skull, bones, bow, quiver, colors, outline, and proportions.
Motion only: small stagger, collapse downward in place, end as a still bone pile with the bow visible. Keep all parts inside the frame, centered side-view, solid background.
```

## Quick Copy-Paste Prompts — VFX

### `PLAYER_DEFENSE_SHIELD_VFX`

Reference:
- `none`

Prompt:

```text
Create a 2D pixel-art defense VFX for a Unity side-view battle game. A small shield appears faintly in front of the player, becomes brighter and clearer, holds for a beat, then fades away. Clean background, readable at small in-game size.
```

### `PLAYER_HIT_IMPACT_VFX`

Reference:
- `none`

Prompt:

```text
Create a 2D pixel-art player hit impact VFX for a side-view battle game. A compact red-orange flash bursts from the center, splits into sharp pixel sparks, then disappears quickly. Strong readable "hit" feeling, clean background.
```

### `PLAYER_STRONG_HIT_VFX`

Reference:
- `none`

Prompt:

```text
Create a 2D pixel-art heavy hit VFX for a player taking a strong attack. A bright white core snaps open with red-orange cracks and chunky sparks, then fades fast. High contrast, side-view game readable, clean background.
```

### `ENEMY_HIT_IMPACT_VFX`

Reference:
- `none`

Prompt:

```text
Create a 2D pixel-art enemy hit impact VFX for a Unity battle game. A sharp white-yellow starburst pops on impact with a few orange pixel sparks and a short diagonal slash accent, then vanishes. Clean background, readable over enemy sprites.
```

### `DICE_GOOD_COMBO_GLOW_VFX`

Reference:
- `none`

Prompt:

```text
Create a 2D pixel-art UI VFX for a good dice combo in a Unity battle game. A gold outline glow and small sparkle ring pulse over the dice rolling area, then fade cleanly. It should feel rewarding without covering the dice.
```

## Scope

v0.1 scope for this queue:

- Character sprite generation for the current Dice/Mahjong/Explore loop.
- Combat VFX that improves attack, defense, hit, combo, and Mahjong feedback.
- Explore/reward/boss emphasis VFX that supports the current linear stage-round flow.
- Prompt text and generation priority only.

Out of scope for this queue:

- Running Grok Imagine, Unity, extraction, upscaling, background removal, rembg, ffmpeg, or image processing.
- Importing or modifying generated assets.
- Adding runtime hooks, code, builders, scenes, prefabs, `.meta`, `.asset`, ProjectSettings, or Packages changes.
- Hold'em, node map, shop, relic, joker, deck, and post-v0.1 macro systems.

## Rules

- Save Grok outputs outside `Assets/` until a separate import/postprocess task is approved.
- Do not create, move, delete, rename, reimport, upscale, rembg, crop, or convert assets as part of this queue task.
- If a prompt references an existing character, upload the listed idle/source image.
- If a VFX has no fixed visual direction or no clear use decision, mark it `BLOCKED_NO_REFERENCE`.
- Runtime fallback behavior prevents some missing sprites from breaking the screen; those are still tracked when v0.1 visuals are weak.

## Priority Legend

| Priority | Meaning |
|---|---|
| `P0_NOW` | Frequently visible in v0.1, directly improves readability/tactility, has a clear or probable hook, and can be attempted immediately in Grok Imagine. |
| `P1_SOON` | Large quality gain, but less frequent, slightly ambiguous to wire, or not blocking the current play loop. |
| `P2_OPTIONAL` | Nice polish, decorative, or already partially covered by existing UI/sprite behavior. |
| `BLOCKED_NO_REFERENCE` | Missing reference image, canonical identity, color language, or use decision. |
| `DEFER_OUT_OF_SCOPE` | Outside v0.1 stabilization scope. |

## Current VFX Hook Investigation

| area | file/class | hook or evidence | likely effect need | confidence | notes |
|---|---|---|---|---|---|
| Shared battle root | `Assets/Editor/SceneBuilderUtility.cs` / `BuildBattleRootBase()` | Adds `BattleDamageVFX`, `BattleAnimations`, and wires `vfx` / `battleAnims` into `BattleControllerBase` subclasses. | Shared impact, hit flash, damage text, shake, future sprite VFX host. | High | Existing hook is code-driven VFX, not imported sprite VFX yet. |
| Damage spawn area | `SceneBuilderUtility.BuildBattleDamageSpawnArea()` plus `DiceBattleSceneBuilder` / `MahjongBattleSceneBuilder` | Both battle builders create `DamageSpawnArea`; `BattleDamageVFX.SpawnDamageText()` places floating damage text over enemy slots. | Enemy hit impact, AOE splash hit, Mahjong AOE damage numbers. | High | Direct damage-number hook exists; separate burst sprites would need runtime addition later. |
| Dice player attack impact | `BattleSceneController.ConfirmScoreRoutine()` / `ApplyPlayerAttackImpact()` | `PlayerAttackAnimator.Play(..., onImpact)` gates damage application; impact spawns damage text, enemy hit animation, flash, and camera shake. | `ENEMY_HIT_IMPACT_VFX`, `AOE_SPLASH_HIT_VFX`, `PLAYER_ATTACK_SWING_VFX`. | High | Strongest timing hook for enemy-side impact effects. |
| Dice combo reward | `DamageCalculator`, `PlayerAttackPipeline`, `BattleSceneController.UpdateDamagePreview()` | Dice combos produce `comboName`, `shakeIntensity`, splash ratio, HUD combo label, and different attack SFX. | `DICE_GOOD_COMBO_GLOW_VFX`, `DICE_JACKPOT_BURST_VFX`. | High | Combo thresholds are known; visual hook around dice area is probable, not currently a sprite field. |
| Dice rolling area | `DiceBattleSceneBuilder` | Builds `DicePanel`, `DiceViewportContainer`, `DiceViewport`, held slots, `ComboLabel`, and `BattleHudPresenter`. | Dice area glow, jackpot burst, confirm flash. | Medium | UI hierarchy exists; no dedicated VFX overlay field yet. |
| Player defense phase | `EnemyCounterAttackDirector.RunCounterAttack()` / `ConfirmDefense()` | Starts `PlayerBodyAnimator.BeginDefenseSession()`, evaluates defense, logs perfect defense, and calls `PlayPlayerDefenseFeedback()`. | `PLAYER_DEFENSE_SHIELD_VFX`. | High | Timing is clear; imported shield VFX would need a visual target near `playerBody`. |
| Player defense feedback | `EnemyCounterAttackDirector.PlayPlayerDefenseFeedback()` / `BattleAnimations.EnemyProjectileAttack()` | Existing feedback is player defense sprite and cyan `FlashHit`; projectile-block path also flashes the player. | Shield fade-in/fade-out, block sparkle. | High | This directly matches the requested shield that appears faintly, brightens, and fades. |
| Player hit feedback | `EnemyCounterAttackDirector.PlayPlayerHitFeedback()` / `BattleAnimations.EnemyMeleeAttack()` / `EnemyProjectileAttack()` | Plays `PlayerBodyAnimator.PlayHitByEnemyRank()` and `BattleAnimations.FlashDamage()` on player body at impact. | `PLAYER_HIT_IMPACT_VFX`, `PLAYER_STRONG_HIT_VFX`. | High | Rank/damage split already exists for small vs strong body hit. |
| Player jump/below effect | `DiceBattleSceneBuilder`, `PlayerJumpAnimator` | Builder wires `belowEffect` and `belowSprites`; `PlayerJumpAnimator` toggles below effect during perfect-defense jump. | Optional dodge/ground pop effect; not the shield request. | High | Existing `JumpBelow` folder is empty and still blocked on direction. |
| Enemy projectile timing | `SceneBuilderUtility.CreateEnemyProjectileImage()`, `EnemyCounterAttackDirector`, `MahjongBattleController` | Shared `enemyProjectile` image is wired into dice and mahjong enemy attack paths. | Projectile impact burst on player. | Medium | Arrow projectile exists; impact sprite is not separated. |
| Enemy damaged feedback | `BattleControllerBase.PlayEnemyDamagedFeedback()` | Calls enemy hit animation and `battleAnims.FlashDamage(enemyBodies[index])`. | `ENEMY_HIT_IMPACT_VFX`. | High | Frequent player feedback point in both dice and mahjong. |
| Target selection | `BattleControllerBase`, `DiceBattleSceneBuilder` | Target markers are wired; `OnEnemyPanelClicked()` updates target markers. | `TARGET_SELECT_EMPHASIS_VFX`. | Medium | Existing marker likely covers readability; VFX is optional polish. |
| Explore boss encounter | `GameExploreController.SetupCombatEncounter(true)`, `Stage1Forest`, `Stage2Cave` | Boss round sets `"보스 등장!"`, prepares boss battle, and enlarges boss slot. | `BOSS_ENCOUNTER_EMPHASIS_VFX`. | Medium | Use scene emphasis independent of boss identity; Stage 2 boss sprite itself remains blocked. |
| Explore item reward | `GameExploreSceneBuilder`, `GameExploreController.SetupItemEncounter()`, `OnItemSelected()` | Item cards, hover outline, reward audio, and power-up selection are already present. | `ITEM_BOX_REWARD_GLOW_VFX`, `REVIVE_TRIGGER_GLOW_VFX`. | Medium | No VFX asset hook yet, but card locations and selection timing are clear. |
| Mahjong enemy attack setup | `MahjongBattleController.PlayEnemyAttackSequence()` | Reveals wait tiles, shows `RonSpeechBubble`, then uses shared enemy attack animation. | Mahjong enemy attack emphasis and player hit impact. | High | Existing wait/ron feedback is good, but final impact still relies on flash/body hit only. |
| Mahjong player win / partial attack | `MahjongBattleController.CheckImmediateWinRoutine()`, `PartialAttackRoutine()`, `ApplyAoeDamage()` | Full win and partial attack both call `PlayPlayerAttackAnimation(() => ApplyAoeDamage(dmg))`. | `MAHJONG_WIN_IMPACT_VFX`, `MAHJONG_PARTIAL_ATTACK_VFX`, AOE splash. | High | Timing is clear; visual distinction between full win and partial attack is not yet present. |
| Mahjong wait info | `MahjongBattleSceneBuilder.BuildWaitInfoPanel()`, `MahjongWaitInfoPanel`, `MahjongTileVisual.MarkAsShot()` | Shot discard gets skull marker and tooltip; wait info panel is built and injected. | Optional tile danger/shot emphasis. | Medium | Current UI is functional; extra VFX is lower priority than attack impacts. |
| Audio timing context | `AudioManager`, builders | Battle/explore builders register attack, defense, reward, transition, and dice SFX names. | Coordinate VFX with existing attack/reward SFX later. | Medium | Useful for timing, but this task does not edit audio or code. |

## Existing Runtime Sprite References

| referenced by | path | purpose | required for v0.1? | notes |
|---|---|---|---|---|
| `SceneBuilderUtility.PlayerIdleSpriteFolder`, `GameExploreSceneBuilder` | `Assets/Player/Sprites/Idle`, `Assets/Player/Sprites/Idle/0.png` | Player idle/body reference in explore and battle | Yes | Runtime-ready and primary player reference. |
| `SceneBuilderUtility.PlayerLowHpSpriteFolder` | `Assets/Player/Sprites/LowHp` | Player low HP state | Yes | Folder has runtime frames; code loads 95. |
| `SceneBuilderUtility.PlayerJumpSpriteFolder`, `PlayerBodyAnimator.PlayJump()` | `Assets/Player/Sprites/Jump` | Player jump body pose during dice defense/perfect defense jump | Yes, but fallback movement exists | Folder exists with 0 PNGs; lower priority than VFX requested here. |
| `SceneBuilderUtility.PlayerJumpBelowSpriteFolder`, `DiceBattleSceneBuilder`, `PlayerJumpAnimator` | `Assets/Player/Sprites/JumpBelow` | Below-body jump effect in DiceBattle | Yes for wired dice scene effect | Folder exists with 0 PNGs; effect direction is still missing. |
| `SceneBuilderUtility.PlayerDefenseSpriteFolder` | `Assets/Player/Sprites/Defense` | Player defense body/session | Yes | Runtime frames exist, but the requested shield VFX is separate from this body animation. |
| `SceneBuilderUtility.PlayerSmallHitSpriteFolder` | `Assets/Player/Sprites/SmallHit` | Player small damage reaction | Yes | Runtime frames exist. |
| `SceneBuilderUtility.PlayerStrongHitSpriteFolder` | `Assets/Player/Sprites/StrongHit` | Player strong damage reaction | Yes | Code-expected 47 frames exist. |
| `SceneBuilderUtility.PlayerDieSpriteFolder`, `DiceBattleSceneBuilder` | `Assets/Player/Sprites/Die/Player_Die_1000x1000` | Player death animation | Yes | Runtime-ready 145-frame sequence exists. |
| `SceneBuilderUtility.PlayerAttack01SpriteFolder`, `PlayerAttackAnimator` | `Assets/Player/Sprites/Attack` | Player attack body animation | Yes | Runtime-ready body throw animation. |
| `SceneBuilderUtility.PlayerWeaponSpritePath`, `PlayerAttackAnimator` | `Assets/Player/Sprites/Weapon/Player_Weapon.png` | Player projectile/weapon image | Yes | Runtime-ready; source provenance unclear. |
| `CharacterSelectSceneBuilder.ApplyDiceButtonIcon()` | `Assets/Player/Sprites/DiceRoll/0.png` | Dice weapon button icon | Yes | Missing canonical source/reference. |
| `Stage1Forest` | `Assets/Mobs/Sprites/Slime/Attack`, `Hit` | Slime action animations | Yes | Referenced folders are empty. |
| `Stage1Forest` | `Assets/Mobs/Sprites/Skeleton/Attack`, `Hit`, `Dead` | Skeleton action/death animations | Yes | Referenced folders are empty. |
| `Stage1Forest`, builders, battle/explore controllers | `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png` | Skeleton projectile | Yes | Runtime-ready. |
| `Stage1Forest` | `Assets/Mobs/Boss_Dracula_example.png` | Stage 1 boss sprite | Yes | Runtime-ready static boss sprite. |
| `Stage2Cave` | `spritePath = null` | Stage 2 boss fallback | Yes, via color fallback | Boss identity/reference remains missing. |
| `MahjongBattleSceneBuilder` | `Assets/Mahjong/Table.png`, `Assets/Mahjong/MahjongTileSprites.asset` | Mahjong table/tile UI | Yes | Runtime-ready for current implementation. |

## Character Sprites

### Character Sprite Queue

| assetId | category | priority | why needed in current project | current hook status | reference strategy | notes |
|---|---|---|---|---|---|---|
| `SLIME_ATTACK` | Character Sprite | `P0_NOW` | Stage 1 directly references Slime `Attack`; current folder is empty and falls back to idle. | `EXISTING_HOOK_CLEAR` | existing asset reference | Upload `Assets/Mobs/Sprites/Slime/Idle/0.png`. |
| `SLIME_HIT` | Character Sprite | `P0_NOW` | Stage 1 directly references Slime `Hit`; damage feedback currently lacks a proper reaction. | `EXISTING_HOOK_CLEAR` | existing asset reference | Upload `Assets/Mobs/Sprites/Slime/Idle/0.png`. |
| `SKELETON_ATTACK` | Character Sprite | `P0_NOW` | Skeleton is a ranged v0.1 enemy and its referenced attack folder is empty. | `EXISTING_HOOK_CLEAR` | existing asset reference | Upload `Assets/Mobs/Sprites/Skeleton/Idle/0.png`; character motion only, with runtime projectile handled separately. |
| `SKELETON_HIT` | Character Sprite | `P0_NOW` | Skeleton `Hit` is directly referenced and currently empty. | `EXISTING_HOOK_CLEAR` | existing asset reference | Upload `Assets/Mobs/Sprites/Skeleton/Idle/0.png`. |
| `SKELETON_DEATH` | Character Sprite | `P0_NOW` | Skeleton death folder is directly referenced and empty; kill feedback falls back to overlay behavior. | `EXISTING_HOOK_CLEAR` | existing asset reference | Upload `Assets/Mobs/Sprites/Skeleton/Idle/0.png`. |
| `PLAYER_JUMP_BODY` | Character Sprite | `P1_SOON` | Dice battle wires jump sprites, but current vertical tween still works without body frames. | `EXISTING_HOOK_CLEAR` | existing asset reference | Generate only if v0.1 wants body pose support beyond current tween. |
| `PLAYER_DEBUFF_STATUS` | Character Sprite | `P2_OPTIONAL` | Builder loads `Debuff`, but no current v0.1 trigger was found. | `NEEDS_RUNTIME_DECISION` | existing asset reference | Decide gameplay trigger before generation. |
| `GOBLIN_HIT` | Character Sprite | `P2_OPTIONAL` | Direct frames are incomplete against code expectation, but an MP4 source exists. | `EXISTING_HOOK_CLEAR` | existing source/postprocess first | Prefer postprocess/extraction before Grok regeneration. |
| `GOBLIN_DEATH` | Character Sprite | `P2_OPTIONAL` | Runtime frames exist; quality/provenance review only. | `EXISTING_HOOK_CLEAR` | existing source review | Not a Grok-first task. |
| `BAT_DEATH` | Character Sprite | `P2_OPTIONAL` | Runtime frames exist, but source/direct length differs. | `EXISTING_HOOK_CLEAR` | existing source review | Not a Grok-first task. |

### `SLIME_ATTACK`

Type:
- Character Sprite

Reference image to upload:
- `Assets/Mobs/Sprites/Slime/Idle/0.png`

Reference strategy:
- existing asset reference

Short prompt:

```text
Use the uploaded slime Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same eyes, mouth, colors, outline, and proportions.
Motion only: compress, small hop up, drop straight down, heavy squash-crush impact, return to Idle/0. Keep the slime fully inside the frame, centered side-view, solid background.
```

Expanded prompt:

```text
Use the uploaded slime Idle/0 as the exact character reference. Frame 1 and the final frame match Idle/0. Do not change the face: same eyes, mouth, colors, outline, and proportions. Motion only: compress, small hop up, drop straight down, heavy squash-crush impact, return to Idle/0. Keep the slime fully inside the frame. Only the slime visible, centered side-view, solid background.
```

Expected motion/result:
- Exact Idle/0 pose -> compress -> small hop upward -> drop straight down -> heavy squash-crush impact -> exact Idle/0 pose.
- It should read as a vertical crushing special move, not a forward tackle or bite.

Queue status:
- `P0_NOW`

### `SLIME_HIT`

Type:
- Character Sprite

Reference image to upload:
- `Assets/Mobs/Sprites/Slime/Idle/0.png`

Reference strategy:
- existing asset reference

Short prompt:

```text
Use the uploaded slime Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same colors, outline, mouth, and proportions.
Motion only: quick hit jolt, tiny sideways squash, eyes briefly close in pain, return to Idle/0. No dancing or rhythmic wobble. Keep the slime fully inside the frame, centered side-view, solid background.
```

Expanded prompt:

```text
Use the uploaded slime Idle/0 as the exact character reference. Frame 1 and the final frame match Idle/0. Keep the same colors, outline, mouth, and proportions. Motion only: quick hit jolt, tiny sideways squash, eyes briefly close in pain, return to Idle/0. Keep the slime fully inside the frame. Only the slime visible, centered side-view, solid background.
```

Expected motion/result:
- Exact Idle/0 pose -> quick hit jolt -> tiny sideways squash -> eyes briefly close in pain -> exact Idle/0 pose.
- It should read as a hit reaction, not dancing, attack, or death.

Queue status:
- `P0_NOW`

### `SKELETON_ATTACK`

Type:
- Character Sprite

Reference image to upload:
- `Assets/Mobs/Sprites/Skeleton/Idle/0.png`

Reference strategy:
- existing asset reference

Short prompt:

```text
Use the uploaded skeleton Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same skull, bones, bow, colors, outline, and proportions.
Motion only: raise bow, pull string, empty string snaps back, return to Idle/0. Empty bow only. No projectile, VFX, sparks, or stars. Keep the full skeleton inside the frame, centered side-view, solid background.
```

Expanded prompt:

```text
Use the uploaded skeleton Idle/0 as the exact character reference. Frame 1 and the final frame match Idle/0. Keep the same skull, bones, bow, colors, outline, and proportions. Motion only: raise bow, pull string, empty string snaps back, return to Idle/0. Empty bow only. No projectile, VFX, sparks, or stars. Keep the full skeleton inside the frame, centered side-view, solid background.
```

Expected motion/result:
- Exact Idle/0 pose -> raise bow -> pull string -> empty string snaps back -> exact Idle/0 pose.
- Preserve the skull, bones, and bow.
- Character motion only; no arrow, projectile, flash, trail, spark, star, or magic effect.

Queue status:
- `P0_NOW`

### `SKELETON_HIT`

Type:
- Character Sprite

Reference image to upload:
- `Assets/Mobs/Sprites/Skeleton/Idle/0.png`

Reference strategy:
- existing asset reference

Short prompt:

```text
Use the uploaded skeleton archer Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same skull, bones, bow, quiver, colors, outline, and proportions.
Motion only: small hit jolt, brief bone shake, bow arm dips, return to Idle/0. Keep the full skeleton inside the frame, centered side-view, solid background.
```

Expanded prompt:

```text
Use the uploaded skeleton archer Idle/0 as the exact character reference. Frame 1 and the final frame match Idle/0. Keep the same skull, eye sockets, bones, bow, quiver, colors, outline, and proportions. Motion only: small hit jolt, brief bone shake, bow arm dips, return to Idle/0. Keep the full skeleton inside the frame, centered side-view, solid background.
```

Expected motion/result:
- Exact Idle/0 pose -> small hit jolt -> brief bone shake -> bow arm dips -> exact Idle/0 pose.
- It should read as damage feedback, not a death animation.

Queue status:
- `P0_NOW`

### `SKELETON_DEATH`

Type:
- Character Sprite

Reference image to upload:
- `Assets/Mobs/Sprites/Skeleton/Idle/0.png`

Reference strategy:
- existing asset reference

Short prompt:

```text
Use the uploaded skeleton archer Idle/0 as the exact character reference.
Frame 1 matches Idle/0. Keep the same skull, bones, bow, quiver, colors, outline, and proportions.
Motion only: small stagger, collapse downward in place, end as a still bone pile with the bow visible. Keep all parts inside the frame, centered side-view, solid background.
```

Expanded prompt:

```text
Use the uploaded skeleton archer Idle/0 as the exact character reference. Frame 1 matches Idle/0. Keep the same skull, eye sockets, bones, bow, quiver, colors, outline, and proportions. Motion only: small stagger, collapse downward in place, end as a still bone pile with the bow visible. Keep all parts inside the frame, centered side-view, solid background.
```

Expected motion/result:
- Exact Idle/0 pose -> small stagger -> collapse downward in place -> final still bone pile.
- Last frame should be stable and readable as defeated.

Queue status:
- `P0_NOW`

### `PLAYER_JUMP_BODY`

Type:
- Character Sprite

Reference image to upload:
- `Assets/Player/Sprites/Idle/0.png`

Reference strategy:
- existing asset reference

Short prompt:

```text
Use the uploaded squirrel Idle/0 as the exact character reference.
Frame 1 and the final frame match Idle/0. Keep the same face, body, tail, colors, outline, and proportions.
Motion only: small crouch, small vertical hop, land, return to Idle/0. Keep the full squirrel inside the frame, centered side-view, solid background.
```

Expanded prompt:

```text
Use the uploaded squirrel Idle/0 as the exact character reference. Frame 1 and the final frame match Idle/0. Keep the same face, body, tail, colors, outline, and proportions. Motion only: small crouch, small vertical hop, land, return to Idle/0. Keep the full squirrel inside the frame, centered side-view, solid background.
```

Expected motion/result:
- Exact Idle/0 pose -> small crouch -> small vertical hop -> land -> exact Idle/0 pose.
- Should work as optional body-pose support for the existing Unity vertical tween.

Queue status:
- `P1_SOON`

## Battle VFX

### Battle VFX Candidate Queue

| assetId | category | priority | why needed in current project | current hook status | reference strategy | notes |
|---|---|---|---|---|---|---|
| `PLAYER_DEFENSE_SHIELD_VFX` | Battle VFX | `P0_NOW` | Dice defense success/defense-in-progress needs clearer visual reading than body pose plus cyan flash. | `EXISTING_HOOK_PROBABLE` | no direct reference needed | Requested shield: faint -> clearer/brighter -> fade. |
| `PLAYER_HIT_IMPACT_VFX` | Battle VFX | `P0_NOW` | Enemy attacks currently rely on player hit body sprites and red flash; a compact impact makes damage moment readable. | `EXISTING_HOOK_CLEAR` | no direct reference needed | Frequent in dice and mahjong enemy attacks. |
| `PLAYER_STRONG_HIT_VFX` | Battle VFX | `P0_NOW` | Strong/rank-based hits already choose strong body animation; heavier impact should distinguish dangerous attacks. | `EXISTING_HOOK_CLEAR` | no direct reference needed | Trigger later by enemy rank/damage threshold. |
| `ENEMY_HIT_IMPACT_VFX` | Battle VFX | `P0_NOW` | Player attacks hit enemies every battle; enemy flash plus damage text lacks the requested "파박" hit feel. | `EXISTING_HOOK_CLEAR` | no direct reference needed | Use on direct target. |
| `DICE_GOOD_COMBO_GLOW_VFX` | Battle VFX / UI VFX | `P0_NOW` | Good dice hand already shows `comboLabel`; a dice-area glow gives reward feedback exactly where the player watches results. | `EXISTING_HOOK_PROBABLE` | no direct reference needed | Place over dice rolling area/tray, not full screen. |
| `PLAYER_ATTACK_SWING_VFX` | Battle VFX | `P1_SOON` | Player attack uses body throw plus weapon projectile; a small trail/launch accent can clarify attack start. | `NEEDS_RUNTIME_DECISION` | style reference | Current attack is projectile/throw, not melee; keep generic attack arc. |
| `DICE_JACKPOT_BURST_VFX` | Battle VFX / UI VFX | `P1_SOON` | YACHT/Four of a Kind should feel meaningfully better than normal combo glow. | `EXISTING_HOOK_PROBABLE` | no direct reference needed | Needs threshold policy, likely YACHT only or top combos. |
| `DICE_CONFIRM_FLASH_VFX` | Battle VFX / UI VFX | `P1_SOON` | Confirming the attack could flash the dice/tray just before projectile impact. | `NEEDS_RUNTIME_DECISION` | no direct reference needed | Useful but not required for core readability. |
| `AOE_SPLASH_HIT_VFX` | Battle VFX | `P1_SOON` | Dice combos and Mahjong attacks can damage multiple enemies; current splash/AOE shares single-target feedback. | `EXISTING_HOOK_PROBABLE` | no direct reference needed | Wider but still compact effect. |
| `MAHJONG_WIN_IMPACT_VFX` | Battle VFX | `P1_SOON` | Full Mahjong win applies AOE damage but reuses generic player attack visuals. | `EXISTING_HOOK_PROBABLE` | no direct reference needed | Needs "ron/tsumo/full hand" emphasis without redesigning tile art. |
| `MAHJONG_PARTIAL_ATTACK_VFX` | Battle VFX | `P1_SOON` | Partial attack is a v0.1 action button; it needs a different, smaller hit feel than full win. | `EXISTING_HOOK_PROBABLE` | no direct reference needed | Use as restrained AOE pulse. |
| `TARGET_SELECT_EMPHASIS_VFX` | Battle VFX / UI VFX | `P2_OPTIONAL` | Target marker already exists; extra emphasis may help but is not blocking. | `RECOMMENDED_BUT_NOT_HOOKED` | no direct reference needed | Optional highlight pulse around selected enemy slot. |
| `REVIVE_TRIGGER_GLOW_VFX` | Battle VFX / Reward VFX | `P1_SOON` | `ReviveOnce` already has HUD flash and log; a charm-like glow would make the save moment clearer. | `EXISTING_HOOK_PROBABLE` | no direct reference needed | Tied to `hud.FlashRevive()` timing later. |
| `LOW_HP_WARNING_PULSE_VFX` | Battle VFX / UI VFX | `P2_OPTIONAL` | Low HP body state and audio already exist; pulse is polish. | `RECOMMENDED_BUT_NOT_HOOKED` | no direct reference needed | Keep below P1 unless QA says low HP is missed. |

### `PLAYER_DEFENSE_SHIELD_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art defense VFX for a Unity side-view battle game. A small shield appears faintly in front of the player, becomes brighter and clearer, holds for a beat, then fades away. Clean background, readable at small in-game size.
```

Expanded prompt:

```text
Create a short 2D pixel-art defense shield VFX for a Unity side-view battle game. The shield starts as a faint transparent silhouette, sharpens into a bright blue-white guard shape with a few tiny gold spark pixels, holds briefly, then dissolves cleanly. No character, clean flat background, centered, sprite-sheet friendly, readable at small in-game size.
```

Expected motion/result:
- Faint shield silhouette -> brighter and clearer shield -> short hold -> soft fade out.
- Reads as "defense success / guarding now" during enemy counterattack defense.

Queue status:
- `P0_NOW`

### `PLAYER_HIT_IMPACT_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art player hit impact VFX for a side-view battle game. A compact red-orange flash bursts from the center, splits into sharp pixel sparks, then disappears quickly. Strong readable "hit" feeling, clean background.
```

Expanded prompt:

```text
Create a short 2D pixel-art hit impact VFX for a player taking damage in a Unity side-view battle game. Start with a bright center pop, then red-orange cracks and small square sparks snap outward, then fade fast. No character, no UI text, clean flat background, high contrast, readable over a player sprite.
```

Expected motion/result:
- Center flash -> pixel sparks/cracks -> immediate fade.
- Reads at the exact moment enemy damage is applied to the player.

Queue status:
- `P0_NOW`

### `PLAYER_STRONG_HIT_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art heavy hit VFX for a player taking a strong attack. A bright white core snaps open with red-orange cracks and chunky sparks, then fades fast. High contrast, side-view game readable, clean background.
```

Expanded prompt:

```text
Create a short 2D pixel-art strong damage impact VFX for a Unity side-view battle game. A white-hot center flash pops open, red-orange crack lines split outward, chunky pixel sparks scatter for one beat, then everything fades quickly. No character, clean flat background, high contrast, stronger than a normal hit but still compact.
```

Expected motion/result:
- Bigger, heavier version of normal player hit impact.
- Should be distinguishable when high-rank enemies or high damage hit the player.

Queue status:
- `P0_NOW`

### `ENEMY_HIT_IMPACT_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art enemy hit impact VFX for a Unity battle game. A sharp white-yellow starburst pops on impact with a few orange pixel sparks and a short diagonal slash accent, then vanishes. Clean background, readable over enemy sprites.
```

Expanded prompt:

```text
Create a compact 2D pixel-art enemy hit impact VFX for a side-view Unity battle game. A bright white-yellow starburst snaps open, a short diagonal slash accent crosses the center, small orange pixel sparks pop outward, then everything vanishes fast. No character, no text, clean flat background, sprite-sheet friendly.
```

Expected motion/result:
- Quick "파박" impact over an enemy body.
- Works for direct player attack impact in dice and mahjong.

Queue status:
- `P0_NOW`

### `PLAYER_ATTACK_SWING_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- style reference

Short prompt:

```text
Create a 2D pixel-art attack swing VFX for a Unity side-view battle game. A short golden arc streak appears near the player attack motion, snaps forward, then fades. Clean background, no character, readable at small size.
```

Expanded prompt:

```text
Create a short 2D pixel-art attack emphasis VFX for a Unity side-view battle game. A compact golden arc streak appears near the start of a player throw or melee swing, snaps forward with a few pixel sparks, then fades quickly. No character, no weapon, clean flat background, sprite-sheet friendly.
```

Expected motion/result:
- Short attack trail/arc that can sit near the player attack start.
- Supports the existing player throw/projectile animation without replacing it.

Queue status:
- `P1_SOON`

### `DICE_GOOD_COMBO_GLOW_VFX`

Type:
- Battle VFX / UI VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art UI VFX for a good dice combo in a Unity battle game. A gold outline glow and small sparkle ring pulse over the dice rolling area, then fade cleanly. It should feel rewarding without covering the dice.
```

Expanded prompt:

```text
Create a short 2D pixel-art dice combo reward VFX for a Unity UI panel. A thin gold outline glow pulses around the dice rolling area, small star pixels sparkle near the corners, and a soft circular wave expands once before fading. Clean transparent-friendly background, readable over a dice tray, not full screen.
```

Expected motion/result:
- Gold outline pulse -> small sparkle ring -> soft fade.
- Plays when a good dice hand is detected or locked in.

Queue status:
- `P0_NOW`

### `DICE_JACKPOT_BURST_VFX`

Type:
- Battle VFX / UI VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art jackpot burst VFX for a great dice result. Gold and white pixel rays pop outward with a few star sparkles, then shrink and fade. UI-friendly, clean background, does not cover the whole screen.
```

Expanded prompt:

```text
Create a short 2D pixel-art jackpot burst VFX for a Unity dice battle UI. A bright gold-white center pop sends chunky pixel rays and tiny stars outward, then the burst shrinks and fades cleanly. It should feel like a rare excellent dice result, UI-friendly, centered, no text, clean background.
```

Expected motion/result:
- Rare high-result burst for YACHT or top-tier combos.
- Stronger than `DICE_GOOD_COMBO_GLOW_VFX` but still contained to the dice area.

Queue status:
- `P1_SOON`

### `DICE_CONFIRM_FLASH_VFX`

Type:
- Battle VFX / UI VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art dice confirm flash VFX. A quick white-gold rectangular pulse sweeps across a dice tray area, then fades instantly. Clean UI effect, no text, readable but subtle.
```

Expanded prompt:

```text
Create a short 2D pixel-art UI flash for confirming a dice attack in a Unity battle game. A thin white-gold pulse sweeps across a dice tray rectangle, small pixels sparkle at the edge, then it fades immediately. No character, no dice, no text, clean background, subtle and readable.
```

Expected motion/result:
- Quick confirmation flash over the dice/tray area.
- Useful between pressing confirm and the attack impact.

Queue status:
- `P1_SOON`

### `AOE_SPLASH_HIT_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art AOE splash hit VFX for a side-view battle game. A small circular shockwave expands with white-yellow sparks, then fades fast. Clean background, readable over multiple enemy positions.
```

Expanded prompt:

```text
Create a short 2D pixel-art area splash impact VFX for a Unity side-view battle. A compact circular shockwave expands from the center, white-yellow sparks pop around the ring, and the whole effect fades quickly. No character, no text, clean flat background, readable but not screen-filling.
```

Expected motion/result:
- Circular splash/shockwave for combo splash and Mahjong AOE damage.
- Should feel wider than a single hit impact without hiding enemy sprites.

Queue status:
- `P1_SOON`

### `MAHJONG_WIN_IMPACT_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art Mahjong win impact VFX for a Unity battle game. A bright gold-white tile-shaped burst pops with small square sparks and a soft ring wave, then fades. Clean background, readable as a full winning attack.
```

Expanded prompt:

```text
Create a short 2D pixel-art Mahjong full-win attack VFX for a Unity side-view battle game. A bright gold-white tile-shaped burst pops in the center, small square sparks scatter outward, and a soft ring wave expands once before fading. No text, no real Mahjong tile symbols, clean background, clear reward feeling.
```

Expected motion/result:
- Full win "화료" attack impact; stronger than partial attack.
- Supports `CheckImmediateWinRoutine()` where full Mahjong win applies AOE damage.

Queue status:
- `P1_SOON`

### `MAHJONG_PARTIAL_ATTACK_VFX`

Type:
- Battle VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art Mahjong partial attack VFX. A small green-gold tile pulse opens into a modest shockwave with a few square sparks, then fades. Clean background, lighter than a full win impact.
```

Expanded prompt:

```text
Create a short 2D pixel-art Mahjong partial attack VFX for a Unity side-view battle. A small green-gold tile-like pulse opens into a modest circular shockwave, a few square sparks pop outward, then it fades quickly. No text, clean background, readable but clearly weaker than a full win.
```

Expected motion/result:
- Lighter AOE pulse for the "중간공격" button.
- Should distinguish partial attack from full win without adding new rules visuals.

Queue status:
- `P1_SOON`

### `TARGET_SELECT_EMPHASIS_VFX`

Type:
- Battle VFX / UI VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art target selection emphasis VFX. A thin yellow corner-frame pulse appears around a selected enemy slot, sparkles lightly, then fades. Clean UI effect, no character, no text.
```

Expanded prompt:

```text
Create a short 2D pixel-art UI target selection VFX for a side-view battle game. Four thin yellow corner brackets pulse around a selected enemy slot, tiny pixel sparkles appear at the corners, then it fades. No character, no text, clean background, readable but subtle.
```

Expected motion/result:
- Optional pulse around selected enemy slot.
- Current target marker already handles function, so this is polish.

Queue status:
- `P2_OPTIONAL`

### `REVIVE_TRIGGER_GLOW_VFX`

Type:
- Battle VFX / Reward VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art revive trigger glow VFX. A small green-white charm light blooms, sparkles upward, then fades. Clean background, no text, readable as a one-time survival effect.
```

Expanded prompt:

```text
Create a short 2D pixel-art revive trigger VFX for a Unity battle game. A green-white charm glow blooms from the center, small bright pixels sparkle upward, and a soft ring fades out. No character, no text, clean background, readable as a one-time survival or protection effect.
```

Expected motion/result:
- Visual support for `ReviveOnce` when lethal damage is prevented.
- Pairs with current HUD revive flash and log line.

Queue status:
- `P1_SOON`

## Explore / Reward / Boss VFX

### Explore / Reward / Boss VFX Candidate Queue

| assetId | category | priority | why needed in current project | current hook status | reference strategy | notes |
|---|---|---|---|---|---|---|
| `BOSS_ENCOUNTER_EMPHASIS_VFX` | Explore VFX | `P1_SOON` | Boss rounds are current v0.1 content; "보스 등장!" text alone can feel flat. | `RECOMMENDED_BUT_NOT_HOOKED` | no direct reference needed | Generic boss warning effect can work even when Stage 2 boss sprite is fallback. |
| `ITEM_BOX_REWARD_GLOW_VFX` | Explore VFX / Reward VFX | `P1_SOON` | Item-box power-up selection is the current reward layer; selection should feel rewarding. | `RECOMMENDED_BUT_NOT_HOOKED` | no direct reference needed | Use around item cards or selected reward. |
| `LOW_HP_WARNING_PULSE_VFX` | Explore/Battle UI VFX | `P2_OPTIONAL` | Low HP sprites and audio already exist; extra pulse can help but is not required. | `RECOMMENDED_BUT_NOT_HOOKED` | no direct reference needed | Keep subtle to avoid clutter. |

### `BOSS_ENCOUNTER_EMPHASIS_VFX`

Type:
- Explore VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art boss encounter emphasis VFX for a Unity RPG battle. A red-black warning pulse expands behind the boss area with a few sharp gold sparks, then fades. Clean background, no text, not full screen.
```

Expanded prompt:

```text
Create a short 2D pixel-art boss encounter emphasis VFX for a Unity side-view RPG. A red-black warning pulse expands behind the boss area, thin dark rays and small gold sparks appear for one beat, then fade cleanly. No boss character, no text, clean background, readable but not a cut-in.
```

Expected motion/result:
- Simple warning pulse for boss encounter presentation.
- Makes boss rounds read differently from normal enemy encounters.

Queue status:
- `P1_SOON`

### `ITEM_BOX_REWARD_GLOW_VFX`

Type:
- Explore VFX / Reward VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a 2D pixel-art item reward glow VFX for a Unity game UI. A soft gold glow outlines an item card, tiny star pixels sparkle upward, then fade. Clean background, no text, card-friendly size.
```

Expanded prompt:

```text
Create a short 2D pixel-art reward selection glow VFX for a Unity item card UI. A soft gold outline appears around a card shape, tiny star pixels sparkle upward from the corners, and the glow fades cleanly. No text, no item icon, clean background, readable over a reward card.
```

Expected motion/result:
- Short reward glow after choosing an item box card.
- Supports current `OnItemSelected()` power-up reward moment.

Queue status:
- `P1_SOON`

### `LOW_HP_WARNING_PULSE_VFX`

Type:
- Battle VFX / UI VFX

Reference image to upload:
- `none`

Reference strategy:
- no direct reference needed

Short prompt:

```text
Create a subtle 2D pixel-art low HP warning pulse VFX. A thin red vignette ring pulses once and fades, clean background, no text, readable but not distracting.
```

Expanded prompt:

```text
Create a subtle 2D pixel-art low HP warning VFX for a Unity battle UI. A thin red vignette ring pulses once from the center, a few dim red pixels flicker, then it fades. No character, no text, clean background, readable but restrained.
```

Expected motion/result:
- Optional low HP cue that does not replace existing low HP body animation.
- Use only if QA misses low HP state.

Queue status:
- `P2_OPTIONAL`

## Newly Proposed VFX Candidates

| assetId | why now | likely usage scene | proposed priority |
|---|---|---|---|
| `PLAYER_DEFENSE_SHIELD_VFX` | Defense phase is core Dice battle readability, and current feedback is only body defense + cyan flash. | `DiceBattleScene` enemy counterattack defense phase | `P0_NOW` |
| `PLAYER_HIT_IMPACT_VFX` | Enemy attacks need a compact "got hit" read at the exact damage moment. | `DiceBattleScene`, `MahjongBattleScene` enemy attacks | `P0_NOW` |
| `PLAYER_STRONG_HIT_VFX` | Strong body hit sprites already exist, but strong damage needs a stronger visual accent. | High-rank enemy attacks / high damage impacts | `P0_NOW` |
| `PLAYER_ATTACK_SWING_VFX` | Player attack has body/projectile animation, but launch emphasis could improve feel. | Player attack start in Dice/Mahjong battles | `P1_SOON` |
| `DICE_GOOD_COMBO_GLOW_VFX` | Good combo feedback should appear on the dice area the player is watching. | Dice roll/confirm area when `comboName` exists | `P0_NOW` |
| `DICE_JACKPOT_BURST_VFX` | Rare top dice hands need stronger reward feedback than normal combo glow. | YACHT or top-tier dice combo result | `P1_SOON` |
| `DICE_CONFIRM_FLASH_VFX` | Attack confirmation can be visually bridged before projectile impact. | Dice confirm button / dice tray area | `P1_SOON` |
| `ENEMY_HIT_IMPACT_VFX` | Player attacks happen constantly; enemy flash + number is not enough for impact feel. | Enemy body impact point in Dice/Mahjong battles | `P0_NOW` |
| `AOE_SPLASH_HIT_VFX` | Dice combo splash and Mahjong AOE damage need a wider read than single-target damage. | Multi-enemy damage moments | `P1_SOON` |
| `BOSS_ENCOUNTER_EMPHASIS_VFX` | Boss rounds are in current stage data and need stronger entrance identity. | Explore boss encounter and/or battle start | `P1_SOON` |
| `ITEM_BOX_REWARD_GLOW_VFX` | Item boxes are the current reward layer; selected reward should feel like a payoff. | Explore item card selection | `P1_SOON` |
| `MAHJONG_WIN_IMPACT_VFX` | Full Mahjong win is a major reward moment but reuses generic attack visuals. | Mahjong full win / AOE attack | `P1_SOON` |
| `MAHJONG_PARTIAL_ATTACK_VFX` | Partial attack is a current action button and needs a smaller distinct attack read. | Mahjong partial attack | `P1_SOON` |
| `TARGET_SELECT_EMPHASIS_VFX` | Target markers already exist; extra target pulse may help clarity but is not required. | Enemy target selection in battle | `P2_OPTIONAL` |
| `REVIVE_TRIGGER_GLOW_VFX` | ReviveOnce is a current power-up; survival prevention should stand out. | Battle lethal-damage prevention | `P1_SOON` |
| `LOW_HP_WARNING_PULSE_VFX` | Existing low HP animation/audio cover the state; extra pulse is optional QA polish. | Battle low HP state | `P2_OPTIONAL` |

## Blocked / Missing Reference

### `PLAYER_DICEROLL_ICON`

Status:
- Blocked: reference or visual direction not fixed yet.

What is missing:
- A chosen dice weapon icon reference or approved visual direction for `Assets/Player/Sprites/DiceRoll/0.png`.

Human action needed:
- Choose a reference image, or approve a simple static 2D pixel-art dice icon direction before any Grok prompt is written.

### `PLAYER_JUMP_BELOW_EFFECT`

Status:
- Blocked: reference or visual direction not fixed yet.

What is missing:
- A visual reference or direction for the below-body jump effect wired as `belowEffect`.

Human action needed:
- Decide whether this should be Grok-generated, procedural/static, or omitted for v0.1.

### `CAVE_GUARDIAN_BOSS`

Status:
- Blocked: reference or visual direction not fixed yet.

What is missing:
- A Stage 2 boss identity/reference image and approved art direction.

Human action needed:
- Choose or provide the cave guardian reference before Grok generation; current code uses color fallback and has no sprite path.

## Deferred / Out of Scope

| assetId | reason |
|---|---|
| `HOLDEM_CARD_ASSETS` | Hold'em battle implementation is out of v0.1 scope. |
| `NODE_MAP_SHOP_RELIC_JOKER_ASSETS` | Node map, shop, relic, joker, deck, and card-modifier systems are out of v0.1 scope. |
| `MAHJONG_FUTURE_REDESIGN` | Mahjong tile assets are current QA/runtime assets; redesign should wait for explicit art/rule scope. |
| `BROAD_UI_SKIN_REPLACEMENT` | This queue targets generated sprites/VFX only, not a UI art overhaul. |
| `FULL_SCREEN_CUTIN_VFX` | Boss and jackpot emphasis should stay lightweight for v0.1; full cut-ins are post-v0.1 polish. |

## Quick Prompt Style Notes

- Keep prompts short and action-oriented.
- Let uploaded idle/source images carry character identity.
- For VFX, prefer `no direct reference needed` unless the effect must match a specific character/object.
- Keep VFX centered, clean-background, sprite-sheet friendly, and readable at small in-game size.
- Avoid long negative prompts; use positive shot direction first.

## Files Inspected

- `AGENTS.md`
- `README.md`
- `docs/grok_generation_queue.md`
- `docs/00_actual_project_audit.md`
- `docs/00_project_brief.md`
- `docs/01_architecture_map.md`
- `docs/02_unity_scene_and_object_construction.md`
- `docs/11_project_decisions.md`
- `docs/12_v0_1_scope.md`
- `docs/13_next_backlog.md`
- `docs/grok-imagine-sprite-prompts.md`
- `docs/assets.md`
- `Assets/Editor/SceneBuilderUtility.cs`
- `Assets/Editor/DiceBattleSceneBuilder.cs`
- `Assets/Editor/MahjongBattleSceneBuilder.cs`
- `Assets/Editor/GameExploreSceneBuilder.cs`
- `Assets/Scripts/Battle/BattleControllerBase.cs`
- `Assets/Scripts/Battle/BattleSceneController.cs`
- `Assets/Scripts/Battle/BattleDamageVFX.cs`
- `Assets/Scripts/Battle/BattleHudPresenter.cs`
- `Assets/Scripts/Battle/PlayerAttackAnimator.cs`
- `Assets/Scripts/Battle/PlayerBodyAnimator.cs`
- `Assets/Scripts/Battle/PlayerDeathAnimator.cs`
- `Assets/Scripts/Battle/PlayerJumpAnimator.cs`
- `Assets/Scripts/Battle/EnemyCounterAttackDirector.cs`
- `Assets/Scripts/Battle/BattleAnimations.cs`
- `Assets/Scripts/Battle/PlayerAttackPipeline.cs`
- `Assets/Scripts/Battle/DamageCalculator.cs`
- `Assets/Scripts/Mahjong/MahjongBattleController.cs`
- `Assets/Scripts/Mahjong/EnemyWaitTilesDisplay.cs`
- `Assets/Scripts/Mahjong/RonSpeechBubble.cs`
- `Assets/Scripts/Mahjong/MahjongTileVisual.cs`
- `Assets/Scripts/Explore/GameExploreController.cs`
- `Assets/Scripts/Audio/AudioManager.cs`
- `Assets/Scripts/Game/UIHoverEffect.cs`
- `Assets/Scripts/Stages/Stage1Forest.cs`
- `Assets/Scripts/Stages/Stage2Cave.cs`

## No-Change Confirmation

- No assets were created.
- No assets were moved.
- No assets were deleted.
- No `.meta` files were edited.
- No `.unity`, `.prefab`, `.asset`, ProjectSettings, or Packages files were edited.
- No Unity validation was run.
- No image/video conversion, rembg, upscaling, ffmpeg, or Python image processing was run.
- No code files were edited.
- This task only updated `docs/grok_generation_queue.md`.
