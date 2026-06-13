# Sprite Upscale Pipeline Plan

Date: 2026-05-27

Scope: investigation and design only. No assets were moved, deleted, renamed, reimported, upscaled, promoted, or edited.

## 1. Executive summary

The repository now has a tracked review-oriented sprite pipeline under `tools/sprite_pipeline/`. It can extract frames into `SpritePipelineWork/<asset_id>/raw_frames`, remove or preserve background into `transparent_frames`, analyze frame metrics, build review packets, export human-selected review frames, create non-destructive upscale candidates, and optionally dry-run promotion planning. The default upscale candidate backend is now GPU AI upscaling through `waifu2x-ncnn-vulkan`; Pillow `nearest`, `lanczos`, and `bicubic` remain explicit fallback/debug/comparison backends only.

The current asset tree contains mixed categories in place: MP4 Grok outputs under `Assets/Player/Sprites/**` and `Assets/Mobs/Sprites/**`, raw extracted frame folders, `_transparent`, `_transparent_clean`, `_764x640`, `_650x640`, `_1000x1000`, runtime-referenced direct folders, and `SpritePipelineWork/**` review outputs. Current runtime and builder references are path-string based, mostly through `SceneBuilderUtility` player constants and `Stage1Forest`/`Stage2Cave` mob definitions.

480p should become the canonical new Grok Imagine input because the 720p generation quota is too limited for current production. Existing 720p-era assets must not be treated as obsolete. They remain valid source-of-truth candidates or current Unity-ready runtime sprites until a manifest and an explicit promotion task says otherwise.

Coexistence principle: keep existing 720p/direct runtime paths stable; generate 480p-derived upscale candidates outside runtime paths first; only change runtime/builder paths in a separate explicit promotion/reference task after visual review, manifest update, and Unity import/reference validation.

## 2. Existing scripts inventory

| Script | Purpose inferred | Inputs | Outputs | Destructive? | Uses interpolation/model? | Safe to reuse? | Notes |
|---|---|---|---|---|---|---|---|
| `tools/sprite_pipeline/extract_frames.py` | Extract numbered PNG frames from MP4 or copy an image sequence into review work. | `--source` MP4 or image folder, `--asset-id`. | `SpritePipelineWork/<asset_id>/raw_frames/*.png`, `extraction_manifest.json`. | Low in review work: refuses `Assets/`; with `--overwrite-work` unlinks existing files in the target work output directory but refuses nested directory deletion. | Uses OpenCV for video decode and Pillow for RGBA image save. No upscale interpolation. | Yes for 480p raw input ingestion. | Captures video width/height/fps in manifest. |
| `tools/sprite_pipeline/remove_background.py` | Remove or preserve frame backgrounds into transparent review output. | `raw_frames` by default or `--input-dir`; methods `rembg`, `chroma`, `alpha-copy`. | `SpritePipelineWork/<asset_id>/transparent_frames/*.png`, `background_removal_manifest.json`. | Low in review work: refuses `Assets/`; with `--overwrite-work` unlinks existing files in `transparent_frames`. | Uses `rembg` model when requested, numpy chroma alpha masking, or alpha threshold copy. No upscale interpolation. | Yes for review-stage transparency. | Good fit before/after upscale experiments, but alpha-edge behavior must be compared visually. |
| `tools/sprite_pipeline/analyze_frames.py` | Compute frame metrics for review and candidate selection. | `transparent_frames` by default, fallback to `raw_frames`, optional `--frames-dir`. | `metrics.csv`, `metrics.json` in `SpritePipelineWork/<asset_id>/`. | Low: writes/overwrites metrics in work root; refuses writes under `Assets/`. | Uses OpenCV connected components and Pillow image reads. No upscale interpolation. | Yes. | Useful for detecting crop risk, edge contact, center jump, scale jump, blank frames, and residue. |
| `tools/sprite_pipeline/build_review_packet.py` | Generate review contact sheets, GIFs, timeline metrics, candidate spans, and default human selection. | Frame directory plus `metrics.json`; `--usage-type`. | Review artifacts in `SpritePipelineWork/<asset_id>/`, including contact sheets, previews, `candidate_spans.json`, `human_selection.yaml`. | Low: writes review outputs in work root and refuses `Assets/`. | Uses Pillow `thumbnail(..., Image.Resampling.LANCZOS)` only for review thumbnails/GIFs. | Yes for visual review. | LANCZOS here is not evidence of a production upscale policy. |
| `tools/sprite_pipeline/apply_human_selection.py` | Export approved frame spans into review-only selected frames. | `human_selection.yaml`, `transparent_frames` by default then `raw_frames`. | `SpritePipelineWork/<asset_id>/selected/*.png`, `selected_preview.gif`, `selected_contact_sheet.png`, `selected_manifest.json`, `unity_copy_plan.md`. | Low to medium in work root: refuses `Assets/`; with `--overwrite-work` unlinks existing files in selected output. | Uses Pillow `thumbnail(..., Image.Resampling.LANCZOS)` for review sheets/GIFs; selected PNGs are copied/saved without upscale. | Yes for review export. | README says `selected_frames/`, but implementation uses `selected/`; update docs in a later cleanup task. |
| `tools/sprite_pipeline/promote_selected_asset.py` | Promote human-approved selected frames into an inferred runtime folder. | `SpritePipelineWork/<asset_id>/selected`, `--visual-status`, `--dry-run` or `--commit`. | Dry run: `destination_resolve_report.md`. Commit: copies frames into `Assets/**`, creates backup under `SpritePipelineWork`, writes promotion report/manifest. | High when `--commit`: overwrites runtime PNGs, removes stale PNGs and stale `.png.meta` files, creates/updates files under `Assets/`. Dry run is safe. | Uses Pillow only for validation of dimensions/alpha. No upscale interpolation. | Dry-run only for this policy phase; do not use `--commit` until a separate promotion task. | Existing resolver prefers exact runtime-referenced action roots, then runtime-referenced `_transparent_clean`/`_transparent`, then best clean candidate. This can conflict with non-destructive upscale coexistence if used too early. |

