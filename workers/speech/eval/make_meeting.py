"""Build a labelled test meeting from clips each person recorded on their own (SCRUM-35 evaluation).

Hand-labelling who spoke when in a real meeting takes hours; stitching individually recorded
utterances gives the exact truth for free. Input (personal data, never commit it):

    <clips>/<person>/<n>.wav (+ optional <n>.txt with what was said)

Output: <out>.wav (16 kHz mono) and <out>.json in the format evaluate.py reads. Turns come in a
shuffled order with 0.4–1.2 s gaps; --noise adds noise at the given SNR. --roster lists who may be
suggested (default: everyone in --people), so leaving someone out stands for a person who did not
consent.

    python -I make_meeting.py <clips> <dataset>/meetings/m01 --people lan minh hoa --seed 1 --noise 20
"""

import argparse
import json
import pathlib
import random
import subprocess

import numpy as np

RATE = 16000


def load(path):
    raw = subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-i", str(path), "-ac", "1", "-ar", str(RATE),
                          "-f", "f32le", "pipe:1"], check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("clips", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--people", nargs="+", required=True)
    ap.add_argument("--roster", nargs="*", help="who may be suggested (default: --people)")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--noise", type=float, help="SNR in dB of added noise; omit for clean audio")
    args = ap.parse_args()
    rng = random.Random(args.seed)

    queue = [(p, c) for p in args.people for c in sorted((args.clips / p).glob("*.wav"))]
    rng.shuffle(queue)
    audio, turns, t = [], [], 0.0
    for person, clip in queue:
        gap = rng.uniform(0.4, 1.2)
        audio.append(np.zeros(int(gap * RATE), dtype=np.float32))
        t += gap
        samples = load(clip)
        audio.append(samples)
        text = clip.with_suffix(".txt")
        turns.append({"start": round(t, 3), "end": round(t + len(samples) / RATE, 3), "person": person,
                      **({"text": text.read_text().strip()} if text.exists() else {})})
        t += len(samples) / RATE
    signal = np.concatenate(audio)
    if args.noise is not None:
        power = np.mean(signal ** 2)
        noise = np.random.default_rng(args.seed).normal(0, np.sqrt(power / 10 ** (args.noise / 10)), signal.shape)
        signal = np.clip(signal + noise.astype(np.float32), -1, 1)
    pcm = (signal * 32767).astype("<i2").tobytes()
    subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-y", "-f", "s16le", "-ar", str(RATE), "-ac", "1",
                    "-i", "pipe:0", str(args.out.with_suffix(".wav"))], input=pcm, check=True)
    args.out.with_suffix(".json").write_text(json.dumps({"people": args.roster or args.people, "turns": turns},
                                                        ensure_ascii=False, indent=1))
    print(f"{args.out.with_suffix('.wav')}: {t:.0f}s, {len(turns)} turns, {len(args.people)} people")


if __name__ == "__main__":
    main()
