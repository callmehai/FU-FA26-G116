"""Draw the table-form ERD (docs/13-erd/muc-tieu) from the EF Core model snapshot.

Run from the repository root:
    python scripts/draw_erd.py              # redraw with the fixed grid below
    python scripts/draw_erd.py --optimize   # search a grid with fewer line crossings, print it
then export the image with draw.io (see docs/13-erd/muc-tieu/README.md).

Tables come from backend/TrustCabinet.Infrastructure/Migrations/TrustCabinetDbContextModelSnapshot.cs, column
order and enum types from backend/TrustCabinet.Domain. Tables sit on a grid. Two related tables in neighbouring
cells (diagonals included), or two cells apart in a straight vertical/horizontal line, are joined by one straight
line when it crosses no other table; every other line runs through the channels between grid cells, so no line
crosses a table. All line ends on one side of a table share one even spread. One line per related table pair. Lines to User are
drawn for every pair except USER_SKIP, the columns that only record who changed a row (they stay marked FK).
"""
import glob, html, json, os, random, re, sys, xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SNAPSHOT = os.path.join(ROOT, "backend", "TrustCabinet.Infrastructure", "Migrations",
                        "TrustCabinetDbContextModelSnapshot.cs")
DOMAIN = os.path.join(ROOT, "backend", "TrustCabinet.Domain")
OUT = os.path.join(ROOT, "docs", "13-erd", "muc-tieu", "trustcabinet-erd-muc-tieu.drawio")


def parse_snapshot(path):
    """Read tables, columns, keys, indexes and FKs out of the EF Core model snapshot."""
    src = open(path, encoding="utf-8-sig").read()

    # Split into top-level modelBuilder.Entity("X", b => { ... }); blocks by brace matching.
    blocks = []
    for m in re.finditer(r'modelBuilder\.Entity\("TrustCabinet\.Domain\.Entities\.(\w+)", b =>\s*\{', src):
        i = m.end(); depth = 1
        while depth:
            c = src[i]
            if c == '{': depth += 1
            elif c == '}': depth -= 1
            i += 1
        blocks.append((m.group(1), src[m.end():i - 1]))

    def stmts(body):
        # statements start with "b." at line start (after spaces) and end with ";"
        out, cur, depth = [], "", 0
        for line in body.splitlines():
            s = line.strip()
            if not s: continue
            cur += " " + s
            depth += s.count("{") - s.count("}")
            if depth == 0 and cur.rstrip().endswith(";"):
                out.append(cur.strip()); cur = ""
        return out

    ents = {}
    for name, body in blocks:
        e = ents.setdefault(name, {"name": name, "table": None, "columns": [], "pk": [], "indexes": [], "fks": [], "checks": []})
        for st in stmts(body):
            if st.startswith("b.Property<") or st.startswith("b.PrimitiveCollection<"):
                m = re.match(r'b\.(?:Property|PrimitiveCollection)<([^>]+)>\("(\w+)"\)', st)
                clr, prop = m.group(1), m.group(2)
                col = re.search(r'HasColumnName\("(\w+)"\)', st).group(1)
                ctype = re.search(r'HasColumnType\("([^"]+)"\)', st)
                ctype = ctype.group(1) if ctype else None
                if clr == "string":
                    nullable = ".IsRequired()" not in st
                else:
                    nullable = clr.endswith("?")
                e["columns"].append({"prop": prop, "col": col, "clr": clr, "type": ctype, "nullable": nullable,
                                     "maxlen": (re.search(r'HasMaxLength\((\d+)\)', st) or [None, None])[1],
                                     "default": (re.search(r'HasDefaultValue(?:Sql)?\(([^)]*)\)', st) or [None, None])[1]})
            elif st.startswith("b.HasKey("):
                e["pk"] = re.findall(r'"(\w+)"', st.split(")")[0])
            elif st.startswith("b.HasIndex("):
                head = st[len("b.HasIndex("):]
                if head.startswith("new[]"):
                    cols = re.findall(r'"(\w+)"', head.split("}")[0])
                else:
                    cols = re.findall(r'"(\w+)"', head.split(")")[0])
                e["indexes"].append({"cols": cols, "unique": ".IsUnique()" in st,
                                     "filter": (re.search(r'HasFilter\("(.*?)"\)', st) or [None, None])[1],
                                     "name": (re.search(r'HasDatabaseName\("(\w+)"\)', st) or [None, None])[1]})
            elif st.startswith("b.ToTable("):
                e["table"] = re.match(r'b\.ToTable\("(\w+)"', st).group(1)
                e["checks"] += re.findall(r'HasCheckConstraint\("(\w+)", "(.*?)"\);', st)
            elif st.startswith("b.HasOne("):
                m = re.match(r'b\.HasOne\("TrustCabinet\.Domain\.Entities\.(\w+)", (?:"(\w+)"|null)\)', st)
                target, nav = m.group(1), m.group(2)
                fkm = re.search(r'\.HasForeignKey\(([^)]*)\)', st)
                fkargs = re.findall(r'"([\w.]+)"', fkm.group(1))
                withone = ".WithOne(" in st
                if withone and fkargs and "." in fkargs[0]:
                    fkargs = fkargs[1:]
                e["fks"].append({"target": target, "nav": nav, "cols": fkargs, "one_to_one": withone,
                                 "required": ".IsRequired()" in st,
                                 "delete": (re.search(r'DeleteBehavior\.(\w+)', st) or [None, None])[1],
                                 "with": (re.search(r'\.With(?:Many|One)\(("?\w*"?)\)', st) or [None, None])[1]})

    return ents


