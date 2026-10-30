"""Run the Engine in one child process, so the HTTP process never stalls behind inference.

sherpa-onnx holds the GIL inside some native calls (diarization of an hour of audio takes
minutes), which would freeze every HTTP thread — /health included — in the same process. The
child process owns the models and runs one job at a time; the parent only moves file paths and
JSON-able results across, and restarts the child if it dies (out of memory, for example).
"""

from __future__ import annotations

import logging
import multiprocessing
import os
import threading
from concurrent.futures import ProcessPoolExecutor
from concurrent.futures.process import BrokenProcessPool

from . import manifest, pipeline

log = logging.getLogger(__name__)

_engine: pipeline.Engine | None = None


def _load() -> None:
    global _engine
    logging.basicConfig(
        level=os.environ.get("SPEECH_LOG_LEVEL", "INFO").upper(),
        format="%(asctime)s %(levelname)s %(name)s[engine]: %(message)s",
    )
    _engine = pipeline.Engine()


def _ready() -> dict[str, str]:
    assert _engine is not None
    return _engine.model_ids


def _transcribe(path: str, diarize: bool, speakers: int | None, profile: str) -> dict:
    assert _engine is not None
    return _engine.transcribe_file(path, diarize, speakers, profile)


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
        self._pool = self._start()
        # Load the models now rather than on the first request: a broken install fails at boot.
        self._pool.submit(_ready).result()

    def _start(self) -> ProcessPoolExecutor:
        return ProcessPoolExecutor(max_workers=1, mp_context=self._context, initializer=_load)

    def _call(self, function, *args):
        pool = self._pool
        try:
            return pool.submit(function, *args).result()
        except BrokenProcessPool:
            with self._restart:
                if self._pool is pool:
                    log.error("engine process died; starting a new one")
                    self._pool = self._start()
            raise RuntimeError("engine process died during the request") from None

    def transcribe_file(self, path: str, diarize: bool = True, speakers: int | None = None,
                        profile: str = "room") -> dict:
        return self._call(_transcribe, os.fspath(path), diarize, speakers, profile)

    def embed_file(self, path: str) -> tuple[bool, dict]:
        return self._call(_embed, os.fspath(path))

    def close(self) -> None:
        self._pool.shutdown(wait=False, cancel_futures=True)
