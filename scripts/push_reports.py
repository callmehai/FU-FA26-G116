"""Publish local report files to Google Drive, replacing content in place.

Direction is one way: `docs/08-reports/source/` is the source, Markdown is
snapshotted from it, and Drive is only ever written from it.

Run:
    python scripts/push_reports.py 3 --check          # is Drive up to date?
    python scripts/push_reports.py 3                  # dry run: where it would write
    python scripts/push_reports.py 3 --confirm        # replace the Drive content
    python scripts/push_reports.py minutes --check    # the Meeting_Minutes folder
    python scripts/push_reports.py minutes --confirm
    python scripts/push_reports.py all --check        # every target

Reports 1-4, 5.0 and 5.2 each have one fixed Drive file, listed in
`docs/08-reports/README.md` ("Link Drive chuẩn"); a report is only ever written
to that ID. Meeting minutes are matched by file name inside the Drive folder
"Meeting_Minutes" and created there when the local file is new, because a new
week brings a new file rather than a new version of an old one.

The Drive target is either a Google Doc or a stored Office file — whichever it
already is. Either way the local file is uploaded with `files.update`, so it
keeps its ID, name, link and type, and no copy is created. Needs a Drive
credential with write access (see `scripts/drive_auth.py`). The targets already
exist and were not created by this client, so `drive.file` is not enough:
request https://www.googleapis.com/auth/drive.
"""

from __future__ import annotations

import argparse
import difflib
import hashlib
import json
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
import drive_auth  # noqa: E402  (same directory, shared credential)
from sync_reports import docx_to_markdown  # noqa: E402  (same directory, shared parser)

ROOT = Path(__file__).resolve().parent.parent
REPORTS_DIR = ROOT / "docs" / "08-reports"
SOURCE_DIR = REPORTS_DIR / "source"
TEST_DIR = REPORTS_DIR / "test-reports"
MINUTES_DIR = SOURCE_DIR / "Meeting_Minutes"

API = "https://www.googleapis.com/drive/v3"
UPLOAD = "https://www.googleapis.com/upload/drive/v3"
DOC_MIME = "application/vnd.google-apps.document"
DOCX_MIME = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
XLSX_MIME = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"

# A member a valid Office archive must contain; used to reject a truncated download.
ZIP_MEMBER = {DOC_MIME: "word/document.xml", DOCX_MIME: "word/document.xml", XLSX_MIME: "xl/workbook.xml"}

# Mime type to send for a local file, by suffix. A stored Drive file keeps whatever
# type it already has; this only labels the bytes on the way up.
SUFFIX_MIME = {
    ".docx": DOCX_MIME,
    ".xlsx": XLSX_MIME,
    ".xlsm": "application/vnd.ms-excel.sheet.macroEnabled.12",
}

# Fixed targets from "Link Drive chuẩn" in docs/08-reports/README.md. A report may
# only ever be written to the ID named there. The Drive name and the local name can
# differ (Report 5.0, 5.2), which is why the ID, not the name, is the key.
TARGETS: dict[str, tuple[str, Path, Path | None]] = {
    "1": (
        "15ncrJ-7eInb0vLIOXmRfZsugiPkunGW6",
        SOURCE_DIR / "Report_1_Project_Introduction.docx",
        REPORTS_DIR / "Report-1_Project_Introduction.md",
    ),
    "2": (
        "1oyefDV6vVOBY0qtO4sCQVMcoj3tDHKqo",
        SOURCE_DIR / "Report_2_Project_Management_Plan.docx",
        REPORTS_DIR / "Report-2_Project_Management_Plan.md",
    ),
    "3": (
        "1JNNCwt0Yc_KLS9dPB6wtP8HW6x7D1fWl",
        SOURCE_DIR / "Report_3_Software_Requirement_Specification.docx",
        REPORTS_DIR / "Report-3_Software_Requirement_Specification.md",
    ),
    "4": (
        "1vPEW9IKH_5GZmkLOz9lkYRZdKHp-cKzd",
        SOURCE_DIR / "Report-4_Software Design Specification.docx",
        REPORTS_DIR / "Report-4_Software Design Specification.md",
    ),
    "5.0": (
        "1hokAKFTmcJAmdnrJuKBfN4Z1zptNJSIy",
        TEST_DIR / "Report5.0_Test_Documentation.docx",
        None,
    ),
    "5.2": (
        "1a9L8H_WE1PvDpXkqj8zpM9hv7plsShqV",
        TEST_DIR / "Report5.2_Integration_Test.xlsx",
        None,
    ),
}

