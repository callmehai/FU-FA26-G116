"""Convert the three downloaded Drive reports to searchable Markdown snapshots.

Download the latest DOCX files from the Drive links below into
docs/08-reports/source/ as Report-1.docx, Report-2.docx, and Report-3.docx,
then run: python scripts/sync_reports.py
"""

from __future__ import annotations

import re
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "docs" / "08-reports" / "source"
REPORTS = (
    (
        "Report-1.docx",
        "Report 1 – Project Introduction",
        "https://docs.google.com/document/d/1Lbw_A5CHPJzDa4kkLQFdjjn_c5KRIwOW/edit",
        ROOT / "docs" / "08-reports" / "Report-1_Project_Introduction.md",
    ),
    (
        "Report-2.docx",
        "Report 2 – Project Management Plan",
        "https://docs.google.com/document/d/1d4K3K3zcURku4SG84nXj7Zj6D-KiqGXa/edit",
        ROOT / "docs" / "08-reports" / "Report-2_Project_Management_Plan.md",
    ),
    (
        "Report-3.docx",
        "Report 3 – Software Requirement Specification",
        "https://docs.google.com/document/d/1rQuQxHWq4nJPJYMrrMlAZQ3P173PTXJz/edit",
        ROOT / "docs" / "08-reports" / "Report-3_Software_Requirement_Specification.md",
    ),
)

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NS = {"w": W}


def paragraph_text(paragraph: ET.Element) -> str:
    parts: list[str] = []
    for node in paragraph.iter():
        if node.tag == f"{{{W}}}t":
            parts.append(node.text or "")
        elif node.tag == f"{{{W}}}tab":
            parts.append("\t")
        elif node.tag in (f"{{{W}}}br", f"{{{W}}}cr"):
            parts.append("\n")
    return "".join(parts).strip()


def markdown_table(table: ET.Element) -> list[str]:
    rows: list[list[str]] = []
    for row in table.findall("w:tr", NS):
        cells = []
        for cell in row.findall("w:tc", NS):
            value = "<br>".join(
                text for text in (paragraph_text(p) for p in cell.findall(".//w:p", NS)) if text
            )
            cells.append(value.replace("|", "\\|").replace("\n", "<br>"))
        if any(cells):
            rows.append(cells)

    if not rows:
        return []

    width = max(map(len, rows))
    rows = [row + [""] * (width - len(row)) for row in rows]
    output = ["| " + " | ".join(rows[0]) + " |", "| " + " | ".join(["---"] * width) + " |"]
    output.extend("| " + " | ".join(row) + " |" for row in rows[1:])
    return output


def docx_to_markdown(path: Path) -> str:
    try:
        with zipfile.ZipFile(path) as document:
            root = ET.fromstring(document.read("word/document.xml"))
    except (OSError, KeyError, zipfile.BadZipFile, ET.ParseError) as error:
        raise ValueError(f"Could not read DOCX {path}: {error}") from error

    body = root.find("w:body", NS)
    if body is None:
        raise ValueError(f"DOCX has no document body: {path}")

    blocks: list[str] = []
    for child in body:
        if child.tag == f"{{{W}}}p":
            text = paragraph_text(child)
            if not text:
                continue
            style = child.find("w:pPr/w:pStyle", NS)
            style_name = style.get(f"{{{W}}}val", "") if style is not None else ""
            is_list_item = child.find("w:pPr/w:numPr", NS) is not None
            heading = re.fullmatch(r"Heading([1-6])", style_name)
            if heading:
                blocks.append(f"{'#' * min(6, int(heading.group(1)) + 1)} {text}")
            elif is_list_item or "List" in style_name:
                blocks.append(f"- {text}")
            else:
                blocks.append(text)
        elif child.tag == f"{{{W}}}tbl":
            table = markdown_table(child)
            if table:
                blocks.append("\n".join(table))

    result = "\n\n".join(blocks).strip()
    if not result:
        raise ValueError(f"DOCX contains no text to sync: {path}")
    return result


def write_atomic(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(
        "w", encoding="utf-8", newline="\n", dir=path.parent, delete=False
    ) as temporary:
        temporary.write(content.rstrip() + "\n")
        temporary_path = Path(temporary.name)
    temporary_path.replace(path)


def main() -> int:
    missing = [SOURCE_DIR / filename for filename, *_ in REPORTS if not (SOURCE_DIR / filename).is_file()]
    if missing:
        names_and_urls = "\n".join(
            f"  - {filename}: {source_url}"
            for filename, _, source_url, _ in REPORTS
            if SOURCE_DIR / filename in missing
        )
        print(
            "Download the missing DOCX files from Google Drive into "
            f"{SOURCE_DIR.relative_to(ROOT)}, then run again:\n"
            + names_and_urls,
            file=sys.stderr,
        )
        return 2

    try:
        converted = []
        for filename, title, source_url, destination in REPORTS:
            body = docx_to_markdown(SOURCE_DIR / filename)
            converted.append(
                (
                    destination,
                    f"# {title}\n\n> Nguồn: [Google Drive]({source_url})\n\n{body}",
                )
            )
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1

    for destination, content in converted:
        write_atomic(destination, content)
        print(f"Synced {destination.relative_to(ROOT)}")
    print("Review changes with: git diff -- docs/08-reports")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
