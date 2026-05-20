from pathlib import Path
import argparse
import importlib.util
import sys


# =========================================================
# TUNING VALUES
# =========================================================

# 사진/일러스트용 기본 AI 업스케일 배율
DEFAULT_SCALE = 4.0

# Real-ESRGAN 기본 모델. 사진에는 x4plus가 가장 무난함.
DEFAULT_MODEL = "RealESRGAN_x4plus"

# CUDA 메모리가 부족하면 256, 128처럼 낮춰서 실행.
DEFAULT_TILE = 0
DEFAULT_TILE_PAD = 10
DEFAULT_PRE_PAD = 0

# realesr-general-x4v3 모델에서만 사용. 1.0 = 강한 노이즈 제거.
DEFAULT_DENOISE_STRENGTH = 0.5

# 기본은 GPU 필수. CPU 실행은 --cpu를 명시해야 함.
REQUIRE_CUDA_BY_DEFAULT = True

IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff"}

MODEL_CHOICES = (
    "RealESRGAN_x4plus",
    "RealESRNet_x4plus",
    "RealESRGAN_x4plus_anime_6B",
    "RealESRGAN_x2plus",
    "realesr-animevideov3",
    "realesr-general-x4v3",
)


# =========================================================
# DEPENDENCIES
# =========================================================

def require_python_packages():
    missing = [
        name for name in ("torch", "cv2", "numpy", "basicsr", "realesrgan")
        if importlib.util.find_spec(name) is None
    ]
    if missing:
        print("오류: Real-ESRGAN 업스케일에 필요한 패키지가 없습니다.")
        print(f"누락 패키지: {', '.join(missing)}")
        print()
        print("설치 예시:")
        print("  pip install realesrgan basicsr opencv-python")
        print("  CUDA PyTorch는 GPU/CUDA 버전에 맞춰 https://pytorch.org/get-started/locally/ 에서 설치하세요.")
        sys.exit(1)

    try:
        import cv2
        import numpy as np
        import torch
        from basicsr.archs.rrdbnet_arch import RRDBNet
        from basicsr.utils.download_util import load_file_from_url
        from realesrgan import RealESRGANer
        from realesrgan.archs.srvgg_arch import SRVGGNetCompact
    except Exception as e:
        print("오류: Real-ESRGAN 관련 패키지를 불러오지 못했습니다.")
        print(f"원인: {e}")
        print()
        print("패키지 버전 충돌일 수 있습니다. CUDA PyTorch와 Real-ESRGAN 의존성을 다시 확인하세요.")
        sys.exit(1)

    return cv2, np, torch, RRDBNet, load_file_from_url, RealESRGANer, SRVGGNetCompact


def ensure_gpu(torch, allow_cpu: bool, gpu_id: int | None):
    if torch.cuda.is_available():
        if gpu_id is not None and gpu_id >= torch.cuda.device_count():
            print(f"오류: GPU {gpu_id}를 찾을 수 없습니다. 사용 가능 GPU 수: {torch.cuda.device_count()}")
            sys.exit(1)
        return

    if allow_cpu:
        print("경고: CUDA GPU를 찾지 못해 CPU로 실행합니다. 매우 느릴 수 있습니다.")
        return

    if REQUIRE_CUDA_BY_DEFAULT:
        print("오류: CUDA GPU를 찾지 못했습니다.")
        print("GPU 기반 업스케일을 기본 요구사항으로 사용합니다.")
        print("CUDA PyTorch를 설치하거나, 느린 CPU 실행을 허용하려면 --cpu를 붙이세요.")
        sys.exit(1)


# =========================================================
# MODEL
# =========================================================