No repo script currently implements production upscaling, 480p-to-720p conversion, spritesheet generation, or AI model upscaling.

## 3. Existing asset categories

| Category | Actual examples found | Track/intermediate status |
|---|---|---|
| Source reference image | `Assets/Mobs/Goblin_for_grok.png` noted in audit; `Assets/Player/Sprites/Idle/0.png`; `Assets/Mobs/Sprites/Slime/Idle/0.png`; `Assets/Mobs/Sprites/Skeleton/Idle/0.png`. | Track candidate. Required for reproducible Grok I2V prompts and identity/scale consistency. |
| Grok-generated still image | `Assets/Mobs/Sprites/Slime/Idle/Slime_0.png`; `Assets/Player/Sprites/Weapon/Player_Weapon.png`; static mobs such as `Assets/Mobs/Enemy_Golem.png`. | Track candidate when used as runtime sprite or source still. Unknown when mixed into runtime folders without manifest. |
| Grok-generated video | `Assets/Player/Sprites/Attack/Player_Attack.mp4`, `Assets/Player/Sprites/Defense/Player_Defense.mp4`, `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack.mp4`, `Assets/Mobs/Sprites/Goblin/Hit/Goblin_Hit.mp4`, `Assets/Mobs/Sprites/Skeleton/Idle/Skeleton_Idle.mp4`. | Intermediate/source candidate. Keep until manifest decides whether MP4s are source of truth for regeneration. |
| Raw extracted frames | `Assets/Player/Sprites/Attack/Player_Attack/*.png`; `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack/*.png`; `SpritePipelineWork/goblin_attack/raw_frames/*.png`; `SpritePipelineWork/goblin_hit/raw_frames/*.png`. | Intermediate. Do not promote directly unless alpha/background state and runtime dimensions are approved. |
| Human-selected frames | `SpritePipelineWork/goblin_attack/selected/*.png`; `SpritePipelineWork/goblin_hit/selected/*.png`; `selected_manifest.json`; `unity_copy_plan.md`. | Intermediate review output. Not Unity-ready by itself. |
| Transparent output | `Assets/Player/Sprites/Attack/Player_Attack_transparent/*.png`; `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent/*.png`; `Assets/Mobs/Sprites/Bat/Hit/Bat_Hit_transparent/*.png`; `SpritePipelineWork/*/transparent_frames/*.png`. | Intermediate unless directly referenced by runtime. Current examples are mostly not final. |
| Transparent clean output | `Assets/Player/Sprites/Attack/Player_Attack_transparent_clean/*.png`; `Assets/Player/Sprites/Defense/Player_Defense_transparent_clean/*.png`; `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean/*.png`. | Track candidate. `Bat_Attack_transparent_clean` is currently runtime-referenced by stage data; others are clean variants but not necessarily runtime final. |
| Upscaled output | Existing naming shows size-normalized folders such as `Assets/Player/Sprites/Idle_764x640`, `Defense_764x640`, `Die_764x640`, `SmallHit_764x640`, `StrongHit/..._transparent_764x640`, and `..._650x640`, but no script/provenance proves these are systematic upscales. | Ambiguous. Treat as size-normalized variants, not canonical final, until manifest records method, source, and promotion status. |
| Unity-ready runtime sprite | `Assets/Player/Sprites/Idle`, `LowHp`, `Defense`, `SmallHit`, `StrongHit`, `Attack`, `Die/Player_Die_1000x1000`, `Weapon/Player_Weapon.png`; `Assets/Mobs/Sprites/Goblin/Idle`, `Goblin/Attack`, `Goblin/Hit`, `Goblin/Dead`; `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean`; `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png`. | Track candidate/current runtime dependency. Must preserve path and `.meta` unless a separate explicit import task changes them. |
| Unknown / ambiguous | Root action folders that contain both runtime frames and nested source/intermediate folders; `Assets/Player/Sprites/StrongHit/Player_StrongHit_20260511_230207*`; empty `*_764x640` folders; `SpritePipelineWork/goblin_hit/selected_alpha_repair`. | Do not delete. Manifest needed before cleanup. |

