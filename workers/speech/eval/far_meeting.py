"""Build long far-field meetings with exact truth from a speech corpus laid out like VIVOS.

The room recordings the worker struggles with are long (30–60 minutes), quiet (one laptop on the table, speech around
-40 dBFS), reverberant, noisy, with people talking over each other and a few people who say almost nothing. A real
meeting has no exact truth of who spoke when; stitching read utterances of known speakers into such a room gives it.
VIVOS train (46 speakers, ~19 minutes each, CC BY-NC-SA 4.0, academic use only; never commit it) has enough audio per
speaker for an hour.

Each meeting:

- `--people` speakers. One chair talks most; the others get random shares; `--rare` of them say only one to three
  short things (they must still come out as themselves, not folded into someone else).
- Turns of 1–4 utterances; a third of the turns are one short utterance (an interjection). Gaps of 0.1–1.2 s, and
  `--overlap` of the turns start before the previous one ended (crosstalk, up to 1.5 s).
- Each person sits at their own distance: level 0 to -15 dB, a synthetic room response (RT60 0.3–0.7 s) whose
  direct-to-reverberant ratio falls with distance; one person (`--laptop`) is heard through a laptop speaker (300–3400
  Hz band). Room noise (pink) at `--snr` dB below the speech, a murmur of other speakers 25 dB below, and the whole
  recording set to `--level` dBFS of speech.

Output, in the layout evaluate.py and meeting_bench.py read:

    <out>/meetings/<name>.wav + .json   {"people": [...], "turns": [{"start", "end", "person", "text"}]}

    python -I far_meeting.py <vivos>/train <out> --meetings 3 --minutes 30 --people 7 --seed 1
"""

from __future__ import annotations

import argparse
import json
import pathlib
import random
import subprocess

import numpy as np

RATE = 16000


def load(path: pathlib.Path) -> np.ndarray:
    """The utterance without the silence VIVOS leaves around it, so the truth's turns are only speech."""
    raw = subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-i", str(path), "-ac", "1", "-ar", str(RATE),
                          "-f", "f32le", "pipe:1"], check=True, capture_output=True).stdout
    samples = np.frombuffer(raw, dtype=np.float32)
    frames = samples[: samples.size // 160 * 160].reshape(-1, 160)
    if not frames.size:
        return samples
    level = 10 * np.log10(np.mean(frames.astype(np.float64) ** 2, axis=1) + 1e-12)
    voiced = np.flatnonzero(level > level.max() - 35)
    first, last = max(voiced[0] - 5, 0), min(voiced[-1] + 6, frames.shape[0])
    return samples[first * 160 : last * 160]


def save(path: pathlib.Path, samples: np.ndarray) -> None:
    pcm = (np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes()
    subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-y", "-f", "s16le", "-ar", str(RATE), "-ac", "1",
                    "-i", "pipe:0", str(path)], input=pcm, check=True)


def room_response(rng: np.random.Generator, rt60: float, drr_db: float) -> np.ndarray:
    """A direct path plus an exponentially decaying noise tail, `drr_db` louder than the tail's energy."""
    length = int(rt60 * RATE)
    t = np.arange(length) / RATE
    tail = rng.normal(0, 1, length) * np.exp(-6.9 * t / rt60)
    tail[: int(0.003 * RATE)] = 0  # first reflections arrive a few ms after the direct sound
    tail /= np.sqrt(np.sum(tail ** 2))
    response = tail * 10 ** (-drr_db / 20)
    response[0] = 1.0
    return response.astype(np.float32)


def convolve(signal: np.ndarray, response: np.ndarray, block: int = 1 << 18) -> np.ndarray:
    """Overlap-add FFT convolution, a block at a time so an hour of audio is never transformed whole."""
    size = 1 << int(np.ceil(np.log2(block + response.size)))
    spectrum = np.fft.rfft(response, size)
    out = np.zeros(signal.size + response.size, np.float32)
    for first in range(0, signal.size, block):
        part = signal[first : first + block]
        out[first : first + part.size + response.size - 1] += np.fft.irfft(np.fft.rfft(part, size) * spectrum, size)[
            : part.size + response.size - 1]
    return out[: signal.size]


