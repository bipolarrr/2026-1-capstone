# Grok Imagine Manual Generation Packet

Audit date: 2026-05-27

Purpose: convert `docs/asset_audit_manifest.md` and `docs/grok_generation_queue.md` into a human-run Grok Imagine packet. This document does not authorize asset import, asset moves, asset deletion, `.meta` edits, Unity runs, scene changes, prefab changes, ProjectSettings changes, or reprocessing existing assets.

## Classification Summary

| Bucket | Items |
|---|---|
| Grok image-to-video needed | `Assets/Mobs/Sprites/Slime/Attack`, `Assets/Mobs/Sprites/Slime/Hit`, `Assets/Mobs/Sprites/Skeleton/Attack`, `Assets/Mobs/Sprites/Skeleton/Hit`, `Assets/Mobs/Sprites/Skeleton/Dead` |
| Static PNG / placeholder needed | `Assets/Player/Sprites/DiceRoll/0.png` |
| Unity/procedural effect preferred | `Assets/Player/Sprites/JumpBelow`, `Assets/Player/Sprites/Debuff` |
| Code/reference clarification needed | `Assets/Player/Sprites/Jump` |
| Postprocess first, do not regenerate yet | Goblin hit/dead/attack, Skeleton idle, Player defense/small hit/attack/weapon, Bat dead, Bat hit transparent intermediate |

## Code Inspection Notes

- `Assets/Player/Sprites/Jump` is loaded into `PlayerBodyAnimator.jumpSprites`, and `PlayerBodyAnimator.PlayJump()` requires sprites to play a body-pose action.
- `PlayerJumpAnimator` also performs a Unity vertical tween through `jumpHeight` and `jumpDuration`, so the core dodge movement is not blocked by missing jump body frames.
- `Assets/Player/Sprites/JumpBelow` is a below-body effect sequence. Because it is VFX-like and synchronized by code, prefer a Unity/procedural or simple placeholder effect decision before Grok I2V.
- `Assets/Player/Sprites/Debuff` is a VFX/status animation. Prefer procedural/static placeholder direction before Grok I2V.
- `Assets/Player/Sprites/DiceRoll/0.png` is currently only used as a character-select Dice weapon icon, so batch 1 should produce a static PNG, not a video.

## Grok Prompt Rules

Use this exact style for prompts:

`Locked wide shot. [one simple action], then [idle/recover/stay still].`

Do not add long style notes to the Grok prompt field. Put style, crop, alpha, and export requirements in acceptance notes outside the prompt.

## GROK-I2V-001 - Slime Attack

| Field | Value |
|---|---|
| Asset ID | `GROK-I2V-001` |
| Current path | `Assets/Mobs/Sprites/Slime/Attack` |
| Why needed for v0.1 | Stage 1 references this folder. It has 0 PNG frames, so attack falls back to idle. |
| Required source still | `Assets/Mobs/Sprites/Slime/Idle/0.png` |
| Recommended aspect/canvas class | 1:1 enemy sprite canvas; match Slime idle scale and padding. |
| One-line Grok prompt | `Locked wide shot. Slime lunges forward, then stays still.` |
| Draft settings | 480p, 1-3 seconds |
| Final settings | 720p, same prompt |

Acceptance checklist:

- [ ] Uses the provided Slime idle still as image-to-video source.
- [ ] Side-view battle sprite remains centered with full motion inside frame.
- [ ] Motion reads as one forward attack and short recovery/hold.
- [ ] Output can be converted to contiguous numbered PNG frames.
- [ ] Transparent-clean export has alpha, no empty frames, consistent dimensions, and padding.
- [ ] Unity-ready direct folder can be populated without path or code changes in a later import task.

Failure category checklist:

- [ ] Wrong character/style.
- [ ] Camera moves or zooms.
- [ ] Subject leaves frame or is cropped.
- [ ] Action is unreadable.
- [ ] Background/text/UI/watermark added.
- [ ] Too short, too long, or loops poorly.
- [ ] Alpha cleanup likely impractical.

## GROK-I2V-002 - Slime Hit

| Field | Value |
|---|---|
| Asset ID | `GROK-I2V-002` |
| Current path | `Assets/Mobs/Sprites/Slime/Hit` |
| Why needed for v0.1 | Stage 1 references this folder. It has 0 PNG frames, so hit reaction falls back to idle. |
| Required source still | `Assets/Mobs/Sprites/Slime/Idle/0.png` |
| Recommended aspect/canvas class | 1:1 enemy sprite canvas; match Slime idle scale and padding. |
| One-line Grok prompt | `Locked wide shot. Slime recoils sideways, then recovers.` |
| Draft settings | 480p, 1-3 seconds |
| Final settings | 720p, same prompt |