Representative dimensions observed by read-only inspection:

| Path | Dimensions |
|---|---:|
| `Assets/Player/Sprites/Idle/0.png` | `1000x1000` |
| `Assets/Player/Sprites/Idle_764x640/0.png` | `764x640` |
| `Assets/Player/Sprites/Attack/0.png` | `918x726` |
| `Assets/Player/Sprites/Attack/Player_Attack/0.png` | `960x960` |
| `Assets/Player/Sprites/Attack/Player_Attack_transparent_clean/0.png` | `918x630` |
| `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean/0.png` | `898x592` |
| `SpritePipelineWork/goblin_attack/raw_frames/0.png` | `928x960` |
| `SpritePipelineWork/goblin_hit/raw_frames/0.png` | `928x976` |

## 4. Resolution coexistence policy

1. New Grok Imagine canonical input resolution: `480p`. Record the exact source pixel dimensions from extraction manifest because Grok's 480p output can still produce non-16:9/cropped frame sizes after postprocess.

2. Existing 720p-era assets: keep as existing source/runtime candidates. Do not rename them to legacy or obsolete. If already runtime-referenced, they remain the active final until explicitly replaced.

3. New 480p upscaled result suffix: use a new explicit suffix that does not imply clean/final status by itself:
   - Review work: `SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/<method_id>/`
   - If a tracked candidate under `Assets/` is approved later but not promoted to runtime: `<BaseName>_480p_upscaled_<target>_<method>`
   - Example candidate folder: `Assets/Mobs/Sprites/Slime/Attack/Slime_Attack_480p_upscaled_runtime_nn`

4. Avoid collision with existing suffixes:
   - Keep `_transparent` for background-removed output.
   - Keep `_transparent_clean` for cleaned transparent output.
   - Keep existing `_764x640`, `_650x640`, `_1000x1000` as historical size-normalized variants.
   - Do not overload `_764x640` for new 480p-derived upscale output.
   - Use `_480p_upscaled_<target>_<method>` for new candidates until promotion.

5. Runtime/builder path changes:
   - Keep paths unchanged while generating and reviewing upscale candidates.
   - Change `SceneBuilderUtility` constants or `StageData` sprite folder paths only in a separate explicit promotion/reference task.
   - Prefer copying a final approved candidate into the currently referenced runtime folder only when the task explicitly allows asset writes and `.meta`/import validation.
   - If changing the runtime path to a new folder is better than in-place replacement, update every hard-coded owner and run isolated Unity validation.

6. Priority when existing 720p and new 480p-upscaled assets coexist for the same mob/action:
   - Current runtime-referenced folder wins for gameplay until promotion.
   - New 480p-upscaled candidate can be marked `candidate` or `approved_visual` in manifest but not `runtime_current`.
   - Promotion priority is: current runtime final, approved upscaled candidate with visual/contact-sheet approval, then source/intermediate variants for regeneration.
   - Never auto-promote based only on file existence or folder suffix.

7. Manifest fields needed to identify Unity-ready final quickly:
   - `promotionStatus`: e.g. `source_only`, `review_candidate`, `approved_visual`, `runtime_current`, `runtime_replaced`, `rejected`.
   - `unityReadyPath`: exact current final path if any.
   - `runtimeReferenceOwner`: e.g. `SceneBuilderUtility.PlayerAttack01SpriteFolder`, `Stage1Forest.Bat.attackSpriteFolderPath`.
   - `isRuntimeReferenced`: boolean.
   - `sourceResolution`, `targetRuntimeResolution`, `upscaleMethod`, `frameCount`, `hasAlpha`, `requiresMetaImport`.

Hard-coded runtime/reference constraints found:

| Owner | Referenced path examples |
|---|---|
| `Assets/Editor/SceneBuilderUtility.cs` | `Assets/Player/Sprites/Idle`, `LowHp`, `Jump`, `JumpBelow`, `Defense`, `SmallHit`, `StrongHit`, `Debuff`, `Die/Player_Die_1000x1000`, `DiceRoll`, `Attack`, `Weapon/Player_Weapon.png`, `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png`. |
| `Assets/Scripts/Stages/Stage1Forest.cs` | `Slime/Idle`, `Slime/Attack`, `Slime/Hit`, `Slime/Dead`; `Goblin/Idle`, `Goblin/Attack`, `Goblin/Hit`, `Goblin/Dead`; `Bat/Attack/Bat_Attack_transparent_clean`; `Skeleton/Attack`, `Skeleton/Hit`, `Skeleton/Dead`, projectile. |
| `Assets/Scripts/Stages/Stage2Cave.cs` | `Bat/Attack/Bat_Attack_transparent_clean`; `Goblin/Attack`, `Goblin/Hit`, `Goblin/Dead`; static `Assets/Mobs/Enemy_Golem.png` and `Assets/Mobs/Water_Elemental.png`. |
| Builders | Character select loads `SceneBuilderUtility.PlayerDiceRollSpriteFolder + "/0.png"`; battle/explore/mahjong builders load skeleton projectile path directly. |

