#!/usr/bin/env python3
"""Export real meetings from the app server's PostgreSQL, read-only, for the AI-draft evaluation.

    python3 tests/ai/real/export.py 01a1fc63-0818-7007-9e26-8532a29914d7:test-mic 01a1fce5-...:thu-ho

Each argument is `<meeting id>[:<slug>]`; the slug names the output files and the drafts (`<slug>-<provider>-<run>.md`).
Writes, into the gitignored `tests/ai/out/real/` (change with --out):

- `<slug>.json`: the system facts (meeting, agenda, attendees, votes, documents, recordings) and the frozen transcript,
  one line per segment or mic sentence with its wall-clock time computed as the app's .txt export does (the take's
  StartedAt plus the offset into it) and, for the room mic, the offset into the recording to scrub the audio;
- `<slug>.txt`: the same transcript in the app's `[HH:MM:SS] speaker: text` form;
- `stored/<slug>-<model>-1.md` (+ `.json`): the draft currently stored in meeting_summaries, if any.

The transcript is personal data: it never goes into the repo. The session runs with default_transaction_read_only on and
issues SELECTs only. Refuses a meeting whose transcript is still being made unless --allow-incomplete. Standard library only.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import unicodedata

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent / "out" / "real"
VIETNAM = dt.timezone(dt.timedelta(hours=7))
UUID = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")

# One JSON document per meeting. Only SELECTs; %(id)s is a validated UUID literal.
SQL = """
with m as (select * from meetings where id = '%(id)s')
select json_build_object(
  'meeting', (select json_build_object('id', id, 'title', title, 'description', description, 'status', status,
      'sensitivity', sensitivity, 'mode', mode, 'location', location, 'plannedStart', planned_start_time,
      'plannedEnd', planned_end_time, 'actualStart', actual_started_at, 'actualEnd', actual_ended_at) from m),
  'agenda', (select coalesce(json_agg(json_build_object('position', a.position, 'title', a.title,
      'description', a.description, 'presenter', coalesce(u.full_name, g.self_declared_display_name, g.invited_name, a.presenter_name),
      'status', a.status, 'start', a.actual_started_at, 'end', a.actual_ended_at, 'conclusion', a.conclusion)
      order by a.position), '[]')
    from agenda_items a left join users u on u.id = a.presenter_id
      left join meeting_guest_invitations g on g.id = a.presenter_guest_invitation_id
    where a.meeting_id = '%(id)s'),
  'participants', (select coalesce(json_agg(json_build_object('name', u.full_name, 'role', p.role,
      'response', p.response, 'attendance', p.attendance, 'current', p.assignment_status = 'Current' and p.removed_at is null,
      'delegatedTo', d.full_name) order by p.role, u.full_name), '[]')
    from meeting_participants p join users u on u.id = p.user_id left join users d on d.id = p.delegated_to_user_id
    where p.meeting_id = '%(id)s'),
  'guests', (select coalesce(json_agg(json_build_object('name', coalesce(self_declared_display_name, invited_name),
      'invitedName', invited_name, 'jobTitle', self_declared_job_title, 'response', response, 'attendance', attendance,
      'current', revoked_at is null) order by invited_name), '[]')
    from meeting_guest_invitations where meeting_id = '%(id)s'),
  'votes', (select coalesce(json_agg(json_build_object('title', v.title, 'description', v.description,
      'anonymous', v.is_anonymous, 'status', v.status, 'opened', v.opened_at, 'concluded', v.concluded_at,
      'closed', v.closed_at, 'eligible', v.eligible_count, 'conclusion', v.conclusion, 'conclusionNote', v.conclusion_note,
      'ballots', (select count(*) from vote_receipts r where r.voting_session_id = v.id),
      'options', (select coalesce(json_agg(json_build_object('label', o.label, 'count', o.ballot_count) order by o.position), '[]')
        from voting_options o where o.voting_session_id = v.id)) order by v.created_at), '[]')
    from voting_sessions v where v.meeting_id = '%(id)s'),
  'documents', (select coalesce(json_agg(json_build_object('fileName', file_name, 'addedAt', added_at) order by added_at), '[]')
    from meeting_documents where meeting_id = '%(id)s' and removed_at is null),
  'recordings', (select coalesce(json_agg(json_build_object('id', r.id, 'source', r.source, 'status', r.status,
      'createdAt', r.created_at, 'durationMs', r.duration_ms, 'takes', r.takes, 'deleted', r.deleted_at is not null,
      'transcript', case when t.id is null then null else json_build_object('id', t.id, 'status', t.status,
        'provider', t.provider, 'completedAt', t.completed_at,
        'speakers', (select coalesce(json_object_agg(s.label, coalesce(su.full_name, sg.self_declared_display_name, sg.invited_name)), '{}')
          from transcript_speakers s left join users su on su.id = s.assigned_user_id
            left join meeting_guest_invitations sg on sg.id = s.assigned_guest_id where s.transcript_id = t.id),
        'segments', (select coalesce(json_agg(json_build_object('id', s.id, 'startMs', s.start_ms, 'endMs', s.end_ms,
            'text', s.text, 'speakerLabel', s.speaker_label) order by s.position), '[]')
          from transcript_segments s where s.transcript_id = t.id and s.text <> '')) end) order by r.created_at), '[]')
    from recordings r left join transcripts t on t.recording_id = r.id where r.meeting_id = '%(id)s'),
  'clips', (select coalesce(json_agg(json_build_object('id', c.id, 'startedAt', c.started_at, 'durationMs', c.duration_ms,
      'text', c.text, 'speaker', coalesce(u.full_name, g.self_declared_display_name, g.invited_name)) order by c.started_at), '[]')
    from speech_clips c left join users u on u.id = c.speaker_id left join meeting_guest_invitations g on g.id = c.guest_invitation_id
    where c.meeting_id = '%(id)s' and c.excluded_at is null and c.text <> ''),
  'summary', (select json_build_object('model', model, 'status', status, 'requestedAt', requested_at,
      'generatedAt', generated_at, 'editedAt', edited_at, 'reviewedAt', reviewed_at, 'content', content)
    from meeting_summaries where meeting_id = '%(id)s')
)::text;
"""


def run_sql(args: argparse.Namespace, sql: str) -> str:
    # The variables are expanded inside the container, so the credentials never reach this machine's process list.
    remote = (f"docker exec -i {args.container} sh -c "
              "'psql -U \"$POSTGRES_USER\" -d \"$POSTGRES_DB\" -v ON_ERROR_STOP=1 -qAt'")
    script = "SET default_transaction_read_only = on;\nBEGIN READ ONLY;\n" + sql + "\nROLLBACK;\n"
    result = subprocess.run(["ssh", "-o", "BatchMode=yes", "-o", "ConnectTimeout=15", args.ssh, remote],
                            input=script, text=True, capture_output=True)
    if result.returncode != 0:
        sys.exit(f"psql failed:\n{result.stderr.strip()}")
    lines = [line for line in result.stdout.splitlines() if line.startswith("{")]
    if len(lines) != 1:
        sys.exit("unexpected psql output (expected one JSON line)")
    return lines[0]


def parse_time(value: str | None) -> dt.datetime | None:
    return None if value is None else dt.datetime.fromisoformat(value)


def clock(at: dt.datetime) -> str:
    return at.astimezone(VIETNAM).strftime("%H:%M:%S")


def offset(ms: int) -> str:
    seconds = max(0, ms) // 1000
    return f"{seconds // 3600:02d}:{seconds // 60 % 60:02d}:{seconds % 60:02d}"


def wall_clock(takes: list[dict] | None, created_at: dt.datetime, ms: int) -> dt.datetime:
    """The app's TranscriptText.LineTime / MeetingTranscriptReader.WallClock: the last take starting at or before ms."""
    if not takes:
        return created_at + dt.timedelta(milliseconds=ms)
    take = next((t for t in reversed(takes) if t["offsetMs"] <= ms), takes[0])
    return parse_time(take["startedAt"]) + dt.timedelta(milliseconds=max(0, ms - take["offsetMs"]))


