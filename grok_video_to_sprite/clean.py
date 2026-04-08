"""
clean.py — rembg 출력 정제

문제:
  - 반투명 경계 픽셀: rembg가 캐릭터색+배경색을 혼합한 픽셀을 남김 → 흰 테두리/번짐
  - 색 지글거림: 동영상 압축으로 동일 픽셀의 RGB가 프레임마다 최대 16 수준으로 달라짐

해결:
  1. 높은 알파 임계값(230+)으로 반투명 경계 픽셀을 완전 투명으로 처리
  2. 각 프레임의 자체 픽셀 색을 사용하여 블록 단위 공간 중앙값으로 압축 노이즈 제거
     → 프레임마다 독립 처리하므로 동적 애니메이션 세부사항(눈, 얼굴 등)이 보존됨
  3. 논리 픽셀 크기를 자동 감지(최다 불투명 픽셀 프레임 기준)해 블록 단위로 단일 색으로 균일화
     → 업스케일된 픽셀아트의 각 "픽셀"이 완전히 같은 색으로 채워짐
"""

from collections import Counter
from pathlib import Path
import sys

import numpy as np
from PIL import Image


# =========================================================
# TUNING VALUES
# =========================================================

# 이 alpha 미만이면 완전 투명으로 처리
# 높을수록 반투명 경계 픽셀이 제거되어 테두리가 깔끔해짐 (권장: 220~245)
ALPHA_THRESHOLD = 230

# 어두운 픽셀(검은 테두리)에 적용할 낮은 알파 임계값
# 밝기가 DARK_BRIGHTNESS 이하인 픽셀은 이 임계값을 사용해 테두리가 날아가지 않게 함
DARK_ALPHA_THRESHOLD = 80
DARK_BRIGHTNESS = 60

# 논리 픽셀 크기 (0 = 자동 감지, 1 = 균일화 없음, N = N×N 블록으로 균일화)
PIXEL_SIZE = 0

# 자동 감지 시 탐색 최대 블록 크기
PIXEL_SIZE_MAX = 20

# 런 감지 시 허용 색 오차 (압축 노이즈 대응)
RUN_COLOR_TOLERANCE = 1

# 블록 내 불투명 픽셀 비율이 이 값 이상이면 블록 전체를 불투명으로 처리
# 낮출수록 캐릭터를 더 많이 포함, 높일수록 더 많이 깎음 (권장: 0.3~0.7)
BLOCK_OPACITY_THRESHOLD = 0.55


# =========================================================
# CORE
# =========================================================

def load_frames(files: list[Path]) -> np.ndarray:
    """(T, H, W, 4) uint8 배열로 반환"""
    return np.stack(
        [np.array(Image.open(f).convert("RGBA")) for f in files],
        axis=0
    )


def detect_pixel_size(median_rgb: np.ndarray, opaque_mask: np.ndarray) -> int:
    """
    불투명 영역에서 유사 색이 수평·수직으로 연속되는 런 길이의 최빈값으로
    논리 픽셀(블록) 크기를 추정한다.

    픽셀아트를 N배 업스케일한 이미지라면 동일 색 런이 N 단위로 나타나므로
    최빈 런 길이 ≈ N.
    """
    H, W, _ = median_rgb.shape
    counter: Counter[int] = Counter()

    # 수평 방향 런
    for y in range(H):
        x = 0
        while x < W:
            if not opaque_mask[y, x]:
                x += 1
                continue
            base = median_rgb[y, x].astype(np.int32)
            start = x
            while x < W and opaque_mask[y, x]:
                if np.abs(median_rgb[y, x].astype(np.int32) - base).max() > RUN_COLOR_TOLERANCE:
                    break
                x += 1
            length = x - start
            if 2 <= length <= PIXEL_SIZE_MAX:
                counter[length] += 1

    # 수직 방향 런
    for x in range(W):
        y = 0
        while y < H:
            if not opaque_mask[y, x]:
                y += 1
                continue
            base = median_rgb[y, x].astype(np.int32)
            start = y
            while y < H and opaque_mask[y, x]:
                if np.abs(median_rgb[y, x].astype(np.int32) - base).max() > RUN_COLOR_TOLERANCE:
                    break
                y += 1
            length = y - start
            if 2 <= length <= PIXEL_SIZE_MAX:
                counter[length] += 1

    if not counter:
        return 1
    return counter.most_common(1)[0][0]


def snap_mask_to_blocks(mask: np.ndarray, pixel_size: int) -> np.ndarray:
    """
    pixel_size × pixel_size 격자 단위로 알파 마스크를 정렬.
    블록 안의 불투명 픽셀 비율이 50% 이상이면 블록 전체를 불투명으로,
    미만이면 전체 투명으로 처리.
    경계가 블록 격자에 딱 맞아 떨어지므로 울퉁불퉁한 테두리가 사라짐.
    """
    H, W = mask.shape
    snapped = np.zeros((H, W), dtype=bool)
    for y in range(0, H, pixel_size):
        for x in range(0, W, pixel_size):
            block = mask[y:y + pixel_size, x:x + pixel_size]
            if block.mean() >= BLOCK_OPACITY_THRESHOLD:
                snapped[y:y + pixel_size, x:x + pixel_size] = True
    return snapped