## 5. Recommended upscale strategy

The existing scripts are reusable for extraction, background removal, metrics, review packets, human selection, and dry-run destination resolution. They are not sufficient for 480p-to-runtime upscaling because no production upscale step exists and `promote_selected_asset.py --commit` is intentionally destructive to runtime folders.

The non-destructive upscale wrapper writes only under `SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/<backend>/` by default and records backend, target canvas, alpha handling, quality gate status, and review artifacts in reports. Do not write directly under `Assets/` in the first upscale task.

| Candidate | Pixel art preservation | Outline/alpha preservation | Color bleed | Frame consistency | 480p to 720p non-integer issue | Unity import relation | Repeatability | Original preservation | Windows feasibility | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| waifu2x-ncnn-vulkan | Good fit for sprite/anime-style generated assets; can improve soft lowres source detail. | Alpha must be audited; if AI output alpha is missing or clearly damaged, recombine deterministic source alpha after target normalization. | Medium; contact sheet review still required. | Medium: model behavior may vary by version/GPU, so backend path and parameters must be recorded. | AI stage uses integer scale, then the pipeline normalizes to the runtime/reference target canvas. | Output still requires separate Unity import/reference validation before promotion. | Medium: executable/model version must be pinned outside the repo. | High if output path is separate. | Medium: requires local portable executable and Vulkan support. | Default backend for new candidate generation. |
| Real-ESRGAN-ncnn-vulkan | Can improve perceived detail but may over-sharpen or alter identity. | Same alpha risk as other AI tools; source-alpha recombine policy would be required. | Medium to high. | Medium. | Same two-stage AI upscale then normalize policy. | Candidate only unless separately approved. | Medium. | High if output path is separate. | Medium. | Follow-up comparison candidate only; never auto-run after default result. |
| Pillow nearest-neighbor | Strong for hard pixels, but can look blocky at 1.5x. | Strong if alpha resized consistently; no semi-transparent halos introduced by interpolation. | Low. | High, deterministic per frame. | Poor visual smoothness at 1.5x; may need canvas pad/crop after scale. | Works best with Unity point filter/pixel-art import; can look too jagged if Unity filters again. | High. | High if output path is separate. | High. | Explicit fallback/debug/comparison backend, not the default. |
| Pillow LANCZOS | Medium; smooths pixel edges and can blur dot details. | Medium risk of alpha fringe unless premultiplied/alpha-aware handling is used. | Medium risk. | High, deterministic. | Handles 1.5x gracefully but softens pixel art. | Better with bilinear-like import; may conflict with pixel-art filter expectations. | High. | High. | High. | Explicit fallback/debug/comparison backend, not the default. |
| Pillow bicubic | Middle-ground deterministic resize. | Medium alpha-fringe risk unless alpha is handled deliberately. | Medium. | High. | Handles arbitrary target canvas but may soften hard pixel clusters. | Candidate only unless separately approved. | High. | High. | High. | Explicit fallback/debug/comparison backend, not the default. |
| Unity import scale only | Preserves source file exactly. | Preserves alpha exactly. | None. | High. | Does not create 720p-equivalent source pixels; visual result depends on camera/import/filter scaling. | Tightly coupled to filter mode, pixels per unit, sprite scale constants. | High. | High. | High. | Could be acceptable for some static assets, but does not solve coexistence with existing 720p-size source frames. |

Policy recommendation: use a default-first AI workflow. Run the default backend, `waifu2x` (`waifu2x-ncnn-vulkan`), first and stop if the review result passes. If the default AI result is failed or ambiguous, do not automatically run other backends; report that a separate comparison task is needed before trying Real-ESRGAN-ncnn-vulkan, waifu2x parameter changes, `bicubic`, `lanczos`, `nearest`, asset-specific override, or another AI upscaler.

Alpha handling requirement for any resize method:

- Resize RGB and alpha in a controlled way.
- Prefer premultiplied-alpha resizing or matte-safe processing to avoid bright/dark halos.
- Preserve transparent pixels as transparent after resize.
- Re-run frame metrics on upscaled candidates.
- Build contact sheets on white, black, and checker backgrounds before any promotion.

## 6. Proposed manifest schema

Use JSON first because current scripts already write JSON manifests.

