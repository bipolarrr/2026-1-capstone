from pathlib import Path
import sys

from PIL import Image


# =========================================================
# TUNING VALUES
# =========================================================

# 업스케일 배율 (정수 권장: 2, 3, 4)
SCALE = 2

# 리샘플링 필터
# NEAREST  : 도트 그래픽 전용 — 픽셀을 그대로 N배 복사, 경계 번짐 없음 (기본값)
# LANCZOS  : 일반 사진/일러스트 — 부드럽게 보간, 도트엔 부적합
# BICUBIC  : 균형
# BILINEAR : 빠르지만 흐림
RESAMPLE = Image.NEAREST


# =========================================================
# CORE
# =========================================================

def upscale_image(input_path: Path, output_path: Path, scale: int = SCALE):
    img = Image.open(input_path)

    # 투명도 채널 보존
    if img.mode not in ("RGBA", "LA"):
        img = img.convert("RGBA")

    new_size = (img.width * scale, img.height * scale)
    upscaled = img.resize(new_size, resample=RESAMPLE)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    upscaled.save(output_path, optimize=False, compress_level=1)
    print(f"Saved: {output_path}  ({img.width}x{img.height} → {new_size[0]}x{new_size[1]})")


def main():
    args = sys.argv[1:]

    if not args or len(args) > 2:
        print("사용법:")
        print("  python upscale.py <이미지 파일 또는 폴더> [배율]")
        print("  python upscale.py idle_transparent 2")
        sys.exit(1)

    target = Path(args[0]).resolve()
    scale = int(args[1]) if len(args) == 2 else SCALE

    if not target.exists():
        print(f"오류: 경로가 없습니다: {target}")
        sys.exit(1)

    exts = {".png", ".jpg", ".jpeg", ".bmp", ".webp"}

    if target.is_file():
        out_dir = target.parent / f"{target.stem}_x{scale}"
        upscale_image(target, out_dir / f"{target.stem}.png", scale)

    elif target.is_dir():
        out_dir = target.parent / f"{target.name}_x{scale}"
        files = sorted(
            p for p in target.iterdir() if p.suffix.lower() in exts
        )
        if not files:
            print("오류: 폴더 안에 이미지가 없습니다.")
            sys.exit(1)

        for f in files:
            try:
                upscale_image(f, out_dir / f"{f.stem}.png", scale)
            except Exception as e:
                print(f"실패: {f.name} → {e}")
    else:
        print("오류: 지원하지 않는 경로입니다.")
        sys.exit(1)


if __name__ == "__main__":
    main()