# The Drive folder that holds the meeting minutes. Minutes are not fixed targets:
# each session adds a new file, so they are matched by name inside this folder.
MINUTES_FOLDER = "14ir6QuR6UWga57nkrSayzSQZSNcRVhce"


def token() -> str:
    return drive_auth.access_token()


def call(request: urllib.request.Request) -> bytes:
    request.add_header("Authorization", f"Bearer {token()}")
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", "replace")[:400]
        raise SystemExit(f"Drive answered {error.code}: {body}") from error


def metadata(file_id: str) -> dict:
    fields = (
        "id,name,mimeType,modifiedTime,size,md5Checksum,parents,"
        "owners(emailAddress),trashed,capabilities(canEdit)"
    )
    request = urllib.request.Request(
        f"{API}/files/{file_id}?fields={urllib.parse.quote(fields)}&supportsAllDrives=true"
    )
    return json.loads(call(request))


def folder_files(folder_id: str) -> dict[str, dict]:
    """Every file directly inside the folder, keyed by name."""
    fields = (
        "nextPageToken,files(id,name,mimeType,size,md5Checksum,modifiedTime,"
        "trashed,capabilities(canEdit))"
    )
    query = {
        "q": f"'{folder_id}' in parents and trashed = false",
        "fields": fields,
        "pageSize": "1000",
        "supportsAllDrives": "true",
        "includeItemsFromAllDrives": "true",
    }
    files: dict[str, dict] = {}
    page = None
    while True:
        if page:
            query["pageToken"] = page
        request = urllib.request.Request(f"{API}/files?{urllib.parse.urlencode(query)}")
        data = json.loads(call(request))
        for item in data.get("files", []):
            files[item["name"]] = item
        page = data.get("nextPageToken")
        if not page:
            return files


def download_media(file_id: str, target: Path, mime_type: str) -> None:
    """Fetch the current content: a Google Doc is exported, a stored file downloaded."""
    if mime_type == DOC_MIME:
        url = f"{API}/files/{file_id}/export?mimeType={urllib.parse.quote(DOCX_MIME)}"
    else:
        url = f"{API}/files/{file_id}?alt=media&supportsAllDrives=true"
    target.write_bytes(call(urllib.request.Request(url)))
    member = ZIP_MEMBER.get(mime_type)
    if member:
        with zipfile.ZipFile(target) as archive:
            archive.read(member)


def multipart(metadata_body: dict, path: Path) -> tuple[str, bytes]:
    mime = SUFFIX_MIME[path.suffix.lower()]
    boundary = f"trustcabinet-{uuid.uuid4().hex}"
    body = b"".join(
        [
            f"--{boundary}\r\n".encode(),
            b"Content-Type: application/json; charset=UTF-8\r\n\r\n",
            json.dumps(metadata_body).encode("utf-8"),
            b"\r\n",
            f"--{boundary}\r\n".encode(),
            f"Content-Type: {mime}\r\n\r\n".encode(),
            path.read_bytes(),
            b"\r\n",
            f"--{boundary}--\r\n".encode(),
        ]
    )
    return boundary, body