```json
{
  "assetId": "slime_attack",
  "characterOrMob": "Slime",
  "action": "Attack",
  "sourceKind": "grok_i2v_480p_video",
  "sourceResolution": {
    "width": 854,
    "height": 480,
    "label": "480p",
    "observedFrameSize": "928x960"
  },
  "targetRuntimeResolution": {
    "width": 1280,
    "height": 720,
    "label": "720p-compatible",
    "canvasPolicy": "pad_or_crop_to_runtime_reference"
  },
  "generatedBy": {
    "tool": "Grok Imagine",
    "mode": "image-to-video",
    "generationDate": "2026-05-27",
    "resolutionSetting": "480p"
  },
  "generationPromptDoc": "docs/grok-imagine-sprite-prompts.md",
  "rawInputPath": "Assets/Mobs/Sprites/Slime/Attack/Slime_Attack.mp4",
  "selectedFramesPath": "SpritePipelineWork/slime_attack/selected",
  "transparentPath": "SpritePipelineWork/slime_attack/transparent_frames",
  "cleanPath": "SpritePipelineWork/slime_attack/clean_frames",
  "upscaledPath": "SpritePipelineWork/slime_attack/upscaled_runtime_candidates/waifu2x",
  "unityReadyPath": "Assets/Mobs/Sprites/Slime/Attack",
  "coexistsWith": [
    {
      "assetId": "slime_attack_existing_runtime",
      "path": "Assets/Mobs/Sprites/Slime/Attack",
      "relationship": "current_runtime_path"
    }
  ],
  "runtimeReferenceOwner": [
    "Assets/Scripts/Stages/Stage1Forest.cs:attackSpriteFolderPath"
  ],
  "isRuntimeReferenced": false,
  "promotionStatus": "review_candidate",
  "upscale": {
    "method": "waifu2x",
    "aiBackend": "waifu2x-ncnn-vulkan",
    "aiScale": 2,
    "aiNoise": 0,
    "canvasPolicy": "ai_integer_scale_then_normalize_to_runtime_reference",
    "alphaPolicy": "inspect_ai_alpha_recombine_source_alpha_if_missing_or_damaged",
    "toolVersion": "external executable path recorded in candidate report"
  },
  "qualityChecks": {
    "frameCount": 0,
    "hasAlpha": true,
    "sequentialNumbering": true,
    "contactSheetReviewed": false,
    "unityImportVerified": false
  },
  "notes": "480p canonical source; current runtime path remains unchanged until promotion."
}
```

Required status vocabulary:

| Status | Meaning |
|---|---|
| `source_only` | Source/reference retained, no review output yet. |
| `raw_extracted` | Frames extracted into work root. |
| `transparent_review` | Background removal output exists in work root. |
| `upscale_candidate` | Upscale candidate exists outside runtime path. |
| `approved_visual` | Human approved visual/contact-sheet output. |
| `runtime_current` | This path is currently loaded by runtime/builder. |
| `runtime_replaced` | Previously runtime-current and replaced by explicit promotion. |
| `rejected` | Do not use without regeneration/rework. |

## 7. Minimal implementation tasks

| Order | Task | Risk | Files likely touched | Acceptance criteria | Stop condition |
|---:|---|---|---|---|---|
| 1 | Current pipeline inventory only | Low | `docs/19_sprite_upscale_pipeline_plan.md` or follow-up inventory doc | Script inventory, suffix inventory, and hard-coded path owners are documented; no asset writes. | Stop before running image processing, Unity, or `promote_selected_asset.py --commit`. |
| 2 | Asset manifest draft | Low | New manifest doc or `docs/assets.md`; possibly `SpritePipelineWork/<asset_id>/asset_manifest.json` outside `Assets/` for examples | Manifest schema records source kind, resolution, pipeline stages, runtime owner, and promotion status. | Stop if asked to classify by deleting/moving/renaming assets. |
| 3 | Non-destructive upscale dry-run tool | Low to Medium | `tools/sprite_pipeline/upscale_runtime_candidate.py`, README update | Tool reads selected/transparent frames, writes only under `SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/<backend>/`, refuses `Assets/`, records reports, supports dry-run/no-overwrite, and defaults to `waifu2x`. | Stop if output path would be under `Assets/`, if the AI executable is missing, or if the tool needs Unity import settings. |
| 4 | 480p-to-runtime candidate generation for one test asset | Medium | `SpritePipelineWork/<asset_id>/**` only; no `Assets/` writes | One missing/low-risk asset, preferably `Slime/Attack` or `Skeleton/Hit`, has a default `waifu2x` candidate plus report/contact sheet. | Stop if source frames are missing, ambiguous, alpha cleanup fails badly, or the default AI result is failed/ambiguous. Do not auto-run other backends. |
| 5 | Visual comparison contact sheet generation | Low | `SpritePipelineWork/<asset_id>/**`, review docs | Contact sheets compare lowres/source, default `waifu2x`, and current runtime/reference on dark/checker backgrounds when a reference is provided. | Stop if visual comparison cannot identify outlines or alpha edges clearly. If default AI is failed/ambiguous, recommend a separate backend-comparison task. |
| 6 | Promotion policy for selected upscaled output | Medium | Docs plus `promote_selected_asset.py` design notes; no commit mode yet | A policy says whether to copy into current runtime path or change stage/builder reference, including backup and `.meta` handling. | Stop before actual `Assets/` writes unless a separate explicit task authorizes them. |
| 7 | Unity import/reference validation | High | Validation result doc; possibly isolated worktree only | In separate validation worktree, Unity imports candidate/promoted output, runtime references resolve, EditMode or manual validation result is recorded. | Stop if Unity changes tracked files unexpectedly or validation would run against foreground checkout. |
| 8 | Documentation cleanup | Low | `tools/sprite_pipeline/README.md`, `docs/assets.md`, prompt docs | README matches implementation (`selected/` vs `selected_frames/`), asset categories are consolidated, and 480p policy is linked. | Stop if cleanup would imply asset deletion or runtime path changes. |

