"""Stub OpenAI-compatible tối thiểu cho QA TRANS_FT61/FT64.

Trả lời POST */chat/completions bằng một completion canned ("OK"), đủ để
backend TrustCabinet (OpenAiCompatibleModel) coi là "kết nối được" (200/MSG412)
và viết dự thảo qua nhánh API. Nội dung dự thảo của stub là chữ cố định —
case FT64 chỉ kiểm tra *đường đi* (Normal qua API, Internal ở máy local),
không kiểm tra chất lượng chữ AI.

Chạy: python tests/qa/fixtures/stub_api.py  (nghe 127.0.0.1:12121)
"""
import json
from http.server import BaseHTTPRequestHandler, HTTPServer

MODEL = "qa-stub-model"
DRAFT_TEXT = (
    "I. Mục tiêu: stub kiểm tra đường đi API. "
    "II. Nội dung: cuộc họp kiểm thử cấu hình AI. "
    "III. Kết luận: đường API hoạt động."
)


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def _send(self, status, obj):
        body = json.dumps(obj).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path.rstrip("/").endswith("/models"):
            return self._send(200, {"data": [{"id": MODEL}]})
        return self._send(404, {"error": "not_found"})

    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        try:
            payload = json.loads(self.rfile.read(length) or b"{}")
        except json.JSONDecodeError:
            payload = {}
        if not self.path.rstrip("/").endswith("/chat/completions"):
            return self._send(404, {"error": "not_found"})
        model = payload.get("model", MODEL)
        text = DRAFT_TEXT
        return self._send(
            200,
            {
                "id": "qa-stub-1",
                "object": "chat.completion",
                "model": model,
                "choices": [
                    {
                        "index": 0,
                        "message": {"role": "assistant", "content": text},
                        "finish_reason": "stop",
                    }
                ],
                "usage": {"prompt_tokens": 10, "completion_tokens": 30, "total_tokens": 40},
            },
        )


if __name__ == "__main__":
    server = HTTPServer(("127.0.0.1", 12121), Handler)
    print("stub API on http://127.0.0.1:12121", flush=True)
    server.serve_forever()
