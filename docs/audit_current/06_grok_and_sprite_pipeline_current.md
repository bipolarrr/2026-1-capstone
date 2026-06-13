# Grok And Sprite Pipeline Current

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: Grok prompt/provenance docs, sprite pipeline README/scripts, `SpritePipelineWork/**` manifests/reports/selection/promotion artifacts, hard-coded runtime asset owners.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no for implementation. Exceptions: Grok/provenance docs are used as asset provenance and pipeline intent evidence.

## Grok / Provenance Document Inventory

| Document | Status | Classification | Current use |
|---|---|---|---|
| `docs/grok-imagine-sprite-prompts.md` | tracked | current-provenance | Prompt/provenance checklist for Grok-generated sprite still/video work. |
| `docs/grok_generation_queue.md` | foreground untracked | current-provenance / partially-current | Manual queue and policy notes; claims must be checked against actual assets. |
| `docs/grok_manual_generation_packet.md` | foreground untracked | current-provenance | Manual packet for Grok prompt handoff. |
| `docs/grok_source_still_checklist.md` | foreground untracked | current-provenance | Source still acceptance checklist. |
| `docs/assets.md` | tracked, modified | partially-current pipeline doc | Asset dependency notes, but not implementation source of truth. |
| `docs/asset_acceptance_contract.md` | foreground untracked | pipeline doc | Acceptance rules for candidate/runtime assets. |
| `docs/asset_audit_manifest.md` | foreground untracked | pipeline/drift doc | Asset status claims, some stale against current scan. |

## Pipeline Script Inventory

Only `tools/sprite_pipeline/README.md` is tracked. The Python scripts below exist in the dirty foreground checkout and were audited read-only plus `--help` only.

| Script path | Purpose | Inputs | Outputs | Destructive risk | Default output root | Refuses `Assets/**`? | Uses AI/upscale/interpolation? | Safe audit command |
|---|---|---|---|---|---|---|---|---|
| `tools/sprite_pipeline/extract_frames.py` | Extract frames from source video | video path, asset id, fps/range options | `raw_frames` | Low if output outside project assets | `SpritePipelineWork/<asset_id>/raw_frames` | Help/README indicate no `Assets/**` default | no | `python ... --help` run |
| `tools/sprite_pipeline/remove_background.py` | Remove/copy alpha/background from frames | frame folder, method options | `transparent_frames` | Medium if output misconfigured | `SpritePipelineWork/<asset_id>/transparent_frames` | yes by policy/help | no AI by default; supports rembg/chroma/alpha-copy behavior | `python ... --help` run |
| `tools/sprite_pipeline/analyze_frames.py` | Analyze dimensions, alpha, crop, frame metrics | frame folder | CSV/JSON metrics | Low | pipeline work folder | n/a | no | `python ... --help` run |
| `tools/sprite_pipeline/build_review_packet.py` | Build contact sheets, timeline/GIF, candidate spans | frame/metrics folders | review packet files | Low | pipeline work folder | n/a | no | `python ... --help` run |
| `tools/sprite_pipeline/apply_human_selection.py` | Apply selected frame ranges | human selection/review data | selected frame folders and copy plan | Medium | pipeline work folder | yes | no | `python ... --help` run |
| `tools/sprite_pipeline/upscale_runtime_candidate.py` | Build upscaled runtime candidates | selected/candidate frames | upscaled runtime candidate folders | Medium | `SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/<backend>` | yes by help/policy | yes, waifu2x backend by default | `python ... --help` run |
| `tools/sprite_pipeline/promote_selected_asset.py` | Promote approved selected frames into runtime asset path | selected frames, inferred/runtime target, approval flags | copies into `Assets/**`, backups, promotion manifest | High | writes target runtime path on `--commit` | no on commit because purpose is promotion | no | `python ... --help` run only |
| `tools/sprite_pipeline/batch_ai_assetize_480p_videos.py` | Batch process 480p source videos into candidates or missing runtime assets | source videos/manifests | batch reports, candidates, optional runtime writes | High when `--write-missing-assets` is used | `SpritePipelineWork/batch_480p_ai_assetization` | refuses unsafe work output under `Assets`; can write missing runtime assets with explicit flag | yes, AI/upscale pipeline | source/help read only |
| `tools/sprite_pipeline/build_lowres_selected_source.py` | Prepare low-resolution selected source material | selected frames | low-res source assets | Low/Medium | pipeline work folder | not promoted by default | no | source/help read only |
| `tools/sprite_pipeline/build_upscale_human_review_packet.py` | Prepare review packet for upscale candidates | upscale candidates | review packet | Low | pipeline work folder | n/a | supports upscale review | source/help read only |
| `tools/sprite_pipeline/compare_lowres_upscale_candidates.py` | Compare low-res and upscaled candidates | candidate folders | comparison metrics | Low | pipeline work folder | n/a | evaluates upscales | source/help read only |
| `tools/sprite_pipeline/compare_upscale_candidates.py` | Compare upscale candidates | candidate folders | comparison metrics | Low | pipeline work folder | n/a | evaluates upscales | source/help read only |
| `tools/sprite_pipeline/alpha_policy_regression.py` | Regression check for alpha policy | image/candidate folders | policy report | Low | pipeline work folder | n/a | no | source/help read only |