def build_model(model_name: str, RRDBNet, SRVGGNetCompact):
    model_name = model_name.split(".")[0]

    if model_name == "RealESRGAN_x4plus":
        model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=23, num_grow_ch=32, scale=4)
        netscale = 4
        urls = ["https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth"]
    elif model_name == "RealESRNet_x4plus":
        model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=23, num_grow_ch=32, scale=4)
        netscale = 4
        urls = ["https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.1/RealESRNet_x4plus.pth"]
    elif model_name == "RealESRGAN_x4plus_anime_6B":
        model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=6, num_grow_ch=32, scale=4)
        netscale = 4
        urls = ["https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.2.4/RealESRGAN_x4plus_anime_6B.pth"]
    elif model_name == "RealESRGAN_x2plus":
        model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=23, num_grow_ch=32, scale=2)
        netscale = 2
        urls = ["https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.1/RealESRGAN_x2plus.pth"]
    elif model_name == "realesr-animevideov3":
        model = SRVGGNetCompact(num_in_ch=3, num_out_ch=3, num_feat=64, num_conv=16, upscale=4, act_type="prelu")
        netscale = 4
        urls = ["https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesr-animevideov3.pth"]
    elif model_name == "realesr-general-x4v3":
        model = SRVGGNetCompact(num_in_ch=3, num_out_ch=3, num_feat=64, num_conv=32, upscale=4, act_type="prelu")
        netscale = 4
        urls = [
            "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesr-general-wdn-x4v3.pth",
            "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesr-general-x4v3.pth",
        ]
    else:
        print(f"오류: 지원하지 않는 모델입니다: {model_name}")
        sys.exit(1)

    return model_name, model, netscale, urls


def resolve_model_path(model_name: str, urls: list[str], model_path: str | None, load_file_from_url):
    if model_path:
        path = Path(model_path).resolve()
        if not path.is_file():
            print(f"오류: 모델 파일이 없습니다: {path}")
            sys.exit(1)
        return str(path)

    weights_dir = Path(__file__).resolve().parent / "weights"
    path = weights_dir / f"{model_name}.pth"
    if path.is_file():
        return str(path)

    try:
        downloaded_path = None
        for url in urls:
            downloaded_path = load_file_from_url(url=url, model_dir=str(weights_dir), progress=True, file_name=None)
        return downloaded_path
    except Exception as e:
        print("오류: Real-ESRGAN 모델 가중치를 다운로드하지 못했습니다.")
        print(f"원인: {e}")
        print(f"수동으로 weights 폴더에 {model_name}.pth 파일을 넣거나 --model-path를 지정하세요.")
        sys.exit(1)


def create_upsampler(args, RRDBNet, load_file_from_url, RealESRGANer, SRVGGNetCompact):
    model_name, model, netscale, urls = build_model(args.model, RRDBNet, SRVGGNetCompact)
    model_path = resolve_model_path(model_name, urls, args.model_path, load_file_from_url)

    dni_weight = None
    if model_name == "realesr-general-x4v3" and args.denoise_strength != 1:
        wdn_model_path = model_path.replace("realesr-general-x4v3", "realesr-general-wdn-x4v3")
        model_path = [model_path, wdn_model_path]
        dni_weight = [args.denoise_strength, 1 - args.denoise_strength]

    return RealESRGANer(
        scale=netscale,
        model_path=model_path,
        dni_weight=dni_weight,
        model=model,
        tile=args.tile,
        tile_pad=args.tile_pad,
        pre_pad=args.pre_pad,
        half=not args.fp32 and not args.cpu,
        gpu_id=args.gpu_id,
    )


# =========================================================
# IO
# =========================================================

def numeric_sort_key(path: Path):
    try:
        return (0, int(path.stem), path.name)
    except ValueError:
        return (1, path.name.lower(), path.name)


def collect_input_files(target: Path) -> list[Path]:
    if target.is_file():
        if target.suffix.lower() not in IMAGE_EXTS:
            print(f"오류: 지원하지 않는 이미지 형식입니다: {target.suffix}")
            sys.exit(1)
        return [target]

    if target.is_dir():
        files = sorted(
            (p for p in target.iterdir() if p.is_file() and p.suffix.lower() in IMAGE_EXTS),
            key=numeric_sort_key,
        )
        if not files:
            print("오류: 폴더 안에 이미지가 없습니다.")
            sys.exit(1)
        return files

    print("오류: 지원하지 않는 경로입니다.")
    sys.exit(1)


def output_dir_for(target: Path, scale: float) -> Path:
    scale_label = str(int(scale)) if scale.is_integer() else str(scale).replace(".", "_")
    if target.is_file():
        return target.parent / f"{target.stem}_x{scale_label}"
    return target.parent / f"{target.name}_x{scale_label}"


def output_extension(input_path: Path, output, ext: str) -> str:
    if ext == "auto":
        chosen = input_path.suffix.lower().lstrip(".")
    else:
        chosen = ext

    if getattr(output, "ndim", 0) == 3 and output.shape[2] == 4:
        return "png"
    return "jpg" if chosen == "jpeg" else chosen


