import http.client
import json
import os
import socket
import sys
import threading
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from speech.pipeline import UnreadableAudio  # noqa: E402
from speech.server import SpeechServer, parse_transcribe_query, Rejected  # noqa: E402


class FakeEngine:
    model_ids = {"asr": "asr@1", "embedding": "emb@1", "vad": "vad@1"}

    def __init__(self):
        self.calls = []
        self.fail_with = None
        self.embed_result = (True, {"embedding": [1.0], "model": "emb@1"})

    def transcribe_file(self, path, diarize=True, speakers=None):
        self.calls.append(("transcribe", Path(path).read_bytes(), diarize, speakers, path))
        if self.fail_with:
            raise self.fail_with
        return {"durationMs": 1, "segments": [], "speakers": []}

    def embed_file(self, path):
        self.calls.append(("embed", Path(path).read_bytes(), path))
        if self.fail_with:
            raise self.fail_with
        return self.embed_result


class ServerTests(unittest.TestCase):
    api_key = None

    def setUp(self):
        self.engine = FakeEngine()
        self.server = SpeechServer(("127.0.0.1", 0), self.engine, self.api_key, max_body=1024)
        self.thread = threading.Thread(target=self.server.serve_forever, args=(0.02,), daemon=True)
        self.thread.start()
        self.port = self.server.server_address[1]

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()

    def request(self, method, path, body=None, headers=None):
        connection = http.client.HTTPConnection("127.0.0.1", self.port, timeout=5)
        try:
            connection.request(method, path, body=body, headers=headers or {})
            response = connection.getresponse()
            return response.status, json.loads(response.read() or b"null")
        finally:
            connection.close()

    def raw(self, request: bytes) -> tuple[int, dict]:
        with socket.create_connection(("127.0.0.1", self.port), timeout=5) as sock:
            sock.sendall(request)
            sock.shutdown(socket.SHUT_WR)
            data = b""
            while chunk := sock.recv(4096):
                data += chunk
        head, _, body = data.partition(b"\r\n\r\n")
        return int(head.split()[1]), json.loads(body)


class OpenServerTests(ServerTests):
    def test_health_reports_models(self):
        status, body = self.request("GET", "/health")
        self.assertEqual(status, 200)
        self.assertEqual(body, {"status": "ok", "models": FakeEngine.model_ids})

    def test_unknown_route_is_404(self):
        self.assertEqual(self.request("GET", "/v1/nothing"), (404, {"error": "not_found"}))

    def test_wrong_method_is_405(self):
        self.assertEqual(self.request("GET", "/v1/transcribe")[0], 405)
        self.assertEqual(self.request("POST", "/health", b"x")[0], 405)

    def test_transcribe_passes_body_and_parameters(self):
        status, _ = self.request("POST", "/v1/transcribe?diarize=false&speakers=3", b"audio-bytes")
        self.assertEqual(status, 200)
        kind, body, diarize, speakers, path = self.engine.calls[0]
        self.assertEqual((kind, body, diarize, speakers), ("transcribe", b"audio-bytes", False, 3))
        self.assertFalse(os.path.exists(path), "the temporary upload must be deleted")

    def test_transcribe_diarizes_by_default(self):
        self.request("POST", "/v1/transcribe", b"a")
        self.assertEqual(self.engine.calls[0][2:4], (True, None))

    def test_invalid_parameters_are_400(self):
        for query in ("diarize=maybe", "speakers=0", "speakers=21", "speakers=x", "diarize=1&diarize=0"):
            with self.subTest(query=query):
                self.assertEqual(
                    self.request("POST", f"/v1/transcribe?{query}", b"a"), (400, {"error": "invalid_parameter"})
                )
        self.assertEqual(self.engine.calls, [])

    def test_body_over_the_limit_is_413(self):
        self.assertEqual(self.request("POST", "/v1/embed", b"x" * 1025), (413, {"error": "too_large"}))
        self.assertEqual(self.engine.calls, [])

    def test_missing_length_is_411(self):
        status, body = self.raw(b"POST /v1/embed HTTP/1.1\r\nHost: x\r\n\r\n")
        self.assertEqual((status, body), (411, {"error": "length_required"}))

    def test_chunked_upload_is_accepted(self):
        status, _ = self.raw(
            b"POST /v1/embed HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n"
            b"5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n"
        )
        self.assertEqual(status, 200)
        self.assertEqual(self.engine.calls[0][1], b"hello world")

    def test_chunked_upload_over_the_limit_is_413(self):
        chunk = b"400\r\n" + b"x" * 0x400 + b"\r\n"
        status, body = self.raw(
            b"POST /v1/embed HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\n\r\n" + chunk + chunk + b"0\r\n\r\n"
        )
        self.assertEqual((status, body), (413, {"error": "too_large"}))

    def test_rejected_sample_is_422_with_metrics(self):
        self.engine.embed_result = (False, {"error": "too_short", "speechMs": 1200})
        self.assertEqual(
            self.request("POST", "/v1/embed", b"a"), (422, {"error": "too_short", "speechMs": 1200})
        )

    def test_unreadable_audio_is_400(self):
        self.engine.fail_with = UnreadableAudio("moov atom not found")
        self.assertEqual(self.request("POST", "/v1/transcribe", b"a"), (400, {"error": "unreadable_audio"}))

    def test_engine_failure_is_500_without_details(self):
        self.engine.fail_with = RuntimeError("secret internals")
        with self.assertLogs("speech.server", level="ERROR"):
            self.assertEqual(self.request("POST", "/v1/embed", b"a"), (500, {"error": "internal"}))

    def test_embed_takes_no_query(self):
        self.assertEqual(self.request("POST", "/v1/embed?x=1", b"a"), (400, {"error": "invalid_parameter"}))


class AuthenticatedServerTests(ServerTests):
    api_key = "s3cret-key"

    def test_health_needs_no_key(self):
        self.assertEqual(self.request("GET", "/health")[0], 200)

    def test_missing_or_wrong_key_is_401(self):
        self.assertEqual(self.request("POST", "/v1/embed", b"a"), (401, {"error": "unauthorized"}))
        self.assertEqual(
            self.request("POST", "/v1/embed", b"a", {"Authorization": "Bearer nope"}),
            (401, {"error": "unauthorized"}),
        )
        self.assertEqual(self.engine.calls, [])

    def test_right_key_is_accepted(self):
        status, _ = self.request("POST", "/v1/embed", b"a", {"Authorization": "Bearer s3cret-key"})
        self.assertEqual(status, 200)


class QueryParsingTests(unittest.TestCase):
    def test_defaults(self):
        self.assertEqual(parse_transcribe_query(""), (True, None))

    def test_values(self):
        self.assertEqual(parse_transcribe_query("diarize=0&speakers=20"), (False, 20))
        self.assertEqual(parse_transcribe_query("diarize=TRUE"), (True, None))

    def test_garbage_is_rejected(self):
        with self.assertRaises(Rejected):
            parse_transcribe_query("&&==")


del ServerTests  # base class only; keeps unittest from running it on its own

if __name__ == "__main__":
    unittest.main()
