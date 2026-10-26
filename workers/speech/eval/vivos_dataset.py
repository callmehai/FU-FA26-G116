"""Build a Voice ID test set from a speech corpus laid out like VIVOS (SCRUM-35 evaluation).

Input (VIVOS test split, CC BY-NC-SA 4.0, academic use only; never commit it):

    <corpus>/prompts.txt              "<SPEAKER>_<id> TEXT" per line
    <corpus>/waves/<SPEAKER>/*.wav

Output, in the layout evaluate.py reads:

    <out>/enrol/<speaker>/1.wav, 2.wav     two enrolment samples, 3–4 utterances each
    <out>/meetings/<name>.wav + .json      simulated meetings with ground truth

Each meeting has 4–6 speakers talking in turns of 1–3 utterances. One of them never enrolled
(stands for someone who did not consent: must never be named) and one enrolled person on the
roster does not speak (stands for an absent member: nobody may be named as them). Enrolment and
meeting utterances never overlap.

--channel room plays each meeting through a room instead of a clean line: echo, band limiting,
noise at 20 dB SNR and a 32 kbps mp3 round trip, while enrolment stays clean. That is the real
situation: people enrol on their own laptop, the meeting is recorded by one device on the table.

    python -I vivos_dataset.py <vivos>/test <out> --meetings 12 --seed 1 [--channel room]
"""

import argparse
import json
import pathlib
import random
import subprocess
import tempfile

import numpy as np

RATE = 16000
ROOM = "aecho=0.8:0.6:40|70:0.25|0.15,highpass=f=150,lowpass=f=3800"


def load(path):
    raw = subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-i", str(path), "-ac", "1", "-ar", str(RATE),
                          "-f", "f32le", "pipe:1"], check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def save(path, samples, through=None):
    """Write 16 kHz mono wav; `through` is an ffmpeg filter chain applied on the way."""
    pcm = (np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes()
    args = ["ffmpeg", "-nostdin", "-loglevel", "error", "-y", "-f", "s16le", "-ar", str(RATE), "-ac", "1", "-i", "pipe:0"]
    if through:
        args += ["-af", through]
    subprocess.run(args + ["-ar", str(RATE), "-ac", "1", str(path)], input=pcm, check=True)


def mp3_round_trip(path):
    with tempfile.TemporaryDirectory() as tmp:
        mp3 = pathlib.Path(tmp) / "x.mp3"
        subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-y", "-i", str(path), "-b:a", "32k", str(mp3)], check=True)
        subprocess.run(["ffmpeg", "-nostdin", "-loglevel", "error", "-y", "-i", str(mp3), "-ar", str(RATE), "-ac", "1",
                        str(path)], check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("corpus", type=pathlib.Path)
    ap.add_argument("out", type=pathlib.Path)
    ap.add_argument("--meetings", type=int, default=12)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--channel", choices=["clean", "room"], default="clean")
    args = ap.parse_args()
    rng = random.Random(args.seed)

    prompts = {}
    for line in (args.corpus / "prompts.txt").read_text(encoding="utf-8").splitlines():
        if line.strip():
            utt, text = line.split(" ", 1)
            prompts[utt] = text.strip().lower()
    utterances = {}
    for wav in sorted((args.corpus / "waves").glob("*/*.wav")):
        utterances.setdefault(wav.parent.name, []).append(wav)
    speakers = sorted(utterances)
    print(f"{len(speakers)} speakers, {sum(map(len, utterances.values()))} utterances")

    # Enrolment: two samples of 3–4 utterances each, taken first; the rest feeds the meetings.
    pool = {}
    for speaker in speakers:
        utts = utterances[speaker][:]
        rng.shuffle(utts)
        enrol_dir = args.out / "enrol" / speaker
        enrol_dir.mkdir(parents=True, exist_ok=True)
        for n in (1, 2):
            take = [utts.pop() for _ in range(rng.choice((3, 4)))]
            gap = np.zeros(int(0.3 * RATE), np.float32)
            save(enrol_dir / f"{n}.wav", np.concatenate([x for u in take for x in (load(u), gap)]))
        pool[speaker] = utts

    meetings_dir = args.out / "meetings"
    meetings_dir.mkdir(parents=True, exist_ok=True)
    for m in range(args.meetings):
        present = rng.sample(speakers, rng.choice((4, 5, 6)))
        stranger = present[-1]  # speaks, never enrolled for this meeting: must never be named
        absent = rng.choice([s for s in speakers if s not in present])  # on the roster, silent
        queue = []
        for speaker in present:
            utts = pool[speaker][:]
            rng.shuffle(utts)
            for _ in range(rng.choice((3, 4, 5))):
                if utts:
                    queue.append((speaker, [utts.pop() for _ in range(min(len(utts), rng.choice((1, 2, 3))))]))
        rng.shuffle(queue)
        audio, turns, t = [], [], 0.0
        for speaker, utts in queue:
            gap = rng.uniform(0.3, 1.0)
            audio.append(np.zeros(int(gap * RATE), np.float32))
            t += gap
            clip = np.concatenate([load(u) for u in utts])
            audio.append(clip)
            turns.append({"start": round(t, 3), "end": round(t + clip.size / RATE, 3), "person": speaker,
                          "text": " ".join(prompts.get(u.stem, "") for u in utts)})
            t += clip.size / RATE
        signal = np.concatenate(audio)
        name = f"m{m + 1:02d}"
        if args.channel == "room":
            power = np.mean(signal ** 2)
            noise = np.random.default_rng(args.seed + m).normal(0, np.sqrt(power / 100), signal.shape)  # 20 dB SNR
            save(meetings_dir / f"{name}.wav", signal + noise.astype(np.float32), through=ROOM)
            mp3_round_trip(meetings_dir / f"{name}.wav")
        else:
            save(meetings_dir / f"{name}.wav", signal)
        roster = [s for s in present if s != stranger] + [absent]
        (meetings_dir / f"{name}.json").write_text(json.dumps(
            {"people": roster, "stranger": stranger, "absent": absent, "turns": turns}, ensure_ascii=False, indent=1))
        print(f"{name}: {t:.0f}s, {len(turns)} turns, speakers {present} (stranger {stranger}), absent {absent}")


if __name__ == "__main__":
    main()