m = parse_snapshot(SNAPSHOT)

ENUMS = set()
for f in glob.glob(os.path.join(DOMAIN, "**", "*.cs"), recursive=True):
    ENUMS |= set(re.findall(r"public enum (\w+)", open(f, encoding="utf-8-sig").read()))
decl = {}
for f in glob.glob(os.path.join(DOMAIN, "Entities", "*.cs")):
    s = open(f, encoding="utf-8-sig").read()
    decl[os.path.splitext(os.path.basename(f))[0]] = re.findall(r"public (?:required )?([\w<>\[\]?]+) (\w+) \{ get", s)

# old edge ids by (parent, child) so existing relationships keep their id
old_ids = {}
for c in (ET.parse(OUT).getroot().iter("mxCell") if os.path.exists(OUT) else []):
    if c.get("edge"):
        old_ids.setdefault((c.get("source"), c.get("target")), c.get("id"))

GRID = [
    [None, None, "TranscriptSegment", "Transcript", "CallSession", "CallParticipant", "TranscriptSpeaker", None, None],
    ["MeetingSummary", "EmailMessage", "MeetingInvitationDispatch", "Recording", "SpeakingRequest", "GuestSession", None, "GuestVerificationChallenge", None],
    [None, "MeetingMinutes", "MeetingParticipant", "Meeting", "MeetingNote", "AgendaItem", "MeetingGuestInvitation", "DocumentComment", "MeetingDocumentAgendaItem"],
    [None, "MinutesSignature", "Notification", "MeetingStatusHistory", None, "SpeechClip", "DocumentReadConfirmation", "MeetingDocument", None],
    ["VoiceSample", "RoleCapability", "Role", "User", "VoteReceipt", "Folder", "Document", "VoteDocument", "StoredFile"],
    ["VoiceConsent", None, None, "AuditLogEntry", "VoteEligibleVoter", "VotingSession", None, "VotingOption", "VoteChoice"],
]
pos = {t: (r, c) for r, row in enumerate(GRID) for c, t in enumerate(row) if t}
assert sorted(pos) == sorted(m), set(m) ^ set(pos)
NR, NC = len(GRID), len(GRID[0])

ROW_H, HEAD_H = 19, 28
MARGIN, SP = 26, 13         # channel margin (room for the crow's foot) and track spacing


def tw(s):
    w = 0.0
    for ch in s:
        if ch in "il.,:;|!'[]()": w += 0.30
        elif ch in "fjrt": w += 0.38
        elif ch in "mwMW": w += 0.86
        elif ch.isupper(): w += 0.68
        elif ch.isdigit(): w += 0.56
        else: w += 0.54
    return w * 11


def type_label(ent, c):
    d = dict((p, t) for t, p in decl.get(ent, [])).get(c["prop"], "").rstrip("?")
    if d in ENUMS: t = "enum"
    elif d.startswith("List<") and d[5:-1] in ENUMS: t = "enum[]"
    else:
        ct = c["type"]
        if ct.startswith("character varying") or ct == "text": t = "string"
        else: t = {"timestamp with time zone": "timestamptz", "integer": "int", "boolean": "bool",
                   "double precision": "float8", "jsonb": "json"}.get(ct, ct)
    return t + ("?" if c["nullable"] and c["prop"] not in m[ent]["pk"] else "")


