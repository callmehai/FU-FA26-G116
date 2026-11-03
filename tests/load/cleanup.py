#!/usr/bin/env python3
"""Undo setup.py once the load test is over: erase every recording of the fixture's meetings, end the meetings, and
deactivate the loadNN members (accounts are deactivated, never deleted). Speech clips go with the recordings'
retention, like any meeting's.

    python3 tests/load/cleanup.py --fixture tests/load/out/server/fixture.json
"""
import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from setup import call, login  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fixture", default="tests/load/out/fixture.json")
    ap.add_argument("--password", default=os.environ.get("LOAD_ADMIN_PASSWORD", "TrustCabinet@2026"))
    args = ap.parse_args()
    f = json.load(open(args.fixture))
    base, domain = f["base"], f["secretary"].split("@")[1]
    secretary = login(base, f["secretary"], args.password)
    chair = login(base, f["chair"], args.password)
    admin = login(base, f"quantri@{domain}", args.password)

    for m in f["meetings"]:
        status, body = call(base, "GET", f"/api/meetings/{m['id']}/recordings", secretary)
        for rec in body.get("items", []) if status == 200 else []:
            if rec["status"] != "Deleted":
                print("erase recording", rec["id"], call(base, "DELETE", f"/api/recordings/{rec['id']}", secretary)[0])
        print("end", m["mode"], m["id"], call(base, "POST", f"/api/meetings/{m['id']}/transitions", chair, {"action": "End"})[0])

    users = {u["email"]: u for u in call(base, "GET", "/api/users", admin)[1]}
    for email in f["members"]:
        user = users.get(email)
        if user and user["isActive"]:
            print("deactivate", email, call(base, "PUT", f"/api/users/{user['id']}/status", admin, {"isActive": False})[0])


if __name__ == "__main__":
    main()
