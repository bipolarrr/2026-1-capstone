# Sprite Asset Audit Manifest

Audit date: 2026-05-27

Scope: reporting only. No assets, `.meta` files, scenes, prefabs, project settings, packages, runtime code, or builder code were modified.

## Inputs Read

- `AGENTS.md`
- `docs/00_actual_project_audit.md`
- `docs/02_unity_scene_and_object_construction.md`
- `docs/11_project_decisions.md`
- `docs/13_next_backlog.md`
- `docs/16_v0_1_manual_qa_checklist.md`
- Asset roots:
  - `Assets/Player/Sprites/**`
  - `Assets/Mobs/Sprites/**`
  - `Assets/Backgrounds/**`
  - `Assets/Story/**`
  - `Assets/UI/**`
  - `Assets/Mahjong/**`
- Code references:
  - `Assets/Editor/SceneBuilderUtility.cs`
  - `Assets/Editor/*SceneBuilder.cs`
  - `Assets/Scripts/Stages/**`
  - `Assets/Scripts/Battle/**`
  - `Assets/Scripts/Mahjong/**`
  - `Assets/Scripts/Explore/**`

## Scan Summary

| Root | Files found |
|---|---:|
| Requested roots, all files | 11,769 |
| `.png` | 5,825 |
| `.png.meta` sidecars missing | 0 |
| `.mp4` | 15 |
| `.jpg` | 4 |
| `.webp` | 3 |
| `.asset` | 1 |
| `.psxprj` | 1 |

`ffprobe` was available and used for MP4 stream metadata. Image checks used file existence, dimensions, alpha channel presence, alpha bounding boxes, direct-folder frame numbering, and `.png.meta` sidecar presence.

## Classification Rules Used

| Classification | Meaning in this audit |
|---|---|
| source image | Static image or unsliced/reference image whose provenance is not proven final. |
| source/intermediate MP4 | Generated video source or intermediate video used for extraction. |
| raw extracted frames | Direct extraction from video, usually opaque numeric PNG frames or nested unprocessed folders. |
| transparent output | Background-removed output in a folder/path containing `_transparent` but not `_transparent_clean`. |
| transparent_clean output | Background-removed and cleaned output in a folder/path containing `_transparent_clean`. |
| Unity-ready PNG | Asset currently referenced by builders/runtime or a numeric frame sequence directly loaded by builders/runtime. |
| missing | Referenced file/folder is absent, or a numbered direct sequence is missing expected frames. |
| defective | Present but fails an acceptance check such as missing alpha where transparency is expected, frame-count mismatch against code expectations, residue touching bounds, or obvious inconsistent resolution. |
| unknown | Present but not referenced by scanned runtime/builder code and not clearly source/intermediate/final from path alone. |

## Hard-Coded Runtime And Builder References

