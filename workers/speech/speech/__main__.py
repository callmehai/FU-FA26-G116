"""`python -m speech` — load the models and serve HTTP on $SPEECH_HOST:$SPEECH_PORT."""

from __future__ import annotations

import logging
import os
import signal
import sys

from .server import SpeechServer

log = logging.getLogger("speech")


def main() -> int:
    logging.basicConfig(
        level=os.environ.get("SPEECH_LOG_LEVEL", "INFO").upper(),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    from .runner import EngineProcess

    engine = EngineProcess()
    host = os.environ.get("SPEECH_HOST", "127.0.0.1")
    port = int(os.environ.get("SPEECH_PORT", "8090"))
    api_key = os.environ.get("SPEECH_API_KEY") or None
    server = SpeechServer((host, port), engine, api_key)

    def stop(*_: object) -> None:
        # PID 1 in a container ignores SIGTERM unless it installs a handler; exit promptly instead
        # of waiting for Docker's SIGKILL.
        raise SystemExit(0)

    signal.signal(signal.SIGTERM, stop)
    log.info("listening on %s:%d, bearer auth %s", host, port, "on" if api_key else "off")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        engine.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
