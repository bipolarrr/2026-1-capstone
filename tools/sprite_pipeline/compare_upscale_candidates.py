#!/usr/bin/env python3
"""Build comparison artifacts for upscaled runtime candidates."""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


METHOD_NOTES = {
    "nearest": {
        "strength": "Best preserves hard pixel edges and outlines.",
        "risk": "Can look blocky when the source is truly enlarged by a non-integer scale.",
    },
    "lanczos": {
        "strength": "Smoothest interpolation and can reduce stair-step artifacts.",
        "risk": "Can soften pixel-art edges and introduce color or alpha fringe near outlines.",
    },
    "bicubic": {
        "strength": "Middle-ground interpolation between nearest and lanczos.",
        "risk": "Can still blur outlines and may not preserve pixel clusters as well as nearest.",
    },
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


def numeric_key(path: Path) -> tuple[int, str]:
    match = re.search(r"(\d+)(?=\.[^.]+$)", path.name)
    return (int(match.group(1)) if match else 10**9, path.name.lower())


def numeric_pngs(path: Path) -> list[Path]:
    if not path.exists() or not path.is_dir():
        raise SystemExit(f"Frame directory does not exist: {path}")
    frames = [
        item
        for item in path.iterdir()
        if item.is_file() and item.suffix.lower() == ".png" and numeric_key(item)[0] != 10**9
    ]
    frames = sorted(frames, key=numeric_key)
    if not frames:
        raise SystemExit(f"No numeric PNG frames found in {path}")
    return frames


def audit_folder(path: Path) -> dict:
    frames = numeric_pngs(path)
    sizes: dict[tuple[int, int], int] = {}
    alpha_counts: dict[bool, int] = {}
    first_five = []
    widths = []
    heights = []
    for index, frame in enumerate(frames):
        with Image.open(frame) as img:
            size = img.size
            alpha = "A" in img.getbands()
        sizes[size] = sizes.get(size, 0) + 1
        alpha_counts[alpha] = alpha_counts.get(alpha, 0) + 1
        widths.append(size[0])
        heights.append(size[1])
        if index < 5:
            first_five.append({"file": frame.name, "width": size[0], "height": size[1], "alpha": alpha})

    unique_sizes = [
        {"width": width, "height": height, "count": count}
        for (width, height), count in sorted(sizes.items(), key=lambda item: (-item[1], item[0]))
    ]
    most = unique_sizes[0]
    return {
        "path": rel(path),
        "pngCount": len(frames),
        "uniqueSizes": unique_sizes,
        "mostCommonSize": most,
        "firstPngSize": {"width": first_five[0]["width"], "height": first_five[0]["height"]},
        "minWidth": min(widths),
        "maxWidth": max(widths),
        "minHeight": min(heights),
        "maxHeight": max(heights),
        "alphaCounts": {str(key).lower(): value for key, value in sorted(alpha_counts.items())},
        "firstFive": first_five,
    }


def choose_frame_indices(paths: list[Path], max_frames: int) -> tuple[list[int], str]:
    indexes = [numeric_key(path)[0] for path in paths]
    if len(indexes) <= max_frames:
        return indexes, f"Included all {len(indexes)} numeric selected frames."
    sample_count = max(8, max_frames)
    selected = []
    for i in range(sample_count):
        pos = round(i * (len(indexes) - 1) / max(1, sample_count - 1))
        selected.append(indexes[pos])
    return sorted(set(selected)), f"Evenly sampled {len(set(selected))} frames across the selected sequence."


def checkerboard(size: tuple[int, int], tile: int = 16) -> Image.Image:
    img = Image.new("RGBA", size, (255, 255, 255, 255))
    draw = ImageDraw.Draw(img)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            color = (64, 64, 64, 255) if ((x // tile + y // tile) % 2) else (28, 28, 28, 255)
            draw.rectangle([x, y, x + tile - 1, y + tile - 1], fill=color)
    return img


def render_thumb(path: Path, thumb: int) -> Image.Image:
    with Image.open(path) as img:
        rgba = img.convert("RGBA")
    rgba.thumbnail((thumb, thumb), Image.Resampling.NEAREST)
    bg = checkerboard((thumb, thumb))
    x = (thumb - rgba.width) // 2
    y = (thumb - rgba.height) // 2
    bg.alpha_composite(rgba, (x, y))
    return bg.convert("RGB")


def make_contact_sheet(
    out_path: Path,
    selected_by_index: dict[int, Path],
    candidate_by_method: dict[str, dict[int, Path]],
    frame_indices: list[int],
    methods: list[str],
) -> None:
    thumb = 160
    label_h = 22
    header_h = 34
    row_h = thumb + label_h
    columns = ["selected"] + methods
    sheet = Image.new("RGB", (len(columns) * thumb, header_h + len(frame_indices) * row_h), (24, 24, 24))
    draw = ImageDraw.Draw(sheet)
    for col, label in enumerate(columns):
        draw.text((col * thumb + 6, 10), label, fill=(255, 255, 255))
    for row, frame_index in enumerate(frame_indices):
        y = header_h + row * row_h
        paths = [selected_by_index[frame_index]] + [candidate_by_method[method][frame_index] for method in methods]
        for col, path in enumerate(paths):
            x = col * thumb
            sheet.paste(render_thumb(path, thumb), (x, y))
            draw.text((x + 6, y + thumb + 3), f"{frame_index}.png", fill=(255, 255, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def alpha_preserved(paths: list[Path]) -> bool:
    for path in paths:
        with Image.open(path) as img:
            if "A" not in img.getbands():
                return False
    return True


def normalized_diff(left: Path, right: Path) -> float | None:
    with Image.open(left) as left_img, Image.open(right) as right_img:
        a = left_img.convert("RGBA")
        b = right_img.convert("RGBA")
    if a.size != b.size:
        return None
    diff = ImageChops.difference(a, b)
    hist = diff.histogram()
    total = sum(value * (index % 256) for index, value in enumerate(hist))
    return total / float(a.width * a.height * 4 * 255)


def method_summary(method: str, selected_by_index: dict[int, Path], candidate_frames: list[Path]) -> dict:
    by_index = {numeric_key(path)[0]: path for path in candidate_frames}
    diffs = []
    for frame_index, selected_path in selected_by_index.items():
        candidate = by_index.get(frame_index)
        if candidate is None:
            continue
        value = normalized_diff(selected_path, candidate)
        if value is not None:
            diffs.append(value)
    report_path = candidate_frames[0].parents[1] / "upscale_candidate_report.json"
    target = None
    if report_path.exists():
        payload = json.loads(report_path.read_text(encoding="utf-8"))
        target = {"width": payload.get("targetWidth"), "height": payload.get("targetHeight")}
    return {
        "method": method,
        "frameCount": len(candidate_frames),
        "alphaPreserved": alpha_preserved(candidate_frames),
        "outputFolder": rel(candidate_frames[0].parent),
        "targetSize": target,
        "meanNormalizedDiffVsSelected": round(sum(diffs) / len(diffs), 8) if diffs else None,
        "maxNormalizedDiffVsSelected": round(max(diffs), 8) if diffs else None,
        "expectedStrength": METHOD_NOTES.get(method, {}).get("strength", ""),
        "expectedRisk": METHOD_NOTES.get(method, {}).get("risk", ""),
    }


def write_markdown_report(path: Path, report: dict) -> None:
    lines = [
        "# Upscale Comparison Report",
        "",
        f"- Asset ID: `{report['assetId']}`",
        f"- Target size: `{report['targetSize']['width']}x{report['targetSize']['height']}`",
        f"- Target size basis: {report['targetSizeBasis']}",
        f"- Contact sheet: `{report['contactSheet']}`",
        f"- Recommendation: `{report['recommendedMethod']}`",
        f"- Recommendation reason: {report['recommendationReason']}",
        "",
        "## Reference Size Audit",
        "",
    ]
    for item in report["referenceSizeAudit"]:
        sizes = ", ".join(f"{s['width']}x{s['height']}={s['count']}" for s in item["uniqueSizes"])
        alpha = ", ".join(f"{key}={value}" for key, value in item["alphaCounts"].items())
        first_five = "; ".join(f"{f['file']}:{f['width']}x{f['height']}:alpha={str(f['alpha']).lower()}" for f in item["firstFive"])
        lines.extend(
            [
                f"### `{item['path']}`",
                "",
                f"- PNG count: `{item['pngCount']}`",
                f"- Unique sizes: `{sizes}`",
                f"- Most common size: `{item['mostCommonSize']['width']}x{item['mostCommonSize']['height']} ({item['mostCommonSize']['count']})`",
                f"- First PNG size: `{item['firstPngSize']['width']}x{item['firstPngSize']['height']}`",
                f"- Width range: `{item['minWidth']}..{item['maxWidth']}`",
                f"- Height range: `{item['minHeight']}..{item['maxHeight']}`",
                f"- Alpha: `{alpha}`",
                f"- First five: `{first_five}`",
                "",
            ]
        )

    lines.extend(["## Method Results", ""])
    for method in report["methods"]:
        lines.extend(
            [
                f"### `{method['method']}`",
                "",
                f"- Output folder: `{method['outputFolder']}`",
                f"- Generated frames: `{method['frameCount']}`",
                f"- Alpha preserved: `{str(method['alphaPreserved']).lower()}`",
                f"- Mean normalized diff vs selected: `{method['meanNormalizedDiffVsSelected']}`",
                f"- Max normalized diff vs selected: `{method['maxNormalizedDiffVsSelected']}`",
                f"- Expected strength: {method['expectedStrength']}",
                f"- Expected risk: {method['expectedRisk']}",
                "",
            ]
        )

    lines.extend(
        [
            "## Contact Sheet Frames",
            "",
            f"- Selection rule: {report['contactSheetFrameSelectionRule']}",
            f"- Included frames: `{', '.join(str(i) for i in report['contactSheetFrames'])}`",
            "",
            "## Visual Review Points",
            "",
        ]
    )
    lines.extend(f"- {point}" for point in report["visualReviewPoints"])
    lines.extend(
        [
            "",
            "## Runtime Promotion Hold",
            "",
            "Do not promote these outputs yet. This comparison only creates review candidates under `SpritePipelineWork`; it does not validate Unity import settings, scene references, animation timing, frame-count contracts, or `.meta` behavior.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--selected-dir", required=True)
    parser.add_argument("--reference-dir", required=True)
    parser.add_argument("--candidate-root")
    parser.add_argument("--methods", nargs="+", default=["nearest", "lanczos", "bicubic"])
    parser.add_argument("--max-frames", type=int, default=50)
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    selected_dir = resolve_project_path(args.selected_dir)
    reference_dir = resolve_project_path(args.reference_dir)
    candidate_root = (
        resolve_project_path(args.candidate_root)
        if args.candidate_root
        else project_root() / "SpritePipelineWork" / asset_id / "upscaled_runtime_candidates"
    )
    comparison_dir = candidate_root / "comparison"
    assert_not_assets_output(comparison_dir)
    comparison_dir.mkdir(parents=True, exist_ok=True)

    selected_frames = numeric_pngs(selected_dir)
    selected_by_index = {numeric_key(path)[0]: path for path in selected_frames}
    frame_indices, selection_rule = choose_frame_indices(selected_frames, args.max_frames)

    candidate_by_method: dict[str, dict[int, Path]] = {}
    method_reports = []
    for method in args.methods:
        frames_dir = candidate_root / method / "frames"
        frames = numeric_pngs(frames_dir)
        by_index = {numeric_key(path)[0]: path for path in frames}
        missing = [index for index in frame_indices if index not in by_index]
        if missing:
            raise SystemExit(f"Candidate method {method} is missing frames: {missing}")
        candidate_by_method[method] = by_index
        method_reports.append(method_summary(method, selected_by_index, frames))

    contact_sheet = comparison_dir / "upscale_comparison_contact_sheet.png"
    make_contact_sheet(contact_sheet, selected_by_index, candidate_by_method, frame_indices, args.methods)

    reference_audit = [audit_folder(selected_dir), audit_folder(reference_dir)]
    target = reference_audit[1]["mostCommonSize"]
    all_diffs_zero = all(
        item["maxNormalizedDiffVsSelected"] == 0 for item in method_reports if item["maxNormalizedDiffVsSelected"] is not None
    )
    recommendation_reason = (
        "All generated candidates are pixel-identical to the selected source frames because the selected frames already match the target canvas; nearest is the safest default for future true pixel-art enlargement."
        if all_diffs_zero
        else "Nearest best preserves hard pixel-art outlines and alpha edges; lanczos/bicubic should be reviewed only if smoother edges are preferred."
    )
    report = {
        "assetId": asset_id,
        "targetSize": {"width": target["width"], "height": target["height"]},
        "targetSizeBasis": "Assets/Mobs/Sprites/Goblin/Attack has one unique PNG size across all 50 runtime-reference frames.",
        "referenceSizeAudit": reference_audit,
        "methods": method_reports,
        "contactSheet": rel(contact_sheet),
        "contactSheetFrames": frame_indices,
        "contactSheetFrameSelectionRule": selection_rule,
        "visualReviewPoints": [
            "Check outline crispness around the club, hood, feet, and silhouette.",
            "Check alpha edge halos against the checkerboard/dark preview background.",
            "Check whether smoothing changes the intended chunky pixel-art style.",
            "Check frame-to-frame consistency around fast swing poses.",
            "Confirm selected source frames are the intended review source before any future promotion.",
        ],
        "recommendedMethod": "nearest",
        "recommendationReason": recommendation_reason,
        "runtimePromotionPerformed": False,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }

    json_path = comparison_dir / "upscale_comparison_report.json"
    md_path = comparison_dir / "upscale_comparison_report.md"
    json_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown_report(md_path, report)
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
