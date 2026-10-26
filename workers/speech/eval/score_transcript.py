"""Score the worker on one real recording against a labelled transcript of it.

The transcript is plain text, one turn per line, as meeting tools export it:

    [00:37] Người nói 3: Vâng. Cái mùng 9 tháng 2 là đưa môi giới lên đâu?

(.docx → text on macOS: `textutil -convert txt file.docx`.) A turn lasts until the next one starts.
Both files are personal data: keep them out of the repo.

Reports WER, the share of each reference speaker's turns that was transcribed at all, speaker
accuracy (each worker label stands for the reference speaker it overlaps most), the confusion
between speakers, and every segment whose speaker disagrees with the reference.

    python -I score_transcript.py meeting.mp3 meeting.txt --url http://127.0.0.1:8090
"""

import argparse
import json
import pathlib
import re
import time
import urllib.request

DIGITS = "không một hai ba bốn năm sáu bảy tám chín".split()
FILLERS = {"ờ", "ừ", "ừm", "à", "ạ"}
TURN = re.compile(r"\[(\d+):(\d+)(?::(\d+))?\]\s*([^:]+):\s*(.*)")


def words(text):
    """Lower case, no punctuation or fillers, digits read out ("4.5" → "bốn chấm năm")."""
    text = re.sub(r"(\d)[.,](\d)", r"\1 chấm \2", text.lower())
    text = re.sub(r"\d", lambda m: f" {DIGITS[int(m[0])]} ", text)
    return [w for w in re.sub(r"[^\w\s]", " ", text).split() if w not in FILLERS]


def edits(ref, hyp):
    row = list(range(len(hyp) + 1))
    for i, r in enumerate(ref, 1):
        prev, row[0] = row[0], i
        for j, h in enumerate(hyp, 1):
            prev, row[j] = row[j], min(row[j] + 1, row[j - 1] + 1, prev + (r != h))
    return row[len(hyp)]


def parse(path):
    turns = []
    for line in path.read_text(encoding="utf-8").splitlines():
        m = TURN.match(line.strip())
        if m:
            h, mi, s = (int(m[1]), int(m[2]), int(m[3])) if m[3] else (0, int(m[1]), int(m[2]))
            turns.append({"start": h * 3600 + mi * 60 + s, "speaker": m[4].strip(), "text": m[5]})
    return turns


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("audio", type=pathlib.Path)
    ap.add_argument("transcript", type=pathlib.Path)
    ap.add_argument("--url", default="http://127.0.0.1:8090")
    args = ap.parse_args()

    turns = parse(args.transcript)
    started = time.perf_counter()
    request = urllib.request.Request(args.url.rstrip("/") + "/v1/transcribe?diarize=true",
                                     data=args.audio.read_bytes(), method="POST")
    with urllib.request.urlopen(request, timeout=7200) as response:
        result = json.loads(response.read())
    took = time.perf_counter() - started
    duration = result["durationMs"] / 1000
    for i, turn in enumerate(turns):
        turn["end"] = turns[i + 1]["start"] if i + 1 < len(turns) else duration

    # Everything on a 10 ms grid: who speaks per the reference, which label the worker gave.
    size = int(duration * 100) + 1
    ref, ours = [""] * size, [""] * size
    for turn in turns:
        for k in range(int(turn["start"] * 100), min(size, int(turn["end"] * 100))):
            ref[k] = turn["speaker"]
    for seg in result["segments"]:
        for k in range(seg["startMs"] // 10, min(size, seg["endMs"] // 10)):
            ours[k] = seg["speaker"] or "?"
    speakers = sorted({t["speaker"] for t in turns})
    mapping = {}
    for label in {o for o in ours if o}:
        counts = {s: sum(1 for k in range(size) if ours[k] == label and ref[k] == s) for s in speakers}
        mapping[label] = max(counts, key=counts.get)

    w = edits(words(" ".join(t["text"] for t in turns)), words(" ".join(s["text"] for s in result["segments"])))
    n = len(words(" ".join(t["text"] for t in turns)))
    print(f"{duration:.0f}s audio in {took:.1f}s (RTF {took / duration:.3f}); {len(result['speakers'])} voices "
          f"for {len(speakers)} reference speakers")
    print(f"WER {w / n:.1%} over {n} words")
    for s in speakers:
        theirs = [k for k in range(size) if ref[k] == s]
        print(f"  {s}: transcribed {sum(1 for k in theirs if ours[k]) / max(len(theirs), 1):.0%} of their turns")
    both = [k for k in range(size) if ref[k] and ours[k]]
    right = sum(1 for k in both if mapping[ours[k]] == ref[k])
    print(f"speaker accuracy {right / max(len(both), 1):.1%} of transcribed time; labels → {dict(sorted(mapping.items()))}")
    print("confusion (reference → as labelled), seconds:")
    for s in speakers:
        row = {t: sum(1 for k in both if ref[k] == s and mapping[ours[k]] == t) / 100 for t in speakers}
        print(f"  {s}: " + "  ".join(f"{t} {v:.0f}" for t, v in row.items()))
    for seg in result["segments"]:
        span = [ref[k] for k in range(seg["startMs"] // 10, min(size, seg["endMs"] // 10)) if ref[k]]
        truth = max(set(span), key=span.count) if span else None
        if truth and mapping[seg["speaker"] or "?"] != truth:
            print(f"  differs {seg['startMs'] / 1000:6.1f}–{seg['endMs'] / 1000:6.1f}s: labelled {mapping[seg['speaker'] or '?']},"
                  f" reference {truth}: {seg['text'][:60]}")


if __name__ == "__main__":
    main()
