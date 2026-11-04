"""Draw the table-form ERD (docs/13-erd/muc-tieu) from the EF Core model snapshot.

Run from the repository root:
    python scripts/draw_erd.py              # redraw with the fixed grid below
    python scripts/draw_erd.py --optimize   # search a grid with fewer line crossings, print it
then export the image with draw.io (see docs/13-erd/muc-tieu/README.md).

Tables come from backend/TrustCabinet.Infrastructure/Migrations/TrustCabinetDbContextModelSnapshot.cs, column
order and enum types from backend/TrustCabinet.Domain. Tables sit on a grid; every line runs through the
channels between grid cells, so no line crosses a table. One line per related table pair. Lines to User are
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
    [None, "TranscriptSpeaker", "MeetingNote", None, "AgendaItem", "MeetingDocumentAgendaItem", None, None, "GuestVerificationChallenge", "MeetingGuestInvitation"],
    ["TranscriptSegment", "Transcript", "SpeakingRequest", "EmailMessage", "MeetingInvitationDispatch", None, None, "DocumentComment", "VoteChoice", None],
    [None, None, "MeetingSummary", "Meeting", "MeetingParticipant", "StoredFile", "MeetingDocument", "VotingOption", "VotingSession", None],
    ["MeetingMinutes", "Recording", None, "Notification", "Folder", "Document", "DocumentReadConfirmation", "VoteDocument", "VoteEligibleVoter", None],
    [None, "CallSession", "MeetingStatusHistory", "AuditLogEntry", "User", "VoiceSample", "VoiceConsent", None, "VoteReceipt", None],
    ["MinutesSignature", None, None, "CallParticipant", "Role", "RoleCapability", None, None, "SpeechClip", "GuestSession"],
]
pos = {t: (r, c) for r, row in enumerate(GRID) for c, t in enumerate(row) if t}
assert sorted(pos) == sorted(m), set(m) ^ set(pos)
NR, NC = len(GRID), len(GRID[0])

ROW_H, HEAD_H = 19, 28
MARGIN, SP = 26, 9          # channel margin (room for the crow's foot) and track spacing


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

def build(grid):
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
    PORT_GAP = 22
    SIDE = set()
    for hub in HUBS:
        hr, hc = pos[hub]
        cap = {"side": 2 * int((tables[hub]["h"] - HEAD_H) / PORT_GAP), "vert": 2 * int(tables[hub]["w"] / PORT_GAP)}
        kids = [p["child"] for p in pairs if p["parent"] == hub and p["child"] != hub]
        adjacent = [k for k in kids if pos[k][0] == hr and abs(pos[k][1] - hc) == 1]
        same_row = [k for k in kids if pos[k][0] == hr and k not in adjacent]
        cap["side"] -= len(adjacent) + len(same_row)
        cap["vert"] -= sum(1 for p in pairs if p["child"] == hub)
        rest = sorted((k for k in kids if pos[k][0] != hr and pos[k][1] != hc),
                      key=lambda k: -abs(pos[k][1] - hc))
        for k in rest:
            if cap["side"] > 0 and (cap["vert"] <= 0 or abs(pos[k][1] - hc) >= 2 or len(rest) - rest.index(k) > cap["vert"]):
                SIDE.add((hub, k)); cap["side"] -= 1
            else:
                cap["vert"] -= 1
    plans = [route_plan(p) for p in pairs]


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

        port = {}
        for (t, side), users in side_users.items():
            x, y, w, h = box(t, xs, ys)
            if side in "TB":
                users.sort(key=lambda u: far_coord(*u)[0])
                n = len(users)
                for i, u in enumerate(users):
                    fx = (i + 1) / (n + 1)
                    port[u] = (x + w * fx, y if side == "T" else y + h, fx, 0 if side == "T" else 1)
            else:
                users.sort(key=lambda u: far_coord(*u)[1])
                n = len(users)
                lo, hi = HEAD_H, h - 4
                for i, u in enumerate(users):
                    py = y + lo + (hi - lo) * (i + 1) / (n + 1)
                    port[u] = (x if side == "L" else x + w, py, 0 if side == "L" else 1, (py - y) / h)
        # channel segments -> tracks (greedy interval colouring)
        segs = {}
        for k, pl in enumerate(plans):
            p0, p1 = port[(k, 0)], port[(k, len(pl) - 1)]
            for i, st in enumerate(pl[1:-1], 1):
                if st[0] == "loop":
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

def segments(lines):
    out = []
    for k, (p0, p1, pts) in enumerate(lines):
        q = [(p0[0], p0[1])] + pts + [(p1[0], p1[1])]
        for a, b in zip(q, q[1:]):
            out.append((k, a, b))
    return out


def crossings(lines):
    segs = segments(lines)
    hs = [(k, min(a[0], b[0]), max(a[0], b[0]), a[1]) for k, a, b in segs if abs(a[1] - b[1]) < 0.5]
    vs = [(k, min(a[1], b[1]), max(a[1], b[1]), a[0]) for k, a, b in segs if abs(a[0] - b[0]) < 0.5]
    n = sum(x2 - x1 for _, x1, x2, _ in hs) / 1500 + sum(y2 - y1 for _, y1, y2, _ in vs) / 1500
    for k1, x1, x2, y in hs:
        for k2, y1, y2, x in vs:
            if k1 != k2 and x1 + 1 < x < x2 - 1 and y1 + 1 < y < y2 - 1:
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
EDGE = ("edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;endArrow={end};startArrow={start};endFill=0;startFill=0;"
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
    style = EDGE.format(end="ERzeroToOne" if p["one"] else "ERzeroToMany",
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
