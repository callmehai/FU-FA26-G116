"""Run the Engine in child processes, so the HTTP process never stalls behind inference.

sherpa-onnx holds the GIL inside some native calls (diarization of an hour of audio takes
minutes), which would freeze every HTTP thread — /health included — in the same process. Each child
process owns a copy of the models and runs one job at a time; the parent only moves file paths and
JSON-able results across, and restarts a child if it dies (out of memory, for example).

There are two children, two lanes. The heavy lane takes whole recordings (the room mic, an upload), which run for
minutes; the light lane takes what someone is waiting for right now: a sentence from a personal mic (profile "clip")
and a Voice ID enrolment sample. With one child, every sentence of a meeting waited behind a room recording being
transcribed again — for half an hour on the test server on 23/11/2026, while the backend gives a sentence 30 s. The
light child costs one more copy of the models (README, "Process con"); SPEECH_CLIP_PROCESS=0 puts both lanes back in
one child.

A caller that goes away (the backend dropped a run made stale by a newer room-mic take) is told to the
children through a small shared list of cancelled job numbers, which the engine reads between steps:
the run stops within a second or so, and the next request in the queue starts at once.

How far the running job has got goes the other way, through a shared pair (job number, thousandths done) per child
that the child writes as the engine reports and the parent reads when the backend asks (GET /v1/progress). A job the
parent was given but no child has started yet is queued behind another.
"""

from __future__ import annotations

import itertools
import logging
import multiprocessing
import os
import threading
import time
from concurrent.futures import Future, ProcessPoolExecutor, TimeoutError as FutureTimeout
from concurrent.futures.process import BrokenProcessPool
from typing import Callable

from . import manifest, pipeline

log = logging.getLogger(__name__)

_engine: pipeline.Engine | None = None
# Job numbers whose caller went away, newest overwriting the oldest; shared with the child processes.
CANCELLED_SLOTS = 32
# How often the parent asks whether the caller of a running job is still there.
POLL_S = 0.5
# The heavy child runs at this lower CPU priority (nice), so on a two-core server a sentence or an enrolment sample
# that arrives during a whole recording takes the cores first, as do the backend, PostgreSQL and LiveKit beside it.
HEAVY_NICE = 10
_cancelled = None
_progress = None


def clip_process() -> bool:
    """Whether sentences and enrolment samples get a child of their own (SPEECH_CLIP_PROCESS, on unless 0)."""
    return os.environ.get("SPEECH_CLIP_PROCESS", "1").lower() not in ("0", "false", "off")


def _share(cancelled, progress=None, name: str | None = None, nice: int = 0) -> None:
    """In the child: keep the list of cancelled job numbers the parent writes to and the pair it reads progress from,
    and lower its own priority by `nice`."""
    global _cancelled, _progress
    _cancelled = cancelled
    _progress = progress
    if nice:
        os.nice(nice)


def stopped(job: int) -> Callable[[], bool] | None:
    """In the child: whether `job`'s caller went away, read from the shared list at each call."""
    shared = _cancelled
    if shared is None or not job:
        return None
    return lambda: job in shared[:]


def reporter(job: int) -> Callable[[float], None] | None:
    """In the child: writes how far `job` has got (0–1) into the pair the parent reads, starting at nothing."""
    shared = _progress
    if shared is None or not job:
        return None

    def report(done: float) -> None:
        with shared.get_lock():
            shared[0], shared[1] = job, round(done * 1000)

    report(0.0)
    return report


def _load(cancelled=None, progress=None, name: str = "engine", nice: int = 0) -> None:
    global _engine
    _share(cancelled, progress, name, nice)
    logging.basicConfig(
        level=os.environ.get("SPEECH_LOG_LEVEL", "INFO").upper(),
        format=f"%(asctime)s %(levelname)s %(name)s[{name}]: %(message)s",
    )
    _engine = pipeline.Engine()


def _ready() -> dict[str, str]:
    assert _engine is not None
    return _engine.model_ids


