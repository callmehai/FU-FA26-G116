"""How many words one person's mic also catches from the others, with and without each fix (SCRUM-109).

Builds synthetic per-mic meetings from VIVOS: 2–3 attendees round a table, each on their own laptop,
every open mic hearing the others. Each mic is cut into sentences by the web app's own ClipCutter
(cut_clips.mjs), every sentence is transcribed by a running worker, and the backend's crosstalk
marking (fold.py) is applied. Run the worker first; audio and transcripts stay in --cache, outside
the repo.

    ../.venv/bin/python -I bleed.py vivos/test --cache /tmp/bleed --profile room --profile clip

Each mic hears its owner through a near room response at -26 dBFS and every other talker through a
far one, 3–30 dB quieter (drawn per pair) and 3–10 ms later, plus -66 dBFS of air-conditioning hum.
The browser's gain control is off, as useMicCapture asks. Clocks are off by up to 0.1 s per laptop.

Scenarios: S1 one talker at a time; S3 the next turn starts 1–4 s before the last one ends;
S4 quick replies, under the 1.2 s that ends a sentence.

Scored per reference word, as AI-eCabinet does (docs/research/2026-10-23-per-mic-crosstalk):
- duplicate: a word also written on another attendee's mic, not marked, while the owner's mic has it;
- misplaced: the same, but the owner's mic missed it;
- lost: a word of the mic's owner that the marking flagged as someone else's;
- recall: the share of each owner's words written, and not marked, on their own mic.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import random
import subprocess
import sys
import urllib.request
import wave
from dataclasses import dataclass
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import fold  # noqa: E402

SR = 16000
OWN_DBFS = -26.0  # --own-dbfs: -38 is a quiet speaker
NOISE_DBFS = -66.0
HERE = Path(__file__).resolve().parent


@dataclass(frozen=True)
class Utt:
    speaker: int
    start: float
    end: float
    words: tuple[str, ...]


def read_wav(path: Path) -> np.ndarray:
    with wave.open(str(path)) as w:
        assert w.getframerate() == SR and w.getnchannels() == 1 and w.getsampwidth() == 2, path
        return np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(np.float32) / 32768


def wav_bytes(samples: np.ndarray) -> bytes:
    pcm = (np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes()
    header = b"RIFF" + (36 + len(pcm)).to_bytes(4, "little") + b"WAVEfmt " + (16).to_bytes(4, "little")
    header += (1).to_bytes(2, "little") + (1).to_bytes(2, "little") + SR.to_bytes(4, "little")
    header += (SR * 2).to_bytes(4, "little") + (2).to_bytes(2, "little") + (16).to_bytes(2, "little")
    return header + b"data" + len(pcm).to_bytes(4, "little") + pcm


def rms_db(x: np.ndarray) -> float:
    return 10 * math.log10(float(np.mean(np.square(x, dtype=np.float64))) + 1e-12)


def room_response(rng: random.Random, rt60: float, drr_db: float) -> np.ndarray:
    """A direct path plus an exponentially decaying diffuse tail, `drr_db` below it in energy."""
    length = int(rt60 * SR)
    t = np.arange(length) / SR
    np_rng = np.random.default_rng(rng.randrange(1 << 30))
    tail = np_rng.standard_normal(length) * np.exp(-6.9 * t / rt60)
    tail[: int(0.002 * SR)] = 0
    tail *= math.sqrt(10 ** (-drr_db / 10) / float(np.sum(tail**2)))
    tail[0] = 1.0
    return tail.astype(np.float32)


class Corpus:
    def __init__(self, root: Path) -> None:
        prompts = {}
        for line in (root / "prompts.txt").read_text(encoding="utf-8").splitlines():
            key, _, text = line.partition(" ")
            prompts[key] = tuple(text.lower().split())
        self.speakers: dict[str, list[tuple[Path, tuple[str, ...]]]] = {}
        for path in sorted((root / "waves").glob("*/*.wav")):
            if path.stem in prompts:
                self.speakers.setdefault(path.parent.name, []).append((path, prompts[path.stem]))


def script(rng: random.Random, corpus: Corpus, people: list[str], scenario: str,
           own_dbfs: float = OWN_DBFS) -> tuple[list[Utt], list[np.ndarray]]:
    """Turns of 1–2 sentences each, alternating speakers, laid out on one timeline."""
    utts: list[Utt] = []
    audio: list[np.ndarray] = []
    at, last = 1.0, -1
    pools = {p: rng.sample(corpus.speakers[p], len(corpus.speakers[p])) for p in people}
    for _ in range(8):
        speaker = rng.choice([i for i in range(len(people)) if i != last])
        last = speaker
        if utts:
            if scenario == "S1":
                at = utts[-1].end + rng.uniform(1.5, 3.0)
            elif scenario == "S3":
                at = max(utts[-1].start + 0.5, utts[-1].end - rng.uniform(1.0, 4.0))
            else:  # S4: replies quicker than the pause that ends a sentence
                at = utts[-1].end + rng.uniform(0.2, 0.9)
        for _ in range(rng.choice((1, 2))):
            if not pools[people[speaker]]:
                break
            path, words = pools[people[speaker]].pop()
            samples = read_wav(path)
            samples *= 10 ** ((own_dbfs - rms_db(samples)) / 20)
            utts.append(Utt(speaker, at, at + samples.size / SR, words))
            audio.append(samples)
            at += samples.size / SR + rng.uniform(0.15, 0.5)
    return utts, audio


def mics(rng: random.Random, utts: list[Utt], audio: list[np.ndarray], count: int) -> list[np.ndarray]:
    length = int((max(u.end for u in utts) + 2.0) * SR)
    near = [room_response(rng, rng.uniform(0.35, 0.6), rng.uniform(6, 10)) for _ in range(count)]
    out = []
    for mic in range(count):
        track = np.zeros(length, np.float32)
        far = {t: (room_response(rng, rng.uniform(0.35, 0.6), rng.uniform(-3, 3)), rng.uniform(-30, -3),
                   rng.uniform(0.003, 0.010)) for t in range(count) if t != mic}
        for utt, samples in zip(utts, audio):
            if utt.speaker == mic:
                heard, delay = np.convolve(samples, near[mic])[: samples.size + near[mic].size], 0.0
            else:
                response, gain_db, delay = far[utt.speaker]
                heard = np.convolve(samples, response) * 10 ** (gain_db / 20)
            first = int((utt.start + delay) * SR)
            end = min(length, first + heard.size)
            track[first:end] += heard[: end - first]
        hum = np.cumsum(np.random.default_rng(rng.randrange(1 << 30)).standard_normal(length)).astype(np.float32)
        hum -= np.convolve(hum, np.ones(400) / 400, "same")
        hum *= 10 ** ((NOISE_DBFS - rms_db(hum)) / 20)
        out.append(track + hum)
    return out


def cut(track: np.ndarray) -> list[tuple[int, int]]:
    done = subprocess.run(["node", str(HERE / "cut_clips.mjs")], input=track.astype("<f4").tobytes(),
                          capture_output=True, check=True)
    return [tuple(c) for c in json.loads(done.stdout)]


def transcribe(url: str, profile: str, audio: bytes, cache: Path, key: str | None) -> dict:
    name = hashlib.sha256(audio + profile.encode() + (key or "").encode()).hexdigest()[:24] + ".json"
    hit = cache / name
    if hit.exists():
        return json.loads(hit.read_text())
    request = urllib.request.Request(f"{url}/v1/transcribe?diarize=false&profile={profile}", data=audio, method="POST",
                                     headers={"Content-Type": "audio/wav"})
    if key:
        request.add_header("Authorization", f"Bearer {key}")
    with urllib.request.urlopen(request, timeout=600) as response:
        result = json.loads(response.read())
    hit.write_text(json.dumps(result))
    return result


def owner_share(utts: list[Utt], mic: int, start: float, end: float) -> tuple[int | None, float]:
    """Who talked most during [start, end), and how much of it was the mic's owner."""
    by: dict[int, float] = {}
    for u in utts:
        overlap = min(end, u.end) - max(start, u.start)
        if overlap > 0:
            by[u.speaker] = by.get(u.speaker, 0) + overlap
    if not by:
        return None, 0.0
    talker = max(by, key=by.get)
    return talker, by.get(mic, 0.0) / sum(by.values())


