#!/usr/bin/env python3
"""Room mic (ADR-0041) under load: one secretary per hybrid meeting records a take at the same time.

Each take is streamed in 10-second parts the way the browser sends them (at --speed × real time), switched off, and
then the transcript is polled until it is done. Reports, per take: upload time, how long the transcript waited in
line, how long it took to run, and the real-time factor (run time ÷ audio length). The transcription worker takes one
recording at a time, so with K takes the K-th waits for the others.

--rounds R records R takes one after another in the same meeting. Each take is appended to the room recording and
the whole recording is transcribed again (PO 01/11/2026, as AI-eCabinet), so the R-th run covers R takes of audio.

    python3 tests/load/room_mic.py --fixture tests/load/out/fixture.json --audio tests/load/out/audio/room-300s.wav \
        --takes 3 --speed 10 > tests/load/out/room-mic.json
"""
import argparse
import json
import os
import threading
import time
import urllib.error
import urllib.request


def request(base, method, path, token, body=None, ctype="application/json"):
    data = body if isinstance(body, (bytes, type(None))) else json.dumps(body).encode()
    req = urllib.request.Request(base + path, data=data, method=method)
    req.add_header("authorization", f"Bearer {token}")
    if data is not None:
        req.add_header("content-type", ctype)
    try:
        with urllib.request.urlopen(req, timeout=120) as res:
            raw = res.read()
            return res.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as err:
        return err.code, err.read().decode(errors="replace")


def login(base, email, password):
    status, body = request(base, "POST", "/api/auth/login", None, {"email": email, "password": password})
    assert status == 200, (status, body)
    return body["accessToken"]


def take(base, token, meeting, pcm, speed, result):
    t0 = time.monotonic()
    status, body = request(base, "POST", f"/api/meetings/{meeting}/recordings/takes", token,
                           {"consentConfirmed": True, "consentPurpose": "Đo tải"})
    if status != 201:
        result.update(error=f"open take: {status} {body}")
        return
    rid, size = body["recordingId"], body["partSize"]
    # The browser's stream: a WAV header whose sizes are "unknown", then raw PCM.
    header = bytearray(pcm[:44])
    header[4:8] = header[40:44] = b"\xff\xff\xff\xff"
    stream = bytes(header) + pcm[44:]
    parts = [stream[i:i + size] for i in range(0, len(stream), size)]
    part_seconds = size / 32000
    worst_part = 0.0
    for n, chunk in enumerate(parts, 1):
        final = n == len(parts)
        s = time.monotonic()
        status, body = request(base, "PUT", f"/api/recordings/{rid}/parts/{n}" + ("?final=true" if final else ""),
                               token, chunk, "application/octet-stream")
        worst_part = max(worst_part, time.monotonic() - s)
        if status != 204:
            result.update(error=f"part {n}: {status} {body}")
            # Switch the mic off so the meeting is not left with an open take.
            request(base, "POST", f"/api/recordings/{rid}/complete", token)
            return
        if not final:
            time.sleep(max(0.0, part_seconds / speed - (time.monotonic() - s)))
    status, body = request(base, "POST", f"/api/recordings/{rid}/complete", token)
    if status != 200:
        result.update(error=f"complete: {status} {body}")
        return
    rid = body.get("recordingId") or rid
    saved = time.monotonic()
    started = None
    while True:
        status, tr = request(base, "GET", f"/api/recordings/{rid}/transcript", token)
        state = tr.get("status") if isinstance(tr, dict) else None
        if state == "Processing" and started is None:
            started = time.monotonic()
        if state in ("Completed", "Failed"):
            break
        if time.monotonic() - saved > 3600:
            state = "Timeout"
            break
        time.sleep(1)
    done = time.monotonic()
    audio = (len(pcm) - 44) / 32000
    # After several takes the run covers the whole room recording, not just this take.
    total = (tr.get("durationMs") or 0) / 1000 if isinstance(tr, dict) else 0
    total = max(total, audio)
    started = started or saved
    result.update(
        recordingId=rid, status=state, audioSeconds=round(audio, 1), transcribedSeconds=round(total, 1), parts=len(parts),
        uploadSeconds=round(saved - t0, 1), slowestPartSeconds=round(worst_part, 2),
        queuedSeconds=round(started - saved, 1), runSeconds=round(done - started, 1),
        realTimeFactor=round((done - started) / total, 3),
        segments=len(tr.get("segments", [])) if isinstance(tr, dict) else 0,
        error=tr.get("errorMessage") if isinstance(tr, dict) and state == "Failed" else None,
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fixture", default="tests/load/out/fixture.json")
    ap.add_argument("--audio", default="tests/load/out/audio/room-60s.wav")
    ap.add_argument("--takes", type=int, default=1, help="meetings recording at the same time")
    ap.add_argument("--speed", type=float, default=1.0, help="send parts this many times faster than real time")
    ap.add_argument("--offset", type=int, default=0, help="skip this many hybrid meetings, to start on a fresh one")
    ap.add_argument("--rounds", type=int, default=1, help="takes in a row per meeting, each waiting for its transcript")
    ap.add_argument("--password", default=os.environ.get("LOAD_ADMIN_PASSWORD", "TrustCabinet@2026"))
    args = ap.parse_args()
    f = json.load(open(args.fixture))
    meetings = [m["id"] for m in f["meetings"] if m["mode"] in ("Hybrid", "InPerson")][args.offset:args.offset + args.takes]
    if len(meetings) < args.takes:
        raise SystemExit(f"fixture has {len(meetings)} hybrid/in-person meetings, need {args.takes}")
    token = login(f["base"], f["secretary"], args.password)
    pcm = open(args.audio, "rb").read()
    results = [[{"meeting": m, "round": n} for n in range(1, args.rounds + 1)] for m in meetings]

    def rounds(meeting, slots):
        for slot in slots:
            take(f["base"], token, meeting, pcm, args.speed, slot)
            if slot.get("error"):
                break

    threads = [threading.Thread(target=rounds, args=(m, r)) for m, r in zip(meetings, results)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    print(json.dumps({"audio": os.path.basename(args.audio), "takes": args.takes, "speed": args.speed,
                      "rounds": args.rounds, "results": [r for slots in results for r in slots]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
