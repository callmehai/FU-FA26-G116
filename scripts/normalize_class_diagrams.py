#!/usr/bin/env python3
"""Normalise draw.io class diagrams in docs/07-classdiagram and route their edges.

Two steps, both idempotent:

1. Boxes. A class drawn in the old layout (a title, one divider inset by the cell padding, a body)
   becomes the layout used by FE-01..FE-04: header band, full-width dividers, an attribute and a
   method compartment (empty ones stay as a thin strip), Helvetica 12. Width fits the longest line,
   height follows the line count, so the box never overflows and never has dead space.
2. Edges. Every edge is routed again on a grid that treats every box as an obstacle: no line passes
   through a box or its text, ports on one side are spread out, and parallel lines are kept apart.

Usage:
    python scripts/normalize_class_diagrams.py docs/07-classdiagram/feature-07-*.drawio ...
    python scripts/normalize_class_diagrams.py --no-route <files>     # boxes only
    python scripts/normalize_class_diagrams.py --no-boxes <files>     # edges only
    python scripts/normalize_class_diagrams.py --relayout <files>     # also place every class again

--relayout drops the manual layout: one band per layer (boundary, control, service/policy/utility,
repository, entity/enumeration), classes in a band ordered to cut crossings, each under its neighbours.

Files already in the new layout keep their boxes. The script rewrites the file in place; the PNG
has to be exported again (see docs/07-classdiagram/README.md).
"""
from __future__ import annotations

import argparse
import heapq
import html
import copy
import math
import random
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

try:
    from PIL import ImageFont
except ImportError:  # pragma: no cover
    ImageFont = None

BOX_STYLE = (
    "rounded=0;whiteSpace=wrap;html=1;align=left;verticalAlign=top;spacing=0;overflow=fill;"
    "fillColor=#ffffff;strokeColor=#000000;fontSize=12;fontFamily=Helvetica;fontColor=#000000;"
)
HR = '<hr style="margin:0;border:none;border-top:1px solid #000000;height:0;">'
LINE_H = 15
HEADER_H = 40
PAD_H = 10  # vertical padding of one compartment
GRID = 10
CLEAR = 10  # minimum distance between a line and a box
STUB = 20

_FONT = {}


def _font(bold: bool):
    if ImageFont is None:
        return None
    if bold not in _FONT:
        for name in (("arialbd.ttf" if bold else "arial.ttf"), ("Arial Bold.ttf" if bold else "Arial.ttf")):
            try:
                _FONT[bold] = ImageFont.truetype(name, 12)
                break
            except OSError:
                continue
        else:
            _FONT[bold] = None
    return _FONT[bold]


def text_width(text: str, bold: bool = False) -> float:
    plain = html.unescape(re.sub(r"<[^>]+>", "", text))
    f = _font(bold)
    if f is None:
        return len(plain) * (6.6 if bold else 6.2)
    return f.getlength(plain)


# --------------------------------------------------------------------------------------- boxes
def is_old_box(value: str) -> bool:
    return "<hr" in value and "border:none" not in value


CONT = "<br>&nbsp;&nbsp;&nbsp;&nbsp;"


def join_wrapped(lines: list[str]) -> list[str]:
    """A line that does not start with a visibility marker continues the one before it."""
    out: list[str] = []
    for l in lines:
        if out and not re.match(r"\s*[+\-#~]\s", l):
            out[-1] += CONT + l.strip()
        else:
            out.append(l.strip())
    return out


def parse_old(value: str):
    """Return (stereotype, name, attrs, methods, single) from an old-layout value."""
    parts = re.split(r"<hr[^>]*>", value)
    head = parts[0]
    m = re.search(r"&lt;&lt;(.*?)&gt;&gt;(?:<br>)?\s*(?:<b>)?(.*?)(?:</b>)?\s*(?:</div>)?$", head, re.S)
    if not m:
        return None
    stereo, name = m.group(1).strip(), re.sub(r"<[^>]+>", "", m.group(2)).strip()

    def lines(part: str):
        inner = re.sub(r"^\s*<div[^>]*>", "", part.strip())
        inner = re.sub(r"</div>\s*$", "", inner)
        inner = inner.strip()
        if not inner:
            return []
        return [x for x in re.split(r"<br\s*/?>", inner) if x.strip()]

    body = [join_wrapped(lines(p)) for p in parts[1:]] if stereo != "enumeration" else [lines(p) for p in parts[1:]]
    if stereo == "enumeration":
        return stereo, name, [], sum(body, []), True
    if len(body) >= 2:
        return stereo, name, body[0], sum(body[1:], []), False
    only = body[0] if body else []
    attrs = [l for l in only if "(" not in l]
    methods = [l for l in only if "(" in l]
    if stereo in ("entity", "value object"):
        return stereo, name, only, [], False
    if not methods:  # nothing callable: treat as members
        return stereo, name, only, [], False
    return stereo, name, attrs, methods, False


