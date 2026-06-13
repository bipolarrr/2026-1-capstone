#!/usr/bin/env python3
"""Export a human-approved review selection inside SpritePipelineWork only.

This writes selected review frames to SpritePipelineWork/<asset_id>/selected/.
It refuses to write under Assets/ and does not promote runtime assets.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
import yaml


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
            "Pass --overwrite-work to replace review-export outputs."
        )
    if existing and overwrite_work:
        for item in existing:
            if item.is_file():
                item.unlink()
            else:
                raise SystemExit(f"Refusing to delete nested directory: {item}")


def read_selected_range(selection: dict) -> tuple[int, int]:
    if "selected_range" in selection and isinstance(selection["selected_range"], dict):
        selected_range = selection["selected_range"]
        start = selected_range.get("start_frame")
        end = selected_range.get("end_frame")
    elif "selected_range" in selection and isinstance(selection["selected_range"], list):
        start, end = selection["selected_range"]
    else:
        start = selection.get("selected_frame_start")
        end = selection.get("selected_frame_end")

    if start is None or end is None:
        raise SystemExit("selected_range or selected_frame_start/selected_frame_end is required.")
    start = int(start)
    end = int(end)
    if end < start:
        raise SystemExit("selected range end must be greater than or equal to start.")
    return start, end


def parse_frame_index(value, field_name: str) -> int:
    try:
        index = int(value)
    except (TypeError, ValueError):
        raise SystemExit(f"{field_name} must be a non-negative integer: {value!r}")
    if index < 0:
        raise SystemExit(f"{field_name} must be a non-negative integer: {value!r}")
    return index


def parse_span(value, field_name: str) -> tuple[int, int]:
    if isinstance(value, str):
        match = re.fullmatch(r"\s*(\d+)\s*-\s*(\d+)\s*", value)
        if not match:
            raise SystemExit(f"{field_name} span must use start-end format: {value!r}")
        start, end = parse_frame_index(match.group(1), field_name), parse_frame_index(match.group(2), field_name)
    elif isinstance(value, dict):
        start = parse_frame_index(value.get("start_frame", value.get("start")), field_name)
        end = parse_frame_index(value.get("end_frame", value.get("end")), field_name)
    elif isinstance(value, list) and len(value) == 2:
        start = parse_frame_index(value[0], field_name)
        end = parse_frame_index(value[1], field_name)
    else:
        raise SystemExit(f"{field_name} span must be start-end, [start, end], or start/end mapping.")

    if end < start:
        raise SystemExit(f"{field_name} span end must be greater than or equal to start: {value!r}")
    return start, end


def expand_span(span: tuple[int, int], frame_step: int) -> list[int]:
    start, end = span
    return list(range(start, end + 1, frame_step))


def assert_requested_frames_exist(frames: list[int], available_frames: set[int], label: str) -> None:
    missing = [frame for frame in frames if frame not in available_frames]
    if missing:
        preview = ", ".join(str(frame) for frame in missing[:12])
        suffix = "..." if len(missing) > 12 else ""
        raise SystemExit(f"{label} contains missing frame indexes: {preview}{suffix}")


def read_selected_frames(selection: dict, available_frames: set[int], frame_step: int) -> tuple[list[int], list[list[int]], list[list[int]]]:
    keep_spans_value = selection.get("keep_spans")
    remove_spans_value = selection.get("remove_spans")
    has_keep_spans = keep_spans_value not in (None, "")
    has_remove_spans = remove_spans_value not in (None, "")
    if has_keep_spans and has_remove_spans:
        raise SystemExit("Use either keep_spans or remove_spans, not both.")

    if has_keep_spans:
        if not isinstance(keep_spans_value, list) or not keep_spans_value:
            raise SystemExit("keep_spans must be a non-empty list.")
        spans = [parse_span(value, "keep_spans") for value in keep_spans_value]
        selected_frames: list[int] = []
        seen: set[int] = set()
        for span in spans:
            span_frames = expand_span(span, frame_step)
            assert_requested_frames_exist(span_frames, available_frames, "keep_spans")
            duplicates = [frame for frame in span_frames if frame in seen]
            if duplicates:
                raise SystemExit(f"keep_spans must not duplicate frame indexes: {duplicates[0]}")
            selected_frames.extend(span_frames)
            seen.update(span_frames)
        return selected_frames, [[start, end] for start, end in spans], []

    if has_remove_spans:
        if not isinstance(remove_spans_value, list) or not remove_spans_value:
            raise SystemExit("remove_spans must be a non-empty list.")
        spans = [parse_span(value, "remove_spans") for value in remove_spans_value]
        removed_frames: set[int] = set()
        for span in spans:
            span_frames = expand_span(span, 1)
            assert_requested_frames_exist(span_frames, available_frames, "remove_spans")
            removed_frames.update(span_frames)
        selected_frames = [frame for frame in sorted(available_frames) if frame not in removed_frames][::frame_step]
        return selected_frames, contiguous_ranges(selected_frames), [[start, end] for start, end in spans]

    start, end = read_selected_range(selection)
    selected_frames = list(range(start, end + 1, frame_step))
    assert_requested_frames_exist(selected_frames, available_frames, "selected_range")
    return selected_frames, [[start, end]], []


def contiguous_ranges(frames: list[int]) -> list[list[int]]:
    if not frames:
        return []
    ranges = []
    start = previous = frames[0]
    for frame in frames[1:]:
        if frame == previous + 1:
            previous = frame
        else:
            ranges.append([start, previous])
            start = previous = frame
    ranges.append([start, previous])
    return ranges


def read_hold(selection: dict) -> tuple[int, int]:
    hold = selection.get("hold")
    if isinstance(hold, dict):
        first = hold.get("duplicate_first_frame", 0)
        last = hold.get("duplicate_last_frame", 0)
    else:
        first = selection.get("duplicate_first_frame", 0)
        last = selection.get("duplicate_last_frame", 0)
    return max(0, int(first or 0)), max(0, int(last or 0))


def read_crop_policy(selection: dict) -> tuple[str, float]:
    crop_policy = selection.get("crop_policy")
    if isinstance(crop_policy, dict):
        mode = crop_policy.get("mode", "none")
        padding_percent = crop_policy.get("padding_percent", 0)
    else:
        mode = crop_policy or "none"
        padding_percent = selection.get("padding_percent", 0)
    return str(mode), float(padding_percent or 0)


def alpha_bbox(image: Image.Image, threshold: int = 8) -> tuple[int, int, int, int] | None:
    rgba = image.convert("RGBA")
    alpha = np.asarray(rgba)[:, :, 3]
    ys, xs = np.where(alpha > threshold)
    if xs.size == 0 or ys.size == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def common_canvas_bbox(images: list[Image.Image], padding_percent: float) -> tuple[int, int, int, int]:
    bboxes = [alpha_bbox(image) for image in images]
    bboxes = [bbox for bbox in bboxes if bbox is not None]
    if not bboxes:
        width, height = images[0].size
        return 0, 0, width, height

    x0 = min(bbox[0] for bbox in bboxes)
    y0 = min(bbox[1] for bbox in bboxes)
    x1 = max(bbox[2] for bbox in bboxes)
    y1 = max(bbox[3] for bbox in bboxes)
    width = x1 - x0
    height = y1 - y0
    pad = int(round(max(width, height) * (padding_percent / 100.0)))
    return x0 - pad, y0 - pad, x1 + pad, y1 + pad


def crop_or_pad_to_bbox(image: Image.Image, bbox: tuple[int, int, int, int]) -> Image.Image:
    rgba = image.convert("RGBA")
    x0, y0, x1, y1 = bbox
    out_w = x1 - x0
    out_h = y1 - y0
    if out_w <= 0 or out_h <= 0:
        raise SystemExit(f"Invalid common canvas bbox: {bbox}")

    output = Image.new("RGBA", (out_w, out_h), (0, 0, 0, 0))
    src_x0 = max(0, x0)
    src_y0 = max(0, y0)
    src_x1 = min(rgba.width, x1)
    src_y1 = min(rgba.height, y1)
    if src_x1 > src_x0 and src_y1 > src_y0:
        crop = rgba.crop((src_x0, src_y0, src_x1, src_y1))
        output.alpha_composite(crop, (src_x0 - x0, src_y0 - y0))
    return output


def checkerboard(size: tuple[int, int], tile: int = 12) -> Image.Image:
    img = Image.new("RGBA", size, (255, 255, 255, 255))
    draw = ImageDraw.Draw(img)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            color = (210, 210, 210, 255) if ((x // tile + y // tile) % 2) else (245, 245, 245, 255)
            draw.rectangle([x, y, x + tile - 1, y + tile - 1], fill=color)
    return img


def composite_frame(path: Path, max_size: int) -> Image.Image:
    with Image.open(path) as img:
        rgba = img.convert("RGBA")
    rgba.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
    bg = checkerboard((max_size, max_size))
    x = (max_size - rgba.width) // 2
    y = (max_size - rgba.height) // 2
    bg.alpha_composite(rgba, (x, y))
    return bg.convert("RGB")


def make_gif(paths: list[Path], out_path: Path, playback_fps: float) -> None:
    if not paths:
        return
    duration_ms = max(1, int(round(1000.0 / max(1.0, playback_fps))))
    frames = [composite_frame(path, 256) for path in paths]
    frames[0].save(
        out_path,
        save_all=True,
        append_images=frames[1:],
        duration=duration_ms,
        loop=0,
        optimize=False,
    )


def make_contact_sheet(paths: list[Path], out_path: Path, columns: int = 8, thumb: int = 128) -> None:
    if not paths:
        return
    rows = (len(paths) + columns - 1) // columns
    header_h = 28
    label_h = 20
    sheet = Image.new("RGB", (columns * thumb, header_h + rows * (thumb + label_h)), (32, 32, 32))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 7), "selected review frames", fill=(255, 255, 255))
    for i, path in enumerate(paths):
        col = i % columns
        row = i // columns
        x = col * thumb
        y = header_h + row * (thumb + label_h)
        sheet.paste(composite_frame(path, thumb), (x, y))
        draw.text((x + 4, y + thumb + 3), path.stem, fill=(255, 255, 255))
    sheet.save(out_path)


def write_unity_copy_plan(path: Path, manifest: dict) -> None:
    lines = [
        "# Unity Copy Plan",
        "",
        "This is a review-only plan. Do not copy these frames into `Assets/` until a separate runtime import task is approved.",
        "",
        f"- Asset ID: `{manifest['asset_id']}`",
        f"- Usage type: `{manifest['usage_type']}`",
        f"- Selected output: `{manifest['output_dir']}`",
        f"- Source directory: `{manifest['source_dir']}`",
        f"- Source ranges: `{', '.join(f'{start}-{end}' for start, end in manifest['selected_ranges'])}`",
        f"- Removed ranges: `{', '.join(f'{start}-{end}' for start, end in manifest['removed_ranges']) or 'none'}`",
        f"- Frame step: `{manifest['frame_step']}`",
        f"- Playback FPS: `{manifest['playback_fps']}`",
        f"- Hold duplicates: first `{manifest['duplicate_first_frame']}`, last `{manifest['duplicate_last_frame']}`",
        f"- Crop policy: `{manifest['crop_policy']}`",
        f"- Padding percent: `{manifest['padding_percent']}`",
        f"- Review frame count: `{manifest['frame_count']}`",
        "",
        "Before any future runtime import:",
        "",
        "- Confirm selected frames visually in `selected_preview.gif` and `selected_contact_sheet.png`.",
        "- Confirm target runtime path and expected frame count from builder/stage code.",
        "- Preserve existing assets and `.meta` files unless a separate task explicitly allows replacement/import.",
        "- Do not hand-edit scenes or prefabs.",
    ]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--work-root", default="SpritePipelineWork")
    parser.add_argument("--selection-file", help="Defaults to human_selection.yaml in the asset work folder")
    parser.add_argument("--source-dir", help="Defaults to transparent_frames, then raw_frames")
    parser.add_argument("--output-dir", help="Defaults to selected in the asset work folder")
    parser.add_argument("--preserve-frame-numbers", action="store_true")
    parser.add_argument("--overwrite-work", action="store_true")
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    work_root = resolve_project_path(args.work_root)
    assert_not_assets_output(work_root)
    asset_root = work_root / asset_id
    selection_file = resolve_project_path(args.selection_file) if args.selection_file else asset_root / "human_selection.yaml"
    if not selection_file.exists():
        raise SystemExit(f"Selection file does not exist: {selection_file}")

    selection = yaml.safe_load(selection_file.read_text(encoding="utf-8")) or {}
    if selection.get("approved_for_review_export") is not True:
        raise SystemExit("approved_for_review_export must be true before exporting selected review frames.")

    usage_type = selection.get("usage_type")
    if usage_type is None:
        raise SystemExit("usage_type is required.")
    frame_step = max(1, int(selection.get("frame_step", 1) or 1))
    playback_fps = float(selection.get("playback_fps", 12) or 12)
    duplicate_first_frame, duplicate_last_frame = read_hold(selection)
    crop_policy, padding_percent = read_crop_policy(selection)

    source_dir = resolve_project_path(args.source_dir) if args.source_dir else asset_root / "transparent_frames"
    if not source_dir.exists():
        source_dir = asset_root / "raw_frames"
    if not source_dir.exists():
        raise SystemExit(f"Source frame directory does not exist: {source_dir}")

    output_dir = resolve_project_path(args.output_dir) if args.output_dir else asset_root / "selected"
    prepare_output_dir(output_dir, args.overwrite_work)

    available_frames = {int(path.stem) for path in source_dir.glob("*.png") if path.stem.isdigit()}
    if not available_frames:
        raise SystemExit(f"No numeric PNG frames found in {source_dir}")
    selected_source_frames, selected_ranges, removed_ranges = read_selected_frames(selection, available_frames, frame_step)
    if not selected_source_frames:
        raise SystemExit("Selection produced no frames.")

    source_images: list[tuple[int, Image.Image]] = []
    for frame_index in selected_source_frames:
        src = source_dir / f"{frame_index}.png"
        if not src.exists():
            raise SystemExit(f"Selected frame is missing: {src}")
        source_images.append((frame_index, Image.open(src).convert("RGBA")))

    if crop_policy == "common_canvas":
        bbox = common_canvas_bbox([img for _, img in source_images], padding_percent)
    else:
        bbox = None

    expanded_frames: list[tuple[int, Image.Image, str]] = []
    first_frame = source_images[0]
    last_frame = source_images[-1]
    for _ in range(duplicate_first_frame):
        expanded_frames.append((first_frame[0], first_frame[1].copy(), "duplicate_first"))
    for frame_index, img in source_images:
        expanded_frames.append((frame_index, img, "selected"))
    for _ in range(duplicate_last_frame):
        expanded_frames.append((last_frame[0], last_frame[1].copy(), "duplicate_last"))

    copied = []
    output_paths: list[Path] = []
    for i, (frame_index, img, role) in enumerate(expanded_frames):
        dst_name = f"{frame_index}.png" if args.preserve_frame_numbers else f"{i}.png"
        dst = output_dir / dst_name
        out_img = crop_or_pad_to_bbox(img, bbox) if bbox else img
        out_img.save(dst)
        output_paths.append(dst)
        copied.append({"source_frame": frame_index, "output": str(dst), "role": role})

    selected_preview = output_dir / "selected_preview.gif"
    selected_contact_sheet = output_dir / "selected_contact_sheet.png"
    selected_manifest = output_dir / "selected_manifest.json"
    unity_copy_plan = output_dir / "unity_copy_plan.md"
    make_gif(output_paths, selected_preview, playback_fps)
    make_contact_sheet(output_paths, selected_contact_sheet)

    manifest = {
        "asset_id": asset_id,
        "usage_type": usage_type,
        "source_dir": str(source_dir),
        "output_dir": str(output_dir),
        "selected_range": [selected_source_frames[0], selected_source_frames[-1]],
        "selected_ranges": selected_ranges,
        "removed_ranges": removed_ranges,
        "frame_step": frame_step,
        "playback_fps": playback_fps,
        "duplicate_first_frame": duplicate_first_frame,
        "duplicate_last_frame": duplicate_last_frame,
        "crop_policy": crop_policy,
        "padding_percent": padding_percent,
        "common_canvas_bbox": list(bbox) if bbox else None,
        "preserve_frame_numbers": args.preserve_frame_numbers,
        "frame_count": len(copied),
        "copied_frames": copied,
        "selected_preview": str(selected_preview),
        "selected_contact_sheet": str(selected_contact_sheet),
        "selected_manifest": str(selected_manifest),
        "unity_copy_plan": str(unity_copy_plan),
        "review_export_only": True,
        "runtime_import_approval": False,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
    }
    selected_manifest.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_unity_copy_plan(unity_copy_plan, manifest)
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
