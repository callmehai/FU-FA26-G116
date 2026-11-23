#!/usr/bin/env python3
"""Build the blind grading workbooks for the AI-draft evaluation on real meetings.

    uv run --with openpyxl tests/ai/real/kit.py --drafts tests/ai/out/real/drafts --ref tests/ai/out/real/y-chuan.xlsx

Reads every `<slug>.json` that export.py wrote (--exports, default tests/ai/out/real) and every draft
`<slug>-<provider>-<run>.md` in --drafts. Writes into --out (default tests/ai/out/real/kit):

- `cham-loc.xlsx`, `cham-khanh.xlsx` (one per --graders name): identical content. Per meeting a facts sheet and the
  frozen transcript; per meeting x label x run one sheet of atomic claims (auto-check, top-3 transcript lines, dropdowns
  for the label, the error source and wrong name/number) and one "Ý chuẩn" sheet of the BA's reference points to mark
  Có / Một phần / Không.
- `key.json`: which provider each label A/B is, per meeting, drawn at random once and reused on every later run. Keep it
  from the graders.

Claim splitting, auto-check and candidate lines are deterministic and unit-tested (tests/test_kit.py). openpyxl is needed
only to write the workbooks.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import math
import pathlib
import random
import re
import string
import sys
import unicodedata
from collections import Counter

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent / "out" / "real"
VIETNAM = dt.timezone(dt.timedelta(hours=7))

LABELS = ["Đúng", "Sai lệch", "Bịa", "Không kiểm được"]
SOURCES = ["Gỡ băng", "AI"]
WRONG = ["Tên", "Số", "Tên và số"]
COVERAGE = ["Có", "Một phần", "Không"]
CLAIM_HEADERS = ["#", "Mục", "Ý (claim)", "Tự kiểm", "Lời thoại gần nhất (giờ · người nói · lời)", "Nhãn", "Nguồn lỗi",
                 "Sai tên/số", "Bằng chứng", "Ghi chú"]
REF_HEADERS = ["#", "Ý chính", "Loại", "Người nói", "Mốc giờ", "Mức quan trọng", "Ghi chú BA", "Có trong dự thảo",
               "Bằng chứng", "Ghi chú"]

# ---------------------------------------------------------------------------------------------------------------- text


def fold(text: str) -> str:
    """Lower case without accents (đ -> d): ASR rarely agrees with the model on tones."""
    out = []
    for c in unicodedata.normalize("NFD", text):
        if unicodedata.category(c) == "Mn":
            continue
        out.append("d" if c in "đĐ" else c.lower())
    return "".join(out)


def words(text: str) -> list[str]:
    return re.findall(r"[0-9]+(?:[.,][0-9]+)*%?|[^\W\d_]+", text)


STOP = set(fold("""và là của có được cho các những một này đó thì mà với trong để đã sẽ đang không cũng như khi ra vào
lên về theo từ tại do nên nếu vì bởi rằng thì ở đây kia ấy nào gì ai ta mình bạn em anh chị ông bà thầy cô người việc
cái con nhiều ít rất lại còn chỉ đều hơn nữa vẫn đi đến sau trước hay hoặc nhưng thế vậy à ạ ừ ờ nhé nha thôi rồi chứ
chưa""").split())

# Words that make a clause carry its own statement; deliberately short and plain (see split_clauses).
VERBS = [fold(v) for v in """đã|sẽ|đang|cần|phải|được|bị|là|có|không|chưa|nêu|đề xuất|đề nghị|thống nhất|báo cáo|trình bày|
giao|yêu cầu|cho biết|cho rằng|đồng ý|nhất trí|chịu trách nhiệm|hoàn thành|triển khai|kiểm tra|thực hiện|chuẩn bị|
xây dựng|cập nhật|gửi|làm|sửa|test|hỏi|trả lời|giải thích|phát biểu|kết luận|quyết định|lưu ý|nhấn mạnh|bổ sung|ghi nhận|
phân công|đánh giá|xem xét|tham gia|sử dụng|dùng|chạy|chọn|đổi|thêm|xoá|xóa|tạo|viết|nói|muốn|thử|ghi|đọc|nghe|xem|
mở|đóng|lưu|tải|bấm|nhận|đạt|giảm|tăng|vượt|mất|còn""".replace("\n", "").split("|")]
ABBREVIATIONS = {"tp", "v.v", "vv", "ths", "ts", "pgs", "gs", "đ/c", "dc", "st", "mr", "ms", "dr", "tr", "q", "p"}
SECTION = re.compile(r"^\s*(#{1,6})\s+(.*)$")
NUMBERED = re.compile(r"^\s*((?:\d+|[IVX]+)[.)])\s+(.*)$")
BULLET = re.compile(r"^\s*(?:[-*•+–]|[a-zđ][.)])\s+(.*)$")


def is_verbish(text: str) -> bool:
    folded = " " + " ".join(fold(w) for w in words(text)) + " "
    folded = folded.replace(" nguoi noi ", " ")  # "Người nói 3" names a voice, it says nothing
    return any(f" {v} " in folded for v in VERBS)


def word_count(text: str) -> int:
    return len(words(text))


def split_sentences(text: str) -> list[str]:
    parts, start = [], 0
    for match in re.finditer(r"[.!?…]+(?=\s+\S)", text):
        before = text[start:match.start()].split()
        last = fold(before[-1]) if before else ""
        if last.rstrip(".") in ABBREVIATIONS:
            continue
        parts.append(text[start:match.end()])
        start = match.end()
    parts.append(text[start:])
    return [p.strip() for p in parts if p.strip()]


def join_on(text: str, connector: str) -> list[str]:
    """Split on `connector` only where both sides are clauses of their own (a verb and three words each)."""
    pieces = text.split(connector)
    out = [pieces[0]]
    for piece in pieces[1:]:
        if is_verbish(out[-1]) and is_verbish(piece) and word_count(out[-1]) >= 3 and word_count(piece) >= 3:
            out.append(piece)
        else:
            out[-1] = out[-1] + connector + piece
    return out


def split_clauses(sentence: str) -> list[str]:
    parts = [p for p in re.split(r";\s+", sentence)]
    for connector in (", đồng thời ", " đồng thời ", " và "):
        parts = [q for p in parts for q in join_on(p, connector)]
    cleaned = []
    for p in parts:
        p = p.strip().strip(";").strip()
        if p and re.search(r"[^\W\d_]", p):
            cleaned.append(p[0].upper() + p[1:] if p[0].islower() and cleaned else p)
    return cleaned


def split_claims(markdown: str) -> list[dict]:
    """A draft as atomic claims: [{"n", "section", "text"}]. Headings set the section and are not claims themselves.

    A numbered line is a heading (an agenda item, "1. Nội dung thảo luận") when it is short, has no sentence punctuation
    and the next line is a bullet or indented; otherwise it is an item of a numbered list and its text is split."""
    claims, top, sub = [], "", ""
    raw_lines = [r for r in markdown.splitlines() if r.strip()]
    for index, raw in enumerate(raw_lines):
        line = raw.strip()
        if m := SECTION.match(line):
            top, sub = m.group(2).strip(), ""
            continue
        body = line
        if m := NUMBERED.match(line):
            rest = m.group(2).strip()
            following = raw_lines[index + 1] if index + 1 < len(raw_lines) else ""
            opens_block = following[:1].isspace() or BULLET.match(following) is not None
            if opens_block and word_count(rest) <= 15 and not re.search(r"[.!?;]$", rest) \
                    and not re.search(r"[.!?;]\s", rest):
                sub = f"{m.group(1)} {rest}"
                continue
            body = rest
        elif m := BULLET.match(line):
            body = m.group(1).strip()
        for sentence in split_sentences(body):
            for clause in split_clauses(sentence):
                claims.append({"n": len(claims) + 1, "section": " › ".join(x for x in (top, sub) if x), "text": clause})
    return claims


# ---------------------------------------------------------------------------------------------------------- auto-check

HONORIFICS = ["đồng chí", "chủ toạ", "chủ tọa", "thư ký", "anh", "chị", "ông", "bà", "bạn", "em", "thầy", "cô", "đ/c"]
SPEECH_VERBS = ["nêu", "cho biết", "cho rằng", "đề nghị", "đề xuất", "báo cáo", "trình bày", "phát biểu", "hỏi", "trả lời",
                "xác nhận", "giải thích", "nhấn mạnh", "góp ý", "bổ sung", "kết luận", "đồng ý", "nói"]
SURNAMES = set("""Nguyễn Trần Lê Phạm Hoàng Huỳnh Phan Vũ Võ Đặng Bùi Đỗ Hồ Ngô Dương Lý Đào Đoàn Vương Trịnh Trương Lâm
Mai Đinh Tô Hà Tạ Lương Cao Chu Kiều Thái Tăng Lưu Quách Phùng Từ Văn Triệu Khúc Mạc Hứa La Diệp Thân""".split())
MEETING_TIME_WORDS = ["bat dau", "ket thuc", "khai mac", "be mac", "dien ra", "hop luc", "to chuc luc", "cuoc hop luc"]


def local(value: str | None) -> dt.datetime | None:
    return None if not value else dt.datetime.fromisoformat(value).astimezone(VIETNAM)


def facts_of(meeting: dict) -> dict:
    """The system facts a claim can be checked against, from export.py's JSON."""
    m = meeting["meeting"]
    times = []
    for label, key in (("bắt đầu dự kiến", "plannedStart"), ("kết thúc dự kiến", "plannedEnd"),
                       ("bắt đầu thực tế", "actualStart"), ("kết thúc thực tế", "actualEnd")):
        if at := local(m.get(key)):
            times.append((label, at))
    for a in meeting["agenda"]:
        for label, key in (("bắt đầu", "start"), ("kết thúc", "end")):
            if at := local(a.get(key)):
                times.append((f"mục {a['position']} {label}", at))
    for v in meeting["votes"]:
        for label, key in (("mở", "opened"), ("đóng", "closed")):
            if at := local(v.get(key)):
                times.append((f"biểu quyết '{v['title']}' {label}", at))
    line_times = [local(line["at"]) for line in meeting["transcript"]]
    if line_times:
        times.append(("lời thoại đầu tiên", min(line_times)))
        times.append(("lời thoại cuối cùng", max(line_times)))
    attendees = [{"name": p["name"], "role": p["role"], "attendance": p["attendance"]}
                 for p in meeting["participants"] if p["current"]]
    attendees += [{"name": g["name"], "role": "Guest", "attendance": g["attendance"]} for g in meeting["guests"] if g["current"]]
    speakers = sorted({line["speaker"] for line in meeting["transcript"]})
    presenters = [a["presenter"] for a in meeting["agenda"] if a.get("presenter")]
    dates = {at.date() for _, at in times}
    return {"title": m["title"], "times": times, "dates": dates, "attendees": attendees, "speakers": speakers,
            "presenters": presenters, "agenda": meeting["agenda"], "votes": meeting["votes"],
            "documents": [d["fileName"] for d in meeting["documents"]]}


