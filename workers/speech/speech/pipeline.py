"""Decode audio, find speech, split it by speaker, transcribe it, and embed voices.

Everything runs on CPU through sherpa-onnx. The models are loaded once per process; one lock
serialises inference, because each job already uses every core it is given and two at once would
only make both slower.

Diarization ("who spoke when") is done here rather than with sherpa-onnx's own pipeline, which
embeds a 10-second window every half second and cost 0.3–1.3× real time — an hour of meeting would
take up to an hour and a half. Instead each stretch of speech the VAD finds is cut into short
overlapping windows, every window is embedded once, the windows are clustered, and runs of the same
cluster become turns. That costs a few hundredths of real time.
"""

from __future__ import annotations

import logging
import math
import os
import subprocess
import threading
import time
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

import numpy as np

from . import manifest, punctuation, text

log = logging.getLogger(__name__)

SAMPLE_RATE = 16000

# --- Transcription ----------------------------------------------------------------------------
# Longest piece handed to the recogniser at once; the VAD cuts speech at this length, and a
# longer turn is split again. The transducer degrades and memory grows on very long inputs.
MAX_CHUNK_S = 25.0
# Turns shorter than this are dropped: too short to hold a word, mostly breaths and clicks.
MIN_TURN_S = 0.3
# Audio per speaker used for that speaker's voice embedding, taken from its longest turns first.
SPEAKER_EMBED_MAX_S = 30.0
# Upper bound for ffmpeg to decode one upload.
DECODE_TIMEOUT_S = 1800
# Silero VAD speech probability cut. Lenient on purpose: at the usual 0.5 it started a sentence
# 0.5 s late and cut off its first words ("Ý kiến…"), and on a real meeting 0.3 still missed most
# of what two quieter participants said; 0.2 (after levelling) caught it, and silence or white noise
# still gave no segment. Missing words is worse than letting in some noise: the recogniser returns
# nothing for noise, and noise windows fold into real speakers.
VAD_THRESHOLD = 0.2
# Speech regions are widened by this much on each side (never into a neighbour) so the VAD's
# late onsets and early offsets do not clip the edges of words.
VAD_PAD_S = 0.1

# --- Punctuation ------------------------------------------------------------------------------
# A speaker's consecutive segments are punctuated as one passage (speech/punctuation.py), so a
# breath in the middle of a sentence does not end it. A longer pause than this starts a new one.
PASSAGE_MAX_GAP_S = 5.0

# --- Levelling --------------------------------------------------------------------------------
# A sliding gain lifts quiet stretches before the VAD. In a real meeting the people far from the
# recorder (or heard through a speakerphone) were 15–20 dB below the chair; the VAD never marked
# their speech, so it was never transcribed, although the recogniser read it fine. The gain at
# each moment brings the level of the surrounding LEVEL_WINDOW_S toward LEVEL_TARGET_DBFS.
# Measured on that meeting (README, "Hiệu chỉnh"): WER 34.9% → 16.1%, and the share of the two
# quiet speakers' turns transcribed rose from 10–12% to 84–91%. A 1.5 s window follows a short
# interjection better than 3 s.
LEVEL_WINDOW_S = 1.5
LEVEL_TARGET_DBFS = -20.0
LEVEL_MAX_GAIN_DB = 30.0
LEVEL_MAX_CUT_DB = 12.0
# Windows this quiet are silence, not a quiet speaker: they get no lift, so room hiss is not
# raised into something the VAD mistakes for speech. The lift fades in over the next 6 dB.
LEVEL_FLOOR_DBFS = -60.0
LEVEL_FADE_DB = 6.0

