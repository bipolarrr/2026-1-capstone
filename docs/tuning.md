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
| Battle | Defense rolls (no combo) | `1` |
| Battle | Defense rolls (combo) | `3` |
| Battle | Splash ratio | combo `50%` / non-combo `0%` |
| Battle | Boss HP | `120` |
| Battle | Boss rank | `5` |
| Game | Player max hearts | `5` (10 half-hearts) |
| Explore | Walk duration | `2.5s` |
| Explore | Scroll speed | `120 px/s` |
| Menu | Intro fade | `0.6s` |
| Popup | Animation duration | `0.2s` |

## Enemy Damage Multipliers

| Combo | Multiplier | Rank 3 | Rank 5 |
|---|---:|---:|---:|
| None | ×1 | 3 half | 5 half |
| Small Straight | ×1.5 | - | 8 half |
| Full House | ×1.5 | - | 8 half |
| Large Straight | ×2 | - | 10 half |
| Four of a Kind | ×2.5 | - | 13 half |
| YACHT | ×3 | - | 15 half |

Combos require rank ≥ 4 (4+ dice).

## Battle UI Layout

```text
panelMargin=0.05, panelGap=0.01, panelMid=0.50
left:  0.05 ~ 0.49  (dice viewport + vault preview)
right: 0.51 ~ 0.95  (battle log + buttons + info text)
```

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
