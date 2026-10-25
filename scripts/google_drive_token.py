"""Create the Google Drive refresh token the publishing scripts use.

One-time setup. It needs an OAuth client of type **Desktop app** from a Google
Cloud project with the Drive API enabled:
https://console.cloud.google.com/apis/credentials

Run:
    $env:GOOGLE_DRIVE_CLIENT_ID = "....apps.googleusercontent.com"
    $env:GOOGLE_DRIVE_CLIENT_SECRET = "...."
    python scripts/google_drive_token.py

The browser opens, the account signs in, and the refresh token is printed.
Put it in GOOGLE_DRIVE_REFRESH_TOKEN next to the client id and secret, then
check the whole setup with:

    python scripts/google_drive_token.py --check

If the browser cannot reach this machine, add --manual: the script prints the
consent URL and asks for the URL the browser ends on (or just the code).

The token is a secret: keep it in the environment, never in the repository.
"""

from __future__ import annotations

import argparse
import http.server
import json
import os
import secrets
import socket
import sys
import urllib.error
import urllib.parse
import urllib.request
import webbrowser
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from drive_auth import SCOPE, TOKEN_URL, access_token, exchange_refresh_token  # noqa: E402

AUTH_URL = "https://accounts.google.com/o/oauth2/v2/auth"
ABOUT_URL = "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress,displayName)"


def consent_url(client_id: str, redirect_uri: str, state: str) -> str:
    return AUTH_URL + "?" + urllib.parse.urlencode(
        {
            "client_id": client_id,
            "redirect_uri": redirect_uri,
            "response_type": "code",
            "scope": SCOPE,
            "access_type": "offline",
            "prompt": "consent",
            "state": state,
        }
    )


