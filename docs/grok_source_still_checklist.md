# Grok Source Still Checklist

Audit date: 2026-05-27

Purpose: list the source stills a human should load into Grok Imagine for batch 1. This document is a checklist only. Do not copy, move, rename, import, reprocess, or modify any asset files as part of this task.

## Batch 1 Source Stills

| Asset ID | Target action | Required source still | Status | Notes |
|---|---|---|---|---|
| `GROK-I2V-001` | `Assets/Mobs/Sprites/Slime/Attack` | `Assets/Mobs/Sprites/Slime/Idle/0.png` | Present in audit | Use as the identity and scale source for Slime. |
| `GROK-I2V-002` | `Assets/Mobs/Sprites/Slime/Hit` | `Assets/Mobs/Sprites/Slime/Idle/0.png` | Present in audit | Use the same still as attack for consistency. |
| `GROK-I2V-003` | `Assets/Mobs/Sprites/Skeleton/Attack` | `Assets/Mobs/Sprites/Skeleton/Idle/0.png` | Present in audit | Use as the identity, bow, and scale source for Skeleton. |
| `GROK-I2V-004` | `Assets/Mobs/Sprites/Skeleton/Hit` | `Assets/Mobs/Sprites/Skeleton/Idle/0.png` | Present in audit | Use the same still as attack for consistency. |
| `GROK-I2V-005` | `Assets/Mobs/Sprites/Skeleton/Dead` | `Assets/Mobs/Sprites/Skeleton/Idle/0.png` | Present in audit | Death needs extra horizontal padding during postprocess/export. |

## Source Still Loading Checklist

- [ ] Open the source still from the exact path listed above.
- [ ] Confirm the image visually matches the target enemy.
- [ ] Confirm the pose is full-body and readable.
- [ ] Confirm the source still is not accidentally a raw opaque frame when a cleaner reference exists.
- [ ] Keep a note of the Grok output name, draft/final setting, prompt, and source still path.
- [ ] Save Grok outputs outside `Assets/` until a separate approved import/postprocess task.

## Non-Grok P0 Source Notes

| Asset | Source still state | Current decision |
|---|---|---|
| `Assets/Player/Sprites/DiceRoll/0.png` | Source still missing for a dedicated icon. Existing dice prefab/texture assets may guide style, but current code needs only one static PNG. | Static/placeholder batch 1. |
| `Assets/Player/Sprites/Jump` | Candidate reference: `Assets/Player/Sprites/Idle/0.png`. | Blocked by human decision because existing jump movement is tweened. |
| `Assets/Player/Sprites/JumpBelow` | Source still missing; this is an effect, not a character identity sequence. | Prefer Unity/procedural or static placeholder decision. |
| `Assets/Player/Sprites/Debuff` | Source still missing; this is an effect/status sequence. | Prefer Unity/procedural or static placeholder decision. |

## Source Still Acceptance

- [ ] Character identity matches the existing idle sequence.
- [ ] Canvas includes enough padding for the requested motion.
- [ ] Side-view orientation matches current battle sprites.
- [ ] Human can identify which target folder the generated clip belongs to.
- [ ] No source still requires asset mutation before use.