def echo_words(text: str) -> set[str]:
    """RoomEcho.Words: lower case, no accents or punctuation."""
    plain = []
    for c in unicodedata.normalize("NFD", text):
        if unicodedata.category(c) == "Mn":
            continue
        plain.append("d" if c in "đĐ" else c.lower() if c.isalnum() else " ")
    return set("".join(plain).split())


def is_echo(start: dt.datetime, end: dt.datetime, text: str, online: list[tuple]) -> bool:
    """RoomEcho.IsEcho (ADR-0061): the room mic's copy of an online attendee's sentence, which the summary leaves out."""
    words = echo_words(text)
    if not words:
        return False
    slack = dt.timedelta(seconds=2)
    for o_start, o_end, o_words in online:
        if o_end + slack < start or end + slack < o_start:
            continue
        shared = sum(1 for w in words if w in o_words)
        if shared >= min(2, len(words)) and shared >= 0.6 * len(words):
            return True
    return False


def speaker_name(label: str, names: dict) -> str:
    if names.get(label):
        return names[label]
    if label.startswith("SPEAKER_") and label[8:].isdigit():
        return f"Người nói {int(label[8:]) + 1}"
    return label or "Người nói"


def build_transcript(raw: dict) -> tuple[list[dict], dict]:
    """The timeline the summary reads (MeetingTranscriptReader): mic sentences plus completed room/upload transcripts."""
    lines, stats = [], {"clips": 0, "segments": 0, "echoDropped": 0, "incomplete": []}
    hybrid = raw["meeting"]["mode"] == "Hybrid"
    online = []
    for c in raw["clips"]:
        at = parse_time(c["startedAt"])
        lines.append({"ref": f"clip:{c['id']}", "source": "mic", "at": at.isoformat(), "clock": clock(at),
                      "recordingId": None, "recordingOffset": None, "speaker": c["speaker"] or "Người nói",
                      "text": c["text"].strip()})
        stats["clips"] += 1
        if hybrid:
            online.append((at, at + dt.timedelta(milliseconds=c["durationMs"]), echo_words(c["text"])))
    for r in raw["recordings"]:
        t = r["transcript"]
        if r["deleted"] or r["status"] != "Available" or t is None:
            continue
        if t["status"] != "Completed":
            stats["incomplete"].append({"recordingId": r["id"], "status": t["status"]})
            continue
        created = parse_time(r["createdAt"])
        for s in t["segments"]:
            at = wall_clock(r["takes"], created, s["startMs"])
            end = at + dt.timedelta(milliseconds=max(0, s["endMs"] - s["startMs"]))
            if online and is_echo(at, end, s["text"], online):
                stats["echoDropped"] += 1
                continue
            lines.append({"ref": f"seg:{s['id']}", "source": "room", "at": at.isoformat(), "clock": clock(at),
                          "recordingId": r["id"], "recordingOffset": offset(s["startMs"]),
                          "speaker": speaker_name(s["speakerLabel"], t["speakers"]), "text": s["text"].strip()})
            stats["segments"] += 1
    lines.sort(key=lambda line: line["at"])
    for number, line in enumerate(lines, 1):
        line["n"] = number
    return lines, stats


