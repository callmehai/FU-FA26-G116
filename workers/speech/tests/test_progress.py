import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import numpy as np  # noqa: E402

from speech import pipeline, progress  # noqa: E402


class TrackerTests(unittest.TestCase):
    def tracker(self, diarize=True):
        sent = []
        return progress.Tracker(sent.append, progress.stages_for(diarize)), sent

    def test_the_weights_make_up_the_whole_run(self):
        self.assertAlmostEqual(sum(progress.STAGE_WEIGHTS.values()), 1.0)

    def test_a_stage_moves_the_bar_within_its_own_share(self):
        track, sent = self.tracker()
        track("decode", 1.0)
        track("detect", 1.0)
        before = sent[-1]
        track("diarize", 0.5)
        self.assertAlmostEqual(sent[-1], round(before + progress.STAGE_WEIGHTS["diarize"] / 2, 3), places=3)
        track("voices", 1.0)
        self.assertEqual(sent[-1], 1.0)

    def test_it_never_goes_back(self):
        track, sent = self.tracker()
        track("recognise", 0.5)
        track("diarize", 0.9)  # an earlier stage, reported late
        track("recognise", 0.2)
        self.assertEqual(sent, sorted(sent))
        self.assertEqual(len(sent), 1)

    def test_it_sends_at_most_once_per_thousandth(self):
        track, sent = self.tracker()
        for step in range(10_000):
            track("recognise", step / 10_000)
        self.assertLessEqual(len(sent), 1000)
        self.assertEqual(len(sent), len(set(sent)))

    def test_without_diarization_the_other_stages_fill_the_whole(self):
        track, sent = self.tracker(diarize=False)
        track("diarize", 1.0)  # not one of its stages: ignored
        self.assertEqual(sent, [])
        track("punctuate", 1.0)
        self.assertEqual(sent[-1], 1.0)
        track, sent = self.tracker(diarize=False)
        track("recognise", 0.0)
        weights = progress.STAGE_WEIGHTS
        share = (weights["decode"] + weights["detect"]) / sum(weights[s] for s in progress.WITHOUT_DIARIZATION)
        self.assertAlmostEqual(sent[-1], share, places=2)

    def test_fractions_out_of_range_are_clamped(self):
        track, sent = self.tracker()
        track("voices", 7.0)
        self.assertEqual(sent, [1.0])


class FakeRecognizer:
    """Hears one word per piece."""

    def create_stream(self):
        stream = SimpleNamespace(result=SimpleNamespace(text="A", tokens=[" A"], timestamps=[0.0], ys_log_probs=[0.0]))
        stream.accept_waveform = lambda rate, samples: None
        return stream

    def decode_streams(self, streams):
        pass


class StageReportTests(unittest.TestCase):
    """The stages report how far they got as they go, not only at their end."""

    def engine(self):
        engine = object.__new__(pipeline.Engine)
        engine._recognizer = FakeRecognizer()
        engine._punctuator = None
        self.reports = []
        engine._track = lambda stage, fraction: self.reports.append((stage, fraction))
        return engine

    def test_recognition_reports_each_piece_by_audio_done(self):
        # 17 pieces, one at a time (RECOGNISE_BATCH); the first eight hold most of the audio.
        self.assertEqual(pipeline.RECOGNISE_BATCH, 1)
        pieces = [np.zeros(16000 * 10, np.float32)] * 8 + [np.zeros(1600, np.float32)] * 9
        texts, _, _ = self.engine()._transcribe_pieces(pieces)
        self.assertEqual(len(texts), 17)
        fractions = [fraction for stage, fraction in self.reports if stage == "recognise"]
        self.assertEqual(len(fractions), 17)
        self.assertEqual(fractions[0], 0.0)
        self.assertGreater(fractions[8], 0.9)
        self.assertEqual(fractions, sorted(fractions))

    def test_punctuation_reports_each_passage(self):
        engine = self.engine()
        engine._punctuator = object()
        pieces = [(0.0, 1.0, "A", None), (20.0, 21.0, "B", None), (40.0, 41.0, "A", None)]
        original = pipeline.punctuation.passage
        pipeline.punctuation.passage = lambda lines, tagger: lines
        try:
            engine._readable(pieces, ["một", "hai", "ba"])
        finally:
            pipeline.punctuation.passage = original
        self.assertEqual(self.reports, [("punctuate", 0.0), ("punctuate", 1 / 3), ("punctuate", 2 / 3)])


if __name__ == "__main__":
    unittest.main()
