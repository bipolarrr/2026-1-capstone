#!/usr/bin/env python3
"""Regression checks for sprite upscale alpha policies."""

from __future__ import annotations

import json
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw

from upscale_runtime_candidate import (
    alpha_policy_failure_reasons,
    build_alpha_policy_diagnostic,
    normalize_ai_frames,
)


def make_source(path: Path) -> None:
    image = Image.new("RGBA", (10, 10), (24, 40, 64, 0))
    draw = ImageDraw.Draw(image)
    draw.rectangle((3, 3, 6, 6), fill=(180, 80, 40, 255))
    image.save(path)


def make_ai_rgb(path: Path) -> None:
    image = Image.new("RGB", (10, 10), (40, 52, 88))
    draw = ImageDraw.Draw(image)
    draw.rectangle((3, 3, 6, 6), fill=(220, 120, 64))
    image.save(path)


def make_bad_ai_alpha(path: Path) -> None:
    image = Image.new("RGBA", (10, 10), (40, 52, 88, 0))
    draw = ImageDraw.Draw(image)
    draw.rectangle((2, 2, 7, 7), fill=(80, 96, 132, 16))
    draw.rectangle((3, 3, 6, 6), fill=(220, 120, 64, 255))
    image.save(path)


def assert_source_policy_removes_halo(source: Path, ai_rgb: Path, root: Path) -> dict[str, object]:
    frames_dir = root / "source_mask" / "frames"
    created, _ = normalize_ai_frames(
        [source],
        [ai_rgb],
        frames_dir,
        (10, 10),
        "source-mask-nearest",
        8,
    )
    with Image.open(created[0]) as final_img:
        final_alpha = final_img.convert("RGBA").getchannel("A")
        assert final_alpha.getpixel((2, 2)) == 0, "source-mask-nearest kept halo alpha outside source mask"
        assert final_alpha.getpixel((3, 3)) == 255, "source-mask-nearest lost source object alpha"
    diagnostic = build_alpha_policy_diagnostic(
        [source],
        created,
        "waifu2x",
        "source-mask-nearest",
        8,
        31,
        0.0001,
        1,
    )
    assert diagnostic["extraAlphaPixelCount"] == 0, "source-mask-nearest reported extra alpha"
    return diagnostic


def assert_trust_ai_alpha_detects_halo(source: Path, bad_ai_alpha: Path, root: Path) -> dict[str, object]:
    frames_dir = root / "trust_ai" / "frames"
    created, _ = normalize_ai_frames(
        [source],
        [bad_ai_alpha],
        frames_dir,
        (10, 10),
        "trust-ai-alpha",
        8,
    )
    diagnostic = build_alpha_policy_diagnostic(
        [source],
        created,
        "waifu2x",
        "trust-ai-alpha",
        8,
        31,
        0.0001,
        1,
    )
    assert diagnostic["extraAlphaPixelCount"] > 0, "trust-ai-alpha did not detect extra halo alpha"
    assert diagnostic["lowAlphaPixelCount"] > 0, "trust-ai-alpha did not report low-alpha residue"
    assert alpha_policy_failure_reasons(diagnostic), "trust-ai-alpha halo did not fail alpha quality gate"
    return diagnostic


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="alpha_policy_regression_") as temp:
        root = Path(temp)
        source = root / "source.png"
        ai_rgb = root / "ai_rgb.png"
        bad_ai_alpha = root / "bad_ai_alpha.png"
        make_source(source)
        make_ai_rgb(ai_rgb)
        make_bad_ai_alpha(bad_ai_alpha)

        trust_diagnostic = assert_trust_ai_alpha_detects_halo(source, bad_ai_alpha, root)
        source_diagnostic = assert_source_policy_removes_halo(source, ai_rgb, root)

        print(
            json.dumps(
                {
                    "trustAiExtraAlphaPixelCount": trust_diagnostic["extraAlphaPixelCount"],
                    "trustAiLowAlphaPixelCount": trust_diagnostic["lowAlphaPixelCount"],
                    "sourceMaskExtraAlphaPixelCount": source_diagnostic["extraAlphaPixelCount"],
                },
                indent=2,
            )
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
