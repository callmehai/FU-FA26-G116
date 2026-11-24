import os
import sys
import threading
import time
import unittest
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


def _pid() -> int:
    return os.getpid()


def _niceness() -> int:
    return os.nice(0)


def _die() -> None:
    os._exit(1)


def _reports(job: int, done: float) -> None:
    """Stands in for a transcription in the child that got `done` of the way."""
    report = runner.reporter(job)
    if report is not None:
        report(done)


class _FakeEngine:
    """pipeline.Engine without models: a room recording runs until cancelled (or 20 s), a sentence answers at once.

    A room recording whose `context` is "short" ends at once too.
    """

    model_ids: dict = {}

    def transcribe_file(self, path, diarize, speakers, profile, context, cancelled, progress_to):
        if profile != "clip" and context != "short":
            deadline = time.monotonic() + 20
            while time.monotonic() < deadline:
                if cancelled is not None and cancelled():
                    raise pipeline.Cancelled()
                if progress_to is not None:
                    progress_to(0.5)
                time.sleep(0.01)
        return {"profile": profile, "pid": os.getpid()}

    def embed_file(self, path):
        return True, {"pid": os.getpid()}


def _fake_load(cancelled=None, progress=None, name="engine", nice=0) -> None:
    runner._share(cancelled, progress, name, nice)
    runner._engine = _FakeEngine()


class ChildCase(unittest.TestCase):
    """An EngineProcess whose children load no model: the tests hand them functions of their own."""

    separate = True

    def setUp(self):
        process = runner.EngineProcess.__new__(runner.EngineProcess)
        process._open(_fake_load, _quick, self.separate)
        self.process = process
        self.lane = process._heavy

    def tearDown(self):
        for lane in self.process._lanes:
            # A room run cancelled at the end of a test starts a recycle, which shuts the same pool down.
            self.settle(lane)
            lane._pool.shutdown(wait=True, cancel_futures=True)

    def settle(self, lane):
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            with lane._gate:
                if not lane._recycling:
                    return
            time.sleep(0.05)
        self.fail("the recycle never finished")

    def wait_running(self, run):
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if self.process.progress(run)["state"] == "running":
                return
            time.sleep(0.05)
        self.fail(f"{run} never started")


class CancelTests(ChildCase):
    """A run whose caller went away stops in the child, so the next request does not wait behind it (item 15)."""

    def test_the_child_stops_a_cancelled_run_and_takes_the_next_one_at_once(self):
        gone = threading.Event()
        threading.Timer(1.0, gone.set).start()
        started = time.monotonic()
        with self.assertRaises(pipeline.Cancelled):
            self.lane.call(_long_run, 7, cancelled=gone.is_set, job=7)
        self.assertEqual(self.lane.call(_quick), "done")
        self.assertLess(time.monotonic() - started, 10)

    def test_a_run_nobody_cancelled_finishes(self):
        self.assertEqual(self.lane.call(_quick, cancelled=lambda: False, job=3), "done")


class ProgressTests(ChildCase):
    """GET /v1/progress: the child writes how far its job got, the parent answers for the caller's run id."""

    def test_a_run_the_process_does_not_hold_is_unknown(self):
        self.assertEqual(self.process.progress("nobody"), {"state": "unknown", "percent": 0})

    def test_a_run_waits_queued_until_the_child_takes_it_then_reports_how_far_it_got(self):
        self.process._runs["run-a"] = 5
        self.lane.call(_reports, 4, 0.9)  # another job is running in the child
        self.assertEqual(self.process.progress("run-a"), {"state": "queued", "percent": 0})
        self.lane.call(_reports, 5, 0.427)
        self.assertEqual(self.process.progress("run-a"), {"state": "running", "percent": 42})
        self.lane.call(_reports, 5, 1.0)
        self.assertEqual(self.process.progress("run-a"), {"state": "running", "percent": 100})

    def test_a_run_is_forgotten_once_answered(self):
        process = self.process
        seen = []

        def call(function, *args, **kwargs):
            seen.append(process.progress("run-b"))
            return "result"

        self.lane.call = call
        self.assertEqual(process.transcribe_file("x.wav", run="run-b"), "result")
        self.assertEqual(seen[0]["state"], "queued")
        self.assertEqual(process.progress("run-b"), {"state": "unknown", "percent": 0})


class RecycleTests(ChildCase):
    """After a whole recording the child is started afresh, to give back the memory the run kept (23/11/2026)."""

    def test_a_heavy_run_restarts_the_child_and_the_next_job_runs_in_the_new_one(self):
        before = self.lane.call(_pid)
        self.assertEqual(self.lane.call(_quick, recycle=True), "done")
        self.settle(self.lane)
        after = self.lane.call(_pid)
        self.assertNotEqual(before, after)
        self.assertFalse(self.lane._dirty)

    def test_a_light_job_keeps_the_child(self):
        before = self.lane.call(_pid)
        self.lane.call(_quick)
        self.settle(self.lane)
        self.assertEqual(self.lane.call(_pid), before)

    def test_the_restart_waits_for_a_job_still_running(self):
        gone = threading.Event()
        threading.Timer(1.5, gone.set).start()
        before = self.lane.call(_pid)
        # A long job holds the child while a whole recording finishes beside it: no restart until both are done.
        worker = threading.Thread(target=lambda: self.assertRaises(
            pipeline.Cancelled, self.lane.call, _long_run, 9, cancelled=gone.is_set, job=9))
        worker.start()
        time.sleep(0.3)
        with self.lane._gate:
            self.lane._dirty = True
        worker.join(30)
        self.settle(self.lane)
        self.assertNotEqual(self.lane.call(_pid), before)

    def test_a_dead_child_is_replaced_and_the_next_job_runs(self):
        with self.assertRaises(RuntimeError):
            self.lane.call(_die)
        self.assertEqual(self.lane.call(_quick), "done")


