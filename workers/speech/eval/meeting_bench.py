"""Transcription and speaker separation of whole meetings with exact truth, run in-process on the worker's own code.

Reads the meeting sets vivos_dataset.py, far_meeting.py and make_meeting.py write (`<set>/meetings/<name>.wav` plus
`.json` with the turns: start, end, person, text) and runs `Engine._transcribe` of the `room` profile on each, without
HTTP, so a knob of `speech/pipeline.py` can be changed per configuration (`--config name:KNOB=value,...`; the knob is
a module constant, e.g. `MIN_SPEAKER_S=15`, or `threshold=` for the clustering cut).

Per set it reports:

- WER and CER on the recogniser's words (punctuation off, numbers in words, as hotwords_wer.py scores them);
- labels: how many speakers the worker gave, how many of them hold at least 30 s, against the true number of people;
- DER: the share of speech time missed, falsely detected or given to the wrong person (NIST-style, no collar,
  each label mapped to at most one person, greedily by overlap, so a person split in two counts the smaller part as
  wrong); and purity: the speech time of each label that belongs to its main person (splits not counted);
- rare found: the people who said only one to three short things (far_meeting.py `rare`) that kept a label of their
  own (mapped one to one, covering half their speech);
- RTF of the run on this machine.

A recording without its `.json` (a real meeting, no truth) only reports how many voices it got and how many seconds each
holds, which is what to compare before and after a change of the diarization on real rooms.

`--diar-only` skips the recogniser (each piece reads as one word), for sweeping diarization knobs fast; window
embeddings are cached in memory across configurations (`--cache` keeps them on disk too: they are voice data, keep
them outside the repo and delete them afterwards).

    ../.venv/bin/python -I meeting_bench.py /tmp/vivos-room /tmp/far --config base --config fold15:MIN_SPEAKER_S=15
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pickle
import sys
import time
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE))

os.environ.setdefault("SPEECH_PUNCTUATION", "0")
os.environ.setdefault("SPEECH_NUMBERS", "0")

from speech import manifest, pipeline  # noqa: E402

from hotwords_wer import edits, words  # noqa: E402

FRAME = 0.01


def parse_config(spec: str) -> tuple[str, dict[str, float]]:
    name, _, body = spec.partition(":")
    knobs = {}
    for part in filter(None, body.split(",")):
        key, _, value = part.partition("=")
        try:
            knobs[key] = float(value)
        except ValueError:
            knobs[key] = value
    return name, knobs


def score_speakers(truth: dict, segments: list[dict]) -> dict:
    turns = truth["turns"]
    end = max([t["end"] for t in turns] + [s["endMs"] / 1000 for s in segments] + [0]) + 1
    count = int(end / FRAME) + 1
    people = sorted({t["person"] for t in turns})
    ref = np.zeros((len(people), count), bool)
    for t in turns:
        ref[people.index(t["person"]), int(t["start"] / FRAME) : int(t["end"] / FRAME)] = True
    labels = sorted({s["speaker"] for s in segments})
    hyp = np.full(count, -1)
    for s in segments:
        hyp[int(s["startMs"] / 1000 / FRAME) : int(s["endMs"] / 1000 / FRAME)] = labels.index(s["speaker"])
    nref = ref.sum(axis=0)
    nhyp = (hyp >= 0).astype(int)
    overlap = np.zeros((len(labels), len(people)))
    for i in range(len(labels)):
        overlap[i] = ref[:, hyp == i].sum(axis=1)
    mapping, used, order = {}, set(), np.dstack(np.unravel_index(np.argsort(-overlap, axis=None), overlap.shape))[0]
    for i, j in order:
        if overlap[i, j] <= 0:
            break
        if i not in mapping and j not in used:
            mapping[i] = j
            used.add(j)
    correct = np.zeros(count, int)
    for i, j in mapping.items():
        correct += (hyp == i) & ref[j]
    total = nref.sum()
    miss = np.maximum(nref - nhyp, 0).sum()
    false_alarm = np.maximum(nhyp - nref, 0).sum()
    confusion = (np.minimum(nref, nhyp) - correct).sum()
    speech_by_label = {labels[i]: (hyp == i).sum() * FRAME for i in range(len(labels))}
    rare_found = 0
    for person in truth.get("rare", []):
        j = people.index(person)
        own = ref[j].sum()
        rare_found += any(jj == j and overlap[i, j] >= 0.5 * own for i, jj in mapping.items())
    return {
        "total": total, "miss": miss, "fa": false_alarm, "conf": confusion,
        "purity_num": overlap.max(axis=1).sum() if len(labels) else 0, "purity_den": overlap.sum(),
        "labels": len(labels), "labels30": sum(1 for v in speech_by_label.values() if v >= 30),
        "people": len(people), "rare": len(truth.get("rare", [])), "rare_found": rare_found,
    }


class CachedEmbeddings:
    def __init__(self, engine, store: dict):
        self.inner, self.store = engine._embed, store

    def __call__(self, samples: np.ndarray) -> np.ndarray:
        key = hashlib.sha1(samples.tobytes()).digest()
        vector = self.store.get(key)
        if vector is None:
            vector = self.store[key] = self.inner(samples)
        return vector


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("sets", nargs="+", type=Path)
    parser.add_argument("--config", action="append", default=[])
    parser.add_argument("--threads", type=int, default=int(os.environ.get("SPEECH_THREADS") or 4))
    parser.add_argument("--diar-only", action="store_true")
    parser.add_argument("--preset", action="append", default=[], help="KNOB=value set before the models load")
    parser.add_argument("--cache", type=Path, help="pickle of window embeddings (voice data: outside the repo)")
    parser.add_argument("--limit", type=int, help="first N meetings of each set")
    parser.add_argument("--out", type=Path, help="write each run's segments as JSON here")
    args = parser.parse_args()
    configs = [parse_config(spec) for spec in (args.config or ["base"])]
    for key, value in parse_config("preset:" + ",".join(args.preset))[1].items():
        setattr(pipeline, key, value)  # before the models load, e.g. ASR_PRECISION=fp32

    engine = pipeline.Engine(manifest.models_dir(), threads=args.threads)
    store = {}
    if args.cache and args.cache.is_file():
        store = pickle.loads(args.cache.read_bytes())
    engine._embed = CachedEmbeddings(engine, store)
    found_speech = engine._speech_segments
    regions_seen: dict = {}

    def speech_segments(samples, config=None):
        # The VAD is single-threaded and the slowest step once embeddings are cached; its regions depend only on the
        # audio and the VAD knobs, so they are found once per file.
        key = (hashlib.sha1(samples.tobytes()).digest(), id(config), pipeline.VAD_PAD_S, pipeline.VAD_THRESHOLD)
        if key not in regions_seen:
            regions_seen[key] = [(start, part.size) for start, part in found_speech(samples, config)]
        return [(start, samples[start : start + size]) for start, size in regions_seen[key]]

    engine._speech_segments = speech_segments
    defaults = {key: getattr(pipeline, key) for _, knobs in configs for key in knobs if hasattr(pipeline, key)}
    base_threshold = engine._cluster_threshold
    real_pieces = engine._transcribe_pieces

    def no_words(pieces):
        return ["x"] * len(pieces), [None] * len(pieces), [None] * len(pieces)

    rows = []
    saved = [len(store)]
    for name, knobs in configs:
        for key, value in defaults.items():
            setattr(pipeline, key, value)
        engine._cluster_threshold = base_threshold
        for key, value in knobs.items():
            if key == "threshold":
                engine._cluster_threshold = value
            elif hasattr(pipeline, key):
                current = getattr(pipeline, key)
                setattr(pipeline, key, value if isinstance(value, str) else
                        bool(value) if isinstance(current, bool) else type(current)(value))
            else:
                raise SystemExit(f"unknown knob {key}")
        engine._transcribe_pieces = no_words if args.diar_only else real_pieces
        for folder in args.sets:
            totals = {"err": 0, "ref": 0, "cerr": 0, "cref": 0, "audio": 0.0, "busy": 0.0, "files": 0}
            spk: dict[str, float] = {}
            label_list = []
            for wav in sorted((folder / "meetings").glob("*.wav"))[: args.limit]:
                truth_file = wav.with_suffix(".json")
                if not truth_file.is_file():
                    # A real recording without truth: only how many voices, and how much each holds.
                    samples = pipeline.decode(wav)
                    started = time.monotonic()
                    result = engine._transcribe(samples, True, None, "room")
                    held = {}
                    for s in result["segments"]:
                        held[s["speaker"]] = held.get(s["speaker"], 0) + (s["endMs"] - s["startMs"]) / 1000
                    print(f"{name:<14} {wav.stem}: {len(held)} voices, "
                          f"{sum(v >= 30 for v in held.values())} with ≥30 s, "
                          f"{sum(v >= 60 for v in held.values())} with ≥60 s; seconds each "
                          f"{sorted((round(v) for v in held.values()), reverse=True)}; "
                          f"RTF {(time.monotonic() - started) / (samples.size / pipeline.SAMPLE_RATE):.3f}", flush=True)
                    continue
                truth = json.loads(truth_file.read_text(encoding="utf-8"))
                samples = pipeline.decode(wav)
                started = time.monotonic()
                result = engine._transcribe(samples, True, None, "room")
                totals["busy"] += time.monotonic() - started
                totals["audio"] += samples.size / pipeline.SAMPLE_RATE
                totals["files"] += 1
                segments = result["segments"]
                for key, value in score_speakers(truth, segments).items():
                    spk[key] = spk.get(key, 0) + value
                people = len({t["person"] for t in truth["turns"]})
                label_list.append(f"{len({s['speaker'] for s in segments})}/{people}")
                if not args.diar_only:
                    ref = words(" ".join(t["text"] for t in sorted(truth["turns"], key=lambda t: t["start"])))
                    hyp = words(" ".join(s["text"] for s in segments))
                    totals["err"] += edits(ref, hyp)
                    totals["ref"] += len(ref)
                    totals["cerr"] += edits(list("".join(ref)), list("".join(hyp)))
                    totals["cref"] += len("".join(ref))
                if args.out:
                    args.out.mkdir(parents=True, exist_ok=True)
                    (args.out / f"{name}.{folder.name}.{wav.stem}.json").write_text(
                        json.dumps(segments, ensure_ascii=False), encoding="utf-8")
            if args.cache and len(store) != saved[0]:
                args.cache.write_bytes(pickle.dumps(store))
                saved[0] = len(store)
            if not spk:
                continue
            total = max(spk.get("total", 0), 1)
            rows.append((name, folder.name, totals["files"],
                         totals["err"] / totals["ref"] if totals["ref"] else None,
                         totals["cerr"] / totals["cref"] if totals["cref"] else None,
                         (spk["miss"] + spk["fa"] + spk["conf"]) / total, spk["conf"] / total, spk["miss"] / total,
                         spk["fa"] / total, spk["purity_num"] / max(spk["purity_den"], 1),
                         f"{int(spk['labels'])} ({int(spk['labels30'])}≥30s) / {int(spk['people'])}",
                         f"{int(spk['rare_found'])}/{int(spk['rare'])}", totals["busy"] / max(totals["audio"], 1e-9),
                         " ".join(label_list)))
            print(_row(rows[-1]), flush=True)
    if args.cache:
        args.cache.write_bytes(pickle.dumps(store))
    print()
    print(f"{'config':<14} {'set':<12} {'n':>3} {'WER':>6} {'CER':>6} {'DER':>6} {'conf':>6} {'miss':>6} {'FA':>6} "
          f"{'purity':>6}  labels (≥30 s) / people  rare  RTF")
    for row in rows:
        print(_row(row))
    return 0


def _row(row) -> str:
    name, folder, n, wer, cer, der, conf, miss, fa, purity, labels, rare, rtf, per_file = row
    pct = lambda v: "     -" if v is None else f"{v:6.1%}"  # noqa: E731
    return (f"{name:<14} {folder:<12} {n:>3} {pct(wer)} {pct(cer)} {pct(der)} {pct(conf)} {pct(miss)} {pct(fa)} "
            f"{pct(purity)}  {labels:<24} {rare:<5} {rtf:.3f}  [{per_file}]")


if __name__ == "__main__":
    sys.exit(main())
