"""Put commas, full stops, question marks and capitals back into the recogniser's output.

Gipformer emits UPPERCASE words with no punctuation at all. viBERT-capu (viBERT by FPT AI, fine-tuned
by dragonSwing on Vietnamese web text; int8 ONNX export by welcomyou; CC BY-SA 4.0) reads lower-cased
words and tags each one with an edit: keep it, capitalise it, or append ``,`` ``.`` ``:`` ``?``. The
tags are applied, and the text read again, up to three times, since one edit can change the next.

The tagger only ever edits a word in place. It never adds, drops or merges words, so the number of
words stays the same and the time of every segment still holds after the text is restored.

The tokenizer is the plain BERT WordPiece, written out here rather than pulling in ``transformers``.
"""

from __future__ import annotations

import unicodedata
from collections.abc import Callable, Sequence
from pathlib import Path
from typing import Protocol

import numpy as np

MODEL_FILE = "punctuation/vibert-capu.int8.onnx"
VOCAB_FILE = "punctuation/vocab.txt"
LABELS_FILE = "punctuation/labels.txt"

START = "$START"
MARKS = ".,:?"
SENTENCE_END = ".?!…"
# Words read at once. A window's last CONTEXT_WORDS are read again at the start of the next one:
# where a window's text runs out, the model ends a sentence the speaker never ended.
CHUNK_WORDS = 80
CONTEXT_WORDS = 20
PASSES = 3
# The tagger is small; two threads keep it from competing with the recogniser for every core.
THREADS = 2

Restore = Callable[[str], str]


class WordPiece:
    """BERT WordPiece over one word already split on spaces: longest match first, ``##`` after."""

    def __init__(self, vocab: Sequence[str]) -> None:
        self._ids = {token: index for index, token in enumerate(vocab)}
        self._unknown = self._ids["[UNK]"]
        # $START was appended after the BERT vocabulary when the tagger was trained.
        self._start = len(vocab)

    def ids(self, word: str) -> list[int]:
        if word == START:
            return [self._start]
        pieces: list[int] = []
        at = 0
        while at < len(word):
            for end in range(len(word), at, -1):
                piece = word[at:end] if at == 0 else "##" + word[at:end]
                if piece in self._ids:
                    pieces.append(self._ids[piece])
                    at = end
                    break
            else:
                return [self._unknown]
        return pieces


def apply_tags(words: Sequence[str], tags: Sequence[str]) -> list[str]:
    """``tags[0]`` belongs to ``$START``; ``tags[i]`` edits ``words[i - 1]``.

    Only punctuation and case edits are applied; the model's other tags (merging words, changing a
    verb form) would change the word count, and they are meant for English grammar anyway.
    """
    out = list(words)
    for index, tag in enumerate(tags[1 : len(words) + 1]):
        if tag.startswith("$APPEND_"):
            mark = tag.removeprefix("$APPEND_")
            if mark in MARKS and out[index][-1:] not in MARKS:  # a later pass adds no second mark
                out[index] += mark
        elif tag == "$TRANSFORM_CASE_CAPITAL":
            out[index] = out[index][:1].upper() + out[index][1:]
        elif tag == "$TRANSFORM_CASE_UPPER":
            out[index] = out[index].upper()
        elif tag == "$TRANSFORM_CASE_LOWER":
            out[index] = out[index].lower()
    return out


class Session(Protocol):
    def run(self, output_names: object, feeds: dict[str, np.ndarray]) -> list[np.ndarray]: ...


class Punctuator:
    """The loaded tagger; call it with plain words, get them back punctuated and capitalised."""

    def __init__(self, tokenizer: WordPiece, labels: Sequence[str], session: Session,
                 chunk_words: int = CHUNK_WORDS, context_words: int = CONTEXT_WORDS) -> None:
        self._tokenizer = tokenizer
        self._labels = list(labels)
        self._session = session
        self._chunk = chunk_words
        self._context = min(context_words, chunk_words - 1)

    @classmethod
    def load(cls, root: Path, threads: int = THREADS) -> Punctuator:
        import onnxruntime  # imported here so the unit tests run without the native library

        options = onnxruntime.SessionOptions()
        options.intra_op_num_threads = threads
        options.inter_op_num_threads = 1
        session = onnxruntime.InferenceSession(
            str(root / MODEL_FILE), options, providers=["CPUExecutionProvider"]
        )
        vocab = (root / VOCAB_FILE).read_text(encoding="utf-8").splitlines()
        labels = (root / LABELS_FILE).read_text(encoding="utf-8").split()
        return cls(WordPiece(vocab), labels, session)

    def __call__(self, text: str) -> str:
        words = unicodedata.normalize("NFC", text).lower().split()
        out: list[str] = []
        at = 0
        while at < len(words):
            chunk = words[at : at + self._chunk]
            for _ in range(PASSES):
                edited = apply_tags(chunk, self._tags(chunk))
                if edited == chunk:
                    break
                chunk = edited
            last = at + self._chunk >= len(words)
            keep = len(chunk) if last else self._chunk - self._context
            out.extend(chunk[:keep])
            at += keep
        return " ".join(out)

    def _tags(self, words: Sequence[str]) -> list[str]:
        ids: list[int] = []
        offsets: list[int] = []
        for word in [START, *words]:
            offsets.append(len(ids))
            ids.extend(self._tokenizer.ids(word))
        input_ids = np.array([ids], dtype=np.int64)
        logits = self._session.run(None, {
            "input_ids": input_ids,
            "attention_mask": np.ones_like(input_ids),
            "token_type_ids": np.zeros_like(input_ids),
            "input_offsets": np.array([offsets], dtype=np.int64),
        })[0]
        return [self._labels[index] for index in logits[0].argmax(axis=-1)]


def passage(lines: Sequence[str], restore: Restore) -> list[str]:
    """Punctuate the lines one speaker said in a row as one passage, then cut them apart again.

    Read alone, every line would end in a full stop wherever the speaker paused for breath. Each
    line keeps its own words, so its time still holds. Should the word count change after all,
    the lines come back as they went in.
    """
    words = [line.split() for line in lines]
    flat = [word for line in words for word in line]
    if not flat:
        return list(lines)
    out = restore(" ".join(flat)).split()
    if len(out) != len(flat):
        return list(lines)
    if out[-1][-1:] not in SENTENCE_END:  # the model may leave the last sentence open
        out[-1] = out[-1].rstrip(MARKS) + "."
    texts = []
    at = 0
    for line in words:
        texts.append(" ".join(out[at : at + len(line)]))
        at += len(line)
    return texts
