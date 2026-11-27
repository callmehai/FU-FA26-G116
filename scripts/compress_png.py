#!/usr/bin/env python3
"""Nén PNG sơ đồ về bản nhẹ để đưa vào repo (giữ chữ sắc nét).

Luồng chuẩn cho mọi script xuất ảnh (export_sequence, sync_sequence_drawio_png):
  1. xuất ảnh gốc đầy đủ độ phân giải;
  2. lưu một bản gốc vào thư mục local (mặc định `anh-goc/`, đã gitignore)
     để sau này dựng báo cáo chuẩn nộp — thư mục này KHÔNG vào git;
  3. nén bản trong repo: cạnh dài tối đa 1920 px (LANCZOS) + palette
     256 màu (MEDIANCUT) + optimize; chỉ ghi đè khi file mới nhỏ hơn.

Chạy lẻ: python scripts/compress_png.py <file.png> [...]
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image

MAX_WIDTH = 1920
PALETTE_COLORS = 256


def compress_bytes(raw: bytes, max_width: int = MAX_WIDTH) -> bytes:
    """Trả về bản nén của ảnh PNG; ném RuntimeError nếu đầu vào không mở được."""
    from io import BytesIO

    im = Image.open(BytesIO(raw))
    im.load()
    w, h = im.size
    work = im.resize((max_width, round(h * max_width / w)), Image.LANCZOS) if w > max_width else im
    rgb = work.convert("RGB")
    pal = rgb.quantize(colors=PALETTE_COLORS, method=Image.MEDIANCUT)
    buf = BytesIO()
    pal.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def compress_file(path: Path, max_width: int = MAX_WIDTH) -> tuple[int, int, bool]:
    """Nén một file PNG tại chỗ; trả (bytes cũ, bytes mới, có ghi đè không)."""
    raw = path.read_bytes()
    try:
        new = compress_bytes(raw, max_width)
    except Exception as exc:  # noqa: BLE001 — file hỏng thì bỏ qua, gọi lại sau khi sửa nguồn
        raise RuntimeError(f"không mở được {path.name}: {exc}") from exc
    if len(new) < len(raw):
        path.write_bytes(new)
        return len(raw), len(new), True
    return len(raw), len(raw), False


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Nén PNG sơ đồ về bản nhẹ (palette 256 màu).")
    parser.add_argument("files", nargs="+", help="File PNG cần nén tại chỗ")
    parser.add_argument("--max-width", type=int, default=MAX_WIDTH, help="cạnh rộng tối đa (mặc định 1920)")
    args = parser.parse_args(argv)
    failed = 0
    for name in args.files:
        path = Path(name)
        try:
            old, new, changed = compress_file(path, args.max_width)
            print(f"{'GHI-DE' if changed else 'GIU-nguyen'} {path} ({old/1e3:.0f} -> {new/1e3:.0f} KB)")
        except (OSError, RuntimeError) as exc:
            print(f"loi: {exc}", file=sys.stderr)
            failed += 1
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
