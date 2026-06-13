# Sprite Promotion Policy Draft

Date: 2026-05-28

## 1. Scope

This document is a draft policy for promoting sprite upscale candidates into Unity runtime assets.

It does not perform promotion. It does not authorize asset deletion, asset moves, asset renames, `.meta` churn, Unity import changes, runtime path changes, or `promote_selected_asset.py --commit`.

For v0.1 stabilization, asset deletion, move, and rename operations remain forbidden unless a separate explicit task authorizes them with rollback and validation steps.

## 2. Asset Categories

| Category | Meaning | Promotion status |
|---|---|---|
| source reference image | Still image used as identity, pose, scale, or prompt reference. | Source only unless separately approved as runtime art. |
| 480p Grok output | New canonical Grok Imagine generation input/output class. Exact extracted frame size must be recorded because 480p settings can yield nonstandard canvas sizes after processing. | Source/intermediate. |
| generated video | Grok or other generated animation source such as MP4. | Source/intermediate; keep provenance. |
| raw frames | Direct extraction from generated video or copied image sequence. | Intermediate; not Unity-ready by itself. |
| transparent frames | Background-removed or alpha-preserved frames. | Intermediate unless already runtime-referenced by existing project data. |
| selected frames | Human-selected action span or review export. | Review intermediate; still not automatically Unity-ready. |
| lowres selected source | Low-resolution source subset corresponding exactly to selected frames. | Review/provenance input for upscale experiments. |
| upscaled runtime candidate | Default AI or explicit fallback upscale result under `SpritePipelineWork/<asset_id>/**`, separated by backend/method. | Candidate only; never runtime-current by folder existence. |
| existing runtime asset | Existing PNGs under `Assets/**` that the current project may load or keep as legacy/current sprites. | Reference canvas, visual comparison source, rollback source, or current runtime dependency; not a completion signal. |
| approved complete asset | Human-approved final asset recorded in `SpritePipelineWork/asset_completion_manifest.json` with source, backend, target canvas, frame count, and approved asset/candidate path. | The only batch skip signal. |
| Unity-ready runtime sprite | Asset path currently loadable by runtime/builder references with correct frame count, alpha, dimensions, import settings, and animation behavior. | Runtime dependency; preserve path and GUIDs. Existing Unity-ready PNGs are not automatically approved complete assets. |
| promoted runtime asset | Candidate that passed gates and was explicitly installed into runtime use by an approved task. | Runtime-current or runtime-replaced, with rollback record and approval manifest update. |

Existing runtime assets and approved complete assets are deliberately separate. A folder can contain valid-looking runtime PNGs and still be unapproved if no human approval record exists, if the record is pending/rejected, or if the source signature/path has gone stale.

Unapproved assets are candidate-generation targets regardless of whether their runtime asset state is `existing`, `missing`, or `partial_or_ambiguous`.

## 2.1 Complete Asset Approval Manifest

The default approval manifest path is:

```text
SpritePipelineWork/asset_completion_manifest.json
```

The manifest records only human-approved complete assets. The batch may create an empty manifest when it is missing, but it must not auto-approve processed candidates.

Skip requires a valid record with `approvalStatus: approved`, a matching source video path/signature, and an existing approved asset or candidate path containing PNGs. Stale or missing approved records are reported as `approved_but_stale_or_missing` and processed again as unapproved.

Allowed approval status values:

```text
approved
rejected
pending
needs_review
```

Only `approved` can skip batch candidate generation. Promotion or manifest updates are separate human approval tasks.

## 3. Promotion Gates

An upscale candidate may be considered for runtime promotion only after all gates below are satisfied:

1. Provenance mapping exists.
2. Source frame count matches selected/runtime expectation.
3. Target canvas matches current runtime reference or an approved target.
4. Alpha is preserved.
5. Contact sheet is generated.
6. Human visual approval is recorded.
7. Method is recorded in manifest/report.
8. Unity import/reference validation is planned.
9. Rollback path exists.
10. No `.meta` or runtime path change occurs without explicit task approval.

Failure of any gate keeps the candidate in review status.

## 4. Default AI Promotion Candidate Policy

