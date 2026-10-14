#!/usr/bin/env python3
"""Export each Use Case tab from the combined sequence diagram to PNG."""

from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs/06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio"
OUTPUT = ROOT / "docs/06-sequencediagram/drawio-png"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
PNG_IEND = b"\x00\x00\x00\x00IEND\xaeB`\x82"
PAGE_ID_PATTERN = re.compile(r"[A-Za-z0-9_-]+\Z")


def resolve_drawio() -> Path:
    candidates: list[Path] = []

    configured = os.environ.get("DRAWIO_EXECUTABLE")
    if configured:
        candidates.append(Path(configured))

    if os.name == "nt":
        local_app_data = os.environ.get("LOCALAPPDATA")
        if local_app_data:
            candidates.append(
                Path(local_app_data) / "Programs" / "draw.io" / "draw.io.exe"
            )
        program_files = os.environ.get("ProgramFiles")
        if program_files:
            candidates.append(Path(program_files) / "draw.io" / "draw.io.exe")

    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()

    for name in ("draw.io.exe", "drawio.exe", "drawio", "draw.io"):
        executable = shutil.which(name)
        if executable and Path(executable).suffix.lower() not in {".cmd", ".bat"}:
            return Path(executable).resolve()

    raise RuntimeError(
        "Cannot find the Draw.io CLI. Install draw.io, add its executable to PATH, "
        "or set DRAWIO_EXECUTABLE to its path."
    )


def get_use_case_pages() -> list[tuple[int, str, str]]:
    if not SOURCE.is_file():
        raise RuntimeError(f"Combined sequence diagram not found: {SOURCE}")

    root = ET.parse(SOURCE).getroot()
    pages: list[tuple[int, str, str]] = []
    seen_ids: set[str] = set()

    for page_index, diagram in enumerate(root.findall("diagram"), start=1):
        name = diagram.get("name", "")
        if not name.startswith("UC-"):
            continue

        page_id = diagram.get("id", "")
        if not PAGE_ID_PATTERN.fullmatch(page_id):
            raise RuntimeError(f"Invalid or missing page id for {name!r}: {page_id!r}")
        if page_id in seen_ids:
            raise RuntimeError(f"Duplicate Draw.io page id: {page_id}")

        seen_ids.add(page_id)
        pages.append((page_index, page_id, name))

    if not pages:
        raise RuntimeError(f"No Use Case tabs found in {SOURCE}")
    return pages


def repair_and_validate_png(path: Path) -> None:
    data = path.read_bytes()
    if not data.startswith(PNG_SIGNATURE):
        raise RuntimeError(f"Draw.io did not produce a PNG: {path.name}")

    if not data.endswith(PNG_IEND):
        if data.endswith(b"\x00\x00\x00\x00"):
            data = data[:-4]
        data += PNG_IEND
        path.write_bytes(data)

    if not path.read_bytes().endswith(PNG_IEND):
        raise RuntimeError(f"PNG export is incomplete: {path.name}")


def export_page(
    executable: Path,
    page_index: int,
    page_name: str,
    output_path: Path,
) -> None:
    command = [
        str(executable),
        "--export",
        "--format",
        "png",
        "--embed-diagram",
        "--scale",
        "2",
        "--page-index",
        str(page_index),
        "--output",
        str(output_path),
        str(SOURCE),
    ]
    try:
        result = subprocess.run(
            command,
            check=True,
            capture_output=True,
            text=True,
            timeout=180,
        )
    except subprocess.CalledProcessError as error:
        details = (error.stderr or error.stdout or "").strip()
        raise RuntimeError(f"Export failed for {page_name}: {details}") from error
    except subprocess.TimeoutExpired as error:
        raise RuntimeError(f"Export timed out for {page_name}") from error

    repair_and_validate_png(output_path)
    if result.stdout.strip():
        print(result.stdout.strip())


def main() -> int:
    try:
        pages = get_use_case_pages()
        executable = resolve_drawio()
        OUTPUT.mkdir(parents=True, exist_ok=True)
        expected_names = {f"{page_id}.drawio.png" for _, page_id, _ in pages}
        completed: list[tuple[Path, Path]] = []

        with tempfile.TemporaryDirectory(prefix=".sync-", dir=OUTPUT) as temp_dir:
            temp_root = Path(temp_dir)
            for index, (page_index, page_id, page_name) in enumerate(pages, start=1):
                temp_png = temp_root / f"{page_id}.drawio.png"
                export_page(executable, page_index, page_name, temp_png)
                completed.append((temp_png, OUTPUT / temp_png.name))
                print(f"[{index}/{len(pages)}] {page_name} -> {temp_png.name}")

            for temp_png, output_png in completed:
                os.replace(temp_png, output_png)

        stale_files = [
            path
            for path in OUTPUT.glob("*.drawio.png")
            if path.name not in expected_names
        ]
        for path in stale_files:
            path.unlink()

        print(
            f"Synced {len(completed)} Use Case PNGs to {OUTPUT.relative_to(ROOT)}"
            + (f"; removed {len(stale_files)} stale export(s)." if stale_files else ".")
        )
        return 0
    except (OSError, ET.ParseError, RuntimeError) as error:
        print(f"Sequence diagram sync failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
