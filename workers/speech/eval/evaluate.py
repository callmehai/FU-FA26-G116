"""Measure transcription, speaker separation and Voice ID end to end against a running speech worker.

Dataset layout (from vivos_dataset.py or recorded by the team; personal data, never commit it):

    <dataset>/enrol/<person>/*.wav        enrolment samples, one person each
    <dataset>/meetings/<name>.wav         a meeting recording
    <dataset>/meetings/<name>.json        {"people": [roster: who may be suggested], "stranger": ..., "absent": ...,
                                           "turns": [{"start": 0.0, "end": 5.8, "person": "lan", "text": "..."}]}

The suggestion rule mirrors SpeakerMatcher.cs: a person's score is their best sample, a voice is
named only at or above the threshold and at least the margin ahead of the next person, and each
person names at most one voice per recording, best pairs first. --voice-margin adds a candidate
rule: the voice must also be that far ahead of the person's next best voice in the same recording,
because everyone recorded in one room scores alike against a sample.

Metrics (speech time is the ground-truth turns):
  named right / named wrong   share of speech time whose suggested name is the true / another person
  enrolled named right        named right over the speech of people who could be named (not the stranger)
  stranger named              share of the non-enrolled speaker's time that got any name (must be 0)
  absent named                meetings where the silent roster member was suggested (must be 0)
  precision                   suggested voices that were the right person
  speaker accuracy            speech time whose voice label maps to the true person (labels mapped
                              many-to-one, so splitting a person in two is not counted wrong)
  WER                         word error rate against the turns' text

    python -I evaluate.py <dataset> --url http://127.0.0.1:8090 --threshold 0.4 0.5 0.6 --margin 0.05 0.1
"""

import argparse
import hashlib
import json
import math
import pathlib
import re
import time
import urllib.error
import urllib.request

FILLERS = {"ờ", "ừ", "ừm", "à", "ạ"}


def post(url, path, audio, key=None):
    headers = {"Content-Type": "application/octet-stream"}
    if key:
        headers["Authorization"] = f"Bearer {key}"
    request = urllib.request.Request(url.rstrip("/") + path, data=audio, method="POST", headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=7200) as response:
            return response.status, json.loads(response.read())
    except urllib.error.HTTPError as error:
        return error.code, json.loads(error.read() or b"{}")


def cached(cache, kind, audio, call):
    """Worker results by audio hash, so trying another threshold does not transcribe again."""
    if cache is None:
        return call()
    path = cache / f"{kind}-{hashlib.sha256(audio).hexdigest()[:20]}.json"
    if path.exists():
        return tuple(json.loads(path.read_text()))
    result = call()
    path.write_text(json.dumps(result))
    return result


def cosine(a, b):
    dot = sum(x * y for x, y in zip(a, b))
    na, nb = math.sqrt(sum(x * x for x in a)), math.sqrt(sum(y * y for y in b))
    return 0.0 if na == 0 or nb == 0 or len(a) != len(b) else dot / (na * nb)


def suggest(voices, samples, roster, threshold, margin, voice_margin=0.0):
    """voices: {label: embedding}; samples: {person: [embedding]} → {label: (person, score)}."""
    scores = {label: sorted(((max(cosine(v, e) for e in samples[p]), p) for p in roster if p in samples), reverse=True)
              for label, v in voices.items()}
    by_person = {}
    for label, ranked in scores.items():
        for score, person in ranked:
            by_person.setdefault(person, []).append(score)
    result, taken = {}, set()
    for score, label, person in sorted(((s, label, p) for label, ranked in scores.items() for s, p in ranked),
                                       reverse=True):
        if label in result or person in taken or score < threshold:
            continue
        runner_up = max((s for s, p in scores[label] if p != person), default=0.0)
        if score - runner_up < margin:
            continue
        other_voice = sorted(by_person[person], reverse=True)[1] if len(by_person[person]) > 1 else 0.0
        if score - other_voice < voice_margin:
            continue
        result[label] = (person, score)
        taken.add(person)
    return result


def words(text):
    text = re.sub(r"[^\w\s]", " ", text.lower())
    return [w for w in text.split() if w not in FILLERS]


def edits(ref, hyp):
    row = list(range(len(hyp) + 1))
    for i, r in enumerate(ref, 1):
        prev, row[0] = row[0], i
        for j, h in enumerate(hyp, 1):
            prev, row[j] = row[j], min(row[j] + 1, row[j - 1] + 1, prev + (r != h))
    return row[len(hyp)]


