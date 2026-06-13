#!/usr/bin/env python3
"""Promote human-approved selected sprite frames into the inferred runtime folder."""

from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

from PIL import Image


ENTITY_CASE = {
    "goblin": "Goblin",
    "slime": "Slime",
    "skeleton": "Skeleton",
    "bat": "Bat",
    "player": "Player",
}

ACTION_CASE = {
    "attack": "Attack",
    "hit": "Hit",
    "dead": "Dead",
    "idle": "Idle",
    "defense": "Defense",
    "small_hit": "SmallHit",
    "strong_hit": "StrongHit",
}

VISUAL_STATUSES = ("normal", "reextract", "regenerate")


@dataclass(frozen=True)
class SelectedFrames:
    manifest: dict
    selected_dir: Path
    preview_path: Path
    frames: list[Path]
    dimensions: tuple[int, int]


@dataclass(frozen=True)
class Destination:
    path: Path
    exists: bool
    mode: str
    reason: str
    candidates: list[dict]
    runtime_reference_count: int
    code_change_required: bool
    blocked_reason: str | None


def project_root() -> Path:
    return Path(__file__).resolve().parents[2]


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root()).as_posix()
    except ValueError:
        return str(path)


def safe_asset_id(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", value.strip())
    if not cleaned:
        raise SystemExit("asset_id cannot be empty")
    return cleaned


def parse_asset_id(asset_id: str) -> tuple[str, str, str, str]:
    parts = asset_id.split("_")
    if len(parts) < 2:
        raise SystemExit("asset_id must use <entity>_<action>, for example goblin_attack")

    entity_key = parts[0].lower()
    action_key = "_".join(parts[1:]).lower()
    if entity_key not in ENTITY_CASE:
        raise SystemExit(f"Unsupported entity in asset_id: {entity_key}")
    if action_key not in ACTION_CASE:
        raise SystemExit(f"Unsupported action in asset_id: {action_key}")
    return entity_key, action_key, ENTITY_CASE[entity_key], ACTION_CASE[action_key]


def target_root_for(entity_key: str, entity: str, action: str) -> Path:
    root = project_root()
    if entity_key == "player":
        return root / "Assets" / "Player" / "Sprites" / action
    return root / "Assets" / "Mobs" / "Sprites" / entity / action


def canonical_folder_name(entity_key: str, entity: str, action: str) -> str:
    prefix = "Player" if entity_key == "player" else entity
    return f"{prefix}_{action}_transparent_clean"


def numbered_pngs(folder: Path) -> list[Path]:
    if not folder.exists():
        return []
    frames = []
    for path in folder.glob("*.png"):
        if path.stem.isdigit():
            frames.append(path)
    return sorted(frames, key=lambda p: int(p.stem))


def validate_sequential(frames: list[Path], label: str) -> None:
    numbers = [int(path.stem) for path in frames]
    expected = list(range(len(frames)))
    if numbers != expected:
        raise SystemExit(f"{label} PNG frames must be sequential 0..{len(frames) - 1}: {numbers}")


def validate_selected(asset_id: str, work_root: Path) -> SelectedFrames:
    selected_dir = work_root / asset_id / "selected"
    manifest_path = selected_dir / "selected_manifest.json"
    preview_path = selected_dir / "selected_preview.gif"

    if not manifest_path.exists():
        raise SystemExit(f"selected_manifest.json does not exist: {manifest_path}")
    if not preview_path.exists():
        raise SystemExit(f"selected_preview.gif does not exist: {preview_path}")
    if not selected_dir.exists() or not selected_dir.is_dir():
        raise SystemExit(f"selected frame folder does not exist: {selected_dir}")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_asset_id = manifest.get("asset_id")
    if manifest_asset_id != asset_id:
        raise SystemExit(f"asset_id mismatch: CLI={asset_id}, selected_manifest={manifest_asset_id}")

    frames = numbered_pngs(selected_dir)
    if not frames:
        raise SystemExit(f"selected PNG frame count must be greater than zero: {selected_dir}")
    validate_sequential(frames, "selected")

    dimensions: tuple[int, int] | None = None
    for frame in frames:
        with Image.open(frame) as image:
            if dimensions is None:
                dimensions = image.size
            elif image.size != dimensions:
                raise SystemExit(
                    f"selected PNG frames must have uniform dimensions: {frame} is {image.size}, expected {dimensions}"
                )
            if "A" not in image.getbands():
                raise SystemExit(f"selected PNG frame has no alpha channel: {frame}")

    if dimensions is None:
        raise SystemExit("selected PNG dimensions could not be read")
    return SelectedFrames(manifest, selected_dir, preview_path, frames, dimensions)


def text_files_for_reference_scan() -> list[Path]:
    roots = [project_root() / "Assets" / "Scripts", project_root() / "Assets" / "Editor"]
    files: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        for pattern in ("*.cs", "*.md", "*.json", "*.yaml", "*.yml", "*.txt"):
            files.extend(root.rglob(pattern))
    return files


def reference_count(asset_path: Path) -> int:
    target = rel(asset_path).replace("\\", "/").rstrip("/")
    if not target:
        return 0
    count = 0
    for path in text_files_for_reference_scan():
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            text = path.read_text(encoding="utf-8", errors="ignore")
        count += text.replace("\\", "/").count(target)
    return count


def exact_folder_reference_count(asset_path: Path) -> int:
    target = rel(asset_path).replace("\\", "/").rstrip("/")
    if not target:
        return 0
    quoted = re.compile(rf'["\']{re.escape(target)}["\']')
    count = 0
    for path in text_files_for_reference_scan():
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            text = path.read_text(encoding="utf-8", errors="ignore")
        count += len(quoted.findall(text.replace("\\", "/")))
    return count


def infer_frame_name_style(frames: list[Path]) -> tuple[str, int | None]:
    if not frames:
        return "unpadded: 0.png, 1.png, 2.png", None

    stems = [path.stem for path in frames]
    zero_stem = next((stem for stem in stems if int(stem) == 0), None)
    if zero_stem is not None and len(zero_stem) > 1 and zero_stem.startswith("0"):
        return f"zero-padded width {len(zero_stem)}: {zero_stem}.png", len(zero_stem)

    padded = [stem for stem in stems if len(stem) > 1 and stem.startswith("0")]
    if padded and all(len(stem) == len(padded[0]) for stem in padded):
        width = len(padded[0])
        return f"zero-padded width {width}: {str(0).zfill(width)}.png, {str(1).zfill(width)}.png", width

    return "unpadded: 0.png, 1.png, 2.png", None


def frame_filename(index: int, pad_width: int | None) -> str:
    stem = str(index).zfill(pad_width) if pad_width is not None else str(index)
    return f"{stem}.png"


def folder_score(folder: Path, entity: str, action: str, suffix: str) -> int:
    name = folder.name.lower()
    canonical = f"{entity}_{action}_{suffix}".lower()
    score = 0
    if name == canonical:
        score += 100
    if entity.lower() in name:
        score += 20
    if action.lower() in name:
        score += 20
    if name.endswith(suffix):
        score += 10
    score += min(len(numbered_pngs(folder)), 999)
    return score


def choose_best(candidates: list[dict]) -> dict | None:
    if not candidates:
        return None
    return sorted(candidates, key=lambda item: (-item["score"], item["path"]))[0]


def resolve_destination(asset_id: str) -> tuple[Destination, Path, str, str]:
    entity_key, action_key, entity, action = parse_asset_id(asset_id)
    target_root = target_root_for(entity_key, entity, action)
    canonical_name = canonical_folder_name(entity_key, entity, action)
    canonical = target_root / canonical_name

    if not target_root.exists():
        return (
            Destination(
                canonical,
                False,
                "blocked",
                f"Target root does not exist: {rel(target_root)}",
                [],
                0,
                True,
                "BLOCKED_DESTINATION_AMBIGUOUS",
            ),
            target_root,
            entity,
            action,
        )

    direct_root_refs = exact_folder_reference_count(target_root)
    if direct_root_refs > 0:
        return (
            Destination(
                target_root,
                True,
                "in-place frame replacement",
                "Selected exact runtime-referenced action root; numeric frame PNGs will be replaced in place while non-frame files and subfolders are preserved.",
                [],
                direct_root_refs,
                False,
                None,
            ),
            target_root,
            entity,
            action,
        )

    clean_candidates = []
    transparent_candidates = []
    for folder in sorted([path for path in target_root.iterdir() if path.is_dir()]):
        refs = exact_folder_reference_count(folder)
        lower = folder.name.lower()
        if lower.endswith("_transparent_clean"):
            clean_candidates.append(
                {
                    "path": rel(folder),
                    "kind": "transparent_clean",
                    "score": folder_score(folder, entity, action, "transparent_clean"),
                    "runtime_reference_count": refs,
                    "png_count": len(numbered_pngs(folder)),
                }
            )
        elif lower.endswith("_transparent"):
            transparent_candidates.append(
                {
                    "path": rel(folder),
                    "kind": "transparent",
                    "score": folder_score(folder, entity, action, "transparent"),
                    "runtime_reference_count": refs,
                    "png_count": len(numbered_pngs(folder)),
                }
            )

    runtime_clean = [candidate for candidate in clean_candidates if candidate["runtime_reference_count"] > 0]
    if runtime_clean:
        chosen = choose_best(runtime_clean)
        path = project_root() / chosen["path"]
        refs = exact_folder_reference_count(path)
        return (
            Destination(
                path,
                path.exists(),
                "full folder frame replacement",
                "Selected existing *_transparent_clean folder because it is already referenced as runtime final.",
                clean_candidates,
                refs,
                False,
                None,
            ),
            target_root,
            entity,
            action,
        )

    runtime_transparent = [candidate for candidate in transparent_candidates if candidate["runtime_reference_count"] > 0]
    if runtime_transparent:
        chosen = choose_best(runtime_transparent)
        path = project_root() / chosen["path"]
        refs = exact_folder_reference_count(path)
        return (
            Destination(
                path,
                path.exists(),
                "full folder frame replacement",
                "Selected existing *_transparent folder because it is already referenced as runtime final.",
                transparent_candidates,
                refs,
                False,
                None,
            ),
            target_root,
            entity,
            action,
        )

    if clean_candidates:
        chosen = choose_best(clean_candidates)
        path = project_root() / chosen["path"]
        refs = exact_folder_reference_count(path)
        return (
            Destination(
                path,
                path.exists(),
                "full folder frame replacement",
                "No runtime-used folder exists; selected existing *_transparent_clean folder with strongest name/count match.",
                clean_candidates,
                refs,
                False,
                None,
            ),
            target_root,
            entity,
            action,
        )

    canonical_refs = exact_folder_reference_count(canonical)
    reason = f"No final subfolder exists; canonical destination is {canonical_name}."

    return (
        Destination(
            canonical,
            canonical.exists(),
            "full folder frame replacement",
            reason,
            clean_candidates + transparent_candidates,
            canonical_refs,
            False,
            None,
        ),
        target_root,
        entity,
        action,
    )


def existing_destination_files(destination: Path) -> tuple[list[Path], list[Path]]:
    pngs = numbered_pngs(destination)
    metas = [path.with_name(path.name + ".meta") for path in pngs if path.with_name(path.name + ".meta").exists()]
    return pngs, metas


def planned_backup_path(work_root: Path, asset_id: str, timestamp: str | None = None) -> Path:
    stamp = timestamp or datetime.now().strftime("%Y%m%d_%H%M%S")
    return work_root / asset_id / "promotion_backup" / stamp


def report_lines(
    asset_id: str,
    selected: SelectedFrames | None,
    destination: Destination | None,
    target_root: Path | None,
    backup_path: Path | None,
    status: str,
    notes: list[str],
) -> list[str]:
    dest_png_count = 0
    stale_png_count = 0
    stale_meta_count = 0
    destination_exists = False
    frame_style = "unknown"
    pad_width = None
    stale_pngs: list[Path] = []
    if selected is not None and destination is not None:
        existing_pngs, _ = existing_destination_files(destination.path)
        frame_style, pad_width = infer_frame_name_style(existing_pngs)
        dest_png_count = len(existing_pngs)
        stale_pngs = existing_pngs[len(selected.frames) :]
        stale_png_count = len(stale_pngs)
        stale_meta_count = sum(1 for path in stale_pngs if path.with_name(path.name + ".meta").exists())
        destination_exists = destination.path.exists()

    lines = [
        "# Destination Resolve Report",
        "",
        f"- Asset ID: `{asset_id}`",
    ]
    if selected is not None:
        lines.extend(
            [
                f"- Selected preview: `{rel(selected.preview_path)}`",
                f"- Selected frame folder: `{rel(selected.selected_dir)}`",
                f"- Selected frame count: `{len(selected.frames)}`",
                f"- Selected frame dimensions: `{selected.dimensions[0]}x{selected.dimensions[1]}`",
            ]
        )
    if target_root is not None:
        lines.append(f"- Target root: `{rel(target_root)}`")
    if destination is not None:
        lines.extend(
            [
                f"- Resolved destination: `{rel(destination.path)}`",
                f"- Mode: `{destination.mode}`",
                f"- Destination exists: `{str(destination_exists).lower()}`",
                f"- Existing destination PNGs: `{dest_png_count}`",
                f"- Frame filename style detected: `{frame_style}`",
                f"- Stale destination frames removed on commit: `{str(stale_png_count > 0).lower()}`",
                f"- Stale destination PNG count: `{stale_png_count}`",
                f"- Stale frame PNGs that would be removed: `{', '.join(path.name for path in stale_pngs) if stale_pngs else 'none'}`",
                f"- Stale `.png.meta` files removed with stale frames: `{str(stale_meta_count > 0).lower()}`",
                f"- Stale `.png.meta` count: `{stale_meta_count}`",
                f"- Runtime reference count for resolved destination: `{destination.runtime_reference_count}`",
                f"- Code change required: `{str(destination.code_change_required).lower()}`",
                f"- Resolve reason: {destination.reason}",
            ]
        )
        if destination.candidates:
            lines.extend(["", "## Candidate Folders", ""])
            for candidate in destination.candidates:
                lines.append(
                    f"- `{candidate['path']}`: kind `{candidate['kind']}`, score `{candidate['score']}`, "
                    f"PNGs `{candidate['png_count']}`, runtime refs `{candidate['runtime_reference_count']}`"
                )
    if backup_path is not None:
        lines.append(f"- Backup path that would be created: `{rel(backup_path)}`")
    if selected is not None:
        example_names = [frame_filename(i, pad_width) for i in range(min(3, len(selected.frames)))]
        lines.append(f"- Destination frame name examples: `{', '.join(example_names)}`")
        lines.append(
            f"- Exact command to commit: `python tools/sprite_pipeline/promote_selected_asset.py --asset-id {asset_id} --visual-status normal --commit`"
        )
    if notes:
        lines.extend(["", "## Notes", ""])
        lines.extend(f"- {note}" for note in notes)
        block_codes = [note for note in notes if note.startswith("BLOCKED_")]
        if block_codes:
            lines.append(f"- Block code: `{block_codes[0]}`")
    lines.extend(["", f"Final status: `{status}`", ""])
    return lines


def write_report(asset_id: str, lines: list[str], work_root: Path) -> Path:
    report_path = work_root / asset_id / "selected" / "destination_resolve_report.md"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines), encoding="utf-8")
    return report_path