def apply_pixel_blocks(rgb: np.ndarray, opaque: np.ndarray, pixel_size: int) -> np.ndarray:
    """
    pixel_size × pixel_size 격자로 이미지를 나누고,
    각 블록 안의 불투명 픽셀을 블록 내 중앙값 색으로 균일하게 덮어씀.
    """
    H, W, _ = rgb.shape
    out = rgb.copy()
    for y in range(0, H, pixel_size):
        for x in range(0, W, pixel_size):
            block_mask = opaque[y:y + pixel_size, x:x + pixel_size]
            if not block_mask.any():
                continue
            block_pixels = rgb[y:y + pixel_size, x:x + pixel_size][block_mask]  # (N, 3)
            color = np.median(block_pixels, axis=0).round().astype(np.uint8)
            out[y:y + pixel_size, x:x + pixel_size][block_mask] = color
    return out


def apply_clean(frames: np.ndarray, alpha_threshold: int, pixel_size_override: int) -> list[np.ndarray]:
    T, H, W, _ = frames.shape

    # 어두운 픽셀(검은 테두리)은 낮은 알파 임계값 적용, 나머지는 일반 임계값 적용
    alpha = frames[:, :, :, 3]
    brightness = frames[:, :, :, :3].astype(np.float32).mean(axis=-1)  # (T, H, W)
    is_dark = brightness <= DARK_BRIGHTNESS
    opaque = (alpha >= alpha_threshold) | (is_dark & (alpha >= DARK_ALPHA_THRESHOLD))  # (T, H, W)

    # 논리 픽셀 크기 결정
    # 자동 감지 시: 불투명 픽셀이 가장 많은 프레임을 기준으로 사용
    if pixel_size_override == 0:
        opaque_counts = opaque.sum(axis=(1, 2))          # (T,)
        best_t = int(np.argmax(opaque_counts))
        if opaque_counts[best_t] == 0:
            pixel_size = 1
            print("경고: 불투명 픽셀 없음, 픽셀 크기 1로 설정")
        else:
            best_rgb = frames[best_t, :, :, :3]          # (H, W, 3)
            best_opaque = opaque[best_t]                  # (H, W) bool
            pixel_size = detect_pixel_size(best_rgb, best_opaque)
            print(f"감지된 픽셀 크기: {pixel_size}×{pixel_size}  (기준 프레임: {best_t})")
    else:
        pixel_size = pixel_size_override
        print(f"픽셀 크기 (수동 설정): {pixel_size}×{pixel_size}")

    # 각 프레임에 적용
    results = []
    for t in range(T):
        result = np.zeros((H, W, 4), dtype=np.uint8)
        mask = opaque[t]

        # 알파 마스크를 블록 격자에 먼저 정렬 → 테두리가 직선이 됨
        if pixel_size > 1:
            mask = snap_mask_to_blocks(mask, pixel_size)

        result[:, :, :3] = frames[t, :, :, :3]
        result[mask, 3] = 255

        # 블록 단위 RGB 균일화
        if pixel_size > 1:
            result[:, :, :3] = apply_pixel_blocks(result[:, :, :3], mask, pixel_size)

        results.append(result)

    return results


# =========================================================
# MAIN
# =========================================================

def main():
    if len(sys.argv) < 2:
        print("사용법: python clean.py <이미지 파일 또는 폴더>")
        sys.exit(1)

    target = Path(sys.argv[1]).resolve()

    if not target.exists():
        print(f"오류: 경로가 없습니다: {target}")
        sys.exit(1)

    exts = {".png", ".jpg", ".jpeg", ".bmp", ".webp"}

    if target.is_file():
        files = [target]
        out_dir = target.parent / f"{target.stem}_clean"
    elif target.is_dir():
        files = sorted(p for p in target.iterdir() if p.suffix.lower() in exts)
        if not files:
            print("오류: 폴더 안에 이미지가 없습니다.")
            sys.exit(1)
        out_dir = target.parent / f"{target.name}_clean"
    else:
        print("오류: 지원하지 않는 경로입니다.")
        sys.exit(1)

    out_dir.mkdir(parents=True, exist_ok=True)

    print(f"프레임 로드 중... ({len(files)}개)")
    frames = load_frames(files)

    print("정제 중...")
    cleaned = apply_clean(frames, ALPHA_THRESHOLD, PIXEL_SIZE)

    for f, arr in zip(files, cleaned):
        out_path = out_dir / f"{f.stem}.png"
        Image.fromarray(arr, mode="RGBA").save(out_path, compress_level=1)
        print(f"저장: {out_path}")

    print(f"\n완료 → {out_dir}")


if __name__ == "__main__":
    main()
