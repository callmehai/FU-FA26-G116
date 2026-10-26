"""Turn the recogniser's raw output into text a person can read in a transcript."""

from __future__ import annotations

SENTENCE_END = ".?!…"


def normalise(raw: str) -> str:
    """Gipformer emits UPPERCASE words without punctuation; make one readable sentence of it.

    Lowercase, capitalise the first letter, collapse whitespace and close with a full stop. No
    attempt is made to restore commas or proper nouns — the secretary corrects those by hand.
    """
    text = " ".join(raw.split()).lower()
    if not text:
        return ""
    text = text[0].upper() + text[1:]
    if text[-1] not in SENTENCE_END:
        text += "."
    return text