tables = {}
for name, e in m.items():
    cols = {c["prop"]: c for c in e["columns"]}
    fkcols = {col for f in e["fks"] for col in f["cols"]}
    uniq = {tuple(i["cols"]) for i in e["indexes"] if i["unique"] and not i["filter"]}
    order = [p for _, p in decl.get(name, []) if p in cols] + [p for p in cols if p not in [q for _, q in decl.get(name, [])]]
    order = list(e["pk"]) + [p for p in order if p not in e["pk"] and p not in ("Id", "CreatedAt", "UpdatedAt")]
    rows = []
    for p in order:
        keys = [k for k, ok in (("PK", p in e["pk"]), ("FK", p in fkcols),
                                ("UK", (p,) in uniq and p not in e["pk"])) if ok]
        rows.append((",".join(keys), p, type_label(name, cols[p])))
    kw = 50 if any("," in r[0] for r in rows) else 36
    nw = int(max(tw(r[1]) for r in rows) + 14)
    tyw = int(max(tw(r[2]) for r in rows) + 8)
    w = max(kw + nw + tyw + 12, int(tw(name) * 1.25 + 30), 150, {"User": 300, "Meeting": 260, "MeetingGuestInvitation": 260}.get(name, 0))
    tables[name] = {"rows": rows, "kw": kw, "nw": nw, "tyw": tyw, "w": w, "h": HEAD_H + ROW_H * len(rows)}

# relationships: one per (parent, child) pair
pairs = {}
for child, e in m.items():
    for f in e["fks"]:
        key = (f["target"], child)
        p = pairs.setdefault(key, {"parent": f["target"], "child": child, "required": False, "one": True})
        p["required"] |= f["required"]
        p["one"] &= f["one_to_one"] or any(tuple(i["cols"]) == tuple(f["cols"]) and i["unique"] and not i["filter"]
                                           for i in e["indexes"])
USER_SKIP = {"CallSession", "MeetingDocument", "MeetingGuestInvitation", "MeetingMinutes", "MeetingSummary",
             "Recording", "StoredFile", "VotingSession", "DocumentComment", "MeetingNote", "SpeakingRequest",
             "TranscriptSegment", "TranscriptSpeaker", "VoteChoice"}
pairs = [p for p in pairs.values() if not (p["parent"] == "User" and p["child"] in USER_SKIP)]

