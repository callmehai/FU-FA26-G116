"""End-to-end check of the real models on a small audio set. Local use only; CI has no models.

    python selfcheck.py <audio dir>

The directory needs enrolA.wav and enrolB.wav (one voice each, 8–30 s) and meeting.wav (the
same two voices taking turns). Exits non-zero when the pipeline does not find two speakers that
match the two enrolment samples, or when a two-voice enrolment sample is accepted.
"""

from __future__ import annotations

import logging
import sys
import tempfile
import time
import wave
from pathlib import Path

import numpy as np

from speech import pipeline


def write_wav(path: Path, samples: np.ndarray) -> None:
    pcm = (np.clip(samples, -1.0, 1.0) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(pipeline.SAMPLE_RATE)
        out.writeframes(pcm.tobytes())


def main(directory: Path) -> int:
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")
    failures = []

    started = time.monotonic()
    engine = pipeline.Engine()
    print(f"models: {engine.model_ids} (loaded in {time.monotonic() - started:.1f}s)")

    enrolled = {}
    for name in ("enrolA", "enrolB"):
        started = time.monotonic()
        accepted, body = engine.embed_file(directory / f"{name}.wav")
        metrics = {k: v for k, v in body.items() if k != "embedding"}
        print(f"embed {name}: accepted={accepted} {metrics} ({time.monotonic() - started:.2f}s)")
        if not accepted:
            failures.append(f"{name} was rejected: {body['error']}")
            continue
        enrolled[name] = np.asarray(body["embedding"], dtype=np.float32)

    both = np.concatenate([pipeline.decode(directory / "enrolA.wav"), pipeline.decode(directory / "enrolB.wav")])
    with tempfile.TemporaryDirectory() as scratch:
        mixed = Path(scratch) / "two-voices.wav"
        write_wav(mixed, both)
        accepted, body = engine.embed_file(mixed)
    print(f"embed enrolA+enrolB: accepted={accepted} {body.get('error')} consistency={body.get('consistency')}")
    if accepted or body.get("error") != "multiple_speakers":
        failures.append("a sample with two voices was not rejected as multiple_speakers")

    meeting = directory / "meeting.wav"
    started = time.monotonic()
    result = engine.transcribe_file(meeting, diarize=True)
    elapsed = time.monotonic() - started
    audio_s = result["durationMs"] / 1000
    print(f"transcribe meeting (diarize): {audio_s:.1f}s audio in {elapsed:.2f}s, RTF {elapsed / audio_s:.3f}")
    for segment in result["segments"]:
        print(f"  [{segment['startMs'] / 1000:5.1f}-{segment['endMs'] / 1000:5.1f}] {segment['speaker']}: {segment['text']}")
    if len(result["speakers"]) != 2:
        failures.append(f"expected 2 speakers, found {len(result['speakers'])}")
    if len(result["segments"]) != 4:
        failures.append(f"expected 4 segments, found {len(result['segments'])}")

    matches = {}
    for voice in result["speakers"]:
        vector = np.asarray(voice["embedding"], dtype=np.float32)
        scores = {name: round(float(vector @ reference), 3) for name, reference in enrolled.items()}
        best = max(scores, key=scores.get) if scores else None
        matches[voice["label"]] = best
        print(f"  {voice['label']} speech {voice['speechMs']} ms, dim {vector.size}, norm "
              f"{np.linalg.norm(vector):.3f}, cosine to enrolled {scores} -> {best}")
    if len(set(matches.values())) != len(matches) or None in matches.values():
        failures.append(f"speakers do not map one-to-one onto the enrolled voices: {matches}")

    started = time.monotonic()
    plain = engine.transcribe_file(meeting, diarize=False)
    print(f"transcribe meeting (no diarize): {len(plain['segments'])} segments, speakers {plain['speakers']}, "
          f"{time.monotonic() - started:.2f}s")
    if any(segment["speaker"] is not None for segment in plain["segments"]):
        failures.append("diarize=false returned speaker labels")

    for failure in failures:
        print(f"FAIL: {failure}")
    print("selfcheck passed" if not failures else f"selfcheck failed ({len(failures)})")
    return 1 if failures else 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(Path(sys.argv[1])))