def find_times(text: str) -> list[tuple[int, int]]:
    found = []
    for m in re.finditer(r"(?<![\d/:.,])(\d{1,2})\s*(?:giờ|h|:)\s*(\d{2})?(?:\s*phút)?(?![\d/])", text, re.IGNORECASE):
        hour, minute = int(m.group(1)), int(m.group(2) or 0)
        if hour < 24 and minute < 60:
            found.append((hour, minute))
    return found


def find_dates(text: str) -> list[tuple[int, int, int | None]]:
    found = []
    for m in re.finditer(r"(?<![\d/])(\d{1,2})\s*/\s*(\d{1,2})(?:\s*/\s*(\d{4}))?(?![\d/])", text):
        day, month = int(m.group(1)), int(m.group(2))
        if 1 <= day <= 31 and 1 <= month <= 12:
            found.append((day, month, int(m.group(3)) if m.group(3) else None))
    return found


def is_cap(word: str) -> bool:
    return word[0].isupper() and (len(word) == 1 or word[1:].islower())


def find_names(text: str) -> list[str]:
    """Person names a draft states: a run of capitalised words after an honorific ("anh Hải"), starting with a common
    surname ("Trần Việt Hải"), or of two or more words followed by a speech verb ("Trường Huy nêu")."""
    tokens = [(m.group(0), m.start(), m.end()) for m in re.finditer(r"[^\W\d_]+(?:/[^\W\d_]+)?", text)]
    names, i = [], 0
    while i < len(tokens):
        if not is_cap(tokens[i][0]):
            i += 1
            continue
        j = i + 1
        while j < len(tokens) and j - i < 5 and is_cap(tokens[j][0]) and text[tokens[j - 1][2]:tokens[j][1]].isspace():
            j += 1
        run = [t[0] for t in tokens[i:j]]
        before = text[:tokens[i][1]].rstrip().casefold()
        if run[0].casefold() in HONORIFICS:
            run, before = run[1:], run[0].casefold()
        rest = text[tokens[j - 1][2]:].lstrip().casefold()
        i = j
        if not run or run[0] in ("Người",) or len(run) > 4:
            continue
        honorific = any(before == h or before.endswith(" " + h) for h in HONORIFICS)
        speaker = len(run) >= 2 and any(rest.startswith(v + " ") or rest == v for v in SPEECH_VERBS)
        if honorific or (run[0] in SURNAMES and len(run) >= 2) or speaker:
            name = " ".join(run)
            if name not in names:
                names.append(name)
    return names


