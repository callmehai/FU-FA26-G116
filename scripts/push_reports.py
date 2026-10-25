"""Publish a local report to Google Drive, replacing the existing file in place.

Direction is one way: `docs/08-reports/source/*.docx` is the source, Markdown is
snapshotted from it, and Drive is only ever written from it.

Run:
    python scripts/push_reports.py 3 --check    # is Drive up to date with local?
    python scripts/push_reports.py 3            # dry run: where it would write
    python scripts/push_reports.py 3 --confirm  # replace the Drive content

The Drive target is a Google Doc, so the local DOCX is uploaded with
`files.update` and converted: the file keeps its ID, name and link, and no copy
is created. Needs GOOGLE_DRIVE_ACCESS_TOKEN with Drive write access. The targets
already exist and were not created by this client, so `drive.file` is not
enough: request https://www.googleapis.com/auth/drive.
"""

from __future__ import annotations

import argparse
import difflib
import json
import os
import re
import sys
import tempfile
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sync_reports import docx_to_markdown  # noqa: E402  (same directory, shared parser)

ROOT = Path(__file__).resolve().parent.parent
SOURCE_DIR = ROOT / "docs" / "08-reports" / "source"
SNAPSHOT_DIR = ROOT / "docs" / "08-reports"

API = "https://www.googleapis.com/drive/v3"
UPLOAD = "https://www.googleapis.com/upload/drive/v3"
DOC_MIME = "application/vnd.google-apps.document"
DOCX_MIME = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"

# Fixed targets from "Link Drive chuẩn Report 1-7" in docs/08-reports/README.md.
# A report may only ever be written to the ID named there.
TARGETS: dict[int, tuple[str, str, str]] = {
    1: (
        "15ncrJ-7eInb0vLIOXmRfZsugiPkunGW6",
        "Report_1_Project_Introduction.docx",
        "Report-1_Project_Introduction.md",
    ),
    2: (
        "1oyefDV6vVOBY0qtO4sCQVMcoj3tDHKqo",
        "Report_2_Project_Management_Plan.docx",
        "Report-2_Project_Management_Plan.md",
    ),
    3: (
        "1JNNCwt0Yc_KLS9dPB6wtP8HW6x7D1fWl",
        "Report_3_Software_Requirement_Specification.docx",
        "Report-3_Software_Requirement_Specification.md",
    ),
    4: (
        "1vPEW9IKH_5GZmkLOz9lkYRZdKHp-cKzd",
        "Report-4_Software Design Specification.docx",
        "Report-4_Software Design Specification.md",
    ),
}


def token() -> str:
    value = os.environ.get("GOOGLE_DRIVE_ACCESS_TOKEN")
    if not value:
        raise SystemExit(
            "GOOGLE_DRIVE_ACCESS_TOKEN is not set; it needs Drive write access."
        )
    return value


def call(request: urllib.request.Request) -> bytes:
    request.add_header("Authorization", f"Bearer {token()}")
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", "replace")[:400]
        raise SystemExit(f"Drive answered {error.code}: {body}") from error


def metadata(file_id: str) -> dict:
    fields = "id,name,mimeType,modifiedTime,size,owners(emailAddress),trashed"
    request = urllib.request.Request(
        f"{API}/files/{file_id}?fields={urllib.parse.quote(fields)}&supportsAllDrives=true"
    )
    return json.loads(call(request))


def export_docx(file_id: str, target: Path) -> None:
    request = urllib.request.Request(
        f"{API}/files/{file_id}/export?mimeType={urllib.parse.quote(DOCX_MIME)}"
    )
    target.write_bytes(call(request))
    with zipfile.ZipFile(target) as document:
        document.read("word/document.xml")


