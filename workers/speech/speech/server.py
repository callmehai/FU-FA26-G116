"""HTTP front of the speech worker: three routes, JSON out, raw audio in.

Only the TrustCabinet backend calls this, over the internal Docker network. Uploads are streamed to
a temporary file (deleted when the request ends) instead of being held in memory, and nothing is
written anywhere else: the worker keeps no audio, no text and no voice vectors.
"""

from __future__ import annotations

import hmac
import json
import logging
import os
import tempfile
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Protocol
from urllib.parse import parse_qs, urlsplit

from .pipeline import UnreadableAudio

log = logging.getLogger(__name__)

MAX_BODY_BYTES = 512 * 1024 * 1024
MAX_SPEAKERS = 20
BLOCK = 1 << 20


class Engine(Protocol):
    model_ids: dict[str, str]

    def transcribe_file(self, path: str, diarize: bool = True, speakers: int | None = None) -> dict: ...

    def embed_file(self, path: str) -> tuple[bool, dict]: ...


class Rejected(Exception):
    """A request the server answers with an error body instead of running the engine."""

    def __init__(self, status: int, error: str, close: bool = False) -> None:
        super().__init__(error)
        self.status = status
        self.error = error
        self.close = close


def parse_transcribe_query(query: str) -> tuple[bool, int | None]:
    """`diarize=true|false` (default true) and optional `speakers=N` (1–20)."""
    try:
        params = parse_qs(query, keep_blank_values=True, strict_parsing=bool(query))
    except ValueError as error:
        raise Rejected(400, "invalid_parameter") from error
    if any(len(values) != 1 for values in params.values()):
        raise Rejected(400, "invalid_parameter")
    diarize_value = params.get("diarize", ["true"])[0].lower()
    if diarize_value not in ("true", "false", "1", "0"):
        raise Rejected(400, "invalid_parameter")
    speakers = None
    if "speakers" in params:
        raw = params["speakers"][0]
        if not raw.isdigit() or not 1 <= int(raw) <= MAX_SPEAKERS:
            raise Rejected(400, "invalid_parameter")
        speakers = int(raw)
    return diarize_value in ("true", "1"), speakers


def authorised(header: str | None, api_key: str | None) -> bool:
    if not api_key:
        return True
    expected = f"Bearer {api_key}".encode()
    return hmac.compare_digest((header or "").encode(), expected)


class SpeechServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, address: tuple[str, int], engine: Engine, api_key: str | None = None,
                 max_body: int = MAX_BODY_BYTES) -> None:
        super().__init__(address, Handler)
        self.engine = engine
        self.api_key = api_key
        self.max_body = max_body


class Handler(BaseHTTPRequestHandler):
    server: SpeechServer
    server_version = "trustcabinet-speech/1"
    protocol_version = "HTTP/1.1"

    def do_GET(self) -> None:  # noqa: N802 — http.server naming
        self._handle("GET")

    def do_POST(self) -> None:  # noqa: N802
        self._handle("POST")

    def do_PUT(self) -> None:  # noqa: N802
        self._handle("PUT")

    def do_DELETE(self) -> None:  # noqa: N802
        self._handle("DELETE")

    def _handle(self, method: str) -> None:
        started = time.monotonic()
        url = urlsplit(self.path)
        body_path = None
        try:
            if url.path == "/health":
                if method != "GET":
                    raise Rejected(405, "method_not_allowed", close=True)
                self._send(200, {"status": "ok", "models": self.server.engine.model_ids})
                return
            if url.path not in ("/v1/transcribe", "/v1/embed"):
                raise Rejected(404, "not_found", close=True)
            if method != "POST":
                raise Rejected(405, "method_not_allowed", close=True)
            if not authorised(self.headers.get("Authorization"), self.server.api_key):
                raise Rejected(401, "unauthorized", close=True)
            if url.path == "/v1/transcribe":
                diarize, speakers = parse_transcribe_query(url.query)
                body_path = self._receive_body()
                self._send(200, self.server.engine.transcribe_file(body_path, diarize, speakers))
            else:
                if url.query:
                    raise Rejected(400, "invalid_parameter", close=True)
                body_path = self._receive_body()
                accepted, payload = self.server.engine.embed_file(body_path)
                self._send(200 if accepted else 422, payload)
        except Rejected as rejection:
            if rejection.close or body_path is None:
                # The body was never read, so the connection cannot be reused for another request.
                self.close_connection = True
            self._send(rejection.status, {"error": rejection.error})
        except UnreadableAudio as error:
            log.info("unreadable audio: %s", error)
            self._send(400, {"error": "unreadable_audio"})
        except Exception:  # noqa: BLE001 — never leak a traceback to the caller
            log.exception("request failed")
            self.close_connection = True
            self._send(500, {"error": "internal"})
        finally:
            if body_path:
                try:
                    os.unlink(body_path)
                except FileNotFoundError:
                    pass
            log.info("%s %s %.2fs", method, url.path, time.monotonic() - started)

    def _receive_body(self) -> str:
        """Stream the request body into a temporary file and return its path."""
        chunked = "chunked" in self.headers.get("Transfer-Encoding", "").lower()
        length_header = self.headers.get("Content-Length")
        if not chunked:
            if length_header is None:
                raise Rejected(411, "length_required", close=True)
            try:
                length = int(length_header)
            except ValueError as error:
                raise Rejected(400, "bad_request", close=True) from error
            if length < 0:
                raise Rejected(400, "bad_request", close=True)
            if length > self.server.max_body:
                raise Rejected(413, "too_large", close=True)
        handle = tempfile.NamedTemporaryFile(prefix="speech-", suffix=".audio", delete=False)
        try:
            with handle:
                if chunked:
                    self._copy_chunked(handle)
                else:
                    self._copy(handle, length)
        except BaseException:
            os.unlink(handle.name)
            raise
        return handle.name

    def _copy(self, out, length: int) -> None:
        remaining = length
        while remaining:
            block = self.rfile.read(min(BLOCK, remaining))
            if not block:
                raise Rejected(400, "incomplete_body", close=True)
            out.write(block)
            remaining -= len(block)

    def _copy_chunked(self, out) -> None:
        total = 0
        while True:
            size_line = self.rfile.readline(1024)
            try:
                size = int(size_line.split(b";", 1)[0].strip(), 16)
            except ValueError as error:
                raise Rejected(400, "bad_request", close=True) from error
            if size == 0:
                while self.rfile.readline(1024) not in (b"\r\n", b"\n", b""):
                    pass  # trailers
                return
            total += size
            if total > self.server.max_body:
                raise Rejected(413, "too_large", close=True)
            self._copy(out, size)
            self.rfile.readline(1024)  # CRLF after each chunk

    def _send(self, status: int, payload: dict) -> None:
        body = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        if self.close_connection:
            self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format: str, *args) -> None:  # noqa: A002 — stdlib signature
        log.debug("%s - %s", self.address_string(), format % args)
