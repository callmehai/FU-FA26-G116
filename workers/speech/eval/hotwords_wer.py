"""WER and CER of Gipformer on a room recording, with and without hotwords and other decoding knobs.

Runs the worker's own code in-process (no HTTP): the `room` profile's levelling, VAD, diarization and
near-silence drop cut each file into the same pieces the worker would transcribe, once; then every
decoding configuration recognises those pieces. Scored on the recogniser's raw words, before
punctuation; both sides go through the worker's own number writing (speech/numbers.py), so "hai mươi
ba" and "23" compare equal, and lose punctuation and the fillers "ờ ừ ừm à ạ".

    ../.venv/bin/python -I hotwords_wer.py --pair a.wav a.ref.txt --pair b.wav b.ref.txt \
        --config base --config hw=1.0 --config hw=1.5 --config hw=2.0 \
        --config paths=8 --config blank=0.5 --hyp whisper=a.whisper.txt,b.whisper.txt --out /tmp/hyps

`--config` takes comma-separated knobs: `hw=<score>` (hotwords on, shipped list or `--hotwords`), `words=<file>`
(another list for this configuration), `paths=<n>` (max_active_paths, default 4), `blank=<penalty>`
(blank_penalty, default 0), `model=<dir>` (another transducer: encoder*/decoder*/joiner*.onnx and tokens.txt, the int8
files when there are both); `base` is the worker's setting before hotwords. `--hyp name=<files>` scores another
recogniser's text (one file per pair, in order) as a reference point. `--out` writes each configuration's text, for reading against the reference;
`--cache` keeps the cut pieces between runs (they are audio: keep them outside the repo too).
`--profile clip --tsv list.tsv` scores many short sentences the way the per-mic profile cuts them instead,
e.g. the VIVOS test set (eval/README.md), as a check that a list does not hurt ordinary speech.

The reference and any `--out` text come from a real meeting: personal data, never committed (README).
"""

from __future__ import annotations

import argparse
import hashlib
import math
import os
import re
import sys
import time
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

os.environ["SPEECH_PUNCTUATION"] = "0"  # the reference is scored on words, so the tagger is not loaded
os.environ["SPEECH_NUMBERS"] = "0"
os.environ["SPEECH_HOTWORDS_FILE"] = "off"  # each configuration builds its own recogniser

from speech import hotwords, manifest, numbers, pipeline  # noqa: E402

FILLERS = {"ờ", "ừ", "ừm", "à", "ạ"}


def words(text: str) -> list[str]:
    """Lower case, numbers in digits as the worker writes them, no punctuation or fillers."""
    text = " ".join(text.lower().split())
    text = numbers.normalise_line(text)[0] if text else ""
    text = re.sub(r"(?<=\d)[.,](?=\d)", "", text)
    return [w for w in re.sub(r"[^\w\s]", " ", text).split() if w not in FILLERS]


def edits(ref: list, hyp: list) -> int:
    row = list(range(len(hyp) + 1))
    for i, r in enumerate(ref, 1):
        prev, row[0] = row[0], i
        for j, h in enumerate(hyp, 1):
            prev, row[j] = row[j], min(row[j] + 1, row[j - 1] + 1, prev + (r != h))
    return row[len(hyp)]


def score(refs: list[str], hyps: list[str]) -> tuple[float, float, int]:
    """(WER, CER, reference words) over all pairs together; CER on the letters of the words, spaces removed."""
    word_errors = word_total = char_errors = char_total = 0
    for ref, hyp in zip(refs, hyps):
        r, h = words(ref), words(hyp)
        word_errors += edits(r, h)
        word_total += len(r)
        char_errors += edits(list("".join(r)), list("".join(h)))
        char_total += len("".join(r))
    return word_errors / max(word_total, 1), char_errors / max(char_total, 1), word_total


def parse_config(spec: str) -> dict:
    knobs = {"hw": None, "words": None, "paths": 4, "blank": 0.0, "model": None}
    for part in filter(None, spec.split(",")):
        if part == "base":
            continue
        key, _, value = part.partition("=")
        if key not in knobs:
            raise SystemExit(f"unknown knob {key!r} in {spec!r}")
        knobs[key] = int(value) if key == "paths" else Path(value) if key in ("words", "model") else float(value)
    return knobs


def model_files(folder: Path) -> dict[str, Path]:
    """encoder/decoder/joiner of another transducer in `folder` (the int8 file when there is one)."""
    out = {}
    for part in ("encoder", "decoder", "joiner"):
        found = sorted(folder.glob(f"{part}*.onnx"), key=lambda p: (".int8." not in p.name, p.name))
        if not found:
            raise SystemExit(f"no {part}*.onnx in {folder}")
        out[part] = found[0]
    return out


def decode(recognizer, pieces: list) -> str:
    """The recogniser's raw text of one file's pieces, decoded as the worker does (RECOGNISE_BATCH at a time)."""
    texts = []
    for first in range(0, len(pieces), pipeline.RECOGNISE_BATCH):
        streams = []
        for piece in pieces[first : first + pipeline.RECOGNISE_BATCH]:
            stream = recognizer.create_stream()
            stream.accept_waveform(pipeline.SAMPLE_RATE, piece)
            streams.append(stream)
        recognizer.decode_streams(streams)
        texts.extend(stream.result.text for stream in streams)
    return " ".join(texts)


