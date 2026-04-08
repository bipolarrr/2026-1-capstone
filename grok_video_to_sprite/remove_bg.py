from collections import deque
from pathlib import Path
import sys

import numpy as np
from PIL import Image
from rembg import remove, new_session
from scipy.ndimage import binary_erosion, binary_fill_holes


# =========================================================
# TUNING VALUES
# =========================================================

# 결과를 캐릭터 외곽 기준으로 자동 크롭할지
ENABLE_CROP = True

# 크롭 시 남길 여백 픽셀
CROP_PADDING = 2

# 크롭 영역을 이 크기의 격자에 스냅 (0 = 스냅 안 함)
# 블록 균일화 시 가장자리에 잘린 직사각형이 생기지 않도록 함
# idle처럼 정면 도트에 유용, 비정형 스프라이트는 0 권장
CROP_GRID_SNAP = 2

# rembg 결과에서 내부 구멍(눈 등)을 복원할지
FILL_INTERIOR_HOLES = True

# 이진화 임계값: rembg의 스무드 알파를 0/255로 변환
# 이 값 이상이면 불투명(캐릭터), 미만이면 투명(배경)으로 판정
# 도트 아트는 반투명 픽셀이 없어야 하므로 낮게 설정 (권장: 10~40)
ALPHA_BINARY_THRESHOLD = 140

# 마스크 침식 횟수: 배경색이 섞인 테두리 픽셀을 제거
# 논리 픽셀이 2px이므로 1px 침식 시 반쪽 픽셀이 생김 → 0 권장
# fringe는 ALPHA_BINARY_THRESHOLD=140으로 이미 제거됨
MASK_ERODE_ITERATIONS = 0

# 내부 구멍 복원용 밀봉 임계값: rembg alpha가 이 값 이상이면 밀봉 마스크에 포함
# 매우 낮은 값(1)으로 설정해 rembg가 살짝이라도 알파를 준 픽셀을 전부 포함시킴
# → 눈 흰자 주변 경로가 닫혀 binary_fill_holes로 내부 구멍 검출 가능
SEAL_THRESHOLD = 5


# =========================================================
# CORE
# =========================================================

SESSION = new_session()


def visible_bounds(img: Image.Image) -> tuple[int, int, int, int] | None:
    """알파 > 0 픽셀의 bounding box (x0, y0, x1, y1) 반환. 없으면 None."""
    alpha = np.array(img)[:, :, 3]
    ys, xs = np.where(alpha > 0)
    if len(xs) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def union_bounds(bounds_list: list[tuple[int, int, int, int]]) -> tuple[int, int, int, int]:
    """여러 bounding box의 합집합을 반환."""
    x0 = min(b[0] for b in bounds_list)
    y0 = min(b[1] for b in bounds_list)
    x1 = max(b[2] for b in bounds_list)
    y1 = max(b[3] for b in bounds_list)
    return x0, y0, x1, y1


