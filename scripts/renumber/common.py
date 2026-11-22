"""Shared helpers for the one-off UC / BR / MSG renumbering."""
import csv
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAP_DIR = Path(__file__).resolve().parent / "mapping"

INVENTORY = ROOT / "docs/01-requirements/00-master-use-case-inventory.md"
BUSINESS_RULES = ROOT / "docs/01-requirements/06-business-rules.md"
MSG_CATALOGUE = ROOT / "docs/03-api/00-danh-muc-ma-thong-bao.md"

# Tokens. The lookbehind keeps us from matching inside longer identifiers.
UC_RE = re.compile(r"(?<![\w-])UC-(\d{2,3})([a-z]?)(?![\w])")
BR_RE = re.compile(r"(?<![\w-])BR-(\d{2,3})(?!\d)")
MSG_RE = re.compile(r"(?<![\w])MSG(\d{2,3})(?!\d)")
# C# constants such as Msg29Created; the digits sit between "Msg" and an upper-case letter.
MSGCONST_RE = re.compile(r"(?<![\w])Msg(\d{2,3})(?=[A-Z_])")
# Ranges written with a bare second number, e.g. "MSG147–151" or "UC-23a–d": need a human look.
RANGE_RE = re.compile(r"(?:MSG\d{2,3}|BR-\d{2,3}|UC-\d{2,3}[a-z]?)\s*[–-]\s*(?:\d{2,3}|[a-z])(?![\w-])")


def fmt(prefix: str, n: int) -> str:
    return f"{prefix}{n:02d}" if n < 100 else f"{prefix}{n}"


def write_csv(path: Path, rows, header):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)


def read_map(name: str) -> dict:
    """Return {old_number(int): new_number(int)}; dropped rows have an empty new number and are skipped."""
    out = {}
    with (MAP_DIR / f"{name}.csv").open(encoding="utf-8", newline="") as f:
        for r in csv.DictReader(f):
            if r["new"].strip():
                out[int(r["old"])] = int(r["new"])
    return out


def read_dropped(name: str) -> set:
    with (MAP_DIR / f"{name}.csv").open(encoding="utf-8", newline="") as f:
        return {int(r["old"]) for r in csv.DictReader(f) if not r["new"].strip()}