def _build(grid, straight):
    global GRID, pos, NR, NC, colw, rowh, SIDE, plans, xs, ys, vx, hy, W, H, port, track, ntr, vw, hw
    GRID = grid
    pos = {t: (r, c) for r, row in enumerate(GRID) for c, t in enumerate(row) if t}
    NR, NC = len(GRID), len(GRID[0])
    colw = [max([tables[GRID[r][c]]["w"] for r in range(NR) if GRID[r][c]] or [0]) for c in range(NC)]
    rowh = [max([tables[GRID[r][c]]["h"] for c in range(NC) if GRID[r][c]] or [0]) for r in range(NR)]


    def place(vw, hw):
        """vw[i]: width of vertical channel i (left of column i, i in 0..NC); hw[j]: horizontal channel j."""
        xs, x = [], 0
        vx = []
        for c in range(NC):
            vx.append(x); x += vw[c]
            xs.append(x); x += colw[c]
        vx.append(x); x += vw[NC]
        ys, y = [], 0
        hy = []
        for r in range(NR):
            hy.append(y); y += hw[r]
            ys.append(y); y += rowh[r]
        hy.append(y); y += hw[NR]
        return xs, ys, vx, hy, x, y


    def box(t, xs, ys):
        r, c = pos[t]
        return xs[c], ys[r], tables[t]["w"], tables[t]["h"]


    def route_plan(p):
        """Abstract route: list of ('port', table, side) / ('H', j) / ('V', i) steps."""
        a, b = p["parent"], p["child"]
        (ra, ca), (rb, cb) = pos[a], pos[b]
        if a == b:
            return [("port", a, "R"), ("loop",), ("port", b, "R")]
        if ra == rb and abs(ca - cb) == 1:
            v = max(ca, cb)
            return [("port", a, "R" if cb > ca else "L"), ("V", v), ("port", b, "L" if cb > ca else "R")]
        if a in HUBS and ca != cb and (ra == rb or (a, b) in SIDE):
            h2, sb = (rb, "T") if rb >= ra else (rb + 1, "B")
            return [("port", a, "L" if cb < ca else "R"), ("V", ca if cb < ca else ca + 1), ("H", h2), ("port", b, sb)]
        if ra == rb:
            return [("port", a, "T"), ("H", ra), ("port", b, "T")]
        down = rb > ra
        h1 = ra + 1 if down else ra
        h2 = rb if down else rb + 1
        sa, sb = ("B", "T") if down else ("T", "B")
        if h1 == h2 and ca != cb:
            return [("port", a, sa), ("H", h1), ("port", b, sb)]
        if h1 == h2:
            return [("port", a, sa), ("H", h1), ("port", b, sb)]
        v = cb if ca < cb else (cb + 1 if ca > cb else ca + 1)
        return [("port", a, sa), ("H", h1), ("V", v), ("H", h2), ("port", b, sb)]


    HUBS = {"User", "Meeting", "MeetingGuestInvitation"}
    PORT_GAP = 26
    SIDE = set()
    for hub in HUBS:
        hr, hc = pos[hub]
        # room on each of the hub's left and right sides, minus the lines that must end there
        room = {s: int((tables[hub]["h"] - HEAD_H - 4) / PORT_GAP) for s in "LR"}
        cap_vert = 2 * int(tables[hub]["w"] / PORT_GAP) - sum(1 for p in pairs if p["child"] == hub)
        kids = []
        for k, p in enumerate(pairs):
            if hub not in (p["parent"], p["child"]) or p["parent"] == p["child"]:
                continue
            other = p["child"] if p["parent"] == hub else p["parent"]
            orow, ocol = pos[other]
            if orow == hr:
                room["R" if ocol > hc else "L"] -= 1
            elif p["parent"] == hub and k not in straight and ocol != hc:
                kids.append(other)
        kids.sort(key=lambda k: -abs(pos[k][1] - hc))
        for i, k in enumerate(kids):
            s = "R" if pos[k][1] > hc else "L"
            if room[s] > 0 and (cap_vert <= 0 or abs(pos[k][1] - hc) >= 2 or len(kids) - i > cap_vert):
                SIDE.add((hub, k)); room[s] -= 1
            else:
                cap_vert -= 1
    def straight_plan(p):
        (ra, ca), (rb, cb) = pos[p["parent"]], pos[p["child"]]
        if ra == rb:
            sa, sb = ("R", "L") if cb > ca else ("L", "R")
        else:
            sa, sb = ("B", "T") if rb > ra else ("T", "B")
        return [("port", p["parent"], sa), ("straight",), ("port", p["child"], sb)]
    plans = [straight_plan(p) if k in straight else route_plan(p) for k, p in enumerate(pairs)]


    def solve(vw, hw):
        xs, ys, vx, hy, W, H = place(vw, hw)
        # ports: distribute along each side, sorted by where the edge goes next
        side_users = {}
        for k, pl in enumerate(plans):
            for idx in (0, len(pl) - 1):
                st = pl[idx]
                side_users.setdefault((st[1], st[2]), []).append((k, idx))

        def far_coord(k, idx):
            pl = plans[k]
            other = pl[-1] if idx == 0 else pl[0]
            ox, oy, ow, oh = box(other[1], xs, ys)
            nxt = pl[1] if idx == 0 else pl[-2]
            if nxt[0] == "V":
                return vx[nxt[1]], oy
            return ox + ow / 2, oy + oh / 2

        # a straight line between two tables that overlap along the facing sides is drawn exactly vertical or
        # horizontal: both ends are pinned to one coordinate inside the overlap
        span = {}
        for k, pl in enumerate(plans):
            if pl[1][0] != "straight":
                continue
            (_, a, sa), (_, b, _) = pl[0], pl[-1]
            A, B = box(a, xs, ys), box(b, xs, ys)
            if sa in "TB":
                lo, hi = max(A[0], B[0]) + 14, min(A[0] + A[2], B[0] + B[2]) - 14
            else:
                lo, hi = max(A[1], B[1]) + HEAD_H, min(A[1] + A[3], B[1] + B[3]) - 4
            if lo <= hi:
                span[k] = (lo, hi)

        def side_range(t, side):
            x, y, w, h = box(t, xs, ys)
            return (x, x + w) if side in "TB" else (y + HEAD_H, y + h - 4)

        def key(u, fixed):
            if u in fixed:
                return fixed[u]
            if u[0] in span:
                return sum(span[u[0]]) / 2
            return far_coord(*u)[0 if plans[u[0]][u[1]][2] in "TB" else 1]

        # pin each line where an even spread of its two sides would put it, kept inside the overlap
        even = {}
        for (t, side), users in side_users.items():
            lo, hi = side_range(t, side)
            users.sort(key=lambda u: key(u, {}))
            for i, u in enumerate(users):
                even[u] = lo + (hi - lo) * (i + 1) / (len(users) + 1)
        fixed = {}
        for k, (lo, hi) in span.items():
            last = len(plans[k]) - 1
            fixed[(k, 0)] = fixed[(k, last)] = min(hi, max(lo, (even[(k, 0)] + even[(k, last)]) / 2))

        # ports: every line ending on a side, pinned or not, shares one spread along it, sorted by where the
        # line goes next; free ports are spaced evenly between the pinned ones so no two ends meet
        port = {}
        for (t, side), users in side_users.items():
            x, y, w, h = box(t, xs, ys)
            tb = side in "TB"
            lo, hi = side_range(t, side)
            users.sort(key=lambda u: key(u, fixed))
            coord, prev, run = {}, lo, []
            for u in users + [None]:
                if u is not None and u not in fixed:
                    run.append(u); continue
                nxt = hi if u is None else fixed[u]
                for i, f in enumerate(run):
                    coord[f] = prev + (nxt - prev) * (i + 1) / (len(run) + 1)
                run = []
                if u is not None:
                    coord[u] = prev = fixed[u]
            for u, c in coord.items():
                if tb:
                    port[u] = (c, y if side == "T" else y + h, (c - x) / w, 0 if side == "T" else 1)
                else:
                    port[u] = (x if side == "L" else x + w, c, 0 if side == "L" else 1, (c - y) / h)
        # channel segments -> tracks (greedy interval colouring)
        segs = {}
        for k, pl in enumerate(plans):
            p0, p1 = port[(k, 0)], port[(k, len(pl) - 1)]
            for i, st in enumerate(pl[1:-1], 1):
                if st[0] in ("loop", "straight"):
                    continue
                # interval along the channel = span between neighbouring coordinates
                prev = p0 if i == 1 else None
                nxt = p1 if i == len(pl) - 2 else None
                if st[0] == "H":
                    a = p0[0] if prev else vx[pl[i - 1][1]]
                    b = p1[0] if nxt else vx[pl[i + 1][1]]
                else:
                    a = p0[1] if prev else hy[pl[i - 1][1]]
                    b = p1[1] if nxt else hy[pl[i + 1][1]]
                # which way each end leaves the channel (-1 up/left, +1 down/right)
                n_last = len(pl) - 1
                def far(j_step, port_pt):
                    if j_step <= 0:
                        return port_pt
                    if j_step >= n_last:
                        return port_pt
                    o = pl[j_step]
                    return (vx[o[1]] + vw[o[1]] / 2, hy[o[1]] + hw[o[1]] / 2) if o[0] == "V" else                         (vx[0], hy[o[1]] + hw[o[1]] / 2) if o[0] == "H" else port_pt
                fa = p0 if i == 1 else far(i - 2, p0)
                fb = p1 if i == n_last - 1 else far(i + 2, p1)
                if st[0] == "H":
                    cc0 = hy[st[1]] + hw[st[1]] / 2
                    da, db = (1 if fa[1] > cc0 else -1), (1 if fb[1] > cc0 else -1)
                else:
                    cc0 = vx[st[1]] + vw[st[1]] / 2
                    da, db = (1 if fa[0] > cc0 else -1), (1 if fb[0] > cc0 else -1)
                segs.setdefault(st, []).append((min(a, b) - 6, max(a, b) + 6, k, i, ((a, da), (b, db))))
        track = {}
        ntr = {}
        for ch, lst in segs.items():
            lst.sort()
            ends = []
            for lo, hi, k, i, _ in lst:
                for t_i, e in enumerate(ends):
                    if e < lo:
                        ends[t_i] = hi; track[(k, i)] = t_i; break
                else:
                    ends.append(hi); track[(k, i)] = len(ends) - 1
            ntr[ch] = len(ends)
            # order the tracks so lines leaving upwards sit above those leaving downwards
            n = len(ends)
            if n > 1:
                items = [(lo, hi, track[(k, i)], e) for lo, hi, k, i, e in lst]

                def cost(posn):
                    c = 0
                    for lo, hi, t, e in items:
                        for x, d in e:
                            for lo2, hi2, t2, _ in items:
                                if t2 != t and lo2 + 6 < x < hi2 - 6 and (posn[t2] - posn[t]) * d > 0:
                                    c += 1
                    return c
                posn = list(range(n))
                best_c = cost(posn)
                improved = True
                while improved:
                    improved = False
                    for t1 in range(n):
                        for t2 in range(t1 + 1, n):
                            posn[t1], posn[t2] = posn[t2], posn[t1]
                            c = cost(posn)
                            if c < best_c:
                                best_c, improved = c, True
                            else:
                                posn[t1], posn[t2] = posn[t2], posn[t1]
                for lo, hi, k, i, _ in lst:
                    track[(k, i)] = posn[track[(k, i)]]
        return xs, ys, vx, hy, W, H, port, track, ntr


    vw = [MARGIN * 2] * (NC + 1)
    hw = [MARGIN * 2] * (NR + 1)
    for _ in range(3):
        xs, ys, vx, hy, W, H, port, track, ntr = solve(vw, hw)
        vw = [max(40, 2 * MARGIN + SP * (ntr.get(("V", i), 1) - 1)) for i in range(NC + 1)]
        hw = [max(40, 2 * MARGIN + SP * (ntr.get(("H", j), 1) - 1)) for j in range(NR + 1)]
        vw[0], vw[NC] = max(vw[0], 30), max(vw[NC], 30)
    xs, ys, vx, hy, W, H, port, track, ntr = solve(vw, hw)


    def chan_coord(st, k, i):
        t = track[(k, i)]
        n = ntr[st]
        if st[0] == "H":
            size = hw[st[1]]
            return hy[st[1]] + (size - SP * (n - 1)) / 2 + SP * t
        size = vw[st[1]]
        return vx[st[1]] + (size - SP * (n - 1)) / 2 + SP * t


    lines = []
    for k, (p, pl) in enumerate(zip(pairs, plans)):
        p0, p1 = port[(k, 0)], port[(k, len(pl) - 1)]
        if pl[1][0] == "straight":
            lines.append((p0, p1, []))
            continue
        pts = []
        cur = (p0[0], p0[1])
        for i, st in enumerate(pl[1:-1], 1):
            if st[0] == "loop":
                lx = max(p0[0], p1[0]) + 24
                pts += [(lx, p0[1]), (lx, p1[1])]
                continue
            cc = chan_coord(st, k, i)
            if st[0] == "H":
                pts.append((cur[0], cc)); cur = (cur[0], cc)
                nxt_x = p1[0] if i == len(pl) - 2 else chan_coord(pl[i + 1], k, i + 1)
                pts.append((nxt_x, cc)); cur = (nxt_x, cc)
            else:
                pts.append((cc, cur[1])); cur = (cc, cur[1])
                nxt_y = p1[1] if i == len(pl) - 2 else chan_coord(pl[i + 1], k, i + 1)
                pts.append((cc, nxt_y)); cur = (cc, nxt_y)
        # drop duplicate consecutive points
        clean = []
        for q in pts:
            if not clean or (abs(clean[-1][0] - q[0]) > 0.5 or abs(clean[-1][1] - q[1]) > 0.5):
                clean.append(q)
        lines.append((p0, p1, clean))
    return lines


