# Repository Inventory Current

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: `git ls-files`, foreground filesystem scan, `git status`, `git check-ignore`, runtime/editor code references, pipeline/provenance files.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: AGENTS safety rules, Grok/provenance documents, and markdown drift comparison only.

## Inventory Rules

Files were included as current evidence only when they were part of a runtime flow, scene/build generation path, Unity validation path, direct asset reference, or sprite pipeline/provenance path. Existing documentation was not used to prove implementation state.

Unreferenced folders were counted but not treated as current runtime evidence.

## Scan Summary

| Path group | Tracked files | Foreground files | Inventory classification | Notes |
|---|---:|---:|---|---|
| `Assets/Scripts` | 171 | 221 | runtime-current plus foreground-only additions | Runtime controllers, stage data, battle logic, debug/audio. Holdem files are foreground untracked. |
| `Assets/Editor` | 49 | 72 | builder/test/source-generation evidence | Scene builders and utilities are current evidence; untracked Holdem/AnimationDebug/incremental builder files are foreground-only. |
| `Assets/Editor/Tests` | 34 | 49 | validation evidence | Clean validation ran tracked tests at HEAD. Foreground extra tests were not validated. |
| `Assets/Scenes` | 0 | 14 | generated scene artifacts, not tracked in HEAD | Foreground scene files exist but are untracked or ignored. |
| `Assets/Dices` | 0 | 944 | builder-input, ignored foreground assets | Dice prefab/art inputs are ignored in foreground. |
| `Assets/Mahjong` | 42 | 50 | runtime-current plus untracked referenced tile art | Tile database is tracked but modified; red-five sprites are untracked. |
| `Assets/Mobs` | 3605 | 5971 | stage-data-runtime plus untracked/generated candidates | Several Stage2 and Slime/Skeleton runtime paths are untracked. |
| `Assets/Player` | 8013 | 8013 | runtime-current | Player animation folders are heavily referenced by `SceneBuilderUtility`. |
| `Assets/Backgrounds` | 4 | 4 | stage-data-runtime | Stage backgrounds. |
| `Assets/UI` | 10 | 23 | runtime-current plus untracked map assets | Heart/logo/background tracked; map icons foreground untracked. |
| `Assets/Se` | 105 | 105 | runtime-current | Audio manager and builder sound references. |
| `Assets/Settings` | 9 | 9 | validation/build settings | Unity modified settings in validation worktree. |
| `ProjectSettings` | 26 | 26 | build/runtime settings | Build settings are dirty in foreground and mutated in validation. |
| `Packages` | 2 | 2 | package configuration | `manifest.json` and `packages-lock.json`. |
| `tools/sprite_pipeline` | 1 | 14 | pipeline evidence | README tracked; scripts foreground untracked but audited by source/help only. |
| `SpritePipelineWork` | 3 | 8697 | pipeline provenance/candidate evidence | Manifests/reports/contact sheets/candidates. |
| `docs` | 3 | 30 | operating/provenance/drift evidence only | Existing docs are not implementation source of truth. |

## Tracked / Untracked / Ignored Distinction

- Tracked evidence came from `git ls-files` and clean validation worktree scans.
- Foreground untracked evidence was recorded when directly referenced by current foreground code/build settings/assets or when it was pipeline provenance.
- Ignored evidence was recorded only when a runtime or builder path references it, such as `Assets/Dices/**` and `Assets/Scenes/**`.

Notable ignored runtime/build paths:

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/CharacterSelect.unity`
- `Assets/Scenes/DiceBattleScene.unity`
- `Assets/Scenes/GameExploreScene.unity`
- `Assets/Scenes/MahjongBattleScene.unity`
- `Assets/Dices/D6_mine.png`
- `Assets/Dices/Prefabs/Dice_d6_mine.prefab`
- `Assets/Dices/Prefabs/Dice_d6.prefab`
- `SpritePipelineWork/**` generated frames/contact sheets

Notable untracked runtime/build paths:

- `Assets/Scenes/HoldemBattleScene.unity`
- `Assets/Editor/HoldemBattleSceneBuilder.cs`
- `Assets/Scripts/Holdem/**`
- `Assets/Holdem/**`
- `Assets/UI/MapIcons/**`
- `Assets/Mahjong/m_5_red.png`, `p_5_red.png`, `s_5_red.png`
- `Assets/Mobs/Sprites/Golem/InGame/**`
- `Assets/Mobs/Sprites/Elemental/WaterCannon/WaterCannon.png`

## Runtime / Build Evidence Included

Included as current evidence:

- Scene transitions in `Assets/Scripts/**`
- Session state in `GameSessionManager`
- Explore and stage flow in `GameExploreController`, `StageRegistry`, `StageData`, `Stage1Forest`, `Stage2Cave`
- Dice battle runtime under `Assets/Scripts/Battle/**`, dice helpers, score/damage calculators
- Mahjong battle runtime under `Assets/Scripts/Mahjong/**`
- Debug console and audio manager runtime
- Scene builders under `Assets/Editor/*SceneBuilder.cs`
- Builder utilities under `Assets/Editor/SceneBuilderUtility.cs`
- Dice prefab builder under `Assets/Editor/DicePrefabBuilder.cs`
- Build settings from clean HEAD and dirty foreground `ProjectSettings/EditorBuildSettings.asset`
- Unity validation XML/logs from the detached validation worktree

## Runtime Asset Evidence Included

Included because code, stage data, builders, ScriptableObject assets, or scene/build references point to them:

- Player sprite roots under `Assets/Player/Sprites/**`
- Stage backgrounds under `Assets/Backgrounds/**`
- Mob sprites referenced by `Stage1Forest` and `Stage2Cave`
- Dice prefab/art/material/physics/texture references used by scene builders
- Mahjong tile database and tile sprites
- UI heart/logo/background/map icon references
- Audio references under `Assets/Se/**`
- Generated scene paths under `Assets/Scenes/**`

## Pipeline Evidence Included

Included as asset generation/provenance evidence:

- `docs/grok-imagine-sprite-prompts.md`
- `docs/grok_generation_queue.md`
- `docs/grok_manual_generation_packet.md`
- `docs/grok_source_still_checklist.md`
- `docs/assets.md`
- `docs/asset_acceptance_contract.md`
- `docs/asset_audit_manifest.md`
- `tools/sprite_pipeline/**`
- `SpritePipelineWork/**` manifests, reports, selected plans, promotion reports, contact metadata

## Excluded Or Count-Only Evidence

The following were not audited in detail as current runtime evidence unless directly referenced:

- Unreferenced asset folders whose only evidence is folder existence.
- Historical QA and old audit conclusions.
- Backlog estimates and planned feature descriptions.
- Ignored generated image sequences without runtime owner or pipeline provenance.
- Untracked local files not connected to code, stage data, builders, or pipeline provenance.

## Confidence Notes

- High confidence: code references, build settings, validation XML/logs, direct asset path existence.
- Medium confidence: foreground untracked files referenced by dirty foreground code or assets but absent from clean validation.
- Low confidence: file/folder existence without direct runtime owner.
- Unknown: manual playability and visual correctness of generated/untracked scenes and assets.