# --- Diarization -------------------------------------------------------------------------------
# Window and hop over each stretch of speech. On a real room recording 1.5 s windows were too
# noisy: two windows of one person were hardly closer than two different people, and a five-minute
# meeting of 4 people came out as 22 speakers. 3 s windows place a speaker change within ~0.75 s.
WINDOW_S = 3.0
HOP_S = 1.5
# Speech stretches shorter than this are not clustered; they take the nearest speaker afterwards.
MIN_REGION_S = 0.5
# A cluster with less speech than this is not a person but noise, laughter or a cough; its windows
# join the nearest real speaker. Skipped when the caller states the number of speakers.
MIN_SPEAKER_S = 2.0
# Cut for sherpa-onnx FastClustering over the window embeddings (README, "Hiệu chỉnh"): with 3 s
# windows, 0.5 found the right number of speakers in 17 of 20 synthetic clips (the misses are pairs
# of near-identical macOS English voices); higher cuts merged real people. SPEECH_CLUSTER_THRESHOLD
# overrides it.
DEFAULT_CLUSTER_THRESHOLD = 0.5
# Second pass: two clusters whose whole-voice embeddings (up to SPEAKER_EMBED_MAX_S each) are at
# least this close are one person the window clustering split. On a real meeting one voice came
# out as two large clusters at 0.94. Short clusters are unreliable (one person 0.36–0.81, two
# people up to 0.73), so this merges only when nearly certain: a person split in two is cheaper
# than two people merged, as the Secretary can name both labels. Skipped when the caller states
# the number of speakers.
MERGE_THRESHOLD = 0.85

# --- Enrolment sample quality gates (POST /v1/embed) ------------------------------------------
# A voice sample becomes someone's reference voice, so a bad one causes wrong suggestions in
# every later meeting. Each gate below rejects one way a sample goes bad.
MAX_ENROL_AUDIO_S = 60.0  # a reading passage takes ~25 s; anything far longer is not a sample
MIN_ENROL_SPEECH_S = 6.0  # less speech than this gives an unstable embedding
MIN_ENROL_RMS_DBFS = -40.0  # quieter speech is mostly room noise to the embedding model
CLIP_LEVEL = 0.99  # |sample| at or above this counts as clipped
MAX_CLIPPED_RATIO = 0.01  # more than 1% clipped speech distorts the voice
# Consistency: cut the speech into non-overlapping ~2 s windows (at most 6, spread evenly), embed
# each, and take the lowest cosine between any two. One voice stays similar to itself; a sample
# where a second person speaks has a window pair that does not match.
CONSISTENCY_WINDOW_S = 2.0
CONSISTENCY_MAX_WINDOWS = 6
# Calibrated on real voices (README, "Hiệu chỉnh"): 94 single-speaker samples from the VIVOS corpus
# scored 0.462–0.741, and 50 samples mixing two speakers half and half at most 0.468 (one of 50
# above 0.40). The first cut, 0.60, was tuned on TTS voices and turned away 38% of real people.
# SPEECH_MIN_CONSISTENCY overrides it without a code change.
MIN_CONSISTENCY = 0.40


class UnreadableAudio(Exception):
    """ffmpeg could not decode the upload as audio."""


