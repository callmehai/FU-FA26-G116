#!/usr/bin/env python3
"""Benchmark the AI draft (SCRUM-36) on meetings from complete to missing one part or another.

    python3 tests/ai/benchmark.py seed                       # creates the [AI-BENCH] meetings, writes out/fixture.json
    python3 tests/ai/benchmark.py run --label qwen2.5-7b     # asks each for a draft, scores it, writes out/<label>.md
    python3 tests/ai/benchmark.py clean                      # cancels the [AI-BENCH] meetings

The model is whatever the backend's `Ai:BaseUrl` / `Ai:Model` point at: change them, restart, run again with another
label, and compare the reports. Seeding writes the meeting data straight into the development database (through
`docker compose exec postgres psql`), so run it against a local stack only. Standard library only.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from cases import CASES  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = pathlib.Path(__file__).parent / "out"
PASSWORD = "TrustCabinet@2026"
PREFIX = "[AI-BENCH]"


def api(base: str, method: str, path: str, token: str | None = None, body: object | None = None) -> tuple[int, dict]:
    data = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(base + path, data=data, method=method)
    request.add_header("content-type", "application/json")
    if token:
        request.add_header("authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as error:
        raw = error.read()
        return error.code, json.loads(raw) if raw else {}


def login(base: str, seed: str) -> str:
    status, body = api(base, "POST", "/api/auth/login", body={"email": f"{seed}@trustcabinet.local", "password": PASSWORD})
    if status != 200:
        sys.exit(f"login {seed}: {status} {body}")
    return body["accessToken"]


def psql(sql: str) -> str:
    result = subprocess.run(
        ["docker", "compose", "exec", "-T", "postgres", "psql", "-U", "trustcabinet", "-d", "trustcabinet",
         "-v", "ON_ERROR_STOP=1", "-qtA"],
        input=sql, text=True, capture_output=True, cwd=ROOT)
    if result.returncode != 0:
        sys.exit(f"psql failed:\n{result.stderr}")
    return result.stdout.strip()


def q(text: str | None) -> str:
    return "null" if text is None else "'" + text.replace("'", "''") + "'"


def seed(base: str) -> None:
    token = login(base, "thuky")
    people = {}
    for who in ("chutoa", "thuky", "thanhvien"):
        _, body = api(base, "GET", f"/api/meetings/participant-options?q={who}@", token)
        people[who] = body["items"][0]["id"]
    fixture = {}
    for case in CASES:
        start = dt.datetime.now(dt.UTC).replace(microsecond=0) - dt.timedelta(hours=3)
        status, body = api(base, "POST", "/api/meetings", token, {
            "title": f"{PREFIX} {case['title']}",
            # The API takes only a future time; the meeting is moved back below, as if it had started 3 hours ago.
            "plannedStartTime": (start + dt.timedelta(days=2)).isoformat(),
            "plannedEndTime": (start + dt.timedelta(days=2, hours=2)).isoformat(),
            "mode": "InPerson", "location": "Phòng họp số 2, tầng 3", "onlineLink": None, "sensitivity": "Normal",
            "description": "", "participants": [
                {"userId": people["chutoa"], "role": "Chair"}, {"userId": people["thuky"], "role": "Secretary"},
                {"userId": people["thanhvien"], "role": "Member"}],
        })
        if status != 201:
            sys.exit(f"create {case['key']}: {status} {body}")
        meeting = body["meeting"]["id"]

        def at(minutes: float) -> str:
            return (start + dt.timedelta(minutes=minutes)).isoformat()

        sql = [f"update meetings set status='InProgress', planned_start_time={q(at(0))}, planned_end_time={q(at(120))}, "
               f"actual_started_at={q(at(0))} where id={q(meeting)};"]
        for position, item in enumerate(case["agenda"], 1):
            started = item["start"] is not None
            done = item.get("end") is not None
            conclusion = item["conclusion"]
            sql.append(
                "insert into agenda_items(id,meeting_id,position,title,description,presenter_id,presenter_name,status,"
                "actual_started_at,actual_ended_at,conclusion,conclusion_updated_at,conclusion_updated_by_id,created_at,updated_at) "
                f"values ({q(str(uuid.uuid4()))},{q(meeting)},{position},{q(item['title'])},'',{q(people[item['presenter']])},'',"
                f"{q('Done' if done else 'InProgress' if started else 'Pending')},{q(at(item['start'])) if started else 'null'},"
                f"{q(at(item['end'])) if done else 'null'},{q(conclusion)},{'now()' if conclusion else 'null'},"
                f"{q(people['thuky']) if conclusion else 'null'},now(),now());")
        for line_at, who, text in case["mic"]:
            sql.append(
                "insert into speech_clips(id,meeting_id,speaker_id,client_clip_id,started_at,duration_ms,text,created_at,updated_at) "
                f"values ({q(str(uuid.uuid4()))},{q(meeting)},{q(people[who])},{q(str(uuid.uuid4()))},{q(at(line_at))},8000,{q(text)},now(),now());")
        if case["room"]:
            recording, transcript = str(uuid.uuid4()), str(uuid.uuid4())
            takes = json.dumps([{"offsetMs": 0, "startedAt": at(0), "durationMs": 40 * 60_000}])
            sql.append(
                "insert into recordings(id,meeting_id,source,status,file_name,storage_path,content_type,byte_size,duration_ms,"
                "consent_purpose,created_by_id,created_at,updated_at,takes) "
                f"values ({q(recording)},{q(meeting)},'BrowserCapture','Available','mic-phong.wav','bench/none','audio/wav',0,"
                f"{40 * 60_000},'Lập biên bản',{q(people['thuky'])},now(),now(),{q(takes)}::jsonb);")
            sql.append(
                "insert into transcripts(id,recording_id,status,provider,language,progress_percent,error_message,completed_at,created_at,updated_at) "
                f"values ({q(transcript)},{q(recording)},'Completed','bench','vi',100,'',now(),now(),now());")
            for label in sorted({speaker for _, speaker, _ in case["room"]}):
                sql.append(
                    "insert into transcript_speakers(id,transcript_id,label,speech_ms,created_at,updated_at) "
                    f"values ({q(str(uuid.uuid4()))},{q(transcript)},{q(label)},60000,now(),now());")
            for position, (line_at, label, text) in enumerate(case["room"]):
                ms = int(line_at * 60_000)
                sql.append(
                    "insert into transcript_segments(id,transcript_id,position,start_ms,end_ms,text,speaker_label,created_at,updated_at) "
                    f"values ({q(str(uuid.uuid4()))},{q(transcript)},{position},{ms},{ms + 8000},{q(text)},{q(label)},now(),now());")
        for vote in case["votes"]:
            vote_id = str(uuid.uuid4())
            sql.append(
                "insert into voting_sessions(id,meeting_id,title,description,is_anonymous,status,opened_at,closed_at,concluded_at,"
                "concluded_by_id,eligible_count,created_by_id,conclusion,conclusion_note,eligible_roles,created_at,updated_at) "
                f"values ({q(vote_id)},{q(meeting)},{q(vote['title'])},'',{str(vote['anonymous']).lower()},'Closed',{q(at(30))},"
                f"{q(at(34))},{q(at(35))},{q(people['chutoa'])},3,{q(people['thuky'])},{q(vote['conclusion'])},{q(vote['note'])},"
                "'{Chair,Secretary,Member}',now(),now());")
            for position, (label, count) in enumerate(vote["options"], 1):
                sql.append(
                    "insert into voting_options(id,voting_session_id,position,label,ballot_count,created_at,updated_at) "
                    f"values ({q(str(uuid.uuid4()))},{q(vote_id)},{position},{q(label)},{count},now(),now());")
        for name in case["documents"]:
            # Only the name reaches the model (as in AI-eCabinet); no file is stored.
            stored = str(uuid.uuid4())
            sql.append(
                "insert into stored_files(id,storage_key,original_file_name,content_type,byte_size,content_sha256,uploaded_by_id,"
                f"uploaded_at,created_at,updated_at) values ({q(stored)},{q('bench/' + stored)},{q(name)},'application/pdf',0,"
                f"{q('0' * 64)},{q(people['thuky'])},now(),now(),now());")
            sql.append(
                "insert into meeting_documents(id,meeting_id,stored_file_id,file_name,added_by_id,added_at,created_at,updated_at) "
                f"values ({q(str(uuid.uuid4()))},{q(meeting)},{q(stored)},{q(name)},{q(people['thuky'])},now(),now(),now());")
        psql("begin;\n" + "\n".join(sql) + "\ncommit;\n")
        fixture[case["key"]] = meeting
        print(f"seeded {case['key']}: {meeting}")
    OUT.mkdir(exist_ok=True)
    (OUT / "fixture.json").write_text(json.dumps(fixture, indent=2))


def flat(draft: dict) -> str:
    return "\n".join([draft["objectives"], draft["discussion"], draft["conclusions"]])


def run(base: str, label: str, timeout: int) -> None:
    fixture = json.loads((OUT / "fixture.json").read_text())
    token = login(base, "thuky")
    results = []
    for case in CASES:
        meeting = fixture[case["key"]]
        status, body = api(base, "POST", f"/api/meetings/{meeting}/summary", token)
        if status != 202:
            sys.exit(f"{case['key']}: {status} {body}")
        began = time.monotonic()
        while True:
            time.sleep(3)
            _, state = api(base, "GET", f"/api/meetings/{meeting}/summary", token)
            summary = state["summary"]
            if summary["status"] in ("Ready", "Failed"):
                break
            if time.monotonic() - began > timeout:
                sys.exit(f"{case['key']}: still {summary['status']} after {timeout}s")
        seconds = round(time.monotonic() - began)
        if summary["status"] == "Failed":
            results.append({"case": case, "seconds": seconds, "error": summary["errorMessage"]})
            print(f"{case['key']}: FAILED {summary['errorMessage']}")
            continue
        text = flat(summary["draft"])
        # "a|b": any of the spellings counts ("18/20|18 trên 20").
        hits = [p for p in case["expect"] if any(alt in text for alt in p.split("|"))]
        slips = [p for p in case["forbid"] if p in text]
        results.append({"case": case, "seconds": seconds, "draft": summary["draft"], "model": summary["model"],
                        "hits": hits, "slips": slips})
        print(f"{case['key']}: {len(hits)}/{len(case['expect'])} expected, {len(slips)} forbidden, {seconds}s")
    write_report(label, results)


def write_report(label: str, results: list[dict]) -> None:
    expected = sum(len(r["case"]["expect"]) for r in results)
    hit = sum(len(r.get("hits", [])) for r in results)
    slips = sum(len(r.get("slips", [])) for r in results)
    lines = [f"# Đo dự thảo AI — {label}", "",
             f"Chạy lúc {dt.datetime.now().strftime('%H:%M %d/%m/%Y')}. Đúng ý cần có **{hit}/{expected}**, "
             f"lỗi cấm **{slips}**, tổng thời gian **{sum(r['seconds'] for r in results)} giây**.", "",
             "| Cuộc họp | Ý cần có | Lỗi cấm | Thời gian |", "|---|---|---|---|"]
    for r in results:
        case = r["case"]
        if "error" in r:
            lines.append(f"| {case['title']} | lỗi: {r['error']} | | {r['seconds']} giây |")
            continue
        missing = [p for p in case["expect"] if p not in r["hits"]]
        lines.append(f"| {case['title']} | {len(r['hits'])}/{len(case['expect'])}"
                     f"{' (thiếu: ' + ', '.join(missing) + ')' if missing else ''} | "
                     f"{', '.join(r['slips']) or '0'} | {r['seconds']} giây |")
    for r in results:
        if "draft" not in r:
            continue
        lines += ["", f"## {r['case']['title']}", "", "```text", "I. Mục tiêu và phạm vi cuộc họp", r["draft"]["objectives"], "",
                  "II. Nội dung thảo luận và đánh giá", r["draft"]["discussion"], "",
                  "III. Kết luận và phân công thực hiện", r["draft"]["conclusions"], "```"]
    OUT.mkdir(exist_ok=True)
    path = OUT / f"{label}.md"
    path.write_text("\n".join(lines) + "\n")
    print(f"report: {path}")


def clean() -> None:
    # meeting_status_history is append-only (a trigger refuses deletes), so the bench meetings are cancelled, not deleted.
    psql(f"""begin;
        update meetings set status='Cancelled', cancelled_at=now(), cancelled_from_status=status,
            cancellation_reason='Dọn dữ liệu đo AI'
        where title like {q(PREFIX + '%')} and status <> 'Cancelled';
        commit;""")
    print("cancelled the [AI-BENCH] meetings")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=["seed", "run", "clean"])
    parser.add_argument("--base", default="http://localhost:5225")
    parser.add_argument("--label", default="model")
    parser.add_argument("--timeout", type=int, default=900, help="seconds to wait for one draft")
    args = parser.parse_args()
    if args.command == "seed":
        seed(args.base)
    elif args.command == "run":
        run(args.base, args.label, args.timeout)
    else:
        clean()


if __name__ == "__main__":
    main()
