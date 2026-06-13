# Sprite Pipeline Review Tools

These tools create review artifacts only. They do not import assets into Unity, do not write under `Assets/` by default, do not edit `.meta` files, and do not promote processed frames to runtime folders.

Default work root:

```powershell
SpritePipelineWork/<asset_id>/
```

Expected outputs:

- `raw_frames/`
- `transparent_frames/`
- `metrics.csv`
- `metrics.json`
- `contact_sheet_all.png`
- `contact_sheet_candidates.png`
- `timeline_metrics.png`
- `preview_all.gif`
- `preview_candidate_01.gif`
- `candidate_spans.json`
- `human_selection.yaml`

## 1. Extract Frames

From an MP4:

```powershell
python tools/sprite_pipeline/extract_frames.py --asset-id goblin_hit --source Assets/Mobs/Sprites/Goblin/Hit/Goblin_Hit.mp4
```

From an existing image sequence:

```powershell
python tools/sprite_pipeline/extract_frames.py --asset-id goblin_attack --source Assets/Mobs/Sprites/Goblin/Attack
```

The output is `SpritePipelineWork/<asset_id>/raw_frames/`.

## 2. Remove Background

For rembg:

```powershell
python tools/sprite_pipeline/remove_background.py --asset-id goblin_hit --method rembg
```

For a flat magenta Grok background:

```powershell
python tools/sprite_pipeline/remove_background.py --asset-id slime_attack --method chroma --chroma-color magenta --tolerance 36
```

For frames that already have alpha:

```powershell
python tools/sprite_pipeline/remove_background.py --asset-id skeleton_idle --method alpha-copy
```

The output is `SpritePipelineWork/<asset_id>/transparent_frames/`.

## 3. Analyze Frames

```powershell
python tools/sprite_pipeline/analyze_frames.py --asset-id goblin_hit --fps 24
```

This writes `metrics.csv` and `metrics.json`.

Metrics include:

- `frame_index`
- `source_time`
- `width`
- `height`
- `alpha_present`
- `alpha_bbox`
- `bbox_area_ratio`
- `bbox_center`
- `bbox_center_delta`
- `bbox_width_delta`
- `bbox_height_delta`
- `opaque_pixel_count`
- `edge_touch_flags`
- `component_count`
- `previous_frame_diff`
- `blank_frame`
- `crop_risk`
- `scale_jump`
- `center_jump`
- `likely_background_residue`
- `duplicate_or_hold_candidate`

## 4. Build Review Packet

```powershell
python tools/sprite_pipeline/build_review_packet.py --asset-id goblin_hit --usage-type hit_reaction
```

This creates contact sheets, GIF previews, timeline metrics, `candidate_spans.json`, and a default `human_selection.yaml`.

Candidate spans are suggestions only. The tool proposes 1-3 spans and labels frames as:

- `idle_pre_roll`
- `active_action`
- `recovery`
- `unstable_tail`
- `rejected`

Allowed usage types:

- `idle_loop`
- `one_shot_attack`
- `hit_reaction`
- `death_once_hold_last`
- `jump_once`
- `debuff_loop`
- `static_placeholder`
- `reject_regenerate`

## 5. Apply Human Selection For Review Export

Edit `SpritePipelineWork/<asset_id>/human_selection.yaml` after visual review. Set:

```yaml
selected_candidate_id: candidate_01
selected_frame_start: 0
selected_frame_end: 68
usage_type: hit_reaction
approved_for_review_export: true
```

Then run:

```powershell
python tools/sprite_pipeline/apply_human_selection.py --asset-id goblin_hit
```

This copies frames only to `SpritePipelineWork/<asset_id>/selected/`. It refuses `Assets/` output and does not create Unity-ready runtime files.

## 6. Build Upscaled Runtime Candidates

Create a non-destructive default AI upscale candidate from selected, transparent, or lowres source frames:

