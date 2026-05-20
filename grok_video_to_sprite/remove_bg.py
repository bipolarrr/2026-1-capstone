from collections import deque
from pathlib import Path
import sys

import numpy as np
from PIL import Image
from rembg import remove, new_session
from scipy.ndimage import binary_erosion, binary_fill_holes, label


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

# 폴더 처리 시 남은 프레임 순서대로 0.png부터 다시 저장할지
RENUMBER_BATCH_OUTPUT = True

# rembg 결과에서 내부 구멍(눈 등)을 복원할지
FILL_INTERIOR_HOLES = True

# pure green 배경 영상은 rembg보다 초록 픽셀을 먼저 제거
AUTO_GREEN_SCREEN = True
GREEN_SCREEN_MIN_EDGE_RATIO = 0.20
GREEN_MIN_G = 70
GREEN_DOMINANCE = 25

# 이진화 임계값: rembg의 스무드 알파를 0/255로 변환
# 이 값 이상이면 불투명(캐릭터), 미만이면 투명(배경)으로 판정
# 높은 값은 얇은 선/어두운 외곽선을 프레임별로 잃기 쉬워 낮게 둔다.
ALPHA_BINARY_THRESHOLD = 32

# 마스크 침식 횟수: 배경색이 섞인 테두리 픽셀을 제거
# 논리 픽셀이 2px이므로 1px 침식 시 반쪽 픽셀이 생김 → 0 권장
# fringe는 배경색 flood fill과 큰 구멍 필터로 제거함
MASK_ERODE_ITERATIONS = 0

# 내부 구멍 복원용 밀봉 임계값: rembg alpha가 이 값 이상이면 밀봉 마스크에 포함
# 매우 낮은 값으로 설정해 rembg가 살짝이라도 알파를 준 얇은 부위를 보존한다.
SEAL_THRESHOLD = 5

# 내부 구멍으로 복원할 최대 면적.
MAX_INTERIOR_HOLE_AREA = 12000

# 배경색과 비슷한 큰 내부 빈 공간을 다시 투명화할지.
# 캐릭터 얼굴/몸 색이 배경과 가까운 영상에서는 주요 부위가 날아갈 수 있어 기본 비활성화한다.
REMOVE_LARGE_INTERIOR_BACKGROUND_HOLES = False

# 원본 가장자리에서 균일 배경색을 찾고, 바깥과 연결된 배경만 제거한다.
# 검은 배경 도트 스프라이트에서 rembg가 놓친 뼈/활 디테일을 보존하는 보조 마스크다.
USE_EXTERIOR_BACKGROUND_MASK = True
BACKGROUND_EDGE_BIN_SIZE = 16
BACKGROUND_MIN_EDGE_RATIO = 0.18
BACKGROUND_COLOR_TOLERANCE = 28
DARK_BACKGROUND_COLOR_TOLERANCE = 12
LARGE_HOLE_BACKGROUND_TOLERANCE = 38
MAX_BACKGROUND_COLOR_RESCUE_AREA = 8000


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


def path_sort_key(path: Path) -> tuple[int, int | str]:
    """숫자 파일명은 애니메이션 순서대로 정렬한다."""
    return (0, int(path.stem)) if path.stem.isdigit() else (1, path.name)


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


def flood_fill_background(can_pass: np.ndarray) -> np.ndarray:
    """가장자리에서 can_pass 픽셀을 따라 도달 가능한 영역을 반환한다."""
    h, w = can_pass.shape
    reachable = np.zeros((h, w), dtype=bool)
    queue = deque()

    for x in range(w):
        if can_pass[0, x]:
            queue.append((0, x))
        if can_pass[h - 1, x]:
            queue.append((h - 1, x))
    for y in range(1, h - 1):
        if can_pass[y, 0]:
            queue.append((y, 0))
        if can_pass[y, w - 1]:
            queue.append((y, w - 1))

    while queue:
        y, x = queue.popleft()
        if reachable[y, x]:
            continue
        reachable[y, x] = True
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and can_pass[ny, nx] and not reachable[ny, nx]:
                queue.append((ny, nx))

    return reachable