def band(signal: np.ndarray, low: float, high: float) -> np.ndarray:
    spectrum = np.fft.rfft(signal)
    freqs = np.fft.rfftfreq(signal.size, 1 / RATE)
    spectrum[(freqs < low) | (freqs > high)] = 0
    return np.fft.irfft(spectrum, signal.size).astype(np.float32)


def pink(rng: np.random.Generator, size: int) -> np.ndarray:
    spectrum = np.fft.rfft(rng.normal(0, 1, size))
    freqs = np.fft.rfftfreq(size, 1 / RATE)
    spectrum[1:] /= np.sqrt(freqs[1:])
    spectrum[0] = 0
    noise = np.fft.irfft(spectrum, size)
    return (noise / np.std(noise)).astype(np.float32)


def dbfs_of_speech(signal: np.ndarray) -> float:
    """Median level of the 100 ms frames that carry speech (the loudest 60%), as a meter on the table would read."""
    frames = signal[: signal.size // 1600 * 1600].reshape(-1, 1600)
    level = 10 * np.log10(np.mean(frames.astype(np.float64) ** 2, axis=1) + 1e-12)
    return float(np.median(np.sort(level)[int(level.size * 0.4) :]))


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("corpus", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--meetings", type=int, default=3)
    ap.add_argument("--minutes", type=float, default=30)
    ap.add_argument("--people", type=int, default=7)
    ap.add_argument("--rare", type=int, default=1, help="people who say only one to three short things")
    ap.add_argument("--overlap", type=float, default=0.15, help="share of turns that start over the previous one")
    ap.add_argument("--snr", type=float, default=15)
    ap.add_argument("--level", type=float, default=-40, help="speech level of the recording, dBFS")
    ap.add_argument("--laptop", type=int, default=1, help="people heard through a laptop speaker")
    ap.add_argument("--seed", type=int, default=1)
    args = ap.parse_args()

    prompts = {}
    for line in (args.corpus / "prompts.txt").read_text(encoding="utf-8").splitlines():
        if line.strip():
            utt, text = line.split(" ", 1)
            prompts[utt] = text.strip().lower()
    utterances: dict[str, list[pathlib.Path]] = {}
    for wav in sorted((args.corpus / "waves").glob("*/*.wav")):
        utterances.setdefault(wav.parent.name, []).append(wav)
    everyone = sorted(utterances)
    (args.out / "meetings").mkdir(parents=True, exist_ok=True)

    for number in range(args.meetings):
        rng = random.Random(args.seed * 1000 + number)
        nrng = np.random.default_rng(args.seed * 1000 + number)
        present = rng.sample(everyone, args.people)
        murmur = [s for s in everyone if s not in present]
        pools = {p: rng.sample(utterances[p], len(utterances[p])) for p in present}
        lengths: dict[pathlib.Path, float] = {}

        def short_one(person: str) -> pathlib.Path | None:
            # An interjection: the shortest of a few candidates, so it lasts one to three seconds.
            pool = pools[person]
            if not pool:
                return None
            picks = pool[-6:]
            for wav in picks:
                lengths.setdefault(wav, wav.stat().st_size / (2 * RATE))
            best = min(picks, key=lengths.get)
            pool.remove(best)
            return best

        rare = present[-args.rare :] if args.rare else []
        shares = {p: (4.0 if i == 0 else rng.uniform(0.5, 2.0)) for i, p in enumerate(present) if p not in rare}
        rare_turns = {p: rng.randint(1, 3) for p in rare}
        queue: list[tuple[str, list[pathlib.Path]]] = []
        planned, previous = 0.0, None
        while planned < args.minutes * 60:
            names = [p for p in shares if p != previous and pools[p]]
            if not names:
                break
            person = rng.choices(names, weights=[shares[p] for p in names])[0]
            if rng.random() < 0.33:
                wav = short_one(person)
                utts = [wav] if wav else []
            else:
                utts = [pools[person].pop() for _ in range(min(len(pools[person]), rng.randint(1, 4)))]
            if utts:
                queue.append((person, utts))
                planned += sum(u.stat().st_size / (2 * RATE) for u in utts) + 0.6
                previous = person
        for person, count in rare_turns.items():
            for _ in range(count):
                wav = short_one(person)
                if wav:
                    queue.insert(rng.randint(1, len(queue)), (person, [wav]))

        # Lay the turns on a timeline, some over the end of the one before.
        placed, t = [], 0.0
        for person, utts in queue:
            clip = np.concatenate([load(u) for u in utts])
            if placed and rng.random() < args.overlap:
                start = max(placed[-1][1], t - rng.uniform(0.3, 1.5))
            else:
                start = t + rng.uniform(0.1, 1.2)
            placed.append((person, start, clip, " ".join(prompts.get(u.stem, "") for u in utts)))
            t = max(t, start + clip.size / RATE)
        total = int((t + 1.0) * RATE)

        # Each person through their own path to the recorder.
        distance = {p: (0.0 if i == 0 else rng.uniform(-15, -2)) for i, p in enumerate(present)}
        laptop = set(rng.sample(present[1:], min(args.laptop, len(present) - 1)))
        mix = np.zeros(total, np.float32)
        for person in present:
            track = np.zeros(total, np.float32)
            for who, start, clip, _ in placed:
                if who == person:
                    first = int(start * RATE)
                    track[first : first + clip.size] += clip / (np.sqrt(np.mean(clip ** 2)) + 1e-9) * 0.05
            if person in laptop:
                track = band(track, 300, 3400)
            drr = 8 + distance[person] * 0.8  # nearer people sound drier
            track = convolve(track, room_response(nrng, rng.uniform(0.3, 0.7), drr))
            mix += track * 10 ** (distance[person] / 20)
        speech_db = dbfs_of_speech(mix)

        # A murmur of other voices far away, and room noise.
        babble = np.zeros(total, np.float32)
        for other in rng.sample(murmur, min(4, len(murmur))):
            at = 0
            for wav in rng.sample(utterances[other], len(utterances[other])):
                if at >= total:
                    break
                clip = load(wav)
                clip = clip / (np.sqrt(np.mean(clip ** 2)) + 1e-9) * 0.05
                babble[at : at + clip.size] += clip[: total - at]
                at += clip.size + int(rng.uniform(0.2, 2.0) * RATE)
        babble = convolve(babble, room_response(nrng, 0.6, -6))
        babble *= 10 ** ((speech_db - 25) / 20) / (np.sqrt(np.mean(babble ** 2)) + 1e-9)
        noise = pink(nrng, total) * 10 ** ((speech_db - args.snr) / 20)
        mix = mix + babble + noise
        mix *= 10 ** ((args.level - dbfs_of_speech(mix)) / 20)

        name = f"f{args.seed:02d}{number + 1:02d}"
        save(args.out / "meetings" / f"{name}.wav", mix)
        turns = [{"start": round(start, 3), "end": round(start + clip.size / RATE, 3), "person": who, "text": text}
                 for who, start, clip, text in sorted(placed, key=lambda x: x[1])]
        (args.out / "meetings" / f"{name}.json").write_text(json.dumps(
            {"people": present, "rare": rare, "laptop": sorted(laptop), "turns": turns}, ensure_ascii=False, indent=1))
        speech_s = {p: round(sum(c.size for w, _, c, _ in placed if w == p) / RATE) for p in present}
        print(f"{name}: {t / 60:.1f} min, {len(turns)} turns, speech per person {speech_s}, rare {rare}, "
              f"laptop {sorted(laptop)}")


if __name__ == "__main__":
    main()
