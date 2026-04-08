import os
import sys
import subprocess
from pathlib import Path


def main():
    if len(sys.argv) != 2:
        print("사용법: python extract_frames.py <video_file>")
        sys.exit(1)

    video_path = Path(sys.argv[1]).resolve()

    if not video_path.is_file():
        print(f"오류: 파일을 찾을 수 없습니다: {video_path}")
        sys.exit(1)

    # 출력 폴더명: 동영상 파일명과 동일(확장자 제외)
    output_dir = video_path.parent / video_path.stem
    output_dir.mkdir(exist_ok=True)

    # ffmpeg 존재 확인
    try:
        subprocess.run(
            ["ffmpeg", "-version"],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            check=True
        )
    except Exception:
        print("오류: ffmpeg가 설치되어 있지 않거나 PATH에 등록되지 않았습니다.")
        print("Windows 예시:")
        print("  winget install Gyan.FFmpeg")
        print("또는")
        print("  scoop install ffmpeg")
        sys.exit(1)

    # PNG로 원본 프레임 전부 추출
    # -vsync 0 : 원본 프레임 그대로 뽑는 데 유리
    # start_number=0 : 0.png부터 시작
    # 압축레벨을 낮춰 저장 속도↑, 화질은 PNG라 동일
    output_pattern = str(output_dir / "%d.png")

    cmd = [
        "ffmpeg",
        "-i", str(video_path),
        "-vsync", "0",
        "-start_number", "0",
        "-compression_level", "1",
        output_pattern
    ]

    print(f"입력 파일: {video_path}")
    print(f"출력 폴더: {output_dir}")
    print("프레임 추출 중...")

    result = subprocess.run(cmd)

    if result.returncode != 0:
        print("오류: ffmpeg 프레임 추출에 실패했습니다.")
        sys.exit(result.returncode)

    print("완료.")
    print(f"저장 위치: {output_dir}")


if __name__ == "__main__":
    main()