| Area | Referenced asset or folder | Code reference | Audit status |
|---|---|---|---|
| Player idle | `Assets/Player/Sprites/Idle`, `Assets/Player/Sprites/Idle/0.png` | `SceneBuilderUtility` | Unity-ready PNG, present, 145 frames, no holes, 1000x1000, alpha present. |
| Player low HP | `Assets/Player/Sprites/LowHp` | `SceneBuilderUtility` expects 95 frames | Unity-ready PNG, present, frames 0-144; code loads 0-94 only. Extra frames should be classified later. |
| Player jump | `Assets/Player/Sprites/Jump` | `SceneBuilderUtility` | Missing/defective: folder exists but has 0 PNG frames. |
| Player jump below | `Assets/Player/Sprites/JumpBelow` | `SceneBuilderUtility` expects 145 frames when used | Missing/defective: folder exists but has 0 PNG frames. |
| Player defense | `Assets/Player/Sprites/Defense` | `SceneBuilderUtility` | Unity-ready PNG, present, 105 direct frames, no holes, 1000x1000, alpha present. Source MP4 and nested raw/clean outputs have 145 frames, so direct runtime set is shortened. |
| Player small hit | `Assets/Player/Sprites/SmallHit` | `SceneBuilderUtility` | Unity-ready PNG, present, 134 direct frames, no holes, 1000x1000, alpha present. Source MP4 has 145 frames, so direct runtime set is shortened. |
| Player strong hit | `Assets/Player/Sprites/StrongHit` | `SceneBuilderUtility` expects 47 frames | Unity-ready PNG, present, 47 frames, no holes, 1000x1000, alpha present. |
| Player debuff | `Assets/Player/Sprites/Debuff` | `SceneBuilderUtility` expects 156 frames | Missing/defective: folder exists but has 0 PNG frames. |
| Player death | `Assets/Player/Sprites/Die/Player_Die_1000x1000` | `SceneBuilderUtility`, `DiceBattleSceneBuilder` expect 145 frames | Unity-ready PNG, present, 145 frames, no holes, 1000x1000, alpha present. |
| Player attack | `Assets/Player/Sprites/Attack` | `SceneBuilderUtility` expects 145 frames | Unity-ready PNG, present, 145 frames, no holes, 918x726, alpha present. Ten frames have alpha bbox touching the left edge; visually inspect for cropping. |
| Player weapon | `Assets/Player/Sprites/Weapon/Player_Weapon.png` | `SceneBuilderUtility` | Unity-ready PNG, present, 502x616, alpha present, bbox touches image edge. |
| Dice icon | `Assets/Player/Sprites/DiceRoll/0.png` | `CharacterSelectSceneBuilder` | Missing/defective: folder exists but has 0 PNG frames, so Dice weapon icon cannot load from this path. |
| UI heart | `Assets/UI/UI_Heart.png` | `SceneBuilderUtility`, `HeartDisplay` | Unity-ready PNG, present, 1040x288, alpha present, meta present. |
| Main menu logo | `Assets/UI/MainScreen_Logo.png` | `MainMenuSceneBuilder` | Unity-ready PNG, present, 2816x1536, alpha present, bbox full-frame. |
| UI background | `Assets/UI/UI_Background.png` | `DiceBattleSceneBuilder` | Unity-ready PNG, present, 1024x1024, RGB opaque, meta present. |
| Story slides | `Assets/Story/Story_CutScene_0.png` through `_2.png` | `CharacterSelectSceneBuilder` | Unity-ready PNG, present, meta present. Slide 0 is 2816x1536; slides 1-2 are 2698x1568. |
| Forest background | `Assets/Backgrounds/Fight_Background_0_Forest.png` | `Stage1Forest` | Unity-ready PNG, present, 2752x1536, alpha present, bbox full-frame. |
| Cave background | `Assets/Backgrounds/Fight_Background_1_Cave.png` | `Stage2Cave` | Unity-ready PNG, present, 1280x720, RGB opaque, meta present. |
| Mahjong table | `Assets/Mahjong/Table.png` | `MahjongBattleSceneBuilder` | Unity-ready PNG, present, 1500x273, alpha present. |
| Mahjong tile DB | `Assets/Mahjong/MahjongTileSprites.asset` | `CharacterSelectSceneBuilder`, `MahjongBattleSceneBuilder` | Unity-ready asset, present, meta present. References `m_full.png`, `p_full.png`, `s_full.png`, and `w.png`. |
| Mahjong white tile | `Assets/Mahjong/w.png` | `CharacterSelectSceneBuilder`, tile DB | Unity-ready PNG, present, 447x619, alpha present. |
| Skeleton projectile | `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png` | `SceneBuilderUtility`, battle builders, `Stage1Forest` | Unity-ready PNG, present, 993x181, alpha present, bbox padded. |

## Stage Enemy References