def crop_to_bounds(img: Image.Image, bounds: tuple[int, int, int, int], padding: int) -> Image.Image:
    x0, y0, x1, y1 = bounds
    x0 = max(x0 - padding, 0)
    y0 = max(y0 - padding, 0)
    x1 = min(x1 + padding, img.width - 1)
    y1 = min(y1 + padding, img.height - 1)

    if CROP_GRID_SNAP > 0:
        gs = CROP_GRID_SNAP
        x0 = (x0 // gs) * gs
        y0 = (y0 // gs) * gs
        x1 = ((x1 // gs) + 1) * gs - 1
        y1 = ((y1 // gs) + 1) * gs - 1
        x1 = min(x1, img.width - 1)
        y1 = min(y1, img.height - 1)

    return img.crop((x0, y0, x1 + 1, y1 + 1))


def make_binary_mask(rembg_img: Image.Image) -> np.ndarray:
    """
    rembg 출력을 도트 아트에 맞게 이진 마스크로 변환한다.

    3단계 복원 방식:
    1. 낮은 임계값(SEAL_THRESHOLD)으로 밀봉 마스크 생성
    2. 밀봉 마스크에서 가장자리 flood fill로 배경 분리 → 캐릭터 내부 영역 확보
       (alpha 1~139인 다리 등 부위도 보존)
    3. binary_fill_holes로 alpha=0 내부 구멍(눈 흰자 등) 복원

    반환: bool 마스크 (True = 캐릭터, False = 배경)
    """
    alpha = np.array(rembg_img)[:, :, 3]
    h, w = alpha.shape

    # 1단계: 낮은 임계값으로 밀봉 마스크
    sealed = alpha >= SEAL_THRESHOLD

    # 2단계: 밀봉 마스크에서 가장자리 flood fill → 배경만 분리
    # alpha 1~139인 캐릭터 내부 픽셀은 sealed에 포함되어 flood fill이 통과 못 함
    is_bg = ~sealed
    reachable = np.zeros((h, w), dtype=bool)
    queue = deque()

    for x in range(w):
        if is_bg[0, x]:
            queue.append((0, x))
        if is_bg[h - 1, x]:
            queue.append((h - 1, x))
    for y in range(1, h - 1):
        if is_bg[y, 0]:
            queue.append((y, 0))
        if is_bg[y, w - 1]:
            queue.append((y, w - 1))

    while queue:
        y, x = queue.popleft()
        if reachable[y, x]:
            continue
        reachable[y, x] = True
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and is_bg[ny, nx] and not reachable[ny, nx]:
                queue.append((ny, nx))

    # flood fill이 도달하지 못한 곳 = 캐릭터 영역 (sealed 포함 + 내부 alpha=0)
    character_region = ~reachable

    # 3단계: 높은 임계값 마스크와 합산
    # character_region에서 alpha=0 내부 구멍도 이미 포함됨
    is_opaque = alpha >= ALPHA_BINARY_THRESHOLD

    if MASK_ERODE_ITERATIONS > 0:
        is_opaque = binary_erosion(is_opaque, iterations=MASK_ERODE_ITERATIONS)

    return is_opaque | character_region


def apply_mask(original: Image.Image, mask: np.ndarray) -> Image.Image:
    """원본 RGB에 이진 마스크를 적용하여 RGBA 이미지를 생성한다."""
    arr = np.array(original.convert("RGBA"))
    arr[mask, 3] = 255
    arr[~mask, 3] = 0
    return Image.fromarray(arr, mode="RGBA")


def process_single(input_path: Path, out_dir: Path):
    img = Image.open(input_path).convert("RGBA")
    rembg_out = remove(img, session=SESSION)

    if FILL_INTERIOR_HOLES:
        mask = make_binary_mask(rembg_out)
        result = apply_mask(img, mask)
    else:
        result = rembg_out

    if ENABLE_CROP:
        result = crop_to_bounds(result, visible_bounds(result) or (0, 0, result.width - 1, result.height - 1), CROP_PADDING)

    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"{input_path.stem}.png"
    result.save(out_path)
    print(f"저장: {out_path}")


def process_batch(files: list[Path], out_dir: Path):
    """
    1단계: 모든 프레임을 rembg 처리 후 메모리에 보관
    2단계: 전체 프레임의 bounding box 합집합 계산
    3단계: 공통 크롭 영역으로 모든 프레임을 동일한 크기로 저장
    """
    print(f"배경 제거 중... ({len(files)}개)")
    results: list[tuple[Path, Image.Image]] = []
    for f in files:
        try:
            img = Image.open(f).convert("RGBA")
            rembg_out = remove(img, session=SESSION)
            if FILL_INTERIOR_HOLES:
                mask = make_binary_mask(rembg_out)
                result = apply_mask(img, mask)
            else:
                result = rembg_out
            results.append((f, result))
            print(f"  처리: {f.name}")
        except Exception as e:
            print(f"  실패: {f.name} → {e}")

    if not results:
        return

    if ENABLE_CROP:
        # 전체 프레임 bounding box 합산
        all_bounds = [b for _, img in results if (b := visible_bounds(img)) is not None]
        if all_bounds:
            shared = union_bounds(all_bounds)
            print(f"공통 크롭 영역: x={shared[0]}~{shared[2]}, y={shared[1]}~{shared[3]}")
        else:
            shared = None
    else:
        shared = None

    out_dir.mkdir(parents=True, exist_ok=True)
    for f, img in results:
        if shared is not None:
            img = crop_to_bounds(img, shared, CROP_PADDING)
        out_path = out_dir / f"{f.stem}.png"
        img.save(out_path)
        print(f"저장: {out_path}")


def main():
    if len(sys.argv) != 2:
        print("사용법:")
        print('  python remove_bg.py "0.png"')
        print('  python remove_bg.py "frames_folder"')
        sys.exit(1)

    target = Path(sys.argv[1]).resolve()

    if not target.exists():
        print(f"오류: 경로가 없습니다: {target}")
        sys.exit(1)

    exts = {".png", ".jpg", ".jpeg", ".bmp", ".webp"}

    if target.is_file():
        out_dir = target.parent / f"{target.stem}_transparent"
        process_single(target, out_dir)

    elif target.is_dir():
        files = [p for p in sorted(target.iterdir()) if p.suffix.lower() in exts]
        if not files:
            print("오류: 폴더 안에 이미지가 없습니다.")
            sys.exit(1)
        out_dir = target.parent / f"{target.name}_transparent"
        process_batch(files, out_dir)

    else:
        print("오류: 지원하지 않는 경로입니다.")
        sys.exit(1)


if __name__ == "__main__":
    main()
