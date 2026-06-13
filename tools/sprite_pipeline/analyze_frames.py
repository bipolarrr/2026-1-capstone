#!/usr/bin/env python3
"""Analyze sprite frame metrics and write metrics.csv plus metrics.json."""

from __future__ import annotations

import argparse
import csv
import json
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


METRIC_FIELDS = [
    "frame_index",
    "source_time",
    "width",
    "height",
    "alpha_present",
    "alpha_bbox",
    "bbox_area_ratio",
    "bbox_center",
    "bbox_center_delta",
    "bbox_width_delta",
    "bbox_height_delta",
    "opaque_pixel_count",
    "edge_touch_flags",
    "component_count",
    "previous_frame_diff",
    "blank_frame",
    "crop_risk",
    "scale_jump",
    "center_jump",
    "likely_background_residue",
    "duplicate_or_hold_candidate",
]


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


def numeric_key(path: Path) -> tuple[int, str]:
    match = re.search(r"(\d+)(?=\.[^.]+$)", path.name)
    return (int(match.group(1)) if match else 10**9, path.name.lower())


def parse_index(path: Path, fallback: int) -> int:
    match = re.search(r"(\d+)(?=\.[^.]+$)", path.name)
    return int(match.group(1)) if match else fallback


def bbox_from_mask(mask: np.ndarray) -> tuple[int, int, int, int] | None:
    ys, xs = np.where(mask)
    if xs.size == 0 or ys.size == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def connected_component_count(mask: np.ndarray) -> int:
    if not mask.any():
        return 0
    count, _ = cv2.connectedComponents(mask.astype(np.uint8), connectivity=8)
    return max(0, int(count) - 1)


def edge_flags(bbox: tuple[int, int, int, int] | None, width: int, height: int, pad: int) -> list[str]:
    if bbox is None:
        return []
    x0, y0, x1, y1 = bbox
    flags: list[str] = []
    if x0 <= pad:
        flags.append("left")
    if y0 <= pad:
        flags.append("top")
    if x1 >= width - pad:
        flags.append("right")
    if y1 >= height - pad:
        flags.append("bottom")
    return flags


def normalized_frame_diff(current: np.ndarray, previous: np.ndarray | None) -> float | None:
    if previous is None or previous.shape != current.shape:
        return None
    return float(np.mean(np.abs(current.astype(np.int16) - previous.astype(np.int16))) / 255.0)