def print_reextract_command(asset_id: str) -> int:
    print("Visual status is reextract; no promotion performed.")
    print(f"Rerun selection/export after adjusting SpritePipelineWork/{asset_id}/human_selection.yaml:")
    print(f"python tools/sprite_pipeline/apply_human_selection.py --asset-id {asset_id} --overwrite-work")
    return 0


def print_regenerate_message(asset_id: str) -> int:
    print("Visual status is regenerate; no promotion performed.")
    print(f"A new Grok source is required for asset_id {asset_id}.")
    return 0


def ensure_inside_assets(path: Path) -> None:
    assets = (project_root() / "Assets").resolve()
    try:
        path.resolve().relative_to(assets)
    except ValueError as exc:
        raise SystemExit(f"Destination must be under Assets/: {path}") from exc


def copy_with_backup(selected: SelectedFrames, destination: Destination, backup: Path) -> dict:
    ensure_inside_assets(destination.path)
    destination.path.mkdir(parents=True, exist_ok=True)

    existing_pngs, existing_metas = existing_destination_files(destination.path)
    _, pad_width = infer_frame_name_style(existing_pngs)
    backup.mkdir(parents=True, exist_ok=False)
    for path in existing_pngs + existing_metas:
        shutil.copy2(path, backup / path.name)

    assets_changed: list[str] = []
    outside_assets_changed: list[str] = []
    overwritten = 0
    copied = 0
    stale_png_removed = 0
    stale_meta_removed = 0

    for index, src in enumerate(selected.frames):
        dst = destination.path / frame_filename(index, pad_width)
        if dst.exists():
            overwritten += 1
        shutil.copy2(src, dst)
        copied += 1
        assets_changed.append(rel(dst))

    for stale in existing_pngs[len(selected.frames) :]:
        meta = stale.with_name(stale.name + ".meta")
        if stale.exists():
            stale.unlink()
            stale_png_removed += 1
            assets_changed.append(rel(stale))
        if meta.exists():
            meta.unlink()
            stale_meta_removed += 1
            assets_changed.append(rel(meta))

    for path in sorted(backup.iterdir()):
        outside_assets_changed.append(rel(path))

    return {
        "copied_frame_count": copied,
        "overwritten_frame_count": overwritten,
        "stale_png_count_removed": stale_png_removed,
        "stale_png_meta_count_removed": stale_meta_removed,
        "files_changed_under_assets": sorted(set(assets_changed)),
        "files_changed_outside_assets": sorted(set(outside_assets_changed)),
    }