def upload(file_id: str, path: Path, mime_type: str) -> dict:
    """Replace the content, keeping the file's existing ID, name and mime type."""
    boundary, body = multipart({"mimeType": mime_type}, path)
    request = urllib.request.Request(
        f"{UPLOAD}/files/{file_id}?uploadType=multipart&supportsAllDrives=true",
        data=body,
        method="PATCH",
        headers={"Content-Type": f"multipart/related; boundary={boundary}"},
    )
    return json.loads(call(request))


def create(folder_id: str, path: Path, name: str) -> dict:
    """Add a new file to the folder, named after the local file."""
    mime = SUFFIX_MIME[path.suffix.lower()]
    boundary, body = multipart({"name": name, "mimeType": mime, "parents": [folder_id]}, path)
    request = urllib.request.Request(
        f"{UPLOAD}/files?uploadType=multipart&supportsAllDrives=true",
        data=body,
        method="POST",
        headers={"Content-Type": f"multipart/related; boundary={boundary}"},
    )
    return json.loads(call(request))


def md5(path: Path) -> str:
    return hashlib.md5(path.read_bytes()).hexdigest()


def normalised(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def changelog_dates(text: str) -> list[str]:
    return re.findall(r"^\| (\d{2}/\d{2}/\d{4}) \|", text, re.M)


def fetch_markdown(file_id: str, mime_type: str) -> str:
    with tempfile.TemporaryDirectory(prefix="push-reports-") as temporary:
        remote = Path(temporary) / "remote.docx"
        download_media(file_id, remote, mime_type)
        return docx_to_markdown(remote)


def text_state(local: Path, info: dict) -> tuple[bool, float]:
    """Compare a local DOCX with the Drive copy as text, ignoring whitespace."""
    remote_text = normalised(fetch_markdown(info["id"], info["mimeType"]))
    local_text = normalised(docx_to_markdown(local))
    ratio = difflib.SequenceMatcher(None, local_text[:200_000], remote_text[:200_000]).ratio()
    return remote_text == local_text, ratio


def check(file_id: str, local: Path, mime_type: str) -> int:
    """Report whether Drive matches the local DOCX, with Change Log detail."""
    remote_markdown = fetch_markdown(file_id, mime_type)
    local_markdown = docx_to_markdown(local)
    remote_text = normalised(remote_markdown)
    local_text = normalised(local_markdown)

    remote_dates = changelog_dates(remote_markdown)
    local_dates = changelog_dates(local_markdown)
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


def writable(info: dict, what: str) -> None:
    if info.get("trashed"):
        raise SystemExit(f"{what} is in the trash; refusing to write.")
    if not info.get("capabilities", {}).get("canEdit"):
        raise SystemExit(
            f"The account behind the token cannot edit {what}; "
            "ask the owner for edit access before pushing."
        )


def run_target(key: str, args: argparse.Namespace) -> int:
    file_id, local, snapshot = TARGETS[key]
    if not local.is_file():
        raise SystemExit(f"Local file not found: {local.relative_to(ROOT)}")

    before = metadata(file_id)
    writable(before, "The Drive target")

    if args.check:
        if local.suffix.lower() == ".docx":
            return check(file_id, local, before["mimeType"])
        if md5(local) == before.get("md5Checksum"):
            print(f"UP TO DATE: Drive matches {local.name} byte for byte.")
            return 0
        print(f"OUT OF DATE: {local.name} differs from the Drive copy.")
        return 1

    print(f"Local  {local.relative_to(ROOT)} ({local.stat().st_size:,} bytes)")
    describe("Drive", before)

    if snapshot is not None and snapshot.is_file() and snapshot.stat().st_mtime < local.stat().st_mtime:
        print(f"\nWARNING: {snapshot.name} is older than the source; run sync_reports.py first.")

    if not args.confirm:
        print("\nDry run. Re-run with --confirm to replace the Drive content, --check to compare.")
        return 0

    print("\nUploading ...")
    upload(file_id, local, before["mimeType"])
    after = metadata(file_id)
    describe("After", after)

    if after.get("id") != file_id:
        raise SystemExit("Drive returned a different file id; stop and check the target.")
    if after.get("modifiedTime") == before.get("modifiedTime"):
        raise SystemExit("modifiedTime did not change; the upload probably did not apply.")
    print("\nOK: same file id, content replaced.")
    return 0


def run_minutes(args: argparse.Namespace) -> int:
    folder = metadata(MINUTES_FOLDER)
    if folder.get("mimeType") != "application/vnd.google-apps.folder":
        raise SystemExit("The minutes target is not a folder; check MINUTES_FOLDER.")

    local = sorted(MINUTES_DIR.glob("*.docx"))
    if not local:
        raise SystemExit(f"No local minutes found in {MINUTES_DIR.relative_to(ROOT)}")
    remote = folder_files(MINUTES_FOLDER)

    if args.check:
        print(f"{'file':<58} {'state':<12} detail")
        missing = outdated = 0
        for path in local:
            info = remote.get(path.name)
            if info is None:
                missing += 1
                print(f"{path.name:<58} {'MISSING':<12} not on Drive ({path.stat().st_size:,} bytes)")
                continue
            if not info.get("capabilities", {}).get("canEdit"):
                print(f"{path.name:<58} {'NO ACCESS':<12} token cannot edit the Drive file")
                continue
            same, ratio = text_state(path, info)
            if same:
                print(f"{path.name:<58} {'up to date':<12} id={info['id']}")
            else:
                outdated += 1
                print(f"{path.name:<58} {'OUT OF DATE':<12} text matches {ratio:.1%}")
        for name in sorted(set(remote) - {path.name for path in local}):
            print(f"{name:<58} {'DRIVE ONLY':<12} not in the repo; left alone")
        print(
            f"\n{len(local)} local, {missing} missing, {outdated} out of date, "
            f"{len(local) - missing - outdated} up to date"
        )
        return 1 if missing or outdated else 0

    if not args.confirm:
        for path in local:
            info = remote.get(path.name)
            if info is None:
                print(f"would create  {path.name} ({path.stat().st_size:,} bytes)")
                continue
            same, _ = text_state(path, info)
            print(f"{'up to date   ' if same else 'would replace'} {path.name} (id={info['id']})")
        print("\nDry run. Re-run with --confirm to write to Drive, --check to compare.")
        return 0

    writable(folder, "The minutes folder")
    created = replaced = 0
    for path in local:
        info = remote.get(path.name)
        if info is None:
            result = create(MINUTES_FOLDER, path, path.name)
            created += 1
            print(f"created       {path.name} -> id={result['id']}")
            continue
        same, _ = text_state(path, info)
        if same:
            print(f"up to date    {path.name}")
            continue
        upload(info["id"], path, info["mimeType"])
        replaced += 1
        print(f"replaced      {path.name} (id={info['id']})")
    print(f"\nOK: {created} created, {replaced} replaced, {len(local)} local file(s) checked.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("target", help="report key (1-4, 5.0, 5.2), 'minutes' or 'all'")
    action = parser.add_mutually_exclusive_group()
    action.add_argument("--check", action="store_true", help="compare Drive with the local file")
    action.add_argument("--confirm", action="store_true", help="actually upload")
    args = parser.parse_args()

    if args.target == "all":
        status = 0
        for key in TARGETS:
            print(f"--- Report {key}")
            status = max(status, run_target(key, args))
        print("--- Meeting minutes")
        return max(status, run_minutes(args))

    if args.target == "minutes":
        return run_minutes(args)

    if args.target not in TARGETS:
        raise SystemExit(
            f"Unknown target {args.target!r}; use one of {sorted(TARGETS)}, 'minutes' or 'all'."
        )
    return run_target(args.target, args)


if __name__ == "__main__":
    sys.exit(main())