class LaneTests(ChildCase):
    """A sentence from a personal mic or an enrolment sample never waits behind a room recording (23/11/2026)."""

    def test_a_clip_is_answered_while_a_room_recording_runs(self):
        gone = threading.Event()
        outcome = []

        def room():
            try:
                outcome.append(self.process.transcribe_file("room.wav", profile="room", cancelled=gone.is_set,
                                                            run="room-1"))
            except pipeline.Cancelled:
                outcome.append("cancelled")

        worker = threading.Thread(target=room)
        worker.start()
        try:
            self.wait_running("room-1")
            started = time.monotonic()
            clip = self.process.transcribe_file("clip.wav", diarize=False, profile="clip", run="clip-1")
            accepted, _ = self.process.embed_file("sample.wav")
            elapsed = time.monotonic() - started
            self.assertEqual(clip["profile"], "clip")
            self.assertTrue(accepted)
            self.assertLess(elapsed, 5)
            # The room run is still going, its progress untouched by the sentence beside it.
            self.assertTrue(worker.is_alive())
            self.assertEqual(outcome, [])
            self.assertEqual(self.process.progress("room-1"), {"state": "running", "percent": 50})
        finally:
            gone.set()
            worker.join(30)
        self.assertEqual(outcome, ["cancelled"])

    def test_clips_run_in_a_child_of_their_own_that_a_room_recording_never_restarts(self):
        clip_pid = self.process.transcribe_file("clip.wav", profile="clip")["pid"]
        room_pid = self.process.transcribe_file("room.wav", profile="room", context="short")["pid"]
        self.assertNotEqual(clip_pid, room_pid)
        self.settle(self.process._heavy)
        self.assertEqual(self.process.transcribe_file("clip.wav", profile="clip")["pid"], clip_pid)
        self.assertNotEqual(self.process.transcribe_file("room.wav", profile="room", context="short")["pid"],
                            room_pid)

    def test_the_room_child_gives_way_on_the_cpu(self):
        light = self.process._light.call(_niceness)
        self.assertEqual(self.process._heavy.call(_niceness), min(19, light + runner.HEAVY_NICE))

    def test_clips_never_restart_their_child(self):
        light = self.process._light
        before = light.call(_pid)
        for _ in range(3):
            self.process.transcribe_file("clip.wav", profile="clip")
        self.settle(light)
        self.assertFalse(light._dirty)
        self.assertEqual(light.call(_pid), before)

    def test_an_enrolment_sample_restarts_the_clip_child_only(self):
        light_before = self.process._light.call(_pid)
        heavy_before = self.process._heavy.call(_pid)
        self.assertEqual(self.process.embed_file("sample.wav")[1]["pid"], light_before)
        self.settle(self.process._light)
        self.assertNotEqual(self.process._light.call(_pid), light_before)
        self.assertEqual(self.process._heavy.call(_pid), heavy_before)

    def test_a_dead_clip_child_leaves_the_room_recording_running(self):
        gone = threading.Event()
        outcome = []
        worker = threading.Thread(target=lambda: outcome.append(self.assertRaises(
            pipeline.Cancelled, self.process.transcribe_file, "room.wav", cancelled=gone.is_set, run="room-2")))
        worker.start()
        try:
            self.wait_running("room-2")
            with self.assertRaises(RuntimeError):
                self.process._light.call(_die)
            self.assertEqual(self.process.transcribe_file("clip.wav", profile="clip")["profile"], "clip")
            self.assertEqual(self.process.progress("room-2")["state"], "running")
        finally:
            gone.set()
            worker.join(30)


class OneChildTests(ChildCase):
    """SPEECH_CLIP_PROCESS=0: one child takes everything, as before; only a whole recording restarts it."""

    separate = False

    def test_both_lanes_are_one_child_and_a_clip_keeps_it(self):
        self.assertIs(self.process._light, self.process._heavy)
        self.assertEqual(self.lane.call(_niceness), os.nice(0))
        before = self.process.transcribe_file("clip.wav", profile="clip")["pid"]
        self.settle(self.lane)
        self.assertEqual(self.process.embed_file("sample.wav")[1]["pid"], before)
        self.settle(self.lane)
        self.assertEqual(self.process.transcribe_file("room.wav", context="short")["pid"], before)
        self.settle(self.lane)
        self.assertNotEqual(self.process.transcribe_file("clip.wav", profile="clip")["pid"], before)

    def test_the_setting(self):
        for value, expected in (("0", False), ("off", False), ("1", True), ("", True)):
            os.environ["SPEECH_CLIP_PROCESS"] = value
            try:
                self.assertEqual(runner.clip_process(), expected, value)
            finally:
                del os.environ["SPEECH_CLIP_PROCESS"]
        self.assertTrue(runner.clip_process())


if __name__ == "__main__":
    unittest.main()