def write_promotion_outputs(
    asset_id: str,
    selected: SelectedFrames,
    destination: Destination,
    backup: Path,
    operation: dict,
    work_root: Path,
    preexisting_status: str,
    extra_outside_changed: list[Path],
) -> dict:
    result_path = work_root / asset_id / "promotion_result.md"
    manifest_path = work_root / asset_id / "promotion_manifest.json"
    outside = (
        operation["files_changed_outside_assets"]
        + [rel(path) for path in extra_outside_changed]
        + [rel(result_path), rel(manifest_path)]
    )
    manifest = {
        "asset_id": asset_id,
        "selected_preview": rel(selected.preview_path),
        "resolved_destination": rel(destination.path),
        "copied_frame_count": operation["copied_frame_count"],
        "overwritten_frame_count": operation["overwritten_frame_count"],
        "stale_png_count_removed": operation["stale_png_count_removed"],
        "stale_png_meta_count_removed": operation["stale_png_meta_count_removed"],
        "backup_path": rel(backup),
        "files_changed_under_assets": operation["files_changed_under_assets"],
        "files_changed_outside_assets": sorted(set(outside)),
        "preexisting_worktree_changes_left_untouched": bool(preexisting_status.strip()),
        "generated_at": datetime.now().isoformat(timespec="seconds"),
    }
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    lines = [
        "# Promotion Result",
        "",
        f"- Asset ID: `{asset_id}`",
        f"- Selected preview: `{manifest['selected_preview']}`",
        f"- Resolved destination: `{manifest['resolved_destination']}`",
        f"- Copied frame count: `{manifest['copied_frame_count']}`",
        f"- Overwritten frame count: `{manifest['overwritten_frame_count']}`",
        f"- Stale PNG count removed: `{manifest['stale_png_count_removed']}`",
        f"- Stale `.png.meta` count removed: `{manifest['stale_png_meta_count_removed']}`",
        f"- Backup path: `{manifest['backup_path']}`",
        f"- Unrelated pre-existing worktree changes left untouched: `{str(manifest['preexisting_worktree_changes_left_untouched']).lower()}`",
        "",
        "## Files Changed Under Assets",
        "",
    ]
    lines.extend(f"- `{path}`" for path in manifest["files_changed_under_assets"])
    lines.extend(["", "## Files Changed Outside Assets", ""])
    lines.extend(f"- `{path}`" for path in manifest["files_changed_outside_assets"])
    result_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return manifest