def dominant_edge_color(rgb: np.ndarray) -> tuple[np.ndarray, float] | None:
    """이미지 가장자리의 대표 배경색과 해당 비율을 구한다."""
    edges = np.concatenate((rgb[0, :, :], rgb[-1, :, :], rgb[:, 0, :], rgb[:, -1, :]), axis=0)
    bins = edges // BACKGROUND_EDGE_BIN_SIZE
    packed = (
        bins[:, 0].astype(np.int32) << 16
        | bins[:, 1].astype(np.int32) << 8
        | bins[:, 2].astype(np.int32)
    )
    values, counts = np.unique(packed, return_counts=True)
    if len(values) == 0:
        return None

    best = int(np.argmax(counts))
    ratio = float(counts[best] / len(edges))
    if ratio < BACKGROUND_MIN_EDGE_RATIO:
        return None

    members = edges[packed == values[best]]
    return np.median(members, axis=0).astype(np.int16), ratio


def source_background_masks(original: Image.Image) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray] | None:
    """원본의 배경색 후보와 외부 배경 영역을 찾는다. 실패하면 None."""
    if not USE_EXTERIOR_BACKGROUND_MASK:
        return None

    rgb = np.array(original.convert("RGBA"))[:, :, :3]
    detected = dominant_edge_color(rgb)
    if detected is None:
        return None

    bg_color, _ = detected
    tolerance = DARK_BACKGROUND_COLOR_TOLERANCE if int(bg_color.max()) < 64 else BACKGROUND_COLOR_TOLERANCE
    diff = np.abs(rgb.astype(np.int16) - bg_color)
    bg_candidate = np.max(diff, axis=2) <= tolerance
    loose_tolerance = max(tolerance, LARGE_HOLE_BACKGROUND_TOLERANCE)
    loose_bg_candidate = np.max(diff, axis=2) <= loose_tolerance
    return (
        bg_candidate,
        flood_fill_background(bg_candidate),
        loose_bg_candidate,
        flood_fill_background(loose_bg_candidate),
    )


def small_interior_holes(mask: np.ndarray, max_area: int) -> np.ndarray:
    """mask 내부의 작은 구멍만 반환한다."""
    filled = binary_fill_holes(mask)
    holes = filled & ~mask
    if not holes.any():
        return holes

    labels, count = label(holes)
    keep = np.zeros_like(holes)
    for idx in range(1, count + 1):
        component = labels == idx
        if int(component.sum()) <= max_area:
            keep |= component
    return keep


def large_interior_holes(mask: np.ndarray, max_area: int) -> np.ndarray:
    """mask 내부의 큰 구멍만 반환한다."""
    filled = binary_fill_holes(mask)
    holes = filled & ~mask
    if not holes.any():
        return holes

    labels, count = label(holes)
    remove = np.zeros_like(holes)
    for idx in range(1, count + 1):
        component = labels == idx
        if int(component.sum()) > max_area:
            remove |= component
    return remove


def small_components(mask: np.ndarray, max_area: int) -> np.ndarray:
    """작은 연결 컴포넌트만 반환한다."""
    labels, count = label(mask)
    keep = np.zeros_like(mask)
    for idx in range(1, count + 1):
        component = labels == idx
        if int(component.sum()) <= max_area:
            keep |= component
    return keep