def analyze_frame(
    path: Path,
    sequence_position: int,
    fps: float,
    alpha_threshold: int,
    edge_padding: int,
    previous_arr: np.ndarray | None,
    previous_bbox: tuple[int, int, int, int] | None,
) -> tuple[dict, np.ndarray, tuple[int, int, int, int] | None]:
    with Image.open(path) as img:
        alpha_present = "A" in img.getbands()
        rgba_img = img.convert("RGBA")
    arr = np.asarray(rgba_img)
    alpha = arr[:, :, 3]
    width, height = rgba_img.size
    mask = alpha > alpha_threshold
    bbox = bbox_from_mask(mask)

    if bbox is None:
        bbox_area_ratio = 0.0
        bbox_center = None
        bbox_width = 0
        bbox_height = 0
    else:
        x0, y0, x1, y1 = bbox
        bbox_width = x1 - x0
        bbox_height = y1 - y0
        bbox_area_ratio = (bbox_width * bbox_height) / float(width * height)
        bbox_center = ((x0 + x1) / 2.0, (y0 + y1) / 2.0)

    previous_diff = normalized_frame_diff(arr, previous_arr)
    if bbox and previous_bbox:
        px0, py0, px1, py1 = previous_bbox
        previous_center = ((px0 + px1) / 2.0, (py0 + py1) / 2.0)
        center_delta = math.dist(bbox_center, previous_center) if bbox_center else None
        width_delta = bbox_width - (px1 - px0)
        height_delta = bbox_height - (py1 - py0)
    else:
        center_delta = None
        width_delta = None
        height_delta = None

    flags = edge_flags(bbox, width, height, edge_padding)
    opaque_count = int(mask.sum())
    blank_frame = opaque_count == 0
    low_alpha = (alpha > 0) & (alpha <= alpha_threshold)
    residue_ratio = float(low_alpha.sum() / max(1, width * height))
    likely_residue = bool(bbox_area_ratio > 0.92 and len(flags) >= 3) or residue_ratio > 0.08
    crop_risk = bool(flags)
    scale_jump = bool(
        width_delta is not None
        and height_delta is not None
        and (abs(width_delta) > width * 0.16 or abs(height_delta) > height * 0.16)
    )
    center_jump = bool(center_delta is not None and center_delta > max(width, height) * 0.08)
    duplicate = bool(previous_diff is not None and previous_diff < 0.004)

    metric = {
        "frame_index": parse_index(path, sequence_position),
        "source_time": round(sequence_position / fps, 6) if fps > 0 else None,
        "width": width,
        "height": height,
        "alpha_present": bool(alpha_present),
        "alpha_bbox": list(bbox) if bbox else None,
        "bbox_area_ratio": round(bbox_area_ratio, 6),
        "bbox_center": [round(bbox_center[0], 3), round(bbox_center[1], 3)] if bbox_center else None,
        "bbox_center_delta": round(center_delta, 6) if center_delta is not None else None,
        "bbox_width_delta": width_delta,
        "bbox_height_delta": height_delta,
        "opaque_pixel_count": opaque_count,
        "edge_touch_flags": flags,
        "component_count": connected_component_count(mask),
        "previous_frame_diff": round(previous_diff, 6) if previous_diff is not None else None,
        "blank_frame": blank_frame,
        "crop_risk": crop_risk,
        "scale_jump": scale_jump,
        "center_jump": center_jump,
        "likely_background_residue": likely_residue,
        "duplicate_or_hold_candidate": duplicate,
    }
    return metric, arr, bbox


def csv_value(value):
    if isinstance(value, (list, dict)):
        return json.dumps(value, separators=(",", ":"))
    if value is None:
        return ""
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--frames-dir", help="Defaults to transparent_frames, then raw_frames")
    parser.add_argument("--work-root", default="SpritePipelineWork")
    parser.add_argument("--fps", type=float, default=24.0)
    parser.add_argument("--alpha-threshold", type=int, default=8)
    parser.add_argument("--edge-padding", type=int, default=0)
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    work_root = resolve_project_path(args.work_root)
    assert_not_assets_output(work_root)
    asset_root = work_root / asset_id
    frames_dir = resolve_project_path(args.frames_dir) if args.frames_dir else asset_root / "transparent_frames"
    if not frames_dir.exists():
        fallback = asset_root / "raw_frames"
        if fallback.exists():
            frames_dir = fallback
    if not frames_dir.exists():
        raise SystemExit(f"Frame directory does not exist: {frames_dir}")

    files = sorted(frames_dir.glob("*.png"), key=numeric_key)
    if not files:
        raise SystemExit(f"No PNG frames found in {frames_dir}")

    metrics = []
    previous_arr = None
    previous_bbox = None
    for position, path in enumerate(files):
        metric, previous_arr, previous_bbox = analyze_frame(
            path, position, args.fps, args.alpha_threshold, args.edge_padding, previous_arr, previous_bbox
        )
        metrics.append(metric)

    asset_root.mkdir(parents=True, exist_ok=True)
    csv_path = asset_root / "metrics.csv"
    json_path = asset_root / "metrics.json"
    with csv_path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=METRIC_FIELDS)
        writer.writeheader()
        for metric in metrics:
            writer.writerow({field: csv_value(metric.get(field)) for field in METRIC_FIELDS})

    payload = {
        "asset_id": asset_id,
        "frames_dir": str(frames_dir),
        "frame_count": len(metrics),
        "fps": args.fps,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "metrics": metrics,
    }
    json_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"wrote {csv_path}")
    print(f"wrote {json_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