## 8. Recommendation

Recommendation: B. Existing scripts preserved + new non-destructive upscale wrapper added.

Reasons:

- The current scripts already enforce the right default safety model for review work: write under `SpritePipelineWork`, refuse `Assets/`, and generate manifests/review packets.
- No existing script performs production upscaling, so option A is insufficient.
- Deprecating the current scripts would throw away useful extraction, background removal, metrics, selection, and dry-run destination logic; option C is too broad.
- `promote_selected_asset.py --commit` is intentionally destructive and should remain a separate explicit promotion tool, not part of the first upscale implementation.

Risks:

- New 480p-upscaled candidates may not visually match existing 720p-era runtime assets, especially for pixel-art outlines.
- Non-integer 1.5x scaling can produce either blockiness or blur depending method.
- Alpha edges can halo if RGB/alpha are resized carelessly.
- Runtime path owners are hard-coded; any final path switch is Medium/High risk and needs explicit validation.
- Existing `_764x640` and `_transparent_clean` folders are mixed with runtime and intermediate meanings; manifest adoption is needed before cleanup.

Implementation note:

- `tools/sprite_pipeline/upscale_runtime_candidate.py` implements the non-destructive upscale candidate wrapper.
- It writes only under `SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/<backend>/` and refuses `Assets/` output.
- For isolated experiments it also supports `--output-root`, constrained to `SpritePipelineWork/<asset_id>/`.
- It requires explicit target size, `--reference-image`, or `--reference-dir`; it does not assume a global 720p size.
- It defaults to `waifu2x`, implemented by an external `waifu2x-ncnn-vulkan` executable that is not committed to the repo.
- It resolves the target canvas from explicit dimensions, a reference image, or a reference directory audit, then normalizes AI output to that canvas.
- It preserves source/input filenames and frame count, audits alpha, recombines deterministic source alpha when AI alpha is missing or clearly damaged, and creates a default AI review sheet.
- `nearest`, `lanczos`, and `bicubic` remain explicit fallback/debug/comparison backends. Runtime promotion remains a separate task.

Experiment note: `goblin_attack`

- Non-destructive candidates were generated for `nearest`, `lanczos`, and `bicubic` under `SpritePipelineWork/goblin_attack/upscaled_runtime_candidates/`.
- Reference size audit found `Assets/Mobs/Sprites/Goblin/Attack` has 50 PNG frames, all `1272x1298`, so `1272x1298` was used as the experiment target.
- `SpritePipelineWork/goblin_attack/selected` also has 50 numeric selected PNG frames at `1272x1298`; therefore this experiment is a candidate-generation/comparison pass, not a true enlargement pass.
- Comparison artifacts are in `SpritePipelineWork/goblin_attack/upscaled_runtime_candidates/comparison/`.
- Historical recommendation for this pass was `nearest`, because all three outputs were pixel-identical to the selected source frames. This historical result is superseded by the current global default: `waifu2x-ncnn-vulkan`.
- Runtime promotion was not performed. The next task should be promotion policy or isolated Unity import/reference validation before any runtime path change.

Lowres experiment note: `goblin_attack`

- The selected-frame experiment above was a no-op for resize quality because `SpritePipelineWork/goblin_attack/selected` was already `1272x1298`.
- A follow-up lowres-source experiment used reliable provenance from `selected/selected_manifest.json` to map selected frames back to `transparent_frames` source indices `3..97` with step `2`, plus two duplicate final frames from source `97`.
- Lowres source subset: `SpritePipelineWork/goblin_attack/lowres_selected_source/frames/`, all `928x960`.
- Target size: `1272x1298`, based on the current runtime canvas in `Assets/Mobs/Sprites/Goblin/Attack`.
- Candidates were generated for `nearest`, `lanczos`, and `bicubic` under `SpritePipelineWork/goblin_attack/upscaled_runtime_candidates_from_lowres/`.
- Comparison artifacts are in `SpritePipelineWork/goblin_attack/upscaled_runtime_candidates_from_lowres/comparison/`.
- Historical recommendation for that comparison remained conservative: `nearest` preserved pixel chunks, while `bicubic` was quantitatively closest to the current runtime reference in normalized diff. This does not define the current global default; new 480p Grok assets now start with `waifu2x-ncnn-vulkan`.
- Runtime promotion was not performed. Remaining decisions are limited to separate approval tasks: whether a fail/ambiguous default AI result should trigger comparison work, and whether final promotion overwrites existing runtime paths or switches references to a new explicit path.

