# Runtime Asset Reference Manifest

- Post-audit closure date: 2026-06-12 KST
- Current HEAD at closure: `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`

## Latest Runtime Asset Source-Of-Truth Delta

This section updates the initial 2026-06-11 manifest after `d007d473` and `311ae2e6`. The original manifest table is preserved below as historical baseline.

| Asset group | Initial baseline status | Latest master status | Evidence | Current classification |
|---|---|---|---|---|
| Runtime scenes | Missing from tracked HEAD; build settings referenced scenes. | Six runtime scenes and their `.meta` files are tracked. | `git ls-files Assets/Scenes`; latest build settings; runtime player build success. | Partially mitigated |
| Hold'em assets | Foreground/untracked prototype assets. | Hold'em scripts, builder, tests, card sprites, scene, and build settings are tracked. | `git show --stat e1337d16`; `git ls-files Assets/Holdem Assets/Scripts/Holdem Assets/Editor/Tests/Holdem`. | Implemented and validated with builder churn risk |
| Mahjong red fives/honors/back tile | Red fives and back tile were untracked/ambiguous. | `m_5_red`, `p_5_red`, `s_5_red`, honor tiles, `tile_back_acorn`, and `MahjongTileSprites.asset` are tracked/updated. | `git show --stat d007d473`; `MahjongTileSpriteDatabase.GetBackSprite()` returns a sprite named `tile_back_acorn`; Mahjong validation 43/43 and 131/131 passed. | Implemented and validated |
| D6 mine dice builder chain | Primary dice prefab inputs ignored/untracked. | Primary D6 mine chain is tracked and unignored through narrow `.gitignore` exceptions. | `git show --stat 311ae2e6`; `git check-ignore -v -n Assets/Dices/Prefabs/Dice_d6_mine.prefab` reports not ignored. | Partially mitigated |
| Imported dice fallback package | Ignored/untracked. | Still ignored by design. | `git check-ignore -v -n Assets/Dices/Prefabs/Dice_d6.prefab` reports `.gitignore` ignore rule. | Open / intentionally excluded |
| Slime Attack/Hit runtime frames | Foreground untracked. | Runtime PNG frames and `.meta` files for referenced folders are tracked in `311ae2e6`; source `.mp4` files are excluded. | `git show --stat 311ae2e6`; runtime asset reference test passed. | Partially mitigated |
| Skeleton Dead runtime frames | Foreground untracked. | Runtime PNG frames and `.meta` files are tracked in `311ae2e6`; source `.mp4`/`.webp` media are excluded. | `git show --stat 311ae2e6`; runtime asset reference test passed. | Partially mitigated |
| Skeleton Attack/Hit empty-folder references | Code referenced empty folders. | Stage1 skeleton attack/hit folder references removed so empty path means intentional fallback. | `git show --stat 311ae2e6`; `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks` passed. | Mitigated for promoted references |
| Goblin Hit frame count | Code expected 69 frames but direct PNG count was 29. | Stage1/Stage2 Goblin `hitSpriteFrameCount` now matches 29. | `git show --stat 311ae2e6`; current `Stage1Forest.cs`; runtime asset reference test passed. | Mitigated for promoted references |
| Golem InGame runtime frames | Foreground untracked. | `Assets/Mobs/Sprites/Golem/InGame/{Idle,Attack,Hit,Dead}` PNG frames and `.meta` files are tracked. | `git show --stat 311ae2e6`; runtime asset reference test passed. | Partially mitigated |
| Elemental WaterCannon | Foreground untracked. | WaterCannon PNG and `.meta` are tracked. | `git show --stat 311ae2e6`; runtime asset reference test passed. | Partially mitigated |
| Explore map UI PNGs | Foreground untracked. | `UI_Map`, `UI_MapIcon`, and `MapIcons` PNGs/metas are tracked. | `git show --stat 311ae2e6`; runtime asset reference test passed. | Partially mitigated |
| Source/intermediate media | Mixed foreground source artifacts. | Not promoted by `311ae2e6`; excluded from runtime source-of-truth commit. | Commit stat excludes targeted `.mp4`/`.webp`/`SpritePipelineWork/**` groups from the runtime asset source-of-truth commit. | Open as provenance/cleanup risk |