def match_name(name: str, known: list[str]) -> tuple[str, str | None]:
    """("exact" | "tone" | "none", the known name): a given name matches a full name ending with it."""
    parts = name.casefold().split()
    for full in known:
        tail = full.casefold().split()[-len(parts):]
        if tail == parts or full.casefold() == name.casefold():
            return "exact", full
    folded = [fold(p) for p in parts]
    for full in known:
        if [fold(p) for p in full.split()[-len(parts):]] == folded:
            return "tone", full
    return "none", None


def content_tokens(text: str) -> list[str]:
    return [t for t in (fold(w) for w in words(text)) if t not in STOP]


def overlap(a: str, b: str) -> float:
    """Share of a's content words that are in b."""
    ta, tb = content_tokens(a), set(content_tokens(b))
    return sum(1 for t in ta if t in tb) / len(ta) if ta else 0.0


ROLE_VI = {"Chair": "Chủ toạ", "Secretary": "Thư ký", "Member": "Thành viên", "Guest": "Khách mời"}


def auto_check(claim: str, facts: dict) -> str:
    """"Khớp dữ liệu: …", "Không khớp: …" or "" — for times, names, attendance, votes, conclusions, agenda, documents."""
    ok, bad = [], []
    folded = fold(claim)
    negated = re.search(r"\b(khong|chua)\b", folded) is not None

    # Times of the meeting itself (a deadline elsewhere is not checked unless it matches a known time).
    about_meeting = any(w in folded for w in MEETING_TIME_WORDS)
    for hour, minute in find_times(claim):
        distance = {label: abs((at.hour * 60 + at.minute) - (hour * 60 + minute)) for label, at in facts["times"]}
        hits = sorted((label for label, d in distance.items() if d <= 5), key=distance.get)
        if hits:
            ok.append(f"{hour:02d}:{minute:02d} = {hits[0]}")
        elif about_meeting:
            known = ", ".join(f"{label} {at:%H:%M}" for label, at in facts["times"][:4])
            bad.append(f"{hour:02d}:{minute:02d} không trùng giờ nào trong dữ liệu ({known})")
    for day, month, year in find_dates(claim):
        if any(d.day == day and d.month == month and (year is None or d.year == year) for d in facts["dates"]) \
                or f"{day}/{month}" in facts["title"] or f"{day:02d}/{month:02d}" in facts["title"]:
            ok.append(f"ngày {day}/{month} khớp")
        elif about_meeting or re.search(r"\bhop\b", folded):
            bad.append(f"ngày {day}/{month} không phải ngày họp ({', '.join(f'{d:%d/%m}' for d in sorted(facts['dates']))})")

    # Names: attendees first, then anyone heard in the transcript or presenting an item.
    attendee_names = [a["name"] for a in facts["attendees"]]
    others = [n for n in facts["speakers"] + facts["presenters"] if not n.startswith("Người nói")]
    for name in find_names(claim):
        kind, full = match_name(name, attendee_names)
        if kind == "exact":
            role = next(a["role"] for a in facts["attendees"] if a["name"] == full)
            ok.append(f"{name} = {full} ({ROLE_VI.get(role, role)})")
            continue
        kind2, full2 = match_name(name, others)
        if kind2 == "exact":
            ok.append(f"{name} có trong lời thoại/chương trình")
        elif kind == "tone" or kind2 == "tone":
            bad.append(f"'{name}' sai dấu so với '{full or full2}'")
        else:
            bad.append(f"không có '{name}' trong danh sách tham dự hay người nói")
    for m in re.finditer(r"Người nói (\d+)", claim):
        label = f"Người nói {m.group(1)}"
        (ok if label in facts["speakers"] else bad).append(
            f"{label} {'có' if label in facts['speakers'] else 'không có'} trong bản gỡ băng")

    # Attendance counts.
    if re.search(r"tham du|co mat|vang mat|\bvang\b", folded):
        marked = [a for a in facts["attendees"] if a["attendance"] != "Unknown"]
        present = sum(a["attendance"] == "Present" for a in facts["attendees"])
        total = len(facts["attendees"])
        for m in re.finditer(r"(\d+)\s*(?:/\s*(\d+))?\s*(?:người|thành viên|đại biểu|thành phần|bạn)", claim):
            n, of = int(m.group(1)), m.group(2) and int(m.group(2))
            if not marked:
                # Nobody was marked present or absent: only the size of the invitation list can be checked.
                (ok if n == total and of in (None, total) else bad).append(
                    f"{m.group(0)}: danh sách mời {total} người, chưa điểm danh")
            elif (n == present or n == total and "moi" in folded) and of in (None, total):
                ok.append(f"{m.group(0)} khớp ({present}/{total} có mặt)")
            else:
                bad.append(f"{m.group(0)}: dữ liệu {present}/{total} có mặt")

    # Votes.
    if re.search(r"bieu quyet|bo phieu|\bphieu\b", folded):
        if not facts["votes"]:
            if not negated:
                bad.append("hệ thống không có biểu quyết nào")
        else:
            vote = max(facts["votes"], key=lambda v: overlap(v["title"], claim))
            counts = {o["count"] for o in vote["options"]}
            tally = ", ".join(f"{o['label']} {o['count']}" for o in vote["options"])
            for m in re.finditer(r"(\d+)\s*phiếu", claim):
                hit = int(m.group(1)) in counts
                (ok if hit else bad).append(f"{m.group(0)} {'khớp' if hit else 'không khớp'} '{vote['title']}' ({tally})")
            said = "NotPassed" if "khong thong qua" in folded else "Deferred" if "hoan" in folded \
                else "Passed" if "thong qua" in folded else None
            if said:
                (ok if said == vote["conclusion"] else bad).append(
                    f"kết luận '{vote['title']}' trong dữ liệu: {vote['conclusion'] or 'chưa ghi'}")

    # Conclusions written on agenda items, agenda titles, documents.
    for a in facts["agenda"]:
        if a.get("conclusion") and word_count(claim) >= 4 and overlap(claim, a["conclusion"]) >= 0.6:
            ok.append(f"trùng kết luận đã ghi của mục {a['position']}")
        if a["title"] and fold(a["title"]) in folded:
            ok.append(f"mục {a['position']} '{a['title']}' có trong chương trình")
    for m in re.finditer(r"[\w\-. ]+\.(?:pdf|docx?|xlsx?|pptx?)\b", claim, re.IGNORECASE):
        name = m.group(0).strip()
        hit = any(fold(name).endswith(fold(d)) or fold(d) in fold(name) for d in facts["documents"])
        (ok if hit else bad).append(f"tài liệu '{name}' {'có' if hit else 'không có'} trong cuộc họp")

    if bad:
        return "Không khớp: " + "; ".join(bad) + (" | khớp: " + "; ".join(ok) if ok else "")
    if ok:
        return "Khớp dữ liệu: " + "; ".join(ok)
    return ""


