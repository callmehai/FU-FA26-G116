#!/usr/bin/env bash
# Export one class diagram to PNG with draw.io CLI. The CLI draws the content at 0.66 when the image
# would be wider than about 2900 px, so the scale is lowered until the width fits.
# Requirements: bash, draw.io desktop CLI (default /c/Program Files/draw.io/draw.io.exe on Windows /
# Git Bash; override with DRAWIO=/path/to/drawio), Python 3 with Pillow for the size check.
# Usage: scripts/export_class_diagram_png.sh docs/07-classdiagram/feature-09-....drawio [output.png]
set -euo pipefail
f="$1"; out="${2:-$1.png}"
w=$(grep -o 'pageWidth="[0-9]*"' "$f" | head -1 | grep -o '[0-9]*')
scale=$(python -c "print(round(min(1.2, 2800/$w), 2))")
"${DRAWIO:-/c/Program Files/draw.io/draw.io.exe}" --export --format png --scale "$scale" --disable-gpu --disable-software-rasterizer --output "$out" "$f" >/dev/null 2>&1
python - "$out" <<'P'
import sys
from PIL import Image
im = Image.open(sys.argv[1]); b = im.convert("L").point(lambda p: 255 if p < 230 else 0).getbbox()
print(sys.argv[1].split("/")[-1], im.size, "drawn", b, "OK" if b[2] > im.size[0] * 0.95 else "SHRUNK")
P
