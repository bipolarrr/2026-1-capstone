#!/usr/bin/env python3
"""Rebuild a low-resolution selected source subset from selected_manifest.json."""

from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image


def project_root() -> Path:
    return Path(__file__).resolve().parents[2]


def resolve_project_path(value: str) -> Path:
    path = Path(value)
    if not path.is_absolute():
        path = project_root() / path
    return path.resolve()


def is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root()).as_posix()
    except ValueError:
        return str(path)


def assert_not_assets_output(path: Path) -> None:
    assets = (project_root() / "Assets").resolve()
    if is_relative_to(path.resolve(), assets):
        raise SystemExit(f"Refusing to write under Assets/: {path}")


def safe_asset_id(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", value.strip())
    if not cleaned:
        raise SystemExit("asset_id cannot be empty")
    return cleaned


def image_info(path: Path) -> dict:
    with Image.open(path) as img:
        return {"width": img.width, "height": img.height, "alpha": "A" in img.getbands()}


def read_manifest(path: Path, asset_id: str) -> dict:
    if not path.exists():
        raise SystemExit(f"Selected manifest does not exist: {path}")
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("asset_id") != asset_id:
        raise SystemExit(f"asset_id mismatch: CLI={asset_id}, manifest={payload.get('asset_id')}")
    copied = payload.get("copied_frames")
    if not isinstance(copied, list) or not copied:
        raise SystemExit("selected_manifest.json has no copied_frames mapping.")
    return payload


def source_path_for(source_root: Path, source_frame: int) -> Path | None:
    path = source_root / f"{source_frame}.png"
    return path if path.exists() else None


def validate_and_prepare_output(output_root: Path, overwrite: bool) -> Path:
    assert_not_assets_output(output_root)
    frames_dir = output_root / "frames"
    if output_root.exists() and not overwrite:
        existing = list(output_root.rglob("*"))
        if existing:
            raise SystemExit(f"Output already exists and is not empty: {output_root}")
    if frames_dir.exists() and overwrite:
        for path in frames_dir.glob("*.png"):
            path.unlink()
    output_root.mkdir(parents=True, exist_ok=True)
    frames_dir.mkdir(parents=True, exist_ok=True)
    return frames_dir


def write_markdown_report(path: Path, manifest: dict) -> None:
    sizes = ", ".join(
        f"{item['width']}x{item['height']}={item['count']}" for item in manifest["sourceFrameSizes"]
    )
    first = manifest["frameMappings"][:5]
    first_lines = [
        f"- `{item['outputFile']}` <= `{item['sourcePath']}` ({item['role']})"
        for item in first
    ]
    lines = [
        "# Lowres Selected Source Report",
        "",
        f"- Asset ID: `{manifest['assetId']}`",
        f"- Mapping confidence: `{manifest['mappingConfidence']}`",
        f"- Mapping evidence: {manifest['mappingEvidence']}",
        f"- Selected manifest: `{manifest['selectedManifest']}`",
        f"- Preferred source root: `{manifest['preferredSourceRoot']}`",
        f"- Fallback source root: `{manifest['fallbackSourceRoot']}`",
        f"- Output frames: `{manifest['framesDir']}`",
        f"- Output frame count: `{manifest['outputFrameCount']}`",
        f"- Source sizes: `{sizes}`",
        f"- Alpha preserved: `{str(manifest['alphaPreserved']).lower()}`",
        f"- Destructive operations: `{len(manifest['destructiveOperations'])}`",
        "",
        "## First Mappings",
        "",
    ]
    lines.extend(first_lines)
    lines.extend(
        [
            "",
            "## Notes",
            "",
            "- Files are copied into `SpritePipelineWork` only.",
            "- Runtime assets are not modified.",
            "- Duplicate hold frames preserve selected output order while pointing to the same source frame.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--selected-manifest", help="Defaults to SpritePipelineWork/<asset_id>/selected/selected_manifest.json")
    parser.add_argument("--transparent-dir", help="Defaults to SpritePipelineWork/<asset_id>/transparent_frames")
    parser.add_argument("--raw-dir", help="Defaults to SpritePipelineWork/<asset_id>/raw_frames")
    parser.add_argument("--output-root", help="Defaults to SpritePipelineWork/<asset_id>/lowres_selected_source")
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    asset_root = project_root() / "SpritePipelineWork" / asset_id
    selected_manifest = resolve_project_path(args.selected_manifest) if args.selected_manifest else asset_root / "selected" / "selected_manifest.json"
    transparent_dir = resolve_project_path(args.transparent_dir) if args.transparent_dir else asset_root / "transparent_frames"
    raw_dir = resolve_project_path(args.raw_dir) if args.raw_dir else asset_root / "raw_frames"
    output_root = resolve_project_path(args.output_root) if args.output_root else asset_root / "lowres_selected_source"

    allowed_root = asset_root.resolve()
    if output_root.resolve() != allowed_root and not is_relative_to(output_root.resolve(), allowed_root):
        raise SystemExit(f"Output root must stay under {allowed_root}: {output_root}")

    payload = read_manifest(selected_manifest, asset_id)
    frames_dir = validate_and_prepare_output(output_root, args.overwrite)

    frame_mappings = []
    sizes: dict[tuple[int, int], int] = {}
    alpha_preserved = True
    for selected_order, item in enumerate(payload["copied_frames"]):
        source_frame = item.get("source_frame")
        output_value = item.get("output")
        role = item.get("role", "selected")
        if source_frame is None or output_value is None:
            raise SystemExit(f"Invalid copied_frames mapping at index {selected_order}: {item}")

        selected_output = Path(output_value)
        output_name = selected_output.name
        source = source_path_for(transparent_dir, int(source_frame))
        source_kind = "transparent_frames"
        if source is None:
            source = source_path_for(raw_dir, int(source_frame))
            source_kind = "raw_frames"
        if source is None:
            raise SystemExit(f"Source frame {source_frame}.png not found in transparent_frames or raw_frames.")

        info = image_info(source)
        sizes[(info["width"], info["height"])] = sizes.get((info["width"], info["height"]), 0) + 1
        alpha_preserved = alpha_preserved and bool(info["alpha"])
        dst = frames_dir / output_name
        shutil.copy2(source, dst)
        frame_mappings.append(
            {
                "selectedOrder": selected_order,
                "selectedOutput": rel(selected_output),
                "outputFile": output_name,
                "sourceFrame": int(source_frame),
                "sourceKind": source_kind,
                "sourcePath": rel(source),
                "outputPath": rel(dst),
                "role": role,
                "width": info["width"],
                "height": info["height"],
                "alpha": info["alpha"],
            }
        )

    source_sizes = [
        {"width": width, "height": height, "count": count}
        for (width, height), count in sorted(sizes.items(), key=lambda entry: (-entry[1], entry[0]))
    ]
    manifest = {
        "assetId": asset_id,
        "mappingConfidence": "reliable",
        "mappingEvidence": "selected_manifest.json copied_frames records source_frame and selected output path for every selected frame.",
        "selectedManifest": rel(selected_manifest),
        "preferredSourceRoot": rel(transparent_dir),
        "fallbackSourceRoot": rel(raw_dir),
        "outputRoot": rel(output_root),
        "framesDir": rel(frames_dir),
        "outputFrameCount": len(frame_mappings),
        "sourceFrameSizes": source_sizes,
        "alphaPreserved": alpha_preserved,
        "frameMappings": frame_mappings,
        "destructiveOperations": [],
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }
    if len(frame_mappings) != 50:
        raise SystemExit(f"Expected 50 output frames for goblin_attack experiment, got {len(frame_mappings)}")
    if source_sizes != [{"width": 928, "height": 960, "count": 50}]:
        raise SystemExit(f"Expected all lowres sources to be 928x960, got {source_sizes}")

    manifest_path = output_root / "lowres_selected_source_manifest.json"
    report_path = output_root / "lowres_selected_source_report.md"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_markdown_report(report_path, manifest)
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