Latest validation evidence:

- Runtime full EditMode passed 132/132.
- `StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks` passed.
- Runtime Windows Standalone x64 build exited 0 and produced `CapstoneRuntimeAssets.exe`.
- Classification: implemented and validated with `pass-with-validation-hygiene-risk`.

Remaining asset caveats:

- The runtime reference test validates existence and minimum direct PNG frame counts, not visual quality.
- Manual visual QA and provenance review remain required.
- Broad dirty/untracked foreground assets outside the promoted groups are not covered by this latest validation.
- Validation worktree settings churn remains open and was not copied into the foreground checkout.

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: C# path strings, scene builders, stage data, ScriptableObject/YAML references, filesystem existence/tracking checks, read-only PNG header inspection.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Method

Scans looked for `AssetDatabase.LoadAssetAtPath`, `Resources.Load`, `SceneManager.LoadScene`, `SpritePath`, `SpriteFolder`, `AudioClip`, `Material`, `Prefab`, and literal `Assets/` path strings in `Assets/Scripts/**` and `Assets/Editor/**`. Referenced paths were checked for existence, tracking/ignore status, `.meta` status, direct PNG counts, and representative dimensions where feasible.

Unreferenced folders were not treated as runtime-current.

## Manifest

Initial audit baseline at `e6de7c9`; use Latest Runtime Asset Source-Of-Truth Delta above for current `master`.

