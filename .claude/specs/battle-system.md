# Battle System Spec

Doc owns battle behavior, dice interaction rules, battle log expectations, debug console behavior.

## Core Battle Model

- Five `d6` dice (player), up to five `d6` dice (enemy, count = rank)
- Player attack phase: up to three rolls
- Player defense phase: 1 roll (no enemy combo) or 3 rolls (enemy combo)
- Dice use Rigidbody physics, rendered via render textures
- `DiceViewportInteraction` maps UI viewport to 3D raycasts on `Dice3D` layer
- Enemy dice use separate physics arena at Z=100 with `EnemyDiceRenderTexture`

## Player HP: Heart System (Binding of Isaac Style)

- Player has 5 heart slots (10 half-hearts)
- Heart types: Red (normal), Black (soul/armor), Blue (temporary)
- Damage absorption order: Blue → Black → Red
- Damage measured in half-hearts
- `HeartContainer` manages heart state
- `HeartDisplay` renders heart slots from `Assets/UI/UI_Heart.png` sliced sprites

## Enemy Rank System

- Each enemy has `rank` (1-5) instead of flat `attack`
- Rank shown as stars (★) next to enemy name
- Rank determines: dice count rolled, base damage on defense failure
- Rank scaling: base rank per mob type, boss always rank 5

## Hold And Vault Rules

- Click die toggles hold/unhold
- Held dice excluded from next roll
- Held dice move to Vault, aligned so current top face visually correct
- Hold order matters:
  - `heldOrder` controls left-to-right vault placement
  - unholding triggers vault rearrange for remaining held dice
- Releasing hold restores die to original transform and physics state
- `YachtDie.ReadTopFace()` prefab-orientation-dependent, only run after settle completes

## Player Attack Damage Rules

`DamageCalculator` pure static class, owns combo detection and base damage calc.

Current combo table (player → enemy):

| Combo | Damage | Shake |
|---|---:|---:|
| Yacht | 50 | 25 |
| Four of a Kind | 40 | 18 |
| Large Straight (2-6) | 35 | 14 |
| Large Straight (1-5) | 30 | 14 |
| Full House | 25 | 10 |
| Small Straight | 20 | 7 |
| None | sum of dice | 0 |

Power-up order in `DamageCalculator.ApplyPowerUps`:

1. `AllOrNothing`
2. `OddEvenDouble`

`ReviveOnce` handled by `GameSessionManager.TakePlayerDamage()`.

Splash behavior:

- Combo hit: target 100%, non-target enemies 50%
- Non-combo hit: no splash

## Enemy Attack Damage Rules

Base formula: `damage(half-hearts) = ceil(rank × multiplier)`

| Condition | Multiplier |
|---|---:|
| No combo (defense fail) | ×0.5 |
| Small Straight | ×1.5 |
| Full House | ×1.5 |
| Large Straight | ×2 |
| Four of a Kind | ×2.5 |
| YACHT | ×3 |

- Combos only possible when rank ≥ 4 (4+ dice)
- Successful defense blocks all damage

## Enemy Attack Positioning

- `EnemyAttackPositionResolver` owns enemy stand/impact/projectile points for all battle scenes.
- `MobDef.attackRangeType` selects the positioning mode:
  - `Default`: projectile mobs are ranged, all others are melee.
  - `Ranged`: enemy stays in its slot and fires a projectile.
  - `MidRange`: enemy moves partway between its slot and melee position.
  - `Melee`: enemy moves to the player-front melee position.
  - `Unique`: enemy stays in its slot; use this only for special no-distance attacks such as jumping in place and crushing downward.
- `MobDef.uniqueAttackProfileId` selects a custom attack motion. It is independent from range.
- Bat uses `Melee` with the `bat` profile: move to player-front position, play its attack sprite clip, return to idle, then return home.
- Slime and Water Elemental use `Unique`: stay in place and perform the in-place jump/crush style attack.

## Defense Rules

- **No combo**: Player rolls once, must have enemy dice composition as subset of their 5 dice
- **Combo**: Player gets 3 rolls (with hold/unhold), must produce same combo name (exact values don't matter)
- `DefenseCalculator.Evaluate()` handles both cases

## Round Flow

```text
Start -> InitDice + SetupEnemyDisplay + UpdateHUD + BattleLog intro

Player attack phase:
  click die -> hold/unhold
  roll -> roll only non-held dice
  settle -> refresh preview
  confirm -> calculate damage -> apply damage -> write battle log

Then:
  all enemies dead -> win routine -> ExploreScene (Won)
  else -> enemy counterattack phase

Enemy counterattack phase (per-enemy sequential):
  for each alive enemy:
    jump animation (up → slam down)
    show enemy dice overlay
    EnemyDiceRoller rolls rank dice
    dice settle → hide overlay → display result above enemy panel
    store EnemyDiceResult on enemy

    defense phase for this enemy:
      determine defense rolls: 1 (no combo) or 3 (combo)
      player defense roll phase (reuses existing RollDice)
      ConfirmDefense:
        evaluate defense → block or take damage
        player dead → defeat → MainMenu, stop loop
      clear this enemy's dice result text

  all enemies done → next round button

Next round:
  resets rolls to 3, clears holds, clears defense state
```

## Battle Log Expectations

Every battle event written through `BattleLog.AddEntry()` is retained in history.
`BattleEventPresentation` controls additional feedback:

- `LogOnly`: history only; use for detailed past records such as dice faces, target damage, splash, kills, and separators.
- `LogAndPopup`: history plus bottom message popup; use only for immediate player action prompts.
- `LogAndAnimation`: history plus existing animation/sound feedback; do not enqueue the bottom popup.

Log covers:

- one combined encounter message with enemy list, rank stars, boss wording
- attack confirm with combo, faces, target damage, splash
- per-enemy kill messages
- enemy dice roll results (combo or plain values)
- defense phase entry (1 or 3 rolls)
- per-enemy defense success/failure with damage
- revive activation
- battle win
- player defeat
- next-round separator
- battle cancel

Do not log transient UI-only details already visible on screen, such as standalone defense dice values or current HP echoes.

## Debug Console

`DebugConsoleController` added to Explore and Battle scenes by builders. Creates own runtime Canvas.

Activation rules:

- Konami command input
- 3-second timeout
- `Esc` closes console

Supported commands:

| Command | Effect | Scene |
|---|---|---|
| `/setdice N N N N N` | Forces the dice results | Battle |
| `/kill player` | Defeat player immediately | Battle |
| `/kill mob @a` | Kill all enemies | Battle |
| `/kill mob 0 1 2` | Kill selected enemies by index | Battle |
| `/sprite play player <kind>` | Play/test player sprite animation (`idle`, `lowhp`, `smallhit`, `stronghit`, `jump`, `defense`, `debuff`) | Battle |
| `/sprite play mob <index> <kind>` | Play/test enemy sprite animation (`idle`, `attack`, `hit`, `death`) | Battle |
| `/help` | Print command list | Any |

`/setdice` behavior:

- spends one roll even when forcing results
- moves all dice to Vault
- applies value and matching rotation via `YachtDie.ForceResultWithRotation()`
- rejects use while dice currently rolling