| Enemy | Referenced assets | Classification | Audit status |
|---|---|---|---|
| Slime | `Idle`, `Attack`, `Hit`, `Dead` under `Assets/Mobs/Sprites/Slime` | Unity-ready PNG folders | `Idle` has 22 direct frames plus `Slime_0.png`; `Dead` has 145 frames; `Attack` and `Hit` folders exist but have 0 PNG frames. Runtime attack/hit animation falls back to idle because no action frames are loaded. |
| Goblin | `Idle`, `Attack`, `Hit`, `Dead` under `Assets/Mobs/Sprites/Goblin` | Unity-ready PNG folders plus MP4 sources | `Idle` 145 frames OK. `Attack` 104 frames OK for code expectation, but 27 frames touch alpha bounds. `Hit` is defective: code expects 69 direct frames but only 29 exist. `Dead` 145 frames exist, but every frame has nonzero alpha at full-frame bounds, indicating low-alpha residue or unclean transparency. |
| Bat | `Idle`, `Attack/Bat_Attack_transparent_clean`, `Hit`, `Dead` | Unity-ready PNG folders plus MP4 sources/intermediates | `Attack/Bat_Attack_transparent_clean` 145 frames OK. `Hit` 53 direct frames OK for code expectation. `Dead` 73 direct frames, no holes, but 48 frames are heavily off-center by bbox heuristic; source MP4 has 145 frames. |
| Skeleton | `Idle`, `Attack`, `Hit`, `Dead`, `Projectile/Skeleton_Arrow_transparent.png` | Unity-ready PNG folders plus projectile | `Idle` has 145 frames, but all alpha bboxes touch bottom/right or top bounds. `Attack`, `Hit`, and `Dead` folders exist but have 0 PNG frames, so attack/hit fall back to idle and death has no frame sequence. Projectile PNG is OK. |
| Stage 2 Golem | `Assets/Mobs/Enemy_Golem.png` | Unity-ready PNG outside requested scan roots | Present, 617x761, alpha present, bbox full-frame. Runtime-referenced but outside `Assets/Mobs/Sprites/**`; should be normalized later only after policy approval. |
| Stage 2 Water Elemental | `Assets/Mobs/Water_Elemental.png` | Unity-ready PNG outside requested scan roots | Present, 466x442, alpha present, bbox full-frame. Runtime-referenced but outside `Assets/Mobs/Sprites/**`; should be normalized later only after policy approval. |
| Stage 1 Dracula boss | `Assets/Mobs/Boss_Dracula_example.png` | Unity-ready PNG outside requested scan roots | Present, 743x1176, alpha present, bbox full-frame. Runtime-referenced but outside `Assets/Mobs/Sprites/**`; should be normalized later only after policy approval. |
| Stage 2 boss | `spritePath = null` | intentional fallback | No sprite expected by current code. Builder creates a color fallback if no boss sprite is assigned. |

## MP4 Sources And Intermediates

| MP4 | Stream metadata | Classification | Notes |
|---|---|---|---|
| `Assets/Player/Sprites/Player_Throwing_Something_Big_Away.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Present but not directly referenced by scanned code. |
| `Assets/Player/Sprites/Attack/Player_Attack.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime frames exist. |
| `Assets/Player/Sprites/Defense/Player_Defense.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime frames only 105; clean intermediate has 145. |
| `Assets/Player/Sprites/Die/Player_Die.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Runtime uses `Die/Player_Die_1000x1000`, not raw nested frames. |
| `Assets/Player/Sprites/Idle/Player_Idle.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime frames exist. |
| `Assets/Player/Sprites/LowHp/Player_LowHp.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Code loads only frames 0-94 from direct folder. |
| `Assets/Player/Sprites/SmallHit/Player_SmallHit_1.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime frames only 134. |
| `Assets/Player/Sprites/StrongHit/Player_StrongHit.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Code uses 47 direct frames. |
| `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Clean runtime path has 145 frames. |
| `Assets/Mobs/Sprites/Bat/Dead/Bat_Dead.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime folder has 73 frames. |
| `Assets/Mobs/Sprites/Bat/Hit/Bat_Hit.mp4` | 960x960, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime folder has expected 53 frames. |
| `Assets/Mobs/Sprites/Goblin/Attack/Goblin_Attack.mp4` | 928x976, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime folder has expected 104 frames. |
| `Assets/Mobs/Sprites/Goblin/Dead/Goblin_Dead.mp4` | 544x544, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime frames are 2176x1808 and have alpha residue. |
| `Assets/Mobs/Sprites/Goblin/Hit/Goblin_Hit.mp4` | 928x976, 24 fps, 145 frames | source/intermediate MP4 | Direct runtime folder is missing frames 29-68 for code expectation. |
| `Assets/Mobs/Sprites/Skeleton/Idle/Skeleton_Idle.mp4` | 992x912, 24 fps, 145 frames | source/intermediate MP4 | Direct idle frames exist but touch alpha bounds. |