def run(args: argparse.Namespace) -> None:
    corpus = Corpus(Path(args.vivos))
    cache = Path(args.cache)
    cache.mkdir(parents=True, exist_ok=True)
    totals: dict[tuple[str, str, bool], dict[str, float]] = {}
    for seed in args.seeds:
        for scenario in ("S1", "S3", "S4"):
            rng = random.Random(f"{seed}-{scenario}")
            people = rng.sample(sorted(corpus.speakers), rng.choice((2, 3)))
            utts, audio = script(rng, corpus, people, scenario, args.own_dbfs)
            tracks = mics(rng, utts, audio, len(people))
            skew = [rng.uniform(-0.1, 0.1) for _ in people]
            cuts = [cut(track) for track in tracks]
            reference = sum(len(u.words) for u in utts)
            for profile in args.profile:
                clips: list[fold.Clip] = []
                truth: dict[str, tuple[int, int | None, float]] = {}
                for mic, (track, found) in enumerate(zip(tracks, cuts)):
                    for number, (first, size) in enumerate(found):
                        result = transcribe(args.url, profile, wav_bytes(track[first : first + size]), cache, args.key)
                        segments = [s for s in result["segments"] if s["text"].strip()]
                        if not segments:
                            continue
                        clip_id = f"{mic}:{number}"
                        clips.append(fold.Clip(
                            clip_id, str(mic), round((first / SR + skew[mic]) * 1000), round(size / SR * 1000),
                            " ".join(s["text"] for s in segments),
                            [fold.Segment(s["startMs"], s["endMs"], len(s["text"].split()), s.get("levelDbfs"))
                             for s in segments]))
                        for index, s in enumerate(segments):
                            talker, own = owner_share(utts, mic, first / SR + s["startMs"] / 1000, first / SR + s["endMs"] / 1000)
                            truth[f"{clip_id}#{index}"] = (mic, talker, own)
                for marked in (False, True):
                    marks = fold.fold(clips) if marked else {}
                    row = totals.setdefault((scenario, profile, marked), {
                        "words": 0, "duplicate": 0, "misplaced": 0, "lost": 0, "own_heard": 0, "own_words": 0})
                    row["words"] += reference
                    owner_words: dict[int, set[str]] = {}
                    for clip in clips:
                        mic = int(clip.person)
                        words = clip.text.split()
                        at = 0
                        flagged = marks.get(clip.id)
                        for index, s in enumerate(clip.segments):
                            seg_words = words[at : at + s.words]
                            at += s.words
                            _, talker, own = truth[f"{clip.id}#{index}"]
                            is_marked = bool(flagged and any(a <= s.start_ms and s.end_ms <= b for a, b, _ in flagged.spans))
                            if not is_marked:
                                owner_words.setdefault(mic, set()).update(fold.tokens(" ".join(seg_words)))
                            if talker == mic or own >= 0.5:
                                if is_marked:
                                    row["lost"] += len(seg_words)
                            elif not is_marked and talker is not None:
                                said = {w for u in utts if u.speaker == talker for w in u.words}
                                heard_on_owner = sum(1 for w in fold.tokens(" ".join(seg_words)) if w in said)
                                row["duplicate"] += heard_on_owner
                                row["misplaced"] += len(seg_words) - heard_on_owner
                    for mic in range(len(people)):
                        own = [w for u in utts if u.speaker == mic for w in u.words]
                        row["own_words"] += len(own)
                        row["own_heard"] += sum(1 for w in own if w in owner_words.get(mic, set()))
    print(f"{'scenario':8} {'profile':7} {'marking':8} {'words':>6} {'duplicate':>10} {'misplaced':>10} {'lost':>7} {'recall':>7}")
    for (scenario, profile, marked), row in sorted(totals.items()) + sorted(_all(totals).items()):
        words = row["words"] or 1
        print(f"{scenario:8} {profile:7} {'on' if marked else 'off':8} {int(row['words']):6d} "
              f"{100 * row['duplicate'] / words:9.1f}% {100 * row['misplaced'] / words:9.1f}% "
              f"{100 * row['lost'] / words:6.2f}% {row['own_heard'] / max(1, row['own_words']):7.3f}")


def _all(totals: dict) -> dict:
    out: dict[tuple[str, str, bool], dict[str, float]] = {}
    for (_, profile, marked), row in totals.items():
        acc = out.setdefault(("ALL", profile, marked), {k: 0 for k in row})
        for k, v in row.items():
            acc[k] += v
    return out


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("vivos", help="VIVOS test split: prompts.txt and waves/<speaker>/*.wav")
    parser.add_argument("--cache", required=True)
    parser.add_argument("--url", default="http://127.0.0.1:8090")
    parser.add_argument("--key", default=os.environ.get("SPEECH_API_KEY"))
    parser.add_argument("--profile", action="append", choices=("room", "clip"), required=True)
    parser.add_argument("--seeds", type=int, nargs="+", default=[0, 1, 2, 3, 4])
    parser.add_argument("--own-dbfs", type=float, default=OWN_DBFS, help="each owner's level at their mic")
    run(parser.parse_args())


if __name__ == "__main__":
    main()
