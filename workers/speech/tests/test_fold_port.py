import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "workers" / "speech" / "eval"))

import fold  # noqa: E402

CASES = ROOT / "tests" / "TrustCabinet.Web.Tests" / "Recordings" / "speech-fold-cases.json"


class FoldPortTests(unittest.TestCase):
    """eval/fold.py must mark exactly what the backend's SpeechClipFold marks."""

    def test_shared_cases(self):
        for case in json.loads(CASES.read_text(encoding="utf-8"))["cases"]:
            with self.subTest(case["name"]):
                clips = [fold.Clip(c["id"], c["speaker"], c["offsetMs"], c["durationMs"], c["text"],
                                   [fold.Segment(*s) for s in c["segments"]], c["excluded"], c["edited"])
                         for c in case["clips"]]
                got = {k: {"leakOf": m.leak_of, "spans": [list(s) for s in m.spans]} for k, m in fold.fold(clips).items()}
                self.assertEqual(got, case["expect"])


if __name__ == "__main__":
    unittest.main()