## Intermediate And Variant Folders

| Folder | Classification | Audit status |
|---|---|---|
| `Assets/Player/Sprites/Attack/Player_Attack` | raw extracted frames | 145 opaque 960x960 frames, no holes. |
| `Assets/Player/Sprites/Attack/Player_Attack_transparent` | transparent output | 145 frames, alpha present, 10 frames touch alpha bounds. |
| `Assets/Player/Sprites/Attack/Player_Attack_transparent_clean` | transparent_clean output | 145 frames, alpha present, 10 frames touch alpha bounds. |
| `Assets/Player/Sprites/Defense/Player_Defense` | raw extracted frames | 145 opaque 960x960 frames, no holes. |
| `Assets/Player/Sprites/Defense/Player_Defense_transparent_clean` | transparent_clean output | 145 frames, 780x652, alpha present. |
| `Assets/Player/Sprites/Die/Player_Die` | raw extracted frames | 145 opaque 960x960 frames, no holes. |
| `Assets/Player/Sprites/Die/Player_Die_transparent_clean` | transparent_clean output | 145 frames, 942x774, alpha present, 56 off-center frames by bbox heuristic. |
| `Assets/Player/Sprites/LowHp/Player_LowHp` | raw extracted frames | 145 opaque 960x960 frames, no holes. |
| `Assets/Player/Sprites/LowHp/Player_LowHp_transparent_clean` | transparent_clean output | 145 frames, 762x672, alpha present. |
| `Assets/Player/Sprites/StrongHit/Player_StrongHit` | raw extracted frames | 24 opaque 960x960 frames. |
| `Assets/Player/Sprites/StrongHit/Player_StrongHit_transparent_clean` | transparent_clean output | 24 frames, 810x718. |
| `Assets/Player/Sprites/StrongHit/Player_StrongHit_20260511_230207*` | source/intermediate variants | 145-frame alternate set, including 650x640 and 764x640 transparent variants. Not referenced by scanned code. |
| `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack` | raw extracted frames | 145 opaque 960x960 frames, no holes. |
| `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent` | transparent output | 145 frames, alpha present, 142 frames touch bounds; clean version fixes dimensions/bounds. |
| `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean` | transparent_clean output and runtime folder | 145 frames, 898x592, alpha present, no holes. |
| `Assets/Mobs/Sprites/Bat/Hit/Bat_Hit` | raw extracted frames | 145 opaque 960x960 frames, no holes. |
| `Assets/Mobs/Sprites/Bat/Hit/Bat_Hit_transparent` | transparent output | 92 frames numbered 53-144. Not directly referenced by code. |

## Defects And Missing Assets

