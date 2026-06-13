#!/usr/bin/env python3
"""Extract numbered PNG frames into SpritePipelineWork/<asset_id>/raw_frames.

This tool never writes into Assets/. It is intended for review work only.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

import cv2
from PIL import Image


IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp", ".bmp"}


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


def assert_not_assets_output(path: Path) -> None:
    assets = (project_root() / "Assets").resolve()
    if is_relative_to(path.resolve(), assets):
        raise SystemExit(f"Refusing to write under Assets/: {path}")


def safe_asset_id(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", value.strip())
    if not cleaned:
        raise SystemExit("asset_id cannot be empty")
    return cleaned


def prepare_output_dir(path: Path, overwrite_work: bool) -> None:
    assert_not_assets_output(path)
    path.mkdir(parents=True, exist_ok=True)
    existing = list(path.iterdir())
    if existing and not overwrite_work:
        raise SystemExit(
            f"Output directory is not empty: {path}\n"
            "Pass --overwrite-work to replace review outputs in SpritePipelineWork."
        )
    if existing and overwrite_work:
        for item in existing:
            if item.is_file():
                item.unlink()
            else:
                raise SystemExit(f"Refusing to delete nested directory: {item}")


def numeric_key(path: Path) -> tuple[int, str]:
    match = re.search(r"(\d+)(?=\.[^.]+$)", path.name)
    return (int(match.group(1)) if match else 10**9, path.name.lower())


def copy_image_sequence(source_dir: Path, output_dir: Path) -> dict:
    files = sorted(
        [p for p in source_dir.iterdir() if p.is_file() and p.suffix.lower() in IMAGE_EXTENSIONS],
        key=numeric_key,
    )
    if not files:
        raise SystemExit(f"No image frames found in {source_dir}")

    first_size = None
    for index, src in enumerate(files):
        with Image.open(src) as img:
            if first_size is None:
                first_size = img.size
            img.convert("RGBA").save(output_dir / f"{index}.png")

    return {
        "source_type": "image_sequence",
        "source_frame_count": len(files),
        "output_frame_count": len(files),
        "first_frame_size": first_size,
    }


def extract_video(source_file: Path, output_dir: Path) -> dict:
    cap = cv2.VideoCapture(str(source_file))
    if not cap.isOpened():
        raise SystemExit(f"Could not open video: {source_file}")

    fps = float(cap.get(cv2.CAP_PROP_FPS) or 0)
    frame_count_meta = int(cap.get(cv2.CAP_PROP_FRAME_COUNT) or 0)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH) or 0)
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT) or 0)

    index = 0
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        Image.fromarray(rgb).save(output_dir / f"{index}.png")
        index += 1

    cap.release()
    if index == 0:
        raise SystemExit(f"No frames extracted from {source_file}")

    return {
        "source_type": "video",
        "source_frame_count": frame_count_meta,
        "output_frame_count": index,
        "fps": fps,
        "width": width,
        "height": height,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True, help="Review asset id, for example goblin_hit")
    parser.add_argument("--source", required=True, help="MP4 file or image-frame folder")
    parser.add_argument("--work-root", default="SpritePipelineWork")
    parser.add_argument("--overwrite-work", action="store_true")
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    source = resolve_project_path(args.source)
    if not source.exists():
        raise SystemExit(f"Source does not exist: {source}")

    work_root = resolve_project_path(args.work_root)
    assert_not_assets_output(work_root)
    asset_root = work_root / asset_id
    raw_dir = asset_root / "raw_frames"
    prepare_output_dir(raw_dir, args.overwrite_work)

    if source.is_dir():
        manifest = copy_image_sequence(source, raw_dir)
    else:
        manifest = extract_video(source, raw_dir)

    manifest.update(
        {
            "asset_id": asset_id,
            "source": str(source),
            "raw_frames": str(raw_dir),
            "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        }
    )

    asset_root.mkdir(parents=True, exist_ok=True)
    (asset_root / "extraction_manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8"
    )
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
