#!/usr/bin/env python3
"""Create non-destructive upscaled runtime candidate frames.

Default output is restricted to:
  SpritePipelineWork/<asset_id>/upscaled_runtime_candidates/<backend>/

The default backend is waifu2x-ncnn-vulkan. Pillow resize backends remain
available only when explicitly selected for fallback/debug/comparison work.

This tool does not write under Assets/, does not promote runtime assets, and
does not delete or move existing runtime files.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageChops, ImageDraw


PILLOW_BACKENDS = {
    "nearest": Image.Resampling.NEAREST,
    "lanczos": Image.Resampling.LANCZOS,
    "bicubic": Image.Resampling.BICUBIC,
}
BACKENDS = ("waifu2x", "nearest", "lanczos", "bicubic")
DEFAULT_BACKEND = "waifu2x"
ALPHA_POLICIES = ("source-mask-nearest", "source-mask-threshold", "trust-ai-alpha")
DEFAULT_ALPHA_POLICY = "source-mask-nearest"
DEFAULT_ALPHA_THRESHOLD = 8
DEFAULT_LOW_ALPHA_THRESHOLD = 31
DEFAULT_EXTRA_ALPHA_FAIL_RATIO = 0.0001
DEFAULT_EDGE_TOUCH_PADDING = 2
SEVERE_EDGE_TOUCH_RATIO = 0.05
CROP_RISK_BBOX_AREA_RATIO = 0.95
WORST_FRAME_LIMIT = 12
AI_BACKEND_NAME = "waifu2x-ncnn-vulkan"
AI_EXE_MISSING_MESSAGE = (
    "GPU AI upscaler executable을 찾지 못했습니다. waifu2x-ncnn-vulkan 설치 또는 "
    "WAIFU2X_NCNN_VULKAN_EXE 설정이 필요합니다."
)
DEFAULT_AI_ESCALATION_MESSAGE = (
    "기본 AI 업스케일 결과가 불합격 또는 애매합니다. 여러 다른 방법을 시도해보는 별도 비교 작업으로 넘어가야 합니다."
)


class PipelineFailure(Exception):
    def __init__(self, message: str, reasons: list[str] | None = None) -> None:
        super().__init__(message)
        self.reasons = reasons or [message]


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


def rel(path: Path | None) -> str | None:
    if path is None:
        return None
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


def read_image_size(path: Path) -> tuple[int, int]:
    with Image.open(path) as img:
        return img.size


def count_sizes(paths: list[Path]) -> list[dict[str, int]]:
    counts: dict[tuple[int, int], int] = {}
    for path in paths:
        size = read_image_size(path)
        counts[size] = counts.get(size, 0) + 1
    return [
        {"width": width, "height": height, "count": count}
        for (width, height), count in sorted(counts.items(), key=lambda item: (-item[1], item[0]))
    ]


def alpha_unique_values(paths: list[Path]) -> list[int]:
    values: set[int] = set()
    for path in paths:
        with Image.open(path) as img:
            alpha = img.convert("RGBA").getchannel("A")
            values.update(index for index, count in enumerate(alpha.histogram()) if count)
    return sorted(values)


def alpha_channel_status(paths: list[Path]) -> tuple[bool, list[str]]:
    missing_alpha = []
    for path in paths:
        with Image.open(path) as img:
            if "A" not in img.getbands():
                missing_alpha.append(path.name)
    return not missing_alpha, missing_alpha


def alpha_has_transparency(alpha: Image.Image) -> bool:
    extrema = alpha.getextrema()
    return extrema[0] < 255


def alpha_is_fully_opaque(alpha: Image.Image) -> bool:
    return alpha.getextrema() == (255, 255)


def alpha_is_empty(alpha: Image.Image) -> bool:
    return alpha.getbbox() is None


def resampling_name(resample: int) -> str:
    for item in Image.Resampling:
        if item == resample:
            return item.name
    return str(resample)


def rgb_resampling_mode(backend: str) -> str:
    if backend == "waifu2x":
        return "waifu2x-output-lanczos"
    return resampling_name(PILLOW_BACKENDS[backend])


def alpha_resampling_mode(alpha_policy: str) -> str:
    if alpha_policy == "source-mask-nearest":
        return "source-alpha-nearest"
    if alpha_policy == "source-mask-threshold":
        return "source-alpha-bilinear-threshold"
    return "ai-alpha-lanczos"


def alpha_uses_source_mask(alpha_policy: str) -> bool:
    return alpha_policy in {"source-mask-nearest", "source-mask-threshold"}


def threshold_alpha_image(alpha: Image.Image, threshold: int) -> Image.Image:
    return alpha.point(lambda value: 255 if value >= threshold else 0)


def source_alpha_for_policy(
    source_rgba: Image.Image,
    target_size: tuple[int, int],
    alpha_policy: str,
    alpha_threshold: int,
) -> Image.Image:
    source_alpha = source_rgba.getchannel("A")
    if alpha_policy == "source-mask-threshold":
        resized = source_alpha.resize(target_size, resample=Image.Resampling.BILINEAR)
        return threshold_alpha_image(resized, alpha_threshold)
    return source_alpha.resize(target_size, resample=Image.Resampling.NEAREST)


def alpha_comparison_policy(alpha_policy: str) -> str:
    if alpha_policy == "source-mask-threshold":
        return "source-mask-threshold"
    return "source-mask-nearest"


def resolve_reference_dir_target(reference_dir: Path) -> tuple[int, int, str, dict[str, Any]]:
    if not reference_dir.exists() or not reference_dir.is_dir():
        raise SystemExit(f"Reference directory does not exist: {reference_dir}")
    pngs = sorted([p for p in reference_dir.iterdir() if p.is_file() and p.suffix.lower() == ".png"], key=numeric_key)
    if not pngs:
        raise SystemExit(f"No PNG files found in reference directory: {reference_dir}")

    sizes = count_sizes(pngs)
    most = sizes[0]
    ratio = most["count"] / len(pngs)
    audit = {
        "referenceDir": rel(reference_dir),
        "pngCount": len(pngs),
        "uniqueSizes": sizes,
        "mostCommonSize": most,
        "mostCommonRatio": round(ratio, 4),
    }

    if len(sizes) == 1:
        source = f"reference-dir:{rel(reference_dir)}:{len(pngs)} png(s):uniform"
        return most["width"], most["height"], source, audit

    if ratio < 0.8:
        unique = ", ".join(f"{item['width']}x{item['height']}={item['count']}" for item in sizes)
        raise SystemExit(
            "Reference directory PNG sizes are mixed and the most common size is below 80%; "
            f"AI upscale was not run. PNG count={len(pngs)}, unique sizes: {unique}"
        )

    source = f"reference-dir:{rel(reference_dir)}:{len(pngs)} png(s):most-common-{ratio:.0%}"
    return most["width"], most["height"], source, audit


def resolve_target_size(args: argparse.Namespace) -> tuple[int, int, str, dict[str, Any] | None]:
    has_width = args.target_width is not None
    has_height = args.target_height is not None
    if has_width != has_height:
        raise SystemExit("--target-width and --target-height must be provided together.")

    if has_width and has_height:
        width = int(args.target_width)
        height = int(args.target_height)
        source = "explicit"
        audit = None
    elif args.reference_image:
        reference = resolve_project_path(args.reference_image)
        if not reference.exists() or not reference.is_file():
            raise SystemExit(f"Reference image does not exist: {reference}")
        width, height = read_image_size(reference)
        source = f"reference-image:{rel(reference)}"
        audit = {"referenceImage": rel(reference), "width": width, "height": height}
    elif args.reference_dir:
        width, height, source, audit = resolve_reference_dir_target(resolve_project_path(args.reference_dir))
    else:
        raise SystemExit(
            "Target size is required. Provide --target-width and --target-height, "
            "--reference-image, or --reference-dir."
        )

    if width < 1 or height < 1:
        raise SystemExit(f"Target width/height must be at least 1: {width}x{height}")
    return width, height, source, audit


def default_candidate_root(asset_id: str) -> Path:
    return project_root() / "SpritePipelineWork" / asset_id / "upscaled_runtime_candidates"


def resolve_output_root(value: str | None, asset_id: str) -> Path:
    root = resolve_project_path(value) if value else default_candidate_root(asset_id)
    allowed_root = (project_root() / "SpritePipelineWork" / asset_id).resolve()
    resolved = root.resolve()
    if resolved != allowed_root and not is_relative_to(resolved, allowed_root):
        raise SystemExit(f"--output-root must stay under {allowed_root}: {resolved}")
    assert_not_assets_output(resolved)
    return resolved


def assert_inside_output_backend(path: Path, backend_root: Path, backend: str) -> None:
    resolved = path.resolve()
    expected = backend_root.resolve()
    if resolved != expected and not is_relative_to(resolved, expected):
        raise SystemExit(f"Output path escaped the backend root: {resolved} is not under {expected}")
    if backend_root.name != backend:
        raise SystemExit(f"Output backend folder mismatch: expected {backend}, got {backend_root.name}")
    assert_not_assets_output(resolved)


def collect_input_files(input_dir: Path) -> tuple[list[Path], list[Path]]:
    if not input_dir.exists() or not input_dir.is_dir():
        raise SystemExit(f"Input directory does not exist: {input_dir}")
    files = [p for p in input_dir.iterdir() if p.is_file()]
    pngs = [p for p in files if p.suffix.lower() == ".png"]
    numeric_pngs = [p for p in pngs if numeric_key(p)[0] != 10**9]
    if numeric_pngs:
        frames = sorted(numeric_pngs, key=numeric_key)
        ignored = [p for p in files if p.suffix.lower() != ".png" or p not in numeric_pngs]
    else:
        frames = sorted(pngs, key=numeric_key)
        ignored = [p for p in files if p.suffix.lower() != ".png"]
    ignored = sorted(ignored, key=lambda p: p.name.lower())
    if not frames:
        raise SystemExit(f"No PNG frames found in input directory: {input_dir}")
    return frames, ignored


def planned_paths(frames_dir: Path, paths: list[Path]) -> list[Path]:
    return [frames_dir / path.name for path in paths]


def resolve_backend(args: argparse.Namespace) -> str:
    legacy_method = args.method
    backend = args.backend
    if legacy_method and backend and legacy_method != backend:
        raise SystemExit(f"--method and --backend disagree: {legacy_method} != {backend}")
    resolved = backend or legacy_method or DEFAULT_BACKEND
    if resolved not in BACKENDS:
        raise SystemExit(f"Unknown backend: {resolved}")
    return resolved


def resolve_ai_exe(value: str | None) -> tuple[Path | None, list[str]]:
    candidates: list[Path] = []
    if value:
        candidates.append(resolve_project_path(value))
    env_value = os.environ.get("WAIFU2X_NCNN_VULKAN_EXE")
    if env_value:
        candidates.append(resolve_project_path(env_value))
    candidates.extend(
        [
            project_root() / "tools" / "external" / "waifu2x-ncnn-vulkan" / "waifu2x-ncnn-vulkan.exe",
            project_root() / "tools" / "external" / "waifu2x-ncnn-vulkan" / "waifu2x-ncnn-vulkan",
        ]
    )

    seen: set[Path] = set()
    checked: list[str] = []
    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        checked.append(rel(resolved) or str(resolved))
        if resolved.exists() and resolved.is_file():
            return resolved, checked
    return None, checked


def resolve_ai_model_path(value: str | None) -> Path | None:
    if not value:
        return None
    path = resolve_project_path(value)
    if not path.exists():
        raise SystemExit(f"AI model path does not exist: {path}")
    return path


def build_ai_command(args: argparse.Namespace, exe: Path, input_dir: Path, output_dir: Path, model_path: Path | None) -> list[str]:
    command = [
        str(exe),
        "-i",
        str(input_dir),
        "-o",
        str(output_dir),
        "-n",
        str(args.ai_noise),
        "-s",
        str(args.ai_scale),
        "-f",
        "png",
    ]
    if model_path:
        command.extend(["-m", str(model_path)])
    if args.ai_gpu_id is not None:
        command.extend(["-g", str(args.ai_gpu_id)])
    if args.ai_tile_size is not None:
        command.extend(["-t", str(args.ai_tile_size)])
    return command


def ensure_no_existing_outputs(paths: list[Path], overwrite_candidates: bool) -> list[Path]:
    existing = [path for path in paths if path.exists()]
    if existing and not overwrite_candidates:
        preview = "\n".join(f"- {path}" for path in existing[:12])
        suffix = "\n..." if len(existing) > 12 else ""
        raise SystemExit(
            "Candidate output files already exist. Pass --overwrite-candidates to overwrite same-name candidate files:\n"
            f"{preview}{suffix}"
        )
    return existing


def remove_temp_dir_if_needed(temp_dir: Path, backend_root: Path, keep_temp: bool, expected_name: str) -> None:
    if keep_temp or not temp_dir.exists():
        return
    resolved = temp_dir.resolve()
    root = backend_root.resolve()
    if resolved == root or not is_relative_to(resolved, root) or resolved.name != expected_name:
        raise SystemExit(f"Refusing unsafe temp cleanup path: {resolved}")
    shutil.rmtree(resolved)


def prepare_temp_dir(temp_dir: Path, backend_root: Path, overwrite_candidates: bool, expected_name: str) -> None:
    if not temp_dir.exists():
        return
    resolved = temp_dir.resolve()
    root = backend_root.resolve()
    if not is_relative_to(resolved, root) or resolved.name != expected_name:
        raise SystemExit(f"Refusing unsafe temp path: {resolved}")
    if not overwrite_candidates:
        raise SystemExit(f"Temp directory already exists. Pass --overwrite-candidates to replace it: {temp_dir}")
    shutil.rmtree(resolved)


def remove_ai_temp_if_needed(ai_temp_dir: Path, backend_root: Path, keep_ai_temp: bool) -> None:
    remove_temp_dir_if_needed(ai_temp_dir, backend_root, keep_ai_temp, "ai_temp")


def prepare_ai_temp(ai_temp_dir: Path, backend_root: Path, overwrite_candidates: bool) -> None:
    prepare_temp_dir(ai_temp_dir, backend_root, overwrite_candidates, "ai_temp")


def remove_ai_input_rgb_if_needed(ai_input_rgb_dir: Path, backend_root: Path, keep_ai_temp: bool) -> None:
    remove_temp_dir_if_needed(ai_input_rgb_dir, backend_root, keep_ai_temp, "ai_input_rgb")


def prepare_ai_input_rgb(ai_input_rgb_dir: Path, backend_root: Path, overwrite_candidates: bool) -> None:
    prepare_temp_dir(ai_input_rgb_dir, backend_root, overwrite_candidates, "ai_input_rgb")


def run_waifu2x(command: list[str]) -> tuple[str, str]:
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if result.returncode != 0:
        message = (
            f"{AI_BACKEND_NAME} command failed with exit code {result.returncode}.\n"
            f"stdout:\n{result.stdout}\n"
            f"stderr:\n{result.stderr}"
        )
        raise PipelineFailure(message, [f"{AI_BACKEND_NAME} command failed with exit code {result.returncode}."])
    return result.stdout, result.stderr


def collect_ai_output_paths(ai_temp_dir: Path, input_pngs: list[Path]) -> tuple[list[Path], list[str]]:
    outputs = sorted([p for p in ai_temp_dir.iterdir() if p.is_file() and p.suffix.lower() == ".png"], key=numeric_key)
    warnings: list[str] = []
    by_name = {path.name: path for path in outputs}
    if all(src.name in by_name for src in input_pngs):
        return [by_name[src.name] for src in input_pngs], warnings
    if len(outputs) == len(input_pngs):
        warnings.append("AI output filenames did not all match input filenames; mapped frames by sorted order.")
        return outputs, warnings
    missing = [src.name for src in input_pngs if src.name not in by_name]
    raise PipelineFailure(
        f"AI output frame count/name mismatch. Expected {len(input_pngs)} PNGs, got {len(outputs)}.",
        [
            f"AI output frame count mismatch: expected {len(input_pngs)}, got {len(outputs)}.",
            f"Missing AI output filenames: {', '.join(missing[:12])}",
        ],
    )


def write_rgb_ai_inputs(input_pngs: list[Path], ai_input_rgb_dir: Path) -> list[Path]:
    ai_input_rgb_dir.mkdir(parents=True, exist_ok=True)
    created: list[Path] = []
    for source_path in input_pngs:
        dst = ai_input_rgb_dir / source_path.name
        with Image.open(source_path) as source_img:
            source_img.convert("RGB").save(dst)
        created.append(dst)
    return created


def normalize_ai_frames(
    input_pngs: list[Path],
    ai_outputs: list[Path],
    frames_dir: Path,
    target_size: tuple[int, int],
    alpha_policy: str,
    alpha_threshold: int,
) -> tuple[list[Path], dict[str, Any]]:
    frames_dir.mkdir(parents=True, exist_ok=True)
    created: list[Path] = []
    ai_had_alpha_flags: list[bool] = []
    recombined_frames: list[str] = [] if alpha_policy == "trust-ai-alpha" else [path.name for path in input_pngs]

    for source_path, ai_path in zip(input_pngs, ai_outputs):
        dst = frames_dir / source_path.name
        with Image.open(source_path) as source_img, Image.open(ai_path) as ai_img:
            source_rgba = source_img.convert("RGBA")
            ai_had_alpha = "A" in ai_img.getbands()
            ai_had_alpha_flags.append(ai_had_alpha)
            if alpha_policy == "trust-ai-alpha":
                normalized = ai_img.convert("RGBA").resize(target_size, resample=Image.Resampling.LANCZOS)
            else:
                rgb = ai_img.convert("RGB").resize(target_size, resample=Image.Resampling.LANCZOS)
                source_alpha = source_alpha_for_policy(source_rgba, target_size, alpha_policy, alpha_threshold)
                normalized = Image.merge("RGBA", (*rgb.split(), source_alpha))
            normalized.save(dst)
        created.append(dst)

    stats = {
        "aiOutputHadAlpha": all(ai_had_alpha_flags) if ai_had_alpha_flags else False,
        "aiOutputFramesWithAlpha": sum(1 for value in ai_had_alpha_flags if value),
        "aiInputMode": "source-rgba" if alpha_policy == "trust-ai-alpha" else "source-rgb",
        "alphaPolicy": alpha_policy,
        "rgbResamplingMode": "waifu2x-output-lanczos",
        "alphaResamplingMode": alpha_resampling_mode(alpha_policy),
        "alphaRecombinedFromSource": alpha_uses_source_mask(alpha_policy),
        "alphaRecombinedFrameCount": len(recombined_frames),
        "alphaRecombinedFrames": recombined_frames,
    }
    return created, stats


def resize_frames(
    paths: list[Path],
    frames_dir: Path,
    size: tuple[int, int],
    resample: int,
    alpha_policy: str,
    alpha_threshold: int,
) -> tuple[list[Path], dict[str, Any]]:
    frames_dir.mkdir(parents=True, exist_ok=True)
    created: list[Path] = []
    recombined_frames: list[str] = [] if alpha_policy == "trust-ai-alpha" else [path.name for path in paths]
    for src in paths:
        dst = frames_dir / src.name
        with Image.open(src) as img:
            source_rgba = img.convert("RGBA")
            if alpha_policy == "trust-ai-alpha":
                out = source_rgba.resize(size, resample=resample)
            else:
                rgb = source_rgba.convert("RGB").resize(size, resample=resample)
                source_alpha = source_alpha_for_policy(source_rgba, size, alpha_policy, alpha_threshold)
                out = Image.merge("RGBA", (*rgb.split(), source_alpha))
            out.save(dst)
        created.append(dst)
    stats = {
        "aiOutputHadAlpha": None,
        "aiOutputFramesWithAlpha": None,
        "aiInputMode": None,
        "alphaPolicy": alpha_policy,
        "rgbResamplingMode": resampling_name(resample),
        "alphaResamplingMode": alpha_resampling_mode(alpha_policy),
        "alphaRecombinedFromSource": alpha_uses_source_mask(alpha_policy),
        "alphaRecombinedFrameCount": len(recombined_frames),
        "alphaRecombinedFrames": recombined_frames,
    }
    return created, stats


def blank_frame_names(paths: list[Path]) -> list[str]:
    blanks = []
    for path in paths:
        with Image.open(path) as img:
            rgba = img.convert("RGBA")
            if alpha_is_empty(rgba.getchannel("A")):
                blanks.append(path.name)
    return blanks


def final_alpha_preserved(paths: list[Path]) -> bool:
    for path in paths:
        with Image.open(path) as img:
            if "A" not in img.getbands():
                return False
    return True


def histogram_count(histogram: list[int], start: int, end: int) -> int:
    start = max(0, min(255, start))
    end = max(0, min(255, end))
    if end < start:
        return 0
    return sum(histogram[start : end + 1])


def histogram_max(histogram: list[int], start: int, end: int = 255) -> int:
    start = max(0, min(255, start))
    end = max(0, min(255, end))
    for value in range(end, start - 1, -1):
        if histogram[value]:
            return value
    return 0


def histogram_median(histogram: list[int], start: int, end: int = 255) -> int:
    start = max(0, min(255, start))
    end = max(0, min(255, end))
    total = histogram_count(histogram, start, end)
    if total == 0:
        return 0
    target = (total + 1) // 2
    running = 0
    for value in range(start, end + 1):
        running += histogram[value]
        if running >= target:
            return value
    return 0


def count_mask_pixels(mask: Image.Image) -> int:
    histogram = mask.histogram()
    return sum(histogram[1:])


def edge_touch_metrics(mask: Image.Image, padding: int) -> dict[str, Any]:
    width, height = mask.size
    if width < 1 or height < 1:
        return {
            "edgeTouchSides": {"top": False, "bottom": False, "left": False, "right": False},
            "edgeTouchSideCount": 0,
            "edgeTouchPixelCount": 0,
            "edgeTouchRatio": 0.0,
        }
    pad_x = max(1, min(padding, width))
    pad_y = max(1, min(padding, height))
    side_pixels = {
        "top": count_mask_pixels(mask.crop((0, 0, width, pad_y))),
        "bottom": count_mask_pixels(mask.crop((0, max(0, height - pad_y), width, height))),
        "left": count_mask_pixels(mask.crop((0, 0, pad_x, height))),
        "right": count_mask_pixels(mask.crop((max(0, width - pad_x), 0, width, height))),
    }
    side_flags = {side: count > 0 for side, count in side_pixels.items()}
    edge_mask = Image.new("L", mask.size, 0)
    draw = ImageDraw.Draw(edge_mask)
    draw.rectangle((0, 0, width - 1, pad_y - 1), fill=255)
    draw.rectangle((0, height - pad_y, width - 1, height - 1), fill=255)
    draw.rectangle((0, 0, pad_x - 1, height - 1), fill=255)
    draw.rectangle((width - pad_x, 0, width - 1, height - 1), fill=255)
    edge_pixels = count_mask_pixels(ImageChops.multiply(mask, edge_mask))
    opaque_pixels = count_mask_pixels(mask)
    ratio = edge_pixels / opaque_pixels if opaque_pixels else 0.0
    return {
        "edgeTouchSides": side_flags,
        "edgeTouchSideCount": sum(1 for touched in side_flags.values() if touched),
        "edgeTouchPixelCount": edge_pixels,
        "edgeTouchRatio": round(ratio, 8),
    }


def analyze_alpha_frame(
    source_path: Path,
    final_path: Path,
    alpha_policy: str,
    alpha_threshold: int,
    low_alpha_threshold: int,
    edge_touch_padding: int,
) -> dict[str, Any]:
    with Image.open(source_path) as source_img, Image.open(final_path) as final_img:
        source_rgba = source_img.convert("RGBA")
        final_rgba = final_img.convert("RGBA")
        final_alpha = final_rgba.getchannel("A")
        final_size = final_rgba.size
        comparison_policy = alpha_comparison_policy(alpha_policy)
        source_alpha = source_alpha_for_policy(source_rgba, final_size, comparison_policy, alpha_threshold)
        source_mask = threshold_alpha_image(source_alpha, alpha_threshold)
        final_mask = threshold_alpha_image(final_alpha, alpha_threshold)
        outside_source_mask = source_mask.point(lambda value: 0 if value else 255)
        extra_alpha = ImageChops.multiply(final_alpha, outside_source_mask)
        extra_histogram = extra_alpha.histogram()
        extra_count = histogram_count(extra_histogram, alpha_threshold, 255)
        low_count = histogram_count(extra_histogram, 1, low_alpha_threshold)
        total_pixels = final_size[0] * final_size[1]
        bbox = final_mask.getbbox()
        if bbox:
            bbox_area = (bbox[2] - bbox[0]) * (bbox[3] - bbox[1])
            bbox_area_ratio = bbox_area / total_pixels if total_pixels else 0.0
            full_frame_bbox = bbox == (0, 0, final_size[0], final_size[1])
            bbox_value: list[int] | None = [bbox[0], bbox[1], bbox[2], bbox[3]]
        else:
            bbox_area = 0
            bbox_area_ratio = 0.0
            full_frame_bbox = False
            bbox_value = None
        edge = edge_touch_metrics(final_mask, edge_touch_padding)
        severe_edge_touch = (
            edge["edgeTouchSideCount"] >= 3
            or edge["edgeTouchRatio"] >= SEVERE_EDGE_TOUCH_RATIO
        )
        crop_risk = full_frame_bbox or severe_edge_touch or bbox_area_ratio >= CROP_RISK_BBOX_AREA_RATIO
        return {
            "frame": source_path.name,
            "sourceFrame": rel(source_path),
            "finalFrame": rel(final_path),
            "sourceMaskSize": {"width": source_rgba.width, "height": source_rgba.height},
            "finalSize": {"width": final_size[0], "height": final_size[1]},
            "extraAlphaPixelCount": extra_count,
            "extraAlphaRatio": round(extra_count / total_pixels, 8) if total_pixels else 0.0,
            "extraAlphaMedian": histogram_median(extra_histogram, alpha_threshold, 255),
            "extraAlphaMax": histogram_max(extra_histogram, alpha_threshold, 255),
            "lowAlphaPixelCount": low_count,
            "lowAlphaRatio": round(low_count / total_pixels, 8) if total_pixels else 0.0,
            "lowAlphaMedian": histogram_median(extra_histogram, 1, low_alpha_threshold),
            "lowAlphaMax": histogram_max(extra_histogram, 1, low_alpha_threshold),
            "bbox": bbox_value,
            "bboxAreaPixels": bbox_area,
            "bboxAreaRatio": round(bbox_area_ratio, 8),
            "fullFrameBbox": full_frame_bbox,
            "cropRisk": crop_risk,
            "severeEdgeTouch": severe_edge_touch,
            **edge,
            "_extraAlphaHistogram": extra_histogram,
        }


def worst_alpha_frames(frames: list[dict[str, Any]], key: str) -> list[dict[str, Any]]:
    selected = [frame for frame in frames if frame.get(key, 0)]
    selected.sort(key=lambda frame: (-frame.get(key, 0), frame["frame"]))
    return [
        {
            "frame": frame["frame"],
            key: frame[key],
            "extraAlphaRatio": frame.get("extraAlphaRatio"),
            "lowAlphaRatio": frame.get("lowAlphaRatio"),
            "edgeTouchRatio": frame.get("edgeTouchRatio"),
            "edgeTouchSideCount": frame.get("edgeTouchSideCount"),
            "bboxAreaRatio": frame.get("bboxAreaRatio"),
        }
        for frame in selected[:WORST_FRAME_LIMIT]
    ]


def build_alpha_policy_diagnostic(
    input_pngs: list[Path],
    created_files: list[Path],
    backend: str,
    alpha_policy: str,
    alpha_threshold: int,
    low_alpha_threshold: int,
    extra_alpha_fail_ratio: float,
    edge_touch_padding: int,
) -> dict[str, Any]:
    frames = [
        analyze_alpha_frame(
            source_path,
            final_path,
            alpha_policy,
            alpha_threshold,
            low_alpha_threshold,
            edge_touch_padding,
        )
        for source_path, final_path in zip(input_pngs, created_files)
    ]
    total_pixels = sum(frame["finalSize"]["width"] * frame["finalSize"]["height"] for frame in frames)
    extra_histogram = [0] * 256
    for frame in frames:
        histogram = frame.pop("_extraAlphaHistogram")
        for index, count in enumerate(histogram):
            extra_histogram[index] += count
    extra_count = sum(frame["extraAlphaPixelCount"] for frame in frames)
    low_count = sum(frame["lowAlphaPixelCount"] for frame in frames)
    edge_touch_frames = [frame for frame in frames if frame["edgeTouchSideCount"] > 0]
    severe_edge_frames = [frame for frame in frames if frame["severeEdgeTouch"]]
    full_frame_bbox_frames = [frame for frame in frames if frame["fullFrameBbox"]]
    crop_risk_frames = [frame for frame in frames if frame["cropRisk"]]
    return {
        "version": 1,
        "backend": backend,
        "alphaPolicy": alpha_policy,
        "rgbResamplingMode": rgb_resampling_mode(backend),
        "alphaResamplingMode": alpha_resampling_mode(alpha_policy),
        "sourceMaskSizes": count_sizes(input_pngs),
        "finalFrameSizes": count_sizes(created_files),
        "sourceMaskComparisonPolicy": alpha_comparison_policy(alpha_policy),
        "thresholds": {
            "alphaThreshold": alpha_threshold,
            "lowAlphaThreshold": low_alpha_threshold,
            "extraAlphaFailRatio": extra_alpha_fail_ratio,
            "edgeTouchPadding": edge_touch_padding,
            "severeEdgeTouchRatio": SEVERE_EDGE_TOUCH_RATIO,
            "cropRiskBboxAreaRatio": CROP_RISK_BBOX_AREA_RATIO,
        },
        "frameCount": len(frames),
        "totalPixelCount": total_pixels,
        "extraAlphaPixelCount": extra_count,
        "extraAlphaRatio": round(extra_count / total_pixels, 8) if total_pixels else 0.0,
        "extraAlphaMedian": histogram_median(extra_histogram, alpha_threshold, 255),
        "extraAlphaMax": histogram_max(extra_histogram, alpha_threshold, 255),
        "worstExtraAlphaFrames": worst_alpha_frames(frames, "extraAlphaPixelCount"),
        "lowAlphaPixelCount": low_count,
        "lowAlphaRatio": round(low_count / total_pixels, 8) if total_pixels else 0.0,
        "lowAlphaMedian": histogram_median(extra_histogram, 1, low_alpha_threshold),
        "lowAlphaMax": histogram_max(extra_histogram, 1, low_alpha_threshold),
        "worstLowAlphaFrames": worst_alpha_frames(frames, "lowAlphaPixelCount"),
        "edgeTouchFrameCount": len(edge_touch_frames),
        "severeEdgeTouchFrameCount": len(severe_edge_frames),
        "worstEdgeTouchFrames": worst_alpha_frames(frames, "edgeTouchPixelCount"),
        "fullFrameBboxFrameCount": len(full_frame_bbox_frames),
        "fullFrameBboxFrames": [frame["frame"] for frame in full_frame_bbox_frames[:WORST_FRAME_LIMIT]],
        "cropRiskFrameCount": len(crop_risk_frames),
        "cropRiskFrames": [
            {
                "frame": frame["frame"],
                "bboxAreaRatio": frame["bboxAreaRatio"],
                "edgeTouchSideCount": frame["edgeTouchSideCount"],
                "edgeTouchRatio": frame["edgeTouchRatio"],
                "fullFrameBbox": frame["fullFrameBbox"],
            }
            for frame in crop_risk_frames[:WORST_FRAME_LIMIT]
        ],
        "frames": frames,
    }


def alpha_policy_failure_reasons(alpha_diagnostic: dict[str, Any]) -> list[str]:
    reasons: list[str] = []
    thresholds = alpha_diagnostic.get("thresholds") or {}
    extra_ratio = float(alpha_diagnostic.get("extraAlphaRatio") or 0.0)
    extra_threshold = float(thresholds.get("extraAlphaFailRatio") or DEFAULT_EXTRA_ALPHA_FAIL_RATIO)
    if extra_ratio > extra_threshold:
        reasons.append(
            "Extra alpha outside source mask exceeded threshold: "
            f"{alpha_diagnostic['extraAlphaPixelCount']} px, ratio={extra_ratio:.8f}, threshold={extra_threshold:.8f}."
        )
    full_frame_frames = alpha_diagnostic.get("fullFrameBboxFrames") or []
    if full_frame_frames:
        reasons.append(f"Full-frame alpha bounding boxes detected: {', '.join(full_frame_frames[:WORST_FRAME_LIMIT])}.")
    severe_count = int(alpha_diagnostic.get("severeEdgeTouchFrameCount") or 0)
    if severe_count:
        worst = alpha_diagnostic.get("worstEdgeTouchFrames") or []
        names = ", ".join(item["frame"] for item in worst[:WORST_FRAME_LIMIT])
        reasons.append(f"Severe edge-touch alpha detected in {severe_count} frame(s): {names}.")
    return reasons


def alpha_policy_warning_messages(alpha_diagnostic: dict[str, Any]) -> list[str]:
    low_count = int(alpha_diagnostic.get("lowAlphaPixelCount") or 0)
    if not low_count:
        return []
    ratio = float(alpha_diagnostic.get("lowAlphaRatio") or 0.0)
    return [
        "Low-alpha residue outside the source mask detected: "
        f"{low_count} px, ratio={ratio:.8f}. This is a hard warning, not an automatic failure."
    ]


def alpha_diagnostic_summary(alpha_diagnostic: dict[str, Any] | None) -> dict[str, Any] | None:
    if not alpha_diagnostic:
        return None
    keys = [
        "alphaPolicy",
        "rgbResamplingMode",
        "alphaResamplingMode",
        "sourceMaskSizes",
        "finalFrameSizes",
        "sourceMaskComparisonPolicy",
        "thresholds",
        "frameCount",
        "totalPixelCount",
        "extraAlphaPixelCount",
        "extraAlphaRatio",
        "extraAlphaMedian",
        "extraAlphaMax",
        "worstExtraAlphaFrames",
        "lowAlphaPixelCount",
        "lowAlphaRatio",
        "lowAlphaMedian",
        "lowAlphaMax",
        "worstLowAlphaFrames",
        "edgeTouchFrameCount",
        "severeEdgeTouchFrameCount",
        "worstEdgeTouchFrames",
        "fullFrameBboxFrameCount",
        "fullFrameBboxFrames",
        "cropRiskFrameCount",
        "cropRiskFrames",
    ]
    return {key: alpha_diagnostic.get(key) for key in keys}


def write_alpha_diagnostic_markdown(path: Path, diagnostic: dict[str, Any]) -> None:
    lines = [
        "# Alpha Policy Diagnostic",
        "",
        f"- Backend: `{diagnostic['backend']}`",
        f"- Alpha policy: `{diagnostic['alphaPolicy']}`",
        f"- RGB resampling mode: `{diagnostic['rgbResamplingMode']}`",
        f"- Alpha resampling mode: `{diagnostic['alphaResamplingMode']}`",
        f"- Frame count: `{diagnostic['frameCount']}`",
        f"- Extra alpha pixels: `{diagnostic['extraAlphaPixelCount']}` (`{diagnostic['extraAlphaRatio']}`)",
        f"- Low-alpha pixels: `{diagnostic['lowAlphaPixelCount']}` (`{diagnostic['lowAlphaRatio']}`)",
        f"- Edge-touch frames: `{diagnostic['edgeTouchFrameCount']}`",
        f"- Full-frame bbox frames: `{diagnostic['fullFrameBboxFrameCount']}`",
        f"- Crop-risk frames: `{diagnostic['cropRiskFrameCount']}`",
        "",
        "## Worst Extra Alpha Frames",
        "",
    ]
    if diagnostic["worstExtraAlphaFrames"]:
        for item in diagnostic["worstExtraAlphaFrames"]:
            lines.append(
                f"- `{item['frame']}`: `{item['extraAlphaPixelCount']}` px, "
                f"ratio `{item['extraAlphaRatio']}`"
            )
    else:
        lines.append("- none")
    lines.extend(["", "## Worst Low-Alpha Frames", ""])
    if diagnostic["worstLowAlphaFrames"]:
        for item in diagnostic["worstLowAlphaFrames"]:
            lines.append(
                f"- `{item['frame']}`: `{item['lowAlphaPixelCount']}` px, "
                f"ratio `{item['lowAlphaRatio']}`"
            )
    else:
        lines.append("- none")
    lines.extend(["", "## Worst Edge-Touch Frames", ""])
    if diagnostic["worstEdgeTouchFrames"]:
        for item in diagnostic["worstEdgeTouchFrames"]:
            lines.append(
                f"- `{item['frame']}`: sides `{item['edgeTouchSideCount']}`, "
                f"ratio `{item['edgeTouchRatio']}`"
            )
    else:
        lines.append("- none")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_alpha_diagnostic_files(output_dir: Path, diagnostic: dict[str, Any]) -> tuple[Path, Path]:
    review_diagnostics_dir = output_dir / "review_diagnostics"
    review_diagnostics_dir.mkdir(parents=True, exist_ok=True)
    json_path = review_diagnostics_dir / "alpha_policy_diagnostic.json"
    md_path = review_diagnostics_dir / "alpha_policy_diagnostic.md"
    json_path.write_text(json.dumps(diagnostic, indent=2, ensure_ascii=False), encoding="utf-8")
    write_alpha_diagnostic_markdown(md_path, diagnostic)
    return json_path, md_path


def evaluate_quality(
    backend: str,
    input_pngs: list[Path],
    created_files: list[Path],
    target_size: tuple[int, int],
    alpha_preserved: bool,
    failure_reasons: list[str] | None = None,
    dry_run: bool = False,
    alpha_diagnostic: dict[str, Any] | None = None,
) -> tuple[str, list[str]]:
    fail_reasons = list(failure_reasons or [])
    ambiguous_reasons: list[str] = []

    if dry_run:
        return "ambiguous", ["Dry run did not execute the backend or create candidate frames."]
    if len(created_files) != len(input_pngs):
        fail_reasons.append(f"Frame count mismatch: input={len(input_pngs)}, output={len(created_files)}.")
    missing = [path.name for path in created_files if not path.exists()]
    if missing:
        fail_reasons.append(f"Output files missing after generation: {', '.join(missing[:12])}.")
    sizes = count_sizes(created_files) if created_files else []
    invalid_sizes = [
        size for size in sizes if (size["width"], size["height"]) != target_size
    ]
    if invalid_sizes:
        rendered = ", ".join(f"{item['width']}x{item['height']}={item['count']}" for item in invalid_sizes)
        fail_reasons.append(f"Target canvas mismatch in final frames: {rendered}.")
    if created_files and not alpha_preserved:
        fail_reasons.append("Final output alpha channel was not preserved.")
    blanks = blank_frame_names(created_files) if created_files else []
    if blanks:
        fail_reasons.append(f"Blank or fully transparent output frames detected: {', '.join(blanks[:12])}.")
    if alpha_diagnostic:
        fail_reasons.extend(alpha_policy_failure_reasons(alpha_diagnostic))
    if backend == "waifu2x" and created_files and len(input_pngs) < 8:
        ambiguous_reasons.append("Fewer than 8 frames are available for the required AI contact sheet sampling.")

    if fail_reasons:
        return "fail", fail_reasons
    if ambiguous_reasons:
        return "ambiguous", ambiguous_reasons
    return "pass", ["Objective candidate checks passed."]


def choose_review_positions(frame_count: int, desired: int = 8) -> list[int]:
    if frame_count <= desired:
        return list(range(frame_count))
    positions = []
    for index in range(desired):
        positions.append(round(index * (frame_count - 1) / max(1, desired - 1)))
    return sorted(set(positions))


def checkerboard(size: tuple[int, int], tile: int = 16) -> Image.Image:
    image = Image.new("RGBA", size, (255, 255, 255, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            color = (72, 72, 72, 255) if ((x // tile + y // tile) % 2) else (28, 28, 28, 255)
            draw.rectangle([x, y, x + tile - 1, y + tile - 1], fill=color)
    return image


def render_preview(path: Path, cell: tuple[int, int], background: str) -> Image.Image:
    with Image.open(path) as img:
        rgba = img.convert("RGBA")
    if background == "dark":
        bg = Image.new("RGBA", rgba.size, (18, 18, 18, 255))
    else:
        bg = checkerboard(rgba.size)
    bg.alpha_composite(rgba)
    preview = bg.convert("RGB")
    preview.thumbnail(cell, Image.Resampling.NEAREST)
    canvas = Image.new("RGB", cell, (18, 18, 18))
    x = (cell[0] - preview.width) // 2
    y = (cell[1] - preview.height) // 2
    canvas.paste(preview, (x, y))
    return canvas


def reference_lookup(args: argparse.Namespace) -> tuple[list[Path], Path | None]:
    reference_frames: list[Path] = []
    reference_image: Path | None = None
    if args.reference_dir:
        reference_dir = resolve_project_path(args.reference_dir)
        if reference_dir.exists() and reference_dir.is_dir():
            reference_frames = sorted(
                [p for p in reference_dir.iterdir() if p.is_file() and p.suffix.lower() == ".png"],
                key=numeric_key,
            )
    if args.reference_image:
        reference_image = resolve_project_path(args.reference_image)
    return reference_frames, reference_image


def select_reference_path(
    source_path: Path,
    position: int,
    reference_frames: list[Path],
    reference_image: Path | None,
) -> Path | None:
    if reference_image:
        return reference_image
    if not reference_frames:
        return None
    by_name = {path.name: path for path in reference_frames}
    if source_path.name in by_name:
        return by_name[source_path.name]
    if position < len(reference_frames):
        return reference_frames[position]
    return None


def build_ai_review_packet(
    asset_id: str,
    input_pngs: list[Path],
    final_pngs: list[Path],
    output_dir: Path,
    args: argparse.Namespace,
    backend: str,
) -> dict[str, Any] | None:
    if backend != "waifu2x" or not final_pngs:
        return None

    review_dir = output_dir / "review"
    assert_inside_output_backend(review_dir, output_dir, backend)
    review_dir.mkdir(parents=True, exist_ok=True)
    reference_frames, reference_image = reference_lookup(args)
    positions = choose_review_positions(len(input_pngs), desired=8)

    source_by_position = {index: path for index, path in enumerate(input_pngs)}
    final_by_position = {index: path for index, path in enumerate(final_pngs)}
    has_reference = bool(reference_frames or reference_image)
    columns = ["source checker", "source dark"]
    if has_reference:
        columns.extend(["reference checker", "reference dark"])
    columns.extend(["waifu2x checker", "waifu2x dark"])

    cell = (180, 180)
    label_h = 24
    header_h = 34
    row_h = cell[1] + label_h
    sheet = Image.new("RGB", (len(columns) * cell[0], header_h + len(positions) * row_h), (18, 18, 18))
    draw = ImageDraw.Draw(sheet)
    for col, label in enumerate(columns):
        draw.text((col * cell[0] + 6, 10), label, fill=(255, 255, 255))

    sampled = []
    for row, position in enumerate(positions):
        source_path = source_by_position[position]
        final_path = final_by_position[position]
        reference_path = select_reference_path(source_path, position, reference_frames, reference_image)
        row_paths: list[tuple[str, Path]] = [("source checker", source_path), ("source dark", source_path)]
        if has_reference and reference_path:
            row_paths.extend([("reference checker", reference_path), ("reference dark", reference_path)])
        elif has_reference:
            row_paths.extend([("reference checker", final_path), ("reference dark", final_path)])
        row_paths.extend([("waifu2x checker", final_path), ("waifu2x dark", final_path)])

        y = header_h + row * row_h
        for col, (label, path) in enumerate(row_paths):
            bg = "dark" if label.endswith("dark") else "checker"
            x = col * cell[0]
            sheet.paste(render_preview(path, cell, bg), (x, y))
            width, height = read_image_size(path)
            draw.text((x + 6, y + cell[1] + 3), f"{path.name} {width}x{height}", fill=(255, 255, 255))
        sampled.append(
            {
                "position": position,
                "sourceFrame": source_path.name,
                "candidateFrame": final_path.name,
                "referenceFrame": reference_path.name if reference_path else None,
            }
        )

    contact_sheet = review_dir / "ai_upscale_contact_sheet.png"
    sheet.save(contact_sheet)
    report = {
        "assetId": asset_id,
        "backend": backend,
        "aiBackend": AI_BACKEND_NAME,
        "contactSheet": rel(contact_sheet),
        "sampledFrames": sampled,
        "backgrounds": ["checker", "dark"],
        "comparisonColumns": columns,
        "runtimePromotionPerformed": False,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }
    json_path = review_dir / "ai_upscale_review_report.json"
    md_path = review_dir / "ai_upscale_review_report.md"
    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    write_ai_review_markdown(md_path, report)
    report["createdFiles"] = [contact_sheet, json_path, md_path]
    return report


def write_ai_review_markdown(path: Path, report: dict[str, Any]) -> None:
    lines = [
        "# AI Upscale Review Report",
        "",
        f"- Asset ID: `{report['assetId']}`",
        f"- Backend: `{report['backend']}`",
        f"- AI backend: `{report['aiBackend']}`",
        f"- Contact sheet: `{report['contactSheet']}`",
        f"- Runtime promotion performed: `{str(report['runtimePromotionPerformed']).lower()}`",
        "",
        "## Sampled Frames",
        "",
    ]
    for item in report["sampledFrames"]:
        reference = item["referenceFrame"] or "none"
        lines.append(
            f"- position `{item['position']}`: source `{item['sourceFrame']}`, "
            f"reference `{reference}`, candidate `{item['candidateFrame']}`"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_report(
    asset_id: str,
    backend: str,
    input_dir: Path,
    output_dir: Path,
    target_width: int,
    target_height: int,
    target_size_source: str,
    input_pngs: list[Path],
    ignored_files: list[Path],
    existing_outputs: list[Path],
    created_files: list[Path],
    skipped_files: list[Path],
    dry_run: bool,
    args: argparse.Namespace,
    ai_exe: Path | None,
    ai_model_path: Path | None,
    ai_temp_sizes: list[dict[str, int]],
    final_sizes: list[dict[str, int]],
    ai_stats: dict[str, Any],
    target_audit: dict[str, Any] | None,
    quality_status: str,
    quality_reasons: list[str],
    extra_warnings: list[str] | None = None,
    ai_command: list[str] | None = None,
    ai_stdout: str | None = None,
    ai_stderr: str | None = None,
    alpha_diagnostic: dict[str, Any] | None = None,
    alpha_diagnostic_paths: tuple[Path, Path] | None = None,
) -> dict[str, Any]:
    source_alpha_ok, missing_alpha_files = alpha_channel_status(input_pngs)
    warnings = list(extra_warnings or [])
    if missing_alpha_files:
        preview = ", ".join(missing_alpha_files[:12])
        suffix = "..." if len(missing_alpha_files) > 12 else ""
        warnings.append(f"Input frames without alpha channel were converted to opaque RGBA: {preview}{suffix}")
    if len(count_sizes(input_pngs)) > 1:
        warnings.append("Input frames have mixed source dimensions.")
    if ignored_files:
        warnings.append(f"Ignored {len(ignored_files)} non-frame or non-PNG file(s) in input directory.")
    if dry_run and existing_outputs:
        warnings.append("Dry run only: existing output files would be overwritten if --overwrite-candidates is used.")

    alpha_before = alpha_unique_values(input_pngs)
    alpha_after = alpha_unique_values(created_files) if created_files else []
    alpha_preserved = final_alpha_preserved(created_files) if created_files else source_alpha_ok and not dry_run
    alpha_summary = alpha_diagnostic_summary(alpha_diagnostic)
    alpha_paths = (
        {
            "json": rel(alpha_diagnostic_paths[0]),
            "md": rel(alpha_diagnostic_paths[1]),
        }
        if alpha_diagnostic_paths
        else None
    )
    report: dict[str, Any] = {
        "assetId": asset_id,
        "backend": backend,
        "method": backend,
        "aiBackend": AI_BACKEND_NAME if backend == "waifu2x" else None,
        "aiUpscalerExe": rel(ai_exe),
        "aiScale": args.ai_scale if backend == "waifu2x" else None,
        "aiNoise": args.ai_noise if backend == "waifu2x" else None,
        "aiModelPath": rel(ai_model_path),
        "aiGpuId": args.ai_gpu_id if backend == "waifu2x" else None,
        "aiTileSize": args.ai_tile_size if backend == "waifu2x" else None,
        "inputDir": rel(input_dir),
        "outputDir": rel(output_dir),
        "framesDir": rel(output_dir / "frames"),
        "targetWidth": target_width,
        "targetHeight": target_height,
        "targetSizeSource": target_size_source,
        "targetSizeAudit": target_audit,
        "inputFrameCount": len(input_pngs),
        "outputFrameCount": len(created_files),
        "ignoredFileCount": len(ignored_files),
        "sourceFrameSizes": count_sizes(input_pngs),
        "aiTempFrameSizes": ai_temp_sizes,
        "finalFrameSizes": final_sizes,
        "sourceFramesHadAlpha": source_alpha_ok,
        "alphaPolicy": args.alpha_policy,
        "rgbResamplingMode": ai_stats.get("rgbResamplingMode") or rgb_resampling_mode(backend),
        "alphaResamplingMode": ai_stats.get("alphaResamplingMode") or alpha_resampling_mode(args.alpha_policy),
        "sourceMaskSizes": (alpha_summary or {}).get("sourceMaskSizes") or count_sizes(input_pngs),
        "finalMaskSizes": (alpha_summary or {}).get("finalFrameSizes") or final_sizes,
        "alphaDiagnosticPaths": alpha_paths,
        "alphaDiagnosticSummary": alpha_summary,
        "extraAlphaPixelCount": (alpha_summary or {}).get("extraAlphaPixelCount"),
        "extraAlphaRatio": (alpha_summary or {}).get("extraAlphaRatio"),
        "extraAlphaMedian": (alpha_summary or {}).get("extraAlphaMedian"),
        "extraAlphaMax": (alpha_summary or {}).get("extraAlphaMax"),
        "worstExtraAlphaFrames": (alpha_summary or {}).get("worstExtraAlphaFrames"),
        "lowAlphaPixelCount": (alpha_summary or {}).get("lowAlphaPixelCount"),
        "lowAlphaRatio": (alpha_summary or {}).get("lowAlphaRatio"),
        "worstLowAlphaFrames": (alpha_summary or {}).get("worstLowAlphaFrames"),
        "edgeTouchFrameCount": (alpha_summary or {}).get("edgeTouchFrameCount"),
        "severeEdgeTouchFrameCount": (alpha_summary or {}).get("severeEdgeTouchFrameCount"),
        "worstEdgeTouchFrames": (alpha_summary or {}).get("worstEdgeTouchFrames"),
        "fullFrameBboxFrameCount": (alpha_summary or {}).get("fullFrameBboxFrameCount"),
        "fullFrameBboxFrames": (alpha_summary or {}).get("fullFrameBboxFrames"),
        "cropRiskFrameCount": (alpha_summary or {}).get("cropRiskFrameCount"),
        "cropRiskFrames": (alpha_summary or {}).get("cropRiskFrames"),
        "aiInputMode": ai_stats.get("aiInputMode"),
        "aiOutputHadAlpha": ai_stats.get("aiOutputHadAlpha"),
        "aiOutputFramesWithAlpha": ai_stats.get("aiOutputFramesWithAlpha"),
        "alphaRecombinedFromSource": ai_stats.get("alphaRecombinedFromSource", False),
        "alphaRecombinedFrameCount": ai_stats.get("alphaRecombinedFrameCount", 0),
        "alphaRecombinedFrames": ai_stats.get("alphaRecombinedFrames", []),
        "alphaPreserved": alpha_preserved,
        "alphaUniqueValuesBefore": alpha_before,
        "alphaUniqueValuesAfter": alpha_after,
        "wouldOverwriteCount": len(existing_outputs),
        "createdFiles": [rel(path) for path in created_files],
        "skippedFiles": [rel(path) for path in skipped_files],
        "warnings": warnings,
        "destructiveOperations": [],
        "promotionPerformed": False,
        "dryRun": dry_run,
        "qualityGateStatus": quality_status,
        "qualityGateReasons": quality_reasons,
        "defaultAiEscalationMessage": (
            DEFAULT_AI_ESCALATION_MESSAGE
            if backend == "waifu2x" and quality_status in {"fail", "ambiguous"}
            else None
        ),
        "aiCommand": ai_command,
        "aiStdout": ai_stdout,
        "aiStderr": ai_stderr,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
    }
    return report


def write_markdown_report(path: Path, report: dict[str, Any]) -> None:
    lines = [
        "# Upscale Candidate Report",
        "",
        f"- Asset ID: `{report['assetId']}`",
        f"- Backend: `{report['backend']}`",
        f"- Method: `{report['method']}`",
        f"- AI backend: `{report['aiBackend']}`",
        f"- AI upscaler executable: `{report['aiUpscalerExe']}`",
        f"- Input directory: `{report['inputDir']}`",
        f"- Output directory: `{report['outputDir']}`",
        f"- Frames directory: `{report['framesDir']}`",
        f"- Target size: `{report['targetWidth']}x{report['targetHeight']}`",
        f"- Target size source: `{report['targetSizeSource']}`",
        f"- Input frame count: `{report['inputFrameCount']}`",
        f"- Output frame count: `{report['outputFrameCount']}`",
        f"- Ignored file count: `{report['ignoredFileCount']}`",
        f"- Would overwrite count: `{report['wouldOverwriteCount']}`",
        f"- Alpha policy: `{report['alphaPolicy']}`",
        f"- RGB resampling mode: `{report['rgbResamplingMode']}`",
        f"- Alpha resampling mode: `{report['alphaResamplingMode']}`",
        f"- Alpha diagnostic JSON: `{(report['alphaDiagnosticPaths'] or {}).get('json') or 'none'}`",
        f"- Alpha diagnostic Markdown: `{(report['alphaDiagnosticPaths'] or {}).get('md') or 'none'}`",
        f"- AI input mode: `{report['aiInputMode']}`",
        f"- AI output had alpha: `{report['aiOutputHadAlpha']}`",
        f"- Alpha recombined from source: `{str(report['alphaRecombinedFromSource']).lower()}`",
        f"- Alpha preserved: `{str(report['alphaPreserved']).lower()}`",
        f"- Quality gate status: `{report['qualityGateStatus']}`",
        f"- Promotion performed: `{str(report['promotionPerformed']).lower()}`",
        f"- Destructive operations: `{len(report['destructiveOperations'])}`",
        "",
    ]
    if report.get("defaultAiEscalationMessage"):
        lines.extend([report["defaultAiEscalationMessage"], ""])

    lines.extend(["## Quality Gate Reasons", ""])
    lines.extend(f"- {reason}" for reason in report["qualityGateReasons"])

    lines.extend(["", "## Source Frame Sizes", ""])
    for size in report["sourceFrameSizes"]:
        lines.append(f"- `{size['width']}x{size['height']}`: `{size['count']}` frame(s)")

    lines.extend(["", "## AI Temp Frame Sizes", ""])
    if report["aiTempFrameSizes"]:
        for size in report["aiTempFrameSizes"]:
            lines.append(f"- `{size['width']}x{size['height']}`: `{size['count']}` frame(s)")
    else:
        lines.append("- none")

    lines.extend(["", "## Final Frame Sizes", ""])
    if report["finalFrameSizes"]:
        for size in report["finalFrameSizes"]:
            lines.append(f"- `{size['width']}x{size['height']}`: `{size['count']}` frame(s)")
    else:
        lines.append("- none")

    lines.extend(["", "## Alpha", ""])
    lines.append(f"- Source unique alpha values: `{report['alphaUniqueValuesBefore']}`")
    lines.append(f"- Final unique alpha values: `{report['alphaUniqueValuesAfter']}`")
    lines.append(f"- Recombined frame count: `{report['alphaRecombinedFrameCount']}`")
    lines.append(f"- Extra alpha pixels: `{report['extraAlphaPixelCount']}`")
    lines.append(f"- Extra alpha ratio: `{report['extraAlphaRatio']}`")
    lines.append(f"- Low-alpha pixels: `{report['lowAlphaPixelCount']}`")
    lines.append(f"- Low-alpha ratio: `{report['lowAlphaRatio']}`")
    lines.append(f"- Edge-touch frame count: `{report['edgeTouchFrameCount']}`")
    lines.append(f"- Full-frame bbox frame count: `{report['fullFrameBboxFrameCount']}`")
    lines.append(f"- Crop-risk frame count: `{report['cropRiskFrameCount']}`")

    lines.extend(["", "## Alpha Worst Frames", ""])
    if report.get("worstExtraAlphaFrames"):
        lines.append("Extra alpha:")
        lines.extend(f"- `{item['frame']}`: `{item['extraAlphaPixelCount']}` px" for item in report["worstExtraAlphaFrames"])
    else:
        lines.append("Extra alpha: none")
    if report.get("worstLowAlphaFrames"):
        lines.append("Low alpha:")
        lines.extend(f"- `{item['frame']}`: `{item['lowAlphaPixelCount']}` px" for item in report["worstLowAlphaFrames"])
    else:
        lines.append("Low alpha: none")
    if report.get("worstEdgeTouchFrames"):
        lines.append("Edge touch:")
        lines.extend(
            f"- `{item['frame']}`: sides `{item['edgeTouchSideCount']}`, ratio `{item['edgeTouchRatio']}`"
            for item in report["worstEdgeTouchFrames"]
        )
    else:
        lines.append("Edge touch: none")

    lines.extend(["", "## Warnings", ""])
    if report["warnings"]:
        lines.extend(f"- {warning}" for warning in report["warnings"])
    else:
        lines.append("- none")

    lines.extend(["", "## Created Files", ""])
    if report["createdFiles"]:
        lines.extend(f"- `{path}`" for path in report["createdFiles"])
    else:
        lines.append("- none")

    lines.extend(["", "## Skipped Files", ""])
    if report["skippedFiles"]:
        lines.extend(f"- `{path}`" for path in report["skippedFiles"])
    else:
        lines.append("- none")

    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_candidate_reports(output_dir: Path, report: dict[str, Any]) -> tuple[Path, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    json_path = output_dir / "upscale_candidate_report.json"
    md_path = output_dir / "upscale_candidate_report.md"
    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    write_markdown_report(md_path, report)
    return json_path, md_path


def report_failure_and_exit(
    asset_id: str,
    backend: str,
    input_dir: Path,
    output_dir: Path,
    target_width: int,
    target_height: int,
    target_size_source: str,
    input_pngs: list[Path],
    ignored_files: list[Path],
    existing_outputs: list[Path],
    args: argparse.Namespace,
    ai_exe: Path | None,
    ai_model_path: Path | None,
    target_audit: dict[str, Any] | None,
    reasons: list[str],
    message: str,
    write_files: bool,
) -> int:
    report = build_report(
        asset_id=asset_id,
        backend=backend,
        input_dir=input_dir,
        output_dir=output_dir,
        target_width=target_width,
        target_height=target_height,
        target_size_source=target_size_source,
        input_pngs=input_pngs,
        ignored_files=ignored_files,
        existing_outputs=existing_outputs,
        created_files=[],
        skipped_files=[],
        dry_run=args.dry_run,
        args=args,
        ai_exe=ai_exe,
        ai_model_path=ai_model_path,
        ai_temp_sizes=[],
        final_sizes=[],
        ai_stats={},
        target_audit=target_audit,
        quality_status="fail",
        quality_reasons=reasons,
        extra_warnings=[message],
    )
    print(message, file=sys.stderr)
    if write_files:
        report_paths = [output_dir / "upscale_candidate_report.json", output_dir / "upscale_candidate_report.md"]
        ensure_no_existing_outputs(report_paths, args.overwrite_candidates)
        report["createdFiles"] = [rel(path) for path in report_paths]
        write_candidate_reports(output_dir, report)
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return 2


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset-id", required=True)
    parser.add_argument("--input-dir", required=True)
    parser.add_argument("--backend", choices=BACKENDS)
    parser.add_argument("--method", choices=BACKENDS, help="Legacy alias for --backend.")
    parser.add_argument("--target-width", type=int)
    parser.add_argument("--target-height", type=int)
    parser.add_argument("--reference-image")
    parser.add_argument("--reference-dir")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--overwrite-candidates", action="store_true")
    parser.add_argument("--output-root", help="Defaults to SpritePipelineWork/<asset_id>/upscaled_runtime_candidates")
    parser.add_argument("--ai-upscaler-exe")
    parser.add_argument("--ai-scale", type=int, choices=(2, 4), default=2)
    parser.add_argument("--ai-noise", type=int, choices=(-1, 0, 1, 2, 3), default=0)
    parser.add_argument("--ai-model-path")
    parser.add_argument("--ai-gpu-id", type=int)
    parser.add_argument("--ai-tile-size", type=int)
    parser.add_argument("--keep-ai-temp", action="store_true")
    parser.add_argument("--alpha-policy", choices=ALPHA_POLICIES, default=DEFAULT_ALPHA_POLICY)
    parser.add_argument("--alpha-threshold", type=int, default=DEFAULT_ALPHA_THRESHOLD)
    parser.add_argument("--low-alpha-threshold", type=int, default=DEFAULT_LOW_ALPHA_THRESHOLD)
    parser.add_argument("--extra-alpha-fail-ratio", type=float, default=DEFAULT_EXTRA_ALPHA_FAIL_RATIO)
    parser.add_argument("--edge-touch-padding", type=int, default=DEFAULT_EDGE_TOUCH_PADDING)
    args = parser.parse_args()

    if args.ai_tile_size is not None and args.ai_tile_size < 1:
        raise SystemExit("--ai-tile-size must be at least 1 when provided.")
    if not 1 <= args.alpha_threshold <= 255:
        raise SystemExit("--alpha-threshold must be between 1 and 255.")
    if not 1 <= args.low_alpha_threshold <= 255:
        raise SystemExit("--low-alpha-threshold must be between 1 and 255.")
    if args.extra_alpha_fail_ratio < 0:
        raise SystemExit("--extra-alpha-fail-ratio must be non-negative.")
    if args.edge_touch_padding < 1:
        raise SystemExit("--edge-touch-padding must be at least 1.")

    asset_id = safe_asset_id(args.asset_id)
    backend = resolve_backend(args)
    target_width, target_height, target_size_source, target_audit = resolve_target_size(args)
    target_size = (target_width, target_height)
    input_dir = resolve_project_path(args.input_dir)
    input_pngs, ignored_files = collect_input_files(input_dir)

    output_root = resolve_output_root(args.output_root, asset_id)
    output_dir = (output_root / backend).resolve()
    frames_dir = output_dir / "frames"
    ai_temp_dir = output_dir / "ai_temp"
    ai_input_rgb_dir = output_dir / "ai_input_rgb"
    review_dir = output_dir / "review"
    review_diagnostics_dir = output_dir / "review_diagnostics"
    assert_inside_output_backend(output_dir, output_dir, backend)
    assert_inside_output_backend(frames_dir, output_dir, backend)
    assert_inside_output_backend(ai_temp_dir, output_dir, backend)
    assert_inside_output_backend(ai_input_rgb_dir, output_dir, backend)
    assert_inside_output_backend(review_dir, output_dir, backend)
    assert_inside_output_backend(review_diagnostics_dir, output_dir, backend)

    output_frame_paths = planned_paths(frames_dir, input_pngs)
    report_paths = [output_dir / "upscale_candidate_report.json", output_dir / "upscale_candidate_report.md"]
    review_paths = [
        review_dir / "ai_upscale_contact_sheet.png",
        review_dir / "ai_upscale_review_report.json",
        review_dir / "ai_upscale_review_report.md",
    ]
    diagnostic_paths = [
        review_diagnostics_dir / "alpha_policy_diagnostic.json",
        review_diagnostics_dir / "alpha_policy_diagnostic.md",
    ]
    planned_existing_paths = output_frame_paths + report_paths + diagnostic_paths + (review_paths if backend == "waifu2x" else [])
    if backend == "waifu2x":
        planned_existing_paths.append(ai_temp_dir)
        if alpha_uses_source_mask(args.alpha_policy):
            planned_existing_paths.append(ai_input_rgb_dir)
    existing_outputs = [path for path in planned_existing_paths if path.exists()]

    ai_exe: Path | None = None
    ai_model_path: Path | None = None
    ai_detection_warnings: list[str] = []
    if backend == "waifu2x":
        ai_exe, checked_paths = resolve_ai_exe(args.ai_upscaler_exe)
        ai_detection_warnings.append(f"Checked waifu2x executable paths: {', '.join(checked_paths) if checked_paths else 'none'}")
        ai_model_path = resolve_ai_model_path(args.ai_model_path)
        if ai_exe is None:
            return report_failure_and_exit(
                asset_id,
                backend,
                input_dir,
                output_dir,
                target_width,
                target_height,
                target_size_source,
                input_pngs,
                ignored_files,
                existing_outputs,
                args,
                None,
                ai_model_path,
                target_audit,
                [AI_EXE_MISSING_MESSAGE],
                AI_EXE_MISSING_MESSAGE,
                write_files=not args.dry_run,
            )

    if args.dry_run:
        quality_status, quality_reasons = evaluate_quality(
            backend,
            input_pngs,
            [],
            target_size,
            alpha_preserved=False,
            dry_run=True,
        )
        report = build_report(
            asset_id=asset_id,
            backend=backend,
            input_dir=input_dir,
            output_dir=output_dir,
            target_width=target_width,
            target_height=target_height,
            target_size_source=target_size_source,
            input_pngs=input_pngs,
            ignored_files=ignored_files,
            existing_outputs=existing_outputs,
            created_files=[],
            skipped_files=[],
            dry_run=True,
            args=args,
            ai_exe=ai_exe,
            ai_model_path=ai_model_path,
            ai_temp_sizes=[],
            final_sizes=[],
            ai_stats={},
            target_audit=target_audit,
            quality_status=quality_status,
            quality_reasons=quality_reasons,
            extra_warnings=ai_detection_warnings,
        )
        print(json.dumps(report, indent=2, ensure_ascii=False))
        return 0

    ensure_no_existing_outputs(planned_existing_paths, args.overwrite_candidates)

    created_files: list[Path]
    ai_temp_sizes: list[dict[str, int]] = []
    ai_stats: dict[str, Any] = {}
    ai_command: list[str] | None = None
    ai_stdout: str | None = None
    ai_stderr: str | None = None
    extra_warnings = list(ai_detection_warnings)

    try:
        if backend == "waifu2x":
            assert ai_exe is not None
            prepare_ai_temp(ai_temp_dir, output_dir, args.overwrite_candidates)
            if alpha_uses_source_mask(args.alpha_policy):
                prepare_ai_input_rgb(ai_input_rgb_dir, output_dir, args.overwrite_candidates)
                write_rgb_ai_inputs(input_pngs, ai_input_rgb_dir)
                ai_source_dir = ai_input_rgb_dir
            else:
                ai_source_dir = input_dir
            ai_temp_dir.mkdir(parents=True, exist_ok=True)
            ai_command = build_ai_command(args, ai_exe, ai_source_dir, ai_temp_dir, ai_model_path)
            ai_stdout, ai_stderr = run_waifu2x(ai_command)
            ai_outputs, ai_warnings = collect_ai_output_paths(ai_temp_dir, input_pngs)
            extra_warnings.extend(ai_warnings)
            ai_temp_sizes = count_sizes(ai_outputs)
            created_files, ai_stats = normalize_ai_frames(
                input_pngs,
                ai_outputs,
                frames_dir,
                target_size,
                args.alpha_policy,
                args.alpha_threshold,
            )
            remove_ai_temp_if_needed(ai_temp_dir, output_dir, args.keep_ai_temp)
            remove_ai_input_rgb_if_needed(ai_input_rgb_dir, output_dir, args.keep_ai_temp)
        else:
            created_files, ai_stats = resize_frames(
                input_pngs,
                frames_dir,
                target_size,
                PILLOW_BACKENDS[backend],
                args.alpha_policy,
                args.alpha_threshold,
            )
    except PipelineFailure as failure:
        remove_ai_temp_if_needed(ai_temp_dir, output_dir, args.keep_ai_temp)
        remove_ai_input_rgb_if_needed(ai_input_rgb_dir, output_dir, args.keep_ai_temp)
        return report_failure_and_exit(
            asset_id,
            backend,
            input_dir,
            output_dir,
            target_width,
            target_height,
            target_size_source,
            input_pngs,
            ignored_files,
            existing_outputs,
            args,
            ai_exe,
            ai_model_path,
            target_audit,
            failure.reasons,
            str(failure),
            write_files=True,
        )

    final_sizes = count_sizes(created_files)
    alpha_preserved = final_alpha_preserved(created_files)
    alpha_diagnostic = build_alpha_policy_diagnostic(
        input_pngs,
        created_files,
        backend,
        args.alpha_policy,
        args.alpha_threshold,
        args.low_alpha_threshold,
        args.extra_alpha_fail_ratio,
        args.edge_touch_padding,
    )
    alpha_diagnostic_paths = write_alpha_diagnostic_files(output_dir, alpha_diagnostic)
    extra_warnings.extend(alpha_policy_warning_messages(alpha_diagnostic))
    review_report = build_ai_review_packet(asset_id, input_pngs, created_files, output_dir, args, backend)
    if review_report:
        extra_warnings.append(f"AI review contact sheet created: {review_report['contactSheet']}")

    quality_status, quality_reasons = evaluate_quality(
        backend,
        input_pngs,
        created_files,
        target_size,
        alpha_preserved,
        alpha_diagnostic=alpha_diagnostic,
    )
    report = build_report(
        asset_id=asset_id,
        backend=backend,
        input_dir=input_dir,
        output_dir=output_dir,
        target_width=target_width,
        target_height=target_height,
        target_size_source=target_size_source,
        input_pngs=input_pngs,
        ignored_files=ignored_files,
        existing_outputs=existing_outputs,
        created_files=created_files,
        skipped_files=[],
        dry_run=False,
        args=args,
        ai_exe=ai_exe,
        ai_model_path=ai_model_path,
        ai_temp_sizes=ai_temp_sizes,
        final_sizes=final_sizes,
        ai_stats=ai_stats,
        target_audit=target_audit,
        quality_status=quality_status,
        quality_reasons=quality_reasons,
        extra_warnings=extra_warnings,
        ai_command=ai_command,
        ai_stdout=ai_stdout,
        ai_stderr=ai_stderr,
        alpha_diagnostic=alpha_diagnostic,
        alpha_diagnostic_paths=alpha_diagnostic_paths,
    )

    artifact_paths = list(report_paths) + list(alpha_diagnostic_paths)
    if review_report:
        artifact_paths.extend(review_report["createdFiles"])
    report["createdFiles"].extend(rel(path) for path in artifact_paths)
    write_candidate_reports(output_dir, report)
    print(json.dumps(report, indent=2, ensure_ascii=False))
    if report.get("defaultAiEscalationMessage"):
        print(report["defaultAiEscalationMessage"])
    return 0 if quality_status == "pass" else 2


if __name__ == "__main__":
    sys.exit(main())