# ----------------------------------------------------------------------------------------------------------- candidates


# Digits the model writes for numbers the transcript spells out ("năm phút" -> "5 phút"), as extra tokens.
NUMBER_WORDS = {"mot": "1", "hai": "2", "ba": "3", "bon": "4", "tu": "4", "nam": "5", "lam": "5", "sau": "6", "bay": "7",
                "tam": "8", "chin": "9", "muoi": "10", "tram": "100", "nghin": "1000"}


def grams(text: str) -> list[str]:
    toks = [fold(w) for w in words(text)]
    content = [t for t in toks if t not in STOP] + [NUMBER_WORDS[t] for t in toks if t in NUMBER_WORDS]
    return content + [f"{a}_{b}" for a, b in zip(toks, toks[1:]) if not (a in STOP and b in STOP)]


class Candidates:
    """BM25 over transcript lines (accent-folded words + word pairs); numbers and names in the claim weigh double."""

    def __init__(self, lines: list[dict]):
        self.lines = lines
        self.docs = [Counter(grams(f"{line['speaker']} {line['text']}")) for line in lines]
        self.avg = sum(sum(d.values()) for d in self.docs) / max(1, len(self.docs))
        df = Counter(t for d in self.docs for t in d)
        n = len(self.docs)
        self.idf = {t: math.log(1 + (n - c + 0.5) / (c + 0.5)) for t, c in df.items()}

    def top(self, claim: str, k: int = 3) -> list[dict]:
        query = Counter(grams(claim))
        boost = {fold(w) for w in words(claim) if w[0].isdigit() or w[0].isupper()}
        scored = []
        for line, doc in zip(self.lines, self.docs):
            length = sum(doc.values())
            score = 0.0
            for t, q in query.items():
                if t not in doc:
                    continue
                tf = doc[t]
                weight = self.idf[t] * tf * 2.2 / (tf + 1.2 * (0.25 + 0.75 * length / self.avg))
                score += weight * (2 if t in boost else 1) * (1.5 if "_" in t else 1)
            if score > 0:
                scored.append((score, line["n"], line))
        scored.sort(key=lambda s: (-s[0], s[1]))
        return [line for _, _, line in scored[:k]]


