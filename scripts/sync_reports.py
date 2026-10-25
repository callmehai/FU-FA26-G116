"""Convert local or Google Drive DOCX reports to searchable Markdown snapshots.

Run:
    python scripts/sync_reports.py             # Converts existing source/ DOCX files (or downloads if missing)
    python scripts/sync_reports.py --download  # Forces downloading latest DOCX from Google Drive and converts
"""

from __future__ import annotations

import argparse
import io
import json
import os
import re
import sys
import tempfile
import urllib.error
import urllib.request
import urllib.parse
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "docs" / "08-reports" / "source"
# Drive targets come from "Link Drive chuẩn Report 1–7" in docs/08-reports/README.md;
# change them together, and only ever point at the file named there.
REPORTS = (
    (
        "Report-1.docx",
        "Report 1 – Project Introduction",
        "https://docs.google.com/document/d/15ncrJ-7eInb0vLIOXmRfZsugiPkunGW6/edit",
        ROOT / "docs" / "08-reports" / "Report-1_Project_Introduction.md",
    ),
    (
        "Report-2.docx",
        "Report 2 – Project Management Plan",
        "https://docs.google.com/document/d/1oyefDV6vVOBY0qtO4sCQVMcoj3tDHKqo/edit",
        ROOT / "docs" / "08-reports" / "Report-2_Project_Management_Plan.md",
    ),
    (
        "Report-3.docx",
        "Report 3 – Software Requirement Specification",
        "https://docs.google.com/document/d/1JNNCwt0Yc_KLS9dPB6wtP8HW6x7D1fWl/edit",
        ROOT / "docs" / "08-reports" / "Report-3_Software_Requirement_Specification.md",
    ),
)

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NS = {"w": W}


def report_number(name: str) -> int:
    match = re.search(r"report[\s_-]*([1-3])(?=[\s_.-]|$)", name, re.IGNORECASE)
    if not match:
        raise ValueError(f"Cannot identify Report 1, 2 or 3 from: {name}. Use --report.")
    return int(match.group(1))