def git_status_porcelain() -> str:
    import subprocess

    result = subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=project_root(),
        check=False,
        capture_output=True,
        text=True,
    )
    return result.stdout


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--visual-status", required=True, choices=VISUAL_STATUSES)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--commit", action="store_true")
    parser.add_argument("--work-root", default="SpritePipelineWork")
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    work_root = Path(args.work_root)
    if not work_root.is_absolute():
        work_root = project_root() / work_root

    if args.visual_status == "reextract":
        return print_reextract_command(asset_id)
    if args.visual_status == "regenerate":
        return print_regenerate_message(asset_id)
    if args.commit and args.visual_status != "normal":
        raise SystemExit("--commit is only allowed with --visual-status normal")

    preexisting_status = git_status_porcelain()
    selected = validate_selected(asset_id, work_root)
    destination, target_root, _, _ = resolve_destination(asset_id)
    backup = planned_backup_path(work_root, asset_id)

    notes: list[str] = []
    status = "READY_TO_COMMIT"
    if destination.blocked_reason is not None:
        status = "BLOCKED"
        notes.append(destination.blocked_reason)
    if destination.code_change_required:
        status = "BLOCKED"
        notes.append("Resolved destination is not currently referenced by runtime/builder code; code changes would be required.")

    lines = report_lines(asset_id, selected, destination, target_root, backup, status, notes)
    report_path = write_report(asset_id, lines, work_root)
    print("\n".join(lines))
    print(f"Report written: {rel(report_path)}")

    if status != "READY_TO_COMMIT":
        return 2 if args.commit else 0
    if args.dry_run:
        return 0

    operation = copy_with_backup(selected, destination, backup)
    promotion_manifest = write_promotion_outputs(
        asset_id,
        selected,
        destination,
        backup,
        operation,
        work_root,
        preexisting_status,
        [report_path],
    )
    print("Promotion committed.")
    print(f"asset_id: {asset_id}")
    print(f"selected preview path: {rel(selected.preview_path)}")
    print(f"resolved destination: {rel(destination.path)}")
    print(f"copied frame count: {operation['copied_frame_count']}")
    print(f"overwritten frame count: {operation['overwritten_frame_count']}")
    print(f"stale PNG count removed: {operation['stale_png_count_removed']}")
    print(f"stale .png.meta count removed: {operation['stale_png_meta_count_removed']}")
    print(f"backup path: {rel(backup)}")
    print("exact files changed under Assets:")
    for path in promotion_manifest["files_changed_under_assets"]:
        print(f"- {path}")
    print("exact files changed outside Assets:")
    for path in promotion_manifest["files_changed_outside_assets"]:
        print(f"- {path}")
    print(
        "unrelated pre-existing worktree changes left untouched: "
        f"{promotion_manifest['preexisting_worktree_changes_left_untouched']}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