def nlines(lines: list[str]) -> int:
    return sum(l.count(CONT) + 1 for l in lines)


def compartment(lines: list[str]) -> str:
    return (
        '<div style="text-align:left;padding:5px 8px 5px 8px;white-space:nowrap;">'
        + "<br>".join(lines)
        + "</div>"
    )


def build_box(stereo: str, name: str, attrs: list[str], methods: list[str], single: bool):
    head = (
        '<div style="text-align:center;font-weight:bold;padding:5px 4px 4px 4px;height:31px;">'
        f"«{stereo}»<br>{name}</div>"
    )
    if single:
        value = head + HR + compartment(methods)
        height = HEADER_H + 1 + PAD_H + LINE_H * nlines(methods)
        lines = methods
    else:
        value = head + HR + compartment(attrs) + HR + compartment(methods)
        height = HEADER_H + 2 + (PAD_H + LINE_H * nlines(attrs)) + (PAD_H + LINE_H * nlines(methods))
        lines = attrs + methods
    width = max(
        max((text_width(x) for l in lines for x in l.split(CONT)), default=0) + 20,
        max(text_width(f"«{stereo}»"), text_width(name, True)) + 24,
        110,
    )
    return value, int(math.ceil(width / 10.0) * 10), height


def normalise_boxes(root: ET.Element) -> int:
    changed = 0
    for c in root.iter("mxCell"):
        if c.get("vertex") != "1":
            continue
        value = c.get("value") or ""
        if not is_old_box(value):
            continue
        parsed = parse_old(value)
        if parsed is None:
            print(f"  ! not parsed: {c.get('id')}", file=sys.stderr)
            continue
        new_value, w, h = build_box(*parsed)
        g = c.find("mxGeometry")
        c.set("value", new_value)
        c.set("style", BOX_STYLE)
        g.set("width", str(w))
        g.set("height", str(h))
        changed += 1
    return changed


# --------------------------------------------------------------------------------------- routing
SIDES = {"top": (0, -1), "bottom": (0, 1), "left": (-1, 0), "right": (1, 0)}


def port(box, side, frac):
    x, y, w, h = box
    if side == "top":
        return (x + frac * w, y)
    if side == "bottom":
        return (x + frac * w, y + h)
    if side == "left":
        return (x, y + frac * h)
    return (x + w, y + frac * h)


def stub(p, side):
    dx, dy = SIDES[side]
    return (p[0] + dx * STUB, p[1] + dy * STUB)