def exchange_code(code: str, client_id: str, client_secret: str, redirect_uri: str) -> dict:
    request = urllib.request.Request(
        TOKEN_URL,
        data=urllib.parse.urlencode(
            {
                "grant_type": "authorization_code",
                "code": code,
                "client_id": client_id,
                "client_secret": client_secret,
                "redirect_uri": redirect_uri,
            }
        ).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.loads(response.read())
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")[:400]
        raise SystemExit(f"Google refused the authorization code ({error.code}): {detail}") from error


def whoami(token: str) -> str:
    request = urllib.request.Request(ABOUT_URL, headers={"Authorization": f"Bearer {token}"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            user = json.loads(response.read())["user"]
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")[:400]
        raise SystemExit(f"Drive refused the token ({error.code}): {detail}") from error
    return f"{user.get('displayName', '?')} <{user.get('emailAddress', '?')}>"


def from_browser(client_id: str, client_secret: str) -> str:
    state = secrets.token_urlsafe(16)
    result: dict[str, str] = {}

    class Handler(http.server.BaseHTTPRequestHandler):
        def do_GET(self) -> None:  # noqa: N802 (http.server naming)
            query = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
            if query.get("state", [""])[0] != state:
                result["error"] = "state mismatch"
            elif "error" in query:
                result["error"] = query["error"][0]
            elif "code" in query:
                result["code"] = query["code"][0]
            else:
                return  # favicon or a stray request; keep waiting
            body = (
                "<p>Đã nhận. Quay lại cửa sổ terminal.</p>"
                if "code" in result
                else f"<p>Không lấy được quyền: {result.get('error')}</p>"
            ).encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *args: object) -> None:
            pass

    server = http.server.HTTPServer(("127.0.0.1", 0), Handler)
    server.socket.settimeout(300)
    redirect_uri = f"http://127.0.0.1:{server.server_address[1]}/"
    url = consent_url(client_id, redirect_uri, state)
    print("Mở trình duyệt và đăng nhập tài khoản có quyền ghi thư mục Drive:")
    print(url)
    print()
    webbrowser.open(url)
    try:
        while not result:
            server.handle_request()
    except (socket.timeout, TimeoutError):
        raise SystemExit("Hết thời gian chờ trình duyệt (5 phút). Chạy lại với --manual.") from None
    finally:
        server.server_close()
    if "code" not in result:
        raise SystemExit(f"Không nhận được authorization code: {result.get('error')}")
    return exchange_code(result["code"], client_id, client_secret, redirect_uri)["refresh_token"]


def from_paste(client_id: str, client_secret: str) -> str:
    redirect_uri = "http://127.0.0.1:8765/"
    state = secrets.token_urlsafe(16)
    print("Mở URL này trên máy có trình duyệt, đăng nhập, rồi dán URL cuối cùng vào đây:")
    print(consent_url(client_id, redirect_uri, state))
    print()
    answer = input("URL cuối (hoặc authorization code): ").strip()
    query = urllib.parse.parse_qs(urllib.parse.urlparse(answer).query)
    code = query.get("code", [answer])[0]
    if query.get("state", [state])[0] != state:
        raise SystemExit("State không khớp — URL không phải của lần chạy này.")
    return exchange_code(code, client_id, client_secret, redirect_uri)["refresh_token"]


def report(refresh: str) -> None:
    print()
    print("Refresh token đã tạo. Thêm vào môi trường (PowerShell):")
    print()
    print(f'  $env:GOOGLE_DRIVE_REFRESH_TOKEN = "{refresh}"')
    print(f'  $env:GOOGLE_DRIVE_CLIENT_ID = "{os.environ["GOOGLE_DRIVE_CLIENT_ID"]}"')
    print(f'  $env:GOOGLE_DRIVE_CLIENT_SECRET = "{os.environ["GOOGLE_DRIVE_CLIENT_SECRET"]}"')
    print()
    print("Giữ lâu dài thì dùng setx cho cả ba (chỉ áp dụng cho cửa sổ mở sau đó):")
    print(f'  setx GOOGLE_DRIVE_REFRESH_TOKEN "{refresh}"')
    print()
    print("Kiểm tra: python scripts/google_drive_token.py --check")


def check() -> int:
    token = access_token()
    source = (
        "GOOGLE_DRIVE_ACCESS_TOKEN"
        if os.environ.get("GOOGLE_DRIVE_ACCESS_TOKEN")
        else "GOOGLE_DRIVE_REFRESH_TOKEN"
    )
    print(f"Credential: {source}")
    print(f"Drive account: {whoami(token)}")
    print("OK: token dùng được với scope Drive đầy đủ.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="verify the credential already in the environment")
    parser.add_argument("--manual", action="store_true", help="paste the final URL instead of opening a browser")
    parser.add_argument("--client-id", default=os.environ.get("GOOGLE_DRIVE_CLIENT_ID"))
    parser.add_argument("--client-secret", default=os.environ.get("GOOGLE_DRIVE_CLIENT_SECRET"))
    args = parser.parse_args()

    if args.check:
        return check()
    if not args.client_id or not args.client_secret:
        raise SystemExit(
            "Cần OAuth client của Google Cloud (loại Desktop app): đặt "
            "GOOGLE_DRIVE_CLIENT_ID và GOOGLE_DRIVE_CLIENT_SECRET, hoặc truyền "
            "--client-id/--client-secret. Xem hướng dẫn ở đầu file này."
        )
    os.environ["GOOGLE_DRIVE_CLIENT_ID"] = args.client_id
    os.environ["GOOGLE_DRIVE_CLIENT_SECRET"] = args.client_secret

    refresh = (
        from_paste(args.client_id, args.client_secret)
        if args.manual
        else from_browser(args.client_id, args.client_secret)
    )
    if not refresh:
        raise SystemExit(
            "Google không trả refresh token. Vào https://myaccount.google.com/permissions "
            "gỡ quyền của app rồi chạy lại (script đã xin prompt=consent)."
        )
    token = exchange_refresh_token(refresh, args.client_id, args.client_secret)
    print(f"Tài khoản Drive: {whoami(token)}")
    report(refresh)
    return 0


if __name__ == "__main__":
    sys.exit(main())
