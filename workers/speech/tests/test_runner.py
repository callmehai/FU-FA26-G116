import itertools
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


def _reports(job: int, done: float) -> None:
    """Stands in for a transcription in the child that got `done` of the way."""
    report = runner.reporter(job)
    if report is not None:
        report(done)


class ChildCase(unittest.TestCase):
    """An EngineProcess whose child loads no model: the tests hand it functions of their own."""

    def setUp(self):
        process = runner.EngineProcess.__new__(runner.EngineProcess)
        process._context = multiprocessing.get_context("spawn")
        process._restart = threading.Lock()
        process._cancelled = process._context.Array("q", runner.CANCELLED_SLOTS, lock=False)
        process._cancel_lock = threading.Lock()
        process._next_slot = 0
        process._progress = process._context.Array("q", 2)
        process._runs = {}
        process._runs_lock = threading.Lock()
        process._jobs = itertools.count(1)
        process._pool = ProcessPoolExecutor(max_workers=1, mp_context=process._context, initializer=runner._share,
                                            initargs=(process._cancelled, process._progress))
        self.process = process

    def tearDown(self):
        self.process._pool.shutdown(wait=True, cancel_futures=True)


class CancelTests(ChildCase):
    """A run whose caller went away stops in the child, so the next request does not wait behind it (item 15)."""

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


class ProgressTests(ChildCase):
    """GET /v1/progress: the child writes how far its job got, the parent answers for the caller's run id."""

    def test_a_run_the_process_does_not_hold_is_unknown(self):
        self.assertEqual(self.process.progress("nobody"), {"state": "unknown", "percent": 0})

    def test_a_run_waits_queued_until_the_child_takes_it_then_reports_how_far_it_got(self):
        self.process._runs["run-a"] = 5
        self.process._call(_reports, 4, 0.9)  # another job is running in the child
        self.assertEqual(self.process.progress("run-a"), {"state": "queued", "percent": 0})
        self.process._call(_reports, 5, 0.427)
        self.assertEqual(self.process.progress("run-a"), {"state": "running", "percent": 42})
        self.process._call(_reports, 5, 1.0)
        self.assertEqual(self.process.progress("run-a"), {"state": "running", "percent": 100})

    def test_a_run_is_forgotten_once_answered(self):
        process = self.process
        seen = []
        original = process._call

        def call(function, *args, **kwargs):
            seen.append(process.progress("run-b"))
            return "result"

        process._call = call
        try:
            self.assertEqual(process.transcribe_file("x.wav", run="run-b"), "result")
        finally:
            process._call = original
        self.assertEqual(seen[0]["state"], "queued")
        self.assertEqual(process.progress("run-b"), {"state": "unknown", "percent": 0})


if __name__ == "__main__":
    unittest.main()