```powershell
$env:WAIFU2X_NCNN_VULKAN_EXE="C:\tools\waifu2x-ncnn-vulkan\waifu2x-ncnn-vulkan.exe"

python tools/sprite_pipeline/upscale_runtime_candidate.py `
  --asset-id goblin_attack `
  --input-dir SpritePipelineWork/goblin_attack/lowres_selected_source/frames `
  --reference-dir Assets/Mobs/Sprites/Goblin/Attack
```

If `--backend` and `--method` are omitted, the tool uses the default backend: `waifu2x` (`waifu2x-ncnn-vulkan`).

AI executable lookup order:

1. `--ai-upscaler-exe <path>`
2. `WAIFU2X_NCNN_VULKAN_EXE`
3. Existing local untracked candidates:
   - `tools/external/waifu2x-ncnn-vulkan/waifu2x-ncnn-vulkan.exe`
   - `tools/external/waifu2x-ncnn-vulkan/waifu2x-ncnn-vulkan`

The tool does not create `tools/external/**`. If no executable is found, it fails with:

```text
GPU AI upscaler executable을 찾지 못했습니다. waifu2x-ncnn-vulkan 설치 또는 WAIFU2X_NCNN_VULKAN_EXE 설정이 필요합니다.
```

The AI stage runs integer-scale upscaling first, then normalizes output frames to the target canvas. Target canvas is resolved in this order:

1. `--target-width` plus `--target-height`
2. `--reference-image`
3. `--reference-dir` PNG frame audit

Real output, when not using `--dry-run`, is restricted to:

```text
SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/waifu2x/
```

The result contains final candidate frames and reports:

```text
SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/waifu2x/
  frames/
  upscale_candidate_report.json
  upscale_candidate_report.md
  review/
    ai_upscale_contact_sheet.png
    ai_upscale_review_report.json
    ai_upscale_review_report.md
```

AI temp output is removed after normalization unless `--keep-ai-temp` is provided.

The tool supports explicit fallback/debug/comparison backends:

```powershell
python tools/sprite_pipeline/upscale_runtime_candidate.py `
  --asset-id goblin_attack `
  --input-dir SpritePipelineWork/goblin_attack/lowres_selected_source/frames `
  --reference-dir Assets/Mobs/Sprites/Goblin/Attack `
  --backend nearest
```

Legacy `--method nearest|lanczos|bicubic` still works as an alias. Fallback backends are never used automatically when the AI executable is missing.

The tool refuses `Assets/` output, does not modify existing runtime sprites, and does not promote runtime assets. Runtime promotion is a separate task.

Default-first operating rule:

1. Run the default AI backend only.
2. Generate/review the default contact sheet or report.
3. If the default AI result passes, stop.
4. If the default AI result fails or is ambiguous, do not automatically run Real-ESRGAN, waifu2x parameter sweeps, `bicubic`, `lanczos`, `nearest`, or other upscalers.

Use this exact message in the report/final output when the default AI result fails or is ambiguous:

```text
기본 AI 업스케일 결과가 불합격 또는 애매합니다. 여러 다른 방법을 시도해보는 별도 비교 작업으로 넘어가야 합니다.
```

Allowed follow-up suggestions are Real-ESRGAN-ncnn-vulkan, waifu2x noise/scale/tile parameter adjustment, `bicubic`, `lanczos`, `nearest`, asset-specific override, or another AI upscaler if needed. Those are separate tasks, not part of the default flow.

## 7. Batch AI Assetize Unapproved 480p Videos

Batch inventory and approval-manifest based candidate generation are handled by:

```text
tools/sprite_pipeline/batch_ai_assetize_480p_videos.py
```

The batch default backend is `waifu2x` through the external `waifu2x-ncnn-vulkan` executable. The executable and model files are not stored in this repo.

The batch skip rule is approval-manifest based:

- Existing runtime PNGs under `Assets/**` are not a skip condition.
- Existing runtime PNGs are reference canvas and visual comparison material.
- A video is skipped only when `SpritePipelineWork/asset_completion_manifest.json` has a valid human-approved complete asset record.
- If no approved manifest record exists, existing, missing, and partial/ambiguous runtime states are all candidate-generation targets.
- Processed assets are reported with `approvalSuggestion: needs_human_review`; the batch does not auto-approve them.

Set the executable path with an environment variable:

```powershell
$env:WAIFU2X_NCNN_VULKAN_EXE="C:\tools\waifu2x-ncnn-vulkan\waifu2x-ncnn-vulkan.exe"
```

Dry-run inventory/classification:

```powershell
python tools\sprite_pipeline\batch_ai_assetize_480p_videos.py `
  --scan-root Assets `
  --work-root SpritePipelineWork `
  --dry-run
```

Dry-run also reports approved/missing/existing/unapproved classification. Existing PNGs with no approved manifest record appear as `process_unapproved`.

Actual candidate-only batch:

```powershell
python tools\sprite_pipeline\batch_ai_assetize_480p_videos.py `
  --scan-root Assets `
  --work-root SpritePipelineWork
```

Default execution is candidate-only and does not write under `Assets/**`. Review packets must be checked by a human before approval or promotion.

Missing-only PNG creation:

```powershell
python tools\sprite_pipeline\batch_ai_assetize_480p_videos.py `
  --scan-root Assets `
  --work-root SpritePipelineWork `
  --write-missing-assets
```

Optional explicit executable path:

```powershell
python tools\sprite_pipeline\batch_ai_assetize_480p_videos.py `
  --scan-root Assets `
  --work-root SpritePipelineWork `
  --ai-upscaler-exe C:\tools\waifu2x-ncnn-vulkan\waifu2x-ncnn-vulkan.exe `
  --dry-run
```

Batch outputs are written under:

```text
SpritePipelineWork/batch_480p_ai_assetization/
```

Per-asset work outputs use:

```text
SpritePipelineWork/<assetId>/batch_480p_assetization/
```

Safety rules:

- `--dry-run` does not create PNG assets under `Assets/**`.
- `--write-missing-assets` is required for any new `Assets/**` PNGs.
- Existing sprite folders are processed as candidates when unapproved, but are never overwritten.
- `--write-missing-assets` still writes only when runtime state is `missing`, target canvas is resolved, quality gate passes, and the target folder has no PNGs.
- `--overwrite-work` only permits replacing this batch's `SpritePipelineWork/**` outputs.
- `--approval-manifest <path>` defaults to `SpritePipelineWork/asset_completion_manifest.json`.
- `--skip-approved` is enabled by default; `--force-reprocess-approved` regenerates candidates for approved records without runtime overwrite.
- `--candidate-only` is the default mode.
- `.meta` files are not manually created or modified.
- If the default AI result fails or is ambiguous, the batch records the failure and does not automatically run other backends.

## Regenerate Review Outputs

To regenerate work outputs, pass `--overwrite-work` to the step being rerun. This only replaces files in `SpritePipelineWork`, not source assets:

```powershell
python tools/sprite_pipeline/extract_frames.py --asset-id goblin_hit --source Assets/Mobs/Sprites/Goblin/Hit/Goblin_Hit.mp4 --overwrite-work
python tools/sprite_pipeline/remove_background.py --asset-id goblin_hit --method rembg --overwrite-work
python tools/sprite_pipeline/analyze_frames.py --asset-id goblin_hit --fps 24
python tools/sprite_pipeline/build_review_packet.py --asset-id goblin_hit --usage-type hit_reaction
```

## Safety Notes

- Do not use these tools to write into `Assets/`.
- Do not edit `.meta` files.
- Do not treat `selected/` or `upscaled_runtime_candidates/` as runtime-ready output.
- Do not import or promote frames until a separate human-approved Unity asset task.
- For shortened runtime folders, use the review packet to decide whether the source sequence should stay trimmed, be rebuilt, or be rejected.