def make_binary_mask(original: Image.Image, rembg_img: Image.Image) -> np.ndarray:
    """
    rembg 출력을 도트 아트에 맞게 이진 마스크로 변환한다.

    복원 방식:
    1. 낮은 임계값(SEAL_THRESHOLD)으로 rembg가 희미하게 잡은 얇은 선을 보존
    2. 원본의 균일 외부 배경을 flood fill로 제거해 rembg가 놓친 색상 디테일 보존
    3. 작은 내부 구멍만 복원해 눈/입 구멍은 살리고 큰 외부 빈 공간은 채우지 않음

    반환: bool 마스크 (True = 캐릭터, False = 배경)
    """
    alpha = np.array(rembg_img)[:, :, 3]
    strong_rembg = alpha >= ALPHA_BINARY_THRESHOLD
    soft_rembg = alpha >= SEAL_THRESHOLD

    source_masks = source_background_masks(original)
    if source_masks is not None:
        _, exterior_bg, loose_bg_candidate, loose_exterior_bg = source_masks
        source_foreground = ~exterior_bg
        large_source_holes = np.zeros_like(source_foreground)
        if FILL_INTERIOR_HOLES:
            source_foreground |= small_interior_holes(source_foreground, MAX_INTERIOR_HOLE_AREA)
            if REMOVE_LARGE_INTERIOR_BACKGROUND_HOLES:
                large_source_holes = large_interior_holes(~loose_bg_candidate, MAX_INTERIOR_HOLE_AREA)

        # 외부 배경과 연결되지 않은 원본 색상 디테일을 우선 보존한다.
        # rembg가 확실히 잡은 얇은 검은 선은 작은 컴포넌트일 때만 배경색이어도 살린다.
        strong_color = strong_rembg & ~loose_bg_candidate
        strong_bg_rescue = small_components(strong_rembg & loose_bg_candidate, MAX_BACKGROUND_COLOR_RESCUE_AREA)
        soft_rescue = soft_rembg & ~loose_exterior_bg
        is_opaque = source_foreground | strong_color | strong_bg_rescue | soft_rescue
        is_opaque &= ~large_source_holes
    else:
        is_opaque = soft_rembg

        if FILL_INTERIOR_HOLES:
            is_opaque |= small_interior_holes(soft_rembg, MAX_INTERIOR_HOLE_AREA)

    if MASK_ERODE_ITERATIONS > 0:
        is_opaque = binary_erosion(is_opaque, iterations=MASK_ERODE_ITERATIONS)

    return is_opaque


def green_screen_candidate(rgb: np.ndarray) -> np.ndarray:
    """압축 노이즈와 어두운 초록 그라데이션을 포함해 초록 배경 후보를 찾는다."""
    arr = rgb.astype(np.int16)
    r = arr[:, :, 0]
    g = arr[:, :, 1]
    b = arr[:, :, 2]
    return (g >= GREEN_MIN_G) & ((g - r) >= GREEN_DOMINANCE) & ((g - b) >= GREEN_DOMINANCE)


def edge_ratio(mask: np.ndarray) -> float:
    edge = np.concatenate((mask[0, :], mask[-1, :], mask[:, 0], mask[:, -1]))
    return float(edge.mean())


def remove_green_screen(img: Image.Image) -> Image.Image | None:
    """pure green 배경이면 초록 영역을 투명화한다."""
    arr = np.array(img.convert("RGBA"))
    green = green_screen_candidate(arr[:, :, :3])
    if edge_ratio(green) < GREEN_SCREEN_MIN_EDGE_RATIO:
        return None

    background = green
    arr[background, 3] = 0
    arr[~background, 3] = 255
    return Image.fromarray(arr, mode="RGBA")


def apply_mask(original: Image.Image, mask: np.ndarray) -> Image.Image:
    """원본 RGB에 이진 마스크를 적용하여 RGBA 이미지를 생성한다."""
    arr = np.array(original.convert("RGBA"))
    arr[mask, 3] = 255
    arr[~mask, 3] = 0
    return Image.fromarray(arr, mode="RGBA")


def remove_background(img: Image.Image) -> Image.Image:
    if AUTO_GREEN_SCREEN:
        green_result = remove_green_screen(img)
        if green_result is not None:
            return green_result

    rembg_out = remove(img, session=SESSION)
    mask = make_binary_mask(img, rembg_out)
    return apply_mask(img, mask)


def process_single(input_path: Path, out_dir: Path):
    img = Image.open(input_path).convert("RGBA")
    result = remove_background(img)

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
            result = remove_background(img)
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
    for idx, (f, img) in enumerate(results):
        if shared is not None:
            img = crop_to_bounds(img, shared, CROP_PADDING)
        out_name = f"{idx}.png" if RENUMBER_BATCH_OUTPUT else f"{f.stem}.png"
        out_path = out_dir / out_name
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
        files = sorted((p for p in target.iterdir() if p.suffix.lower() in exts), key=path_sort_key)
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
