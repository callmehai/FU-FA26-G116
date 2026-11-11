#!/usr/bin/env python3
"""Một lệnh xuất đủ HAI bộ ảnh cho mỗi sơ đồ tuần tự .txt được truyền vào:

  1. tab trong TrustCabinet_Sequence_Diagrams.drawio + docs/06-sequencediagram/drawio-png/<page_id>.drawio.png
     (scripts/txt_to_sequence_drawio_png.py — chụp bằng Edge/Chrome, tự chọn scale để không cắt ảnh)
  2. ảnh sequencediagram.org nằm cạnh file .txt
     (scripts/export-diagrams.js — Playwright)

Chỉ xuất đúng file được truyền; KHÔNG nhận thư mục (xuất cả thư mục làm đổi ảnh của sơ đồ không sửa).
Mỗi tham số là đường dẫn .txt hoặc mã UC (vd. UC84b, uc-84b) — mã UC khớp tên file UC84b_*.txt.

Ví dụ:
  python scripts/export_sequence.py UC84b UC84f
  python scripts/export_sequence.py docs/06-sequencediagram/role-management/UC11_ConfigureRolePermissions.txt
  python scripts/export_sequence.py UC84b --only drawio     # chỉ bộ 1
  python scripts/export_sequence.py UC84b --only seqorg     # chỉ bộ 2
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SEQ_DIR = ROOT / "docs" / "06-sequencediagram"
SCRIPTS = ROOT / "scripts"


def resolve(arg: str) -> Path:
    path = Path(arg)
    if path.suffix.lower() == ".txt":
        path = path if path.is_absolute() else (ROOT / path if (ROOT / path).exists() else path)
        if not path.is_file():
            raise SystemExit(f"lỗi: không thấy file {arg}")
        return path.resolve()
    if path.is_dir() or Path(arg).is_dir():
        raise SystemExit(f"lỗi: {arg} là thư mục — truyền từng file .txt hoặc mã UC")
    code = re.sub(r"[^0-9a-z]", "", arg.lower().removeprefix("uc").removeprefix("-"))
    matches = [
        p for p in SEQ_DIR.rglob("*.txt")
        if re.sub(r"[^0-9a-z]", "", p.stem.split("_")[0].lower().removeprefix("uc")) == code
    ]
    if not matches:
        raise SystemExit(f"lỗi: không có file .txt cho mã UC '{arg}'")
    if len(matches) > 1:
        names = ", ".join(str(m.relative_to(ROOT)) for m in matches)
        raise SystemExit(f"lỗi: mã '{arg}' khớp nhiều file: {names}")
    return matches[0].resolve()


def run(cmd: list[str]) -> None:
    print("$", " ".join(cmd), flush=True)
    if subprocess.run(cmd, cwd=ROOT).returncode != 0:
        raise SystemExit(f"lỗi: lệnh thất bại: {' '.join(cmd)}")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("targets", nargs="+", help="đường dẫn .txt hoặc mã UC (UC84b)")
    parser.add_argument("--only", choices=["drawio", "seqorg"], help="chỉ xuất một bộ ảnh")
    parser.add_argument("--scale", type=float, help="ép scale drawio.png (mặc định tự chọn)")
    args = parser.parse_args()

    files = []
    for target in args.targets:
        path = resolve(target)
        if path not in files:
            files.append(path)
    rel = [str(p) for p in files]

    if args.only != "seqorg":
        cmd = [sys.executable, "-X", "utf8", str(SCRIPTS / "txt_to_sequence_drawio_png.py"), *rel]
        if args.scale:
            cmd += ["--scale", str(args.scale)]
        run(cmd)
    if args.only != "drawio":
        run(["node", str(SCRIPTS / "export-diagrams.js"), *rel])

    print("\nxong — kiểm tra bằng: git status --short docs/06-sequencediagram", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