def describe(line: dict, limit: int = 300) -> str:
    where = f"{line['clock']}" + (f" (bản ghi {line['recordingOffset']})" if line.get("recordingOffset") else "")
    text = line["text"] if len(line["text"]) <= limit else line["text"][:limit] + "…"
    return f"[{where}] {line['speaker']}: {text}"


# ---------------------------------------------------------------------------------------------------------------- inputs


def load_exports(folder: pathlib.Path) -> dict[str, dict]:
    meetings = {}
    for path in sorted(folder.glob("*.json")):
        try:
            doc = json.loads(path.read_text())
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        if isinstance(doc, dict) and doc.get("kind") == "trustcabinet-real-meeting":
            meetings[doc["slug"]] = doc
    return dict(sorted(meetings.items(), key=lambda kv: kv[1]["meeting"]["actualStart"] or kv[1]["meeting"]["plannedStart"]))


def parse_draft_name(name: str, slugs: list[str]) -> tuple[str, str, int] | None:
    """`<slug>-<provider>-<run>.md`, the slug being one of the exported meetings (the longest that fits)."""
    m = re.fullmatch(r"(.+)-(\d+)\.md", name)
    if not m:
        return None
    for slug in sorted(slugs, key=len, reverse=True):
        if m.group(1).startswith(slug + "-") and len(m.group(1)) > len(slug) + 1:
            return slug, m.group(1)[len(slug) + 1:], int(m.group(2))
    return None


