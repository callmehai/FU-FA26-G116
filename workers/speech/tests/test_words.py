import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import numpy as np  # noqa: E402

from speech import pipeline  # noqa: E402


class WordStartsTests(unittest.TestCase):
    def test_each_word_starts_at_its_first_token(self):
        # As sherpa-onnx returns Gipformer's tokens: the word mark shows as a leading space.
        tokens = [" KÍNH", " THƯ", "A", " CÁC"]
        self.assertEqual(pipeline.word_starts(tokens, [0.0, 0.28, 0.4, 0.6], 3), [0.0, 0.28, 0.6])

    def test_the_sentencepiece_mark_counts_too(self):
        self.assertEqual(pipeline.word_starts(["▁XIN", "▁CHÀO"], [0.1, 0.5], 2), [0.1, 0.5])

    def test_a_bare_mark_starts_the_word_that_follows_it(self):
        self.assertEqual(pipeline.word_starts(["▁", "XIN", " CHÀO"], [0.1, 0.2, 0.5], 2), [0.1, 0.5])

    def test_a_first_token_without_a_mark_still_starts_a_word(self):
        self.assertEqual(pipeline.word_starts(["XIN", " CHÀO"], [0.0, 0.4], 2), [0.0, 0.4])

    def test_no_timing_when_the_count_does_not_match_the_text(self):
        self.assertIsNone(pipeline.word_starts([" XIN", "CHÀO"], [0.0, 0.2], 2))
        self.assertIsNone(pipeline.word_starts([" XIN", " CHÀO"], [], 2))
        self.assertIsNone(pipeline.word_starts([], [], 0))


class WordTimesTests(unittest.TestCase):
    def test_times_move_to_the_recording_clock_and_stay_inside_the_piece(self):
        self.assertEqual(pipeline.word_times_ms(12.0, 13.0, [-0.1, 0.25, 0.2, 1.5]), [12000, 12250, 12250, 13000])

    def test_unknown_stays_unknown(self):
        self.assertIsNone(pipeline.word_times_ms(0.0, 1.0, None))

    def test_times_are_sent_only_while_they_match_the_readable_words(self):
        self.assertEqual(pipeline.aligned_words("Xin chào.", [0, 400]), [0, 400])
        self.assertIsNone(pipeline.aligned_words("Xin chào anh.", [0, 400]))
        self.assertIsNone(pipeline.aligned_words("Xin chào.", None))


def engine_with(restore):
    """An Engine with only the tagger set, enough for _readable."""
    engine = object.__new__(pipeline.Engine)
    engine._punctuator = restore
    return engine


def piece(start, end):
    return (start, end, None, np.zeros(0, np.float32))


class ContextTests(unittest.TestCase):
    def test_the_words_before_lose_their_marks_and_case(self):
        self.assertEqual(pipeline.context_words("Theo tôi thì, kế hoạch này."), "theo tôi thì kế hoạch này")
        self.assertIsNone(pipeline.context_words(None))
        self.assertIsNone(pipeline.context_words(" . "))
        self.assertIsNone(pipeline.context_words("chữ " * (pipeline.MAX_CONTEXT_WORDS + 1)))

    def test_a_sentence_cut_at_a_breath_goes_on_from_the_one_before(self):
        seen = []

        def restore(text):
            seen.append(text)
            return "Theo tôi thì kế hoạch này cần thêm thời gian"

        texts, before = engine_with(restore)._readable([piece(0, 2)], ["CẦN THÊM THỜI GIAN"], "Theo tôi thì kế hoạch này.")
        self.assertEqual(seen, ["theo tôi thì kế hoạch này cần thêm thời gian"])
        self.assertEqual(texts, ["cần thêm thời gian."])
        self.assertEqual(before, "Theo tôi thì kế hoạch này")

    def test_a_new_sentence_after_the_one_before_keeps_both_closed(self):
        restore = lambda text: "Rồi rồi Đây rồi"  # noqa: E731
        texts, before = engine_with(restore)._readable([piece(0, 1)], ["ĐÂY RỒI"], "Rồi rồi.")
        self.assertEqual((texts, before), (["Đây rồi."], "Rồi rồi."))

    def test_the_sentence_before_keeps_the_case_it_started_with(self):
        restore = lambda text: "Thêm thời gian Sao tôi không thấy"  # noqa: E731
        texts, before = engine_with(restore)._readable([piece(0, 1)], ["SAO TÔI KHÔNG THẤY"], "thêm thời gian.")
        self.assertEqual((texts, before), (["Sao tôi không thấy."], "thêm thời gian."))

    def test_without_context_nothing_comes_back_for_it(self):
        restore = lambda text: "Xin chào"  # noqa: E731
        self.assertEqual(engine_with(restore)._readable([piece(0, 1)], ["XIN CHÀO"]), (["Xin chào."], None))


if __name__ == "__main__":
    unittest.main()
