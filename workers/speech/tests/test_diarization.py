import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech import pipeline  # noqa: E402


class WindowStartTests(unittest.TestCase):
    def test_short_region_is_one_window(self):
        self.assertEqual(pipeline.window_starts(10, 15, 7), [0])

    def test_last_window_is_aligned_to_the_end(self):
        self.assertEqual(pipeline.window_starts(40, 15, 7), [0, 7, 14, 21, 25])

    def test_exact_fit_adds_nothing(self):
        self.assertEqual(pipeline.window_starts(29, 15, 7), [0, 7, 14])


class PadRegionTests(unittest.TestCase):
    def test_pads_without_overlapping_neighbours_or_bounds(self):
        regions = [(5, 20), (24, 40), (100, 118)]
        self.assertEqual(
            pipeline.pad_regions(regions, total=120, pad=10),
            [(0, 24), (20, 50), (90, 120)],
        )

    def test_empty(self):
        self.assertEqual(pipeline.pad_regions([], total=10, pad=3), [])


def unit(*values):
    vector = np.array(values, dtype=np.float32)
    return vector / np.linalg.norm(vector)


class FoldSmallClusterTests(unittest.TestCase):
    def test_small_cluster_joins_the_nearest_large_one(self):
        vectors = np.stack([unit(1, 0), unit(1, 0.1), unit(1, -0.1), unit(0, 1), unit(0.1, 1),
                            unit(-0.1, 1), unit(0.9, 0.2)])
        labels = np.array([0, 0, 0, 1, 1, 1, 2])
        self.assertEqual(pipeline.fold_small_clusters(vectors, labels, 3).tolist(), [0, 0, 0, 1, 1, 1, 0])

    def test_nothing_changes_when_every_cluster_is_small(self):
        labels = np.array([0, 1])
        vectors = np.stack([unit(1, 0), unit(0, 1)])
        self.assertEqual(pipeline.fold_small_clusters(vectors, labels, 3).tolist(), [0, 1])


class SmoothTests(unittest.TestCase):
    def test_single_disagreeing_window_takes_its_neighbours_label(self):
        labels = np.array([0, 1, 0, 0, 1, 1])
        self.assertEqual(pipeline.smooth(labels, [0] * 6).tolist(), [0, 0, 0, 0, 1, 1])

    def test_does_not_smooth_across_regions(self):
        labels = np.array([0, 1, 0])
        self.assertEqual(pipeline.smooth(labels, [0, 1, 2]).tolist(), [0, 1, 0])


class SpansToTurnsTests(unittest.TestCase):
    def test_overlaps_split_at_their_middle_and_runs_merge(self):
        spans = [(0, 0, 15), (0, 7, 22), (0, 14, 29), (0, 21, 36)]
        labels = np.array([0, 0, 1, 1])
        # Boundary between window 1 (7–22) and window 2 (14–29) is the middle of 14–22.
        self.assertEqual(pipeline.spans_to_turns(spans, labels), [(0, 18, 0), (18, 36, 1)])

    def test_regions_never_merge(self):
        spans = [(0, 0, 10), (1, 50, 60)]
        self.assertEqual(pipeline.spans_to_turns(spans, np.array([0, 0])), [(0, 10, 0), (50, 60, 0)])


class MergeTurnsTests(unittest.TestCase):
    def test_back_to_back_turns_of_one_speaker_join(self):
        turns = [(18, 36, 0), (0, 18, 0), (36, 50, 1)]
        self.assertEqual(pipeline.merge_turns(turns), [(0, 36, 0), (36, 50, 1)])

    def test_a_pause_stays_a_cut(self):
        self.assertEqual(pipeline.merge_turns([(0, 10, 0), (50, 60, 0)]), [(0, 10, 0), (50, 60, 0)])


class ClosestPairTests(unittest.TestCase):
    def test_finds_the_most_similar_voices_and_keeps_the_lower_label(self):
        voices = {0: unit(1, 0), 3: unit(0, 1), 5: unit(1, 0.1)}
        cosine, keep, drop = pipeline.closest_pair(voices)
        self.assertAlmostEqual(cosine, float(unit(1, 0) @ unit(1, 0.1)), places=5)
        self.assertEqual((keep, drop), (0, 5))

    def test_one_voice_has_no_pair(self):
        self.assertIsNone(pipeline.closest_pair({0: unit(1, 0)}))


class FoldSmallVoiceTests(unittest.TestCase):
    """After the second pass, a voice under MIN_SPEAKER_SHARE of the speech gives each of its turns to the closest
    other voice, judged on the whole turn (stubs: a turn's audio is its start, its embedding is looked up)."""

    def engine(self, turn_vectors, voice_vectors):
        engine = object.__new__(pipeline.Engine)
        engine._slice = lambda regions, start, end: np.array([start])
        engine._embed = lambda audio: turn_vectors[int(audio[0])]
        engine._voice = lambda parts: voice_vectors[tuple(int(audio[0]) for audio in parts)]
        engine._check = lambda: None
        return engine

    def test_each_small_turn_takes_its_own_nearest_voice(self):
        # Voices 0 and 1 hold 98 and 100 samples; voice 2 holds 2 of 200 (1%), in two turns that sound like 1 and 0.
        turns = [(0, 98, 0), (100, 101, 2), (101, 201, 1), (300, 301, 2)]
        engine = self.engine({100: unit(0.1, 1), 300: unit(1, 0.1)}, {(0,): unit(1, 0), (101,): unit(0, 1)})
        self.assertEqual(engine._fold_small_voices([], turns), [(0, 98, 0), (100, 201, 1), (300, 301, 0)])

    def test_a_voice_at_the_share_stays(self):
        turns = [(0, 96, 0), (100, 104, 2), (104, 200, 1)]  # 4 of 196 samples: 2.04%
        self.assertEqual(self.engine({}, {})._fold_small_voices([], turns), turns)


if __name__ == "__main__":
    unittest.main()