def build(grid):
    """Lay the grid out. A pair whose tables sit in neighbouring cells (diagonals included) is joined by one
    straight line, and so is a pair two cells apart when that line is exactly vertical or horizontal; never when
    the line would cross another table. Every other pair runs through the channels."""
    pg = {t: (r, c) for r, row in enumerate(grid) for c, t in enumerate(row) if t}
    straight = {k for k, p in enumerate(pairs) if p["parent"] != p["child"]
                and max(abs(pg[p["parent"]][0] - pg[p["child"]][0]), abs(pg[p["parent"]][1] - pg[p["child"]][1])) <= 2}
    while True:
        lines = _build(grid, straight)
        blocked = set()
        for k in straight:
            (x0, y0, _, _), (x1, y1, _, _), _ = lines[k]
            a, b = pg[pairs[k]["parent"]], pg[pairs[k]["child"]]
            if max(abs(a[0] - b[0]), abs(a[1] - b[1])) == 2 and abs(x0 - x1) > 0.5 and abs(y0 - y1) > 0.5:
                blocked.add(k); continue
            for t in pos:
                if t in (pairs[k]["parent"], pairs[k]["child"]):
                    continue
                bx, by, bw, bh = xs[pos[t][1]], ys[pos[t][0]], tables[t]["w"], tables[t]["h"]
                if any(bx - 4 < x0 + (x1 - x0) * s / 40 < bx + bw + 4 and by - 4 < y0 + (y1 - y0) * s / 40 < by + bh + 4
                       for s in range(41)):
                    blocked.add(k); break
        if not blocked:
            return lines
        straight -= blocked


