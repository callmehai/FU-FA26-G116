import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech import pipeline  # noqa: E402


class AudibleTests(unittest.TestCase):
    """The room profile drops a segment that is both near-silent and far below the rest."""

    def test_test_server_meeting(self):
        # The segment levels of the meeting where a room mic heard "Đây." twice (03/11).
        levels = [-23.3, -23.1, -27.3, -27.3, -22.8, -21.4, -20.1, -20.4, -21.2, -18.8, -20.3,
                  -32.3, -50.2, -22.4, -24.5, -25.3, -27.0, -54.2, -23.2, -22.4, -23.2]
        keep = pipeline.audible(levels)
        self.assertEqual([level for level, kept in zip(levels, keep) if not kept], [-50.2, -54.2])

    def test_quiet_participant_stays(self):
        # 20 dB below the chair, as the quiet speakers of the calibration meeting were.
        self.assertEqual(pipeline.audible([-24.0, -25.0, -23.0, -44.0, -46.0]), [True] * 5)

    def test_quiet_recording_keeps_everything(self):
        # A recorder far from everyone: all of it is quiet, none of it far below the rest.
        self.assertEqual(pipeline.audible([-50.0, -52.0, -48.0, -55.0]), [True] * 4)

    def test_unknown_levels_stay(self):
        self.assertEqual(pipeline.audible([None, -20.0, -21.0, -60.0]), [True, True, True, False])
        self.assertEqual(pipeline.audible([None, None]), [True, True])
        self.assertEqual(pipeline.audible([]), [])


if __name__ == "__main__":
    unittest.main()
