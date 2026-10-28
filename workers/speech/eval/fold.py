"""The backend's crosstalk marking (SpeechClipFold.cs), in Python, so bleed.py can score it.

Keep the two in step: tests/test_fold_port.py runs the cases of SpeechClipFoldTests against this file.

A sentence on one person's mic is marked as "probably someone else's" segment by segment. A segment
is marked when another attendee's mic, at the clock offset within ±2 s that lines the two up best,
was speaking through at least 60 % of it, holds at least half of its words with at most 3 words the
louder copy never said, and was at least 6 dB louder there. Nothing is hidden or deleted: the
Secretary decides (AI-eCabinet's per-segment Fold, without its voice-sample arbitration).
"""

from __future__ import annotations

import math
import re
from dataclasses import dataclass, field

LAG_MS = 2000
COVER_PCT = 60
LEVEL_GAP_DB = 6.0
CONTAINMENT = 0.5
STRAY_WORDS = 3
WORD_SLACK_MS = 1000


@dataclass(frozen=True)
class Segment:
    start_ms: int
    end_ms: int
    words: int
    level_dbfs: float | None


@dataclass
class Clip:
    id: str
    person: str
    offset_ms: int  # when the sentence began, on the meeting clock
    duration_ms: int
    text: str
    segments: list[Segment]
    excluded: bool = False
    edited: bool = False


@dataclass
class Mark:
    leak_of: str | None = None  # the whole sentence reads as this clip's speaker
    spans: list[tuple[int, int, str]] = field(default_factory=list)  # (start ms, end ms, clip id), from the clip's start


_WORD = re.compile(r"[^\W_]+", re.UNICODE)


def tokens(text: str) -> dict[str, int]:
    counts: dict[str, int] = {}
    for word in _WORD.findall(text.lower()):
        counts[word] = counts.get(word, 0) + 1
    return counts


def measured(clip: Clip) -> bool:
    """Segments that account for every word of the text, as the worker left them."""
    if clip.edited or not clip.segments:
        return False
    if any(s.words < 0 or s.end_ms < s.start_ms for s in clip.segments):
        return False
    return sum(s.words for s in clip.segments) == len(clip.text.split())


def containment(q: dict[str, int], l: dict[str, int]) -> float:
    total = sum(q.values())
    return sum(min(n, l.get(w, 0)) for w, n in q.items()) / total if total else 0.0


def strays(q: dict[str, int], l: dict[str, int]) -> int:
    return sum(max(0, n - l.get(w, 0)) for w, n in q.items())


def best_lag(q0: int, q1: int, l: Clip) -> tuple[int, int]:
    candidates = [0]
    for t in l.segments:
        candidates += [l.offset_ms + t.start_ms - q0, l.offset_ms + t.end_ms - q1]
    lag, covered = 0, -1
    for c in candidates:
        c = max(-LAG_MS, min(LAG_MS, c))
        got = sum(max(0, min(q1 + c, l.offset_ms + t.end_ms) - max(q0 + c, l.offset_ms + t.start_ms)) for t in l.segments)
        if got > covered or (got == covered and abs(c) < abs(lag)):
            lag, covered = c, got
    return lag, covered


def level_between(l: Clip, start: int, end: int) -> float:
    power = weight = 0.0
    for t in l.segments:
        if t.level_dbfs is None or l.offset_ms + t.end_ms <= start or l.offset_ms + t.start_ms >= end:
            continue
        d = t.end_ms - t.start_ms
        power += d * 10 ** (t.level_dbfs / 10)
        weight += d
    return 10 * math.log10(power / weight) if weight else -math.inf


def words_between(l: Clip, start: int, end: int) -> dict[str, int]:
    words = l.text.split()
    said: list[str] = []
    at = 0
    for t in l.segments:
        if l.offset_ms + t.end_ms > start and l.offset_ms + t.start_ms < end:
            said += words[at : at + t.words]
        at += t.words
    return tokens(" ".join(said))


def leak_of(q: Clip, s: Segment, q_words: dict[str, int], clips: list[Clip]) -> str | None:
    q0, q1 = q.offset_ms + s.start_ms, q.offset_ms + s.end_ms
    best, best_gap = None, -math.inf
    for l in sorted(clips, key=lambda c: (c.offset_ms, c.id)):
        if l.person == q.person or l.excluded or not measured(l):
            continue
        if l.offset_ms > q1 + LAG_MS or l.offset_ms + l.duration_ms < q0 - LAG_MS:
            continue
        lag, covered = best_lag(q0, q1, l)
        if covered * 100 < COVER_PCT * (q1 - q0):
            continue
        said = words_between(l, q0 + lag - WORD_SLACK_MS, q1 + lag + WORD_SLACK_MS)
        if containment(q_words, said) < CONTAINMENT or strays(q_words, said) > STRAY_WORDS:
            continue
        if s.level_dbfs is None:
            continue
        gap = level_between(l, q0 + lag, q1 + lag) - s.level_dbfs
        if gap < LEVEL_GAP_DB:
            continue
        if best is None or gap > best_gap:
            best, best_gap = l.id, gap
    return best


def fold(clips: list[Clip]) -> dict[str, Mark]:
    out: dict[str, Mark] = {}
    for q in clips:
        if q.excluded or not measured(q):
            continue
        words = q.text.split()
        spans: list[tuple[int, int, str]] = []
        at = 0
        for s in q.segments:
            said = tokens(" ".join(words[at : at + s.words]))
            at += s.words
            if s.words == 0:
                continue
            if (of := leak_of(q, s, said, clips)) is not None:
                spans.append((s.start_ms, s.end_ms, of))
        if not spans:
            continue
        spoken = sum(1 for s in q.segments if s.words > 0)
        mark = Mark(spans=spans)
        if len(spans) == spoken:
            by: dict[str, int] = {}
            for start, end, of in spans:
                by[of] = by.get(of, 0) + end - start
            mark.leak_of = max(sorted(by), key=lambda k: by[k])
        out[q.id] = mark
    return out