| Priority | Asset/folder | Classification | Finding |
|---|---|---|---|
| P0 | `Assets/Player/Sprites/DiceRoll/0.png` | missing | `CharacterSelectSceneBuilder` references it for the Dice button icon, but the folder has 0 PNGs. |
| P0 | `Assets/Player/Sprites/Jump` | missing | Builder loads available jump frames, but folder has 0 PNGs. |
| P0 | `Assets/Player/Sprites/JumpBelow` | missing | Builder path expects 145 numbered frames when jump-below is included, but folder has 0 PNGs. |
| P0 | `Assets/Player/Sprites/Debuff` | missing | Builder expects 156 numbered frames, but folder has 0 PNGs. |
| P0 | `Assets/Mobs/Sprites/Slime/Attack` | missing | Stage references folder; folder has 0 PNGs. Runtime will use idle fallback for attack. |
| P0 | `Assets/Mobs/Sprites/Slime/Hit` | missing | Stage references folder; folder has 0 PNGs. Runtime will use idle fallback for hit. |
| P0 | `Assets/Mobs/Sprites/Skeleton/Attack` | missing | Stage references folder; folder has 0 PNGs. Runtime will use idle fallback for attack. |
| P0 | `Assets/Mobs/Sprites/Skeleton/Hit` | missing | Stage references folder; folder has 0 PNGs. Runtime will use idle fallback for hit. |
| P0 | `Assets/Mobs/Sprites/Skeleton/Dead` | missing | Stage references folder; folder has 0 PNGs. Skeleton has no death frame sequence. |
| P1 | `Assets/Mobs/Sprites/Goblin/Hit` | defective | Code expects 69 direct frames; only frames 0-28 exist. Source MP4 has 145 frames, so this is likely postprocessing/extraction work, not Grok regeneration. |
| P1 | `Assets/Mobs/Sprites/Goblin/Dead` | defective | 145 direct frames exist, but all have nonzero alpha across full image bounds and 2176x1808 resolution, suggesting low-alpha background residue and normalization/crop work. |
| P1 | `Assets/Mobs/Sprites/Goblin/Attack` | defective | Expected 104 frames exist, but 27 frames have alpha bbox touching the image bounds. Needs visual crop/pad inspection. |
| P1 | `Assets/Mobs/Sprites/Skeleton/Idle` | defective | 145 frames exist, but all alpha bboxes touch bounds. Needs visual crop/pad inspection or repadded output from existing MP4. |
| P1 | `Assets/Player/Sprites/Defense` | defective/shortened | Direct runtime folder has 105 frames while source MP4 and clean intermediates have 145. Code accepts available frames, but the pipeline is inconsistent. |
| P1 | `Assets/Player/Sprites/SmallHit` | defective/shortened | Direct runtime folder has 134 frames while source MP4 has 145. Code accepts available frames, but the pipeline is inconsistent. |
| P1 | `Assets/Player/Sprites/Attack` | possible defective | 145 expected frames exist, but 10 frames touch the left alpha bound. Needs visual inspection for cropped swing frames. |
| P1 | `Assets/Player/Sprites/Weapon/Player_Weapon.png` | possible defective | Alpha bbox touches image bounds. Needs visual inspection before deciding whether to crop or pad. |
| P2 | `Assets/Mobs/Enemy_Golem.png`, `Assets/Mobs/Water_Elemental.png`, `Assets/Mobs/Boss_Dracula_example.png` | acceptable but non-normalized | Runtime-referenced static mob sprites exist outside `Assets/Mobs/Sprites/**`. Do not move now; normalize later only with explicit policy and builder/stage updates. |

## Unknown Or Unreferenced Assets

| Asset/folder | Classification | Notes |
|---|---|---|
| `Assets/UI/UI_Chat.png` | unknown/source image | Present, meta present, not referenced by scanned code. |
| `Assets/UI/UI_Background.webp` | unknown/source image | Present, meta present, not referenced by scanned code. PNG version is referenced. |
| `Assets/Mahjong/s_full.jpg` | unknown/source image | Present, meta present. Tile DB uses `s_full.png`, not the JPG. |
| `Assets/Mahjong/061dce84dbd64acca9996170440a6979.png` | unknown/source image | Present, meta present, not referenced by tile DB or scanned code. |
| `Assets/Mahjong/b_*`, `m_1*`, `m_2.png`, `m_3.png`, `m_4.png` | unknown/source images or variants | Present, meta present. Tile DB references `m_full.png`, `p_full.png`, `s_full.png`, and `w.png`; these variants are not directly referenced by scanned code. |

## Audit Limits

- Unity was not run.
- Import settings were not changed or verified through the Unity Editor.
- Visual quality was inferred from metadata and alpha bounds only; human visual review is still required for cropped/off-center calls.
- Fallback files under `Assets/Generated/**` were not in the requested scan roots and were not audited here.