Acceptance checklist:

- [ ] Uses the provided Slime idle still as image-to-video source.
- [ ] Side-view battle sprite remains centered with full reaction inside frame.
- [ ] Motion reads as one damage reaction and recovery.
- [ ] Output can be converted to contiguous numbered PNG frames.
- [ ] Transparent-clean export has alpha, no empty frames, consistent dimensions, and padding.
- [ ] Unity-ready direct folder can be populated without path or code changes in a later import task.

Failure category checklist:

- [ ] Wrong character/style.
- [ ] Camera moves or zooms.
- [ ] Subject leaves frame or is cropped.
- [ ] Action is unreadable.
- [ ] Background/text/UI/watermark added.
- [ ] Too short, too long, or loops poorly.
- [ ] Alpha cleanup likely impractical.

## GROK-I2V-003 - Skeleton Attack

| Field | Value |
|---|---|
| Asset ID | `GROK-I2V-003` |
| Current path | `Assets/Mobs/Sprites/Skeleton/Attack` |
| Why needed for v0.1 | Stage 1 references this folder. It has 0 PNG frames, so attack falls back to idle. |
| Required source still | `Assets/Mobs/Sprites/Skeleton/Idle/0.png` |
| Recommended aspect/canvas class | Enemy archer sprite canvas; match Skeleton idle dimensions and bow padding. |
| One-line Grok prompt | `Locked wide shot. Skeleton fires arrow, then recovers.` |
| Draft settings | 480p, 1-3 seconds |
| Final settings | 720p, same prompt |

Acceptance checklist:

- [ ] Uses the provided Skeleton idle still as image-to-video source.
- [ ] Side-view battle sprite remains centered with bow and full body inside frame.
- [ ] Motion reads as one bow shot and recovery.
- [ ] Output can be converted to contiguous numbered PNG frames.
- [ ] Transparent-clean export has alpha, no empty frames, consistent dimensions, and padding.
- [ ] Unity-ready direct folder can be populated without path or code changes in a later import task.

Failure category checklist:

- [ ] Wrong character/style.
- [ ] Camera moves or zooms.
- [ ] Subject leaves frame or is cropped.
- [ ] Action is unreadable.
- [ ] Background/text/UI/watermark added.
- [ ] Too short, too long, or loops poorly.
- [ ] Alpha cleanup likely impractical.

## GROK-I2V-004 - Skeleton Hit

| Field | Value |
|---|---|
| Asset ID | `GROK-I2V-004` |
| Current path | `Assets/Mobs/Sprites/Skeleton/Hit` |
| Why needed for v0.1 | Stage 1 references this folder. It has 0 PNG frames, so hit reaction falls back to idle. |
| Required source still | `Assets/Mobs/Sprites/Skeleton/Idle/0.png` |
| Recommended aspect/canvas class | Enemy archer sprite canvas; match Skeleton idle dimensions and bow padding. |
| One-line Grok prompt | `Locked wide shot. Skeleton staggers back, then recovers.` |
| Draft settings | 480p, 1-3 seconds |
| Final settings | 720p, same prompt |

Acceptance checklist:

- [ ] Uses the provided Skeleton idle still as image-to-video source.
- [ ] Side-view battle sprite remains centered with bow and full body inside frame.
- [ ] Motion reads as one damage reaction and recovery.
- [ ] Output can be converted to contiguous numbered PNG frames.
- [ ] Transparent-clean export has alpha, no empty frames, consistent dimensions, and padding.
- [ ] Unity-ready direct folder can be populated without path or code changes in a later import task.

Failure category checklist:

- [ ] Wrong character/style.
- [ ] Camera moves or zooms.
- [ ] Subject leaves frame or is cropped.
- [ ] Action is unreadable.
- [ ] Background/text/UI/watermark added.
- [ ] Too short, too long, or loops poorly.
- [ ] Alpha cleanup likely impractical.

## GROK-I2V-005 - Skeleton Dead