class Router:
    def __init__(self, boxes: dict[str, tuple], title_box=None):
        self.boxes = boxes
        self.obst = list(boxes.values()) + ([title_box] if title_box else [])
        xs = [b[0] for b in self.obst] + [b[0] + b[2] for b in self.obst]
        ys = [b[1] for b in self.obst] + [b[1] + b[3] for b in self.obst]
        self.x0, self.x1 = min(xs) - 60, max(xs) + 60
        self.y0, self.y1 = min(ys) - 60, max(ys) + 60
        self.occ_h: dict[tuple, int] = {}
        self.occ_v: dict[tuple, int] = {}

    # grid built per phase so ports/stubs sit exactly on grid lines
    def build_grid(self, extra_x, extra_y):
        xs = set(range(int(self.x0 // GRID * GRID), int(self.x1) + GRID, GRID))
        ys = set(range(int(self.y0 // GRID * GRID), int(self.y1) + GRID, GRID))
        xs.update(round(v, 3) for v in extra_x)
        ys.update(round(v, 3) for v in extra_y)
        self.X = sorted(xs)
        self.Y = sorted(ys)
        self.xi = {v: i for i, v in enumerate(self.X)}
        self.yi = {v: i for i, v in enumerate(self.Y)}
        nx, ny = len(self.X), len(self.Y)
        blocked = bytearray(nx * ny)
        import bisect

        for (bx, by, bw, bh) in self.obst:
            i0 = bisect.bisect_right(self.X, bx - CLEAR)
            i1 = bisect.bisect_left(self.X, bx + bw + CLEAR)
            j0 = bisect.bisect_right(self.Y, by - CLEAR)
            j1 = bisect.bisect_left(self.Y, by + bh + CLEAR)
            for i in range(i0, i1):
                base = i * ny
                for j in range(j0, j1):
                    blocked[base + j] = 1
        self.blocked = blocked
        self.nx, self.ny = nx, ny
        self.occ_h.clear()
        self.occ_v.clear()

    def node(self, p):
        return self.xi[round(p[0], 3)], self.yi[round(p[1], 3)]

    def route(self, start, start_side, end, end_side, limit=None):
        """A* from start stub node to end stub node. Returns (cost, [(x,y)...]) or None."""
        s = self.node(start)
        e = self.node(end)
        ny = self.ny
        X, Y = self.X, self.Y
        blocked = self.blocked
        occ_h, occ_v = self.occ_h, self.occ_v
        dirs = ((1, 0), (-1, 0), (0, 1), (0, -1))
        sd = {"right": 0, "left": 1, "bottom": 2, "top": 3}
        s_dir = sd[start_side]
        # arrival must be moving *against* the outward normal of the end side
        e_normal = {"right": 1, "left": 0, "bottom": 3, "top": 2}[end_side]
        BEND = 9.0
        ex, ey = X[e[0]], Y[e[1]]

        def h(i, j, d):
            return (abs(X[i] - ex) + abs(Y[j] - ey)) / GRID

        start_state = (s[0], s[1], s_dir)
        best = {start_state: 0.0}
        prev = {}
        heap = [(h(s[0], s[1], s_dir), 0.0, start_state)]
        while heap:
            f, g, st = heapq.heappop(heap)
            if best.get(st, 1e18) < g - 1e-9:
                continue
            i, j, d = st
            if (i, j) == e and d == e_normal:
                path = [(X[i], Y[j])]
                cur = st
                while cur in prev:
                    cur = prev[cur]
                    path.append((X[cur[0]], Y[cur[1]]))
                path.reverse()
                return g, path
            if limit is not None and f > limit:
                return None
            for nd, (dx, dy) in enumerate(dirs):
                if nd == (d ^ 1) and (i, j) != s:  # no u-turn
                    continue
                ni, nj = i + dx, j + dy
                if not (0 <= ni < self.nx and 0 <= nj < ny):
                    continue
                if blocked[ni * ny + nj] and (ni, nj) != e:
                    continue
                step = (abs(X[ni] - X[i]) + abs(Y[nj] - Y[j])) / GRID
                cost = step
                if nd != d:
                    cost += BEND
                key = (ni, nj)
                if dx:
                    if occ_h.get(key):
                        cost += 30 * step
                    if occ_v.get(key):
                        cost += 6
                else:
                    if occ_v.get(key):
                        cost += 30 * step
                    if occ_h.get(key):
                        cost += 6
                ng = g + cost
                nst = (ni, nj, nd)
                if ng < best.get(nst, 1e18):
                    best[nst] = ng
                    prev[nst] = st
                    heapq.heappush(heap, (ng + h(ni, nj, nd), ng, nst))
        return None

    def mark(self, pts):
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            ai, aj = self.xi[round(ax, 3)], self.yi[round(ay, 3)]
            bi, bj = self.xi[round(bx, 3)], self.yi[round(by, 3)]
            if ai == bi:
                for j in range(min(aj, bj), max(aj, bj) + 1):
                    self.occ_v[(ai, j)] = self.occ_v.get((ai, j), 0) + 1
            else:
                for i in range(min(ai, bi), max(ai, bi) + 1):
                    self.occ_h[(i, aj)] = self.occ_h.get((i, aj), 0) + 1


def simplify(pts, tol: float = 0.5):
    def same(u, v):
        return abs(u - v) <= tol

    out = [pts[0]]
    for p in pts[1:]:
        if not (same(p[0], out[-1][0]) and same(p[1], out[-1][1])):
            out.append(p)
    res = [out[0]]
    for a, b, c in zip(out, out[1:], out[2:]):
        if (same(a[0], b[0]) and same(b[0], c[0])) or (same(a[1], b[1]) and same(b[1], c[1])):
            continue
        res.append(b)
    res.append(out[-1])
    return res


def set_style(style: str, **kv) -> str:
    items = [p for p in style.split(";") if p]
    keep = [p for p in items if p.split("=")[0] not in kv]
    for k, v in kv.items():
        keep.append(f"{k}={v}")
    return ";".join(keep) + ";"


OPPOSITE = {"top": "bottom", "bottom": "top", "left": "right", "right": "left"}


def straighten(edges, choice, fracs, slots, boxes, margin: int = 12, gap: int = 16) -> None:
    """Two boxes that face each other and overlap on the shared axis get one shared port coordinate,
    so the line between them runs straight instead of jogging; other ports stay `gap` px away."""
    def coord(bid, side, f):
        x, y, w, h = boxes[bid]
        return x + f * w if side in ("top", "bottom") else y + f * h

    def frac(bid, side, v):
        x, y, w, h = boxes[bid]
        return (v - x) / w if side in ("top", "bottom") else (v - y) / h

    for c in edges:
        cid = c.get("id")
        if cid not in choice:
            continue
        ss, ts = choice[cid]
        if OPPOSITE[ss] != ts:
            continue
        s, t = c.get("source"), c.get("target")
        (sx, sy, sw, sh), (tx, ty, tw, th) = boxes[s], boxes[t]
        if ss in ("top", "bottom"):
            lo, hi = max(sx, tx) + margin, min(sx + sw, tx + tw) - margin
        else:
            lo, hi = max(sy, ty) + margin, min(sy + sh, ty + th) - margin
        if lo > hi:
            continue
        want = (coord(s, ss, fracs[(cid, "s")]) + coord(t, ts, fracs[(cid, "t")])) / 2
        others = [coord(bid, side, fracs[(o.get("id"), end)])
                  for bid, side in ((s, ss), (t, ts)) for o, end in slots[(bid, side)] if o is not c]
        grid = range(int(math.ceil(lo / GRID)) * GRID, int(hi) + 1, GRID)
        for v in sorted(grid, key=lambda v: abs(v - want)) or [min(max(want, lo), hi)]:
            if all(abs(v - o) >= gap for o in others):
                fracs[(cid, "s")], fracs[(cid, "t")] = frac(s, ss, v), frac(t, ts, v)
                break


def route_edges(root: ET.Element, title_cell=None) -> tuple[int, int]:
    boxes = {}
    for c in root.iter("mxCell"):
        if c.get("vertex") == "1" and c.get("parent") == "1":
            g = c.find("mxGeometry")
            if g is None or g.get("x") is None:
                continue
            if c.get("id") == "title":
                continue
            boxes[c.get("id")] = (float(g.get("x")), float(g.get("y")), float(g.get("width")), float(g.get("height")))
    tb = None
    if title_cell is not None:
        g = title_cell.find("mxGeometry")
        tb = (float(g.get("x")), float(g.get("y")), float(g.get("width")), float(g.get("height")))
    edges = []
    for c in root.iter("mxCell"):
        if c.get("edge") == "1" and c.get("source") in boxes and c.get("target") in boxes:
            if c.get("source") == c.get("target"):  # self association: a loop on the right side
                bx, by, bw, bh = boxes[c.get("source")]
                c.set("style", set_style(c.get("style") or "", rounded="0", exitX="1", exitY="0.3", exitDx="0", exitDy="0", entryX="1", entryY="0.7", entryDx="0", entryDy="0"))
                g = c.find("mxGeometry")
                for a in g.findall("Array"):
                    g.remove(a)
                arr = ET.SubElement(g, "Array")
                arr.set("as", "points")
                for yy in (by + bh * 0.3, by + bh * 0.7):
                    pt_ = ET.SubElement(arr, "mxPoint")
                    pt_.set("x", f"{bx + bw + 40:g}")
                    pt_.set("y", f"{yy:g}")
                continue
            edges.append(c)
    R = Router(boxes, tb)

    def center(b):
        return (b[0] + b[2] / 2, b[1] + b[3] / 2)

    # ---- phase 1: choose sides with centred ports
    extra_x, extra_y = set(), set()
    for c in edges:
        for bid in (c.get("source"), c.get("target")):
            b = boxes[bid]
            for side in SIDES:
                p = port(b, side, 0.5)
                s = stub(p, side)
                extra_x.update([p[0], s[0]])
                extra_y.update([p[1], s[1]])
    R.build_grid(extra_x, extra_y)
    order = sorted(
        edges,
        key=lambda c: abs(center(boxes[c.get("source")])[0] - center(boxes[c.get("target")])[0])
        + abs(center(boxes[c.get("source")])[1] - center(boxes[c.get("target")])[1]),
    )
    choice = {}
    for c in order:
        sb, tb_ = boxes[c.get("source")], boxes[c.get("target")]
        cands = []
        for ss in SIDES:
            for ts in SIDES:
                ps, pt = port(sb, ss, 0.5), port(tb_, ts, 0.5)
                lb = (abs(ps[0] - pt[0]) + abs(ps[1] - pt[1]) + 2 * STUB) / GRID
                cands.append((lb, ss, ts))
        cands.sort()
        best = None
        for lb, ss, ts in cands:
            if best is not None and lb >= best[0]:
                break
            ps, pt = port(sb, ss, 0.5), port(tb_, ts, 0.5)
            r = R.route(stub(ps, ss), ss, stub(pt, ts), ts, limit=None if best is None else best[0])
            if r is None:
                continue
            cost = r[0] + lb * 0  # path cost already includes bends and overlaps
            if best is None or cost < best[0]:
                best = (cost, ss, ts, r[1])
        if best is None:
            continue
        choice[c.get("id")] = (best[1], best[2])
        R.mark([port(sb, best[1], 0.5), stub(port(sb, best[1], 0.5), best[1])] + best[3] + [stub(port(tb_, best[2], 0.5), best[2]), port(tb_, best[2], 0.5)])

    # ---- phase 2: spread ports along each side, route again
    slots: dict[tuple, list] = {}
    for c in edges:
        if c.get("id") not in choice:
            continue
        ss, ts = choice[c.get("id")]
        slots.setdefault((c.get("source"), ss), []).append((c, "s"))
        slots.setdefault((c.get("target"), ts), []).append((c, "t"))
    fracs = {}
    for (bid, side), lst in slots.items():
        b = boxes[bid]

        def other(item):
            c, end = item
            o = boxes[c.get("target") if end == "s" else c.get("source")]
            cx, cy = center(o)
            return cx if side in ("top", "bottom") else cy

        lst.sort(key=other)
        n = len(lst)
        span = b[2] if side in ("top", "bottom") else b[3]
        lo, hi = (0.15, 0.85) if n > 1 else (0.5, 0.5)
        # keep ports at least 16 px apart where the side allows it
        for k, item in enumerate(lst):
            f = 0.5 if n == 1 else lo + (hi - lo) * k / (n - 1)
            fracs[(item[0].get("id"), item[1])] = round(f, 4)
    straighten(edges, choice, fracs, slots, boxes)
    extra_x, extra_y = set(), set()
    for c in edges:
        cid = c.get("id")
        if cid not in choice:
            continue
        ss, ts = choice[cid]
        for bid, side, end in ((c.get("source"), ss, "s"), (c.get("target"), ts, "t")):
            p = port(boxes[bid], side, fracs[(cid, end)])
            s = stub(p, side)
            extra_x.update([p[0], s[0]])
            extra_y.update([p[1], s[1]])
    R.build_grid(extra_x, extra_y)
    routed = failed = 0
    for c in order:
        cid = c.get("id")
        if cid not in choice:
            failed += 1
            continue
        ss, ts = choice[cid]
        sb, tb_ = boxes[c.get("source")], boxes[c.get("target")]
        fs, ft = fracs[(cid, "s")], fracs[(cid, "t")]
        ps, pt = port(sb, ss, fs), port(tb_, ts, ft)
        r = R.route(stub(ps, ss), ss, stub(pt, ts), ts)
        if r is None:
            failed += 1
            continue
        pts = [ps, stub(ps, ss)] + r[1] + [stub(pt, ts), pt]
        R.mark(pts)
        pts = simplify(pts)
        inner = pts[1:-1]
        st = c.get("style") or ""
        dx_s, dy_s = fs if ss in ("top", "bottom") else {"left": 0, "right": 1}[ss], {"top": 0, "bottom": 1}[ss] if ss in ("top", "bottom") else fs
        dx_t, dy_t = ft if ts in ("top", "bottom") else {"left": 0, "right": 1}[ts], {"top": 0, "bottom": 1}[ts] if ts in ("top", "bottom") else ft
        st = set_style(
            st,
            rounded="0",
            endSize="10",
            fontSize="11",
            fontFamily="Helvetica",
            exitX=f"{dx_s:g}",
            exitY=f"{dy_s:g}",
            exitDx="0",
            exitDy="0",
            entryX=f"{dx_t:g}",
            entryY=f"{dy_t:g}",
            entryDx="0",
            entryDy="0",
        )
        c.set("style", st)
        g = c.find("mxGeometry")
        for a in g.findall("Array"):
            g.remove(a)
        arr = ET.SubElement(g, "Array")
        arr.set("as", "points")
        for (x, y) in inner:
            pt_ = ET.SubElement(arr, "mxPoint")
            pt_.set("x", f"{x:g}")
            pt_.set("y", f"{y:g}")
        routed += 1
    return routed, failed


def compact(root: ET.Element, gap: int = 70) -> None:
    """Close empty rows. Boxes are grouped into bands (rows that overlap vertically stay together) and
    each band moves up until it is `gap` px below the band above. Layers and columns stay as drawn."""
    cells = []
    for c in root.iter("mxCell"):
        if c.get("vertex") == "1" and c.get("parent") == "1" and c.get("id") != "title":
            g = c.find("mxGeometry")
            if g is not None and g.get("x") is not None:
                cells.append(g)
    cells.sort(key=lambda g: float(g.get("y")))
    bands: list[list] = []
    bottom = -1.0
    for g in cells:
        y, h = float(g.get("y")), float(g.get("height"))
        if bands and y < bottom:
            bands[-1].append(g)
            bottom = max(bottom, y + h)
        else:
            bands.append([g])
            bottom = y + h
    cursor = 80.0
    for band in bands:
        top = min(float(g.get("y")) for g in band)
        shift = top - cursor
        if shift > 0:
            for g in band:
                g.set("y", f"{float(g.get('y')) - shift:g}")
        cursor = max(float(g.get("y")) + float(g.get("height")) for g in band) + gap


def overlaps(root: ET.Element):
    rects = []
    for c in root.iter("mxCell"):
        if c.get("vertex") == "1" and c.get("parent") == "1" and c.get("id") != "title":
            g = c.find("mxGeometry")
            if g is not None and g.get("x") is not None:
                rects.append((c.get("id"), float(g.get("x")), float(g.get("y")), float(g.get("width")), float(g.get("height"))))
    bad = []
    for i, a in enumerate(rects):
        for b in rects[i + 1:]:
            if a[1] < b[1] + b[3] and b[1] < a[1] + a[3] and a[2] < b[2] + b[4] and b[2] < a[2] + a[4]:
                bad.append((a[0], b[0]))
    return bad


def fit_page(root: ET.Element, model: ET.Element):
    xs, ys = [0], [0]
    for c in root.iter("mxCell"):
        if c.get("vertex") == "1" and c.get("parent") == "1" and c.get("id") != "title":
            g = c.find("mxGeometry")
            xs.append(float(g.get("x")) + float(g.get("width")))
            ys.append(float(g.get("y")) + float(g.get("height")))
        if c.get("edge") == "1":
            for p in c.iter("mxPoint"):
                if p.get("x"):
                    xs.append(float(p.get("x")))
                    ys.append(float(p.get("y")))
    w, h = int(max(xs) + 40), int(max(ys) + 40)
    for k, v in (("dx", w), ("pageWidth", w), ("dy", h), ("pageHeight", h)):
        model.set(k, str(v))
    for c in root.iter("mxCell"):
        if c.get("id") == "title":
            g = c.find("mxGeometry")
            g.set("width", str(int(text_width(html.unescape(c.get("value") or ""), True) * 1.25 + 20)))
    return w, h


# Layer of each stereotype, top to bottom: UI, controllers, services and rules, repositories, entities, value types.
TIER = {"boundary": 0, "control": 1, "service": 2, "policy": 2, "utility": 2, "interface": 2,
        "repository": 3, "entity": 4, "enumeration": 5, "dto": 5, "value": 5}


def _pava(targets: list[float]) -> list[float]:
    """Non-decreasing sequence closest (least squares) to targets."""
    blocks: list[list[float]] = []  # [mean, weight]
    for t in targets:
        blocks.append([t, 1.0])
        while len(blocks) > 1 and blocks[-2][0] > blocks[-1][0]:
            m2, w2 = blocks.pop()
            m1, w1 = blocks.pop()
            blocks.append([(m1 * w1 + m2 * w2) / (w1 + w2), w1 + w2])
    out = []
    for m, w in blocks:
        out += [m] * int(w)
    return out


def _cross(a, b, c, d) -> bool:
    def o(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    return o(a, b, c) * o(a, b, d) < 0 and o(c, d, a) * o(c, d, b) < 0


def _hits_box(a, b, r) -> bool:
    x, y, w, h = r
    t0, t1 = 0.0, 1.0
    dx, dy = b[0] - a[0], b[1] - a[1]
    for p, q in ((-dx, a[0] - x), (dx, x + w - a[0]), (-dy, a[1] - y), (dy, y + h - a[1])):
        if p == 0:
            if q < 0:
                return False
        else:
            t = q / p
            if p < 0:
                t0 = max(t0, t)
            else:
                t1 = min(t1, t)
    return t0 < t1


def relayout(root: ET.Element, aspect: float = 2.2, seed: int | None = None,
             hgap: int = 100, vgap: int = 120, top: int = 80) -> None:
    """Place every class again: one band per layer (TIER), long bands wrapped at about
    sqrt(area * aspect) px, classes in a band ordered to cut crossings and line length, each pulled under its neighbours."""
    geo, tier = {}, {}
    for c in root.iter("mxCell"):
        if c.get("vertex") == "1" and c.get("parent") == "1" and c.get("id") != "title":
            m = re.search(r"«([^»]+)»|<<(.+?)>>", html.unescape(c.get("value") or ""))
            st = (m.group(1) or m.group(2)).split()[0].lower() if m else ""
            geo[c.get("id")] = c.find("mxGeometry")
            tier[c.get("id")] = TIER.get(st, 2)
    W = {i: float(g.get("width")) for i, g in geo.items()}
    H = {i: float(g.get("height")) for i, g in geo.items()}
    edges = sorted({tuple(sorted((c.get("source"), c.get("target")))) for c in root.iter("mxCell")
                    if c.get("edge") == "1" and c.get("source") in geo and c.get("target") in geo
                    and c.get("source") != c.get("target")})
    nbr: dict[str, list[str]] = {i: [] for i in geo}
    for s, t in edges:
        nbr[s].append(t)
        nbr[t].append(s)
    area = sum((W[i] + hgap) * (H[i] + vgap) for i in geo)
    max_w = max(2000.0, math.sqrt(area * aspect))
    start = sorted(geo, key=lambda i: float(geo[i].get("x")))
    if seed is not None:
        random.Random(seed).shuffle(start)
    order = {}
    for i in start:
        order.setdefault(tier[i], []).append(i)

    def place(order):
        rows = []  # a long band is split into k rows of about equal width, order kept
        for t in sorted(order):
            ids = order[t]
            total = sum(W[i] + hgap for i in ids)
            k = max(1, math.ceil(total / max_w))
            band, acc = [[]], 0.0
            for i in ids:
                if len(band) < k and band[-1] and acc + (W[i] + hgap) / 2 > total * len(band) / k:
                    band.append([])
                band[-1].append(i)
                acc += W[i] + hgap
            rows += band
        pos, y = {}, float(top)
        for row in rows:
            x = (max_w - sum(W[i] + hgap for i in row) + hgap) / 2
            for i in row:
                pos[i] = [x, y]
                x += W[i] + hgap
            y += max(H[i] for i in row) + vgap
        for _ in range(4):
            for row in rows:
                off, acc = [], 0.0
                for i in row:
                    off.append(acc)
                    acc += W[i] + hgap
                want = []
                for i, o in zip(row, off):
                    ns = [n for n in nbr[i] if n not in row]
                    cx = (sum(pos[n][0] + W[n] / 2 for n in ns) / len(ns)) if ns else pos[i][0] + W[i] / 2
                    want.append(cx - W[i] / 2 - o)
                for i, o, v in zip(row, off, _pava(want)):
                    pos[i][0] = v + o
        return pos

    def cost(pos):
        seg = [((pos[s][0] + W[s] / 2, pos[s][1] + H[s] / 2), (pos[t][0] + W[t] / 2, pos[t][1] + H[t] / 2), s, t)
               for s, t in edges]
        c = sum(math.dist(a, b) for a, b, _, _ in seg) / 300
        for k, (a, b, s, t) in enumerate(seg):
            for a2, b2, s2, t2 in seg[k + 1:]:
                if {s, t} & {s2, t2}:
                    continue
                if _cross(a, b, a2, b2):
                    c += 1
            for i in geo:
                if i not in (s, t) and _hits_box(a, b, (pos[i][0], pos[i][1], W[i], H[i])):
                    c += 2
        return c

    best = cost(place(order))
    improved = True
    passes = 0
    while improved and passes < 6:
        improved = False
        passes += 1
        for t in sorted(order):
            ids = order[t]
            for i in list(ids):
                for j in range(len(ids)):
                    cur = ids.index(i)
                    if j == cur:
                        continue
                    trial = ids[:cur] + ids[cur + 1:]
                    trial.insert(j, i)
                    order[t] = trial
                    c = cost(place(order))
                    if c < best - 1e-6:
                        best, ids, improved = c, trial, True
                    else:
                        order[t] = ids
                order[t] = ids
    pos = place(order)
    x0 = min(p[0] for p in pos.values())
    for i, (x, y) in pos.items():  # snap the centre, so boxes aligned on one axis get straight lines
        geo[i].set("x", f"{round((x - x0 + 30 + W[i] / 2) / GRID) * GRID - W[i] / 2:g}")
        geo[i].set("y", f"{round(y / GRID) * GRID:g}")


def route_score(root: ET.Element) -> tuple[int, float, int]:
    """(crossing edge pairs, total line length, bends) of the routed edges; crossings are counted the
    way the drawio-skill validator counts them."""
    box = {}
    for c in root.iter("mxCell"):
        if c.get("vertex") == "1" and c.get("parent") == "1":
            g = c.find("mxGeometry")
            box[c.get("id")] = tuple(float(g.get(k)) for k in ("x", "y", "width", "height"))
    paths = []
    for c in root.iter("mxCell"):
        if c.get("edge") != "1" or c.get("source") not in box or c.get("target") not in box or c.get("source") == c.get("target"):
            continue
        st = dict(p.split("=", 1) for p in (c.get("style") or "").split(";") if "=" in p)
        (sx, sy, sw, sh), (tx, ty, tw, th) = box[c.get("source")], box[c.get("target")]
        pts = [(sx + float(st.get("exitX", 0.5)) * sw, sy + float(st.get("exitY", 0.5)) * sh)]
        pts += [(float(p.get("x")), float(p.get("y"))) for p in c.iter("mxPoint") if p.get("x") is not None]
        pts.append((tx + float(st.get("entryX", 0.5)) * tw, ty + float(st.get("entryY", 0.5)) * th))
        paths.append(list(zip(pts, pts[1:])))
    n = 0
    for k, a in enumerate(paths):
        for b in paths[k + 1:]:
            if any(_cross(p, q, r, s) for p, q in a for r, s in b):
                n += 1
    length = sum(math.dist(p, q) for a in paths for p, q in a)
    bends = sum(len(a) - 1 for a in paths)
    return n, length, bends


def best_layout(root: ET.Element, title) -> ET.Element:
    """Relayout with several band widths and start orders, route each, keep the cleanest routing."""
    best = None
    for aspect in (1.4, 2.2):  # wider pages print too small in Report 4
        for seed in (None, 1, 2, 3, 4, 5):
            trial = copy.deepcopy(root)
            relayout(trial, aspect, seed)
            t = next((c for c in trial.iter("mxCell") if c.get("id") == "title"), None)
            route_edges(trial, t)
            x, length, bends = route_score(trial)
            x = x * 1500 + length + bends * 40  # one crossing weighs like 1500 px of line
            if best is None or x < best[0]:
                best = (x, trial)
    return best[1]


def process(path: Path, boxes: bool, route: bool, gap: int = 0, layout: bool = False):
    tree = ET.parse(path)
    model = tree.getroot().find(".//mxGraphModel")
    root = model.find("root")
    n = normalise_boxes(root) if boxes else 0
    if layout:
        title = next((c for c in root.iter("mxCell") if c.get("id") == "title"), None)
        new = best_layout(root, title)
        model.remove(root)
        model.append(new)
        root = new
    if gap:
        compact(root, gap)
    bad = overlaps(root)
    title = next((c for c in root.iter("mxCell") if c.get("id") == "title"), None)
    if title is not None:
        g = title.find("mxGeometry")
        g.set("width", str(int(text_width(html.unescape(title.get("value") or ""), True) * 1.25 + 20)))
    r = f = 0
    if route:
        r, f = route_edges(root, title)
    w, h = fit_page(root, model)
    tree.write(path, encoding="utf-8", xml_declaration=True)
    print(f"{path.name}: {n} boxes rewritten, {r} edges routed, {f} not routed, page {w}x{h}, overlapping boxes: {bad or 'none'}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="+", type=Path)
    ap.add_argument("--no-route", action="store_true")
    ap.add_argument("--no-boxes", action="store_true")
    ap.add_argument("--compact", type=int, metavar="GAP", default=0, help="close empty space, keeping GAP px between boxes")
    ap.add_argument("--relayout", action="store_true", help="place every class again by layer (replaces the manual layout)")
    a = ap.parse_args()
    for f in a.files:
        process(f, not a.no_boxes, not a.no_route, a.compact, a.relayout)


if __name__ == "__main__":
    main()