def decode(path: str | os.PathLike[str]) -> np.ndarray:
    """Decode any container ffmpeg reads into 16 kHz mono float32 samples.

    Reads from a file rather than stdin: MP4/M4A put their index at the end of the file and
    cannot be decoded from a pipe.
    """
    try:
        proc = subprocess.run(
            ["ffmpeg", "-nostdin", "-loglevel", "error", "-i", os.fspath(path),
             "-vn", "-ac", "1", "-ar", str(SAMPLE_RATE), "-f", "f32le", "pipe:1"],
            capture_output=True,
            timeout=DECODE_TIMEOUT_S,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        raise UnreadableAudio("decoding timed out") from error
    if proc.returncode != 0:
        raise UnreadableAudio(proc.stderr.decode("utf-8", "replace").strip()[:500])
    return np.frombuffer(proc.stdout, dtype="<f4").astype(np.float32, copy=True)


def rms_dbfs(samples: np.ndarray) -> float | None:
    if samples.size == 0:
        return None
    rms = float(np.sqrt(np.mean(np.square(samples, dtype=np.float64))))
    return 20.0 * math.log10(rms) if rms > 0 else -120.0


def clipped_ratio(samples: np.ndarray) -> float:
    if samples.size == 0:
        return 0.0
    return float(np.mean(np.abs(samples) >= CLIP_LEVEL))


def windows(speech: np.ndarray, size: int, max_count: int) -> list[np.ndarray]:
    """Non-overlapping windows of `size` samples, at most `max_count`, spread over the speech."""
    available = speech.size // size
    if available == 0:
        return []
    picks = np.linspace(0, available - 1, num=min(available, max_count)).round().astype(int)
    return [speech[i * size : (i + 1) * size] for i in sorted(set(picks.tolist()))]


def min_pairwise_cosine(vectors: list[np.ndarray]) -> float:
    if len(vectors) < 2:
        return 1.0
    matrix = np.stack([v / (np.linalg.norm(v) or 1.0) for v in vectors])
    similarity = matrix @ matrix.T
    upper = similarity[np.triu_indices(len(vectors), k=1)]
    return float(upper.min())


@dataclass(frozen=True)
class Verdict:
    error: str | None
    speech_ms: int
    rms_dbfs: float | None
    clipped_ratio: float | None
    consistency: float | None

    def metrics(self) -> dict:
        return {
            "speechMs": self.speech_ms,
            "rmsDbfs": None if self.rms_dbfs is None else round(self.rms_dbfs, 1),
            "clippedRatio": None if self.clipped_ratio is None else round(self.clipped_ratio, 4),
            "consistency": None if self.consistency is None else round(self.consistency, 3),
        }


def assess_enrolment(
    samples: np.ndarray,
    speech: np.ndarray,
    consistency_of: Callable[[np.ndarray], float],
    min_consistency: float = MIN_CONSISTENCY,
) -> Verdict:
    """Apply the enrolment gates in order, cheapest first; consistency runs the model, so last.

    `speech` is the voiced part of `samples` as found by the VAD.
    """
    speech_ms = round(speech.size * 1000 / SAMPLE_RATE)
    if samples.size > MAX_ENROL_AUDIO_S * SAMPLE_RATE:
        return Verdict("too_long", speech_ms, None, None, None)
    if speech.size < MIN_ENROL_SPEECH_S * SAMPLE_RATE:
        return Verdict("too_short", speech_ms, rms_dbfs(speech), None, None)
    loudness = rms_dbfs(speech)
    if loudness is None or loudness < MIN_ENROL_RMS_DBFS:
        return Verdict("too_quiet", speech_ms, loudness, None, None)
    clipped = clipped_ratio(speech)
    if clipped > MAX_CLIPPED_RATIO:
        return Verdict("clipped", speech_ms, loudness, clipped, None)
    consistency = consistency_of(speech)
    if consistency < min_consistency:
        return Verdict("multiple_speakers", speech_ms, loudness, clipped, consistency)
    return Verdict(None, speech_ms, loudness, clipped, consistency)


def model_ids(hashes: dict[str, str]) -> dict[str, str]:
    """`<model>@<first 12 hex of its sha256>`, reported by /health and stored with each result."""

    def short(path: str) -> str:
        return hashes[path][:12]

    return {
        "asr": f"gipformer-1.5-68m-int8@{short('gipformer/encoder.int8.onnx')}",
        "embedding": f"eres2net-base-200k@{short('eres2net_base.onnx')}",
        "vad": f"silero-vad@{short('silero_vad.onnx')}",
        "punctuation": f"vibert-capu-int8@{short(punctuation.MODEL_FILE)}",
    }


def passages(pieces: list[tuple[float, float, str | None, np.ndarray]], raws: list[str]) -> list[list[int]]:
    """Indices of the pieces, in time order, grouped into runs of one speaker without a long pause.

    Pieces the recogniser found no words in are left out; they neither join nor split a passage.
    """
    groups: list[list[int]] = []
    last_end = -math.inf
    last_label: str | None = None
    for index in sorted(range(len(pieces)), key=lambda i: (pieces[i][0], pieces[i][1])):
        if not raws[index].split():
            continue
        start, end, label, _ = pieces[index]
        if groups and label == last_label and start - last_end <= PASSAGE_MAX_GAP_S:
            groups[-1].append(index)
        else:
            groups.append([index])
        last_end, last_label = end, label
    return groups


def level(samples: np.ndarray, window_s: float = LEVEL_WINDOW_S, target_db: float = LEVEL_TARGET_DBFS,
          max_gain_db: float = LEVEL_MAX_GAIN_DB) -> np.ndarray:
    """The recording with a slowly varying gain applied, computed per 10 ms frame (see LEVEL_*)."""
    frame = SAMPLE_RATE // 100
    count = -(-samples.size // frame)
    if count == 0:
        return samples
    power = np.empty(count)
    block = 1 << 16  # frames at a time: an hour of audio is never copied whole
    for first in range(0, count, block):
        chunk = samples[first * frame : (first + block) * frame]
        chunk = np.pad(chunk, (0, -chunk.size % frame)).reshape(-1, frame)
        power[first : first + chunk.shape[0]] = np.square(chunk, dtype=np.float64).mean(axis=1)
    width = max(1, round(window_s * 100))
    total = np.concatenate([[0.0], np.cumsum(power)])
    index = np.arange(count)
    low, high = np.clip(index - width // 2, 0, count), np.clip(index + width - width // 2, 0, count)
    level_db = 10 * np.log10((total[high] - total[low]) / (high - low) + 1e-12)
    gain_db = np.clip(target_db - level_db, -LEVEL_MAX_CUT_DB, max_gain_db)
    gain_db *= np.clip((level_db - LEVEL_FLOOR_DBFS) / LEVEL_FADE_DB, 0.0, 1.0)
    gain = (10 ** (gain_db / 20)).astype(np.float32)
    out = np.empty_like(samples)
    for first in range(0, count, block):
        part = samples[first * frame : (first + block) * frame]
        out[first * frame : first * frame + part.size] = part * np.repeat(gain[first : first + block], frame)[: part.size]
    return np.clip(out, -1.0, 1.0, out=out)


def hard_split(samples: np.ndarray, max_s: float = MAX_CHUNK_S) -> list[tuple[int, np.ndarray]]:
    """Equal pieces of at most `max_s`: 25.4 s becomes two 12.7 s pieces, not 25 s plus a 0.4 s
    sliver the recogniser turns into a stray word."""
    count = math.ceil(samples.size / (max_s * SAMPLE_RATE)) or 1
    size = math.ceil(samples.size / count)
    return [(start, samples[start : start + size]) for start in range(0, samples.size, size)]


def pad_regions(regions: list[tuple[int, int]], total: int, pad: int = int(VAD_PAD_S * SAMPLE_RATE)
                ) -> list[tuple[int, int]]:
    """Widen sorted (start, end) regions by `pad` each side without overlapping a neighbour."""
    padded = []
    for i, (start, end) in enumerate(regions):
        low = regions[i - 1][1] if i > 0 else 0
        high = regions[i + 1][0] if i + 1 < len(regions) else total
        padded.append((max(low, start - pad, 0), min(high, end + pad, total)))
    return padded


# --- Diarization helpers (pure, unit-tested) ---------------------------------------------------

def window_starts(length: int, window: int, hop: int) -> list[int]:
    """Starts of `window`-long windows every `hop` samples; the last one is aligned to the end."""
    if length <= window:
        return [0]
    starts = list(range(0, length - window + 1, hop))
    if starts[-1] + window < length:
        starts.append(length - window)
    return starts


def centroids(vectors: np.ndarray, labels: np.ndarray) -> dict[int, np.ndarray]:
    result = {}
    for label in np.unique(labels):
        mean = vectors[labels == label].mean(axis=0)
        result[int(label)] = mean / (np.linalg.norm(mean) or 1.0)
    return result


def nearest(vector: np.ndarray, by_label: dict[int, np.ndarray]) -> int:
    return max(by_label, key=lambda label: float(vector @ by_label[label]))


def fold_small_clusters(vectors: np.ndarray, labels: np.ndarray, min_count: int) -> np.ndarray:
    """Move the windows of every cluster smaller than `min_count` to the nearest large cluster."""
    counts = {int(label): int(count) for label, count in zip(*np.unique(labels, return_counts=True))}
    large = {label for label, count in counts.items() if count >= min_count}
    if not large or len(large) == len(counts):
        return labels
    anchors = {label: c for label, c in centroids(vectors, labels).items() if label in large}
    folded = labels.copy()
    for index, label in enumerate(labels):
        if int(label) not in large:
            folded[index] = nearest(vectors[index], anchors)
    return folded


def smooth(labels: np.ndarray, groups: list[int]) -> np.ndarray:
    """Within one stretch of speech, a single window that disagrees with both neighbours —
    which agree with each other — takes their label. One window is too short to be a turn."""
    smoothed = labels.copy()
    for i in range(1, len(labels) - 1):
        if groups[i - 1] == groups[i] == groups[i + 1] and labels[i - 1] == labels[i + 1] != labels[i]:
            smoothed[i] = labels[i - 1]
    return smoothed


def spans_to_turns(spans: list[tuple[int, int, int]], labels: np.ndarray) -> list[tuple[int, int, int]]:
    """Overlapping (group, start, end) windows plus one label each → (start, end, label) turns.

    Within a group, two neighbouring windows split their overlap at its middle; runs of one label
    merge into one turn. Groups never merge with each other: a pause the VAD found stays a cut.
    """
    turns: list[tuple[int, int, int]] = []
    for i, (group, start, end) in enumerate(spans):
        same_before = i > 0 and spans[i - 1][0] == group
        same_after = i + 1 < len(spans) and spans[i + 1][0] == group
        begin = (spans[i - 1][2] + start) // 2 if same_before else start
        finish = (end + spans[i + 1][1]) // 2 if same_after else end
        label = int(labels[i])
        if same_before and turns and turns[-1][2] == label:
            turns[-1] = (turns[-1][0], finish, label)
        else:
            turns.append((begin, finish, label))
    return turns


def merge_turns(turns: list[tuple[int, int, int]]) -> list[tuple[int, int, int]]:
    """Join back-to-back turns of one speaker, so a relabelled turn is not cut mid-sentence."""
    merged: list[tuple[int, int, int]] = []
    for start, end, label in sorted(turns):
        if merged and merged[-1][2] == label and merged[-1][1] == start:
            merged[-1] = (merged[-1][0], end, label)
        else:
            merged.append((start, end, label))
    return merged


def closest_pair(voices: dict[int, np.ndarray]) -> tuple[float, int, int] | None:
    """The most similar two voices as (cosine, kept label, dropped label); the lower label is kept."""
    labels = sorted(voices)
    pairs = [(float(voices[a] @ voices[b]), a, b) for i, a in enumerate(labels) for b in labels[i + 1 :]]
    return max(pairs) if pairs else None


class Engine:
    """The loaded models plus the operations the HTTP server exposes."""

    def __init__(
        self,
        root: Path | None = None,
        threads: int | None = None,
        cluster_threshold: float | None = None,
    ) -> None:
        import sherpa_onnx  # imported here so the unit tests run without the native library

        self._onnx = sherpa_onnx
        root = root or manifest.models_dir()
        hashes = manifest.verify(root)
        threads = threads or int(os.environ.get("SPEECH_THREADS") or min(4, os.cpu_count() or 1))
        self._cluster_threshold = (
            cluster_threshold
            if cluster_threshold is not None
            else float(os.environ.get("SPEECH_CLUSTER_THRESHOLD") or DEFAULT_CLUSTER_THRESHOLD)
        )
        self._min_consistency = float(os.environ.get("SPEECH_MIN_CONSISTENCY") or MIN_CONSISTENCY)
        self._lock = threading.Lock()
        self.model_ids = model_ids(hashes)

        started = time.monotonic()
        self._recognizer = sherpa_onnx.OfflineRecognizer.from_transducer(
            encoder=str(root / "gipformer/encoder.int8.onnx"),
            decoder=str(root / "gipformer/decoder.int8.onnx"),
            joiner=str(root / "gipformer/joiner.int8.onnx"),
            tokens=str(root / "gipformer/tokens.txt"),
            num_threads=threads,
            sample_rate=SAMPLE_RATE,
            feature_dim=80,
            decoding_method="modified_beam_search",
        )
        self._extractor = sherpa_onnx.SpeakerEmbeddingExtractor(
            sherpa_onnx.SpeakerEmbeddingExtractorConfig(
                model=str(root / "eres2net_base.onnx"), num_threads=threads
            )
        )

        self._vad_config = sherpa_onnx.VadModelConfig()
        self._vad_config.silero_vad.model = str(root / "silero_vad.onnx")
        self._vad_config.silero_vad.threshold = VAD_THRESHOLD
        self._vad_config.silero_vad.min_silence_duration = 0.5
        self._vad_config.silero_vad.min_speech_duration = 0.25
        # Leaves room for the padding below while staying under MAX_CHUNK_S.
        self._vad_config.silero_vad.max_speech_duration = MAX_CHUNK_S - 2 * VAD_PAD_S - 0.5
        self._vad_config.sample_rate = SAMPLE_RATE
        self._vad_config.num_threads = 1
        # SPEECH_PUNCTUATION=0 turns it off, to compare with and without; the model is still pinned.
        self._punctuator = (
            punctuation.Punctuator.load(root)
            if os.environ.get("SPEECH_PUNCTUATION", "1").lower() not in ("0", "false", "off")
            else None
        )
        log.info(
            "models loaded in %.1fs, %d threads, embedding dim %d, cluster threshold %.2f, "
            "min enrolment consistency %.2f, punctuation %s",
            time.monotonic() - started, threads, self._extractor.dim, self._cluster_threshold,
            self._min_consistency, "on" if self._punctuator else "off",
        )

    # --- building blocks (call with the lock held) ---------------------------------------------

    def _speech_segments(self, samples: np.ndarray) -> list[tuple[int, np.ndarray]]:
        """Voiced stretches as (start sample, samples), padded, each at most MAX_CHUNK_S long."""
        vad = self._onnx.VoiceActivityDetector(self._vad_config, buffer_size_in_seconds=MAX_CHUNK_S * 3)
        window = self._vad_config.silero_vad.window_size
        found: list[tuple[int, int]] = []

        def drain() -> None:
            while not vad.empty():
                found.append((vad.front.start, vad.front.start + len(vad.front.samples)))
                vad.pop()

        for start in range(0, samples.size, window):
            vad.accept_waveform(samples[start : start + window])
            drain()
        vad.flush()
        drain()
        return [(start, samples[start:end]) for start, end in pad_regions(found, samples.size)]

    def _transcribe_pieces(self, pieces: list[np.ndarray]) -> list[str]:
        texts: list[str] = []
        for first in range(0, len(pieces), 8):
            streams = []
            for piece in pieces[first : first + 8]:
                stream = self._recognizer.create_stream()
                stream.accept_waveform(SAMPLE_RATE, piece)
                streams.append(stream)
            self._recognizer.decode_streams(streams)
            texts.extend(stream.result.text for stream in streams)
        return texts

    def _readable(self, pieces: list[tuple[float, float, str | None, np.ndarray]], raws: list[str]) -> list[str]:
        """The recogniser's raw text of each piece, punctuated and capitalised."""
        if self._punctuator is None:
            return [text.normalise(raw) for raw in raws]
        out = [""] * len(pieces)
        for indices in passages(pieces, raws):
            lines = [" ".join(raws[i].split()).lower() for i in indices]
            restored = punctuation.passage(lines, self._punctuator)
            if restored == lines:  # the tagger changed the word count, or nothing at all
                restored = [text.normalise(line) for line in lines]
            for index, sentence in zip(indices, restored):
                out[index] = sentence
        return out

    def _embed(self, samples: np.ndarray) -> np.ndarray:
        stream = self._extractor.create_stream()
        stream.accept_waveform(SAMPLE_RATE, samples)
        stream.input_finished()
        vector = np.asarray(self._extractor.compute(stream), dtype=np.float32)
        return vector / (np.linalg.norm(vector) or 1.0)

    def _consistency(self, speech: np.ndarray) -> float:
        parts = windows(speech, int(CONSISTENCY_WINDOW_S * SAMPLE_RATE), CONSISTENCY_MAX_WINDOWS)
        return min_pairwise_cosine([self._embed(part) for part in parts])

    def _voice(self, turns: list[np.ndarray]) -> np.ndarray:
        """One embedding for a speaker, from its longest turns, at most SPEAKER_EMBED_MAX_S."""
        budget = int(SPEAKER_EMBED_MAX_S * SAMPLE_RATE)
        taken = []
        for audio in sorted(turns, key=lambda audio: audio.size, reverse=True):
            if budget <= 0:
                break
            taken.append(audio[:budget])
            budget -= taken[-1].size
        return self._embed(np.concatenate(taken))

    def _merge_same_voices(self, regions: list[tuple[int, np.ndarray]],
                           turns: list[tuple[int, int, int]]) -> list[tuple[int, int, int]]:
        """Merge clusters whose whole-voice embeddings say they are one person (MERGE_THRESHOLD)."""
        parts: dict[int, list[np.ndarray]] = defaultdict(list)
        for start, end, label in turns:
            parts[label].append(self._slice(regions, start, end))
        voices = {label: self._voice(audio) for label, audio in parts.items()}
        while (pair := closest_pair(voices)) is not None and pair[0] >= MERGE_THRESHOLD:
            _, keep, drop = pair
            parts[keep].extend(parts.pop(drop))
            del voices[drop]
            voices[keep] = self._voice(parts[keep])
            turns = [(start, end, keep if label == drop else label) for start, end, label in turns]
        return merge_turns(turns)

    def _diarize(self, regions: list[tuple[int, np.ndarray]], speakers: int | None) -> list[tuple[int, int, int]]:
        """(start sample, end sample, cluster) turns over the VAD's speech regions."""
        window, hop = int(WINDOW_S * SAMPLE_RATE), int(HOP_S * SAMPLE_RATE)
        spans: list[tuple[int, int, int]] = []
        short: list[tuple[int, int]] = []
        for group, (offset, audio) in enumerate(regions):
            if audio.size < MIN_REGION_S * SAMPLE_RATE:
                short.append((offset, offset + audio.size))
                continue
            for start in window_starts(audio.size, window, hop):
                spans.append((group, offset + start, offset + min(start + window, audio.size)))
        if not spans:
            return []

        by_group = {group: (offset, audio) for group, (offset, audio) in enumerate(regions)}

        def audio_of(group: int, start: int, end: int) -> np.ndarray:
            offset, audio = by_group[group]
            return audio[start - offset : end - offset]

        vectors = np.stack([self._embed(audio_of(*span)) for span in spans])
        clustering = self._onnx.FastClustering(
            self._onnx.FastClusteringConfig(
                num_clusters=min(speakers, len(spans)) if speakers else -1,
                threshold=self._cluster_threshold,
            )
        )
        labels = np.asarray(clustering(vectors), dtype=np.int64)
        if not speakers:
            labels = fold_small_clusters(vectors, labels, math.ceil(MIN_SPEAKER_S / HOP_S))
        labels = smooth(labels, [span[0] for span in spans])
        turns = spans_to_turns(spans, labels)

        if short:
            anchors = centroids(vectors, labels)
            for start, end in short:
                turns.append((start, end, nearest(self._embed(self._slice(regions, start, end)), anchors)))
        turns = [turn for turn in sorted(turns) if turn[1] - turn[0] >= MIN_TURN_S * SAMPLE_RATE]
        return self._merge_same_voices(regions, turns) if turns and not speakers else turns

    @staticmethod
    def _slice(regions: list[tuple[int, np.ndarray]], start: int, end: int) -> np.ndarray:
        for offset, audio in regions:
            if offset <= start and end <= offset + audio.size:
                return audio[start - offset : end - offset]
        raise ValueError("span outside every speech region")

    # --- operations ------------------------------------------------------------------------------

    def transcribe_file(self, path: str | os.PathLike[str], diarize: bool = True,
                        speakers: int | None = None) -> dict:
        samples = decode(path)
        with self._lock:
            started = time.monotonic()
            result = self._transcribe(samples, diarize, speakers)
            elapsed = time.monotonic() - started
        audio_s = samples.size / SAMPLE_RATE
        log.info(
            "transcribed %.1fs audio in %.1fs (RTF %.3f), diarize=%s, %d segments, %d speakers",
            audio_s, elapsed, elapsed / audio_s if audio_s else 0.0, diarize,
            len(result["segments"]), len(result["speakers"]),
        )
        return result

    def _transcribe(self, samples: np.ndarray, diarize: bool, speakers: int | None) -> dict:
        samples = level(samples)
        regions = self._speech_segments(samples)
        pieces: list[tuple[float, float, str | None, np.ndarray]] = []
        turns_by_label: dict[str, list[np.ndarray]] = defaultdict(list)
        if diarize:
            labels: dict[int, str] = {}
            for start, end, cluster in self._diarize(regions, speakers):
                label = labels.setdefault(cluster, f"SPEAKER_{len(labels):02d}")
                audio = self._slice(regions, start, end)
                turns_by_label[label].append(audio)
                for offset, part in hard_split(audio) if audio.size > MAX_CHUNK_S * SAMPLE_RATE else [(0, audio)]:
                    begin = (start + offset) / SAMPLE_RATE
                    pieces.append((begin, begin + part.size / SAMPLE_RATE, label, part))
        if not pieces:
            turns_by_label.clear()
            for offset, part in regions:
                begin = offset / SAMPLE_RATE
                pieces.append((begin, begin + part.size / SAMPLE_RATE, None, part))

        texts = self._readable(pieces, self._transcribe_pieces([piece[3] for piece in pieces]))
        segments = []
        for (start, end, label, _), sentence in zip(pieces, texts):
            if sentence:
                segments.append({
                    "startMs": round(start * 1000),
                    "endMs": round(end * 1000),
                    "speaker": label,
                    "text": sentence,
                })
        segments.sort(key=lambda segment: (segment["startMs"], segment["endMs"]))

        voices = []
        for label in sorted(turns_by_label):
            turns = turns_by_label[label]
            voices.append({
                "label": label,
                "speechMs": round(sum(audio.size for audio in turns) * 1000 / SAMPLE_RATE),
                "embedding": [round(float(x), 6) for x in self._voice(turns)],
            })

        return {
            "durationMs": round(samples.size * 1000 / SAMPLE_RATE),
            "language": "vi",
            "asrModel": self.model_ids["asr"],
            "embeddingModel": self.model_ids["embedding"],
            "segments": segments,
            "speakers": voices,
        }

    def embed_file(self, path: str | os.PathLike[str]) -> tuple[bool, dict]:
        """(accepted, body): the voice embedding of an enrolment sample, or why it was rejected."""
        samples = decode(path)
        with self._lock:
            speech = np.zeros(0, np.float32)
            if samples.size <= MAX_ENROL_AUDIO_S * SAMPLE_RATE:
                voiced = [part for _, part in self._speech_segments(samples)]
                if voiced:
                    speech = np.concatenate(voiced)
            verdict = assess_enrolment(samples, speech, self._consistency, self._min_consistency)
            if verdict.error:
                log.info("enrolment sample rejected: %s %s", verdict.error, verdict.metrics())
                return False, {"error": verdict.error, **verdict.metrics()}
            vector = self._embed(speech)
        return True, {
            "embedding": [round(float(x), 6) for x in vector],
            "model": self.model_ids["embedding"],
            **verdict.metrics(),
        }