| Field | Value |
|---|---|
| Asset ID | `GROK-I2V-005` |
| Current path | `Assets/Mobs/Sprites/Skeleton/Dead` |
| Why needed for v0.1 | Stage 1 references this folder. It has 0 PNG frames, so Skeleton has no death frame sequence. |
| Required source still | `Assets/Mobs/Sprites/Skeleton/Idle/0.png` |
| Recommended aspect/canvas class | Enemy archer sprite canvas; allow extra ground-level horizontal padding. |
| One-line Grok prompt | `Locked wide shot. Skeleton collapses, then stays still.` |
| Draft settings | 480p, 1-3 seconds |
| Final settings | 720p, same prompt |

Acceptance checklist:

- [ ] Uses the provided Skeleton idle still as image-to-video source.
- [ ] Side-view battle sprite remains centered with bow and full body inside frame.
- [ ] Motion reads as one collapse and final still pose.
- [ ] Output can be converted to contiguous numbered PNG frames.
- [ ] Transparent-clean export has alpha, no empty frames, consistent dimensions, and padding.
- [ ] Unity-ready direct folder can be populated without path or code changes in a later import task.

Failure category checklist:

- [ ] Wrong character/style.
- [ ] Camera moves or zooms.
- [ ] Subject leaves frame or is cropped.
- [ ] Action is unreadable.
- [ ] Background/text/UI/watermark added.
- [ ] Too short, too long, or loops poorly.
- [ ] Alpha cleanup likely impractical.

## Static / Placeholder Batch 1

| Asset | Classification | Why | Manual output target |
|---|---|---|---|
| `Assets/Player/Sprites/DiceRoll/0.png` | Static PNG / placeholder needed | `CharacterSelectSceneBuilder` loads this exact icon path for the Dice weapon button. Current code does not require a DiceRoll video sequence. | One transparent static Dice icon PNG, prepared outside `Assets/` until an explicit import task. |

Suggested human instruction: create a readable transparent dice weapon icon matching character-select button scale. Do not make an animation unless the DiceRoll folder contract is changed by a later task.

## Unity / Procedural Preferred Batch 1

| Asset | Classification | Why |
|---|---|---|
| `Assets/Player/Sprites/JumpBelow` | Unity/procedural effect preferred | It is a below-body VFX sequence tied to a short jump tween. A simple procedural burst, shadow squash, or static placeholder is likely lower risk than Grok video generation. |
| `Assets/Player/Sprites/Debuff` | Unity/procedural effect preferred | It is a status/VFX animation with a hard-coded frame count, not a character source still problem. Decide visual language before generating a long sequence. |

## Code / Reference Clarification

| Asset | Classification | Blocking question |
|---|---|---|
| `Assets/Player/Sprites/Jump` | Code/reference clarification needed | Should v0.1 rely on the existing vertical tween only, or require a generated body-pose sequence for `PlayerBodyAnimator.PlayJump()` and debug playback? |

## Postprocess First, Do Not Regenerate Yet

See `docs/asset_postprocess_plan.md` for the first postprocess batch.

## Grok generation batch 1

- `GROK-I2V-001`: `Assets/Mobs/Sprites/Slime/Attack`
- `GROK-I2V-002`: `Assets/Mobs/Sprites/Slime/Hit`
- `GROK-I2V-003`: `Assets/Mobs/Sprites/Skeleton/Attack`
- `GROK-I2V-004`: `Assets/Mobs/Sprites/Skeleton/Hit`
- `GROK-I2V-005`: `Assets/Mobs/Sprites/Skeleton/Dead`

## Static/placeholder batch 1

- `Assets/Player/Sprites/DiceRoll/0.png`

## Postprocess batch 1

- `Assets/Mobs/Sprites/Goblin/Hit`
- `Assets/Mobs/Sprites/Goblin/Dead`
- `Assets/Mobs/Sprites/Goblin/Attack`
- `Assets/Mobs/Sprites/Skeleton/Idle`
- `Assets/Player/Sprites/Defense`
- `Assets/Player/Sprites/SmallHit`
- `Assets/Player/Sprites/Attack`
- `Assets/Player/Sprites/Weapon/Player_Weapon.png`
- `Assets/Mobs/Sprites/Bat/Dead`
- `Assets/Mobs/Sprites/Bat/Hit/Bat_Hit_transparent`

## Items blocked by human decision

- `Assets/Player/Sprites/Jump`: decide whether sprite sequence is required for v0.1 or the existing Unity tween is sufficient.
- `Assets/Player/Sprites/JumpBelow`: decide procedural/static effect direction before any video generation.
- `Assets/Player/Sprites/Debuff`: decide procedural/static effect direction and expected visual behavior before any video generation.