def overlap(a0, a1, b0, b1):
    return max(0.0, min(a1, b1) - max(a0, b0))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dataset", type=pathlib.Path)
    ap.add_argument("--url", default="http://127.0.0.1:8090")
    ap.add_argument("--api-key")
    ap.add_argument("--threshold", type=float, nargs="+", default=[0.4, 0.5, 0.6])
    ap.add_argument("--margin", type=float, nargs="+", default=[0.1])
    ap.add_argument("--voice-margin", type=float, nargs="+", default=[0.0])
    ap.add_argument("--cache", type=pathlib.Path, help="directory to keep worker results between runs")
    args = ap.parse_args()
    if args.cache:
        args.cache.mkdir(parents=True, exist_ok=True)

    samples, rejected = {}, []
    for person_dir in sorted((args.dataset / "enrol").iterdir()):
        for wav in sorted(person_dir.glob("*.wav")):
            audio = wav.read_bytes()
            status, body = cached(args.cache, "embed", audio, lambda: post(args.url, "/v1/embed", audio, args.api_key))
            if status == 200:
                samples.setdefault(person_dir.name, []).append(body["embedding"])
            else:
                rejected.append(f"{person_dir.name}/{wav.name}: {body.get('error')} (consistency {body.get('consistency')})")
    total = sum(1 for _ in (args.dataset / "enrol").glob("*/*.wav"))
    print(f"enrolment: {total - len(rejected)}/{total} samples accepted, {len(samples)} people enrolled")
    for line in rejected:
        print(f"  rejected {line}")

    grid = {(t, m, v): {"speech": 0.0, "right": 0.0, "wrong": 0.0, "stranger": 0.0, "stranger_named": 0.0,
                     "absent_named": 0, "suggested": 0, "correct": 0}
            for t in args.threshold for m in args.margin for v in args.voice_margin}
    err_words = ref_words = 0
    spk_right = spk_total = audio_s = busy_s = 0.0
    found = []
    for wav in sorted((args.dataset / "meetings").glob("*.wav")):
        truth = json.loads(wav.with_suffix(".json").read_text(encoding="utf-8"))
        audio = wav.read_bytes()
        started = time.perf_counter()
        status, result = cached(args.cache, "transcribe", audio,
                                lambda: post(args.url, "/v1/transcribe?diarize=true", audio, args.api_key))
        busy_s += time.perf_counter() - started
        assert status == 200, result
        audio_s += result["durationMs"] / 1000
        turns, segments = truth["turns"], result["segments"]

        def true_person(seg):
            return max(turns, key=lambda t: overlap(seg["startMs"] / 1000, seg["endMs"] / 1000, t["start"], t["end"]))["person"]

        # Speaker accuracy: each label stands for the person it overlaps most.
        time_by = {}
        for seg in segments:
            for turn in turns:
                o = overlap(seg["startMs"] / 1000, seg["endMs"] / 1000, turn["start"], turn["end"])
                time_by.setdefault(seg["speaker"], {}).setdefault(turn["person"], 0.0)
                time_by[seg["speaker"]][turn["person"]] += o
        label_truth = {label: max(by, key=by.get) for label, by in time_by.items()}
        spk_total += sum(sum(by.values()) for by in time_by.values())
        spk_right += sum(by[label_truth[label]] for label, by in time_by.items())
        found.append((len(result["speakers"]), len({t["person"] for t in turns})))

        ref = words(" ".join(t.get("text", "") for t in turns))
        if ref:
            err_words += edits(ref, words(" ".join(s["text"] for s in segments)))
            ref_words += len(ref)

        voices = {s["label"]: s["embedding"] for s in result["speakers"]}
        for (threshold, margin, voice_margin), tot in grid.items():
            names = suggest(voices, samples, truth["people"], threshold, margin, voice_margin)
            for seg in segments:
                length = (seg["endMs"] - seg["startMs"]) / 1000
                person, named = true_person(seg), names.get(seg["speaker"], (None, 0))[0]
                tot["speech"] += length
                tot["right"] += length if named == person else 0
                tot["wrong"] += length if named not in (None, person) else 0
                if person == truth.get("stranger"):
                    tot["stranger"] += length
                    tot["stranger_named"] += length if named else 0
            tot["absent_named"] += any(p == truth.get("absent") for p, _ in names.values())
            tot["suggested"] += len(names)
            tot["correct"] += sum(1 for label, (p, _) in names.items() if p == label_truth.get(label))

    exact = sum(1 for got, want in found if got == want)
    print(f"\n{len(found)} meetings, {audio_s / 60:.1f} min of audio")
    print(f"speakers found = true count in {exact}/{len(found)} meetings; "
          f"split: {sum(1 for g, w in found if g > w)}, merged: {sum(1 for g, w in found if g < w)}")
    print(f"speaker accuracy {spk_right / max(spk_total, 1e-9):.1%} of speech time")
    if ref_words:
        print(f"WER {err_words / ref_words:.1%} over {ref_words} words")
    print(f"processing {busy_s:.0f}s for {audio_s:.0f}s of audio (RTF {busy_s / max(audio_s, 1e-9):.3f}; cached runs read 0)")
    print("\nthreshold margin voice-margin | named right | enrolled named right | named wrong | stranger named |"
          " absent named | precision")
    for (threshold, margin, voice_margin), tot in sorted(grid.items()):
        speech = max(tot["speech"], 1e-9)
        enrolled = max(tot["speech"] - tot["stranger"], 1e-9)
        print(f"   {threshold:.2f}   {margin:.2f}   {voice_margin:.2f}       |   {tot['right'] / speech:6.1%}    |"
              f"        {tot['right'] / enrolled:6.1%}        |   {tot['wrong'] / speech:6.1%}    |"
              f"     {tot['stranger_named'] / max(tot['stranger'], 1e-9):6.1%}     |    {tot['absent_named']:3d}       |"
              f" {tot['correct']}/{tot['suggested']}")


if __name__ == "__main__":
    main()
