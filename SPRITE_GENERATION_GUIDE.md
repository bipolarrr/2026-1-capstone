# Sprite Generation Guide

This document is the first thing Codex should read when the user asks to create a sprite from a video, for example:

- "스프라이트를 만들어달라"
- "이 영상으로 스프라이트 뽑아줘"
- "새 MP4도 기존 파이프라인처럼 처리해줘"
- "Golem_Idle.mp4로 스프라이트 만들어줘"

The default goal is not to immediately copy files into `Assets/`. The default goal is to create a human-reviewable sprite packet under:

```text
SpritePipelineWork/<asset_id>
```

The human should inspect the generated preview GIF/contact sheet and choose one of:

```text
정상
재추출
재생성
배경제거 재시도
```

## Default Workflow

When given a video file, Codex should:

1. Derive an `asset_id` from the filename.
2. Find the source video in the repository.
3. Use the existing sprite pipeline tools under `tools/sprite_pipeline`.
4. Generate raw frames.
5. Remove background.
6. Analyze frames.
7. Build a review packet.
8. Report the preview GIF/contact sheet path the human should inspect.
9. Stop before promotion.

## Asset ID Rule

Convert the source filename to snake_case without extension.

Examples:

```text
Golem_Idle.mp4 -> golem_idle
Goblin_Hit.mp4 -> goblin_hit
Slime_Attack.mp4 -> slime_attack
CaveBoss_Death.mp4 -> cave_boss_death
```

If the repository already uses a different naming convention for that asset, follow the repository convention.

## Existing Tools

Prefer these existing tools:

```text
tools/sprite_pipeline/extract_frames.py
tools/sprite_pipeline/remove_background.py
tools/sprite_pipeline/analyze_frames.py
tools/sprite_pipeline/build_review_packet.py
tools/sprite_pipeline/apply_human_selection.py
tools/sprite_pipeline/promote_selected_asset.py
tools/sprite_pipeline/README.md
```

Always read `tools/sprite_pipeline/README.md` and each script's `--help` before running commands. Actual flags may differ from examples.

## Hard Stop Rules

During review packet generation, do not:

- run Unity
- run Unity validation/build/test
- run `promote_selected_asset.py`
- copy, move, overwrite, or delete files under `Assets/`
- manually edit `.meta` files
- edit runtime code
- edit builder code
- edit scenes or prefabs
- edit `ProjectSettings/`, `Packages/`, `Library/`, `Temp/`, or `UserSettings/`
- delete source videos, raw frames, transparent frames, prompt docs, provenance docs, or existing outputs
- touch unrelated pre-existing worktree changes
- stage or commit unless explicitly requested

## First Checks

PowerShell:

```powershell
git status --porcelain

Get-ChildItem -Force tools/sprite_pipeline
Get-Content tools/sprite_pipeline/README.md

python tools/sprite_pipeline/extract_frames.py --help
python tools/sprite_pipeline/remove_background.py --help
python tools/sprite_pipeline/analyze_frames.py --help
python tools/sprite_pipeline/build_review_packet.py --help
python tools/sprite_pipeline/apply_human_selection.py --help

Get-ChildItem -Force SpritePipelineWork -ErrorAction SilentlyContinue
```

Git Bash:

```bash
git status --porcelain

ls -la tools/sprite_pipeline
sed -n '1,220p' tools/sprite_pipeline/README.md

python tools/sprite_pipeline/extract_frames.py --help
python tools/sprite_pipeline/remove_background.py --help
python tools/sprite_pipeline/analyze_frames.py --help
python tools/sprite_pipeline/build_review_packet.py --help
python tools/sprite_pipeline/apply_human_selection.py --help

ls -la SpritePipelineWork 2>/dev/null || true
```

## Finding The Source Video

If the user gives a filename, search for that exact filename.

PowerShell:

```powershell
Get-ChildItem -Recurse -Force . -Filter "<SOURCE_FILENAME>" -ErrorAction SilentlyContinue |
  Where-Object {
    $_.FullName -notmatch '\\Library\\|\\Temp\\|\\UserSettings\\|\\.git\\'
  } |
  Select-Object FullName
```

Git Bash:

```bash
find . \
  -path './Library' -prune -o \
  -path './Temp' -prune -o \
  -path './UserSettings' -prune -o \
  -path './.git' -prune -o \
  -name '<SOURCE_FILENAME>' -print
```

If no source is found, stop and report:

```text
BLOCKED: <SOURCE_FILENAME> source video not found
```

If multiple matching source files are found:

1. Prefer a clear character/action folder such as `Assets/Mobs/Sprites/<Name>/<Action>`.
2. Then prefer a clear source/generated/provenance folder.
3. Ignore `Library`, `Temp`, cache, and `.git`.
4. If still ambiguous, stop and report:

```text
BLOCKED: multiple <SOURCE_FILENAME> sources found
```