def upload(file_id: str, path: Path) -> dict:
    boundary = f"trustcabinet-{uuid.uuid4().hex}"
    body = b"".join(
        [
            f"--{boundary}\r\n".encode(),
            b"Content-Type: application/json; charset=UTF-8\r\n\r\n",
            json.dumps({"mimeType": DOC_MIME}).encode("utf-8"),
            b"\r\n",
            f"--{boundary}\r\n".encode(),
            f"Content-Type: {DOCX_MIME}\r\n\r\n".encode(),
            path.read_bytes(),
            b"\r\n",
            f"--{boundary}--\r\n".encode(),
        ]
    )
    request = urllib.request.Request(
        f"{UPLOAD}/files/{file_id}?uploadType=multipart&supportsAllDrives=true",
        data=body,
        method="PATCH",
        headers={"Content-Type": f"multipart/related; boundary={boundary}"},
    )
    return json.loads(call(request))


def normalised(path: Path) -> str:
    return re.sub(r"\s+", " ", docx_to_markdown(path)).strip()


def changelog_dates(text: str) -> list[str]:
    return re.findall(r"^\| (\d{2}/\d{2}/\d{4}) \|", text, re.M)


def check(file_id: str, local: Path) -> int:
    with tempfile.TemporaryDirectory(prefix="push-reports-") as temporary:
        remote = Path(temporary) / "remote.docx"
        export_docx(file_id, remote)
        remote_text = normalised(remote)
    local_text = normalised(local)

    remote_dates = changelog_dates(remote_text)
    local_dates = changelog_dates(local_text)
    print(f"local  Change Log rows: {len(local_dates)} (last {local_dates[-1] if local_dates else '-'})")
    print(f"drive  Change Log rows: {len(remote_dates)} (last {remote_dates[-1] if remote_dates else '-'})")

    if remote_text == local_text:
        print("UP TO DATE: Drive matches the local DOCX.")
        return 0

    missing = [date for date in local_dates if date not in remote_dates]
    if missing:
        print(f"DRIVE IS BEHIND: missing {len(missing)} Change Log row(s) from {missing[0]} onward.")
    ratio = difflib.SequenceMatcher(None, local_text[:200_000], remote_text[:200_000]).ratio()
    print(f"OUT OF DATE: text matches {ratio:.1%} over the first 200k characters.")
    return 1


def describe(label: str, info: dict) -> None:
    owner = ", ".join(item.get("emailAddress", "?") for item in info.get("owners", [])) or "?"
    print(f"{label:<6} id={info.get('id')}")
    print(f"       name={info.get('name')!r} mimeType={info.get('mimeType')}")
    print(f"       modifiedTime={info.get('modifiedTime')} owner={owner}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=int, help="report number, 1-4")
    action = parser.add_mutually_exclusive_group()
    action.add_argument("--check", action="store_true", help="compare Drive with the local DOCX")
    action.add_argument("--confirm", action="store_true", help="actually upload")
    args = parser.parse_args()

    if args.report not in TARGETS:
        raise SystemExit(f"Report {args.report} has no fixed Drive target; use one of {sorted(TARGETS)}.")
    file_id, docx_name, md_name = TARGETS[args.report]
    local = SOURCE_DIR / docx_name
    if not local.is_file():
        raise SystemExit(f"Local DOCX not found: {local}")

    before = metadata(file_id)
    if before.get("trashed"):
        raise SystemExit("The Drive target is in the trash; refusing to write.")

    if args.check:
        return check(file_id, local)

    print(f"Local  {local.relative_to(ROOT)} ({local.stat().st_size:,} bytes)")
    describe("Drive", before)

    snapshot = SNAPSHOT_DIR / md_name
    if snapshot.is_file() and snapshot.stat().st_mtime < local.stat().st_mtime:
        print(f"\nWARNING: {snapshot.name} is older than the DOCX; run sync_reports.py first.")

    if not args.confirm:
        print("\nDry run. Re-run with --confirm to replace the Drive content, --check to compare.")
        return 0

    print("\nUploading ...")
    upload(file_id, local)
    after = metadata(file_id)
    describe("After", after)

    if after.get("id") != file_id:
        raise SystemExit("Drive returned a different file id; stop and check the target.")
    if after.get("modifiedTime") == before.get("modifiedTime"):
        raise SystemExit("modifiedTime did not change; the upload probably did not apply.")
    print("\nOK: same file id, content replaced.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