def segments(lines):
    out = []
    for k, (p0, p1, pts) in enumerate(lines):
        q = [(p0[0], p0[1])] + pts + [(p1[0], p1[1])]
        for a, b in zip(q, q[1:]):
            out.append((k, a, b))
    return out


def crossings(lines):
    segs = segments(lines)
    n = sum(abs(a[0] - b[0]) + abs(a[1] - b[1]) for _, a, b in segs) / 1500
    hs = [(k, min(a[0], b[0]), max(a[0], b[0]), a[1]) for k, a, b in segs if abs(a[1] - b[1]) < 0.5]
    vs = [(k, min(a[1], b[1]), max(a[1], b[1]), a[0]) for k, a, b in segs if abs(a[0] - b[0]) < 0.5]
    for k1, x1, x2, y in hs:
        for k2, y1, y2, x in vs:
            if k1 != k2 and x1 + 1 < x < x2 - 1 and y1 + 1 < y < y2 - 1:
                n += 1
    # straight diagonal lines against every other segment
    cr = lambda o, a, b: (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    diag = [s for s in segs if abs(s[1][0] - s[2][0]) >= 0.5 and abs(s[1][1] - s[2][1]) >= 0.5]
    for i, (k1, a, b) in enumerate(diag):
        for k2, c, d in segs:
            if k1 == k2 or ((k2, c, d) in diag[:i + 1]):
                continue
            if cr(a, b, c) * cr(a, b, d) < -1 and cr(c, d, a) * cr(c, d, b) < -1:
                n += 1
    return n


random.seed(7)
best_grid = [row[:] for row in GRID]
best = crossings(build(best_grid))
cur_grid, cur = [row[:] for row in best_grid], best
ITER = 15000 if "--optimize" in sys.argv else 0
for it in range(ITER):
    g = [row[:] for row in cur_grid]
    r1, c1 = random.randrange(len(g)), random.randrange(len(g[0]))
    r2, c2 = random.randrange(len(g)), random.randrange(len(g[0]))
    if (r1, c1) == (r2, c2) or (g[r1][c1] is None and g[r2][c2] is None):
        continue
    g[r1][c1], g[r2][c2] = g[r2][c2], g[r1][c1]
    sc = crossings(build(g))
    temp = max(0.01, 2.0 * (1 - it / ITER))
    if sc <= cur or random.random() < pow(2.718, (cur - sc) / temp):
        cur_grid, cur = g, sc
        if sc < best:
            best_grid, best = [row[:] for row in g], sc
best_grid = [row for row in best_grid if any(row)]
keep = [c for c in range(len(best_grid[0])) if any(row[c] for row in best_grid)]
best_grid = [[row[c] for c in keep] for row in best_grid]
lines = build(best_grid)
if ITER:
    print("crossing score", round(best, 1), "grid:", json.dumps(best_grid))


def box(t, xs, ys):
    r, c = pos[t]
    return xs[c], ys[r], tables[t]["w"], tables[t]["h"]
# ------------------------------------------------------------------ XML
E = lambda s: html.escape(s, quote=True)
TABLE = ("swimlane;fontStyle=1;childLayout=stackLayout;horizontal=1;startSize=28;horizontalStack=0;resizeParent=1;"
         "resizeParentMax=0;resizeLast=0;collapsible=0;marginBottom=0;html=1;fillColor=#ffffff;strokeColor=#000000;"
         "fontSize=13;swimlaneFillColor=#ffffff;")
ROW = ("text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;spacingLeft=6;spacingRight=6;"
       "overflow=hidden;fontSize=11;")
EDGE = ("edgeStyle={es};rounded=0;html=1;endArrow={end};startArrow={start};endFill=0;startFill=0;"
        "endSize=12;startSize=12;strokeColor=#000000;fontSize=10;jumpStyle=arc;jumpSize=6;labelBackgroundColor=#ffffff;"
        "exitX={ex:.4f};exitY={ey:.4f};exitDx=0;exitDy=0;entryX={nx:.4f};entryY={ny:.4f};entryDx=0;entryDy=0;")
out = []
for t, (r, c) in pos.items():
    T = tables[t]
    x, y, w, h = box(t, xs, ys)
    out.append('<mxCell id="{t}" parent="1" style="{s}" value="{t}" vertex="1"><mxGeometry height="{h}" width="{w}" '
               'x="{x:.0f}" y="{y:.0f}" as="geometry"/></mxCell>'.format(t=t, s=TABLE, h=h, w=w, x=x, y=y))
    for i, (k, n, ty) in enumerate(T["rows"]):
        v = ('<table style="width:{tw}px;table-layout:fixed;border-collapse:collapse;font-size:11px;white-space:nowrap">'
             '<tr><td style="width:{kw}px;padding:0">{k}</td><td style="width:{nw}px;padding:0">{n}</td>'
             '<td style="width:{yw}px;padding:0;text-align:left;color:#000000">{ty}</td></tr></table>').format(
            tw=w - 12, kw=T["kw"], nw=T["nw"], yw=w - 12 - T["kw"] - T["nw"], k=("<b>%s</b>" % k) if k else "",
            n=E(n), ty=E(ty))
        out.append('<mxCell id="{t}_{i}" parent="{t}" style="{s}" value="{v}" vertex="1"><mxGeometry height="{rh}" '
                   'width="{w}" y="{y}" as="geometry"/></mxCell>'.format(t=t, i=i, s=ROW, v=E(v), rh=ROW_H, w=w,
                                                                       y=HEAD_H + i * ROW_H))

for k, (p, pl, (p0, p1, clean)) in enumerate(zip(pairs, plans, lines)):
    eid = old_ids.get((p["parent"], p["child"])) or "r_{}_{}".format(p["parent"], p["child"])
    style = EDGE.format(es="none" if pl[1][0] == "straight" else "orthogonalEdgeStyle", end="ERzeroToOne" if p["one"] else "ERzeroToMany",
                        start="ERmandOne" if p["required"] else "ERzeroToOne",
                        ex=p0[2], ey=p0[3], nx=p1[2], ny=p1[3])
    pts_xml = "".join('<mxPoint x="{:.1f}" y="{:.1f}"/>'.format(*q) for q in clean)
    out.append('<mxCell id="{id}" edge="1" parent="1" source="{s}" target="{t}" value="" style="{st}">'
               '<mxGeometry relative="1" as="geometry"><Array as="points">{pts}</Array></mxGeometry></mxCell>'.format(
                   id=eid, s=p["parent"], t=p["child"], st=style, pts=pts_xml))

xml = ('<mxfile host="app.diagrams.net">\n  <diagram name="ERD" id="0">\n    <mxGraphModel dx="1393" dy="1248" '
       'grid="0" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="0" pageScale="1" '
       'pageWidth="850" pageHeight="1100" math="0" shadow="0">\n      <root>\n        <mxCell id="0"/>\n'
       '        <mxCell id="1" parent="0"/>\n')
xml += "".join("        " + o + "\n" for o in out)
xml += "      </root>\n    </mxGraphModel>\n  </diagram>\n</mxfile>\n"
open(OUT, "w", encoding="utf-8", newline="\n").write(xml)
print("wrote {} ({} tables, {} lines)".format(os.path.relpath(OUT, ROOT), len(pos), len(pairs)))
