# Tuning

This file owns gameplay constants, layout tuning, and visual size tables. If balance changes, update this file instead of `CLAUDE.md`.

## Font Sizes

| Use | Size |
|---|---|
| Large title | 60-64 |
| Medium title | 44 |
| Name label | 28-30 |
| Body / description | 24-28 |
| HUD values | 22-26 |
| Heart display | 36 |
| Button label | 28 |

## Key Constants

| System | Setting | Value |
|---|---|---|
| Dice | `settleThreshold` | `0.03 m/s` |
| Dice | `settleConfirmTime` | `0.3s` |
| Battle | Enemy damage formula | `ceil(rank × multiplier)` half-hearts |
| Battle | Player attack rolls | `3` per round |
| Battle | Player idle sprite playback | `24 fps` |
| Battle | Enemy sprite playback | `12 fps` |
| Battle | Bat attack sprite playback | `30 fps` |
| Battle | Mid-range enemy approach | `55%` toward melee point |
| Battle | Unique-range enemy approach | stays at home slot |
| Battle | Defense rolls (no combo) | `1` |
| Battle | Defense rolls (combo) | `3` |
| Battle | Splash ratio | combo `50%` / non-combo `0%` |
| Battle | Boss HP | `120` |
| Battle | Boss rank | `5` |
| Mahjong | Enemy tsumo chance | `2%` per living enemy per turn |
| Mahjong | Rank 3 wait reveal chance | `5%` per turn |
| Game | Player max hearts | `5` (10 half-hearts) |
| Explore | Walk duration | `2.5s` |
| Explore | Scroll speed | `120 px/s` |
| Menu | Intro fade | `0.6s` |
| Popup | Animation duration | `0.2s` |

## Enemy Damage Multipliers

| Combo | Multiplier | Rank 3 | Rank 5 |
|---|---:|---:|---:|
| None | ×0.5 | 2 half | 3 half |
| Small Straight | ×1.5 | - | 8 half |
| Full House | ×1.5 | - | 8 half |
| Large Straight | ×2 | - | 10 half |
| Four of a Kind | ×2.5 | - | 13 half |
| YACHT | ×3 | - | 15 half |

Combos require rank ≥ 4 (4+ dice).

## Battle UI Layout

```text
bottomFocusY=0.02~0.315 (screen bottom third with margins)
fieldMaskY=0.333~1.000, battleGroundY=0.44
DiceBattle: left 0.00~0.26 action buttons, right 0.28~1.00 dice viewport + held dice
MahjongBattle: top 0.76~1.00 action buttons, middle 0.43~0.74 dora/discards, bottom 0.00~0.40 hand
Message popup and history log cover the same bottom focus area when active.
```

## Enemy Dice Overlay

| Setting | Value |
|---|---|
| Overlay aspect | `16:9` |
| Overlay size | fixed `298.7 × 168 px` for every enemy |
| Head gap | `8 px` |
| Background color | `RGBA(0.10, 0.18, 0.32, 0.92)` |
| Outline color | `RGBA(0.35, 0.62, 1.00, 0.65)`, distance `(2, -2)` |
| RenderTexture inset | `0 px`; viewport fills overlay so corners match arena bounds |
| Placement anchor | above enemy name/HP panel |
| Camera mode | orthographic |
| Camera offset | `(0, 7, 0)` from enemy dice vault center |
| Camera rotation | `(90, 0, 0)` |
| Camera orthographic size | `1.80` |
| Dice scale | `0.90` |
| Dice spacing | `1.00` |
| Arena size | `(6.4, 8.0, 3.6)` |

## Explore Encounter Presentation

- `MobBodyAnchors` uses different body placements for slime, goblin, bat, and skeleton
- Enemy bob animation uses per-mob speed and phase differences
- Item cards use `UIHoverEffect`

## Enemy Balance Reference

| Enemy | HP Range | Rank | Role |
|---|---|---|---|
| Slime | 30-40 | 1 | Tank |
| Goblin | 18-25 | 2 | Balanced |
| Bat | 10-15 | 3 | Damage dealer |
| Skeleton | 22-30 | 2 | Off-tank |

Design note:

- Killing high-rank enemies first reduces incoming damage and combo threats
- Rank 4-5 enemies can produce combos, making defense harder
- Defense success requires matching dice (no combo) or matching combo name (combo)