Human review and promotion policy note: the enlarged historical review packet for `goblin_attack` is in `SpritePipelineWork/goblin_attack/upscaled_runtime_candidates_from_lowres/human_review_packet/`, and the promotion policy draft is `docs/20_sprite_promotion_policy_draft.md`. That packet remains historical comparison evidence only; current default candidate generation uses `waifu2x`. No runtime promotion was performed.

Default-first AI workflow note: previous experiments proved that backend comparison is possible, but normal operation is now simplified. The default backend is `waifu2x` (`waifu2x-ncnn-vulkan`); Codex should run only the default AI backend for an asset and stop if it passes. If the default AI result is failed or ambiguous, Codex must not automatically expand into Real-ESRGAN, waifu2x parameter sweeps, `bicubic`, `lanczos`, `nearest`, or other upscaler trials, and should instead report: `기본 AI 업스케일 결과가 불합격 또는 애매합니다. 여러 다른 방법을 시도해보는 별도 비교 작업으로 넘어가야 합니다.`

## 9. 480p video batch AI assetization workflow

`tools/sprite_pipeline/batch_ai_assetize_480p_videos.py` is the batch wrapper for 480p and probable-480p video sources that do not yet have a human-approved complete asset record.

Workflow:

1. Scan `Assets/**/*.mp4`, `Assets/**/*.mov`, `SpritePipelineWork/**/*.mp4`, and `SpritePipelineWork/**/*.mov`.
2. Inventory video metadata with `ffprobe` first, then Python OpenCV only when needed.
3. Infer `assetId`, category, actor, action, and target sprite folder from the existing sprite tree. If target inference fails, create a sanitized video-stem plus short-hash `assetId`, keep `targetAssetDir = null`, and allow candidate-only work under `SpritePipelineWork/<assetId>/`.
4. Classify the target folder as `existing`, `missing`, or `partial_or_ambiguous`; this is now runtime asset state only, not a skip condition.
5. Read `SpritePipelineWork/asset_completion_manifest.json` or the path supplied by `--approval-manifest`.
6. Skip only when the manifest has a valid `approvalStatus: approved` complete asset record for the source and `--force-reprocess-approved` is not set.
7. Treat all other records as unapproved, including existing runtime PNG folders, pending/rejected records, stale approved records, missing records, and unmappable targets.
8. Resolve target canvas in this order: existing PNGs in the target folder, same actor/action reference, same actor dominant reference, manifest target canvas, then no resolved canvas.
9. If no target canvas resolves, still create candidate-only AI output using native AI/source-scaled size when possible and mark `qualityGateStatus=ambiguous`.
10. Extract raw frames for every unapproved processing target.
11. Remove background into transparent RGBA frames.
12. Run the default AI backend, `waifu2x-ncnn-vulkan`, using transparent frames as input.
13. Normalize final candidate frames to the resolved target canvas when available; otherwise keep candidate-only native AI/source-scaled output.
14. Create review packets with raw samples, transparent samples, waifu2x candidates, existing runtime reference frames when available, target-canvas state, and quality-gate status.
15. Write new PNGs under `Assets/Mobs/Sprites/**` or `Assets/Player/Sprites/**` only when `--write-missing-assets` is provided and all missing-asset write gates pass.
16. Create per-asset reports, review contact sheets, and the full batch report under `SpritePipelineWork/batch_480p_ai_assetization/`.

Pillow `nearest`, `lanczos`, and `bicubic` are not part of this batch default path. They remain fallback/debug/comparison methods for separate explicit comparison tasks only.

Approval-manifest skip policy:

- Existing runtime PNGs are not a completion signal and are never sufficient for skip.
- A complete asset means a human-approved record in `SpritePipelineWork/asset_completion_manifest.json`.
- The batch validates approved records against source path, source signature, and approved asset/candidate PNG path where possible.
- If an approved record is stale or missing required paths, it is reported as `approved_but_stale_or_missing` and processed again as unapproved.
- The batch does not automatically write `approved` records. Processed assets report `approvalSuggestion: needs_human_review`.

Candidate/write policy:

- Default mode is candidate-only and writes no PNGs under `Assets/**`.
- Existing runtime sprite folders are reference canvas and visual comparison sources only.
- Existing runtime PNGs are never overwritten, deleted, moved, renamed, or cleaned up.
- `--write-missing-assets` can write only when `runtimeAssetState=missing`, the asset is unapproved, target canvas is resolved, quality gate passes, the target folder has no PNGs, and candidate frame/alpha validation passes.
- `partial_or_ambiguous` can produce review candidates, but it cannot write to `Assets/**`.
- `--overwrite-work` can replace only this batch's `SpritePipelineWork/**` outputs. It never permits `Assets/**` overwrite.
- `.meta` files are not manually created or edited. Unity import/reference validation remains a later step.

