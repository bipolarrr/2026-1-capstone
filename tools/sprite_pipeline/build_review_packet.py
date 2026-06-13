#!/usr/bin/env python3
"""Build contact sheets, timeline plot, GIF previews, candidate spans, and human_selection.yaml."""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

import matplotlib.pyplot as plt
from PIL import Image, ImageDraw, ImageFont


USAGE_TYPES = {
    "idle_loop",
    "one_shot_attack",
    "hit_reaction",
    "death_once_hold_last",
    "jump_once",
    "debuff_loop",
    "static_placeholder",
    "reject_regenerate",
}


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


def infer_usage_type(asset_id: str) -> str:
    key = asset_id.lower()
    if "dead" in key or "death" in key or "die" in key:
        return "death_once_hold_last"
    if "hit" in key:
        return "hit_reaction"
    if "attack" in key:
        return "one_shot_attack"
    if "jump" in key:
        return "jump_once"
    if "debuff" in key:
        return "debuff_loop"
    if "idle" in key:
        return "idle_loop"
    if "icon" in key or "dice_roll_0" in key or "placeholder" in key:
        return "static_placeholder"
    return "one_shot_attack"


def load_metrics(asset_root: Path) -> dict:
    path = asset_root / "metrics.json"
    if not path.exists():
        raise SystemExit(f"Missing metrics.json. Run analyze_frames.py first: {path}")
    return json.loads(path.read_text(encoding="utf-8"))


def frame_rejection_reason(metric: dict) -> str | None:
    reasons = []
    if metric.get("blank_frame"):
        reasons.append("blank_frame")
    if metric.get("likely_background_residue"):
        reasons.append("likely_background_residue")
    if metric.get("crop_risk"):
        reasons.append("edge_contact_crop_risk")
    if metric.get("scale_jump"):
        reasons.append("scale_jump")
    if metric.get("center_jump"):
        reasons.append("center_jump")
    return ", ".join(reasons) if reasons else None


def contiguous_ranges(indices: list[int]) -> list[list[int]]:
    if not indices:
        return []
    indices = sorted(indices)
    ranges = []
    start = prev = indices[0]
    for idx in indices[1:]:
        if idx == prev + 1:
            prev = idx
        else:
            ranges.append([start, prev])
            start = prev = idx
    ranges.append([start, prev])
    return ranges


