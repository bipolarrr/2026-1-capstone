#!/usr/bin/env python3
"""Remove or preserve frame backgrounds into transparent_frames.

Default mode uses rembg. For flat Grok/chroma backgrounds, use:
  --method chroma --chroma-color 255,0,255

This tool writes only to SpritePipelineWork by default and refuses Assets/ output.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
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


def parse_rgb(value: str) -> tuple[int, int, int]:
    presets = {"magenta": (255, 0, 255), "green": (0, 255, 0), "white": (255, 255, 255)}
    key = value.strip().lower()
    if key in presets:
        return presets[key]
    parts = [int(p.strip()) for p in value.split(",")]
    if len(parts) != 3 or any(p < 0 or p > 255 for p in parts):
        raise argparse.ArgumentTypeError("Use a preset or R,G,B values from 0-255")
    return tuple(parts)  # type: ignore[return-value]


def chroma_remove(img: Image.Image, color: tuple[int, int, int], tolerance: float) -> Image.Image:
    rgba = img.convert("RGBA")
    arr = np.asarray(rgba).copy()
    rgb = arr[:, :, :3].astype(np.int16)
    target = np.array(color, dtype=np.int16)
    distance = np.linalg.norm(rgb - target, axis=2)
    arr[:, :, 3] = np.where(distance <= tolerance, 0, arr[:, :, 3])
    return Image.fromarray(arr, "RGBA")


def alpha_copy(img: Image.Image, alpha_threshold: int) -> Image.Image:
    rgba = img.convert("RGBA")
    arr = np.asarray(rgba).copy()
    arr[:, :, 3] = np.where(arr[:, :, 3] <= alpha_threshold, 0, arr[:, :, 3])
    return Image.fromarray(arr, "RGBA")


def rembg_remove(img: Image.Image, session):
    from rembg import remove

    return remove(img.convert("RGBA"), session=session)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--input-dir", help="Defaults to SpritePipelineWork/<asset_id>/raw_frames")
    parser.add_argument("--work-root", default="SpritePipelineWork")
    parser.add_argument("--method", choices=["rembg", "chroma", "alpha-copy"], default="rembg")
    parser.add_argument("--chroma-color", type=parse_rgb, default=(255, 0, 255))
    parser.add_argument("--tolerance", type=float, default=36.0)
    parser.add_argument("--alpha-threshold", type=int, default=8)
    parser.add_argument("--overwrite-work", action="store_true")
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    work_root = resolve_project_path(args.work_root)
    assert_not_assets_output(work_root)
    asset_root = work_root / asset_id
    input_dir = resolve_project_path(args.input_dir) if args.input_dir else asset_root / "raw_frames"
    output_dir = asset_root / "transparent_frames"

    if not input_dir.exists():
        raise SystemExit(f"Input frame directory does not exist: {input_dir}")
    prepare_output_dir(output_dir, args.overwrite_work)

    files = sorted(input_dir.glob("*.png"), key=numeric_key)
    if not files:
        raise SystemExit(f"No PNG frames found in {input_dir}")

    rembg_session = None
    if args.method == "rembg":
        from rembg import new_session

        rembg_session = new_session()

    for src in files:
        with Image.open(src) as img:
            if args.method == "rembg":
                out = rembg_remove(img, rembg_session)
            elif args.method == "chroma":
                out = chroma_remove(img, args.chroma_color, args.tolerance)
            else:
                out = alpha_copy(img, args.alpha_threshold)
            out.save(output_dir / src.name)

    manifest = {
        "asset_id": asset_id,
        "input_dir": str(input_dir),
        "transparent_frames": str(output_dir),
        "method": args.method,
        "frame_count": len(files),
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
    }
    (asset_root / "background_removal_manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8"
    )
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
