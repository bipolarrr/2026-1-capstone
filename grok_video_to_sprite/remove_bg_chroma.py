"""
remove_bg_chroma.py — flood fill 기반 배경 제거

rembg 대신 사용. 체커보드/단색 등 고정 배경에 최적.
모서리에서 시작해 색이 비슷한 픽셀을 배경으로 판단 → 투명 처리.
경계를 보간하지 않으므로 도트 그래픽의 선명한 경계가 그대로 유지됨.
"""

from pathlib import Path
from collections import deque
import sys

import numpy as np
from PIL import Image


# =========================================================
# TUNING VALUES
# =========================================================

# 배경 판단 채도 임계값: max(R,G,B) - min(R,G,B) 이하면 무채색으로 간주
# 배경(회색계열) = 0~5, 캐릭터(웜톤) = 30~60 → 15~20 사이면 안전
ACHROMATIC_THRESHOLD = 18

# flood fill 시작 위치: 네 모서리에서 이 픽셀 수만큼 안쪽까지 시드로 사용
SEED_MARGIN = 3


# =========================================================
# CORE
# =========================================================

def is_achromatic(pixel: np.ndarray) -> bool:
    """R≈G≈B인 무채색(회색계열)이면 True"""
    return int(pixel.max()) - int(pixel.min()) < ACHROMATIC_THRESHOLD


def flood_fill_background(rgb: np.ndarray) -> np.ndarray:
    """
    네 모서리 근처 픽셀을 시드로 삼아 flood fill.
    무채색(R≈G≈B)이고 외곽과 연결된 픽셀을 배경으로 판단.
    배경으로 판단된 픽셀은 True인 bool 마스크를 반환.
    """
    h, w = rgb.shape[:2]
    visited = np.zeros((h, w), dtype=bool)
    is_bg = np.zeros((h, w), dtype=bool)

    queue = deque()

    # 네 모서리 시드 수집
    for y in range(SEED_MARGIN):
        for x in range(w):
            queue.append((y, x))
            queue.append((h - 1 - y, x))
    for x in range(SEED_MARGIN):
        for y in range(h):
            queue.append((y, x))
            queue.append((y, w - 1 - x))

    while queue:
        y, x = queue.popleft()
        if visited[y, x]:
            continue
        visited[y, x] = True

        if is_achromatic(rgb[y, x]):
            is_bg[y, x] = True
            for dy, dx in ((-1,0),(1,0),(0,-1),(0,1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx]:
                    queue.append((ny, nx))

    return is_bg


def remove_background(input_path: Path, output_path: Path):
    img = Image.open(input_path).convert("RGBA")
    arr = np.array(img)
    rgb = arr[:, :, :3]

    is_bg = flood_fill_background(rgb)

    result = arr.copy()
    result[is_bg] = [0, 0, 0, 0]

    output_path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result, mode="RGBA").save(output_path)
    print(f"저장: {output_path}")


# =========================================================
# MAIN
# =========================================================

def main():
    if len(sys.argv) != 2:
        print("사용법: python remove_bg_chroma.py <이미지 파일 또는 폴더>")
        sys.exit(1)

    target = Path(sys.argv[1]).resolve()

    if not target.exists():
        print(f"오류: 경로가 없습니다: {target}")
        sys.exit(1)

    exts = {".png", ".jpg", ".jpeg", ".bmp", ".webp"}

    if target.is_file():
        out_dir = target.parent / f"{target.stem}_transparent"
        remove_background(target, out_dir / f"{target.stem}.png")

    elif target.is_dir():
        out_dir = target.parent / f"{target.name}_transparent"
        out_dir.mkdir(exist_ok=True)
        files = sorted(p for p in target.iterdir() if p.suffix.lower() in exts)
        if not files:
            print("오류: 폴더 안에 이미지가 없습니다.")
            sys.exit(1)
        for f in files:
            try:
                remove_background(f, out_dir / f"{f.stem}.png")
            except Exception as e:
                print(f"실패: {f.name} → {e}")
    else:
        print("오류: 지원하지 않는 경로입니다.")
        sys.exit(1)


if __name__ == "__main__":
    main()
