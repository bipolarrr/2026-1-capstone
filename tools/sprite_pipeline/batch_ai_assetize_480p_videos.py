#!/usr/bin/env python3
"""Batch assetize unapproved 480p video sprite sources with waifu2x.

Default operation is candidate-only. Runtime PNGs under Assets/ are reference
canvas/comparison material only; a video is skipped only when the complete
asset approval manifest has a valid human-approved record.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


VIDEO_EXTENSIONS = {".mp4", ".mov"}
IMAGE_EXTENSIONS = {".png"}
DEFAULT_AI_ESCALATION_MESSAGE = (
    "기본 AI 업스케일 결과가 불합격 또는 애매합니다. 여러 다른 방법을 시도해보는 별도 비교 작업으로 넘어가야 합니다."
)
AI_EXE_MISSING_MESSAGE = (
    "GPU AI upscaler executable을 찾지 못했습니다. waifu2x-ncnn-vulkan 설치 또는 "
    "WAIFU2X_NCNN_VULKAN_EXE 설정이 필요합니다."
)
AI_BACKEND_NAME = "waifu2x-ncnn-vulkan"
DEFAULT_APPROVAL_MANIFEST = "SpritePipelineWork/asset_completion_manifest.json"
APPROVAL_APPROVED = "approved"
APPROVAL_UNAPPROVED_STATES = {"pending", "rejected", "needs_review"}
NATIVE_COPY_BACKEND_DIR = "native_copy"
WAIFU2X_BACKEND_DIR = "waifu2x"
NATIVE_CANVAS_SOURCES = {
    "source_video_ai_native_size",
    "source_video_native_size",
    "extracted_frame_ai_native_size",
    "extracted_frame_native_size",
}
REQUIRED_READ_FILES = [
    "AGENTS.md",
    "docs/19_sprite_upscale_pipeline_plan.md",
    "docs/20_sprite_promotion_policy_draft.md",
    "docs/assets.md",
    "docs/grok-imagine-sprite-prompts.md",
    "tools/sprite_pipeline/README.md",
    "tools/sprite_pipeline/extract_frames.py",
    "tools/sprite_pipeline/remove_background.py",
    "tools/sprite_pipeline/upscale_runtime_candidate.py",
    "tools/sprite_pipeline/build_upscale_human_review_packet.py",
    "tools/sprite_pipeline/promote_selected_asset.py",
]


class BatchError(Exception):
    def __init__(
        self,
        code: str,
        message: str,
        reasons: list[str] | None = None,
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.reasons = reasons or [message]
        self.details = details or {}


def project_root() -> Path:
    return Path(__file__).resolve().parents[2]


def resolve_project_path(value: str) -> Path:
    path = Path(value)
    if not path.is_absolute():
        path = project_root() / path
    return path.resolve()


def is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.resolve().relative_to(parent.resolve())
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


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def numeric_key(path: Path) -> tuple[int, str]:
    match = re.search(r"(\d+)(?=\.[^.]+$)", path.name)
    return (int(match.group(1)) if match else 10**9, path.name.lower())


def safe_asset_id(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", value.strip())
    if not cleaned:
        raise BatchError("invalid_asset_id", "asset_id cannot be empty")
    return cleaned


def snake_case(value: str) -> str:
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)
    value = re.sub(r"[^A-Za-z0-9]+", "_", value)
    return value.strip("_").lower()


def short_path_hash(path: Path) -> str:
    key = (rel(path) or str(path)).replace("\\", "/").lower()
    return hashlib.sha1(key.encode("utf-8")).hexdigest()[:10]


def fallback_asset_id(path: Path) -> str:
    return safe_asset_id(f"{snake_case(path.stem) or 'video'}_{short_path_hash(path)}")


def source_signature(path: Path) -> dict[str, Any]:
    stat = path.stat()
    mtime = datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat()
    return {
        "sizeBytes": stat.st_size,
        "mtimeUtc": mtime,
    }


def parse_manifest_target_canvas(record: dict[str, Any] | None) -> dict[str, int] | None:
    if not record:
        return None
    value = record.get("targetCanvas")
    if isinstance(value, list) and len(value) == 2:
        width, height = value
    elif isinstance(value, dict):
        width = value.get("width")
        height = value.get("height")
    else:
        return None
    if isinstance(width, int) and isinstance(height, int) and width > 0 and height > 0:
        return {"width": width, "height": height}
    return None


def path_has_png(path: Path) -> bool:
    if path.is_file():
        return path.suffix.lower() == ".png" and path.stat().st_size > 0
    if not path.exists() or not path.is_dir():
        return False
    return any(child.is_file() and child.suffix.lower() == ".png" and child.stat().st_size > 0 for child in path.iterdir())


def resolve_manifest_path(value: str) -> Path:
    path = resolve_project_path(value)
    work_root = (project_root() / "SpritePipelineWork").resolve()
    if path.resolve() != work_root and not is_relative_to(path.resolve(), work_root):
        raise SystemExit(f"--approval-manifest must stay under SpritePipelineWork/: {path}")
    return path


def load_or_create_approval_manifest(path: Path) -> tuple[dict[str, Any], bool]:
    missing = not path.exists()
    if missing:
        manifest = {"version": 1, "assets": {}}
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        return manifest, True
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SystemExit(f"Approval manifest JSON is invalid: {path}: {exc}") from exc
    if not isinstance(payload, dict):
        raise SystemExit(f"Approval manifest root must be a JSON object: {path}")
    assets = payload.setdefault("assets", {})
    if not isinstance(assets, dict):
        raise SystemExit(f"Approval manifest `assets` must be a JSON object: {path}")
    payload.setdefault("version", 1)
    return payload, False


def parse_iso_datetime(value: str) -> datetime | None:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def source_signature_matches(record_signature: dict[str, Any] | None, current_signature: dict[str, Any]) -> bool:
    if not isinstance(record_signature, dict):
        return False
    if record_signature.get("sizeBytes") != current_signature.get("sizeBytes"):
        return False
    recorded_mtime = record_signature.get("mtimeUtc")
    current_mtime = current_signature.get("mtimeUtc")
    if not isinstance(recorded_mtime, str) or not isinstance(current_mtime, str):
        return False
    recorded = parse_iso_datetime(recorded_mtime)
    current = parse_iso_datetime(current_mtime)
    if recorded is None or current is None:
        return False
    return abs((recorded - current).total_seconds()) <= 1.0


def evaluate_approval_state(
    asset_id: str | None,
    video: Path,
    signature: dict[str, Any],
    manifest: dict[str, Any],
    manifest_was_missing: bool,
) -> dict[str, Any]:
    if not asset_id:
        return {
            "approvalState": "missing_record",
            "approvalStatus": None,
            "approvalRecord": None,
            "approvalReasons": ["asset_id_missing"],
            "approvedForSkip": False,
        }
    assets = manifest.get("assets") if isinstance(manifest.get("assets"), dict) else {}
    if manifest_was_missing:
        return {
            "approvalState": "missing_manifest",
            "approvalStatus": None,
            "approvalRecord": None,
            "approvalReasons": ["approval_manifest_was_missing"],
            "approvedForSkip": False,
        }
    record = assets.get(asset_id)
    if not isinstance(record, dict):
        return {
            "approvalState": "missing_record",
            "approvalStatus": None,
            "approvalRecord": None,
            "approvalReasons": ["approval_record_missing"],
            "approvedForSkip": False,
        }

    status = record.get("approvalStatus")
    if status != APPROVAL_APPROVED:
        state = status if status in {"pending", "rejected"} else "pending"
        reasons = [f"approval_status_{status or 'missing'}"]
        if status == "needs_review":
            reasons.append("needs_review_is_unapproved")
        return {
            "approvalState": state,
            "approvalStatus": status,
            "approvalRecord": record,
            "approvalReasons": reasons,
            "approvedForSkip": False,
        }

    reasons: list[str] = []
    source_path_value = record.get("sourceVideoPath")
    if not isinstance(source_path_value, str) or not source_path_value.strip():
        reasons.append("approved_record_missing_sourceVideoPath")
    else:
        source_path = resolve_project_path(source_path_value)
        if not source_path.exists():
            reasons.append("approved_source_video_missing")
        elif source_path.resolve() != video.resolve():
            reasons.append("approved_source_video_path_differs_from_current_video")

    if not source_signature_matches(record.get("sourceSignature"), signature):
        reasons.append("approved_source_signature_stale_or_missing")

    candidate_paths = []
    for key in ("approvedAssetPath", "approvedCandidatePath"):
        value = record.get(key)
        if isinstance(value, str) and value.strip():
            candidate_paths.append(resolve_project_path(value))
    if not candidate_paths:
        reasons.append("approved_record_missing_approved_asset_or_candidate_path")
    elif not any(path_has_png(path) for path in candidate_paths):
        reasons.append("approved_asset_or_candidate_path_missing_png")

    if reasons:
        return {
            "approvalState": "approved_but_stale_or_missing",
            "approvalStatus": status,
            "approvalRecord": record,
            "approvalReasons": reasons,
            "approvedForSkip": False,
        }

    return {
        "approvalState": "approved",
        "approvalStatus": status,
        "approvalRecord": record,
        "approvalReasons": ["approved_record_valid"],
        "approvedForSkip": True,
    }


def assert_work_path(path: Path, work_root: Path) -> None:
    resolved = path.resolve()
    root = work_root.resolve()
    if resolved != root and not is_relative_to(resolved, root):
        raise BatchError("unsafe_work_path", f"Work output escaped work root: {resolved}")
    assets = (project_root() / "Assets").resolve()
    if is_relative_to(resolved, assets):
        raise BatchError("unsafe_assets_write", f"Refusing work output under Assets/: {resolved}")


def assert_batch_asset_path(path: Path) -> None:
    resolved = path.resolve()
    allowed_roots = [
        (project_root() / "Assets" / "Mobs" / "Sprites").resolve(),
        (project_root() / "Assets" / "Player" / "Sprites").resolve(),
    ]
    if not any(resolved == root or is_relative_to(resolved, root) for root in allowed_roots):
        raise BatchError("unsafe_asset_path", f"Asset output path is not an allowed sprite folder: {resolved}")


def safe_prepare_dir(path: Path, work_root: Path, overwrite_work: bool) -> None:
    assert_work_path(path, work_root)
    if path.exists() and any(path.iterdir()):
        if not overwrite_work:
            raise BatchError(
                "work_output_exists",
                f"Work output already exists; pass --overwrite-work to replace this batch output: {rel(path)}",
            )
        if not is_relative_to(path, work_root) or "batch_480p_assetization" not in path.parts:
            raise BatchError("unsafe_work_cleanup", f"Refusing to clear non-batch work path: {path}")
        shutil.rmtree(path)
    path.mkdir(parents=True, exist_ok=True)


def find_waifu2x_exe(cli_value: str | None) -> tuple[Path | None, list[str]]:
    candidates: list[Path] = []
    if cli_value:
        candidates.append(resolve_project_path(cli_value))
    env_value = os.environ.get("WAIFU2X_NCNN_VULKAN_EXE")
    if env_value:
        candidates.append(resolve_project_path(env_value))
    candidates.extend(
        [
            project_root() / "tools" / "external" / "waifu2x-ncnn-vulkan" / "waifu2x-ncnn-vulkan.exe",
            project_root() / "tools" / "external" / "waifu2x-ncnn-vulkan" / "waifu2x-ncnn-vulkan",
        ]
    )

    checked: list[str] = []
    seen: set[Path] = set()
    for candidate in candidates:
        resolved = candidate.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        checked.append(rel(resolved) or str(resolved))
        if resolved.exists() and resolved.is_file():
            return resolved, checked
    return None, checked


def parse_fraction(value: str | None) -> float | None:
    if not value:
        return None
    if "/" not in value:
        try:
            return float(value)
        except ValueError:
            return None
    left, right = value.split("/", 1)
    try:
        denominator = float(right)
        if denominator == 0:
            return None
        return float(left) / denominator
    except ValueError:
        return None


def ffprobe_metadata(path: Path) -> tuple[dict[str, Any] | None, str]:
    exe = shutil.which("ffprobe")
    if not exe:
        return None, "ffprobe_not_found"
    command = [
        exe,
        "-v",
        "error",
        "-select_streams",
        "v:0",
        "-show_entries",
        "stream=width,height,avg_frame_rate,r_frame_rate,nb_frames,duration",
        "-show_entries",
        "format=duration",
        "-of",
        "json",
        str(path),
    ]
    result = subprocess.run(command, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        return None, f"ffprobe_failed:{result.stderr.strip()[:200]}"
    try:
        payload = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        return None, f"ffprobe_json_failed:{exc}"
    streams = payload.get("streams") or []
    if not streams:
        return None, "ffprobe_no_video_stream"
    stream = streams[0]
    width = int(stream.get("width") or 0)
    height = int(stream.get("height") or 0)
    fps = parse_fraction(stream.get("avg_frame_rate")) or parse_fraction(stream.get("r_frame_rate")) or 0.0
    duration = stream.get("duration") or (payload.get("format") or {}).get("duration") or 0
    try:
        duration_float = float(duration or 0)
    except ValueError:
        duration_float = 0.0
    nb_frames = stream.get("nb_frames")
    try:
        frame_count = int(nb_frames) if nb_frames not in (None, "N/A") else 0
    except ValueError:
        frame_count = 0
    if frame_count == 0 and duration_float and fps:
        frame_count = round(duration_float * fps)
    return (
        {
            "width": width,
            "height": height,
            "duration": duration_float,
            "fps": fps,
            "frameCount": frame_count,
            "metadataTool": "ffprobe",
        },
        "ok",
    )


def opencv_metadata(path: Path) -> tuple[dict[str, Any] | None, str]:
    try:
        import cv2  # type: ignore
    except ImportError as exc:
        return None, f"opencv_not_available:{exc}"
    cap = cv2.VideoCapture(str(path))
    if not cap.isOpened():
        return None, "opencv_open_failed"
    fps = float(cap.get(cv2.CAP_PROP_FPS) or 0)
    frame_count = int(cap.get(cv2.CAP_PROP_FRAME_COUNT) or 0)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH) or 0)
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT) or 0)
    duration = frame_count / fps if fps else 0.0
    cap.release()
    if width < 1 or height < 1:
        return None, "opencv_no_dimensions"
    return (
        {
            "width": width,
            "height": height,
            "duration": duration,
            "fps": fps,
            "frameCount": frame_count,
            "metadataTool": "opencv",
        },
        "ok",
    )


def video_metadata(path: Path) -> dict[str, Any]:
    metadata, reason = ffprobe_metadata(path)
    attempts = [{"tool": "ffprobe", "result": reason}]
    if metadata is None:
        metadata, reason = opencv_metadata(path)
        attempts.append({"tool": "opencv", "result": reason})
    if metadata is None:
        return {
            "width": None,
            "height": None,
            "duration": None,
            "fps": None,
            "frameCount": None,
            "metadataTool": None,
            "metadataAttempts": attempts,
            "metadataStatus": "failed",
        }
    metadata["metadataAttempts"] = attempts
    metadata["metadataStatus"] = "ok"
    return metadata


def classify_480p(path: Path, metadata: dict[str, Any]) -> dict[str, Any]:
    width = metadata.get("width")
    height = metadata.get("height")
    reasons: list[str] = []
    exact = bool(width == 480 or height == 480)
    probable = False
    if exact:
        probable = True
        reasons.append("exact_480_dimension")
    if re.search(r"(^|[^0-9])480p([^0-9]|$)", path.as_posix(), flags=re.IGNORECASE):
        probable = True
        reasons.append("path_mentions_480p")
    if width is None or height is None:
        reasons.append("ambiguous_resolution")
    if not reasons:
        reasons.append("resolution_metadata_available_but_not_480p")
    return {
        "isExact480p": exact,
        "isProbable480p": probable,
        "classificationReason": "; ".join(reasons),
    }


def classify_video_source(path: Path, metadata: dict[str, Any], include_720p_960_class: bool) -> dict[str, Any]:
    classification = classify_480p(path, metadata)
    width = metadata.get("width")
    height = metadata.get("height")
    is_720p_960_class = bool(include_720p_960_class and width == 960 and height == 960)
    if is_720p_960_class:
        source_class = "720p_960_class"
        assetization_mode = "native_copy"
        reason = f"{classification['classificationReason']}; include_720p_960_class:960x960_source"
    elif classification["isExact480p"]:
        source_class = "480p"
        assetization_mode = "ai_upscale"
        reason = classification["classificationReason"]
    elif classification["isProbable480p"]:
        source_class = "probable_480p"
        assetization_mode = "ai_upscale"
        reason = classification["classificationReason"]
    else:
        source_class = "not_eligible"
        assetization_mode = None
        reason = classification["classificationReason"]
    return {
        **classification,
        "is720p960Class": is_720p_960_class,
        "sourceClass": source_class,
        "assetizationMode": assetization_mode,
        "classificationReason": reason,
    }


def scan_videos(scan_root: Path, work_root: Path) -> list[Path]:
    roots = [scan_root.resolve(), work_root.resolve()]
    seen: set[Path] = set()
    videos: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if path.is_file() and path.suffix.lower() in VIDEO_EXTENSIONS:
                resolved = path.resolve()
                if resolved not in seen:
                    seen.add(resolved)
                    videos.append(resolved)
    assets_root = (project_root() / "Assets").resolve()
    return sorted(videos, key=lambda p: (0 if is_relative_to(p, assets_root) else 1, rel(p) or str(p)))


def known_action_name(value: str) -> str:
    compact = re.sub(r"[^A-Za-z0-9]", "", value).lower()
    mapping = {
        "idle": "Idle",
        "lowhp": "LowHp",
        "jump": "Jump",
        "jumpbelow": "JumpBelow",
        "defense": "Defense",
        "smallhit": "SmallHit",
        "stronghit": "StrongHit",
        "debuff": "Debuff",
        "die": "Die",
        "death": "Dead",
        "dead": "Dead",
        "diceroll": "DiceRoll",
        "attack": "Attack",
        "hit": "Hit",
        "projectile": "Projectile",
        "weapon": "Weapon",
    }
    return mapping.get(compact, value)


def infer_from_assets_path(path: Path) -> dict[str, Any]:
    root = project_root()
    mobs_root = root / "Assets" / "Mobs" / "Sprites"
    player_root = root / "Assets" / "Player" / "Sprites"
    resolved = path.resolve()
    warnings: list[str] = []
    if is_relative_to(resolved, mobs_root.resolve()):
        parts = resolved.relative_to(mobs_root).parts
        if len(parts) < 3:
            raise BatchError("cannot_infer_asset_id_or_target", "Mob video must be under Assets/Mobs/Sprites/<Mob>/<Action>/")
        actor = parts[0]
        action = known_action_name(parts[1])
        target = mobs_root / actor / parts[1]
        category = "mob"
    elif is_relative_to(resolved, player_root.resolve()):
        parts = resolved.relative_to(player_root).parts
        if len(parts) < 2:
            raise BatchError("cannot_infer_asset_id_or_target", "Player video must be under Assets/Player/Sprites/<Action>/")
        actor = "Player"
        action = known_action_name(parts[0])
        target = player_root / parts[0]
        category = "player"
    else:
        raise BatchError("cannot_infer_asset_id_or_target", "Video is not under a known Assets sprite root.")

    file_lower = path.stem.lower()
    actor_lower = actor.lower()
    action_lower = action.lower()
    if actor_lower not in file_lower or action_lower not in file_lower:
        warnings.append(
            f"Filename `{path.name}` does not clearly match inferred actor/action `{actor}`/`{action}`; path inference was used."
        )
    asset_id = safe_asset_id(f"{snake_case(actor)}_{snake_case(action)}")
    return {
        "assetId": asset_id,
        "category": category,
        "actor": actor,
        "action": action,
        "targetAssetDir": target,
        "inferenceWarnings": warnings,
    }


def infer_from_work_path(path: Path) -> dict[str, Any]:
    work_root = project_root() / "SpritePipelineWork"
    parts = path.resolve().relative_to(work_root.resolve()).parts
    if not parts:
        raise BatchError("cannot_infer_asset_id_or_target", "Work video is not under an asset id folder.")
    asset_id = safe_asset_id(parts[0])
    tokens = asset_id.split("_")
    if len(tokens) < 2:
        raise BatchError("cannot_infer_asset_id_or_target", f"Cannot parse work asset id: {asset_id}")
    actor_key = tokens[0]
    action = known_action_name("_".join(tokens[1:]))
    if actor_key == "player":
        actor = "Player"
        category = "player"
        target = project_root() / "Assets" / "Player" / "Sprites" / action
    else:
        actor = actor_key[:1].upper() + actor_key[1:]
        category = "mob"
        target = project_root() / "Assets" / "Mobs" / "Sprites" / actor / action
    return {
        "assetId": asset_id,
        "category": category,
        "actor": actor,
        "action": action,
        "targetAssetDir": target,
        "inferenceWarnings": ["Inferred target from SpritePipelineWork asset id."],
    }


def infer_asset(path: Path, work_root: Path) -> dict[str, Any]:
    assets_root = project_root() / "Assets"
    if is_relative_to(path, assets_root):
        return infer_from_assets_path(path)
    if is_relative_to(path, work_root):
        return infer_from_work_path(path)
    raise BatchError("cannot_infer_asset_id_or_target", "Video is outside Assets/ and SpritePipelineWork/.")


def read_png_size(path: Path) -> tuple[int, int]:
    with Image.open(path) as img:
        return img.size


def png_audit(folder: Path) -> dict[str, Any]:
    if not folder.exists() or not folder.is_dir():
        return {
            "folderExists": False,
            "pngCount": 0,
            "validPngCount": 0,
            "invalidPngCount": 0,
            "emptyPngCount": 0,
            "sizeDistribution": [],
            "dominantSize": None,
            "dominantRatio": 0.0,
            "validPngs": [],
            "invalidPngs": [],
        }
    pngs = sorted([p for p in folder.iterdir() if p.is_file() and p.suffix.lower() == ".png"], key=numeric_key)
    counts: Counter[tuple[int, int]] = Counter()
    valid: list[Path] = []
    invalid: list[dict[str, str]] = []
    empty = 0
    for path in pngs:
        if path.stat().st_size == 0:
            empty += 1
            invalid.append({"path": rel(path) or str(path), "reason": "empty_file"})
            continue
        try:
            size = read_png_size(path)
        except Exception as exc:  # noqa: BLE001
            invalid.append({"path": rel(path) or str(path), "reason": f"unreadable:{exc}"})
            continue
        counts[size] += 1
        valid.append(path)
    distribution = [
        {"width": size[0], "height": size[1], "count": count}
        for size, count in sorted(counts.items(), key=lambda item: (-item[1], item[0]))
    ]
    dominant = distribution[0] if distribution else None
    ratio = (dominant["count"] / len(valid)) if dominant and valid else 0.0
    return {
        "folderExists": True,
        "pngCount": len(pngs),
        "validPngCount": len(valid),
        "invalidPngCount": len(invalid),
        "emptyPngCount": empty,
        "sizeDistribution": distribution,
        "dominantSize": dominant,
        "dominantRatio": round(ratio, 4),
        "validPngSamples": [rel(path) for path in valid[:12]],
        "validPngSampleTruncated": len(valid) > 12,
        "invalidPngs": invalid,
    }


def child_sprite_folder_audits(target: Path) -> list[dict[str, Any]]:
    if not target.exists() or not target.is_dir():
        return []
    audits = []
    for child in sorted([p for p in target.iterdir() if p.is_dir()], key=lambda p: p.name.lower()):
        audit = png_audit(child)
        if audit["validPngCount"] > 0 or audit["invalidPngCount"] > 0:
            audits.append(
                {
                    "path": rel(child),
                    "name": child.name,
                    "validPngCount": audit["validPngCount"],
                    "invalidPngCount": audit["invalidPngCount"],
                    "dominantSize": audit["dominantSize"],
                    "dominantRatio": audit["dominantRatio"],
                    "sizeDistribution": audit["sizeDistribution"],
                }
            )
    return audits


def classify_asset_folder(target: Path) -> dict[str, Any]:
    direct = png_audit(target)
    children = child_sprite_folder_audits(target)
    reasons: list[str] = []
    status = "missing"
    if direct["validPngCount"] > 0:
        if direct["invalidPngCount"] > 0:
            status = "partial_or_ambiguous"
            reasons.append("Target folder contains unreadable or empty PNG files; 사람 판단 필요.")
        elif direct["validPngCount"] < 3:
            status = "partial_or_ambiguous"
            reasons.append("Target folder contains very few PNG frames; 사람 판단 필요.")
        elif len(direct["sizeDistribution"]) > 1 and direct["dominantRatio"] < 0.8:
            status = "partial_or_ambiguous"
            reasons.append("Target folder PNG sizes are heavily mixed; 사람 판단 필요.")
        else:
            status = "existing"
            reasons.append("Target folder already contains readable PNG sprite frames.")
    elif direct["invalidPngCount"] > 0:
        status = "partial_or_ambiguous"
        reasons.append("Target folder contains PNG files, but none were readable; 사람 판단 필요.")
    elif children:
        valid_children = [item for item in children if item["validPngCount"] > 0]
        invalid_children = [item for item in children if item["invalidPngCount"] > 0]
        if invalid_children:
            status = "partial_or_ambiguous"
            reasons.append("Nested sprite folder contains invalid PNG files; 사람 판단 필요.")
        elif len(valid_children) == 1:
            status = "existing"
            reasons.append("A nested sprite asset folder already contains readable PNG frames.")
        else:
            status = "partial_or_ambiguous"
            reasons.append("Multiple nested sprite frame folders exist under the target folder; 사람 판단 필요.")
    elif target.exists():
        status = "missing"
        reasons.append("Target folder exists but has no direct or nested readable PNG sprite frames.")
    else:
        status = "missing"
        reasons.append("Target folder does not exist.")

    return {
        "assetStatus": status,
        "assetStatusReasons": reasons,
        "directPngAudit": direct,
        "childSpriteFolders": children,
    }


def actor_root(category: str, actor: str) -> Path:
    if category == "player":
        return project_root() / "Assets" / "Player" / "Sprites"
    return project_root() / "Assets" / "Mobs" / "Sprites" / actor


def valid_sprite_folders_for_actor(category: str, actor: str, exclude: Path | None = None) -> list[dict[str, Any]]:
    root = actor_root(category, actor)
    if not root.exists():
        return []
    folders = []
    exclude_resolved = exclude.resolve() if exclude else None
    for folder in sorted([p for p in root.rglob("*") if p.is_dir()], key=lambda p: rel(p) or str(p)):
        if exclude_resolved and folder.resolve() == exclude_resolved:
            continue
        audit = png_audit(folder)
        if audit["validPngCount"] < 3 or audit["invalidPngCount"] > 0:
            continue
        if not audit["dominantSize"]:
            continue
        if len(audit["sizeDistribution"]) > 1 and audit["dominantRatio"] < 0.8:
            continue
        folders.append(
            {
                "path": rel(folder),
                "name": folder.name,
                "validPngCount": audit["validPngCount"],
                "dominantSize": audit["dominantSize"],
                "dominantRatio": audit["dominantRatio"],
            }
        )
    return folders


def action_matches_folder(folder_info: dict[str, Any], action: str) -> bool:
    action_key = snake_case(action)
    name_key = snake_case(folder_info["name"])
    path_key = snake_case(folder_info["path"] or "")
    return action_key == name_key or action_key in name_key.split("_") or action_key in path_key.split("_")


def most_common_folder_canvas(folders: list[dict[str, Any]]) -> tuple[dict[str, int] | None, float, list[dict[str, Any]]]:
    counts: Counter[tuple[int, int]] = Counter()
    for folder in folders:
        size = folder["dominantSize"]
        counts[(size["width"], size["height"])] += 1
    if not counts:
        return None, 0.0, []
    (width, height), count = counts.most_common(1)[0]
    ratio = count / len(folders)
    examples = [folder for folder in folders if folder["dominantSize"]["width"] == width and folder["dominantSize"]["height"] == height]
    return {"width": width, "height": height, "count": count}, ratio, examples


def target_folder_canvas(target: Path) -> dict[str, Any] | None:
    direct = png_audit(target)
    if direct.get("dominantSize"):
        size = direct["dominantSize"]
        return {
            "width": size["width"],
            "height": size["height"],
            "source": "target_asset_folder_existing_png",
            "reason": f"Target folder has {direct['validPngCount']} readable direct PNG frame(s).",
            "referenceFolders": [{"path": rel(target), "dominantSize": size, "validPngCount": direct["validPngCount"]}],
        }
    children = child_sprite_folder_audits(target)
    valid_children = [item for item in children if item["validPngCount"] > 0 and item.get("dominantSize")]
    if not valid_children:
        return None
    best = sorted(valid_children, key=lambda item: (-item["validPngCount"], item["path"] or ""))[0]
    size = best["dominantSize"]
    return {
        "width": size["width"],
        "height": size["height"],
        "source": "target_asset_folder_existing_nested_png",
        "reason": f"Nested runtime/reference folder `{best['path']}` has readable PNG frame(s).",
        "referenceFolders": [best],
    }


def manifest_target_canvas(record: dict[str, Any] | None) -> dict[str, Any] | None:
    size = parse_manifest_target_canvas(record)
    if size is None:
        return None
    return {
        "width": size["width"],
        "height": size["height"],
        "source": "approval_manifest_target_canvas",
        "reason": "Approval manifest record contains a targetCanvas value.",
        "referenceFolders": [],
    }


def source_ai_native_canvas(metadata: dict[str, Any], native_canvas_scale: int) -> dict[str, Any] | None:
    width = metadata.get("width")
    height = metadata.get("height")
    if isinstance(width, int) and isinstance(height, int) and width > 0 and height > 0:
        source = "source_video_native_size" if native_canvas_scale == 1 else "source_video_ai_native_size"
        reason = (
            "No reliable target canvas was found; candidate will keep the source video native output size."
            if native_canvas_scale == 1
            else "No reliable target canvas was found; candidate will keep the default AI native output size."
        )
        return {
            "status": "ambiguous",
            "width": width * native_canvas_scale,
            "height": height * native_canvas_scale,
            "source": source,
            "confidence": "low",
            "reason": reason,
            "referenceFolders": [],
            "actorCanvasAudit": [],
        }
    return None


def allow_native_canvas_for_missing_if_requested(
    canvas: dict[str, Any],
    runtime_asset_state: str | None,
    target: Path | None,
    allow_native_canvas_for_missing: bool,
) -> dict[str, Any]:
    if (
        not allow_native_canvas_for_missing
        or runtime_asset_state != "missing"
        or target is None
        or canvas.get("status") != "ambiguous"
        or canvas.get("source") not in NATIVE_CANVAS_SOURCES
    ):
        return canvas
    allowed = dict(canvas)
    allowed["status"] = "resolved"
    allowed["confidence"] = "source-derived"
    allowed["nativeCanvasAllowedForMissing"] = True
    allowed["reason"] = f"{canvas.get('reason') or 'Source-derived canvas selected.'} Allowed by --allow-native-canvas-for-missing."
    return allowed


def resolve_target_canvas(
    category: str | None,
    actor: str | None,
    action: str | None,
    target: Path | None,
    metadata: dict[str, Any],
    native_canvas_scale: int,
    approval_record: dict[str, Any] | None = None,
) -> dict[str, Any]:
    if target is not None:
        target_canvas = target_folder_canvas(target)
        if target_canvas:
            return {
                "status": "resolved",
                "width": target_canvas["width"],
                "height": target_canvas["height"],
                "source": target_canvas["source"],
                "confidence": "high",
                "reason": target_canvas["reason"],
                "referenceFolders": target_canvas["referenceFolders"],
                "actorCanvasAudit": [],
            }

    folders = valid_sprite_folders_for_actor(category, actor, exclude=target) if category and actor else []
    action_folders = [folder for folder in folders if action and action_matches_folder(folder, action)]
    action_canvas, action_ratio, action_examples = most_common_folder_canvas(action_folders)
    if action_canvas and action_ratio >= 0.8:
        return {
            "status": "resolved",
            "width": action_canvas["width"],
            "height": action_canvas["height"],
            "source": "same_actor_same_action_reference",
            "confidence": "high",
            "reason": f"{len(action_examples)}/{len(action_folders)} same-action reference folders use this canvas.",
            "referenceFolders": action_examples,
            "actorCanvasAudit": folders,
        }

    actor_canvas, actor_ratio, actor_examples = most_common_folder_canvas(folders)
    if actor_canvas and actor_ratio >= 0.8:
        return {
            "status": "resolved",
            "width": actor_canvas["width"],
            "height": actor_canvas["height"],
            "source": "same_actor_default_reference",
            "confidence": "medium",
            "reason": f"{len(actor_examples)}/{len(folders)} actor sprite folders use this canvas.",
            "referenceFolders": actor_examples,
            "actorCanvasAudit": folders,
        }

    manifest_canvas = manifest_target_canvas(approval_record)
    if manifest_canvas:
        return {
            "status": "resolved",
            "width": manifest_canvas["width"],
            "height": manifest_canvas["height"],
            "source": manifest_canvas["source"],
            "confidence": "low",
            "reason": manifest_canvas["reason"],
            "referenceFolders": manifest_canvas["referenceFolders"],
            "actorCanvasAudit": folders,
        }

    native_canvas = source_ai_native_canvas(metadata, native_canvas_scale)
    if native_canvas:
        native_canvas["actorCanvasAudit"] = folders
        return native_canvas

    return {
        "status": "unresolved",
        "width": None,
        "height": None,
        "source": None,
        "confidence": "none",
        "reason": "No usable target, actor/action, manifest, or source-size canvas was available.",
        "referenceFolders": [],
        "actorCanvasAudit": folders,
    }


def candidate_canvas_from_extraction(
    target_canvas: dict[str, Any],
    extraction: dict[str, Any],
    native_canvas_scale: int,
) -> dict[str, Any]:
    width = target_canvas.get("width")
    height = target_canvas.get("height")
    if isinstance(width, int) and isinstance(height, int) and width > 0 and height > 0:
        return target_canvas
    source_width = extraction.get("width")
    source_height = extraction.get("height")
    if isinstance(source_width, int) and isinstance(source_height, int) and source_width > 0 and source_height > 0:
        source = "extracted_frame_native_size" if native_canvas_scale == 1 else "extracted_frame_ai_native_size"
        reason = (
            "Target canvas was unresolved before extraction; using extracted frame native size for candidate output."
            if native_canvas_scale == 1
            else "Target canvas was unresolved before extraction; using extracted frame size times AI scale for candidate-only output."
        )
        return {
            "status": "ambiguous",
            "width": source_width * native_canvas_scale,
            "height": source_height * native_canvas_scale,
            "source": source,
            "confidence": "low",
            "reason": reason,
            "referenceFolders": target_canvas.get("referenceFolders", []),
            "actorCanvasAudit": target_canvas.get("actorCanvasAudit", []),
        }
    raise BatchError("target_canvas_unresolved", "Target canvas could not be inferred even after extracting source frames.")


def update_probable_from_canvas(entry: dict[str, Any]) -> None:
    meta = entry["metadata"]
    canvas = entry.get("targetCanvas") or {}
    width = meta.get("width")
    height = meta.get("height")
    target_width = canvas.get("width")
    target_height = canvas.get("height")
    if entry["isExact480p"] or entry["isProbable480p"] or entry.get("is720p960Class"):
        return
    if not all(isinstance(value, int) and value > 0 for value in [width, height, target_width, target_height]):
        return
    if (width < target_width or height < target_height) and max(width, height) <= max(target_width, target_height):
        entry["isProbable480p"] = True
        entry["sourceClass"] = "probable_480p"
        entry["assetizationMode"] = "ai_upscale"
        entry["classificationReason"] += (
            f"; source_canvas_smaller_than_reference_target:{width}x{height}->{target_width}x{target_height}"
        )


def extract_video_frames(video: Path, output_dir: Path) -> dict[str, Any]:
    try:
        import cv2  # type: ignore
    except ImportError as exc:
        raise BatchError("frame_extraction_failed", f"OpenCV is required for frame extraction: {exc}") from exc
    cap = cv2.VideoCapture(str(video))
    if not cap.isOpened():
        raise BatchError("frame_extraction_failed", f"Could not open video: {rel(video)}")
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
        Image.fromarray(rgb).save(output_dir / f"{index:04d}.png")
        index += 1
    cap.release()
    if index == 0:
        raise BatchError("frame_extraction_failed", f"No frames extracted from {rel(video)}")
    return {
        "sourceType": "video",
        "source": rel(video),
        "rawFrames": rel(output_dir),
        "sourceFrameCountMetadata": frame_count_meta,
        "outputFrameCount": index,
        "fps": fps,
        "width": width,
        "height": height,
    }


def transparent_frame_has_alpha(path: Path) -> bool:
    with Image.open(path) as img:
        rgba = img.convert("RGBA")
        return "A" in rgba.getbands() and rgba.getchannel("A").getextrema()[0] < 255


def remove_background_frames(input_dir: Path, output_dir: Path) -> dict[str, Any]:
    try:
        from rembg import new_session, remove  # type: ignore
    except ImportError as exc:
        raise BatchError("background_removal_failed", f"rembg is required for default background removal: {exc}") from exc
    frames = sorted([p for p in input_dir.iterdir() if p.is_file() and p.suffix.lower() == ".png"], key=numeric_key)
    if not frames:
        raise BatchError("background_removal_failed", f"No raw frames found in {rel(input_dir)}")
    session = new_session()
    created: list[Path] = []
    for src in frames:
        with Image.open(src) as img:
            out = remove(img.convert("RGBA"), session=session)
            out.convert("RGBA").save(output_dir / src.name)
        created.append(output_dir / src.name)
    alpha_failures = [path.name for path in created if not transparent_frame_has_alpha(path)]
    if alpha_failures:
        raise BatchError(
            "background_removal_failed",
            f"Background removal did not create transparent alpha in frames: {', '.join(alpha_failures[:12])}",
        )
    return {
        "method": "rembg",
        "inputDir": rel(input_dir),
        "transparentFrames": rel(output_dir),
        "frameCount": len(created),
        "alphaChannelPresent": True,
    }


def upscale_report_record(batch_root: Path, report: dict[str, Any]) -> dict[str, Any]:
    alpha_summary = report.get("alphaDiagnosticSummary")
    backend_dir = report.get("candidateBackendDir") or report.get("backend") or WAIFU2X_BACKEND_DIR
    return {
        "qualityGateStatus": report.get("qualityGateStatus"),
        "qualityGateReasons": report.get("qualityGateReasons") or [],
        "json": rel(batch_root / "upscaled_runtime_candidate" / backend_dir / "upscale_candidate_report.json"),
        "md": rel(batch_root / "upscaled_runtime_candidate" / backend_dir / "upscale_candidate_report.md"),
        "alphaPolicy": report.get("alphaPolicy"),
        "alphaDiagnosticPaths": report.get("alphaDiagnosticPaths"),
        "alphaDiagnosticSummary": alpha_summary,
        "extraAlphaPixelCount": (alpha_summary or {}).get("extraAlphaPixelCount"),
        "extraAlphaRatio": (alpha_summary or {}).get("extraAlphaRatio"),
        "lowAlphaPixelCount": (alpha_summary or {}).get("lowAlphaPixelCount"),
        "lowAlphaRatio": (alpha_summary or {}).get("lowAlphaRatio"),
        "edgeTouchFrameCount": (alpha_summary or {}).get("edgeTouchFrameCount"),
        "fullFrameBboxFrameCount": (alpha_summary or {}).get("fullFrameBboxFrameCount"),
        "cropRiskFrameCount": (alpha_summary or {}).get("cropRiskFrameCount"),
    }


def alpha_result_fields_from_upscale(batch_root: Path, report: dict[str, Any]) -> dict[str, Any]:
    record = upscale_report_record(batch_root, report)
    return {
        "upscaleReport": record,
        "alphaPolicy": record.get("alphaPolicy"),
        "alphaDiagnosticPaths": record.get("alphaDiagnosticPaths"),
        "alphaDiagnosticSummary": record.get("alphaDiagnosticSummary"),
    }


def run_upscale_candidate(
    asset_id: str,
    transparent_dir: Path,
    batch_root: Path,
    target_canvas: dict[str, Any],
    args: argparse.Namespace,
    ai_exe: Path,
) -> dict[str, Any]:
    output_root = batch_root / "upscaled_runtime_candidate"
    command = [
        sys.executable,
        str(project_root() / "tools" / "sprite_pipeline" / "upscale_runtime_candidate.py"),
        "--asset-id",
        asset_id,
        "--input-dir",
        str(transparent_dir),
        "--output-root",
        str(output_root),
        "--target-width",
        str(target_canvas["width"]),
        "--target-height",
        str(target_canvas["height"]),
        "--ai-upscaler-exe",
        str(ai_exe),
        "--ai-scale",
        str(args.ai_scale),
        "--ai-noise",
        str(args.ai_noise),
    ]
    if args.overwrite_work:
        command.append("--overwrite-candidates")
    if args.alpha_policy:
        command.extend(["--alpha-policy", args.alpha_policy])
    if args.ai_model_path:
        command.extend(["--ai-model-path", str(resolve_project_path(args.ai_model_path))])
    if args.ai_gpu_id is not None:
        command.extend(["--ai-gpu-id", str(args.ai_gpu_id)])
    if args.ai_tile_size is not None:
        command.extend(["--ai-tile-size", str(args.ai_tile_size)])
    if args.keep_ai_temp:
        command.append("--keep-ai-temp")

    result = subprocess.run(command, cwd=project_root(), capture_output=True, text=True, check=False)
    report = parse_json_prefix(result.stdout)
    if report is None:
        raise BatchError(
            "ai_upscale_failed",
            f"Could not parse upscale report JSON. Exit code {result.returncode}. stderr: {result.stderr[:500]}",
        )
    report["batchCommand"] = command
    report["batchCommandExitCode"] = result.returncode
    report["batchCommandStderr"] = result.stderr
    if result.returncode != 0 or report.get("qualityGateStatus") != "pass":
        if can_accept_source_derived_edge_only_alpha_failure(report, target_canvas, args):
            original_reasons = list(report.get("qualityGateReasons") or [])
            report["batchAcceptedAlphaWarnings"] = original_reasons
            report["qualityGateStatus"] = "pass"
            report["qualityGateReasons"] = [
                "Accepted source-derived missing canvas edge-touch crop risk under --allow-native-canvas-for-missing.",
                *original_reasons,
            ]
            report["defaultAiEscalationMessage"] = None
            return report
        reasons = list(report.get("qualityGateReasons") or [])
        if not reasons:
            reasons.append(f"waifu2x candidate command exited with {result.returncode}.")
        raise BatchError(
            "ai_upscale_failed",
            "waifu2x AI upscale failed or was ambiguous.",
            reasons,
            alpha_result_fields_from_upscale(batch_root, report),
        )
    return report


def can_accept_source_derived_edge_only_alpha_failure(
    report: dict[str, Any],
    target_canvas: dict[str, Any],
    args: argparse.Namespace,
) -> bool:
    if not args.allow_native_canvas_for_missing or not target_canvas.get("nativeCanvasAllowedForMissing"):
        return False
    reasons = list(report.get("qualityGateReasons") or [])
    if not reasons:
        return False
    allowed_prefixes = (
        "Full-frame alpha bounding boxes detected:",
        "Severe edge-touch alpha detected",
        "Crop-risk alpha bounding boxes detected",
    )
    if any(not any(reason.startswith(prefix) for prefix in allowed_prefixes) for reason in reasons):
        return False
    summary = report.get("alphaDiagnosticSummary") or {}
    if summary.get("extraAlphaPixelCount", 0) != 0:
        return False
    if summary.get("lowAlphaPixelCount", 0) != 0:
        return False
    return True


def write_native_copy_markdown(path: Path, report: dict[str, Any]) -> None:
    lines = [
        "# Native Copy Candidate Report",
        "",
        f"- Asset ID: `{report['assetId']}`",
        f"- Backend: `{report['backend']}`",
        f"- Input directory: `{report['inputDir']}`",
        f"- Output directory: `{report['outputDir']}`",
        f"- Frames directory: `{report['framesDir']}`",
        f"- Target size: `{report['targetWidth']}x{report['targetHeight']}`",
        f"- Input frame count: `{report['inputFrameCount']}`",
        f"- Output frame count: `{report['outputFrameCount']}`",
        f"- Quality gate status: `{report['qualityGateStatus']}`",
        "",
        "## Quality Gate Reasons",
        "",
    ]
    lines.extend(f"- {reason}" for reason in report["qualityGateReasons"])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def copy_native_candidate_frames(
    asset_id: str,
    transparent_dir: Path,
    batch_root: Path,
    target_canvas: dict[str, Any],
    args: argparse.Namespace,
) -> dict[str, Any]:
    output_dir = batch_root / "upscaled_runtime_candidate" / NATIVE_COPY_BACKEND_DIR
    frames_dir = output_dir / "frames"
    frames = frame_files(transparent_dir)
    if not frames:
        raise BatchError("native_copy_failed", f"No transparent frames found in {rel(transparent_dir)}")
    output_dir.mkdir(parents=True, exist_ok=True)
    if frames_dir.exists() and any(frames_dir.iterdir()) and not args.overwrite_work:
        raise BatchError("native_copy_failed", f"Native candidate frames already exist: {rel(frames_dir)}")
    if frames_dir.exists() and args.overwrite_work:
        shutil.rmtree(frames_dir)
    frames_dir.mkdir(parents=True, exist_ok=True)
    created: list[Path] = []
    target_size = (target_canvas["width"], target_canvas["height"])
    for src in frames:
        dst = frames_dir / src.name
        if dst.exists():
            raise BatchError("native_copy_failed", f"Refusing to overwrite existing native candidate frame: {rel(dst)}")
        with Image.open(src) as img:
            rgba = img.convert("RGBA")
            if rgba.size != target_size:
                raise BatchError(
                    "native_copy_failed",
                    f"Native copy frame size mismatch: {src.name} is {rgba.size}, expected {target_size}.",
                )
            rgba.save(dst)
        created.append(dst)
    report = {
        "assetId": asset_id,
        "backend": NATIVE_COPY_BACKEND_DIR,
        "candidateBackendDir": NATIVE_COPY_BACKEND_DIR,
        "method": "native_copy",
        "candidateFrameLabel": "native-copy final candidate",
        "inputDir": rel(transparent_dir),
        "outputDir": rel(output_dir),
        "framesDir": rel(frames_dir),
        "targetWidth": target_canvas["width"],
        "targetHeight": target_canvas["height"],
        "inputFrameCount": len(frames),
        "outputFrameCount": len(created),
        "createdFiles": [rel(path) for path in created],
        "qualityGateStatus": "pass",
        "qualityGateReasons": ["720p 960-class source copied without upscaling after background removal."],
        "alphaPolicy": "native-copy-source-alpha",
        "alphaDiagnosticPaths": None,
        "alphaDiagnosticSummary": None,
        "promotionPerformed": False,
        "dryRun": False,
        "generatedAtUtc": now_utc(),
    }
    json_path = output_dir / "upscale_candidate_report.json"
    md_path = output_dir / "upscale_candidate_report.md"
    report["createdFiles"].extend([rel(json_path), rel(md_path)])
    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    write_native_copy_markdown(md_path, report)
    return report


def parse_json_prefix(text: str) -> dict[str, Any] | None:
    stripped = text.lstrip()
    if not stripped:
        return None
    decoder = json.JSONDecoder()
    try:
        payload, _ = decoder.raw_decode(stripped)
    except json.JSONDecodeError:
        return None
    return payload if isinstance(payload, dict) else None


def frame_files(folder: Path) -> list[Path]:
    if not folder.exists():
        return []
    return sorted([p for p in folder.iterdir() if p.is_file() and p.suffix.lower() == ".png"], key=numeric_key)


def validate_final_frames(folder: Path, expected_count: int, target_canvas: dict[str, Any]) -> tuple[bool, list[str]]:
    reasons: list[str] = []
    frames = frame_files(folder)
    if len(frames) != expected_count:
        reasons.append(f"Final frame count mismatch: expected={expected_count}, actual={len(frames)}.")
    target_size = (target_canvas["width"], target_canvas["height"])
    blank_frames: list[str] = []
    for path in frames:
        try:
            with Image.open(path) as img:
                rgba = img.convert("RGBA")
                if rgba.size != target_size:
                    reasons.append(f"Target canvas mismatch: {path.name} is {rgba.size}, expected {target_size}.")
                if "A" not in rgba.getbands():
                    reasons.append(f"Alpha channel missing: {path.name}.")
                elif rgba.getchannel("A").getbbox() is None:
                    blank_frames.append(path.name)
        except Exception as exc:  # noqa: BLE001
            reasons.append(f"Could not read final frame {path.name}: {exc}.")
    if blank_frames:
        reasons.append(f"Blank or fully transparent final frames: {', '.join(blank_frames[:12])}.")
    return not reasons, reasons


def ensure_no_existing_sprite_asset(target: Path) -> None:
    status = classify_asset_folder(target)
    if status["assetStatus"] != "missing":
        raise BatchError(
            "existing_asset_overwrite_risk",
            f"Refusing to write because target is no longer missing: {rel(target)}",
            status["assetStatusReasons"],
        )
    if target.exists():
        for png in target.glob("*.png"):
            raise BatchError("existing_asset_overwrite_risk", f"Refusing to overwrite existing PNG: {rel(png)}")


def create_missing_asset_frames(final_frames_dir: Path, target: Path) -> dict[str, Any]:
    assert_batch_asset_path(target)
    ensure_no_existing_sprite_asset(target)
    frames = frame_files(final_frames_dir)
    if not frames:
        raise BatchError("asset_creation_failed", f"No final candidate frames found in {rel(final_frames_dir)}")
    target.mkdir(parents=True, exist_ok=True)
    created: list[Path] = []
    for index, src in enumerate(frames):
        dst = target / f"{index:04d}.png"
        if dst.exists():
            raise BatchError("asset_creation_failed", f"Refusing to overwrite existing asset frame: {rel(dst)}")
        shutil.copyfile(src, dst)
        created.append(dst)
    return {
        "targetAssetDir": rel(target),
        "createdFrameCount": len(created),
        "createdFiles": [rel(path) for path in created],
        "metaFilesCreatedManually": False,
        "unityImportRequired": True,
        "note": "Unity may create .meta files on later import; this script did not create or modify .meta files.",
    }


def choose_review_positions(count: int, desired: int = 8) -> list[int]:
    if count <= 0:
        return []
    if count <= desired:
        return list(range(count))
    return sorted({round(index * (count - 1) / max(1, desired - 1)) for index in range(desired)})


def checkerboard(size: tuple[int, int], tile: int = 16) -> Image.Image:
    image = Image.new("RGBA", size, (255, 255, 255, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            color = (90, 90, 90, 255) if ((x // tile + y // tile) % 2) else (42, 42, 42, 255)
            draw.rectangle([x, y, x + tile - 1, y + tile - 1], fill=color)
    return image


def preview_image(path: Path, cell: tuple[int, int]) -> Image.Image:
    with Image.open(path) as img:
        rgba = img.convert("RGBA")
    bg = checkerboard(rgba.size)
    bg.alpha_composite(rgba)
    scale = min(cell[0] / max(1, bg.width), cell[1] / max(1, bg.height), 1.0)
    preview = bg.convert("RGB").resize((max(1, round(bg.width * scale)), max(1, round(bg.height * scale))), Image.Resampling.NEAREST)
    canvas = Image.new("RGB", cell, (24, 24, 24))
    canvas.paste(preview, ((cell[0] - preview.width) // 2, (cell[1] - preview.height) // 2))
    return canvas


def runtime_reference_dir_for_review(target: Path | None) -> Path | None:
    if target is None or not target.exists() or not target.is_dir():
        return None
    if frame_files(target):
        return target
    child_dirs = []
    for child in sorted([path for path in target.iterdir() if path.is_dir()], key=lambda path: path.name.lower()):
        frames = frame_files(child)
        if frames:
            child_dirs.append((len(frames), child))
    if not child_dirs:
        return None
    return sorted(child_dirs, key=lambda item: (-item[0], rel(item[1]) or str(item[1])))[0][1]


def build_assetization_review_packet(
    asset_id: str,
    batch_root: Path,
    raw_dir: Path,
    transparent_dir: Path,
    final_frames_dir: Path,
    runtime_reference_dir: Path | None,
    created_asset_dir: Path | None,
    result: dict[str, Any],
) -> dict[str, Any]:
    review_dir = batch_root / "review"
    review_dir.mkdir(parents=True, exist_ok=True)
    raw_frames = frame_files(raw_dir)
    transparent_frames = frame_files(transparent_dir)
    candidate_frames = frame_files(final_frames_dir)
    runtime_reference_frames = frame_files(runtime_reference_dir) if runtime_reference_dir else []
    asset_frames = frame_files(created_asset_dir) if created_asset_dir else []
    count = max(
        len(raw_frames),
        len(transparent_frames),
        len(candidate_frames),
        len(runtime_reference_frames),
        len(asset_frames),
    )
    positions = choose_review_positions(count)
    rows = [
        ("source raw frame", raw_frames),
        ("transparent frame", transparent_frames),
        (result.get("candidateFrameLabel") or "final candidate", candidate_frames),
    ]
    if runtime_reference_frames:
        rows.append(("existing runtime reference", runtime_reference_frames))
    if asset_frames:
        rows.append(("created asset PNG", asset_frames))
    cell = (180, 180)
    header_h = 26
    label_w = 190
    sheet = Image.new("RGB", (label_w + len(positions) * cell[0], header_h + len(rows) * (cell[1] + header_h)), (18, 18, 18))
    draw = ImageDraw.Draw(sheet)
    for col, position in enumerate(positions):
        draw.text((label_w + col * cell[0] + 8, 8), f"f{position}", fill=(255, 255, 255))
    for row_index, (label, frames) in enumerate(rows):
        y = header_h + row_index * (cell[1] + header_h)
        draw.text((8, y + 78), label, fill=(255, 255, 255))
        for col, position in enumerate(positions):
            if position >= len(frames):
                continue
            preview = preview_image(frames[position], cell)
            sheet.paste(preview, (label_w + col * cell[0], y + header_h))
    contact_sheet = review_dir / "ai_assetization_contact_sheet.png"
    sheet.save(contact_sheet)
    report = {
        "assetId": asset_id,
        "contactSheet": rel(contact_sheet),
        "reviewPoints": [
            "외곽선",
            "alpha halo",
            "캐릭터 위치/크기",
            "motion consistency",
            "blank frame 여부",
            "target canvas 일치",
            "과도한 AI detail 변형 여부",
        ],
        "sampledFrames": positions,
        "rawFrameCount": len(raw_frames),
        "transparentFrameCount": len(transparent_frames),
        "candidateFrameCount": len(candidate_frames),
        "existingRuntimeReferencePath": rel(runtime_reference_dir) if runtime_reference_frames else None,
        "existingRuntimeReferenceFrameCount": len(runtime_reference_frames),
        "createdAssetFrameCount": len(asset_frames),
        "qualityGateStatus": result.get("qualityGateStatus"),
        "runtimePromotionPerformed": False,
        "generatedAtUtc": now_utc(),
    }
    json_path = review_dir / "ai_assetization_review_report.json"
    md_path = review_dir / "ai_assetization_review_report.md"
    json_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    write_review_markdown(md_path, report)
    report["createdFiles"] = [rel(contact_sheet), rel(json_path), rel(md_path)]
    return report


def write_review_markdown(path: Path, report: dict[str, Any]) -> None:
    lines = [
        "# AI Assetization Review Report",
        "",
        f"- Asset ID: `{report['assetId']}`",
        f"- Contact sheet: `{report['contactSheet']}`",
        f"- Quality gate status: `{report['qualityGateStatus']}`",
        f"- Runtime promotion performed: `{str(report['runtimePromotionPerformed']).lower()}`",
        "",
        "## Required Review Points",
        "",
    ]
    lines.extend(f"- {item}" for item in report["reviewPoints"])
    lines.extend(["", "## Frame Counts", ""])
    lines.append(f"- Raw: `{report['rawFrameCount']}`")
    lines.append(f"- Transparent: `{report['transparentFrameCount']}`")
    lines.append(f"- Waifu2x final candidate: `{report['candidateFrameCount']}`")
    lines.append(f"- Existing runtime reference: `{report['existingRuntimeReferenceFrameCount']}`")
    lines.append(f"- Created asset PNG: `{report['createdAssetFrameCount']}`")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_assetization_reports(batch_root: Path, result: dict[str, Any]) -> dict[str, str]:
    json_path = batch_root / "assetization_report.json"
    md_path = batch_root / "assetization_report.md"
    json_path.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    lines = [
        "# Assetization Report",
        "",
        f"- Asset ID: `{result['assetId']}`",
        f"- Video: `{result['videoPath']}`",
        f"- Target folder: `{result['targetAssetDir']}`",
        f"- Runtime asset state: `{result.get('runtimeAssetState')}`",
        f"- Approval state: `{result.get('approvalState')}`",
        f"- Processing decision: `{result.get('processingDecision')}`",
        f"- Status: `{result['status']}`",
        f"- Quality gate: `{result['qualityGateStatus']}`",
        f"- Target canvas: `{result.get('targetCanvasWidth')}x{result.get('targetCanvasHeight')}`",
        f"- Target canvas source: `{result.get('targetCanvasSource')}`",
        f"- Target canvas resolved: `{str(result.get('targetCanvasResolved')).lower()}`",
        f"- Candidate output: `{result.get('candidateOutputPath')}`",
        f"- Review packet: `{result.get('reviewPacketPath')}`",
        f"- Assets written: `{str(result.get('assetsWritten')).lower()}`",
        f"- Created asset folder: `{result.get('createdAssetDir') or 'none'}`",
        f"- Created frame count: `{result.get('createdFrameCount', 0)}`",
        f"- Manual .meta changes: `false`",
        f"- Unity import required: `{str(bool(result.get('createdAssetDir'))).lower()}`",
        "",
    ]
    if result.get("defaultAiEscalationMessage"):
        lines.extend([result["defaultAiEscalationMessage"], ""])
    lines.extend(["## Reasons", ""])
    lines.extend(f"- {reason}" for reason in result.get("reasons", []))
    lines.extend(["", "## Quality Gate Reasons", ""])
    lines.extend(f"- {reason}" for reason in result.get("qualityGateReasons", []))
    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return {"json": rel(json_path) or str(json_path), "md": rel(md_path) or str(md_path)}


def init_entry(video: Path, include_720p_960_class: bool) -> dict[str, Any]:
    metadata = video_metadata(video)
    classification = classify_video_source(video, metadata, include_720p_960_class)
    entry = {
        "videoPath": rel(video),
        "absoluteVideoPath": str(video),
        "width": metadata.get("width"),
        "height": metadata.get("height"),
        "duration": metadata.get("duration"),
        "fps": metadata.get("fps"),
        "frameCount": metadata.get("frameCount"),
        "metadata": metadata,
        "isExact480p": classification["isExact480p"],
        "isProbable480p": classification["isProbable480p"],
        "is720p960Class": classification["is720p960Class"],
        "sourceClass": classification["sourceClass"],
        "assetizationMode": classification["assetizationMode"],
        "classificationReason": classification["classificationReason"],
        "assetId": None,
        "category": None,
        "actor": None,
        "action": None,
        "targetAssetDir": None,
        "absoluteTargetAssetDir": None,
        "assetStatus": None,
        "runtimeAssetState": None,
        "approvalState": None,
        "approvalStatus": None,
        "approvalReasons": [],
        "processingDecision": None,
        "sourceSignature": None,
        "targetCanvas": None,
        "targetCanvasResolved": False,
        "candidateOutputPath": None,
        "reviewPacketPath": None,
        "qualityGateStatus": None,
        "qualityGateReasons": [],
        "assetsWritten": False,
        "assetsWrittenPath": None,
        "approvalSuggestion": None,
        "skipReason": None,
        "status": "inventory_only",
        "reasons": [],
        "warnings": [],
    }
    return entry


def enrich_entry(
    entry: dict[str, Any],
    work_root: Path,
    ai_scale: int,
    allow_native_canvas_for_missing: bool,
    approval_manifest: dict[str, Any],
    approval_manifest_was_missing: bool,
) -> None:
    video = Path(entry["absoluteVideoPath"])
    if entry["metadata"].get("metadataStatus") != "ok":
        entry["reasons"].append("ambiguous_resolution")
        entry["warnings"].append("Video metadata could not be read reliably; extraction may still fail later.")
    try:
        inferred = infer_asset(video, work_root)
    except BatchError as exc:
        inferred = {
            "assetId": fallback_asset_id(video),
            "category": None,
            "actor": None,
            "action": None,
            "targetAssetDir": None,
            "inferenceWarnings": [*exc.reasons, "cannot_infer_target_asset_dir"],
        }
        entry["reasons"].append("cannot_infer_target_asset_dir")
    entry.update(
        {
            "assetId": inferred["assetId"],
            "category": inferred["category"],
            "actor": inferred["actor"],
            "action": inferred["action"],
            "targetAssetDir": rel(inferred["targetAssetDir"]) if inferred["targetAssetDir"] is not None else None,
            "absoluteTargetAssetDir": str(inferred["targetAssetDir"]) if inferred["targetAssetDir"] is not None else None,
        }
    )
    entry["warnings"].extend(inferred["inferenceWarnings"])
    if inferred["targetAssetDir"] is not None:
        asset_classification = classify_asset_folder(inferred["targetAssetDir"])
    else:
        asset_classification = {
            "assetStatus": "missing",
            "assetStatusReasons": ["Target asset folder could not be inferred; Assets/** writes are forbidden for this source."],
            "directPngAudit": {},
            "childSpriteFolders": [],
        }
    entry.update(asset_classification)
    entry["runtimeAssetState"] = asset_classification["assetStatus"]
    signature = source_signature(video)
    entry["sourceSignature"] = signature
    approval = evaluate_approval_state(
        entry.get("assetId"),
        video,
        signature,
        approval_manifest,
        approval_manifest_was_missing,
    )
    entry.update(approval)
    native_canvas_scale = 1 if entry.get("assetizationMode") == "native_copy" else ai_scale
    inferred_target = inferred["targetAssetDir"]
    canvas = resolve_target_canvas(
        inferred["category"],
        inferred["actor"],
        inferred["action"],
        inferred_target,
        entry["metadata"],
        native_canvas_scale,
        approval.get("approvalRecord"),
    )
    if entry.get("assetizationMode") == "native_copy" and entry.get("runtimeAssetState") == "missing":
        native_canvas = source_ai_native_canvas(entry["metadata"], native_canvas_scale)
        if native_canvas:
            canvas = native_canvas
    canvas = allow_native_canvas_for_missing_if_requested(
        canvas,
        entry.get("runtimeAssetState"),
        inferred_target,
        allow_native_canvas_for_missing,
    )
    entry["targetCanvas"] = canvas
    entry["targetCanvasResolved"] = canvas.get("status") == "resolved"
    update_probable_from_canvas(entry)


def is_eligible_video(entry: dict[str, Any]) -> bool:
    return bool(entry.get("isExact480p") or entry.get("isProbable480p") or entry.get("is720p960Class"))


def candidate_backend_dir_for_entry(entry: dict[str, Any]) -> str:
    return NATIVE_COPY_BACKEND_DIR if entry.get("assetizationMode") == "native_copy" else WAIFU2X_BACKEND_DIR


def planned_candidate_frames_path(work_root: Path, asset_id: str | None, backend_dir: str) -> str | None:
    if not asset_id:
        return None
    return rel(work_root / asset_id / "batch_480p_assetization" / "upscaled_runtime_candidate" / backend_dir / "frames")


def planned_review_packet_path(work_root: Path, asset_id: str | None) -> str | None:
    if not asset_id:
        return None
    return rel(work_root / asset_id / "batch_480p_assetization" / "review")


def assign_processing_decision(entry: dict[str, Any], args: argparse.Namespace, work_root: Path) -> None:
    if not is_eligible_video(entry):
        entry["status"] = "skipped_not_480p"
        entry["processingDecision"] = None
        entry["skipReason"] = "not_480p_or_probable_480p"
        return
    if not entry.get("assetId"):
        entry["status"] = "skipped"
        entry["processingDecision"] = "skip_unmappable"
        entry["skipReason"] = "asset_id_missing"
        return
    if not entry.get("absoluteTargetAssetDir"):
        entry["status"] = "skipped"
        entry["processingDecision"] = "skip_unmappable_target"
        entry["skipReason"] = "target_asset_dir_unmapped"
        entry["qualityGateStatus"] = "not_run"
        entry["qualityGateReasons"] = ["Target asset folder could not be inferred; Assets/** writes are forbidden for this source."]
        return

    backend_dir = candidate_backend_dir_for_entry(entry)
    entry["candidateOutputPath"] = planned_candidate_frames_path(work_root, entry.get("assetId"), backend_dir)
    entry["reviewPacketPath"] = planned_review_packet_path(work_root, entry.get("assetId"))

    runtime_state_filter = getattr(args, "runtime_state_filter", "any")
    if runtime_state_filter != "any" and entry.get("runtimeAssetState") != runtime_state_filter:
        entry["status"] = "skipped_runtime_state_filter"
        entry["processingDecision"] = "skip_runtime_state_filter"
        entry["skipReason"] = f"runtime_asset_state_{entry.get('runtimeAssetState') or 'none'}_not_{runtime_state_filter}"
        entry["qualityGateStatus"] = "not_run"
        entry["qualityGateReasons"] = [f"Runtime asset state filter is `{runtime_state_filter}`."]
        return

    if entry.get("approvalState") == "approved" and args.skip_approved and not args.force_reprocess_approved:
        entry["status"] = "skipped_approved"
        entry["processingDecision"] = "skip_approved"
        entry["skipReason"] = "approved_complete_asset_manifest_record_valid"
        entry["qualityGateStatus"] = "not_run"
        entry["qualityGateReasons"] = ["Approved complete asset record is valid."]
        return

    if entry.get("approvalState") == "approved":
        entry["status"] = "approved_candidate_forced"
        entry["processingDecision"] = "process_forced"
    else:
        entry["status"] = "unapproved_candidate"
        entry["processingDecision"] = "process_unapproved"
    entry["approvalSuggestion"] = "needs_human_review"
    if entry.get("targetCanvasResolved"):
        entry["qualityGateStatus"] = "pending"
        entry["qualityGateReasons"] = ["Candidate processing has not run yet."]
    else:
        entry["qualityGateStatus"] = "ambiguous"
        entry["qualityGateReasons"] = ["target_canvas_unresolved_candidate_only"]


def should_process(entry: dict[str, Any]) -> bool:
    return entry.get("processingDecision") in {"process_unapproved", "process_forced"}


def mark_ai_missing(entry: dict[str, Any]) -> None:
    entry["status"] = "failed"
    entry["qualityGateStatus"] = "fail"
    entry["qualityGateReasons"] = [AI_EXE_MISSING_MESSAGE]
    entry["reasons"].append(AI_EXE_MISSING_MESSAGE)
    entry["defaultAiEscalationMessage"] = DEFAULT_AI_ESCALATION_MESSAGE


def process_missing_entry(
    entry: dict[str, Any],
    work_root: Path,
    args: argparse.Namespace,
    ai_exe: Path | None,
) -> dict[str, Any]:
    asset_id = entry["assetId"]
    target = Path(entry["absoluteTargetAssetDir"]) if entry.get("absoluteTargetAssetDir") else None
    batch_root = work_root / asset_id / "batch_480p_assetization"
    raw_dir = batch_root / "raw_frames"
    transparent_dir = batch_root / "transparent_frames"
    backend_dir = candidate_backend_dir_for_entry(entry)
    backend_name = backend_dir
    native_canvas_scale = 1 if entry.get("assetizationMode") == "native_copy" else args.ai_scale
    target_canvas = dict(entry["targetCanvas"] or {})
    target_canvas_resolved = target_canvas.get("status") == "resolved"
    result: dict[str, Any] = {
        "assetId": asset_id,
        "sourceVideoPath": entry["videoPath"],
        "videoPath": entry["videoPath"],
        "targetAssetDir": entry["targetAssetDir"],
        "runtimeAssetState": entry.get("runtimeAssetState"),
        "approvalState": entry.get("approvalState"),
        "approvalStatus": entry.get("approvalStatus"),
        "processingDecision": entry.get("processingDecision"),
        "sourceSignature": entry.get("sourceSignature"),
        "status": "failed",
        "qualityGateStatus": "fail",
        "qualityGateReasons": [],
        "reasons": [],
        "warnings": entry.get("warnings", []),
        "targetCanvas": [target_canvas.get("width"), target_canvas.get("height")]
        if target_canvas.get("width") and target_canvas.get("height")
        else None,
        "targetCanvasWidth": target_canvas.get("width"),
        "targetCanvasHeight": target_canvas.get("height"),
        "targetCanvasSource": target_canvas.get("source"),
        "targetCanvasResolved": target_canvas_resolved,
        "backend": backend_name,
        "aiScale": args.ai_scale,
        "aiNoise": args.ai_noise,
        "alphaPolicy": None,
        "alphaDiagnosticPaths": None,
        "alphaDiagnosticSummary": None,
        "candidateOutputPath": rel(batch_root / "upscaled_runtime_candidate" / backend_dir / "frames"),
        "reviewPacketPath": rel(batch_root / "review"),
        "assetsWritten": False,
        "assetsWrittenPath": None,
        "approvalSuggestion": "needs_human_review",
        "skipReason": None,
        "defaultAiEscalationMessage": DEFAULT_AI_ESCALATION_MESSAGE,
        "createdAssetDir": None,
        "createdFrameCount": 0,
        "generatedAtUtc": now_utc(),
    }
    batch_root_prepared = False
    try:
        frame_count = entry.get("frameCount")
        if isinstance(frame_count, int) and frame_count > args.max_frames:
            raise BatchError("needs_frame_selection", f"Frame count {frame_count} exceeds --max-frames {args.max_frames}.")
        safe_prepare_dir(batch_root, work_root, args.overwrite_work)
        batch_root_prepared = True
        raw_dir.mkdir(parents=True, exist_ok=True)
        extraction = extract_video_frames(Path(entry["absoluteVideoPath"]), raw_dir)
        if extraction["outputFrameCount"] > args.max_frames:
            raise BatchError(
                "needs_frame_selection",
                f"Extracted frame count {extraction['outputFrameCount']} exceeds --max-frames {args.max_frames}.",
            )
        target_canvas = candidate_canvas_from_extraction(target_canvas, extraction, native_canvas_scale)
        target_canvas = allow_native_canvas_for_missing_if_requested(
            target_canvas,
            entry.get("runtimeAssetState"),
            target,
            args.allow_native_canvas_for_missing,
        )
        target_canvas_resolved = target_canvas.get("status") == "resolved"
        result.update(
            {
                "targetCanvas": [target_canvas.get("width"), target_canvas.get("height")],
                "targetCanvasWidth": target_canvas.get("width"),
                "targetCanvasHeight": target_canvas.get("height"),
                "targetCanvasSource": target_canvas.get("source"),
                "targetCanvasResolved": target_canvas_resolved,
            }
        )
        (batch_root / "extraction_manifest.json").write_text(
            json.dumps({"assetId": asset_id, **extraction, "generatedAtUtc": now_utc()}, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
        transparent_dir.mkdir(parents=True, exist_ok=True)
        background = remove_background_frames(raw_dir, transparent_dir)
        if background["frameCount"] != extraction["outputFrameCount"]:
            raise BatchError(
                "background_removal_failed",
                f"Transparent frame count mismatch: raw={extraction['outputFrameCount']}, transparent={background['frameCount']}.",
            )
        (batch_root / "background_removal_manifest.json").write_text(
            json.dumps({"assetId": asset_id, **background, "generatedAtUtc": now_utc()}, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
        if backend_dir == WAIFU2X_BACKEND_DIR:
            if ai_exe is None:
                raise BatchError("ai_upscale_failed", AI_EXE_MISSING_MESSAGE, [AI_EXE_MISSING_MESSAGE])
            upscale = run_upscale_candidate(asset_id, transparent_dir, batch_root, target_canvas, args, ai_exe)
        else:
            upscale = copy_native_candidate_frames(asset_id, transparent_dir, batch_root, target_canvas, args)
        final_frames_dir = batch_root / "upscaled_runtime_candidate" / backend_dir / "frames"
        ok, validation_reasons = validate_final_frames(final_frames_dir, background["frameCount"], target_canvas)
        if not ok:
            raise BatchError("final_candidate_validation_failed", "Final candidate validation failed.", validation_reasons)
        created_asset: dict[str, Any] | None = None
        quality_status = "pass" if target_canvas_resolved else "ambiguous"
        quality_reasons = (
            [
                "Native-copy assetization objective checks passed."
                if backend_dir == NATIVE_COPY_BACKEND_DIR
                else "Default waifu2x AI assetization objective checks passed."
            ]
            if target_canvas_resolved
            else [
                "target_canvas_unresolved_candidate_only",
                target_canvas.get("reason") or "Target canvas was not resolved from a reliable reference.",
            ]
        )
        if (
            args.write_missing_assets
            and entry.get("runtimeAssetState") == "missing"
            and target is not None
            and target_canvas_resolved
            and quality_status == "pass"
        ):
            created_asset = create_missing_asset_frames(final_frames_dir, target)
        review = build_assetization_review_packet(
            asset_id,
            batch_root,
            raw_dir,
            transparent_dir,
            final_frames_dir,
            runtime_reference_dir_for_review(target),
            target if created_asset else None,
            upscale,
        )
        upscale_record = upscale_report_record(batch_root, upscale)
        result.update(
            {
                "status": "created" if created_asset else "candidate_created",
                "qualityGateStatus": quality_status,
                "qualityGateReasons": quality_reasons,
                "defaultAiEscalationMessage": None if quality_status == "pass" else DEFAULT_AI_ESCALATION_MESSAGE,
                "reasons": quality_reasons,
                "extraction": extraction,
                "backgroundRemoval": background,
                "upscaleReport": upscale_record,
                "alphaPolicy": upscale_record.get("alphaPolicy"),
                "alphaDiagnosticPaths": upscale_record.get("alphaDiagnosticPaths"),
                "alphaDiagnosticSummary": upscale_record.get("alphaDiagnosticSummary"),
                "reviewPacket": review,
                "reviewPacketPath": rel(batch_root / "review"),
                "createdAsset": created_asset,
                "createdAssetDir": created_asset["targetAssetDir"] if created_asset else None,
                "createdFrameCount": created_asset["createdFrameCount"] if created_asset else 0,
                "assetsWritten": created_asset is not None,
                "assetsWrittenPath": created_asset["targetAssetDir"] if created_asset else None,
            }
        )
    except BatchError as exc:
        result["status"] = "failed"
        result["qualityGateStatus"] = "fail"
        result["qualityGateReasons"] = [exc.code, *exc.reasons]
        result["reasons"] = [exc.code, *exc.reasons]
        if exc.details:
            result.update(exc.details)
    if batch_root_prepared or args.overwrite_work or not batch_root.exists():
        result["assetizationReportPaths"] = write_assetization_reports(batch_root, result)
    else:
        result["assetizationReportPaths"] = None
    return result


def batch_report_root(work_root: Path) -> Path:
    return work_root / "batch_480p_ai_assetization"


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")


def write_inventory_markdown(path: Path, entries: list[dict[str, Any]]) -> None:
    lines = [
        "# 480p Video Inventory",
        "",
        "| Video | Size | FPS | Frames | Exact 480p | Probable 480p | 720p 960-class | Source class | Asset ID | Target | Runtime asset state | Approval state | Decision | Reason |",
        "|---|---:|---:|---:|---|---|---|---|---|---|---|---|---|---|",
    ]
    for item in entries:
        size = f"{item.get('width')}x{item.get('height')}" if item.get("width") and item.get("height") else "unknown"
        lines.append(
            f"| `{item['videoPath']}` | `{size}` | `{item.get('fps')}` | `{item.get('frameCount')}` | "
            f"`{str(item.get('isExact480p')).lower()}` | `{str(item.get('isProbable480p')).lower()}` | "
            f"`{str(item.get('is720p960Class')).lower()}` | `{item.get('sourceClass') or 'none'}` | "
            f"`{item.get('assetId') or 'none'}` | `{item.get('targetAssetDir') or 'none'}` | "
            f"`{item.get('runtimeAssetState') or item.get('assetStatus') or 'none'}` | "
            f"`{item.get('approvalState') or 'none'}` | `{item.get('processingDecision') or 'none'}` | "
            f"{item.get('classificationReason') or ''} |"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_list_markdown(path: Path, title: str, rows: list[dict[str, Any]], empty: str) -> None:
    lines = [f"# {title}", ""]
    if not rows:
        lines.append(empty)
    else:
        for item in rows:
            reasons = "; ".join(item.get("reasons") or item.get("qualityGateReasons") or item.get("assetStatusReasons") or [])
            lines.append(
                f"- `{item.get('assetId') or 'none'}`: `{item.get('videoPath') or item.get('sourceVideoPath')}` -> "
                f"`{item.get('targetAssetDir') or item.get('createdAssetDir') or item.get('assetsWrittenPath') or 'none'}`"
                + (f" ({reasons})" if reasons else "")
            )
            if item.get("defaultAiEscalationMessage"):
                lines.append(f"  - {item['defaultAiEscalationMessage']}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_batch_markdown(path: Path, report: dict[str, Any]) -> None:
    lines = [
        "# Batch 480p AI Assetization Report",
        "",
        f"- Scanned videos: `{report['scannedVideoCount']}`",
        f"- Exact 480p videos: `{report['exact480pVideoCount']}`",
        f"- Probable 480p videos: `{report['probable480pVideoCount']}`",
        f"- Included 720p 960-class videos: `{report['included720p960ClassVideoCount']}`",
        f"- Runtime state filter: `{report['runtimeStateFilter']}`",
        f"- Native canvas for missing: `{str(report['allowNativeCanvasForMissing']).lower()}`",
        f"- Existing assets: `{report['existingAssetCount']}`",
        f"- Missing assets: `{report['missingAssetCount']}`",
        f"- Partial/ambiguous assets: `{report['partialOrAmbiguousAssetCount']}`",
        f"- Approved skipped: `{report['approvedSkippedCount']}`",
        f"- Unapproved process targets: `{report['unapprovedProcessedCount']}`",
        f"- Existing but unapproved process targets: `{report['existingButUnapprovedProcessedCount']}`",
        f"- Missing process targets: `{report['missingProcessedCount']}`",
        f"- Target canvas unresolved process targets: `{report['targetCanvasUnresolvedProcessedCount']}`",
        f"- Candidate-only count: `{report['candidateOnlyCount']}`",
        f"- Assets written: `{report['assetsWrittenCount']}`",
        f"- Needs human review: `{report['needsHumanReviewCount']}`",
        f"- Created assets: `{report['createdAssetCount']}`",
        f"- Failed assets: `{report['failedAssetCount']}`",
        f"- Alpha policy counts: `{report['alphaPolicyCounts']}`",
        f"- Waifu2x executable: `{report['waifu2xExecutable'] or 'not found'}`",
        f"- AI scale/noise: `{report['aiScale']}` / `{report['aiNoise']}`",
        f"- Requested alpha policy: `{report['alphaPolicy']}`",
        f"- Approval manifest: `{report['approvalManifestPath']}`",
        f"- Candidate-only mode: `{str(report['candidateOnly']).lower()}`",
        f"- Forbidden paths touched: `{report['forbiddenPathsTouched']}`",
        "",
    ]
    if report.get("waifu2xExecutableMissingMessage"):
        lines.extend([report["waifu2xExecutableMissingMessage"], ""])
    if report["assetsFailed"]:
        lines.extend(["## Failed Or Ambiguous", ""])
        for item in report["assetsFailed"]:
            lines.append(f"- `{item.get('assetId')}`: {', '.join(item.get('reasons', []))}")
            if item.get("defaultAiEscalationMessage"):
                lines.append(f"  - {item['defaultAiEscalationMessage']}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def target_canvas_record(value: dict[str, Any] | None) -> list[int] | None:
    if not value:
        return None
    width = value.get("width")
    height = value.get("height")
    if isinstance(width, int) and isinstance(height, int) and width > 0 and height > 0:
        return [width, height]
    return None


def asset_record_from_entry(entry: dict[str, Any], args: argparse.Namespace) -> dict[str, Any]:
    canvas = entry.get("targetCanvas") or {}
    quality_status = entry.get("qualityGateStatus")
    return {
        "assetId": entry.get("assetId"),
        "sourceVideoPath": entry.get("videoPath"),
        "targetAssetDir": entry.get("targetAssetDir"),
        "runtimeAssetState": entry.get("runtimeAssetState") or entry.get("assetStatus"),
        "approvalState": entry.get("approvalState"),
        "approvalStatus": entry.get("approvalStatus"),
        "processingDecision": entry.get("processingDecision"),
        "sourceSignature": entry.get("sourceSignature"),
        "targetCanvas": target_canvas_record(canvas),
        "targetCanvasSource": canvas.get("source"),
        "targetCanvasResolved": canvas.get("status") == "resolved",
        "backend": candidate_backend_dir_for_entry(entry),
        "aiScale": args.ai_scale,
        "aiNoise": args.ai_noise,
        "alphaPolicy": entry.get("alphaPolicy"),
        "alphaDiagnosticPaths": entry.get("alphaDiagnosticPaths"),
        "alphaDiagnosticSummary": entry.get("alphaDiagnosticSummary"),
        "candidateOutputPath": entry.get("candidateOutputPath"),
        "reviewPacketPath": entry.get("reviewPacketPath"),
        "qualityGateStatus": entry.get("qualityGateStatus"),
        "qualityGateReasons": entry.get("qualityGateReasons") or entry.get("reasons") or [],
        "assetsWritten": False,
        "assetsWrittenPath": None,
        "approvalSuggestion": entry.get("approvalSuggestion"),
        "skipReason": entry.get("skipReason"),
        "defaultAiEscalationMessage": entry.get("defaultAiEscalationMessage")
        or (
            DEFAULT_AI_ESCALATION_MESSAGE
            if quality_status in {"fail", "ambiguous"} and should_process(entry)
            else None
        ),
        "status": entry.get("status"),
        "reasons": entry.get("reasons", []),
        "warnings": entry.get("warnings", []),
    }


def merge_result_into_record(record: dict[str, Any], result: dict[str, Any]) -> dict[str, Any]:
    merged = dict(record)
    for key in (
        "targetCanvas",
        "targetCanvasSource",
        "targetCanvasResolved",
        "backend",
        "alphaPolicy",
        "alphaDiagnosticPaths",
        "alphaDiagnosticSummary",
        "candidateOutputPath",
        "reviewPacketPath",
        "upscaleReport",
        "qualityGateStatus",
        "qualityGateReasons",
        "assetsWritten",
        "assetsWrittenPath",
        "approvalSuggestion",
        "skipReason",
        "defaultAiEscalationMessage",
        "status",
        "reasons",
        "warnings",
    ):
        if key in result:
            merged[key] = result[key]
    return merged


def write_asset_records_markdown(path: Path, records: list[dict[str, Any]]) -> None:
    lines = [
        "# Batch Asset Records",
        "",
        "| Asset ID | Source | Runtime state | Approval state | Decision | Quality | Alpha policy | Candidate | Review | Assets written |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for record in records:
        lines.append(
            f"| `{record.get('assetId') or 'none'}` | `{record.get('sourceVideoPath') or 'none'}` | "
            f"`{record.get('runtimeAssetState') or 'none'}` | `{record.get('approvalState') or 'none'}` | "
            f"`{record.get('processingDecision') or 'none'}` | `{record.get('qualityGateStatus') or 'none'}` | "
            f"`{record.get('alphaPolicy') or 'none'}` | "
            f"`{record.get('candidateOutputPath') or 'none'}` | `{record.get('reviewPacketPath') or 'none'}` | "
            f"`{str(record.get('assetsWritten')).lower()}` |"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_batch_reports(
    entries: list[dict[str, Any]],
    asset_results: list[dict[str, Any]],
    output_root: Path,
    ai_exe: Path | None,
    checked_ai_paths: list[str],
    args: argparse.Namespace,
) -> dict[str, Any]:
    eligible = [entry for entry in entries if is_eligible_video(entry)]
    existing = [entry for entry in eligible if entry.get("assetStatus") == "existing"]
    missing = [entry for entry in eligible if entry.get("assetStatus") == "missing"]
    partial = [entry for entry in eligible if entry.get("assetStatus") == "partial_or_ambiguous"]
    result_by_source = {result.get("sourceVideoPath") or result.get("videoPath"): result for result in asset_results}
    asset_records = []
    for entry in eligible:
        record = asset_record_from_entry(entry, args)
        result = result_by_source.get(entry.get("videoPath"))
        if result:
            record = merge_result_into_record(record, result)
        asset_records.append(record)
    created = [result for result in asset_results if result.get("status") == "created"]
    candidate_only = [result for result in asset_results if result.get("status") == "candidate_created"]
    approved_skipped = [record for record in asset_records if record.get("processingDecision") == "skip_approved"]
    process_records = [record for record in asset_records if record.get("processingDecision") in {"process_unapproved", "process_forced"}]
    unapproved_process = [record for record in asset_records if record.get("processingDecision") == "process_unapproved"]
    existing_unapproved = [
        record
        for record in unapproved_process
        if record.get("runtimeAssetState") == "existing"
    ]
    missing_process = [
        record
        for record in process_records
        if record.get("runtimeAssetState") == "missing"
    ]
    target_canvas_unresolved = [
        record
        for record in process_records
        if not record.get("targetCanvasResolved")
    ]
    needs_review = [record for record in process_records if record.get("approvalSuggestion") == "needs_human_review"]
    assets_written_records = [record for record in asset_records if record.get("assetsWritten")]
    candidate_only_records = [record for record in process_records if not record.get("assetsWritten")]
    failed_or_ambiguous_records = [
        record
        for record in asset_records
        if record.get("qualityGateStatus") in {"fail", "ambiguous"}
        or record.get("status") in {"failed", "ambiguous"}
    ]
    alpha_policy_counts = dict(Counter(record.get("alphaPolicy") or "not_run" for record in process_records))
    report = {
        "scannedVideoCount": len(entries),
        "exact480pVideoCount": sum(1 for entry in entries if entry.get("isExact480p")),
        "probable480pVideoCount": sum(1 for entry in entries if entry.get("isProbable480p") and not entry.get("isExact480p")),
        "included720p960ClassVideoCount": sum(1 for entry in entries if entry.get("is720p960Class")),
        "eligibleVideoCount": len(eligible),
        "eligible480pVideoCount": len(eligible),
        "existingAssetCount": len(existing),
        "missingAssetCount": len(missing),
        "partialOrAmbiguousAssetCount": len(partial),
        "createdAssetCount": len(created),
        "candidateOnlyAssetCount": len(candidate_only),
        "skippedExistingAssetCount": 0,
        "approvedSkippedCount": len(approved_skipped),
        "unapprovedProcessedCount": len(unapproved_process),
        "existingButUnapprovedProcessedCount": len(existing_unapproved),
        "missingProcessedCount": len(missing_process),
        "targetCanvasUnresolvedProcessedCount": len(target_canvas_unresolved),
        "candidateOnlyCount": len(candidate_only_records),
        "assetsWrittenCount": len(assets_written_records),
        "needsHumanReviewCount": len(needs_review),
        "failedAssetCount": len(failed_or_ambiguous_records),
        "alphaPolicyCounts": alpha_policy_counts,
        "waifu2xExecutable": rel(ai_exe),
        "waifu2xExecutableCheckedPaths": checked_ai_paths,
        "waifu2xExecutableMissingMessage": AI_EXE_MISSING_MESSAGE if ai_exe is None else None,
        "aiScale": args.ai_scale,
        "aiNoise": args.ai_noise,
        "alphaPolicy": args.alpha_policy,
        "approvalManifestPath": rel(resolve_manifest_path(args.approval_manifest)),
        "skipApproved": args.skip_approved,
        "forceReprocessApproved": args.force_reprocess_approved,
        "candidateOnly": args.candidate_only,
        "dryRun": args.dry_run,
        "writeMissingAssets": args.write_missing_assets,
        "runtimeStateFilter": args.runtime_state_filter,
        "include720p960Class": args.include_720p_960_class,
        "allowNativeCanvasForMissing": args.allow_native_canvas_for_missing,
        "assetsCreated": created,
        "assetsCandidateOnly": candidate_only,
        "assetsSkipped": approved_skipped,
        "assetsApprovedSkipped": approved_skipped,
        "assetsExistingButUnapprovedProcessed": existing_unapproved,
        "assetsMissingProcessed": missing_process,
        "assetsTargetCanvasUnresolvedProcessed": target_canvas_unresolved,
        "assetsNeedsHumanReview": needs_review,
        "assetRecords": asset_records,
        "assetsPartialOrAmbiguous": partial,
        "assetsFailed": failed_or_ambiguous_records,
        "sourceMappings": [
            {
                "videoPath": entry["videoPath"],
                "assetId": entry.get("assetId"),
                "targetAssetDir": entry.get("targetAssetDir"),
                "runtimeAssetState": entry.get("runtimeAssetState") or entry.get("assetStatus"),
                "approvalState": entry.get("approvalState"),
                "processingDecision": entry.get("processingDecision"),
                "targetCanvas": entry.get("targetCanvas"),
            }
            for entry in entries
            if entry.get("assetId")
        ],
        "forbiddenPathsTouched": [],
        "generatedAtUtc": now_utc(),
    }
    write_json(output_root / "inventory_480p_videos.json", entries)
    write_inventory_markdown(output_root / "inventory_480p_videos.md", entries)
    write_json(output_root / "batch_assetization_report.json", report)
    write_batch_markdown(output_root / "batch_assetization_report.md", report)
    write_asset_records_markdown(output_root / "batch_asset_records.md", asset_records)
    write_list_markdown(output_root / "skipped_approved_assets.md", "Skipped Approved Assets", approved_skipped, "No approved assets were skipped.")
    write_list_markdown(
        output_root / "failed_or_ambiguous_assets.md",
        "Failed Or Ambiguous Assets",
        report["assetsFailed"],
        "No failed or ambiguous assets were reported.",
    )
    write_list_markdown(output_root / "created_missing_assets.md", "Created Missing Assets", created, "No missing assets were created.")
    write_list_markdown(
        output_root / "existing_but_unapproved_assets.md",
        "Existing Runtime Assets Processed As Unapproved",
        existing_unapproved,
        "No existing runtime assets were processed as unapproved.",
    )
    return report


def required_file_status() -> list[dict[str, Any]]:
    rows = []
    for relative in REQUIRED_READ_FILES:
        path = project_root() / relative
        rows.append({"path": relative, "exists": path.exists(), "readable": path.exists() and path.is_file()})
    return rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan-root", default="Assets")
    parser.add_argument("--work-root", default="SpritePipelineWork")
    parser.add_argument("--approval-manifest", default=DEFAULT_APPROVAL_MANIFEST)
    parser.add_argument("--ai-upscaler-exe")
    parser.add_argument("--ai-scale", type=int, choices=(2, 4), default=2)
    parser.add_argument("--ai-noise", type=int, choices=(-1, 0, 1, 2, 3), default=0)
    parser.add_argument("--ai-model-path")
    parser.add_argument("--ai-gpu-id", type=int)
    parser.add_argument("--ai-tile-size", type=int)
    parser.add_argument(
        "--alpha-policy",
        choices=("source-mask-nearest", "source-mask-threshold", "trust-ai-alpha"),
        default="source-mask-threshold",
    )
    parser.add_argument("--max-frames", type=int, default=240)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--skip-approved", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--force-reprocess-approved", action="store_true")
    parser.add_argument("--candidate-only", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--write-missing-assets", action="store_true")
    parser.add_argument("--runtime-state-filter", choices=("any", "missing"), default="any")
    parser.add_argument("--include-720p-960-class", action="store_true")
    parser.add_argument("--allow-native-canvas-for-missing", action="store_true")
    parser.add_argument("--overwrite-work", action="store_true")
    parser.add_argument("--keep-ai-temp", action="store_true")
    args = parser.parse_args()

    if args.ai_tile_size is not None and args.ai_tile_size < 1:
        raise SystemExit("--ai-tile-size must be at least 1 when provided.")
    if args.max_frames < 1:
        raise SystemExit("--max-frames must be at least 1.")
    if args.write_missing_assets:
        args.candidate_only = False

    scan_root = resolve_project_path(args.scan_root)
    work_root = resolve_project_path(args.work_root)
    approval_manifest_path = resolve_manifest_path(args.approval_manifest)
    approval_manifest, approval_manifest_was_missing = load_or_create_approval_manifest(approval_manifest_path)
    output_root = batch_report_root(work_root)
    assert_work_path(output_root, work_root)
    output_root.mkdir(parents=True, exist_ok=True)

    ai_exe, checked_ai_paths = find_waifu2x_exe(args.ai_upscaler_exe)
    videos = scan_videos(scan_root, work_root)
    entries = [init_entry(video, args.include_720p_960_class) for video in videos]
    for entry in entries:
        enrich_entry(
            entry,
            work_root,
            args.ai_scale,
            args.allow_native_canvas_for_missing,
            approval_manifest,
            approval_manifest_was_missing,
        )
    for entry in entries:
        assign_processing_decision(entry, args, work_root)

    asset_results: list[dict[str, Any]] = []
    if not args.dry_run:
        for entry in entries:
            if not should_process(entry):
                continue
            if candidate_backend_dir_for_entry(entry) == WAIFU2X_BACKEND_DIR and ai_exe is None:
                mark_ai_missing(entry)
                continue
            result = process_missing_entry(entry, work_root, args, ai_exe)
            asset_results.append(result)

    report = build_batch_reports(entries, asset_results, output_root, ai_exe, checked_ai_paths, args)
    run_summary = {
        "requiredFiles": required_file_status(),
        "approvalManifest": rel(approval_manifest_path),
        "approvalManifestCreated": approval_manifest_was_missing,
        "batchReport": rel(output_root / "batch_assetization_report.json"),
        "inventory": rel(output_root / "inventory_480p_videos.json"),
        "scannedVideoCount": report["scannedVideoCount"],
        "eligible480pVideoCount": report["eligible480pVideoCount"],
        "included720p960ClassVideoCount": report["included720p960ClassVideoCount"],
        "existingAssetCount": report["existingAssetCount"],
        "missingAssetCount": report["missingAssetCount"],
        "partialOrAmbiguousAssetCount": report["partialOrAmbiguousAssetCount"],
        "approvedSkippedCount": report["approvedSkippedCount"],
        "unapprovedProcessedCount": report["unapprovedProcessedCount"],
        "existingButUnapprovedProcessedCount": report["existingButUnapprovedProcessedCount"],
        "targetCanvasUnresolvedProcessedCount": report["targetCanvasUnresolvedProcessedCount"],
        "candidateOnlyCount": report["candidateOnlyCount"],
        "assetsWrittenCount": report["assetsWrittenCount"],
        "createdAssetCount": report["createdAssetCount"],
        "waifu2xExecutable": report["waifu2xExecutable"],
        "waifu2xExecutableMissingMessage": report["waifu2xExecutableMissingMessage"],
    }
    print(json.dumps(run_summary, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
