"""Resolve a Google Drive access token for the scripts that talk to Drive.

Both ways of authenticating come from the environment; nothing is read from a
file, and no token is ever written into the repository.

* ``GOOGLE_DRIVE_ACCESS_TOKEN`` — a short-lived access token, about one hour.
  Handy for a single run, but it has to be pasted again every time.
* ``GOOGLE_DRIVE_REFRESH_TOKEN`` together with ``GOOGLE_DRIVE_CLIENT_ID`` and
  ``GOOGLE_DRIVE_CLIENT_SECRET`` — exchanged for a fresh access token on every
  run, so the setup is done once. Create it with
  ``python scripts/google_drive_token.py``.

Either way the token needs ``https://www.googleapis.com/auth/drive``: the files
we publish already exist on Drive and were not created by this client, so the
narrower ``drive.file`` scope cannot see them.
"""

from __future__ import annotations

import json
import os
import urllib.error
import urllib.parse
import urllib.request

SCOPE = "https://www.googleapis.com/auth/drive"
TOKEN_URL = "https://oauth2.googleapis.com/token"

_cached: str | None = None


def configured() -> bool:
    """True when the environment holds either kind of credential."""
    return bool(
        os.environ.get("GOOGLE_DRIVE_ACCESS_TOKEN")
        or os.environ.get("GOOGLE_DRIVE_REFRESH_TOKEN")
    )


def access_token() -> str:
    """Return an access token, exchanging the refresh token when that is what is set."""
    global _cached
    direct = os.environ.get("GOOGLE_DRIVE_ACCESS_TOKEN")
    if direct:
        return direct
    if _cached:
        return _cached
    refresh = os.environ.get("GOOGLE_DRIVE_REFRESH_TOKEN")
    if not refresh:
        raise SystemExit(missing_credential())
    client_id = os.environ.get("GOOGLE_DRIVE_CLIENT_ID")
    client_secret = os.environ.get("GOOGLE_DRIVE_CLIENT_SECRET")
    if not client_id or not client_secret:
        raise SystemExit(
            "GOOGLE_DRIVE_REFRESH_TOKEN also needs GOOGLE_DRIVE_CLIENT_ID and "
            "GOOGLE_DRIVE_CLIENT_SECRET, the OAuth client the token was issued to."
        )
    _cached = exchange_refresh_token(refresh, client_id, client_secret)
    return _cached


def exchange_refresh_token(refresh: str, client_id: str, client_secret: str) -> str:
    """Trade a refresh token for an access token that lasts about an hour."""
    payload = _post(
        {
            "grant_type": "refresh_token",
            "refresh_token": refresh,
            "client_id": client_id,
            "client_secret": client_secret,
        },
        "Google refused the refresh token",
    )
    token = payload.get("access_token")
    if not token:
        raise SystemExit(f"Google returned no access token: {payload}")
    return token


def missing_credential() -> str:
    return (
        "No Drive credential. Set GOOGLE_DRIVE_REFRESH_TOKEN with GOOGLE_DRIVE_CLIENT_ID "
        "and GOOGLE_DRIVE_CLIENT_SECRET (create them once with "
        "`python scripts/google_drive_token.py`), or set GOOGLE_DRIVE_ACCESS_TOKEN for a "
        f"single run. Both need the scope {SCOPE}."
    )


def _post(fields: dict[str, str], failure: str) -> dict:
    request = urllib.request.Request(
        TOKEN_URL,
        data=urllib.parse.urlencode(fields).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.loads(response.read())
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")[:400]
        raise SystemExit(f"{failure} ({error.code}): {detail}") from error
