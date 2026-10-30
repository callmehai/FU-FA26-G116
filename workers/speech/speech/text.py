"""Turn the recogniser's raw output into text a person can read in a transcript."""

from __future__ import annotations

SENTENCE_END = ".?!…"


def normalise(raw: str) -> str:
    """Gipformer emits UPPERCASE words without punctuation; make one readable sentence of it.

    Lowercase, capitalise the first letter, collapse whitespace and close with a full stop. This is
    the fallback when punctuation is off or the tagger changed the word count
    (speech/punctuation.py restores commas and capitals otherwise).
    """
    text = " ".join(raw.split()).lower()
    if not text:
        return ""
    text = text[0].upper() + text[1:]
    if text[-1] not in SENTENCE_END:
        text += "."
    return text