def propose_spans(asset_id: str, metrics: list[dict], usage_type: str) -> dict:
    frame_indices = [m["frame_index"] for m in metrics]
    rejected = [
        {"frame_index": m["frame_index"], "reason": frame_rejection_reason(m)}
        for m in metrics
        if frame_rejection_reason(m)
    ]
    rejected_set = {r["frame_index"] for r in rejected}
    valid = [idx for idx in frame_indices if idx not in rejected_set]
    if not valid:
        rejected_ranges = contiguous_ranges(frame_indices)
        return {
            "asset_id": asset_id,
            "usage_type_suggestion": "reject_regenerate",
            "candidates": [],
            "label_summary": {
                "idle_pre_roll": [],
                "active_action": [],
                "recovery": [],
                "unstable_tail": [],
                "rejected": rejected_ranges,
            },
            "frame_labels": [
                {"range": span, "label": "rejected", "reason": "no valid frames"} for span in rejected_ranges
            ],
            "rejected_frames": rejected,
        }

    movement = [
        m["frame_index"]
        for m in metrics
        if m.get("previous_frame_diff") is not None and m.get("previous_frame_diff", 0) >= 0.012
    ]
    first_valid, last_valid = min(valid), max(valid)
    first_active = min(movement) if movement else first_valid
    last_active = max(movement) if movement else last_valid

    candidates = []
    candidates.append(
        {
            "candidate_id": "candidate_01",
            "range": [first_valid, last_valid],
            "suggested_usage_type": usage_type,
            "reason": "Broadest contiguous usable review span after excluding hard rejects.",
            "not_final": True,
        }
    )
    action_start = max(first_valid, first_active - 2)
    action_end = min(last_valid, last_active + 4)
    if [action_start, action_end] != [first_valid, last_valid] and action_start <= action_end:
        candidates.append(
            {
                "candidate_id": "candidate_02",
                "range": [action_start, action_end],
                "suggested_usage_type": usage_type,
                "reason": "Motion-focused span around active frame differences.",
                "not_final": True,
            }
        )
    if usage_type in {"idle_loop", "debuff_loop"}:
        stable = [m["frame_index"] for m in metrics if m.get("duplicate_or_hold_candidate") and m["frame_index"] in valid]
        stable_ranges = contiguous_ranges(stable)
        if stable_ranges:
            candidates.append(
                {
                    "candidate_id": "candidate_03",
                    "range": stable_ranges[0],
                    "suggested_usage_type": usage_type,
                    "reason": "Stable low-diff loop candidate.",
                    "not_final": True,
                }
            )
    candidates = candidates[:3]

    label_summary = {
        "idle_pre_roll": [],
        "active_action": [],
        "recovery": [],
        "unstable_tail": [],
        "rejected": contiguous_ranges(sorted(rejected_set)),
    }
    if first_valid < first_active:
        label_summary["idle_pre_roll"].append([first_valid, first_active - 1])
    label_summary["active_action"].append([first_active, last_active])
    if last_active < last_valid:
        label_summary["recovery"].append([last_active + 1, last_valid])

    tail_candidates = [
        m["frame_index"]
        for m in metrics
        if m["frame_index"] > last_active and (m.get("center_jump") or m.get("scale_jump"))
    ]
    label_summary["unstable_tail"] = contiguous_ranges(tail_candidates)

    labels = []
    for label_name in ["idle_pre_roll", "active_action", "recovery", "unstable_tail", "rejected"]:
        for span in label_summary[label_name]:
            entry = {"range": span, "label": label_name}
            if label_name == "rejected":
                entry["reason"] = "see rejected_frames"
            labels.append(entry)

    return {
        "asset_id": asset_id,
        "usage_type_suggestion": usage_type,
        "candidate_count": len(candidates),
        "candidates": candidates,
        "label_summary": label_summary,
        "frame_labels": labels,
        "rejected_frames": rejected,
    }


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


def build_contact_sheet(paths: list[Path], out_path: Path, title: str, columns: int = 8, thumb: int = 128) -> None:
    if not paths:
        return
    rows = math.ceil(len(paths) / columns)
    header_h = 28
    label_h = 20
    sheet = Image.new("RGB", (columns * thumb, header_h + rows * (thumb + label_h)), (32, 32, 32))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 7), title, fill=(255, 255, 255))
    for i, path in enumerate(paths):
        col = i % columns
        row = i // columns
        x = col * thumb
        y = header_h + row * (thumb + label_h)
        frame = composite_frame(path, thumb)
        sheet.paste(frame, (x, y))
        draw.text((x + 4, y + thumb + 3), path.stem, fill=(255, 255, 255))
    sheet.save(out_path)


def make_gif(paths: list[Path], out_path: Path, max_size: int = 256, duration_ms: int = 70) -> None:
    if not paths:
        return
    frames = [composite_frame(path, max_size) for path in paths]
    frames[0].save(
        out_path,
        save_all=True,
        append_images=frames[1:],
        duration=duration_ms,
        loop=0,
        optimize=False,
    )


def plot_timeline(metrics: list[dict], out_path: Path) -> None:
    xs = [m["frame_index"] for m in metrics]
    area = [m.get("bbox_area_ratio") or 0 for m in metrics]
    diff = [m.get("previous_frame_diff") or 0 for m in metrics]
    center = [m.get("bbox_center_delta") or 0 for m in metrics]
    flags = [1 if m.get("crop_risk") or m.get("blank_frame") or m.get("likely_background_residue") else 0 for m in metrics]

    plt.figure(figsize=(12, 6))
    plt.plot(xs, area, label="bbox_area_ratio")
    plt.plot(xs, diff, label="previous_frame_diff")
    plt.plot(xs, center, label="bbox_center_delta")
    plt.fill_between(xs, 0, flags, color="red", alpha=0.15, label="risk flag")
    plt.xlabel("frame_index")
    plt.legend()
    plt.tight_layout()
    plt.savefig(out_path)
    plt.close()


