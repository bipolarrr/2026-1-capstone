#!/usr/bin/env python3
"""Build enlarged visual review sheets for lowres upscale candidates."""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


METHODS = ["nearest", "lanczos", "bicubic"]


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


def safe_asset_id(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", value.strip())
    if not cleaned:
        raise SystemExit("asset_id cannot be empty")
    return cleaned


def assert_safe_output(path: Path, asset_id: str) -> None:
    resolved = path.resolve()
    assets = (project_root() / "Assets").resolve()
    allowed = (project_root() / "SpritePipelineWork" / asset_id).resolve()
    if is_relative_to(resolved, assets):
        raise SystemExit(f"Refusing to write under Assets/: {resolved}")
    if resolved != allowed and not is_relative_to(resolved, allowed):
        raise SystemExit(f"Output must stay under {allowed}: {resolved}")


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


def by_index(paths: list[Path]) -> dict[int, Path]:
    return {numeric_key(path)[0]: path for path in paths}


def read_rgba(path: Path) -> Image.Image:
    with Image.open(path) as img:
        return img.convert("RGBA")


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    return bbox if bbox else (0, 0, image.width, image.height)


def expand_bbox(bbox: tuple[int, int, int, int], size: tuple[int, int], padding: int) -> tuple[int, int, int, int]:
    left, top, right, bottom = bbox
    return (
        max(0, left - padding),
        max(0, top - padding),
        min(size[0], right + padding),
        min(size[1], bottom + padding),
    )


def union_bbox(images: list[Image.Image], padding: int) -> tuple[int, int, int, int]:
    boxes = [alpha_bbox(image) for image in images]
    left = min(box[0] for box in boxes)
    top = min(box[1] for box in boxes)
    right = max(box[2] for box in boxes)
    bottom = max(box[3] for box in boxes)
    return expand_bbox((left, top, right, bottom), images[0].size, padding)


def checkerboard(size: tuple[int, int], tile: int = 16) -> Image.Image:
    image = Image.new("RGBA", size, (255, 255, 255, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            color = (72, 72, 72, 255) if ((x // tile + y // tile) % 2) else (28, 28, 28, 255)
            draw.rectangle([x, y, x + tile - 1, y + tile - 1], fill=color)
    return image


def composite(image: Image.Image, background: str) -> Image.Image:
    if background == "dark":
        bg = Image.new("RGBA", image.size, (18, 18, 18, 255))
    else:
        bg = checkerboard(image.size)
    bg.alpha_composite(image)
    return bg.convert("RGB")


def contain(image: Image.Image, cell: tuple[int, int], resample: int) -> Image.Image:
    if image.width < 1 or image.height < 1:
        raise SystemExit("Cannot render an empty crop")
    scale = min(cell[0] / image.width, cell[1] / image.height)
    scale = max(1.0, min(3.0, scale))
    resized = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), resample)
    canvas = Image.new("RGB", cell, (18, 18, 18))
    x = (cell[0] - resized.width) // 2
    y = (cell[1] - resized.height) // 2
    canvas.paste(resized.convert("RGB"), (x, y))
    return canvas


def render_crop(image: Image.Image, bbox: tuple[int, int, int, int], cell: tuple[int, int], background: str, resample: int) -> Image.Image:
    crop = image.crop(bbox)
    preview = composite(crop, background)
    return contain(preview, cell, resample)


def alpha_edge_preview(image: Image.Image, bbox: tuple[int, int, int, int], cell: tuple[int, int]) -> Image.Image:
    alpha = image.getchannel("A")
    edge = alpha.filter(ImageFilter.FIND_EDGES)
    mask = Image.merge("RGBA", (edge, edge, edge, Image.new("L", image.size, 255))).crop(bbox)
    return contain(mask.convert("RGB"), cell, Image.Resampling.NEAREST)


def draw_cell(sheet: Image.Image, draw: ImageDraw.ImageDraw, image: Image.Image, x: int, y: int, label: str) -> None:
    sheet.paste(image, (x, y + 20))
    draw.text((x + 6, y + 4), label, fill=(255, 255, 255))


def choose_review_frames(report: dict, count: int) -> tuple[list[int], str]:
    candidates = [int(value) for value in report.get("contactSheetFrames", [])]
    candidates = [value for value in candidates if value < 48]
    if len(candidates) >= count:
        selected = candidates[:count]
    else:
        selected = sorted(set(candidates + [0, 6, 12, 18, 24, 30, 36, 42, 47]))[:count]
    return selected, (
        "Used frames from the lowres comparison report, excluding duplicate-last frames 48 and 49; "
        "kept broad coverage across windup, swing, recovery, first, and late frames."
    )


def choose_motion_frames(indices: list[int]) -> tuple[list[int], str]:
    usable = [index for index in indices if index < 48]
    start = 18 if all(index in usable for index in range(18, 30)) else usable[max(0, len(usable) // 2 - 6)]
    frames = [index for index in range(start, start + 12) if index in usable]
    if len(frames) < 8:
        frames = usable[: min(12, len(usable))]
    return frames, "Selected a contiguous non-duplicate swing segment and excluded duplicate-last source frames 48 and 49."


def build_zoom_sheet(
    out_path: Path,
    frame_indices: list[int],
    lowres: dict[int, Path],
    runtime: dict[int, Path],
    candidates: dict[str, dict[int, Path]],
) -> None:
    columns = ["lowres", "runtime"] + METHODS
    cell = (220, 220)
    header_h = 28
    row_label_w = 64
    row_h = cell[1] + header_h
    sheet = Image.new("RGB", (row_label_w + len(columns) * cell[0], header_h + len(frame_indices) * row_h), (18, 18, 18))
    draw = ImageDraw.Draw(sheet)
    for col, label in enumerate(columns):
        draw.text((row_label_w + col * cell[0] + 6, 8), label, fill=(255, 255, 255))
    for row, frame_index in enumerate(frame_indices):
        y = header_h + row * row_h
        draw.text((8, y + 92), f"f{frame_index}", fill=(255, 255, 255))
        target_images = [read_rgba(runtime[frame_index])] + [read_rgba(candidates[method][frame_index]) for method in METHODS]
        target_bbox = union_bbox(target_images, padding=48)
        lowres_image = read_rgba(lowres[frame_index])
        lowres_bbox = expand_bbox(alpha_bbox(lowres_image), lowres_image.size, padding=36)
        row_images = [lowres_image] + target_images
        row_boxes = [lowres_bbox] + [target_bbox] * len(target_images)
        for col, (image, bbox, label) in enumerate(zip(row_images, row_boxes, columns)):
            resample = Image.Resampling.NEAREST if label in {"lowres", "nearest"} else Image.Resampling.BICUBIC
            preview = render_crop(image, bbox, cell, "checker", resample)
            draw_cell(sheet, draw, preview, row_label_w + col * cell[0], y, f"{label} {frame_index}.png")
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def build_alpha_sheet(
    out_path: Path,
    frame_indices: list[int],
    runtime: dict[int, Path],
    candidates: dict[str, dict[int, Path]],
) -> None:
    columns = ["runtime mask", "runtime dark", "nearest mask", "nearest dark", "lanczos mask", "lanczos dark", "bicubic mask", "bicubic dark"]
    cell = (170, 170)
    header_h = 28
    row_label_w = 64
    row_h = cell[1] + header_h
    sheet = Image.new("RGB", (row_label_w + len(columns) * cell[0], header_h + len(frame_indices) * row_h), (18, 18, 18))
    draw = ImageDraw.Draw(sheet)
    for col, label in enumerate(columns):
        draw.text((row_label_w + col * cell[0] + 4, 8), label, fill=(255, 255, 255))
    for row, frame_index in enumerate(frame_indices):
        y = header_h + row * row_h
        draw.text((8, y + 72), f"f{frame_index}", fill=(255, 255, 255))
        images = {"runtime": read_rgba(runtime[frame_index])}
        images.update({method: read_rgba(candidates[method][frame_index]) for method in METHODS})
        bbox = union_bbox(list(images.values()), padding=36)
        previews = []
        for name in ["runtime", "nearest", "lanczos", "bicubic"]:
            previews.append((f"{name} alpha", alpha_edge_preview(images[name], bbox, cell)))
            previews.append((f"{name} dark", render_crop(images[name], bbox, cell, "dark", Image.Resampling.BICUBIC)))
        for col, (label, preview) in enumerate(previews):
            draw_cell(sheet, draw, preview, row_label_w + col * cell[0], y, label)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def build_motion_sheet(
    out_path: Path,
    frame_indices: list[int],
    runtime: dict[int, Path],
    candidates: dict[str, dict[int, Path]],
) -> None:
    rows = ["runtime"] + METHODS
    cell = (112, 112)
    header_h = 28
    row_label_w = 84
    row_h = cell[1] + 26
    sheet = Image.new("RGB", (row_label_w + len(frame_indices) * cell[0], header_h + len(rows) * row_h), (18, 18, 18))
    draw = ImageDraw.Draw(sheet)
    for col, frame_index in enumerate(frame_indices):
        draw.text((row_label_w + col * cell[0] + 6, 8), f"f{frame_index}", fill=(255, 255, 255))
    all_images = []
    for frame_index in frame_indices:
        all_images.append(read_rgba(runtime[frame_index]))
        all_images.extend(read_rgba(candidates[method][frame_index]) for method in METHODS)
    bbox = union_bbox(all_images, padding=42)
    for row, name in enumerate(rows):
        y = header_h + row * row_h
        draw.text((8, y + 48), name, fill=(255, 255, 255))
        for col, frame_index in enumerate(frame_indices):
            path = runtime[frame_index] if name == "runtime" else candidates[name][frame_index]
            image = read_rgba(path)
            resample = Image.Resampling.NEAREST if name == "nearest" else Image.Resampling.BICUBIC
            preview = render_crop(image, bbox, cell, "checker", resample)
            sheet.paste(preview, (row_label_w + col * cell[0], y + 16))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def method_table(previous: dict) -> list[dict]:
    metrics = {item["method"]: item for item in previous["methods"]}
    return [
        {
            "method": "nearest",
            "quantitativeClosenessToRuntime": "Poor",
            "pixelArtEdgePreservation": "Good",
            "alphaEdgeQuality": "Needs human review",
            "colorBleedingRisk": "Good",
            "motionConsistencyRisk": "Needs human review",
            "recommendation": "Keep as global conservative default candidate.",
            "meanNormalizedDiffVsRuntime": metrics["nearest"]["meanNormalizedDiffVsRuntime"],
        },
        {
            "method": "lanczos",
            "quantitativeClosenessToRuntime": "Medium",
            "pixelArtEdgePreservation": "Needs human review",
            "alphaEdgeQuality": "Needs human review",
            "colorBleedingRisk": "Medium",
            "motionConsistencyRisk": "Needs human review",
            "recommendation": "Keep as comparison candidate only.",
            "meanNormalizedDiffVsRuntime": metrics["lanczos"]["meanNormalizedDiffVsRuntime"],
        },
        {
            "method": "bicubic",
            "quantitativeClosenessToRuntime": "Good",
            "pixelArtEdgePreservation": "Medium",
            "alphaEdgeQuality": "Needs human review",
            "colorBleedingRisk": "Medium",
            "motionConsistencyRisk": "Needs human review",
            "recommendation": "Best Goblin Attack candidate pending human approval.",
            "meanNormalizedDiffVsRuntime": metrics["bicubic"]["meanNormalizedDiffVsRuntime"],
        },
    ]


def write_markdown(path: Path, report: dict) -> None:
    lines = [
        "# Goblin Attack Upscale Human Review Report",
        "",
        "## Summary",
        "",
        "- Purpose: provide enlarged crop, alpha-edge, and motion-strip views so a human can choose an upscale method before runtime promotion.",
        "- This packet is not runtime promotion. It does not write to or replace `Assets/**` runtime sprites.",
        "- Bicubic is quantitatively closest to the current runtime reference, but pixel-art edge preservation still requires human visual approval.",
        "",
        "## Frame Selection",
        "",
        f"- Zoom/alpha frames: `{', '.join(str(item) for item in report['zoomFrames'])}`",
        f"- Motion frames: `{', '.join(str(item) for item in report['motionFrames'])}`",
        f"- Selection rule: {report['frameSelectionRule']}",
        f"- Motion rule: {report['motionSelectionRule']}",
        "- Duplicate source frames 48 and 49 were excluded from the zoom/alpha decision set and from the motion strip to avoid judging duplicated hold frames as interpolation behavior.",
        "",
        "## Method Comparison",
        "",
        "| Method | Quantitative closeness to runtime | Pixel-art edge preservation | Alpha edge quality | Color bleeding risk | Motion consistency risk | Recommendation |",
        "|---|---|---|---|---|---|---|",
    ]
    for row in report["methodComparison"]:
        lines.append(
            f"| {row['method']} | {row['quantitativeClosenessToRuntime']} | {row['pixelArtEdgePreservation']} | "
            f"{row['alphaEdgeQuality']} | {row['colorBleedingRisk']} | {row['motionConsistencyRisk']} | {row['recommendation']} |"
        )
    lines.extend(
        [
            "",
            "## Decision Options",
            "",
            "### Option A: nearest as the default",
            "",
            "Use when dot texture and hard outlines matter more than matching the current runtime reference. Risk: non-integer `928x960 -> 1272x1298` enlargement can look overly stepped and differs most from the runtime reference.",
            "",
            "### Option B: bicubic as the Goblin Attack candidate",
            "",
            "Use when matching the current runtime reference shape, position, and color is more important than strict pixel clusters. Risk: it may soften outlines and should not become a global default from one asset.",
            "",
            "### Option C: asset-specific manifest policy",
            "",
            "Keep nearest as the global conservative default, allow bicubic/lanczos per asset after human review, and require the approved method in the manifest before promotion. Risk: pipeline management becomes more complex and method drift is possible.",
            "",
            "## Recommended Next Action",
            "",
            f"`{report['recommendedDecisionOption']}`.",
            "Human approval of the contact sheets is still required before any runtime promotion or Unity import/reference validation task.",
            "",
            "## Generated Review Sheets",
            "",
            f"- Zoom comparison: `{report['artifacts']['zoomComparisonContactSheet']}`",
            f"- Alpha edge comparison: `{report['artifacts']['alphaEdgeComparisonContactSheet']}`",
            f"- Motion consistency: `{report['artifacts']['motionConsistencyContactSheet']}`",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--lowres-dir", required=True)
    parser.add_argument("--runtime-dir", required=True)
    parser.add_argument("--candidate-root", required=True)
    parser.add_argument("--comparison-report-json", required=True)
    parser.add_argument("--output-dir")
    parser.add_argument("--frame-count", type=int, default=10)
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    candidate_root = resolve_project_path(args.candidate_root)
    output_dir = resolve_project_path(args.output_dir) if args.output_dir else candidate_root / "human_review_packet"
    assert_safe_output(output_dir, asset_id)
    output_dir.mkdir(parents=True, exist_ok=True)

    lowres = by_index(numeric_pngs(resolve_project_path(args.lowres_dir)))
    runtime = by_index(numeric_pngs(resolve_project_path(args.runtime_dir)))
    candidates = {
        method: by_index(numeric_pngs(candidate_root / method / "frames"))
        for method in METHODS
    }
    comparison_report_path = resolve_project_path(args.comparison_report_json)
    previous = json.loads(comparison_report_path.read_text(encoding="utf-8"))

    common_indices = sorted(set(lowres) & set(runtime) & set(candidates["nearest"]) & set(candidates["lanczos"]) & set(candidates["bicubic"]))
    if len(common_indices) < 50:
        raise SystemExit(f"Expected at least 50 common frame indices, got {len(common_indices)}")

    zoom_frames, frame_rule = choose_review_frames(previous, max(8, args.frame_count))
    motion_frames, motion_rule = choose_motion_frames(common_indices)

    zoom_path = output_dir / "zoom_comparison_contact_sheet.png"
    alpha_path = output_dir / "alpha_edge_comparison_contact_sheet.png"
    motion_path = output_dir / "motion_consistency_contact_sheet.png"
    build_zoom_sheet(zoom_path, zoom_frames, lowres, runtime, candidates)
    build_alpha_sheet(alpha_path, zoom_frames, runtime, candidates)
    build_motion_sheet(motion_path, motion_frames, runtime, candidates)

    report = {
        "assetId": asset_id,
        "purpose": "Human final method review before any runtime promotion.",
        "runtimePromotionPerformed": False,
        "sourceSize": previous["sourceSize"],
        "targetSize": previous["targetSize"],
        "provenanceMappingConfidence": previous["provenance"]["mappingConfidence"],
        "zoomFrames": zoom_frames,
        "motionFrames": motion_frames,
        "frameSelectionRule": frame_rule,
        "motionSelectionRule": motion_rule,
        "methodComparison": method_table(previous),
        "decisionOptions": {
            "optionA": "nearest as global conservative default",
            "optionB": "bicubic as Goblin Attack candidate pending human approval",
            "optionC": "asset-specific manifest method policy",
        },
        "recommendedDecisionOption": "Default: nearest, GoblinAttack candidate: bicubic pending human approval",
        "artifacts": {
            "zoomComparisonContactSheet": rel(zoom_path),
            "alphaEdgeComparisonContactSheet": rel(alpha_path),
            "motionConsistencyContactSheet": rel(motion_path),
            "humanReviewReportMd": rel(output_dir / "human_review_report.md"),
            "humanReviewReportJson": rel(output_dir / "human_review_report.json"),
        },
        "previousComparisonReport": rel(comparison_report_path),
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }
    (output_dir / "human_review_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown(output_dir / "human_review_report.md", report)
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
