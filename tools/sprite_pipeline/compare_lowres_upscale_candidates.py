#!/usr/bin/env python3
"""Compare lowres upscale candidates against the current runtime reference."""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


METHOD_NOTES = {
    "nearest": {
        "observation": "Preserves hard pixels and outlines best, but shows stair-step scaling when the whole lowres canvas is enlarged.",
        "recommendation": "Best default only when preserving pixel-art chunks is more important than smoothness.",
    },
    "lanczos": {
        "observation": "Smooths the strongest stair steps, but can soften outlines and create edge/color blending.",
        "recommendation": "Use only if visual review accepts softer non-pixel edges.",
    },
    "bicubic": {
        "observation": "Middle ground; smoother than nearest and usually less aggressive than lanczos.",
        "recommendation": "Useful comparison candidate, but still risks softened outlines.",
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


def by_index(paths: list[Path]) -> dict[int, Path]:
    return {numeric_key(path)[0]: path for path in paths}


def alpha_preserved(paths: list[Path]) -> bool:
    for path in paths:
        with Image.open(path) as img:
            if "A" not in img.getbands():
                return False
    return True


def image_size(path: Path) -> tuple[int, int]:
    with Image.open(path) as img:
        return img.size


def unique_sizes(paths: list[Path]) -> list[dict]:
    counts: dict[tuple[int, int], int] = {}
    for path in paths:
        size = image_size(path)
        counts[size] = counts.get(size, 0) + 1
    return [
        {"width": width, "height": height, "count": count}
        for (width, height), count in sorted(counts.items(), key=lambda item: (-item[1], item[0]))
    ]


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


def alpha_normalized_diff(left: Path, right: Path) -> float | None:
    with Image.open(left) as left_img, Image.open(right) as right_img:
        a = left_img.convert("RGBA").getchannel("A")
        b = right_img.convert("RGBA").getchannel("A")
    if a.size != b.size:
        return None
    diff = ImageChops.difference(a, b)
    hist = diff.histogram()
    total = sum(value * index for index, value in enumerate(hist))
    return total / float(a.width * a.height * 255)


def choose_frame_indices(runtime_indices: list[int], max_frames: int) -> tuple[list[int], str]:
    if len(runtime_indices) <= max_frames:
        return runtime_indices, f"Included all {len(runtime_indices)} frames."
    sample_count = max(8, max_frames)
    sampled = {runtime_indices[0], runtime_indices[-1], runtime_indices[len(runtime_indices) // 2]}
    for i in range(sample_count):
        pos = round(i * (len(runtime_indices) - 1) / max(1, sample_count - 1))
        sampled.add(runtime_indices[pos])
    return sorted(sampled), (
        f"Evenly sampled {len(sampled)} frames across the sequence, forcing first, middle, and last frames."
    )


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
    path: Path,
    frame_indices: list[int],
    lowres: dict[int, Path],
    runtime: dict[int, Path],
    candidates: dict[str, dict[int, Path]],
    methods: list[str],
) -> None:
    columns = ["lowres", "runtime"] + methods
    thumb = 160
    header_h = 34
    label_h = 22
    row_h = thumb + label_h
    sheet = Image.new("RGB", (len(columns) * thumb, header_h + len(frame_indices) * row_h), (24, 24, 24))
    draw = ImageDraw.Draw(sheet)
    for col, label in enumerate(columns):
        draw.text((col * thumb + 6, 10), label, fill=(255, 255, 255))
    for row, frame_index in enumerate(frame_indices):
        row_paths = [lowres[frame_index], runtime[frame_index]] + [candidates[method][frame_index] for method in methods]
        y = header_h + row * row_h
        for col, frame_path in enumerate(row_paths):
            x = col * thumb
            sheet.paste(render_thumb(frame_path, thumb), (x, y))
            draw.text((x + 6, y + thumb + 3), f"{frame_index}.png", fill=(255, 255, 255))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)


def method_report(method: str, runtime: dict[int, Path], candidate_paths: list[Path]) -> dict:
    candidate = by_index(candidate_paths)
    diffs = []
    alpha_diffs = []
    for frame_index, runtime_path in runtime.items():
        candidate_path = candidate.get(frame_index)
        if candidate_path is None:
            continue
        value = normalized_diff(runtime_path, candidate_path)
        alpha_value = alpha_normalized_diff(runtime_path, candidate_path)
        if value is not None:
            diffs.append(value)
        if alpha_value is not None:
            alpha_diffs.append(alpha_value)
    return {
        "method": method,
        "frameCount": len(candidate_paths),
        "alphaPreserved": alpha_preserved(candidate_paths),
        "outputFolder": rel(candidate_paths[0].parent),
        "meanNormalizedDiffVsRuntime": round(sum(diffs) / len(diffs), 8) if diffs else None,
        "maxNormalizedDiffVsRuntime": round(max(diffs), 8) if diffs else None,
        "meanAlphaDiffVsRuntime": round(sum(alpha_diffs) / len(alpha_diffs), 8) if alpha_diffs else None,
        "maxAlphaDiffVsRuntime": round(max(alpha_diffs), 8) if alpha_diffs else None,
        "observation": METHOD_NOTES[method]["observation"],
        "recommendation": METHOD_NOTES[method]["recommendation"],
    }


def write_markdown(path: Path, report: dict) -> None:
    lines = [
        "# Lowres Upscale Comparison Report",
        "",
        f"- Asset ID: `{report['assetId']}`",
        f"- Provenance mapping confidence: `{report['provenance']['mappingConfidence']}`",
        f"- Source size: `{report['sourceSize']['width']}x{report['sourceSize']['height']}`",
        f"- Target size: `{report['targetSize']['width']}x{report['targetSize']['height']}`",
        f"- Target basis: {report['targetBasis']}",
        f"- Contact sheet: `{report['contactSheet']}`",
        f"- Recommended method: `{report['recommendedMethod']}`",
        f"- Recommendation reason: {report['recommendationReason']}",
        "",
        "## Provenance",
        "",
        f"- Selected manifest: `{report['provenance']['selectedManifest']}`",
        f"- Lowres manifest: `{report['provenance']['lowresManifest']}`",
        f"- Mapping summary: {report['provenance']['mappingSummary']}",
        f"- Source frame range summary: `{report['provenance']['sourceFrameSummary']}`",
        "",
        "## Method Results",
        "",
    ]
    for method in report["methods"]:
        lines.extend(
            [
                f"### `{method['method']}`",
                "",
                f"- Output folder: `{method['outputFolder']}`",
                f"- Frame count: `{method['frameCount']}`",
                f"- Alpha preserved: `{str(method['alphaPreserved']).lower()}`",
                f"- Mean normalized diff vs runtime: `{method['meanNormalizedDiffVsRuntime']}`",
                f"- Max normalized diff vs runtime: `{method['maxNormalizedDiffVsRuntime']}`",
                f"- Mean alpha diff vs runtime: `{method['meanAlphaDiffVsRuntime']}`",
                f"- Max alpha diff vs runtime: `{method['maxAlphaDiffVsRuntime']}`",
                f"- Observation: {method['observation']}",
                f"- Recommendation note: {method['recommendation']}",
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
    lines.extend(f"- {item}" for item in report["visualReviewPoints"])
    lines.extend(
        [
            "",
            "## Non-Recommendations",
            "",
        ]
    )
    lines.extend(f"- {item}" for item in report["notRecommended"])
    lines.extend(
        [
            "",
            "## Runtime Promotion Hold",
            "",
            "Do not promote these outputs yet. This experiment validates resize behavior only; it does not validate Unity import settings, `.meta` generation, animation timing, runtime references, or whether full-frame resize is the desired canvas policy.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def source_frame_summary(mappings: list[dict]) -> str:
    pairs = [f"{item['outputFile']}<=source {item['sourceFrame']}" for item in mappings[:5]]
    pairs.append("...")
    pairs.extend(f"{item['outputFile']}<=source {item['sourceFrame']}" for item in mappings[-3:])
    return "; ".join(pairs)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--lowres-dir", required=True)
    parser.add_argument("--runtime-dir", required=True)
    parser.add_argument("--candidate-root", required=True)
    parser.add_argument("--lowres-manifest", required=True)
    parser.add_argument("--selected-manifest", required=True)
    parser.add_argument("--methods", nargs="+", default=["nearest", "lanczos", "bicubic"])
    parser.add_argument("--max-frames", type=int, default=12)
    args = parser.parse_args()

    asset_id = safe_asset_id(args.asset_id)
    lowres_dir = resolve_project_path(args.lowres_dir)
    runtime_dir = resolve_project_path(args.runtime_dir)
    candidate_root = resolve_project_path(args.candidate_root)
    lowres_manifest_path = resolve_project_path(args.lowres_manifest)
    selected_manifest_path = resolve_project_path(args.selected_manifest)
    comparison_dir = candidate_root / "comparison"
    assert_not_assets_output(comparison_dir)
    comparison_dir.mkdir(parents=True, exist_ok=True)

    lowres_frames = numeric_pngs(lowres_dir)
    runtime_frames = numeric_pngs(runtime_dir)
    lowres = by_index(lowres_frames)
    runtime = by_index(runtime_frames)
    indices = sorted(set(lowres) & set(runtime))
    if len(indices) != 50:
        raise SystemExit(f"Expected 50 matching lowres/runtime frame indices, got {len(indices)}")

    candidates: dict[str, dict[int, Path]] = {}
    method_reports = []
    for method in args.methods:
        frames = numeric_pngs(candidate_root / method / "frames")
        candidate = by_index(frames)
        missing = [index for index in indices if index not in candidate]
        if missing:
            raise SystemExit(f"Candidate {method} missing frames: {missing}")
        candidates[method] = candidate
        method_reports.append(method_report(method, runtime, frames))

    frame_indices, frame_rule = choose_frame_indices(indices, args.max_frames)
    contact_sheet = comparison_dir / "lowres_upscale_comparison_contact_sheet.png"
    make_contact_sheet(contact_sheet, frame_indices, lowres, runtime, candidates, args.methods)

    lowres_manifest = json.loads(lowres_manifest_path.read_text(encoding="utf-8"))
    selected_manifest = json.loads(selected_manifest_path.read_text(encoding="utf-8"))
    best = min(
        method_reports,
        key=lambda item: (
            item["meanNormalizedDiffVsRuntime"] if item["meanNormalizedDiffVsRuntime"] is not None else 999,
            item["meanAlphaDiffVsRuntime"] if item["meanAlphaDiffVsRuntime"] is not None else 999,
        ),
    )
    recommendation = (
        "Nearest remains the safest pixel-art default, but this full-frame resize experiment is not promotion-ready; "
        f"{best['method']} is quantitatively closest to the current runtime reference in normalized diff."
    )
    report = {
        "assetId": asset_id,
        "provenance": {
            "mappingConfidence": lowres_manifest["mappingConfidence"],
            "selectedManifest": rel(selected_manifest_path),
            "lowresManifest": rel(lowres_manifest_path),
            "mappingSummary": lowres_manifest["mappingEvidence"],
            "sourceFrameSummary": source_frame_summary(lowres_manifest["frameMappings"]),
            "selectedRange": selected_manifest.get("selected_range"),
            "frameStep": selected_manifest.get("frame_step"),
            "duplicateLastFrame": selected_manifest.get("duplicate_last_frame"),
        },
        "sourceSize": {"width": 928, "height": 960},
        "targetSize": {"width": 1272, "height": 1298},
        "targetBasis": "Current runtime Goblin Attack canvas: Assets/Mobs/Sprites/Goblin/Attack has 50 frames at 1272x1298.",
        "sourceUniqueSizes": unique_sizes(lowres_frames),
        "runtimeUniqueSizes": unique_sizes(runtime_frames),
        "methods": method_reports,
        "contactSheet": rel(contact_sheet),
        "contactSheetFrames": frame_indices,
        "contactSheetFrameSelectionRule": frame_rule,
        "visualReviewPoints": [
            "Compare silhouette position and scale against the current runtime reference.",
            "Check outline stair-stepping around the club, hood, feet, and hands.",
            "Check color bleeding or softened edges on lanczos and bicubic.",
            "Check alpha edge halo on the checkerboard/dark background.",
            "Check frame-to-frame stability during the swing arc.",
            "Confirm whether full-frame resize is acceptable or whether crop/pad canvas reconstruction is the better policy.",
        ],
        "recommendedMethod": "nearest",
        "recommendationReason": recommendation,
        "notRecommended": [
            "Do not promote any candidate directly from this experiment; full-frame resize changes source canvas semantics compared with the current runtime reference.",
            "Do not use lanczos as a default without visual approval because it can blur pixel-art outlines.",
            "Do not use bicubic as a default without visual approval because it still softens hard pixel clusters.",
        ],
        "runtimePromotionPerformed": False,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }
    json_path = comparison_dir / "lowres_upscale_comparison_report.json"
    md_path = comparison_dir / "lowres_upscale_comparison_report.md"
    json_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown(md_path, report)
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