def _transcribe(path: str, diarize: bool, speakers: int | None, profile: str, context: str | None,
                job: int = 0) -> dict:
    assert _engine is not None
    return _engine.transcribe_file(path, diarize, speakers, profile, context, stopped(job), reporter(job))


def _embed(path: str) -> tuple[bool, dict]:
    assert _engine is not None
    return _engine.embed_file(path)


class Lane:
    """One child process holding the models and running one job at a time.

    It is started afresh when it dies, and after a job that leaves memory behind once nothing else waits for it
    (recycle).
    """

    def __init__(self, name: str, context, cancelled, cancel: Callable[[int], None],
                 initializer: Callable = _load, warm: Callable = _ready, nice: int = 0) -> None:
        self.name = name
        self._nice = nice
        self._context = context
        self._cancelled = cancelled
        self._cancel = cancel
        self._initializer = initializer
        self._warm = warm
        # (job number, thousandths done) of the job this child runs now, written by the child (reporter).
        self.progress = context.Array("q", 2)
        self._restart = threading.Lock()
        # Jobs given to the pool and not answered yet; a recycle waits until there are none, and new jobs wait for it.
        self._gate = threading.Condition()
        self._inflight = 0
        self._recycling = False
        self._dirty = False
        self._pool = self._start()

    def _start(self) -> ProcessPoolExecutor:
        return ProcessPoolExecutor(max_workers=1, mp_context=self._context, initializer=self._initializer,
                                   initargs=(self._cancelled, self.progress, self.name, self._nice))

    def warming(self) -> Future:
        """Loads the models in the child now rather than on the first request: a broken install fails at boot."""
        return self._pool.submit(self._warm)

    def call(self, function, *args, cancelled: Callable[[], bool] | None = None, job: int = 0,
             recycle: bool = False):
        """`recycle`: a job that leaves memory behind (a whole recording, an enrolment sample), after which the child is
        started afresh once nothing else waits for it."""
        with self._gate:
            while self._recycling:
                self._gate.wait()
            self._inflight += 1
            pool = self._pool
        try:
            return self._run(pool, function, *args, cancelled=cancelled, job=job)
        finally:
            with self._gate:
                self._inflight -= 1
                self._dirty = self._dirty or recycle
                start = self._dirty and self._inflight == 0 and not self._recycling
                if start:
                    self._recycling = True
            if start:
                threading.Thread(target=self._recycle, name=f"{self.name}-recycle", daemon=True).start()

    def _recycle(self) -> None:
        """Starts the child afresh after a whole recording or an enrolment sample, while nothing waits for it.

        The child keeps the memory a long run needed (onnxruntime arenas, the decoded audio, the diarization
        windows) for as long as it lives: on the test server it held 2.4 GB of RAM and 1.8 GB of swap while idle
        after a 35-minute recording (23/11/2026), and every later run crawled through swap. A fresh child holds
        only the models; loading them takes about six seconds, which requests arriving meanwhile wait out. An enrolment
        sample of 25 s leaves the light child some 400 MB larger (the embedding model over the whole sample).
        """
        started = time.monotonic()
        try:
            with self._restart:
                self._pool.shutdown(wait=True)
                self._pool = self._start()
                self.warming().result()
            log.info("%s process restarted to give its memory back, in %.1fs", self.name, time.monotonic() - started)
        except Exception:  # noqa: BLE001 - a failed restart must not take the server down; the next job retries
            log.exception("could not restart the %s process to give its memory back", self.name)
        finally:
            with self._gate:
                self._recycling = False
                self._dirty = False
                self._gate.notify_all()

    def _run(self, pool: ProcessPoolExecutor, function, *args, cancelled: Callable[[], bool] | None = None,
             job: int = 0):
        try:
            future = pool.submit(function, *args)
            while True:
                try:
                    return future.result(timeout=POLL_S if cancelled else None)
                except FutureTimeout:
                    if cancelled is not None and cancelled():
                        # Queued or running, the child skips or stops it at its next step; nobody waits for it.
                        future.cancel()
                        self._cancel(job)
                        raise pipeline.Cancelled() from None
        except BrokenProcessPool:
            with self._restart:
                if self._pool is pool:
                    log.error("%s process died; starting a new one", self.name)
                    self._pool = self._start()
            raise RuntimeError("engine process died during the request") from None

    def close(self) -> None:
        self._pool.shutdown(wait=False, cancel_futures=True)


