# Battle System Spec

This document owns battle behavior, dice interaction rules, battle log expectations, and debug console behavior.

## Core Battle Model

- Five `d6` dice (player), up to five `d6` dice (enemy, count = rank)
- Player attack phase: up to three rolls
- Player defense phase: 1 roll (no enemy combo) or 3 rolls (enemy combo)
- Dice use Rigidbody physics and are rendered through render textures
- `DiceViewportInteraction` maps the UI viewport to 3D raycasts on the `Dice3D` layer
- Enemy dice use a separate physics arena at Z=100 with `EnemyDiceRenderTexture`

## Player HP: Heart System (Binding of Isaac Style)

- Player has 5 heart slots (10 half-hearts)
- Heart types: Red (normal), Black (soul/armor), Blue (temporary)
- Damage absorption order: Blue → Black → Red
- Damage is measured in half-hearts
- `HeartContainer` class manages heart state
- `HeartDisplay` renders hearts as TMP colored emoji (♥/♡)

## Enemy Rank System

- Each enemy has a `rank` (1-5) instead of a flat `attack` value
- Rank is displayed as stars (★) next to the enemy name
- Rank determines: number of dice rolled, base damage on defense failure
- Rank scaling: base rank per mob type, boss is always rank 5

## Hold And Vault Rules

- Clicking a die toggles hold / unhold
- Held dice are excluded from the next roll
- Held dice move into the Vault and are aligned so the current top face is visually correct
- Hold order matters:
  - `heldOrder` controls left-to-right vault placement
  - unholding triggers a vault rearrange for the remaining held dice
- Releasing hold restores the die to its original transform and physics state
- `YachtDie.ReadTopFace()` is prefab-orientation-dependent and must only run after settle completes

## Player Attack Damage Rules

`DamageCalculator` is a pure static class and owns combo detection and base damage calculation.

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

Power-up application order in `DamageCalculator.ApplyPowerUps`:

1. `AllOrNothing`
2. `OddEvenDouble`

`ReviveOnce` is handled by `GameSessionManager.TakePlayerDamage()`.

Splash behavior:

- Combo hit: target takes 100%, non-target enemies take 50%
- Non-combo hit: no splash

## Enemy Attack Damage Rules

Base formula: `damage(half-hearts) = ceil(rank × multiplier)`

| Condition | Multiplier |
|---|---:|
| No combo (defense fail) | ×1 |
| Small Straight | ×1.5 |
| Full House | ×1.5 |
| Large Straight | ×2 |
| Four of a Kind | ×2.5 |
| YACHT | ×3 |

- Combos only possible when rank ≥ 4 (4+ dice)
- Successful defense blocks all damage

## Defense Rules

- **No combo**: Player rolls once, must have enemy's dice composition as a subset of their 5 dice
- **Combo**: Player gets 3 rolls (with hold/unhold), must produce the same combo name (exact values don't matter)
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
    show enemy dice popup (center screen)
    EnemyDiceRoller rolls rank dice
    dice settle → hide popup → display result above enemy panel
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

The log should cover:

- battle start with enemy list, rank stars, and boss flag
- attack confirm with combo, faces, target damage, and splash
- per-enemy kill messages
- enemy dice roll results (combo or plain values)
- defense phase entry (1 or 3 rolls)
- per-enemy defense success/failure with damage
- revive activation
- battle win
- player defeat
- next-round separator
- battle cancel

## Debug Console

`DebugConsoleController` is added to Explore and Battle scenes by the builders. It creates its own runtime Canvas.

Activation rules:

- Konami command input
- 3-second timeout
- `Esc` closes the console

Supported commands:

| Command | Effect | Scene |
|---|---|---|
| `/setdice N N N N N` | Forces the dice results | Battle |
| `/kill player` | Defeat player immediately | Battle |
| `/kill mob @a` | Kill all enemies | Battle |
| `/kill mob 0 1 2` | Kill selected enemies by index | Battle |
| `/help` | Print command list | Any |

`/setdice` behavior:

- spends one roll even when forcing results
- moves all dice into the Vault
- applies value and matching rotation through `YachtDie.ForceResultWithRotation()`
- rejects use while dice are currently rolling
