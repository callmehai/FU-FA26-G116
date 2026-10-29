import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech import pipeline  # noqa: E402
from speech.punctuation import Punctuator, WordPiece, apply_tags, passage  # noqa: E402

LABELS = ["$KEEP", "$TRANSFORM_CASE_CAPITAL", "$APPEND_,", "$APPEND_.", "$APPEND_?", "$MERGE_SPACE"]


class ScriptedSession:
    """Stands in for the ONNX session: tags each word from a table, and records what it was fed."""

    def __init__(self, tags_by_word: dict[int, str]) -> None:
        self.tags_by_word = tags_by_word  # token id -> tag
        self.calls: list[dict[str, np.ndarray]] = []

    def run(self, _outputs, feeds):
        self.calls.append(feeds)
        ids = feeds["input_ids"][0]
        offsets = feeds["input_offsets"][0]
        logits = np.zeros((1, len(offsets), len(LABELS)), dtype=np.float32)
        for position, offset in enumerate(offsets):
            tag = self.tags_by_word.get(int(ids[offset]), "$KEEP")
            logits[0, position, LABELS.index(tag)] = 1.0
        return [logits]


VOCAB = ["[PAD]", "[UNK]", "xin", "chào", "anh", "nam", "##m", "có", "không"]


def ids(word: str) -> int:
    return VOCAB.index(word)


class WordPieceTests(unittest.TestCase):
    def test_longest_match_then_continuation(self):
        self.assertEqual(WordPiece(VOCAB).ids("namm"), [ids("nam"), ids("##m")])

    def test_a_word_with_no_match_is_unknown(self):
        self.assertEqual(WordPiece(VOCAB).ids("xyz"), [ids("[UNK]")])

    def test_start_token_comes_after_the_vocabulary(self):
        self.assertEqual(WordPiece(VOCAB).ids("$START"), [len(VOCAB)])


class ApplyTagsTests(unittest.TestCase):
    def test_first_tag_belongs_to_start(self):
        self.assertEqual(
            apply_tags(["xin", "chào"], ["$APPEND_.", "$TRANSFORM_CASE_CAPITAL", "$APPEND_."]),
            ["Xin", "chào."],
        )

    def test_a_second_pass_adds_no_second_mark(self):
        self.assertEqual(apply_tags(["chào."], ["$KEEP", "$APPEND_,"]), ["chào."])

    def test_tags_that_would_change_the_word_count_are_ignored(self):
        self.assertEqual(apply_tags(["xin", "chào"], ["$KEEP", "$MERGE_SPACE", "$KEEP"]), ["xin", "chào"])


class PunctuatorTests(unittest.TestCase):
    def test_lowercases_then_applies_the_tags(self):
        session = ScriptedSession({ids("xin"): "$TRANSFORM_CASE_CAPITAL", ids("chào"): "$APPEND_."})
        punctuator = Punctuator(WordPiece(VOCAB), LABELS, session)
        self.assertEqual(punctuator("XIN CHÀO"), "Xin chào.")

    def test_stops_reading_again_once_nothing_changes(self):
        session = ScriptedSession({})
        Punctuator(WordPiece(VOCAB), LABELS, session)("xin chào")
        self.assertEqual(len(session.calls), 1)

    def test_long_text_is_read_in_overlapping_windows_and_keeps_every_word(self):
        session = ScriptedSession({})
        punctuator = Punctuator(WordPiece(VOCAB), LABELS, session, chunk_words=4, context_words=1)
        words = ["xin", "chào", "anh", "nam", "có", "không", "xin", "chào", "anh"]
        self.assertEqual(punctuator(" ".join(words)), " ".join(words))
        # Windows start at 0, 3, 6: each one re-reads the last word of the window before.
        starts = [len(call["input_offsets"][0]) - 1 for call in session.calls]
        self.assertEqual(starts, [4, 4, 3])


class PassageTests(unittest.TestCase):
    def test_one_sentence_across_two_lines_is_closed_only_at_the_end(self):
        restore = lambda text: "Xin chào anh Nam, có không"  # noqa: E731
        self.assertEqual(passage(["xin chào", "anh nam có không"], restore), ["Xin chào", "anh Nam, có không."])

    def test_a_changed_word_count_returns_the_lines_unchanged(self):
        restore = lambda text: "Xinchào."  # noqa: E731
        self.assertEqual(passage(["xin chào"], restore), ["xin chào"])

    def test_no_words_returns_the_lines(self):
        self.assertEqual(passage(["", " "], lambda text: text), ["", " "])


def piece(start: float, end: float, label: str | None):
    return (start, end, label, np.zeros(0, np.float32))


class PassagesTests(unittest.TestCase):
    def test_runs_of_one_speaker_join_until_a_long_pause_or_another_speaker(self):
        pieces = [
            piece(0, 2, "SPEAKER_00"),
            piece(2.5, 4, "SPEAKER_00"),
            piece(4.2, 6, "SPEAKER_01"),
            piece(6.5, 8, "SPEAKER_00"),
            piece(8 + pipeline.PASSAGE_MAX_GAP_S + 1, 20, "SPEAKER_00"),
        ]
        self.assertEqual(pipeline.passages(pieces, ["A"] * 5), [[0, 1], [2], [3], [4]])

    def test_pieces_without_words_are_skipped_and_order_is_by_time(self):
        pieces = [piece(3, 4, None), piece(0, 1, None), piece(1.5, 2, None)]
        self.assertEqual(pipeline.passages(pieces, ["B", "A", "  "]), [[1, 0]])


if __name__ == "__main__":
    unittest.main()