| Asset ID | Referenced path | Owner file/class | Reference mechanism | Runtime role | Exists? | Tracked? | Ignored? | Direct PNG count / relevant file count | Representative dimensions if image | `.meta` status | Classification | Risk | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| scene-main-menu | `Assets/Scenes/MainMenu.unity` | Build settings, `MainMenuSceneBuilder` | build setting/builder path | Main menu scene | yes foreground | no | yes | 1 scene | n/a | ignored | generated-scene-artifact | High | Missing from clean HEAD tracking. |
| scene-character-select | `Assets/Scenes/CharacterSelect.unity` | Build settings, `CharacterSelectSceneBuilder` | build setting/builder path | Character select scene | yes foreground | no | yes | 1 scene | n/a | ignored | generated-scene-artifact | High | Runtime loads `CharacterSelect`. |
| scene-explore | `Assets/Scenes/GameExploreScene.unity` | Build settings, runtime scene strings, builder | build setting/load scene/builder path | Explore scene | yes foreground | no | yes | 1 scene | n/a | ignored | generated-scene-artifact | High | Runtime load target, not tracked. |
| scene-dice | `Assets/Scenes/DiceBattleScene.unity` | Build settings, runtime scene strings, builder | build setting/load scene/builder path | Dice battle scene | yes foreground | no | yes | 1 scene | n/a | ignored | generated-scene-artifact | High | Runtime load target, not tracked. |
| scene-mahjong | `Assets/Scenes/MahjongBattleScene.unity` | Build settings, runtime scene strings, builder | build setting/load scene/builder path | Mahjong battle scene | yes foreground | no | yes | 1 scene | n/a | ignored | generated-scene-artifact | High | Runtime load target, not tracked. |
| scene-holdem | `Assets/Scenes/HoldemBattleScene.unity` | dirty foreground build settings/untracked builder | foreground build setting/prototype | Holdem prototype scene | yes foreground | no | no | 1 scene | n/a | untracked | ambiguous | High | Not tracked and not routed by tracked runtime. |
| ui-logo | `Assets/UI/MainScreen_Logo.png` | `MainMenuSceneBuilder` | asset path | Main menu logo | yes | yes, modified | no | 1 PNG | 1672x941 | tracked | builder-input | Medium | Dirty foreground asset. |
| ui-heart | `Assets/UI/UI_Heart.png` | `SceneBuilderUtility`, controllers | asset path | Heart UI sprite | yes | yes | no | 1 PNG | 1040x288 | tracked | runtime-current | Low | Direct UI runtime reference. |
| ui-background | `Assets/UI/UI_Background.png` | builders | asset path | UI background | yes | yes | no | 1 PNG | 1024x1024 | tracked | builder-input | Low | Referenced by generated UI. |
| ui-map | `Assets/UI/UI_Map.png` | `GameExploreSceneBuilder` | asset path | Explore map visual | yes foreground | no | no | 1 PNG | 1122x1402 | untracked | ambiguous | Medium | Foreground map presentation only. |
| ui-map-icons | `Assets/UI/MapIcons/**` | `GameExploreSceneBuilder` | asset path | Map node icons | yes foreground | no | no | 4 PNG | 627x627 | untracked | ambiguous | Medium | Boss/shop/heal/combat icons are untracked. |
| story-slides | `Assets/Story/Story_CutScene_*.png` | `CharacterSelectSceneBuilder` | asset path | Intro/story slides | yes | yes, modified | no | 3 PNG | 1672x941, 1646x956 | tracked | builder-input | Medium | Dirty foreground art. |
| font-mona | `Assets/TextMesh Pro/Fonts/Mona12.asset` | `SceneBuilderUtility` | asset path | TMP font | yes | yes | no | 1 asset | n/a | tracked | runtime-current | Low | Direct UI font reference. |
| player-idle | `Assets/Player/Sprites/Player_Idle` | `SceneBuilderUtility` | sprite folder path | Player idle animation | yes | yes | no | 145 direct PNG | 1000x1000 | tracked | runtime-current | Low | Direct runtime animation folder. |
| player-lowhp | `Assets/Player/Sprites/Player_LowHp` | `SceneBuilderUtility` | sprite folder path | Player low HP animation | yes | yes | no | 145 direct PNG, 580 recursive | 1000x1000 | tracked | runtime-current | Low | Code uses subset counts. |
| player-jump | `Assets/Player/Sprites/Player_Jump` | `SceneBuilderUtility` | sprite folder path | Player jump animation | yes | yes | no | 0 direct PNG | n/a | tracked | ambiguous | Medium | Folder exists but no direct frames. |
| player-defense | `Assets/Player/Sprites/Player_Defense` | `SceneBuilderUtility` | sprite folder path | Player defense animation | yes | yes | no | 105 direct PNG, 540 recursive | 1000x1000 | tracked | runtime-current | Low | Direct runtime animation folder. |
| player-hit | `Assets/Player/Sprites/Player_SmallHit`, `Player_StrongHit` | `SceneBuilderUtility` | sprite folder path | Player hit animations | yes | yes | no | 134 and 47 direct PNG | 1000x1000 class | tracked | runtime-current | Low | StrongHit has many recursive variants. |
| player-debuff | `Assets/Player/Sprites/Player_Debuff` | `SceneBuilderUtility` | sprite folder path | Player debuff animation | yes | yes | no | 0 direct PNG | n/a | tracked | ambiguous | Medium | Folder exists but no direct frames. |
| player-death | `Assets/Player/Sprites/Player_Die/Player_Die_1000x1000` | `SceneBuilderUtility` | sprite folder path | Player death animation | yes | yes | no | 145 direct PNG | 1000x1000 | tracked | runtime-current | Low | Direct runtime animation folder. |
| player-attack | `Assets/Player/Sprites/Player_Attack` | `SceneBuilderUtility` | sprite folder path | Player attack animation | yes | yes | no | 145 direct PNG, 580 recursive | 918x726 sample | tracked | runtime-current | Low | Direct runtime animation folder. |
| player-weapon | `Assets/Player/Sprites/Weapon/Player_Weapon.png` | `SceneBuilderUtility` | sprite path | Player weapon sprite | yes | yes | no | 1 PNG | 502x616 | tracked | runtime-current | Low | Direct runtime sprite. |
| bg-forest | `Assets/Backgrounds/Fight_Background_0_Forest.png` | `Stage1Forest` | stage data path | Stage 1 background | yes | yes | no | 1 PNG | 2752x1536 | tracked | stage-data-runtime | Low | Direct stage reference. |
| bg-cave | `Assets/Backgrounds/Fight_Background_1_Cave.png` | `Stage2Cave` | stage data path | Stage 2 background | yes | yes | no | 1 file | not read | tracked | stage-data-runtime | Low | Direct stage reference. |
| slime-idle | `Assets/Mobs/Sprites/Slime/Idle` | `Stage1Forest` | stage data sprite folder | Slime idle | yes | yes | no | 22 direct PNG | 664x588 | tracked | stage-data-runtime | Low | Direct enemy animation. |
| slime-attack-hit | `Assets/Mobs/Sprites/Slime/Attack`, `Hit` | `Stage1Forest` | stage data sprite folder | Slime attack/hit | yes foreground | no | no | 145 each | 1152x1024 | untracked | ambiguous | Medium | Runtime path exists only as foreground untracked folders. |
| slime-dead | `Assets/Mobs/Sprites/Slime/Dead` | `Stage1Forest` | stage data sprite folder | Slime death | yes | yes | no | 145 direct PNG | 890x604 | tracked | stage-data-runtime | Low | Direct enemy animation. |
| goblin-attack | `Assets/Mobs/Sprites/Goblin/Attack` | `Stage1Forest` | stage data sprite folder | Goblin attack | yes | yes, modified | no | 50 direct PNG | 771x789 | tracked | stage-data-runtime | Medium | Matches `attackFrameCount=50`, but dirty foreground art. |
| goblin-hit | `Assets/Mobs/Sprites/Goblin/Hit` | `Stage1Forest` | stage data sprite folder | Goblin hit | yes | yes | no | 29 direct PNG | 908x976 | tracked | stage-data-runtime | High | Code sets `hitFrameCount=69`; direct PNG count is 29. |
| bat-attack | `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean` | `Stage1Forest`, `Stage2Cave` | stage data sprite folder | Bat attack | yes | yes | no | 145 direct PNG | sample not recorded | tracked | stage-data-runtime | Low | Direct enemy animation. |
| skeleton-attack-hit | `Assets/Mobs/Sprites/Skeleton/Attack`, `Hit` | `Stage1Forest` | stage data sprite folder | Skeleton attack/hit | yes folders | yes | no | 0 direct PNG | n/a | tracked folders | missing-reference | High | Runtime references have no direct frames. |
| skeleton-dead | `Assets/Mobs/Sprites/Skeleton/Dead` | `Stage1Forest` | stage data sprite folder | Skeleton death | yes foreground | no | no | 145 direct PNG | 820x912 | untracked | ambiguous | Medium | Runtime path exists only foreground untracked. |
| skeleton-projectile | `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png` | `Stage1Forest`, `SceneBuilderUtility` | sprite path | Skeleton projectile | yes | yes | no | 1 PNG | 993x181 | tracked | stage-data-runtime | Low | Direct projectile reference. |
| boss-dracula | `Assets/Mobs/Boss_Dracula_example.png` | `Stage1Forest` | stage data sprite path | Stage 1 boss sprite | yes | yes | no | 1 PNG | 743x1176 | tracked | stage-data-runtime | Low | Direct boss reference. |
| golem-ingame | `Assets/Mobs/Sprites/Golem/InGame/**` | `Stage2Cave` | stage data sprite folders | Stage 2 Golem | yes foreground | no | no | 130/65/29/130 PNG | sample not recorded | untracked | ambiguous | Medium | Referenced by Stage2 but untracked. |
| elemental-water | `Assets/Mobs/Water_Elemental.png` | `Stage2Cave` | stage data sprite path | Stage 2 elemental | yes | yes | no | 1 PNG | 466x442 | tracked | stage-data-runtime | Low | Direct enemy sprite. |
| elemental-watercannon | `Assets/Mobs/Sprites/Elemental/WaterCannon/WaterCannon.png` | `Stage2Cave` | VFX path | Elemental projectile/VFX | yes foreground | no | no | 1 PNG | 1879x837 | untracked | ambiguous | Medium | Referenced by Stage2 but untracked. |
| dice-prefab-mine | `Assets/Dices/Prefabs/Dice_d6_mine.prefab` | `DiceBattleSceneBuilder` | prefab path | Physical dice prefab | yes foreground | no | yes | 1 prefab | n/a | ignored | builder-input | High | Builder input is ignored/untracked. |
| dice-prefab-fallback | `Assets/Dices/Prefabs/Dice_d6.prefab` | `DiceBattleSceneBuilder` | fallback prefab path | Physical dice prefab fallback | yes foreground | no | yes | 1 prefab | n/a | ignored | builder-input | High | Builder input is ignored/untracked. |
| dice-texture | `Assets/Dices/D6_mine.png` | `DicePrefabBuilder` | texture path | Dice source texture | yes foreground | no | yes | 1 PNG | 1536x1024 | ignored | builder-input | High | Required by prefab builder, ignored. |
| dice-faces | `Assets/Textures/DiceFaces/face*.png` | `DicePrefabBuilder` | texture paths | Dice face textures | yes | yes | no | 6 PNG | not recorded | tracked | builder-input | Low | Builder inputs tracked. |
| mahjong-db | `Assets/Mahjong/MahjongTileSprites.asset` | `MahjongBattleSceneBuilder`, `MahjongBattleController` | ScriptableObject path/reference | Tile sprite database | yes | yes, modified | no | 1 asset | n/a | tracked | scriptableobject-runtime | Medium | Dirty foreground asset. |
| mahjong-red-fives | `Assets/Mahjong/m_5_red.png`, `p_5_red.png`, `s_5_red.png` | `MahjongTileSpriteDatabase` | serialized/editor fallback paths | Red-five tile sprites | yes foreground | no | no | 3 PNG | 281x399, 568x797, 523x726 | untracked | scriptableobject-runtime | Medium | Referenced art is untracked. |
| mahjong-table | `Assets/Mahjong/Table.png` | `MahjongBattleSceneBuilder` | asset path | Mahjong table UI | yes | yes | no | 1 PNG | 1500x273 | tracked | builder-input | Low | Direct builder input. |
| audio-catalog | `Assets/Se/True 8-bit Sound Effect Collection - Lite` | `AudioManager`, `SceneBuilderUtility` | folder/path lookup | Sound effects | yes | yes | no | 105 tracked files under group | n/a | tracked | runtime-current | Low | Direct audio catalog. |
| audio-dice-roll | `Assets/Se/DiceRoll_WakuWaku.wav` | `SceneBuilderUtility` | audio clip path | Dice roll sound | yes | yes | no | 1 WAV | n/a | tracked | runtime-current | Low | Direct clip reference. |
| holdem-cards | `Assets/Holdem/Sprites/Cards` | foreground untracked Holdem builder | asset path | Holdem card prototype | yes foreground | no | no | 58 recursive PNG | 240x336 sample | untracked | ambiguous | High | Not routed by tracked runtime. |
| holdem-community-mat | `Assets/Holdem/Sprites/holdem_community_mat.png` | foreground untracked Holdem builder | asset path | Holdem prototype table | no | no | no | 0 | n/a | missing | missing-reference | High | Foreground prototype reference missing. |

## Excluded Unreferenced Assets

Many foreground assets under `Assets/Mobs/**`, `Assets/Dices/**`, `Assets/Holdem/**`, and `SpritePipelineWork/**` were not audited in detail because no current runtime/builder/stage/pipeline owner was confirmed for them. They remain count-only inventory unless referenced later.

## Key Risks From Asset Scan

- Clean commit has no tracked scene files but runtime and build settings depend on scene names.
- Dice prefab inputs are ignored/untracked.
- Some stage runtime paths point to untracked folders or folders with zero direct PNG frames.
- Mahjong red-five sprites are referenced but untracked.
- Foreground map and Holdem assets are untracked and not clean-validation evidence.
- Several asset paths are dirty in the foreground, so visual state may differ from clean validation.