## Standard Pipeline

Use real script help as the source of truth. The expected shape is:

```powershell
python tools/sprite_pipeline/extract_frames.py `
  --asset-id <asset_id> `
  --source "<source_path>" `
  --output-dir SpritePipelineWork/<asset_id>

python tools/sprite_pipeline/remove_background.py `
  --asset-id <asset_id> `
  --work-dir SpritePipelineWork/<asset_id>

python tools/sprite_pipeline/analyze_frames.py `
  --asset-id <asset_id> `
  --work-dir SpritePipelineWork/<asset_id>

python tools/sprite_pipeline/build_review_packet.py `
  --asset-id <asset_id> `
  --work-dir SpritePipelineWork/<asset_id>
```

If actual script options differ, adapt to the actual `--help` output and report the exact commands used.

## Expected Review Outputs

Check for outputs such as:

```text
SpritePipelineWork/<asset_id>/raw_frames/
SpritePipelineWork/<asset_id>/transparent_frames/
SpritePipelineWork/<asset_id>/candidate_spans.json
SpritePipelineWork/<asset_id>/human_selection.yaml
SpritePipelineWork/<asset_id>/selected/selected_preview.gif
SpritePipelineWork/<asset_id>/selected/selected_contact_sheet.png
SpritePipelineWork/<asset_id>/selected/selected_manifest.json
SpritePipelineWork/<asset_id>/selected/unity_copy_plan.md
```

If `selected/selected_preview.gif` does not exist, report the main review preview GIF/contact sheet created by `build_review_packet.py`.

Do not force `apply_human_selection.py` unless the human has approved frame ranges or the tool explicitly supports a safe default selection.

## Quality Checks

Inspect whether:

- source video is readable
- enough useful frames were extracted
- background removal damaged important foreground details
- body, hands, weapons, rocks, decorations, or silhouette were removed
- alpha is too weak or semi-transparent
- idle/attack/hit/death loop includes bad transition frames
- frame order is wrong
- crop is too tight
- contact sheet or GIF is not human-reviewable
- `candidate_spans.json` recommends reject/regenerate

If important detail or alpha is suspicious, create diagnostics under:

```text
SpritePipelineWork/<asset_id>/review_diagnostics/
```

Useful diagnostic artifacts:

```text
SpritePipelineWork/<asset_id>/review_diagnostics/dark_bg_preview.gif
SpritePipelineWork/<asset_id>/review_diagnostics/checkerboard_preview.gif
SpritePipelineWork/<asset_id>/review_diagnostics/raw_vs_transparent_contact_sheet.png
SpritePipelineWork/<asset_id>/review_diagnostics/alpha_opacity_notes.md
```

Do not repair by default. Repair is a separate task unless explicitly requested.

## Golem_Idle.mp4 Example

If the user asks to process `Golem_Idle.mp4`:

```text
source filename: Golem_Idle.mp4
asset id: golem_idle
work dir: SpritePipelineWork/golem_idle
visual purpose: Golem idle animation
```

Find the exact `Golem_Idle.mp4` path, then run the standard pipeline with `asset_id = golem_idle`.

Expected output for human inspection:

```text
SpritePipelineWork/golem_idle/selected/selected_preview.gif
```

If that does not exist, report the review preview GIF/contact sheet generated by the review packet tool.

## Final Report Format

After finishing, report only:

```text
1. Asset ID
   - <asset_id>

2. Source used
   - <exact source path>

3. Commands run
   - exact commands actually run

4. Generated review outputs
   - review preview GIF
   - selected preview GIF, if any
   - contact sheet
   - manifest JSON
   - candidate_spans.json
   - human_selection.yaml
   - report/markdown files

5. Human should inspect
   - one GIF path if possible
   - prefer SpritePipelineWork/<asset_id>/selected/selected_preview.gif
   - otherwise use review preview GIF/contact sheet

6. Human decision options
   - 정상
   - 재추출
   - 재생성
   - 배경제거 재시도

7. Problems found
   - include frame ranges if possible
   - if none, say: none obvious from automated inspection

8. Scope confirmation
   - changes are limited to SpritePipelineWork/<asset_id>
   - Assets/ was not modified
   - Unity was not run
   - .meta, scenes, prefabs, ProjectSettings, Packages, Library, Temp, UserSettings were not touched

9. Git/worktree status
   - git status --porcelain
   - clearly flag any change outside SpritePipelineWork/<asset_id>
```

## Promotion Is A Separate Task

Only after the human chooses `정상`, use a separate task for selection finalization or promotion.

Before promotion, check:

- selected frame count
- selected frame dimensions
- alpha/background quality
- intended runtime destination
- existing runtime references
- builder hard-coded paths
- animation/stage frame count references
- Unity import implications

Always try `promote_selected_asset.py --dry-run` before any real promotion.
