import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech import pipeline  # noqa: E402

RATE = pipeline.SAMPLE_RATE


def sine(seconds: float, amplitude: float, hz: float = 220.0) -> np.ndarray:
    t = np.arange(int(seconds * RATE)) / RATE
    return (amplitude * np.sin(2 * np.pi * hz * t)).astype(np.float32)


def consistent(_: np.ndarray) -> float:
    return 0.9


class EnrolmentGateTests(unittest.TestCase):
    """The gates take the VAD's speech as input, so synthetic signals stand in for it here."""

    def test_silence_is_too_short(self):
        silence = np.zeros(8 * RATE, np.float32)
        verdict = pipeline.assess_enrolment(silence, np.zeros(0, np.float32), consistent)
        self.assertEqual(verdict.error, "too_short")
        self.assertEqual(verdict.metrics()["speechMs"], 0)
        self.assertIsNone(verdict.metrics()["rmsDbfs"])

    def test_short_speech_is_too_short(self):
        speech = sine(5.9, 0.3)
        self.assertEqual(pipeline.assess_enrolment(speech, speech, consistent).error, "too_short")

    def test_very_quiet_sine_is_too_quiet(self):
        quiet = sine(8, 0.005)  # about -49 dBFS
        verdict = pipeline.assess_enrolment(quiet, quiet, consistent)
        self.assertEqual(verdict.error, "too_quiet")
        self.assertLess(verdict.rms_dbfs, pipeline.MIN_ENROL_RMS_DBFS)

    def test_clipped_square_is_clipped(self):
        square = np.sign(sine(8, 1.0)).astype(np.float32)
        verdict = pipeline.assess_enrolment(square, square, consistent)
        self.assertEqual(verdict.error, "clipped")
        self.assertGreater(verdict.clipped_ratio, pipeline.MAX_CLIPPED_RATIO)

    def test_inconsistent_voice_is_multiple_speakers(self):
        speech = sine(8, 0.3)
        verdict = pipeline.assess_enrolment(speech, speech, lambda _: 0.2)
        self.assertEqual(verdict.error, "multiple_speakers")
        self.assertEqual(verdict.metrics()["consistency"], 0.2)

    def test_threshold_can_be_overridden(self):
        speech = sine(8, 0.3)
        self.assertIsNone(pipeline.assess_enrolment(speech, speech, lambda _: 0.5, 0.4).error)

    def test_longer_than_a_minute_is_too_long(self):
        speech = sine(61, 0.3)
        self.assertEqual(pipeline.assess_enrolment(speech, speech, consistent).error, "too_long")

    def test_good_sample_passes_with_metrics(self):
        speech = sine(8, 0.3)
        verdict = pipeline.assess_enrolment(speech, speech, consistent)
        self.assertIsNone(verdict.error)
        metrics = verdict.metrics()
        self.assertEqual(metrics["speechMs"], 8000)
        self.assertAlmostEqual(metrics["rmsDbfs"], -13.5, delta=0.2)
        self.assertEqual(metrics["clippedRatio"], 0.0)
        self.assertEqual(metrics["consistency"], 0.9)

    def test_consistency_is_not_computed_when_an_earlier_gate_fails(self):
        def fail(_: np.ndarray) -> float:
            raise AssertionError("consistency must not run")

        quiet = sine(8, 0.005)
        self.assertEqual(pipeline.assess_enrolment(quiet, quiet, fail).error, "too_quiet")


class HelperTests(unittest.TestCase):
    def test_windows_are_non_overlapping_and_capped(self):
        speech = np.arange(20 * RATE, dtype=np.float32)
        parts = pipeline.windows(speech, 2 * RATE, 6)
        self.assertEqual(len(parts), 6)
        starts = [int(part[0]) for part in parts]
        self.assertEqual(starts, sorted(set(starts)))
        self.assertTrue(all(part.size == 2 * RATE for part in parts))
        self.assertEqual(pipeline.windows(np.zeros(RATE, np.float32), 2 * RATE, 6), [])

    def test_min_pairwise_cosine(self):
        a, b = np.array([1.0, 0.0]), np.array([0.0, 1.0])
        self.assertAlmostEqual(pipeline.min_pairwise_cosine([a, a, b]), 0.0)
        self.assertEqual(pipeline.min_pairwise_cosine([a]), 1.0)

    def test_hard_split_covers_everything(self):
        audio = np.ones(int(60 * RATE), np.float32)
        parts = pipeline.hard_split(audio)
        self.assertEqual(sum(part.size for _, part in parts), audio.size)
        self.assertTrue(all(part.size <= pipeline.MAX_CHUNK_S * RATE for _, part in parts))

    def test_level_lifts_a_quiet_speaker_toward_the_target(self):
        quiet = (0.01 * np.sin(np.arange(5 * RATE) * 0.05)).astype(np.float32)  # about -43 dBFS
        lifted = pipeline.level(quiet)
        self.assertEqual(lifted.size, quiet.size)
        self.assertAlmostEqual(pipeline.rms_dbfs(lifted[RATE:-RATE]), pipeline.LEVEL_TARGET_DBFS, delta=0.5)

    def test_level_caps_the_lift(self):
        faint = (0.0005 * np.sin(np.arange(5 * RATE) * 0.05)).astype(np.float32)  # about -69 dBFS
        self.assertTrue(np.allclose(pipeline.level(faint), faint))  # below the floor: silence, no lift
        weak = (0.003 * np.sin(np.arange(5 * RATE) * 0.05)).astype(np.float32)  # ~-53 dBFS
        gain_db = pipeline.rms_dbfs(pipeline.level(weak)[RATE:-RATE]) - pipeline.rms_dbfs(weak[RATE:-RATE])
        self.assertAlmostEqual(gain_db, pipeline.LEVEL_MAX_GAIN_DB, delta=0.5)

    def test_level_lowers_loud_speech_at_most_max_cut(self):
        loud = (0.9 * np.sin(np.arange(5 * RATE) * 0.05)).astype(np.float32)  # about -4 dBFS
        cut_db = pipeline.rms_dbfs(loud[RATE:-RATE]) - pipeline.rms_dbfs(pipeline.level(loud)[RATE:-RATE])
        self.assertAlmostEqual(cut_db, pipeline.LEVEL_MAX_CUT_DB, delta=0.5)

    def test_level_of_nothing_is_nothing(self):
        self.assertEqual(pipeline.level(np.zeros(0, np.float32)).size, 0)

    def test_hard_split_leaves_no_sliver(self):
        parts = pipeline.hard_split(np.ones(int(25.4 * RATE), np.float32))
        self.assertEqual([part.size for _, part in parts], [int(12.7 * RATE)] * 2)


if __name__ == "__main__":
    unittest.main()
