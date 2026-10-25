"""Publish local diagrams to Google Drive, replacing existing files in place.

Direction is one way: the repository is the source and Drive is only ever
written from it. Nothing is copied from Drive back into the repository, and a
file that exists only on Drive is reported but never removed unless `--prune`
is passed together with `--confirm`.

Run:
    python scripts/push_diagrams.py --list                 # groups and local folders
    python scripts/push_diagrams.py all --check            # is Drive up to date?
    python scripts/push_diagrams.py sequence --check       # one group
    python scripts/push_diagrams.py sequence/auth/UC01     # dry run, one file
    python scripts/push_diagrams.py all                    # dry run: the plan
    python scripts/push_diagrams.py all --confirm          # upload
    python scripts/push_diagrams.py sequence --prune       # list remote-only files

Every target folder already exists on Drive and was not created by this client,
so `drive.file` is not enough: the token needs
https://www.googleapis.com/auth/drive write access. Credentials are resolved by
`drive_auth.py`, the same way as the report scripts: set
GOOGLE_DRIVE_REFRESH_TOKEN with GOOGLE_DRIVE_CLIENT_ID and
GOOGLE_DRIVE_CLIENT_SECRET (created once by `scripts/google_drive_token.py`),
or GOOGLE_DRIVE_ACCESS_TOKEN for a single run. Check with:

    python scripts/google_drive_token.py --check

The Drive folder is "Diagrams":
https://drive.google.com/drive/folders/18E52RJTqrrd3j9D9ROJllVC2kAvtOHJx

Files are matched by name inside the mapped Drive folder. An existing file is
updated with its content replaced, keeping the file id, name, parent and link.
A missing file is created in that folder. Folders that do not exist yet (a new
feature folder, for example) are created under the group folder.

A file listed in `Source.renames` is looked up under its old Drive name, gets
its content updated, and is then renamed to the local name; the id, parent and
link survive, so an existing share link keeps working.

The manifest below is the only place that knows where a local diagram goes.
When a diagram folder is added or a Drive name changes, change it here.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from dataclasses import dataclass, field
from pathlib import Path, PurePosixPath
from typing import Iterable, Mapping

import drive_auth  # same directory, shared credential

ROOT = Path(__file__).resolve().parent.parent

API = "https://www.googleapis.com/drive/v3"
UPLOAD = "https://www.googleapis.com/upload/drive/v3"
FOLDER_MIME = "application/vnd.google-apps.folder"

MIME_BY_SUFFIX = {
    ".drawio": "application/vnd.jgraph.mxfile",
    ".png": "image/png",
    ".pdf": "application/pdf",
    ".txt": "text/plain; charset=utf-8",
}


@dataclass(frozen=True)
class Source:
    """A local folder whose files belong in one Drive folder."""

    local: Path
    patterns: tuple[str, ...]
    recursive: bool = False
    drive_dir: str = ""
    # Local subfolder name -> Drive subfolder name.
    dir_map: Mapping[str, str] = field(default_factory=dict)
    # Local file path (posix, relative to `local`) -> Drive path including name.
    file_map: Mapping[str, str] = field(default_factory=dict)
    # Local file path (posix, relative to `local`) -> Drive name currently in
    # use, when the local name is canonical and Drive still has the old one.
    # The file keeps its id and is renamed on Drive after its content update.
    renames: Mapping[str, str] = field(default_factory=dict)


@dataclass(frozen=True)
class Group:
    key: str
    title: str
    drive_folder: str
    sources: tuple[Source, ...]


# Drive folder ids come from the "Diagrams" folder. They are fixed targets: a
# diagram may only ever be written inside the folder named here.
GROUPS: tuple[Group, ...] = (
    Group(
        key="sequence",
        title="Sequence diagram",
        drive_folder="18oxf85AjCDC16443ceP1Ok_LA2lfojab",
        sources=(
            Source(
                local=ROOT / "docs" / "06-sequencediagram",
                patterns=("*.txt", "*.drawio", "*.png"),
                recursive=True,
                dir_map={
                    "auth": "Authentication",
                    "user-management": "User Management",
                    "role-management": "Role & Permission Management",
                    "meeting-management": "Meeting Management",
                    "agenda-management": "Agenda Management",
                    "meeting-participant-management": "Meeting Participant Management",
                    "notification-management": "Notification Management",
                    "document-management": "Document Management",
                    "voting-management": "Voting Management",
                    "drawio-png": "drawio",
                },
                file_map={
                    "TrustCabinet_Sequence_Diagrams.drawio": "drawio/TrustCabinet_Sequence_Diagrams.drawio",
                },
            ),
        ),
    ),
    Group(
        key="class",
        title="Class diagram",
        drive_folder="1nctA4Ffgewj_iFy2qe8KvVyeyLVx1Oui",
        sources=(
            Source(
                local=ROOT / "docs" / "07-classdiagram",
                patterns=("*.drawio", "*.png"),
            ),
        ),
    ),
    Group(
        key="context",
        title="Context diagram",
        drive_folder="1FzilzjQjoQtbNscTVoPiA1N-sWMMkD0y",
        sources=(
            Source(
                local=ROOT / "docs" / "11-contextdiagram",
                patterns=("*.drawio", "*.png"),
            ),
        ),
    ),
    Group(
        key="erd",
        title="ERD",
        drive_folder="1S-3R7ys_ymQEC9E9Zkw3AHztoOjnR4lB",
        sources=(
            Source(
                local=ROOT / "docs" / "13-erd" / "muc-tieu",
                patterns=("*.drawio", "*.png", "*.pdf"),
            ),
            Source(
                local=ROOT / "docs" / "13-erd" / "chen",
                patterns=("*.drawio", "*.png", "*.pdf"),
            ),
        ),
    ),
    Group(
        key="main-flow",
        title="Main Flow",
        drive_folder="1sgtXubMouySCezShiYC8PYtab7OJ2A2K",
        sources=(
            Source(
                local=ROOT / "docs" / "09-bussinessflow",
                patterns=("*.drawio", "*.png"),
            ),
        ),
    ),
    Group(
        key="screen-flow",
        title="Screen flow",
        drive_folder="1FI9ytaVh7zNZmW4oflbcO874rdHEwtgr",
        sources=(
            Source(
                local=ROOT / "docs" / "12-screenflow",
                patterns=("*.drawio", "*.png"),
            ),
        ),
    ),
    Group(
        key="uc",
        title="UC diagram",
        drive_folder="1YPCo8mUbu8QCS_el_M_SbnSzHfsqWPKf",
        sources=(
            Source(
                local=ROOT / "docs" / "14-uc-diagram",
                patterns=("*.drawio", "*.png"),
                file_map={
                    "uc-system-admin.png": "trustcabinet-use-cases-System Admin.drawio.png",
                    "uc-meeting-organizer.png": "trustcabinet-use-cases-Meeting Organizer.drawio.png",
                    "uc-meeting-approver.png": "trustcabinet-use-cases-Meeting Approver.drawio.png",
                    "uc-chair.png": "trustcabinet-use-cases-Chair.drawio.png",
                    "uc-secretary.png": "trustcabinet-use-cases-Secretary.drawio.png",
                    "uc-participant.png": "trustcabinet-use-cases-Participant.drawio.png",
                    "uc-guest.png": "trustcabinet-use-cases-Guest.drawio.png",
                },
            ),
        ),
    ),
)

GROUPS_BY_KEY = {group.key: group for group in GROUPS}


def token() -> str:
    return drive_auth.access_token()


RETRYABLE_STATUS = {429, 500, 502, 503, 504}


def call(request: urllib.request.Request, attempts: int = 3) -> bytes:
    """Send one request, retrying the failures that are safe to retry.

    A dropped connection or a 5xx answer means the request may or may not have
    been applied, so callers that create something pass `attempts=1` and let the
    user re-run instead: the next run sees the created folder or file and skips
    it. Reads and content updates are idempotent and retry here.
    """

    request.add_header("Authorization", f"Bearer {token()}")
    for attempt in range(1, attempts + 1):
        try:
            with urllib.request.urlopen(request, timeout=300) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            body = error.read().decode("utf-8", "replace")[:400]
            if error.code in RETRYABLE_STATUS and attempt < attempts:
                time.sleep(2 * attempt)
                continue
            raise SystemExit(f"Drive answered {error.code}: {body}") from error
        except urllib.error.URLError as error:
            if attempt < attempts:
                time.sleep(2 * attempt)
                continue
            raise SystemExit(
                f"Drive connection dropped ({error.reason}) after {attempt} attempt(s). "
                "Nothing further was written; re-run the same command to continue."
            ) from error
    raise SystemExit("Drive request failed; re-run the same command to continue.")


def call_json(request: urllib.request.Request, attempts: int = 3) -> dict:
    return json.loads(call(request, attempts))


def file_md5(path: Path) -> str:
    digest = hashlib.md5(usedforsecurity=False)
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def mime_for(path: Path) -> str:
    return MIME_BY_SUFFIX.get(path.suffix.lower(), "application/octet-stream")


@dataclass(frozen=True)
class Entry:
    group: Group
    local: Path
    relative: PurePosixPath
    remote_dir: PurePosixPath
    remote_name: str
    size: int
    md5: str
    rename_from: str | None = None

    @property
    def remote_path(self) -> str:
        if not self.remote_dir.parts:
            return self.remote_name
        return str(self.remote_dir / self.remote_name)


def remote_path(source: Source, relative: PurePosixPath) -> PurePosixPath:
    mapped = source.file_map.get(relative.as_posix())
    if mapped:
        return PurePosixPath(mapped)

    parent = relative.parent.as_posix()
    parent = "" if parent == "." else parent
    parent = source.dir_map.get(parent, parent)
    parts = [part for part in (source.drive_dir, parent) if part]
    return PurePosixPath(*parts, relative.name)


def local_files(source: Source) -> list[Path]:
    if not source.local.is_dir():
        raise SystemExit(f"Local folder not found: {source.local}")

    found: dict[str, Path] = {}
    for pattern in source.patterns:
        paths = source.local.rglob(pattern) if source.recursive else source.local.glob(pattern)
        for path in paths:
            if path.is_file() and path.name != "README.md":
                found[path.as_posix()] = path
    return [found[key] for key in sorted(found)]


def plan(group: Group) -> list[Entry]:
    entries: list[Entry] = []
    for source in group.sources:
        base = source.local.resolve()
        for path in local_files(source):
            relative = PurePosixPath(path.resolve().relative_to(base).as_posix())
            target = remote_path(source, relative)
            entries.append(
                Entry(
                    group=group,
                    local=path,
                    relative=relative,
                    remote_dir=target.parent,
                    remote_name=target.name,
                    size=path.stat().st_size,
                    md5=file_md5(path),
                    rename_from=source.renames.get(relative.as_posix()),
                )
            )
    return entries


def select_entries(targets: list[str]) -> tuple[list[Entry], bool]:
    """Return the entries to sync and whether a path filter narrowed them."""

    if not targets:
        raise SystemExit("Name at least one target, or pass --all. Use --list to see the groups.")

    chosen: list[Entry] = []
    filters: list[str] = []
    whole_groups = True

    for target in targets:
        if target.lower() == "all":
            chosen = [entry for group in GROUPS for entry in plan(group)]
            filters.clear()
            whole_groups = True
            break

        head, _, rest = target.partition("/")
        group = GROUPS_BY_KEY.get(head.lower())
        if group is not None:
            chosen.extend(plan(group))
            if rest:
                filters.append(rest.lower())
                whole_groups = False
            continue

        filters.append(target.lower())
        whole_groups = False

    if not chosen:
        chosen = [entry for group in GROUPS for entry in plan(group)]

    if filters:
        chosen = [
            entry
            for entry in chosen
            if any(
                needle in entry.relative.as_posix().lower() or needle in entry.remote_path.lower()
                for needle in filters
            )
        ]

    if not chosen:
        raise SystemExit(f"No local diagram matches: {', '.join(targets)}")
    return chosen, whole_groups and not filters


class Drive:
    """A small Drive client with a per-run cache of folder children."""

    def __init__(self, create: bool) -> None:
        self.create = create
        self.children_cache: dict[str, list[dict]] = {}

    def children(self, folder_id: str) -> list[dict]:
        if folder_id in self.children_cache:
            return self.children_cache[folder_id]

        files: list[dict] = []
        page_token: str | None = None
        while True:
            params = {
                "q": f"'{folder_id}' in parents and trashed = false",
                "fields": "nextPageToken,files(id,name,mimeType,md5Checksum,size)",
                "pageSize": "1000",
                "supportsAllDrives": "true",
            }
            if page_token:
                params["pageToken"] = page_token
            data = call_json(
                urllib.request.Request(f"{API}/files?{urllib.parse.urlencode(params)}")
            )
            files.extend(data.get("files", []))
            page_token = data.get("nextPageToken")
            if not page_token:
                break

        self.children_cache[folder_id] = files
        return files

    def folder(self, root_id: str, path: PurePosixPath) -> str | None:
        folder_id = root_id
        for part in path.parts:
            match = next(
                (
                    item
                    for item in self.children(folder_id)
                    if item.get("name") == part and item.get("mimeType") == FOLDER_MIME
                ),
                None,
            )
            if match is None:
                if not self.create:
                    return None
                match = call_json(
                    urllib.request.Request(
                        f"{API}/files?supportsAllDrives=true&fields=id,name,mimeType",
                        data=json.dumps(
                            {"name": part, "mimeType": FOLDER_MIME, "parents": [folder_id]}
                        ).encode("utf-8"),
                        method="POST",
                        headers={"Content-Type": "application/json; charset=UTF-8"},
                    ),
                    attempts=1,
                )
                self.children(folder_id).append(match)
            folder_id = match["id"]
        return folder_id

    def find_file(self, folder_id: str, name: str) -> dict | None:
        return next(
            (
                item
                for item in self.children(folder_id)
                if item.get("name") == name and item.get("mimeType") != FOLDER_MIME
            ),
            None,
        )

    def create_file(self, folder_id: str, name: str, path: Path) -> dict:
        boundary = f"trustcabinet-{uuid.uuid4().hex}"
        metadata = {"name": name, "parents": [folder_id]}
        body = b"".join(
            [
                f"--{boundary}\r\n".encode(),
                b"Content-Type: application/json; charset=UTF-8\r\n\r\n",
                json.dumps(metadata).encode("utf-8"),
                b"\r\n",
                f"--{boundary}\r\n".encode(),
                f"Content-Type: {mime_for(path)}\r\n\r\n".encode(),
                path.read_bytes(),
                b"\r\n",
                f"--{boundary}--\r\n".encode(),
            ]
        )
        request = urllib.request.Request(
            f"{UPLOAD}/files?uploadType=multipart&supportsAllDrives=true"
            "&fields=id,name,md5Checksum,size,modifiedTime,parents",
            data=body,
            method="POST",
            headers={"Content-Type": f"multipart/related; boundary={boundary}"},
        )
        return call_json(request, attempts=1)

    def update_file(self, file_id: str, path: Path) -> dict:
        request = urllib.request.Request(
            f"{UPLOAD}/files/{file_id}?uploadType=media&supportsAllDrives=true"
            "&fields=id,name,md5Checksum,size,modifiedTime,parents",
            data=path.read_bytes(),
            method="PATCH",
            headers={"Content-Type": mime_for(path)},
        )
        return call_json(request)

    def rename(self, file_id: str, name: str) -> dict:
        return call_json(
            urllib.request.Request(
                f"{API}/files/{file_id}?supportsAllDrives=true&fields=id,name,md5Checksum",
                data=json.dumps({"name": name}).encode("utf-8"),
                method="PATCH",
                headers={"Content-Type": "application/json; charset=UTF-8"},
            )
        )

    def metadata(self, file_id: str) -> dict:
        fields = "id,name,mimeType,md5Checksum,size,modifiedTime,parents"
        return call_json(
            urllib.request.Request(
                f"{API}/files/{file_id}?supportsAllDrives=true"
                f"&fields={urllib.parse.quote(fields)}"
            )
        )

    def trash(self, file_id: str) -> None:
        call(
            urllib.request.Request(
                f"{API}/files/{file_id}?supportsAllDrives=true",
                data=json.dumps({"trashed": True}).encode("utf-8"),
                method="PATCH",
                headers={"Content-Type": "application/json; charset=UTF-8"},
            )
        )


@dataclass
class Action:
    entry: Entry
    kind: str  # unchanged | update | create
    file_id: str | None = None
    rename_to: str | None = None


def build_actions(entries: list[Entry], drive: Drive) -> list[Action]:
    actions: list[Action] = []
    for entry in entries:
        folder_id = drive.folder(entry.group.drive_folder, entry.remote_dir)
        if folder_id is None:
            actions.append(Action(entry, "create"))
            continue

        current = drive.find_file(folder_id, entry.remote_name)
        rename_to = None
        if current is None and entry.rename_from:
            current = drive.find_file(folder_id, entry.rename_from)
            if current is not None:
                rename_to = entry.remote_name
        if current is None:
            actions.append(Action(entry, "create"))
            continue

        kind = "unchanged" if current.get("md5Checksum") == entry.md5 else "update"
        actions.append(Action(entry, kind, current["id"], rename_to))
    return actions


def scan_remote_only(entries: list[Entry], drive: Drive) -> list[str]:
    """Files in a mapped Drive folder that no local diagram produced."""

    managed: dict[tuple[str, str], set[str]] = {}
    for entry in entries:
        folder_id = drive.folder(entry.group.drive_folder, entry.remote_dir)
        if folder_id is None:
            continue
        managed.setdefault((entry.group.key, folder_id), set()).add(entry.remote_name)
        if entry.rename_from:
            managed[(entry.group.key, folder_id)].add(entry.rename_from)

    extras: list[str] = []
    seen: set[tuple[str, str]] = set()
    for entry in entries:
        folder_id = drive.folder(entry.group.drive_folder, entry.remote_dir)
        if folder_id is None:
            continue
        key = (entry.group.key, folder_id)
        if key in seen:
            continue
        seen.add(key)
        for item in drive.children(folder_id):
            if item.get("mimeType") == FOLDER_MIME:
                continue
            if item.get("name") not in managed[key]:
                remote_dir = entry.remote_dir
                path = item.get("name", "?") if not remote_dir.parts else str(remote_dir / item.get("name", "?"))
                extras.append(f"{entry.group.key}: {path}")
    return sorted(extras)


def describe(action: Action) -> str:
    prefix = {
        "unchanged": "same    ",
        "update": "update  ",
        "create": "create  ",
    }[action.kind]
    suffix = f'  [rename from "{action.entry.rename_from}"]' if action.rename_to else ""
    return f"  {prefix} {action.entry.remote_path}  ({action.entry.size:,} bytes){suffix}"


def list_groups() -> None:
    print("Drive folder: https://drive.google.com/drive/folders/18E52RJTqrrd3j9D9ROJllVC2kAvtOHJx\n")
    for group in GROUPS:
        entries = plan(group)
        print(f"{group.key:<11} {group.title:<16} {len(entries):>3} file(s)")
        for source in group.sources:
            try:
                shown = source.local.relative_to(ROOT)
            except ValueError:
                shown = source.local
            print(f"            {shown}")
        print(f"            https://drive.google.com/drive/folders/{group.drive_folder}")
        print()


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "targets",
        nargs="*",
        help="group name (sequence, class, erd, …), <group>/<path fragment>, a path fragment, or all",
    )
    parser.add_argument("--all", action="store_true", help="every group")
    parser.add_argument("--list", action="store_true", help="show groups and local folders, no Drive call")
    parser.add_argument("--check", action="store_true", help="compare with Drive and exit 1 on drift")
    parser.add_argument("--confirm", action="store_true", help="actually write to Drive")
    parser.add_argument(
        "--prune",
        action="store_true",
        help="with --confirm, move Drive-only files in mapped folders to the trash",
    )
    args = parser.parse_args()

    if args.list:
        list_groups()
        return 0

    if args.check and args.confirm:
        raise SystemExit("--check and --confirm cannot be combined.")

    targets = list(args.targets)
    if args.all:
        targets.append("all")
    entries, whole_groups = select_entries(targets)

    seen: dict[str, Entry] = {}
    for entry in entries:
        key = f"{entry.group.key}:{entry.remote_path}"
        if key in seen:
            raise SystemExit(
                f"Two local files map to the same Drive file: {seen[key].local} and {entry.local}"
            )
        seen[key] = entry

    drive = Drive(create=args.confirm)
    actions = build_actions(entries, drive)

    print(f"Plan: {len(entries)} local file(s)")
    by_group: dict[str, list[Action]] = {}
    for action in actions:
        by_group.setdefault(action.entry.group.key, []).append(action)
    for key, group_actions in by_group.items():
        print(f"\n[{key}] {GROUPS_BY_KEY[key].title}")
        for action in group_actions:
            print(describe(action))

    created = sum(1 for action in actions if action.kind == "create")
    updated = sum(1 for action in actions if action.kind == "update")
    unchanged = sum(1 for action in actions if action.kind == "unchanged")
    renamed = sum(1 for action in actions if action.rename_to)
    print(
        f"\nSummary: {created} create, {updated} update, "
        f"{unchanged} unchanged, {renamed} rename"
    )

    extras = scan_remote_only(entries, drive) if whole_groups else []
    if extras:
        print(f"\nOn Drive but not in the repo ({len(extras)}):")
        for extra in extras:
            print(f"  remote-only  {extra}")
        if not (args.prune and args.confirm):
            print("  (left alone; --prune --confirm moves them to the trash)")

    if not args.confirm:
        if args.check:
            if created or updated or renamed:
                print("\nOUT OF DATE: Drive does not match the local diagrams.")
                return 1
            print("\nUP TO DATE: Drive matches the local diagrams.")
            return 0
        print("\nDry run. Re-run with --confirm to write, --check to compare.")
        return 0

    for action in actions:
        if action.kind == "unchanged" and not action.rename_to:
            continue
        entry = action.entry
        folder_id = drive.folder(entry.group.drive_folder, entry.remote_dir)
        if folder_id is None:
            raise SystemExit(f"Drive folder disappeared while writing: {entry.remote_path}")

        if action.kind == "create":
            result = drive.create_file(folder_id, entry.remote_name, entry.local)
            action.file_id = result.get("id")
            print(f"created  {entry.group.key}: {entry.remote_path}")
        elif action.kind == "update":
            result = drive.update_file(action.file_id or "", entry.local)
            print(f"updated  {entry.group.key}: {entry.remote_path}")
        else:
            result = None

        if result is not None:
            if not result.get("md5Checksum"):
                result = drive.metadata(action.file_id or result["id"])
            if result.get("md5Checksum") != entry.md5:
                raise SystemExit(
                    f"Drive did not store the local bytes for {entry.remote_path}; stop and check the target."
                )

        if action.rename_to:
            drive.rename(action.file_id or "", action.rename_to)
            print(f"renamed  {entry.group.key}: {entry.remote_path} -> {action.rename_to}")

    if args.prune:
        for extra in extras:
            group_key, path = extra.split(": ", 1)
            entry = next(item for item in entries if item.group.key == group_key)
            target = PurePosixPath(path)
            folder_id = drive.folder(entry.group.drive_folder, target.parent)
            current = drive.find_file(folder_id, target.name) if folder_id else None
            if current is None:
                continue
            drive.trash(current["id"])
            print(f"trashed  {extra}")

    print("\nOK: Drive now matches the local diagrams.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