New 480p/Grok source frames must first produce a promotion candidate through the default AI upscaler.

Default backend: `waifu2x-ncnn-vulkan`.

Default candidate path:

```text
SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/waifu2x/
```

Candidate requirements:

1. Candidate frames are generated only under `SpritePipelineWork/**`.
2. The target canvas is based on the existing runtime/reference asset: explicit target size, reference image size, or audited reference directory frame size.
3. AI output is normalized to the target canvas after integer-scale AI processing.
4. Source/input frame names and frame count are preserved.
5. Alpha is audited; source alpha is recombined when AI alpha is missing or clearly damaged.
6. Reports and contact sheets are generated for human review.
7. A passing candidate is still not automatically promoted.
8. Promotion requires separate human approval plus Unity import/reference validation in an isolated validation workflow.

This policy does not authorize asset deletion, `.meta` churn, runtime overwrite, runtime path promotion, or `promote_selected_asset.py --commit`.

## 5. Missing Sprite Asset Creation

Missing sprite asset creation is separate from existing runtime asset promotion and separate from approval-manifest completion.

Allowed missing-only creation:

- A 480p source video may create new PNG frames under `Assets/Mobs/Sprites/**` or `Assets/Player/Sprites/**` only when the target sprite folder is classified as `missing` and the asset is not already approved complete.
- `missing` means the target folder does not exist, or it exists but has zero readable direct PNG frames and no nested readable sprite asset family.
- The batch must still resolve a target canvas from an existing actor/action or actor reference before writing Unity-facing PNGs.
- `--write-missing-assets` is required before any `Assets/**` PNG is created.
- Existing PNGs, existing sprite folders, nested sprite asset families, and ambiguous folder structures can still generate review candidates, but they are never written by the batch.
- New PNGs may cause Unity to create `.meta` files later during import. The pipeline must not manually create, edit, delete, or copy `.meta` files.

Forbidden promotion behavior remains unchanged:

- Do not overwrite existing runtime sprite PNGs.
- Do not delete stale PNGs or `.png.meta` files.
- Do not move, rename, or retarget runtime asset folders.
- Do not treat existing PNGs as complete or approved without a human approval manifest record.
- Do not update the approval manifest to `approved` from the batch run.
- Do not run `promote_selected_asset.py --commit` as part of missing-only creation.
- Do not change stage data, scene builder constants, runtime C#, Unity scenes, prefabs, assets, project settings, or package files.

Unity import/reference validation for newly created missing PNG folders is the next step and must be handled separately. It should verify that Unity-generated `.meta` files, import settings, sprite slicing, runtime references, and animation behavior are correct before any broader runtime reference work.

## 6. Method Selection Policy

### Default-First Policy

All new 480p/Grok outputs start with the default AI upscale backend only.

Default backend: `waifu2x` (`waifu2x-ncnn-vulkan`).

Default success path:

1. Confirm provenance.
2. Confirm lowres selected source or specified input.
3. Generate candidate with the default AI backend.
4. Normalize AI output to the runtime/reference target canvas.
5. Generate the default review/contact sheet.
6. Record backend, source, target, frame count, AI parameters, and alpha status.
7. Judge result as pass, fail, or ambiguous.
8. If the result passes, stop without running other methods.

Pass means frame count matches, target canvas matches, alpha is preserved, no obvious crop/position mismatch is visible, no obvious alpha halo is visible, no obvious animation jitter is visible, and the result is not immediately rejectable against the runtime reference or intended visual.

If the default AI result fails or is ambiguous, do not promote and do not automatically run other methods. Reports and final task output must include this message:

```text
기본 AI 업스케일 결과가 불합격 또는 애매합니다. 여러 다른 방법을 시도해보는 별도 비교 작업으로 넘어가야 합니다.
```

Allowed follow-up suggestions are limited to Real-ESRGAN-ncnn-vulkan, waifu2x noise/scale/tile parameter adjustment, `bicubic`, `lanczos`, `nearest`, asset-specific override, or another AI upscaler if needed. They must be separate explicit tasks.

### Method Escalation Policy

Try another method only when:

1. The default AI result fails.
2. The default AI result is ambiguous.
3. A human approves method comparison for a specific asset.
4. `waifu2x-ncnn-vulkan` is visually unsuitable for that asset.

Do not make Real-ESRGAN, waifu2x parameter sweeps, nearest/lanczos/bicubic comparison, or other method comparison the default workflow.

Draft defaults and exceptions:

- Global default: `waifu2x` via `waifu2x-ncnn-vulkan`.
- `nearest`, `lanczos`, and `bicubic` are fallback/debug/comparison backends only.
- Asset-specific override is allowed only after human visual review.
- Current `goblin_attack` candidate: `bicubic pending human approval`, because bicubic is quantitatively closest to the current runtime reference in normalized diff.
- `lanczos` remains a candidate-only method unless human review shows superior visual quality for a specific asset/action.

Do not promote based on quantitative diff alone. For pixel-art animation, outline quality, alpha edge behavior, and frame-to-frame stability can matter more than aggregate pixel distance.

## 7. Promotion Modes

### Mode A: No Promotion

Keep candidates under `SpritePipelineWork`. The runtime continues using the existing `Assets/**` path.

Use this mode while visual review, manifest policy, and Unity validation are incomplete.

### Mode B: Overwrite Existing Runtime Folder

Replace files in the currently referenced runtime folder.

This mode is high risk because it directly affects current gameplay references and can cause `.meta` or import-setting issues. It requires a backup, rollback plan, frame count validation, `.meta` preservation check, and explicit approval. During v0.1 stabilization, this mode is forbidden by default.

### Mode C: New Runtime Folder Path

Place promoted output in a new runtime folder and update stage data, builder constants, or runtime references to point to that folder.

This preserves the existing runtime folder, but it requires code/path changes and isolated Unity import/reference validation. It must be a separate task.

### Mode D: Manifest-Driven Future Selection

Keep runtime paths unchanged while manifest records candidates, review status, method, source, and intended target.

This is the preferred near-term management mode. Automated runtime selection from manifest is a future task, not part of the current pipeline.

## 8. Rollback Criteria

Rollback is required if any of these are observed after a promotion attempt:

- Sprite is missing or invisible in Unity.
- Alpha halo is visually severe.
- Animation jitter or frame-to-frame position instability is severe.
- Runtime reference path is missing or incorrect.
- Frame count does not match the expected action sequence.
- Import setting mismatch changes visual scale, filtering, pivot, packing, or animation behavior.
- `.meta` churn occurs unexpectedly.
- Character size or position no longer matches the current runtime baseline.
- Builder/runtime code paths no longer resolve the intended asset folder.

Rollback must restore the previous runtime asset path and behavior before further candidate attempts.

## 9. Current goblin_attack Recommendation

Current experiment summary:

- Source: `928x960`
- Target: `1272x1298`
- Source subset: `SpritePipelineWork/goblin_attack/lowres_selected_source/frames/`
- Candidate root: `SpritePipelineWork/goblin_attack/upscaled_runtime_candidates_from_lowres/`
- Methods compared: `nearest`, `lanczos`, `bicubic`
- Human review packet: `SpritePipelineWork/goblin_attack/upscaled_runtime_candidates_from_lowres/human_review_packet/`

Quantitative result:

- `nearest` mean normalized diff vs runtime: `0.50602314`
- `lanczos` mean normalized diff vs runtime: `0.16233057`
- `bicubic` mean normalized diff vs runtime: `0.15854814`

Draft recommendation:

- Treat the existing `nearest`/`lanczos`/`bicubic` comparison as historical evidence for that asset only; it does not define the current global default.
- Keep `waifu2x-ncnn-vulkan` as the global default for new 480p/Grok candidate generation.
- Bicubic was quantitatively closest to the current runtime reference, but this result must not be generalized into the global default.
- Treat `bicubic` as a possible `goblin_attack` exception only if a separate human approval and promotion task authorizes it.
- Keep `lanczos` as a comparison candidate only.
- Adopt the default-first AI policy for future operations: run `waifu2x` first, stop if it passes, and escalate only through a separate comparison task if the default AI result fails or is ambiguous.
- Do not promote any candidate until the human review packet is approved and a separate Unity import/reference validation task is created.