def paths_for_range(frame_paths: dict[int, Path], span: list[int]) -> list[Path]:
    start, end = span
    return [frame_paths[i] for i in range(start, end + 1) if i in frame_paths]


def write_human_selection(asset_root: Path, asset_id: str, spans: dict) -> None:
    candidates = spans.get("candidates", [])
    candidate_lines = "\n".join(
        f"# - {c['candidate_id']}: frames {c['range'][0]}-{c['range'][1]}, {c['suggested_usage_type']}"
        for c in candidates
    )
    text = f"""# Human frame-selection review file.
# Edit this file after reviewing contact sheets, GIFs, metrics, and candidate_spans.json.
# This file is not runtime approval and must not be used to write into Assets/.
#
# Candidate examples:
{candidate_lines if candidate_lines else "# - no candidate spans generated"}

asset_id: {asset_id}
selected_candidate_id:
selected_frame_start:
selected_frame_end:
usage_type:
# Allowed usage_type values:
# - idle_loop
# - one_shot_attack
# - hit_reaction
# - death_once_hold_last
# - jump_once
# - debuff_loop
# - static_placeholder
# - reject_regenerate
approved_for_review_export: false
notes:
"""
    (asset_root / "human_selection.yaml").write_text(text, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--work-root", default="SpritePipelineWork")
    parser.add_argument("--frames-dir", help="Defaults to transparent_frames, then raw_frames")
    parser.add_argument("--usage-type", choices=sorted(USAGE_TYPES))
    parser.add_argument("--gif-step", type=int, default=1)
    parser.add_argument("--max-gif-frames", type=int, default=160)
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    work_root = resolve_project_path(args.work_root)
    assert_not_assets_output(work_root)
    asset_root = work_root / asset_id
    metrics_payload = load_metrics(asset_root)
    metrics = metrics_payload["metrics"]

    frames_dir = resolve_project_path(args.frames_dir) if args.frames_dir else asset_root / "transparent_frames"
    if not frames_dir.exists():
        frames_dir = asset_root / "raw_frames"
    if not frames_dir.exists():
        raise SystemExit(f"Frame directory does not exist: {frames_dir}")

    frame_paths = {int(path.stem): path for path in sorted(frames_dir.glob("*.png"), key=numeric_key) if path.stem.isdigit()}
    if not frame_paths:
        raise SystemExit(f"No numeric PNG frames found in {frames_dir}")

    usage_type = args.usage_type or infer_usage_type(asset_id)
    spans = propose_spans(asset_id, metrics, usage_type)
    spans["generated_at_utc"] = datetime.now(timezone.utc).isoformat()
    spans["frames_dir"] = str(frames_dir)
    (asset_root / "candidate_spans.json").write_text(json.dumps(spans, indent=2), encoding="utf-8")

    all_paths = [frame_paths[i] for i in sorted(frame_paths)]
    build_contact_sheet(all_paths, asset_root / "contact_sheet_all.png", f"{asset_id} all frames")

    candidate_paths: list[Path] = []
    if spans["candidates"]:
        candidate_paths = paths_for_range(frame_paths, spans["candidates"][0]["range"])
    build_contact_sheet(candidate_paths, asset_root / "contact_sheet_candidates.png", f"{asset_id} candidate_01")
    plot_timeline(metrics, asset_root / "timeline_metrics.png")

    gif_paths = all_paths[:: max(1, args.gif_step)][: args.max_gif_frames]
    make_gif(gif_paths, asset_root / "preview_all.gif")
    make_gif(candidate_paths[: args.max_gif_frames], asset_root / "preview_candidate_01.gif")
    write_human_selection(asset_root, asset_id, spans)

    print(f"wrote {asset_root / 'candidate_spans.json'}")
    print(f"wrote {asset_root / 'human_selection.yaml'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
