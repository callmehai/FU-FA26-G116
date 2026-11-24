"""Hotwords: bias Gipformer towards the words a TrustCabinet meeting uses (contextual biasing).

sherpa-onnx's modified beam search can boost listed phrases while it decodes: every partial
hypothesis that walks a listed phrase gets `score` added per token, and loses it again when it leaves
the phrase half-way (Aho-Corasick context graph, the same "hotwords" icefall uses). The model is not
changed, the cost is a few per cent of decoding time.

Gipformer is a BPE model, so sherpa-onnx needs the SentencePiece vocabulary (`piece<TAB>score`) to
cut each phrase into the model's own tokens. The repository publishes `bpe.model`, not that list; the
vocabulary is read out of `bpe.model` here (a protobuf, parsed by hand: the worker has no
sentencepiece or protobuf package) and written next to the prepared phrase list in a folder under the
system temporary directory.

The phrase list (`hotwords.txt` beside this file) is one phrase per line, written the way the
model can spell it: the model writes UPPERCASE Vietnamese syllables, and English words only in the
spelling it learnt (it has tokens for every Latin letter, so "DEPLOY" is reachable, but whether the
acoustics lead there is measured, not assumed — see README, "Từ khoá ưu tiên"). A line may end in
` :<score>` to give that phrase its own boost; `#` starts a comment.
"""

from __future__ import annotations

import hashlib
import os
import struct
import tempfile
from dataclasses import dataclass
from pathlib import Path

# The shipped list, used unless SPEECH_HOTWORDS_FILE names another (or turns it off).
DEFAULT_LIST = Path(__file__).resolve().parent / "hotwords.txt"
# The model's SentencePiece model, pinned in models.manifest.
BPE_MODEL = "gipformer/bpe.model"
# Boost added per matched token (sherpa-onnx's `hotwords_score`). On the room recording of 23/11/2026 (README, "Từ khoá
# ưu tiên") 1.0 got more of the listed words right at the same WER; 1.5, 2 and 3 got a few more of them but changed
# other words too, and WER rose. SPEECH_HOTWORDS_SCORE overrides it.
DEFAULT_SCORE = 1.0
OFF = ("0", "false", "off", "no", "none")


@dataclass(frozen=True)
class Settings:
    """Where the phrase list is and how hard it pulls; `path` None means hotwords are off."""

    path: Path | None
    score: float


def settings(environ: dict[str, str] | None = None) -> Settings:
    """Read SPEECH_HOTWORDS_FILE (a path, or 0/off) and SPEECH_HOTWORDS_SCORE (0 turns it off too)."""
    environ = os.environ if environ is None else environ
    named = (environ.get("SPEECH_HOTWORDS_FILE") or "").strip()
    raw_score = (environ.get("SPEECH_HOTWORDS_SCORE") or "").strip()
    score = float(raw_score) if raw_score else DEFAULT_SCORE
    if named.lower() in OFF or score <= 0:
        return Settings(None, score)
    return Settings(Path(named).expanduser() if named else DEFAULT_LIST, score)


def phrases(text: str) -> list[str]:
    """The phrase lines of a list, upper-cased the way the model writes, without comments or repeats.

    A trailing ` :<score>` is kept as sherpa-onnx reads it; whitespace inside a phrase collapses to one space.
    """
    seen: set[str] = set()
    out: list[str] = []
    for line in text.splitlines():
        line = line.split("#", 1)[0].strip()
        if not line:
            continue
        words = line.split()
        boost = words.pop() if len(words) > 1 and words[-1].startswith(":") and _is_number(words[-1][1:]) else None
        if not words:
            continue
        phrase = " ".join(word.upper() for word in words)
        if phrase in seen:
            continue
        seen.add(phrase)
        out.append(f"{phrase} {boost}" if boost else phrase)
    return out


def _is_number(text: str) -> bool:
    try:
        float(text)
    except ValueError:
        return False
    return True


# --- SentencePiece model → vocabulary ---------------------------------------------------------------
# ModelProto { repeated SentencePiece pieces = 1; ... }, SentencePiece { string piece = 1; float score = 2; ... }.