No image processing, extraction, background removal, upscaling, generation, promotion, or asset writing command was run during this audit.

## SpritePipelineWork Artifact Inventory

Tracked subset:

- `SpritePipelineWork/asset_completion_manifest.json`
- `SpritePipelineWork/goblin_attack/promotion_result.md`
- `SpritePipelineWork/goblin_attack/selected/unity_copy_plan.md`

Foreground untracked work areas include:

- `SpritePipelineWork/batch_480p_ai_assetization/**`
- `SpritePipelineWork/goblin_attack/**`
- `SpritePipelineWork/goblin_hit/**`
- `SpritePipelineWork/slime_attack/**`
- `SpritePipelineWork/slime_hit/**`
- `SpritePipelineWork/skeleton_dead/**`
- `SpritePipelineWork/golem_*`
- `SpritePipelineWork/bat_*`
- `SpritePipelineWork/elemental_*`
- `SpritePipelineWork/enemy_dice_*`
- `SpritePipelineWork/mahjong_red_dora_review/**`

Current manifest result:

- `SpritePipelineWork/asset_completion_manifest.json` has version 1 and an empty `assets` map.
- Therefore no asset is currently approved complete by that manifest.

Important provenance artifacts:

- `SpritePipelineWork/goblin_attack/selected/unity_copy_plan.md` says the selected output is review-only and should not be copied to `Assets/**` until a separate approved runtime import task.
- `SpritePipelineWork/goblin_attack/promotion_result.md` records a prior promotion that copied 50 frames into `Assets/Mobs/Sprites/Goblin/Attack`, overwrote 50 frames, and removed stale PNG/meta files under a backup. This is provenance of past work, not a command run during this audit.
- `SpritePipelineWork/batch_480p_ai_assetization/batch_assetization_report.md` records one probable 480p video, missing runtime state, one asset written, human review needed, and no forbidden paths touched in that recorded batch.

## Source / Intermediate / Candidate / Runtime Classification

| Class | Current evidence | Audit classification |
|---|---|---|
| Source | Grok prompt docs, manual packet/checklist, source still/video references | source/provenance |
| Intermediate | `raw_frames`, `transparent_frames`, metrics, contact sheets under `SpritePipelineWork/**` | intermediate/review |
| Candidate | selected frames, upscaled candidates, comparison packets | candidate/needs human review unless manifest says approved |
| Runtime-current | assets referenced by stage data/builders/controllers under `Assets/**` | runtime-current only when owner reference exists |
| Approved | Empty `asset_completion_manifest.json` gives no current approved-complete entries | none by manifest |
| Rejected | Human review/rejection data exists in work folders but was not exhaustively classified | ambiguous unless specific manifest says rejected |
| Ambiguous | Untracked foreground art referenced by runtime but lacking approved manifest | ambiguous runtime dependency |

## 480p / Grok Source Policy

Observed policy expression:

- Grok docs and queue files describe manual Grok generation outside `Assets/**` until reviewed.
- Pipeline README states default outputs should stay under `SpritePipelineWork/<asset_id>/`.
- Batch 480p script supports candidate-only mode and a separate `--write-missing-assets` mode.
- Promotion requires explicit `promote_selected_asset.py --commit` and writes a promotion result/backup.

Current risk:

- Some foreground runtime paths appear to have already received untracked/generated assets.
- The empty completion manifest means approval state cannot be inferred globally.

## Runtime Path Owners And Pipeline Candidate Relationship

Hard-coded runtime owners:

- `SceneBuilderUtility` owns player sprite roots, fallback sprites, UI heart/font, audio, and some projectile/fallback paths.
- `Stage1Forest` owns Stage 1 mob/background/boss sprite paths.
- `Stage2Cave` owns Stage 2 mob/background/VFX paths.
- Scene builders own dice, Mahjong, UI, story, and generated scene paths.
- Foreground untracked Holdem builder owns `Assets/Holdem/**` prototype card/table paths.

Pipeline candidate paths are not runtime-current unless copied/promoted into one of these owned `Assets/**` paths and then referenced by runtime/builders/stage data.

## Non-Destructive Status

Non-destructive by default:

- Extraction/review/analysis/selection/upscale review commands that write only under `SpritePipelineWork/**`.

Potentially destructive or runtime-mutating:

- `promote_selected_asset.py --commit`
- `batch_ai_assetize_480p_videos.py --write-missing-assets`
- Any command writing generated frames directly under `Assets/**`
- Any command deleting stale PNG or `.meta` files

These were not run.

## Current Pipeline Conclusions

1. The pipeline is usable as a review/candidate workflow when kept under `SpritePipelineWork/**`.
2. Promotion into runtime assets is a separate high-risk task requiring explicit approval.
3. Existing pipeline docs are useful provenance but not sufficient proof that an asset is current runtime-approved.
4. Some runtime-referenced assets are untracked or dirty foreground outputs, so asset provenance and repository tracking need reconciliation.