Failure policy:

- If `waifu2x-ncnn-vulkan` is missing, the batch records the inventory/classification/approval report and stops before processing candidates.
- If frame extraction, background removal, AI upscale, alpha validation, or final frame validation fails, the item is marked failed and the batch continues with the next item.
- If target canvas is unresolved but candidate generation can continue, the item is marked ambiguous and remains candidate-only.
- If the default AI result is failed or ambiguous, do not automatically run Real-ESRGAN, waifu2x parameter sweeps, `bicubic`, `lanczos`, `nearest`, or another backend.
- Runtime promotion and runtime overwrite are still forbidden. Final promotion/approval is a separate human approval task.

## Investigation record

Required documents read:

- `AGENTS.md`
- `REFACTOR_BACKLOG.md`
- `docs/00_actual_project_audit.md`
- `docs/02_unity_scene_and_object_construction.md`
- `docs/08_open_questions.md`
- `docs/11_project_decisions.md`
- `docs/12_v0_1_scope.md`
- `docs/13_next_backlog.md`
- `docs/assets.md`
- `docs/grok-imagine-sprite-prompts.md`
- `tools/sprite_pipeline/README.md`

Additional documents searched for current asset policy context:

- `docs/asset_acceptance_contract.md`
- `docs/asset_audit_manifest.md`
- `docs/grok_generation_queue.md`
- `docs/grok_manual_generation_packet.md`
- `docs/grok_source_still_checklist.md`

Python scripts found:

- `tools/sprite_pipeline/analyze_frames.py`
- `tools/sprite_pipeline/apply_human_selection.py`
- `tools/sprite_pipeline/build_review_packet.py`
- `tools/sprite_pipeline/extract_frames.py`
- `tools/sprite_pipeline/promote_selected_asset.py`
- `tools/sprite_pipeline/remove_background.py`

Python script locations not found:

- Repo root `*.py`: none.
- `scripts/**/*.py`: `scripts` directory missing.
- `Assets/**/*.py`: none found.
- `SpritePipelineWork/**/*.py`: none found.

Historical pre-implementation upscale/resize-related code locations found:

- `tools/sprite_pipeline/build_review_packet.py`: `rgba.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)` for review thumbnails.
- `tools/sprite_pipeline/apply_human_selection.py`: `rgba.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)` for selected contact sheets/GIFs.
- At the time of the initial scan, no `upscale`, `cv2.resize`, `Image.resize`, production `nearest`, 480p-to-720p, or 764x640 generation implementation was found in Python scripts. This is superseded by `tools/sprite_pipeline/upscale_runtime_candidate.py`, which now implements the non-destructive AI-default candidate wrapper.

480p/720p/764x640 path evidence:

- `docs/grok_manual_generation_packet.md` currently describes `Draft settings | 480p` and `Final settings | 720p`; new policy should supersede that by making 480p canonical source and upscaling postprocess explicit.
- `Assets/Player/Sprites/Idle_764x640`
- `Assets/Player/Sprites/Defense_764x640`
- `Assets/Player/Sprites/Die_764x640`
- `Assets/Player/Sprites/SmallHit_764x640`
- `Assets/Player/Sprites/StrongHit/Player_StrongHit_20260511_230207_650x640`
- `Assets/Player/Sprites/StrongHit/Player_StrongHit_20260511_230207_transparent_764x640`
- `SpritePipelineWork/goblin_attack/metrics.*` and `SpritePipelineWork/goblin_hit/metrics.*` contain observed work-frame dimensions such as `928x960` and `928x976`; these are not literal 720p/480p folders.

Validation not run:

- Unity batchmode, builds, and tests were not run because this task is documentation-only and the user explicitly forbade Unity validation.
- No image upscaling, background removal, frame extraction, or promotion command was run.
- No `promote_selected_asset.py --commit` was run because it can overwrite runtime assets and remove stale `.png.meta` files.

Human decisions needed next:

- Confirm target runtime canvas policy for 480p-derived sprites: exact 720p canvas, current action-folder dimensions, or per-asset reference matching.
- Choose initial test asset for non-destructive upscale comparison.
- Decide whether 480p source MP4s should be tracked under `Assets/`, stored outside `Assets/`, or represented only by manifest/provenance.
- Decide whether final runtime promotion should overwrite current referenced folders or switch stage/builder paths to new explicit folders.
- For any fail/ambiguous default AI result, decide whether to approve a separate comparison task using Real-ESRGAN-ncnn-vulkan, waifu2x parameter changes, `bicubic`, `lanczos`, `nearest`, asset-specific override, or another AI upscaler.