def drive_request(url: str) -> bytes:
    headers = {"User-Agent": "Mozilla/5.0"}
    token = os.environ.get("GOOGLE_DRIVE_ACCESS_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=60) as response:
        return response.read()


def drive_api(path: str, **params: str) -> bytes:
    if not os.environ.get("GOOGLE_DRIVE_ACCESS_TOKEN"):
        raise ValueError("Drive folder/private file requires GOOGLE_DRIVE_ACCESS_TOKEN with Drive read access.")
    return drive_request(f"https://www.googleapis.com/drive/v3/{path}?{urllib.parse.urlencode(params)}")


def download_docx(url: str, target: Path, mime_type: str | None = None) -> None:
    parsed = urllib.parse.urlparse(url)
    if parsed.scheme != "https" or parsed.hostname not in {"drive.google.com", "docs.google.com"}:
        raise ValueError("Expected an HTTPS Google Drive or Google Docs URL.")
    match = re.search(r"/d/([\w-]+)", parsed.path)
    file_id = match.group(1) if match else urllib.parse.parse_qs(parsed.query).get("id", [""])[0]
    if not re.fullmatch(r"[\w-]+", file_id):
        raise ValueError(f"Invalid Drive file URL: {url}")
    if os.environ.get("GOOGLE_DRIVE_ACCESS_TOKEN"):
        if mime_type is None:
            metadata = json.loads(drive_api(f"files/{file_id}", fields="mimeType", supportsAllDrives="true"))
            mime_type = metadata["mimeType"]
        if mime_type == "application/vnd.google-apps.document":
            data = drive_api(f"files/{file_id}/export", mimeType="application/vnd.openxmlformats-officedocument.wordprocessingml.document")
        else:
            data = drive_api(f"files/{file_id}", alt="media", supportsAllDrives="true")
    else:
        # Public Docs export and stored DOCX download use different URLs.
        if parsed.hostname == "docs.google.com":
            data = drive_request(f"https://docs.google.com/document/d/{file_id}/export?format=docx")
        else:
            data = drive_request(f"https://drive.google.com/uc?export=download&id={file_id}")
    try:
        with zipfile.ZipFile(io.BytesIO(data)) as document:
            ET.fromstring(document.read("word/document.xml"))
    except (zipfile.BadZipFile, KeyError, ET.ParseError) as error:
        raise ValueError("Drive did not return a DOCX. Check sharing/access; for private or large files set GOOGLE_DRIVE_ACCESS_TOKEN.") from error
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("wb", dir=target.parent, delete=False) as temporary:
        temporary.write(data)
        temporary_path = Path(temporary.name)
    temporary_path.replace(target)


def folder_reports(url: str) -> list[tuple[str, str, str]]:
    parsed = urllib.parse.urlparse(url)
    match = re.fullmatch(r"/drive/(?:u/\d+/)?folders/([\w-]+)/?", parsed.path)
    if parsed.scheme != "https" or parsed.hostname != "drive.google.com" or not match:
        raise ValueError("Invalid Google Drive folder URL.")
    params = {
        "q": f"'{match.group(1)}' in parents and trashed = false",
        "fields": "nextPageToken,files(id,name,mimeType)",
        "pageSize": "100", "supportsAllDrives": "true", "includeItemsFromAllDrives": "true",
    }
    files = []
    while True:
        page = json.loads(drive_api("files", **params))
        for item in page.get("files", []):
            if item["mimeType"] in {
                "application/vnd.google-apps.document",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            }:
                files.append((item["name"], f"https://drive.google.com/file/d/{item['id']}/view", item["mimeType"]))
        if not page.get("nextPageToken"):
            return files
        params["pageToken"] = page["nextPageToken"]


def download_reports(force: bool = False) -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    for filename, title, source_url, _ in REPORTS:
        target = SOURCE_DIR / filename
        if target.is_file() and not force:
            continue
        print(f"Downloading {filename} ({title}) from Google Drive...")
        download_docx(source_url, target)



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
    parser = argparse.ArgumentParser(description="Sync local or Google Drive DOCX reports to Markdown.")
    parser.add_argument("--download", "-d", action="store_true", help="Refresh the three configured Drive reports")
    parser.add_argument(
        "--allow-drive-import",
        action="store_true",
        help="Allow --download to overwrite docs/08-reports/source/*.docx from Drive",
    )
    parser.add_argument("--source", help="Local DOCX/directory (relative to repo root), or Drive file/folder URL")
    parser.add_argument("--report", type=int, choices=(1, 2, 3), help="Report to sync; required for a single Drive file")
    parser.add_argument("--report3-source", type=Path, help="Legacy alias for --source LOCAL_DOCX --report 3")
    parser.add_argument("--output-dir", type=Path, help="Alternative Markdown output directory")
    args = parser.parse_args()
    if args.download and (args.source or args.report3_source):
        parser.error("--download cannot be combined with an explicit source")
    if args.download and not args.allow_drive_import:
        parser.error(
            "--download overwrites docs/08-reports/source/*.docx with the Drive copy, "
            "and the repo is the source of truth; pass --allow-drive-import if you really mean it"
        )
    if args.report3_source and (args.source or args.report):
        parser.error("--report3-source cannot be combined with --source or --report")
    source_arg = str(args.report3_source) if args.report3_source else args.source
    selected = 3 if args.report3_source else args.report
    output_dir = args.output_dir
    if output_dir and not output_dir.is_absolute():
        output_dir = ROOT / output_dir
    try:
        with tempfile.TemporaryDirectory(prefix="sync-reports-") as temporary:
            sources: dict[int, tuple[Path, str]] = {}

            def add(number: int, path: Path, origin: str) -> None:
                if number in sources:
                    raise ValueError(f"Multiple source files match Report {number}; choose a single file.")
                sources[number] = (path, origin)

            if source_arg and source_arg.startswith("https://"):
                if "/folders/" in source_arg:
                    candidates = folder_reports(source_arg)
                    matches = []
                    for name, url, mime in candidates:
                        try:
                            number = report_number(name)
                        except ValueError:
                            continue
                        if selected is None or selected == number:
                            path = Path(temporary) / f"Report-{number}.docx"
                            add(number, path, url)
                            matches.append((url, path, mime))
                    for url, path, mime in matches:
                        download_docx(url, path, mime)
                else:
                    if selected is None:
                        raise ValueError("A single Drive file requires --report 1, 2 or 3.")
                    path = Path(temporary) / f"Report-{selected}.docx"
                    download_docx(source_arg, path)
                    add(selected, path, source_arg)
            else:
                local = Path(source_arg) if source_arg else SOURCE_DIR
                if not local.is_absolute():
                    local = ROOT / local
                local = local.resolve()
                if source_arg and not local.exists():
                    raise ValueError(f"Local source does not exist: {local}")
                if args.download:
                    download_reports(force=True)
                    for number, (name, _, url, _) in enumerate(REPORTS, 1):
                        if selected is None or selected == number:
                            add(number, SOURCE_DIR / name, url)
                else:
                    paths = [local] if local.is_file() else sorted(local.glob("*"))
                    for path in paths:
                        if not path.is_file() or path.suffix.lower() != ".docx" or path.name.startswith("~$"):
                            continue
                        try:
                            number = selected if local.is_file() and selected else report_number(path.name)
                        except ValueError:
                            continue
                        if selected is None or selected == number:
                            origin = path.as_uri()
                            if path.is_relative_to(ROOT):
                                origin = "source/" + urllib.parse.quote(path.name) if path.parent == SOURCE_DIR else path.as_uri()
                            add(number, path, origin)
                    # Preserve automatic download for missing default reports only.
                    if not source_arg:
                        for number, (name, _, url, _) in enumerate(REPORTS, 1):
                            if number not in sources and (selected is None or selected == number):
                                path = SOURCE_DIR / name
                                download_docx(url, path)
                                add(number, path, url)
            if not sources:
                raise ValueError("No matching Report 1/2/3 DOCX files found in the source.")
            converted = []
            for number, (path, origin) in sorted(sources.items()):
                _, title, _, destination = REPORTS[number - 1]
                if output_dir:
                    destination = output_dir / destination.name
                body = docx_to_markdown(path)
                converted.append((destination, f"# {title}\n\n> Nguồn: [Tài liệu nguồn]({origin})\n\n{body}"))
            for destination, content in converted:
                write_atomic(destination, content)
                print(f"Synced {destination}")
    except (ValueError, OSError, urllib.error.URLError) as error:
        print(f"Sync failed: {error}", file=sys.stderr)
        return 1
    print("Review changes with: git diff -- docs/08-reports")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