def read_image(path: Path, cv2, np):
    data = cv2.imdecode(
        buf=np.fromfile(str(path), dtype=np.uint8),
        flags=cv2.IMREAD_UNCHANGED,
    )
    if data is None:
        print(f"오류: 이미지를 읽지 못했습니다: {path}")
        sys.exit(1)
    return data


def save_image(path: Path, image, cv2):
    path.parent.mkdir(parents=True, exist_ok=True)
    ok, encoded = cv2.imencode(path.suffix, image)
    if not ok:
        raise RuntimeError(f"이미지 인코딩 실패: {path.suffix}")
    encoded.tofile(str(path))


# =========================================================
# CLI
# =========================================================

def parse_args():
    parser = argparse.ArgumentParser(
        description="Real-ESRGAN을 사용해 사진/일러스트를 GPU 기반으로 업스케일합니다.",
    )
    parser.add_argument("target", help="이미지 파일 또는 이미지 폴더")
    parser.add_argument("positional_scale", nargs="?", type=float, help="업스케일 배율. 예: 2, 3, 4")
    parser.add_argument("-s", "--scale", type=float, default=None, help="업스케일 배율. 위치 인자보다 우선합니다.")
    parser.add_argument("-n", "--model", default=DEFAULT_MODEL, choices=MODEL_CHOICES, help="Real-ESRGAN 모델")
    parser.add_argument("--model-path", default=None, help="직접 사용할 .pth 모델 파일 경로")
    parser.add_argument("-t", "--tile", type=int, default=DEFAULT_TILE, help="타일 크기. 0은 타일링 없음")
    parser.add_argument("--tile-pad", type=int, default=DEFAULT_TILE_PAD, help="타일 패딩")
    parser.add_argument("--pre-pad", type=int, default=DEFAULT_PRE_PAD, help="입력 가장자리 패딩")
    parser.add_argument("--denoise-strength", type=float, default=DEFAULT_DENOISE_STRENGTH, help="realesr-general-x4v3 전용 노이즈 제거 강도")
    parser.add_argument("--fp32", action="store_true", help="fp16 대신 fp32로 실행")
    parser.add_argument("--cpu", action="store_true", help="CUDA가 없어도 CPU 실행 허용")
    parser.add_argument("-g", "--gpu-id", type=int, default=None, help="사용할 GPU 번호")
    parser.add_argument("--ext", choices=("auto", "png", "jpg", "jpeg", "webp"), default="png", help="출력 확장자")
    return parser.parse_args()


def main():
    args = parse_args()
    args.scale = args.scale if args.scale is not None else args.positional_scale
    if args.scale is None:
        args.scale = DEFAULT_SCALE
    if args.scale <= 0:
        print("오류: 배율은 0보다 커야 합니다.")
        sys.exit(1)

    target = Path(args.target).resolve()
    if not target.exists():
        print(f"오류: 경로가 없습니다: {target}")
        sys.exit(1)

    cv2, np, torch, RRDBNet, load_file_from_url, RealESRGANer, SRVGGNetCompact = require_python_packages()
    ensure_gpu(torch, args.cpu, args.gpu_id)

    files = collect_input_files(target)
    out_dir = output_dir_for(target, args.scale)
    upsampler = create_upsampler(args, RRDBNet, load_file_from_url, RealESRGANer, SRVGGNetCompact)

    failed = 0
    for input_path in files:
        image = read_image(input_path, cv2, np)
        ext = output_extension(input_path, image, args.ext)
        output_path = out_dir / f"{input_path.stem}.{ext}"

        try:
            output, _ = upsampler.enhance(image, outscale=args.scale)
            save_image(output_path, output, cv2)
            in_h, in_w = image.shape[:2]
            out_h, out_w = output.shape[:2]
            print(f"Saved: {output_path}  ({in_w}x{in_h} -> {out_w}x{out_h})")
        except RuntimeError as e:
            failed += 1
            print(f"실패: {input_path.name} -> {e}")
            print("CUDA 메모리 부족이면 --tile 256 또는 --tile 128처럼 낮춰 다시 실행하세요.")
        except Exception as e:
            failed += 1
            print(f"실패: {input_path.name} -> {e}")

    if failed:
        print(f"완료: 성공 {len(files) - failed}개, 실패 {failed}개")
        sys.exit(1)

    print(f"완료: {len(files)}개 이미지 업스케일")


if __name__ == "__main__":
    main()
