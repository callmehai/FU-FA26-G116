#!/usr/bin/env python3
"""Prepare a load test: N throwaway members and meetings that are already in progress.

Creates users loadNN@trustcabinet.local (skipped when they exist), then one meeting per mode given, walks each
through Submit → Approve → Publish → Start, and prints a JSON fixture the k6 scripts read. Uses only the public API,
so the same script works locally and on the test server. Standard library only.

    python3 tests/load/setup.py --base http://localhost:5225 --users 20 --modes Online,Hybrid > tests/load/out/fixture.json

Seeded demo accounts do the admin, organizer, approver and chair work; pass their password with --password or
LOAD_ADMIN_PASSWORD. The members' password comes from --member-password or LOAD_MEMBER_PASSWORD.
"""
import argparse
import datetime as dt
import json
import os
import sys
import urllib.error
import urllib.request


def call(base, method, path, token=None, body=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(base + path, data=data, method=method)
    req.add_header("content-type", "application/json")
    if token:
        req.add_header("authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=30) as res:
            raw = res.read()
            return res.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as err:
        raw = err.read()
        try:
            return err.code, json.loads(raw)
        except ValueError:
            return err.code, raw.decode(errors="replace")


def login(base, email, password):
    status, body = call(base, "POST", "/api/auth/login", body={"email": email, "password": password})
    if status != 200:
        sys.exit(f"login {email}: {status} {body}")
    return body["accessToken"]


def must(status, body, *ok, what):
    if status not in ok:
        sys.exit(f"{what}: {status} {body}")
    return body


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="http://localhost:5225")
    ap.add_argument("--users", type=int, default=20)
    ap.add_argument("--modes", default="Online,Hybrid",
                    help="one meeting per entry, repeats allowed: Online, Hybrid, InPerson (e.g. Hybrid,Hybrid,Hybrid)")
    ap.add_argument("--domain", default="trustcabinet.local")
    ap.add_argument("--password", default=os.environ.get("LOAD_ADMIN_PASSWORD", "TrustCabinet@2026"))
    ap.add_argument("--member-password", default=os.environ.get("LOAD_MEMBER_PASSWORD", "LoadTest@2026-members"))
    ap.add_argument("--title", default="[TEST] Đo tải")
    args = ap.parse_args()
    base, d = args.base.rstrip("/"), args.domain

    admin = login(base, f"quantri@{d}", args.password)
    organizer = login(base, f"thuky@{d}", args.password)
    approver = login(base, f"chutoa@{d}", args.password)

    roles = must(*call(base, "GET", "/api/roles", admin), 200, what="roles")
    member_role = next(r["id"] for r in roles if r["isDefault"])
    existing = {u["email"]: u for u in must(*call(base, "GET", "/api/users", admin), 200, what="users")}

    members = []
    for i in range(1, args.users + 1):
        email = f"load{i:02d}@{d}"
        user = existing.get(email)
        if user is None:
            body = must(*call(base, "POST", "/api/users", admin, {
                "fullName": f"Tải Thử {i:02d}", "email": email,
                "password": args.member_password, "roleId": member_role,
            }), 201, what=f"create {email}")
            user = body["user"]
        members.append({"id": user["id"], "email": email})

    chair_id = must(*call(base, "GET", "/api/auth/me", approver), 200, what="me")["id"]
    secretary_id = must(*call(base, "GET", "/api/auth/me", organizer), 200, what="me")["id"]
    approvers = must(*call(base, "GET", "/api/meetings/approvers", organizer), 200, what="approvers")
    if chair_id not in {a["id"] for a in approvers}:
        sys.exit("chutoa is not an approver; pick the meeting's approver by hand")
    approver_id = chair_id

    now = dt.datetime.now(dt.timezone.utc)
    meetings = []
    for n, mode in enumerate( [m.strip() for m in args.modes.split(",") if m.strip()], 1):
        participants = [{"userId": chair_id, "role": "Chair"}, {"userId": secretary_id, "role": "Secretary"}]
        participants += [{"userId": m["id"], "role": "Member"} for m in members]
        body = must(*call(base, "POST", "/api/meetings", organizer, {
            "title": f"{args.title} {mode} {n} {now:%d/%m %H:%M}",
            "plannedStartTime": (now + dt.timedelta(minutes=5)).isoformat(timespec="seconds"),
            "plannedEndTime": (now + dt.timedelta(hours=3)).isoformat(timespec="seconds"),
            "mode": mode, "location": None if mode == "Online" else "Phòng đo tải",
            "sensitivity": "Normal", "description": "Dữ liệu đo tải, xoá được.",
            "participants": participants,
        }), 201, what=f"create {mode} meeting")
        mid = body["meeting"]["id"]
        steps = [
            (organizer, {"action": "SubmitForApproval", "assignedApproverId": approver_id}),
            (approver, {"action": "Approve"}),
            (organizer, {"action": "Publish"}),
            (approver, {"action": "Start"}),
        ]
        for token, step in steps:
            must(*call(base, "POST", f"/api/meetings/{mid}/transitions", token, step), 200, what=f"{mode} {step['action']}")
        meetings.append({"mode": mode, "id": mid})

    json.dump({
        "base": base,
        "secretary": f"thuky@{d}", "chair": f"chutoa@{d}",
        "members": [m["email"] for m in members],
        "meetings": meetings,
    }, sys.stdout, ensure_ascii=False, indent=2)
    print()


if __name__ == "__main__":
    main()