def load_drafts(folder: pathlib.Path, slugs: list[str]) -> list[dict]:
    drafts = []
    for path in sorted(folder.glob("*.md")):
        parsed = parse_draft_name(path.name, slugs)
        if not parsed:
            print(f"skip {path.name}: not <meeting>-<provider>-<run>.md for an exported meeting", file=sys.stderr)
            continue
        meta_path = path.with_suffix(".json")
        meta = json.loads(meta_path.read_text()) if meta_path.exists() else {}
        drafts.append({"file": path.name, "meeting": parsed[0], "provider": parsed[1], "run": parsed[2],
                       "text": path.read_text(), "meta": meta})
    return drafts


def assign_labels(drafts: list[dict], meetings: dict, key_path: pathlib.Path, seed: int | None) -> dict:
    """A/B per meeting, drawn once; key.json keeps it so every later run (and score.py) reads the same mapping."""
    key = json.loads(key_path.read_text()) if key_path.exists() else {"meetings": {}}
    key.setdefault("seed", seed if seed is not None else random.SystemRandom().randrange(10**9))
    rng = random.Random(key["seed"])
    for number, slug in enumerate(meetings, 1):
        entry = key["meetings"].setdefault(slug, {"code": f"M{number}", "labels": {}})
        providers = sorted({d["provider"] for d in drafts if d["meeting"] == slug})
        new = [p for p in providers if p not in entry["labels"].values()]
        rng.shuffle(new)
        free = [c for c in string.ascii_uppercase if c not in entry["labels"]]
        for provider, label in zip(new, free):
            entry["labels"][label] = provider
        entry["title"] = meetings[slug]["meeting"]["title"]
    key["drafts"] = []
    for d in drafts:
        entry = key["meetings"][d["meeting"]]
        d["label"] = next(label for label, p in entry["labels"].items() if p == d["provider"])
        d["code"] = entry["code"]
        d["sheet"] = f"{d['code']}-{d['label']}{d['run']}"
        key["drafts"].append({k: d[k] for k in ("file", "meeting", "provider", "run", "label", "sheet")})
    key_path.write_text(json.dumps(key, ensure_ascii=False, indent=1))
    return key


def match_sheet(meeting: dict, sheets: list[str], overrides: dict[str, str]) -> str | None:
    """The BA's sheet for a meeting: --sheet slug=Name, else the sheet named "DD-MM …" for its day with most title words."""
    if meeting["slug"] in overrides:
        return overrides[meeting["slug"]]
    start = local(meeting["meeting"]["actualStart"] or meeting["meeting"]["plannedStart"])
    day = f"{start:%d-%m}"
    same_day = [s for s in sheets if s.strip().startswith(day)]
    if not same_day:
        return None
    title = set(content_tokens(meeting["meeting"]["title"]))
    return max(same_day, key=lambda s: len(title & set(content_tokens(s))))


def load_reference(path: pathlib.Path | None, meetings: dict, overrides: dict[str, str]) -> dict[str, list[dict]]:
    """The BA's blind key points: # | Ý chính | Loại | Người nói | Mốc giờ | Mức quan trọng | Ghi chú, one sheet a meeting."""
    if path is None:
        return {}
    from openpyxl import load_workbook
    book = load_workbook(path, read_only=True, data_only=True)
    reference = {}
    for slug, meeting in meetings.items():
        sheet = match_sheet(meeting, book.sheetnames, overrides)
        if sheet is None or sheet not in book.sheetnames:
            print(f"{slug}: no reference sheet found in {path.name}", file=sys.stderr)
            continue
        rows = list(book[sheet].iter_rows(values_only=True))
        header = next((i for i, r in enumerate(rows) if r and any(str(c or "").strip() == "Ý chính" for c in r)), None)
        if header is None:
            continue
        names = [str(c or "").strip() for c in rows[header]]
        points = []
        for r in rows[header + 1:]:
            row = {names[i]: ("" if c is None else str(c).strip()) for i, c in enumerate(r) if i < len(names)}
            if row.get("Ý chính"):
                points.append(row | {"#": row.get("#") or str(len(points) + 1)})
        reference[slug] = points
        print(f"{slug}: {len(points)} reference points from sheet '{sheet}'")
    return reference


# --------------------------------------------------------------------------------------------------------------- workbook