class Pieces:
    """Runs the worker's room profile up to the recogniser and keeps what it would recognise."""

    def __init__(self, engine: pipeline.Engine):
        self.engine = engine
        self.kept: list = []

    def __call__(self, audio):
        self.kept = list(audio)
        return [""] * len(audio), [None] * len(audio), [None] * len(audio)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pair", nargs=2, action="append", default=[], metavar=("AUDIO", "REF"))
    parser.add_argument("--tsv", type=Path, help="many short files: one 'audio<TAB>reference text' per line")
    parser.add_argument("--profile", choices=pipeline.PROFILES, default="room",
                        help="room: diarized room recording (default); clip: one sentence from a person's own mic")
    parser.add_argument("--config", action="append", default=[], help="see above; repeatable")
    parser.add_argument("--hotwords", type=Path, default=hotwords.DEFAULT_LIST)
    parser.add_argument("--hyp", action="append", default=[], metavar="NAME=FILES")
    parser.add_argument("--threads", type=int, default=int(os.environ.get("SPEECH_THREADS") or 2))
    parser.add_argument("--out", type=Path)
    parser.add_argument("--repeat", type=int, default=1, help="decode each configuration this many times, keep the fastest")
    parser.add_argument("--cache", type=Path, help="keep the cut pieces here (audio, so outside the repo)")
    args = parser.parse_args()

    pairs = [(audio, Path(ref).read_text(encoding="utf-8")) for audio, ref in args.pair]
    if args.tsv:
        for line in args.tsv.read_text(encoding="utf-8").splitlines():
            if line.strip():
                audio, _, text = line.partition("\t")
                pairs.append((str(args.tsv.parent / audio), text))
    if not pairs:
        parser.error("give --pair or --tsv")
    refs = [text for _, text in pairs]
    rows = []
    for spec in args.hyp:
        name, _, files = spec.partition("=")
        texts = [Path(path).read_text(encoding="utf-8") for path in files.split(",")]
        rows.append((name, *score(refs, texts), None))

    if args.config:
        root = manifest.models_dir()
        engine = pipeline.Engine(root, threads=args.threads)
        import sherpa_onnx

        pieces: list[list] = []
        durations = 0.0
        for audio, _ in pairs:
            samples = pipeline.decode(audio)
            durations += samples.size / pipeline.SAMPLE_RATE
            key = hashlib.sha256(samples.tobytes()).hexdigest()[:16]
            cached = args.cache / f"{key}{'' if args.profile == 'room' else '.' + args.profile}.npz" if args.cache else None
            if cached and cached.is_file():
                with np.load(cached) as stored:
                    pieces.append([stored[f"p{i}"] for i in range(len(stored.files))])
                continue
            catcher = Pieces(engine)
            engine._transcribe_pieces = catcher  # the pieces, not the text: each configuration recognises them
            engine._transcribe(samples, args.profile == "room", None, args.profile)
            pieces.append(catcher.kept)
            if cached:
                args.cache.mkdir(parents=True, exist_ok=True)
                np.savez(cached, **{f"p{i}": piece for i, piece in enumerate(catcher.kept)})

        for spec in args.config:
            knobs = parse_config(spec)
            options = {}
            if knobs["hw"] is not None:
                options = hotwords.recognizer_options(
                    hotwords.prepare(root, hotwords.Settings(knobs["words"] or args.hotwords, knobs["hw"])))
            files = model_files(knobs["model"]) if knobs["model"] else {
                part: root / path for part, path in pipeline.ASR_FILES.items()}
            tokens = knobs["model"] / "tokens.txt" if knobs["model"] else root / "gipformer/tokens.txt"
            recognizer = sherpa_onnx.OfflineRecognizer.from_transducer(
                encoder=str(files["encoder"]), decoder=str(files["decoder"]), joiner=str(files["joiner"]),
                tokens=str(tokens),
                num_threads=args.threads, sample_rate=pipeline.SAMPLE_RATE, feature_dim=80,
                decoding_method="modified_beam_search", max_active_paths=knobs["paths"],
                blank_penalty=knobs["blank"], **options,
            )
            elapsed = math.inf
            for _ in range(args.repeat):  # the fastest of a few runs: other work on the machine only slows one down
                started = time.monotonic()
                hyps = [decode(recognizer, file_pieces) for file_pieces in pieces]
                elapsed = min(elapsed, time.monotonic() - started)
            rtf = elapsed / durations
            label = re.sub(r"(words|model)=([^,]*)", lambda m: f"{m[1]}={Path(m[2]).name}", spec)
            rows.append((label, *score(refs, hyps), rtf))
            if args.out:
                args.out.mkdir(parents=True, exist_ok=True)
                for number, hyp in enumerate(hyps):
                    (args.out / f"{re.sub(r'[^\w.=-]', '_', label)}.{number}.txt").write_text(hyp + "\n", encoding="utf-8")

    print(f"{'configuration':<28} {'WER':>7} {'CER':>7} {'words':>6} {'RTF*':>6}")
    for name, wer, cer, total, rtf in rows:
        print(f"{name:<28} {wer:7.1%} {cer:7.1%} {total:6d} {'' if rtf is None else f'{rtf:6.3f}'}")
    print("* RTF of the recogniser alone, on this machine, with --threads threads")
    return 0


if __name__ == "__main__":
    sys.exit(main())
