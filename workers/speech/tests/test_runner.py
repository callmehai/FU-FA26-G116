import multiprocessing
import sys
import threading
import time
import unittest
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech import pipeline, runner  # noqa: E402


def _long_run(job: int) -> str:
    """Stands in for a transcription in the child: asks between steps, as pipeline.Engine does."""
    stop = runner.stopped(job)
    deadline = time.monotonic() + 20
    while time.monotonic() < deadline:
        if stop is not None and stop():
            raise pipeline.Cancelled()
        time.sleep(0.01)
    return "finished"


def _quick() -> str:
    return "done"


class CancelTests(unittest.TestCase):
    """A run whose caller went away stops in the child, so the next request does not wait behind it (item 15)."""

    def setUp(self):
        process = runner.EngineProcess.__new__(runner.EngineProcess)
        process._context = multiprocessing.get_context("spawn")
        process._restart = threading.Lock()
        process._cancelled = process._context.Array("q", runner.CANCELLED_SLOTS, lock=False)
        process._cancel_lock = threading.Lock()
        process._next_slot = 0
        process._pool = ProcessPoolExecutor(max_workers=1, mp_context=process._context, initializer=runner._share,
                                            initargs=(process._cancelled,))
        self.process = process

    def tearDown(self):
        self.process._pool.shutdown(wait=True, cancel_futures=True)

    def test_the_child_stops_a_cancelled_run_and_takes_the_next_one_at_once(self):
        gone = threading.Event()
        threading.Timer(1.0, gone.set).start()
        started = time.monotonic()
        with self.assertRaises(pipeline.Cancelled):
            self.process._call(_long_run, 7, cancelled=gone.is_set, job=7)
        self.assertEqual(self.process._call(_quick), "done")
        self.assertLess(time.monotonic() - started, 10)

    def test_a_run_nobody_cancelled_finishes(self):
        self.assertEqual(self.process._call(_quick, cancelled=lambda: False, job=3), "done")


if __name__ == "__main__":
    unittest.main()