def write_workbook(path: pathlib.Path, meetings: dict, drafts: list[dict], key: dict, reference: dict,
                   rows: dict[str, list[dict]]) -> None:
    from openpyxl import Workbook
    from openpyxl.styles import Alignment, Font, PatternFill
    from openpyxl.worksheet.datavalidation import DataValidation

    wrap = Alignment(wrap_text=True, vertical="top")
    bold = Font(bold=True)
    head_fill = PatternFill("solid", fgColor="DCE6F1")
    book = Workbook()
    guide = book.active
    guide.title = "Hướng dẫn"
    guide_lines = [
        "Chấm độc lập, không trao đổi với người chấm kia cho đến khi nộp. Không đoán nhà cung cấp: A/B được xáo riêng từng cuộc họp.",
        "Mỗi dòng ở sheet Mx-Ay là một ý (claim) của dự thảo. Một dòng chứa hai ý thì chèn dòng, đánh số 5a, 5b… và chép nửa sau sang.",
        "Nhãn: Đúng = khớp bản ghi/dữ liệu hệ thống; Sai lệch = có thật nhưng sai chi tiết (tên, số, giờ, ai nói, kết luận);",
        "Bịa = không có trong bản ghi lẫn dữ liệu; Không kiểm được = không nghe rõ/không đủ căn cứ để kết luận.",
        "Nguồn lỗi (chỉ cho Sai lệch/Bịa): Gỡ băng = bản gỡ băng đã sai và AI chép theo; AI = bản gỡ băng đúng mà AI viết sai.",
        "Sai tên/số: điền khi lỗi nằm ở tên người hoặc con số (kể cả giờ, ngày, số phiếu).",
        "Bằng chứng: giờ trong bản ghi (vd 14:23:05, mốc bản ghi 00:12:31) hoặc bản ghi hệ thống (mục nghị sự, điểm danh, biểu quyết).",
        "Cột Tự kiểm và Lời thoại gần nhất chỉ là gợi ý của máy; nhãn cuối cùng phải dựa trên nghe lại bản ghi khi lời thoại có thể sai.",
        "Sheet 'Mx-Ay Ý chuẩn': với từng ý chính BA ghi, đánh dấu dự thảo đó Có / Một phần / Không nhắc tới.",
        "",
        "Cuộc họp:",
    ]
    for slug, entry in key["meetings"].items():
        if slug not in meetings:
            continue
        m = meetings[slug]["meeting"]
        start = local(m["actualStart"] or m["plannedStart"])
        guide_lines.append(f"{entry['code']}: {m['title']} — {start:%d/%m/%Y %H:%M}, "
                           f"{len(meetings[slug]['transcript'])} dòng lời thoại (đóng băng {meetings[slug]['exportedAt'][:16]})")
    for line in guide_lines:
        guide.append([line])
    guide.column_dimensions["A"].width = 160

    def header(sheet, names, widths):
        sheet.append(names)
        for cell in sheet[1]:
            cell.font, cell.fill, cell.alignment = bold, head_fill, wrap
        for col, width in zip(string.ascii_uppercase, widths):
            sheet.column_dimensions[col].width = width
        sheet.freeze_panes = "A2"

    def dropdown(sheet, column, options, last):
        dv = DataValidation(type="list", formula1='"' + ",".join(options) + '"', allow_blank=True)
        sheet.add_data_validation(dv)
        dv.add(f"{column}2:{column}{last}")

    for slug, meeting in meetings.items():
        code = key["meetings"][slug]["code"]
        facts_sheet = book.create_sheet(f"{code} Dữ liệu")
        header(facts_sheet, ["Loại", "Nội dung"], [22, 140])
        m = meeting["meeting"]
        def put(kind, text):
            facts_sheet.append([kind, text])
        put("Tên cuộc họp", m["title"])
        put("Hình thức / mức mật", f"{m['mode']} / {m['sensitivity']}")
        for label, key_name in (("Dự kiến", "plannedStart"), ("Dự kiến kết thúc", "plannedEnd"),
                                ("Bắt đầu thực tế", "actualStart"), ("Kết thúc thực tế", "actualEnd")):
            at = local(m.get(key_name))
            put(label, f"{at:%d/%m/%Y %H:%M:%S}" if at else "—")
        for p in meeting["participants"]:
            put("Thành phần", f"{p['name']} — {ROLE_VI.get(p['role'], p['role'])} — điểm danh: {p['attendance']}"
                + ("" if p["current"] else " (đã rút khỏi danh sách)"))
        for g in meeting["guests"]:
            put("Khách mời", f"{g['name']} — điểm danh: {g['attendance']}" + ("" if g["current"] else " (đã thu hồi)"))
        for a in meeting["agenda"]:
            s, e = local(a.get("start")), local(a.get("end"))
            put(f"Mục {a['position']}", f"{a['title']} — trình bày: {a.get('presenter') or '—'} — "
                f"{s:%H:%M}–{e:%H:%M}" if s and e else f"{a['title']} — trình bày: {a.get('presenter') or '—'}")
            if a.get("conclusion"):
                put(f"Kết luận mục {a['position']}", a["conclusion"])
        for v in meeting["votes"]:
            options = ", ".join(f"{o['label']} {o['count']}" for o in v["options"])
            put("Biểu quyết", f"{v['title']} — {v['status']} — {options} — kết luận: {v['conclusion'] or '—'} {v['conclusionNote']}")
        for d in meeting["documents"]:
            put("Tài liệu", d["fileName"])
        if not meeting["agenda"]:
            put("Chương trình", "Không có mục nghị sự")
        if not meeting["votes"]:
            put("Biểu quyết", "Không có")
        for row in facts_sheet.iter_rows(min_row=2):
            for cell in row:
                cell.alignment = wrap

        transcript = book.create_sheet(f"{code} Gỡ băng")
        header(transcript, ["Dòng", "Giờ", "Mốc bản ghi", "Nguồn", "Người nói", "Lời thoại"], [7, 10, 12, 8, 22, 120])
        for line in meeting["transcript"]:
            transcript.append([line["n"], line["clock"], line.get("recordingOffset") or "",
                               "Mic phòng" if line["source"] == "room" else "Mic riêng", line["speaker"], line["text"]])
        for row in transcript.iter_rows(min_row=2, min_col=6, max_col=6):
            row[0].alignment = wrap

    for d in sorted(drafts, key=lambda d: d["sheet"]):
        claims = rows[d["file"]]
        sheet = book.create_sheet(d["sheet"])
        header(sheet, CLAIM_HEADERS, [6, 24, 60, 34, 70, 14, 12, 12, 30, 24])
        for c in claims:
            sheet.append([c["n"], c["section"], c["text"], c["check"], "\n".join(c["candidates"]), "", "", "", "", ""])
        last = max(len(claims) + 60, 200)
        dropdown(sheet, "F", LABELS, last)
        dropdown(sheet, "G", SOURCES, last)
        dropdown(sheet, "H", WRONG, last)
        for row in sheet.iter_rows(min_row=2, max_col=5):
            for cell in row:
                cell.alignment = wrap

        ref = book.create_sheet(f"{d['sheet']} Ý chuẩn")
        header(ref, REF_HEADERS, [6, 60, 14, 18, 10, 12, 24, 16, 30, 24])
        for p in reference.get(d["meeting"], []):
            ref.append([p.get("#"), p.get("Ý chính"), p.get("Loại"), p.get("Người nói"), p.get("Mốc giờ"),
                        p.get("Mức quan trọng"), p.get("Ghi chú"), "", "", ""])
        if not reference.get(d["meeting"]):
            ref.append(["", "(Chưa có danh sách ý chính của BA cho cuộc họp này: chạy lại kit.py với --ref)"])
        dropdown(ref, "H", COVERAGE, max(len(reference.get(d["meeting"], [])) + 20, 60))
        for row in ref.iter_rows(min_row=2, max_col=7):
            for cell in row:
                cell.alignment = wrap
    book.save(path)