def draft_markdown(content: dict) -> str:
    """The stored draft (objectives, discussion, conclusions) as the three sections of the minutes."""
    return (f"## I. Mục tiêu và phạm vi cuộc họp\n\n{content.get('objectives', '').strip()}\n\n"
            f"## II. Nội dung thảo luận\n\n{content.get('discussion', '').strip()}\n\n"
            f"## III. Kết luận\n\n{content.get('conclusions', '').strip()}\n")


def safe(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9.]+", "", name.replace(":", ".")) or "model"


def export(args: argparse.Namespace, meeting_id: str, slug: str) -> dict:
    raw = json.loads(run_sql(args, SQL % {"id": meeting_id}))
    if raw["meeting"] is None:
        sys.exit(f"{meeting_id}: no such meeting")
    lines, stats = build_transcript(raw)
    if stats["incomplete"] and not args.allow_incomplete:
        print(f"SKIP {slug}: transcript not Completed ({stats['incomplete']}); rerun later or pass --allow-incomplete")
        return {}
    text = "\n".join(f"[{line['clock']}] {line['speaker']}: {line['text']}" for line in lines)
    recordings = [{k: v for k, v in r.items() if k != "transcript"}
                  | {"transcript": None if r["transcript"] is None else
                     {k: v for k, v in r["transcript"].items() if k != "segments"}
                     | {"segmentCount": len(r["transcript"]["segments"])}}
                  for r in raw["recordings"]]
    doc = {
        "kind": "trustcabinet-real-meeting",
        "slug": slug,
        "exportedAt": dt.datetime.now(dt.timezone.utc).isoformat(),
        "transcriptSha256": hashlib.sha256(text.encode()).hexdigest(),
        "transcriptStats": stats,
        **{k: raw[k] for k in ("meeting", "agenda", "participants", "guests", "votes", "documents")},
        "recordings": recordings,
        "summary": None if raw["summary"] is None else {k: v for k, v in raw["summary"].items() if k != "content"},
        "transcript": lines,
    }
    out = pathlib.Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    (out / f"{slug}.json").write_text(json.dumps(doc, ensure_ascii=False, indent=1))
    (out / f"{slug}.txt").write_text(f"{raw['meeting']['title']}\nBản gỡ băng đóng băng lúc {doc['exportedAt']}\n\n{text}\n")
    stored = raw["summary"]
    if stored and stored.get("content"):
        content = json.loads(stored["content"])
        folder = out / "stored"
        folder.mkdir(exist_ok=True)
        name = f"{slug}-{safe(stored['model'])}-1"
        (folder / f"{name}.md").write_text(draft_markdown(content))
        (folder / f"{name}.json").write_text(json.dumps(
            {"model": stored["model"], "requestedAt": stored["requestedAt"], "generatedAt": stored["generatedAt"],
             "elapsedMs": None, "source": "meeting_summaries"}, ensure_ascii=False, indent=1))
    print(f"{slug}: {len(lines)} lines ({stats['segments']} room segments, {stats['clips']} mic sentences, "
          f"{stats['echoDropped']} echoes dropped), {len(raw['agenda'])} agenda items, "
          f"{sum(p['current'] for p in raw['participants'])} participants, {len(raw['guests'])} guests, "
          f"{len(raw['votes'])} votes, {len(raw['documents'])} documents, "
          f"stored draft: {stored['model'] if stored else 'none'}")
    return doc


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("meetings", nargs="+", help="<meeting id>[:<slug>]")
    parser.add_argument("--ssh", default="ubuntu@13.250.183.97")
    parser.add_argument("--container", default="trustcabinet-test-postgres-1")
    parser.add_argument("--out", default=str(OUT))
    parser.add_argument("--allow-incomplete", action="store_true", help="export even while a transcript is being made")
    args = parser.parse_args()
    for item in args.meetings:
        meeting_id, _, slug = item.partition(":")
        if not UUID.match(meeting_id):
            sys.exit(f"not a meeting id: {meeting_id}")
        if slug and not re.fullmatch(r"[a-z0-9][a-z0-9-]*", slug):
            sys.exit(f"slug must be lower-case letters, digits and hyphens: {slug}")
        export(args, meeting_id, slug or meeting_id[:8])


if __name__ == "__main__":
    main()
