"""Kappa, agreement and per-draft metrics of score.py. Standard library only:

    python3 -m unittest discover -s tests/ai/real/tests
"""

import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
import score  # noqa: E402


class Kappa(unittest.TestCase):
    def test_textbook_example(self):
        # 50 items: both yes 20, A yes/B no 5, A no/B yes 10, both no 15 -> po 0.70, pe 0.50, kappa 0.40.
        a = ["yes"] * 25 + ["no"] * 25
        b = ["yes"] * 20 + ["no"] * 5 + ["yes"] * 10 + ["no"] * 15
        self.assertAlmostEqual(score.cohen_kappa(a, b), 0.4)
        self.assertAlmostEqual(score.percent_agreement(a, b), 0.7)

    def test_perfect_and_chance(self):
        self.assertEqual(score.cohen_kappa(["Đúng", "Bịa"], ["Đúng", "Bịa"]), 1.0)
        self.assertEqual(score.cohen_kappa(["Đúng"] * 4, ["Đúng"] * 4), 1.0)
        self.assertAlmostEqual(score.cohen_kappa(["a", "a", "b", "b"], ["a", "b", "a", "b"]), 0.0)
        self.assertLess(score.cohen_kappa(["a", "b"], ["b", "a"]), 0)

    def test_empty_and_mismatched(self):
        self.assertIsNone(score.cohen_kappa([], []))
        with self.assertRaises(ValueError):
            score.cohen_kappa(["a"], [])

    def test_words(self):
        self.assertEqual(score.kappa_words(0.75), "khá cao")
        self.assertEqual(score.kappa_words(0.9), "gần như tuyệt đối")
        self.assertEqual(score.kappa_words(None), "—")


class Metrics(unittest.TestCase):
    def test_draft_metrics(self):
        claims = [{"label": "Đúng", "source": "", "wrong": ""}] * 6 + [
            {"label": "Sai lệch", "source": "Gỡ băng", "wrong": "Tên"},
            {"label": "Bịa", "source": "AI", "wrong": "Số"},
            {"label": "Không kiểm được", "source": "", "wrong": ""},
            {"label": "", "source": "", "wrong": ""},
        ]
        points = [{"level": "Chính", "coverage": "Có"}, {"level": "Chính", "coverage": "Một phần"},
                  {"level": "Phụ", "coverage": "Không"}, {"level": "Chính", "coverage": ""}]
        m = score.draft_metrics(claims, points)
        self.assertEqual((m["claims"], m["labelled"], m["unlabelled"], m["checkable"]), (10, 9, 1, 8))
        self.assertAlmostEqual(m["precision"], 6 / 8)
        self.assertAlmostEqual(m["hallucination"], 1 / 9)
        self.assertEqual((m["errAsr"], m["errAi"], m["wrongNames"], m["wrongNumbers"]), (1, 1, 1, 1))
        self.assertAlmostEqual(m["recall"], 1.5 / 3)
        self.assertAlmostEqual(m["recallStrict"], 1 / 3)
        self.assertAlmostEqual(m["recallMain"], 1.5 / 2)

    def test_pool_is_micro_average(self):
        a = score.draft_metrics([{"label": "Đúng", "source": "", "wrong": ""}] * 9 + [{"label": "Bịa", "source": "AI", "wrong": ""}], [])
        b = score.draft_metrics([{"label": "Bịa", "source": "AI", "wrong": ""}], [])
        pooled = score.pool([a, b])
        self.assertAlmostEqual(pooled["precision"], 9 / 11)
        self.assertAlmostEqual(pooled["hallucination"], 2 / 11)
        self.assertIsNone(pooled["recall"])

    def test_wilson_and_spread(self):
        low, high = score.wilson(8, 10)
        self.assertTrue(0.44 < low < 0.5 and 0.94 < high < 0.97)
        self.assertEqual(score.spread([None, None]), (None, None))
        mean, sd = score.spread([1.0, 3.0, None])
        self.assertEqual(mean, 2.0)
        self.assertAlmostEqual(sd, 2 ** 0.5)


if __name__ == "__main__":
    unittest.main()