class EngineProcess:
    """Same interface as pipeline.Engine, executed in child processes: one for whole recordings, one for the rest."""

    def __init__(self) -> None:
        # Verified here as well as in the children, so /health can report the ids without asking them.
        self.model_ids = pipeline.model_ids(manifest.verify(manifest.models_dir()))
        self._open(_load, _ready, clip_process())

    def _open(self, initializer: Callable, warm: Callable, separate: bool) -> None:
        """Starts the children: `initializer` loads the models in each, `warm` makes it wait for them."""
        context = multiprocessing.get_context("spawn")
        self._cancelled = context.Array("q", CANCELLED_SLOTS, lock=False)
        self._cancel_lock = threading.Lock()
        self._next_slot = 0
        self._jobs = itertools.count(1)
        # The caller's run id of each transcription given to a child and not answered yet.
        self._runs: dict[str, int] = {}
        self._runs_lock = threading.Lock()
        # Alone, the one child keeps its priority: a sentence waiting behind a recording would gain nothing by it.
        self._heavy = Lane("engine", context, self._cancelled, self._cancel, initializer, warm,
                           nice=HEAVY_NICE if separate else 0)
        self._light = (Lane("engine-clip", context, self._cancelled, self._cancel, initializer, warm)
                       if separate else self._heavy)
        # Both children load their models at the same time.
        for warming in [lane.warming() for lane in self._lanes]:
            warming.result()

    @property
    def _lanes(self) -> tuple[Lane, ...]:
        return (self._heavy,) if self._light is self._heavy else (self._heavy, self._light)

    def _cancel(self, job: int) -> None:
        with self._cancel_lock:
            self._cancelled[self._next_slot] = job
            self._next_slot = (self._next_slot + 1) % CANCELLED_SLOTS

    def transcribe_file(self, path: str, diarize: bool = True, speakers: int | None = None,
                        profile: str = "room", context: str | None = None,
                        cancelled: Callable[[], bool] | None = None, run: str | None = None) -> dict:
        """`run`: the caller's id for this run, to ask progress() about while it waits for the answer.

        A sentence from a personal mic goes to the light lane and never starts a recycle; anything else is a whole
        recording, for the heavy lane.
        """
        clip = profile == "clip"
        job = next(self._jobs)
        if run:
            with self._runs_lock:
                self._runs[run] = job
        try:
            lane = self._light if clip else self._heavy
            return lane.call(_transcribe, os.fspath(path), diarize, speakers, profile, context, job,
                             cancelled=cancelled, job=job, recycle=not clip)
        finally:
            if run:
                with self._runs_lock:
                    if self._runs.get(run) == job:
                        del self._runs[run]

    def progress(self, run: str) -> dict:
        """How far the transcription the caller named `run` has got: `queued` while another job runs ahead of it,
        `running` with the per cent done, `unknown` for a run this process is not working on (answered, never sent,
        or sent before the worker restarted)."""
        with self._runs_lock:
            job = self._runs.get(run)
        if job is None:
            return {"state": "unknown", "percent": 0}
        for lane in self._lanes:
            with lane.progress.get_lock():
                current, permille = lane.progress[0], lane.progress[1]
            if current == job:
                return {"state": "running", "percent": max(0, min(100, permille // 10))}
        return {"state": "queued", "percent": 0}

    def embed_file(self, path: str) -> tuple[bool, dict]:
        """On the light lane, which restarts afterwards to give back what the sample took. With one child, as before,
        the child keeps it."""
        return self._light.call(_embed, os.fspath(path), recycle=self._light is not self._heavy)

    def close(self) -> None:
        for lane in self._lanes:
            lane.close()