def _varint(data: bytes, at: int) -> tuple[int, int]:
    value = shift = 0
    while True:
        if at >= len(data):
            raise ValueError("truncated varint")
        byte = data[at]
        at += 1
        value |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return value, at
        shift += 7


def _fields(data: bytes):
    """(field number, wire type, value) of each field of one protobuf message; nested messages stay bytes."""
    at = 0
    while at < len(data):
        key, at = _varint(data, at)
        number, wire = key >> 3, key & 7
        if wire == 0:
            value, at = _varint(data, at)
        elif wire == 1:
            value, at = data[at : at + 8], at + 8
        elif wire == 2:
            length, at = _varint(data, at)
            value, at = data[at : at + length], at + length
        elif wire == 5:
            value, at = data[at : at + 4], at + 4
        else:
            raise ValueError(f"unsupported protobuf wire type {wire}")
        if at > len(data):
            raise ValueError("truncated protobuf field")
        yield number, wire, value


def bpe_vocab(model: bytes) -> list[tuple[str, float]]:
    """The (piece, score) list of a SentencePiece model, in id order — what `spm_export_vocab` writes."""
    vocab = []
    for number, wire, value in _fields(model):
        if number != 1 or wire != 2:
            continue
        piece, score = None, 0.0
        for inner, inner_wire, inner_value in _fields(value):
            if inner == 1 and inner_wire == 2:
                piece = inner_value.decode("utf-8")
            elif inner == 2 and inner_wire == 5:
                score = struct.unpack("<f", inner_value)[0]
        if piece is None:
            raise ValueError("a SentencePiece entry has no piece")
        vocab.append((piece, score))
    if not vocab:
        raise ValueError("no pieces in the SentencePiece model")
    return vocab


@dataclass(frozen=True)
class Prepared:
    """The files sherpa-onnx reads, plus what the log says about them."""

    hotwords_file: str
    bpe_vocab: str
    score: float
    count: int
    source: Path


def prepare(models_root: Path, chosen: Settings, folder: Path | None = None) -> Prepared | None:
    """Write the upper-cased phrase list and the BPE vocabulary for sherpa-onnx; None when off or the list is empty."""
    if chosen.path is None:
        return None
    lines = phrases(chosen.path.read_text(encoding="utf-8"))
    if not lines:
        return None
    model = (models_root / BPE_MODEL).read_bytes()
    listed = "\n".join(lines) + "\n"
    vocab = "".join(f"{piece}\t{score:.6g}\n" for piece, score in bpe_vocab(model))
    if folder is None:
        # Named by what it holds, so the model process the worker restarts after each long job reuses it rather
        # than leaving one more folder in /tmp every time.
        digest = hashlib.sha256(model + b"\0" + listed.encode("utf-8")).hexdigest()[:16]
        folder = Path(tempfile.gettempdir()) / f"speech-hotwords-{digest}"
    folder.mkdir(parents=True, exist_ok=True)
    list_path, vocab_path = folder / "hotwords.txt", folder / "bpe.vocab"
    _write(vocab_path, vocab)
    _write(list_path, listed)
    return Prepared(str(list_path), str(vocab_path), chosen.score, len(lines), chosen.path)


def _write(path: Path, content: str) -> None:
    """Write whole or not at all: a second worker process may be reading the same file."""
    if path.is_file() and path.read_text(encoding="utf-8") == content:
        return
    partial = path.with_name(f".{path.name}.{os.getpid()}")
    partial.write_text(content, encoding="utf-8")
    partial.replace(path)


def recognizer_options(prepared: Prepared | None) -> dict:
    """The extra keyword arguments for `OfflineRecognizer.from_transducer`; empty when hotwords are off."""
    if prepared is None:
        return {}
    return {
        "hotwords_file": prepared.hotwords_file,
        "hotwords_score": prepared.score,
        "modeling_unit": "bpe",
        "bpe_vocab": prepared.bpe_vocab,
    }
