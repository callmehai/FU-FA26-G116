"""Run the Engine in one child process, so the HTTP process never stalls behind inference.

sherpa-onnx holds the GIL inside some native calls (diarization of an hour of audio takes
minutes), which would freeze every HTTP thread — /health included — in the same process. The
child process owns the models and runs one job at a time; the parent only moves file paths and
JSON-able results across, and restarts the child if it dies (out of memory, for example).

A caller that goes away (the backend dropped a run made stale by a newer room-mic take) is told to the
child through a small shared list of cancelled job numbers, which the engine reads between steps:
the run stops within a second or so, and the next request in the queue starts at once.

How far the running job has got goes the other way, through a second shared pair (job number, thousandths done) that
the child writes as the engine reports and the parent reads when the backend asks (GET /v1/progress). A job the
parent was given but the child has not started yet is queued behind another.
"""

from __future__ import annotations

import itertools
import logging
import multiprocessing
import os
import threading
import time
from concurrent.futures import ProcessPoolExecutor, TimeoutError as FutureTimeout
from concurrent.futures.process import BrokenProcessPool
from typing import Callable

from . import manifest, pipeline

log = logging.getLogger(__name__)

_engine: pipeline.Engine | None = None
# Job numbers whose caller went away, newest overwriting the oldest; shared with the child process.
CANCELLED_SLOTS = 32
# How often the parent asks whether the caller of a running job is still there.
POLL_S = 0.5
_cancelled = None
_progress = None


def _share(cancelled, progress=None) -> None:
    """In the child: keep the list of cancelled job numbers the parent writes to, and the pair it reads progress from."""
    global _cancelled, _progress
    _cancelled = cancelled
    _progress = progress


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


def _load(cancelled=None, progress=None) -> None:
    global _engine
    _share(cancelled, progress)
    logging.basicConfig(
        level=os.environ.get("SPEECH_LOG_LEVEL", "INFO").upper(),
        format="%(asctime)s %(levelname)s %(name)s[engine]: %(message)s",
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


class EngineProcess:
    """Same interface as pipeline.Engine, executed in a child process."""

    def __init__(self) -> None:
        # Verified here as well as in the child, so /health can report the ids without asking it.
        self.model_ids = pipeline.model_ids(manifest.verify(manifest.models_dir()))
        self._context = multiprocessing.get_context("spawn")
        self._restart = threading.Lock()
        self._cancelled = self._context.Array("q", CANCELLED_SLOTS, lock=False)
        self._cancel_lock = threading.Lock()
        self._next_slot = 0
        self._jobs = itertools.count(1)
        # (job number, thousandths done) of the job the child runs now, written by the child (reporter).
        self._progress = self._context.Array("q", 2)
        # The caller's run id of each transcription given to the child and not answered yet.
        self._runs: dict[str, int] = {}
        self._runs_lock = threading.Lock()
        self._init_recycling()
        self._pool = self._start()
        # Load the models now rather than on the first request: a broken install fails at boot.
        self._warm()

    def _init_recycling(self) -> None:
        # Jobs given to the pool and not answered yet; a recycle waits until there are none, and new jobs wait for it.
        self._gate = threading.Condition()
        self._inflight = 0
        self._recycling = False
        self._dirty = False

    def _warm(self) -> None:
        self._pool.submit(_ready).result()

    def _start(self) -> ProcessPoolExecutor:
        return ProcessPoolExecutor(max_workers=1, mp_context=self._context, initializer=_load,
                                   initargs=(self._cancelled, self._progress))

    def _cancel(self, job: int) -> None:
        with self._cancel_lock:
            self._cancelled[self._next_slot] = job
            self._next_slot = (self._next_slot + 1) % CANCELLED_SLOTS

    def _call(self, function, *args, cancelled: Callable[[], bool] | None = None, job: int = 0,
              heavy: bool = False):
        """`heavy`: a whole recording, after which the child is started afresh once nothing else waits (recycle)."""
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
                self._dirty = self._dirty or heavy
                start = self._dirty and self._inflight == 0 and not self._recycling
                if start:
                    self._recycling = True
            if start:
                threading.Thread(target=self._recycle, name="engine-recycle", daemon=True).start()

    def _recycle(self) -> None:
        """Starts the child afresh after a whole recording, while nothing waits for it.

        The child keeps the memory a long run needed (onnxruntime arenas, the decoded audio, the diarization
        windows) for as long as it lives: on the test server it held 2.4 GB of RAM and 1.8 GB of swap while idle
        after a 35-minute recording (23/11/2026), and every later run crawled through swap. A fresh child holds
        only the models; loading them takes about six seconds, which requests arriving meanwhile wait out.
        """
        started = time.monotonic()
        try:
            with self._restart:
                self._pool.shutdown(wait=True)
                self._pool = self._start()
                self._warm()
            log.info("engine process restarted after a long run, in %.1fs, to give its memory back",
                     time.monotonic() - started)
        except Exception:  # noqa: BLE001 - a failed restart must not take the server down; the next job retries
            log.exception("could not restart the engine process after a long run")
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
                    log.error("engine process died; starting a new one")
                    self._pool = self._start()
            raise RuntimeError("engine process died during the request") from None

    def transcribe_file(self, path: str, diarize: bool = True, speakers: int | None = None,
                        profile: str = "room", context: str | None = None,
                        cancelled: Callable[[], bool] | None = None, run: str | None = None) -> dict:
        """`run`: the caller's id for this run, to ask progress() about while it waits for the answer."""
        job = next(self._jobs)
        if run:
            with self._runs_lock:
                self._runs[run] = job
        try:
            return self._call(_transcribe, os.fspath(path), diarize, speakers, profile, context, job,
                              cancelled=cancelled, job=job, heavy=profile != "clip")
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
        with self._progress.get_lock():
            current, permille = self._progress[0], self._progress[1]
        if current != job:
            return {"state": "queued", "percent": 0}
        return {"state": "running", "percent": max(0, min(100, permille // 10))}

    def embed_file(self, path: str) -> tuple[bool, dict]:
        return self._call(_embed, os.fspath(path))

    def close(self) -> None:
        self._pool.shutdown(wait=False, cancel_futures=True)
