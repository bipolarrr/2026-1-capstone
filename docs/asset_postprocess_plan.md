# Asset Postprocess Plan

Audit date: 2026-05-27

Purpose: define the first manual postprocess batch for assets that already have source MP4s, direct frames, or transparent outputs. This is a plan only. It does not authorize modifying assets, `.meta` files, scenes, prefabs, ProjectSettings, Packages, Library, Temp, UserSettings, or files under `Assets/`.

## Rules

- Do not regenerate these items in Grok batch 1.
- Use existing MP4s, raw frames, or transparent outputs first.
- Keep all working outputs outside `Assets/` until a separate approved import task.
- Preserve current runtime paths and frame numbering contracts.
- Do not change code or builders as part of postprocess planning.
- Do not run Unity for this task.

## Postprocess Batch 1

| Item | Existing source | Required postprocess | Acceptance target |
|---|---|---|---|
| `Assets/Mobs/Sprites/Goblin/Hit` | `Assets/Mobs/Sprites/Goblin/Hit/Goblin_Hit.mp4` | Extract/background-remove/export missing direct frames 29-68, or rebuild the full direct runtime set 0-68 from the source. | 69 direct frames, contiguous `0.png` through `68.png`, consistent dimensions, alpha, no empty frames. |
| `Assets/Mobs/Sprites/Goblin/Dead` | `Assets/Mobs/Sprites/Goblin/Dead/Goblin_Dead.mp4`; existing direct frames | Remove low-alpha residue, normalize crop/padding, preserve frame order. | 145 direct frames, transparent-clean quality, bbox not full-frame from residue. |
| `Assets/Mobs/Sprites/Goblin/Attack` | Existing direct frames and `Goblin_Attack.mp4` | Visually inspect 27 edge-contact frames; pad/crop only if clipping is real. | Existing 104-frame count remains valid; no visible weapon/body clipping. |
| `Assets/Mobs/Sprites/Skeleton/Idle` | `Assets/Mobs/Sprites/Skeleton/Idle/Skeleton_Idle.mp4`; existing direct frames | Visually inspect bound-touching frames; repad from existing source if visible clipping exists. | 145 direct frames, alpha, stable centering, adequate padding. |
| `Assets/Player/Sprites/Defense` | `Assets/Player/Sprites/Defense/Player_Defense.mp4`; nested 145-frame transparent-clean output | Decide whether the 105-frame runtime folder is intentional; if not, rebuild from existing clean output. | Either documented 105-frame trim or rebuilt consistent runtime set. |
| `Assets/Player/Sprites/SmallHit` | `Assets/Player/Sprites/SmallHit/Player_SmallHit_1.mp4`; direct frames | Decide whether the 134-frame runtime folder is intentional; if not, rebuild from source. | Either documented 134-frame trim or rebuilt consistent runtime set. |
| `Assets/Player/Sprites/Attack` | `Assets/Player/Sprites/Attack/Player_Attack.mp4`; clean output exists | Visually inspect ten left-edge-contact frames; pad/crop only if the swing is clipped. | 145 direct frames stay valid; no visible clipped body/weapon. |
| `Assets/Player/Sprites/Weapon/Player_Weapon.png` | Existing PNG | Visually inspect edge-contact bbox; pad only if weapon is clipped. | Transparent PNG with enough canvas padding for battle use. |
| `Assets/Mobs/Sprites/Bat/Dead` | `Assets/Mobs/Sprites/Bat/Dead/Bat_Dead.mp4`; 73 direct frames | Confirm whether 73-frame death is intentional; if not, export a consistent set from source. | Either documented 73-frame trim or rebuilt consistent runtime set. |
| `Assets/Mobs/Sprites/Bat/Hit/Bat_Hit_transparent` | `Assets/Mobs/Sprites/Bat/Hit/Bat_Hit.mp4`; direct runtime folder exists | Classify or complete the partial transparent intermediate numbered 53-144. | Intermediate is either documented as partial or completed outside runtime folder. |

## Manual Postprocess Checklist

- [ ] Confirm the runtime loading mode before changing any future output.
- [ ] Confirm expected frame count or document available-frame semantics.
- [ ] Keep output dimensions consistent within each target folder.
- [ ] Preserve numeric order and avoid frame gaps.
- [ ] Keep source MP4s and intermediate folders intact.
- [ ] Check alpha, empty frames, edge contact, residue, centering, and clipping.
- [ ] Do not touch `.meta` files manually.
- [ ] Do not place outputs under `Assets/` until a separate approved import task.

## Human Decisions Before Import

| Question | Affects |
|---|---|
| Are shortened runtime folders intentional trims when source MP4s have 145 frames? | Player Defense, Player SmallHit, Bat Dead |
| Should root direct folders remain Unity-ready finals, or should `_transparent_clean` become the only final convention? | All generated character animations |
| Should edge-contact frames be padded even when visual clipping is not obvious? | Goblin Attack, Skeleton Idle, Player Attack, Player Weapon |
| Should partial transparent intermediates be completed, documented, or ignored? | Bat Hit transparent intermediate |