# ------------------------------------------------------------------------------------------------------------------ main


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--exports", default=str(OUT), help="folder with export.py's <slug>.json")
    parser.add_argument("--drafts", required=True, help="folder with <slug>-<provider>-<run>.md (+ .json meta)")
    parser.add_argument("--ref", help="the BA's filled reference workbook")
    parser.add_argument("--sheet", action="append", default=[], help="slug=Sheet name, when the day+title guess fails")
    parser.add_argument("--out", default=str(OUT / "kit"))
    parser.add_argument("--graders", default="loc,khanh")
    parser.add_argument("--seed", type=int, help="only for the first run; key.json keeps it afterwards")
    args = parser.parse_args()

    meetings = load_exports(pathlib.Path(args.exports))
    if not meetings:
        sys.exit(f"no export.py JSON in {args.exports}")
    drafts = load_drafts(pathlib.Path(args.drafts), list(meetings))
    if not drafts:
        sys.exit(f"no drafts in {args.drafts}")
    meetings = {slug: m for slug, m in meetings.items() if any(d["meeting"] == slug for d in drafts)}
    out = pathlib.Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    key = assign_labels(drafts, meetings, out / "key.json", args.seed)
    overrides = dict(item.split("=", 1) for item in args.sheet)
    reference = load_reference(pathlib.Path(args.ref) if args.ref else None, meetings, overrides)

    rows = {}
    for slug, meeting in meetings.items():
        facts, finder = facts_of(meeting), Candidates(meeting["transcript"])
        changed = {d["meta"].get("transcriptChangedAt") for d in drafts if d["meeting"] == slug} - {None}
        if len(changed) > 1:
            print(f"WARNING {slug}: drafts were written from different transcript versions: {sorted(changed)}", file=sys.stderr)
        for d in (d for d in drafts if d["meeting"] == slug):
            claims = split_claims(d["text"])
            for c in claims:
                c["check"] = auto_check(c["text"], facts)
                c["candidates"] = [describe(line) for line in finder.top(c["text"])]
            rows[d["file"]] = claims
            checks = Counter(c["check"].split(":")[0] for c in claims if c["check"])
            print(f"{d['file']} -> {d['sheet']}: {len(claims)} claims, auto-check {checks.get('Khớp dữ liệu', 0)} khớp, "
                  f"{checks.get('Không khớp', 0)} không khớp, {len(claims) - sum(checks.values())} trống")
    for grader in args.graders.split(","):
        path = out / f"cham-{grader.strip()}.xlsx"
        write_workbook(path, meetings, drafts, key, reference, rows)
        print(f"wrote {path}")
    print(f"wrote {out / 'key.json'} (keep it from the graders)")


if __name__ == "__main__":
    main()
