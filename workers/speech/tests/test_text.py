import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech.text import normalise  # noqa: E402


class NormaliseTests(unittest.TestCase):
    def test_uppercase_recogniser_output_becomes_a_sentence(self):
        self.assertEqual(
            normalise("KÍNH THƯA CÁC ĐỒNG CHÍ HÔM NAY CHÚNG TA HỌP"),
            "Kính thưa các đồng chí hôm nay chúng ta họp.",
        )

    def test_vietnamese_capital_d_is_lowercased(self):
        self.assertEqual(normalise("ĐỒNG Ý"), "Đồng ý.")

    def test_whitespace_is_collapsed(self):
        self.assertEqual(normalise("  TÔI   ĐỒNG\tÝ \n"), "Tôi đồng ý.")

    def test_existing_sentence_end_is_kept(self):
        self.assertEqual(normalise("CÓ AI PHẢN ĐỐI KHÔNG?"), "Có ai phản đối không?")

    def test_empty_text_stays_empty(self):
        self.assertEqual(normalise(""), "")
        self.assertEqual(normalise("   "), "")


if __name__ == "__main__":
    unittest.main()
