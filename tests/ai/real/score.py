#!/usr/bin/env python3
"""Score the graded workbooks of the AI-draft evaluation on real meetings.

    uv run --with openpyxl tests/ai/real/score.py --kit tests/ai/out/real/kit --drafts tests/ai/out/real/drafts \
        --price api=0.27,1.10 --out tests/ai/out/real/ket-qua-vong-1

Reads `key.json` and every `cham-<grader>.xlsx` in --kit (or --grader name=path), the draft meta files
(`<slug>-<provider>-<run>.json` from generate.js: model, elapsedMs, optional costUsd) in --drafts, and optionally the BA's
reference workbook (--ref) to count the key points when a grader's "Ý chuẩn" sheet was left empty. Writes `ket-qua.md`
and `ket-qua.xlsx` into --out: every metric per provider and per meeting, the variance across the runs, the agreement of
the first two graders (percent and Cohen's kappa) and the list of disagreements to reconcile, plus a methods paragraph.

Run it once on the independent grading and keep that output (the agreement it reports is the one to cite); run it again
into another --out after the graders reconcile. The metric and kappa functions are unit-tested (tests/test_score.py).
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE.parent / "out" / "real"
sys.path.insert(0, str(HERE))
from kit import load_exports  # noqa: E402

LABELS = ["Đúng", "Sai lệch", "Bịa", "Không kiểm được"]
ERRORS = ("Sai lệch", "Bịa")


# ---------------------------------------------------------------------------------------------------------------- maths


def cohen_kappa(a: list[str], b: list[str]) -> float | None:
    """Cohen's kappa for two raters over the same items; None without items. Perfect agreement on one category is 1."""
    if len(a) != len(b):
        raise ValueError("both raters must label the same items")
    n = len(a)
    if n == 0:
        return None
    po = sum(x == y for x, y in zip(a, b)) / n
    categories = set(a) | set(b)
    pe = sum((a.count(c) / n) * (b.count(c) / n) for c in categories)
    if pe == 1:
        return 1.0 if po == 1 else 0.0
    return (po - pe) / (1 - pe)


def percent_agreement(a: list[str], b: list[str]) -> float | None:
    return None if not a else sum(x == y for x, y in zip(a, b)) / len(a)


def kappa_words(k: float | None) -> str:
    """Landis & Koch (1977)."""
    if k is None:
        return "—"
    if k < 0:
        return "kém hơn ngẫu nhiên"
    for limit, words in ((0.2, "rất thấp"), (0.4, "thấp"), (0.6, "trung bình"), (0.8, "khá cao"), (1.01, "gần như tuyệt đối")):
        if k <= limit:
            return words
    return "gần như tuyệt đối"


def wilson(successes: float, n: float, z: float = 1.96) -> tuple[float, float] | None:
    if n <= 0:
        return None
    p = successes / n
    centre = (p + z * z / (2 * n)) / (1 + z * z / n)
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / (1 + z * z / n)
    return max(0.0, centre - half), min(1.0, centre + half)


def ratio(a: float, b: float) -> float | None:
    return None if b == 0 else a / b


def draft_metrics(claims: list[dict], points: list[dict]) -> dict:
    """One grader's labels on one draft -> counts and rates. `claims`: {label, source, wrong}; `points`: {level, coverage}."""
    labelled = [c for c in claims if c["label"] in LABELS]
    count = {label: sum(c["label"] == label for c in labelled) for label in LABELS}
    checkable = len(labelled) - count["Không kiểm được"]
    errors = [c for c in labelled if c["label"] in ERRORS]
    graded = [p for p in points if p["coverage"] in ("Có", "Một phần", "Không")]
    main = [p for p in graded if p["level"].casefold() == "chính"]

    def covered(ps, partial=0.5):
        return sum(1 if p["coverage"] == "Có" else partial if p["coverage"] == "Một phần" else 0 for p in ps)

    return {
        "claims": len(claims), "labelled": len(labelled), "unlabelled": len(claims) - len(labelled), **count,
        "checkable": checkable,
        "precision": ratio(count["Đúng"], checkable),
        "hallucination": ratio(count["Bịa"], len(labelled)),
        "distortion": ratio(count["Sai lệch"], len(labelled)),
        "errors": len(errors),
        "errAsr": sum(c["source"] == "Gỡ băng" for c in errors),
        "errAi": sum(c["source"] == "AI" for c in errors),
        "errUnknown": sum(c["source"] not in ("Gỡ băng", "AI") for c in errors),
        "wrongNames": sum(c["wrong"] in ("Tên", "Tên và số") for c in labelled),
        "wrongNumbers": sum(c["wrong"] in ("Số", "Tên và số") for c in labelled),
        "points": len(graded), "covered": covered(graded), "coveredStrict": covered(graded, 0),
        "recall": ratio(covered(graded), len(graded)), "recallStrict": ratio(covered(graded, 0), len(graded)),
        "mainPoints": len(main), "mainCovered": covered(main), "recallMain": ratio(covered(main), len(main)),
    }


POOLED = ["claims", "labelled", "unlabelled", *LABELS, "checkable", "errors", "errAsr", "errAi", "errUnknown",
          "wrongNames", "wrongNumbers", "points", "covered", "coveredStrict", "mainPoints", "mainCovered"]


def pool(rows: list[dict]) -> dict:
    """Sum the counts of several drafts (and graders), then recompute the rates (micro average)."""
    total = {k: sum(r[k] for r in rows) for k in POOLED}
    total["precision"] = ratio(total["Đúng"], total["checkable"])
    total["hallucination"] = ratio(total["Bịa"], total["labelled"])
    total["distortion"] = ratio(total["Sai lệch"], total["labelled"])
    total["recall"] = ratio(total["covered"], total["points"])
    total["recallStrict"] = ratio(total["coveredStrict"], total["points"])
    total["recallMain"] = ratio(total["mainCovered"], total["mainPoints"])
    total["drafts"] = len(rows)
    return total


def spread(values: list[float | None]) -> tuple[float | None, float | None]:
    present = [v for v in values if v is not None]
    if not present:
        return None, None
    return statistics.mean(present), statistics.stdev(present) if len(present) > 1 else None


# ---------------------------------------------------------------------------------------------------------------- input


def cell(value) -> str:
    return "" if value is None else str(value).strip()


def read_grader(path: pathlib.Path, key: dict) -> dict[str, dict]:
    """{sheet: {"claims": {#: row}, "points": {#: row}}} for every draft sheet in key.json."""
    from openpyxl import load_workbook
    book = load_workbook(path, read_only=True, data_only=True)
    out = {}
    for d in key["drafts"]:
        sheet = d["sheet"]
        claims, points = {}, {}
        if sheet in book.sheetnames:
            for r in list(book[sheet].iter_rows(min_row=2, values_only=True)):
                r = [cell(v) for v in r] + [""] * 10
                if not r[2]:
                    continue
                claims[r[0] or f"?{len(claims) + 1}"] = {"n": r[0], "text": r[2], "label": r[5], "source": r[6],
                                                          "wrong": r[7], "evidence": r[8], "note": r[9]}
        ref_sheet = f"{sheet} Ý chuẩn"
        if ref_sheet in book.sheetnames:
            for r in list(book[ref_sheet].iter_rows(min_row=2, values_only=True)):
                r = [cell(v) for v in r] + [""] * 10
                if not r[0] or not r[1]:
                    continue
                points[r[0]] = {"n": r[0], "text": r[1], "level": r[5], "coverage": r[7], "evidence": r[8]}
        out[sheet] = {"claims": claims, "points": points}
    return out


def load_meta(folder: pathlib.Path | None, file: str) -> dict:
    if folder is None:
        return {}
    path = folder / pathlib.Path(file).with_suffix(".json").name
    return json.loads(path.read_text()) if path.exists() else {}


# --------------------------------------------------------------------------------------------------------------- report


def pct(value: float | None) -> str:
    return "—" if value is None else f"{value * 100:.1f}%"


def num(value: float | None, digits: int = 1) -> str:
    return "—" if value is None else f"{value:.{digits}f}"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--kit", default=str(OUT / "kit"), help="folder with key.json and cham-*.xlsx")
    parser.add_argument("--grader", action="append", default=[], help="name=path to a graded workbook")
    parser.add_argument("--drafts", help="folder with the drafts' .json meta (elapsedMs, model, costUsd)")
    parser.add_argument("--exports", default=str(OUT), help="export.py output, to estimate the tokens sent")
    parser.add_argument("--price", action="append", default=[],
                        help="provider=input,output USD per million tokens, for an estimated cost")
    parser.add_argument("--chars-per-token", type=float, default=3.0)
    parser.add_argument("--out", help="default: <kit>/ket-qua")
    args = parser.parse_args()

    kit = pathlib.Path(args.kit)
    key = json.loads((kit / "key.json").read_text())
    graders = dict(item.split("=", 1) for item in args.grader) or {
        p.stem.removeprefix("cham-"): str(p) for p in sorted(kit.glob("cham-*.xlsx"))}
    if not graders:
        sys.exit(f"no cham-*.xlsx in {kit}")
    grades = {name: read_grader(pathlib.Path(path), key) for name, path in graders.items()}
    names = list(grades)
    drafts_dir = pathlib.Path(args.drafts) if args.drafts else None
    exports = load_exports(pathlib.Path(args.exports)) if pathlib.Path(args.exports).exists() else {}
    prices = {p: tuple(float(x) for x in v.split(",")) for p, v in (item.split("=", 1) for item in args.price)}
    out = pathlib.Path(args.out) if args.out else kit / "ket-qua"
    out.mkdir(parents=True, exist_ok=True)

    # Per draft, per grader.
    per_draft = []
    for d in key["drafts"]:
        meta = load_meta(drafts_dir, d["file"])
        row = {**d, "model": meta.get("model", ""), "elapsedMs": meta.get("elapsedMs"), "graders": {}}
        for name in names:
            sheet = grades[name][d["sheet"]]
            row["graders"][name] = draft_metrics(list(sheet["claims"].values()), list(sheet["points"].values()))
        # Cost: measured if the meta says so, else estimated from characters for a provider with a price.
        cost, estimated = meta.get("costUsd"), False
        if cost is None and d["provider"] in prices and d["meeting"] in exports:
            transcript = exports[d["meeting"]]["transcript"]
            sent = sum(len(line["speaker"]) + len(line["text"]) + 12 for line in transcript) + 4000
            draft_file = drafts_dir / d["file"] if drafts_dir else None
            written = len(draft_file.read_text()) if draft_file and draft_file.exists() else 4000
            price_in, price_out = prices[d["provider"]]
            cost = (sent / args.chars_per_token * price_in + written / args.chars_per_token * price_out) / 1e6
            estimated = True
        row["costUsd"], row["costEstimated"] = cost, estimated
        per_draft.append(row)

    def group(keyfn) -> dict:
        groups = {}
        for row in per_draft:
            groups.setdefault(keyfn(row), []).append(row)
        return groups

    def summarise(rows: list[dict]) -> dict:
        everyone = [r["graders"][n] for r in rows for n in names]
        pooled = pool(everyone)
        pooled["drafts"] = len(rows)
        by_grader = {n: pool([r["graders"][n] for r in rows]) for n in names}
        # Variance across runs: each draft's rate averaged over the graders, then mean and SD over the drafts.
        def per_run(metric):
            values = []
            for r in rows:
                vs = [r["graders"][n][metric] for n in names if r["graders"][n][metric] is not None]
                values.append(statistics.mean(vs) if vs else None)
            return spread(values)
        return {
            "pooled": pooled, "byGrader": by_grader,
            "runs": {m: per_run(m) for m in ("precision", "hallucination", "recall", "recallMain")},
            "claimsPerDraft": spread([statistics.mean(r["graders"][n]["claims"] for n in names) for r in rows]),
            "elapsed": spread([r["elapsedMs"] / 1000 if r["elapsedMs"] else None for r in rows]),
            "cost": spread([r["costUsd"] for r in rows]),
            "costEstimated": any(r["costEstimated"] for r in rows),
            "models": sorted({r["model"] for r in rows if r["model"]}),
            "ciPrecision": wilson(pooled["Đúng"], pooled["checkable"]),
            "ciHallucination": wilson(pooled["Bịa"], pooled["labelled"]),
        }

    by_provider = {p: summarise(rows) for p, rows in sorted(group(lambda r: r["provider"]).items())}
    by_meeting = {k: summarise(rows) for k, rows in sorted(group(lambda r: (r["meeting"], r["provider"])).items())}

    # Agreement and disagreements between the first two graders.
    agreement, disagreements = {}, []
    if len(names) >= 2:
        g1, g2 = names[0], names[1]
        labels1, labels2, src1, src2, cov1, cov2 = [], [], [], [], [], []
        for d in key["drafts"]:
            s1, s2 = grades[g1][d["sheet"]], grades[g2][d["sheet"]]
            for n in sorted(set(s1["claims"]) | set(s2["claims"]), key=lambda x: (len(x), x)):
                a, b = s1["claims"].get(n), s2["claims"].get(n)
                if a is None or b is None:
                    only = a or b
                    disagreements.append({"sheet": d["sheet"], "kind": "Ý", "n": n, "text": only["text"],
                                          g1: "(không có dòng)" if a is None else a["label"],
                                          g2: "(không có dòng)" if b is None else b["label"], "why": "chỉ một người tách dòng này"})
                    continue
                if a["label"] in LABELS and b["label"] in LABELS:
                    labels1.append(a["label"])
                    labels2.append(b["label"])
                    if a["label"] in ERRORS and b["label"] in ERRORS and a["source"] and b["source"]:
                        src1.append(a["source"])
                        src2.append(b["source"])
                why = []
                if a["label"] != b["label"]:
                    why.append("nhãn")
                elif a["label"] in ERRORS and a["source"] != b["source"]:
                    why.append("nguồn lỗi")
                if a["wrong"] != b["wrong"] and a["label"] in ERRORS:
                    why.append("sai tên/số")
                if why:
                    disagreements.append({"sheet": d["sheet"], "kind": "Ý", "n": n, "text": a["text"],
                                          g1: " / ".join(x for x in (a["label"], a["source"], a["wrong"], a["evidence"]) if x),
                                          g2: " / ".join(x for x in (b["label"], b["source"], b["wrong"], b["evidence"]) if x),
                                          "why": ", ".join(why)})
            for n in sorted(set(s1["points"]) & set(s2["points"]), key=lambda x: (len(x), x)):
                a, b = s1["points"][n], s2["points"][n]
                if a["coverage"] and b["coverage"]:
                    cov1.append(a["coverage"])
                    cov2.append(b["coverage"])
                    if a["coverage"] != b["coverage"]:
                        disagreements.append({"sheet": d["sheet"] + " Ý chuẩn", "kind": "Ý chuẩn", "n": n, "text": a["text"],
                                              g1: a["coverage"], g2: b["coverage"], "why": "mức bao phủ"})
        agreement = {
            "pair": (g1, g2),
            "label": (len(labels1), percent_agreement(labels1, labels2), cohen_kappa(labels1, labels2)),
            "source": (len(src1), percent_agreement(src1, src2), cohen_kappa(src1, src2)),
            "coverage": (len(cov1), percent_agreement(cov1, cov2), cohen_kappa(cov1, cov2)),
        }

    write_markdown(out / "ket-qua.md", key, names, per_draft, by_provider, by_meeting, agreement, disagreements)
    write_excel(out / "ket-qua.xlsx", names, per_draft, by_provider, by_meeting, agreement, disagreements)
    print(f"wrote {out / 'ket-qua.md'} and {out / 'ket-qua.xlsx'}: {len(per_draft)} drafts, {len(names)} graders, "
          f"{len(disagreements)} disagreements")


METRIC_ROWS = [
    ("Số bản dự thảo", lambda s: str(s["pooled"]["drafts"])),
    ("Ý mỗi bản (TB ± SD)", lambda s: f"{num(s['claimsPerDraft'][0])} ± {num(s['claimsPerDraft'][1])}"),
    ("Độ chính xác (Đúng / ý kiểm được)", lambda s: f"{pct(s['pooled']['precision'])} "
        f"[{pct(s['ciPrecision'][0]) if s['ciPrecision'] else '—'}–{pct(s['ciPrecision'][1]) if s['ciPrecision'] else '—'}]"),
    ("Tỉ lệ bịa (Bịa / tổng ý)", lambda s: f"{pct(s['pooled']['hallucination'])} "
        f"[{pct(s['ciHallucination'][0]) if s['ciHallucination'] else '—'}–{pct(s['ciHallucination'][1]) if s['ciHallucination'] else '—'}]"),
    ("Tỉ lệ sai lệch", lambda s: pct(s["pooled"]["distortion"])),
    ("Độ phủ ý chuẩn (Có = 1, Một phần = 0,5)", lambda s: pct(s["pooled"]["recall"])),
    ("Độ phủ ý chuẩn, chỉ tính Có", lambda s: pct(s["pooled"]["recallStrict"])),
    ("Độ phủ ý Chính", lambda s: pct(s["pooled"]["recallMain"])),
    ("Sai tên (tổng, cả 2 người chấm)", lambda s: str(s["pooled"]["wrongNames"])),
    ("Sai số (tổng, cả 2 người chấm)", lambda s: str(s["pooled"]["wrongNumbers"])),
    ("Lỗi do gỡ băng / do AI / chưa ghi", lambda s: f"{s['pooled']['errAsr']} / {s['pooled']['errAi']} / {s['pooled']['errUnknown']}"),
    ("Thời gian mỗi bản, giây (TB ± SD)", lambda s: f"{num(s['elapsed'][0])} ± {num(s['elapsed'][1])}"),
    ("Chi phí mỗi bản, USD", lambda s: ("—" if s["cost"][0] is None else f"{s['cost'][0]:.4f}" + (" (ước tính)" if s["costEstimated"] else ""))),
    ("Dao động giữa các lần chạy: độ chính xác (TB ± SD)", lambda s: f"{pct(s['runs']['precision'][0])} ± {pct(s['runs']['precision'][1])}"),
    ("Dao động: tỉ lệ bịa", lambda s: f"{pct(s['runs']['hallucination'][0])} ± {pct(s['runs']['hallucination'][1])}"),
    ("Dao động: độ phủ ý chuẩn", lambda s: f"{pct(s['runs']['recall'][0])} ± {pct(s['runs']['recall'][1])}"),
    ("Ý chưa gán nhãn", lambda s: str(s["pooled"]["unlabelled"])),
]


def write_markdown(path, key, names, per_draft, by_provider, by_meeting, agreement, disagreements) -> None:
    providers = list(by_provider)
    lines = ["# Kết quả đo độ chính xác dự thảo AI trên cuộc họp thật", ""]
    lines += ["## Theo nhà cung cấp", "", "| Chỉ số | " + " | ".join(
        f"{p} ({', '.join(by_provider[p]['models']) or '—'})" for p in providers) + " |",
        "|---|" + "---|" * len(providers)]
    for title, fn in METRIC_ROWS:
        lines.append(f"| {title} | " + " | ".join(fn(by_provider[p]) for p in providers) + " |")
    lines += ["", "Khoảng trong ngoặc vuông: khoảng tin cậy 95% Wilson trên tổng số ý (các ý trong một bản không độc lập "
              "hoàn toàn, nên coi là gần đúng).", "", "## Theo cuộc họp", ""]
    lines += ["| Cuộc họp | Nhà cung cấp | Bản | Ý/bản | Chính xác | Bịa | Sai lệch | Phủ ý chuẩn | Phủ ý Chính | Sai tên | Sai số | Giây/bản |",
              "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for (meeting, provider), s in by_meeting.items():
        title = key["meetings"].get(meeting, {}).get("code", meeting)
        p = s["pooled"]
        lines.append(f"| {title} {meeting} | {provider} | {p['drafts']} | {num(s['claimsPerDraft'][0])} | {pct(p['precision'])} | "
                     f"{pct(p['hallucination'])} | {pct(p['distortion'])} | {pct(p['recall'])} | {pct(p['recallMain'])} | "
                     f"{p['wrongNames']} | {p['wrongNumbers']} | {num(s['elapsed'][0])} |")
    lines += ["", "## Đồng thuận giữa hai người chấm", ""]
    if agreement:
        g1, g2 = agreement["pair"]
        lines += [f"{g1} và {g2}, chấm độc lập.", "", "| Đối tượng | Số mục chấm chung | Đồng ý | Cohen's kappa | Mức |",
                  "|---|---|---|---|---|"]
        for title, k in (("Nhãn của từng ý", "label"), ("Nguồn lỗi (cả hai cùng cho là lỗi)", "source"),
                         ("Độ phủ ý chuẩn", "coverage")):
            n, po, kappa = agreement[k]
            lines.append(f"| {title} | {n} | {pct(po)} | {num(kappa, 2)} | {kappa_words(kappa)} |")
        lines += ["", f"{len(disagreements)} chỗ bất đồng, liệt kê ở sheet \"Bất đồng\" của ket-qua.xlsx để hai người "
                  "chấm thống nhất (nghe lại bản ghi), rồi chạy lại score.py vào thư mục khác."]
    else:
        lines.append("Chỉ có một người chấm: chưa tính được đồng thuận.")
    lines += ["", "## Từng bản dự thảo", "", "| Sheet | Nhà cung cấp | Lần | Model | Giây | " +
              " | ".join(f"Chính xác ({n}) | Bịa ({n}) | Phủ ({n})" for n in names) + " |",
              "|---|---|---|---|---|" + "---|---|---|" * len(names)]
    for r in per_draft:
        lines.append(f"| {r['sheet']} | {r['provider']} | {r['run']} | {r['model'] or '—'} | "
                     f"{num(r['elapsedMs'] / 1000 if r['elapsedMs'] else None)} | " + " | ".join(
                         f"{pct(r['graders'][n]['precision'])} | {pct(r['graders'][n]['hallucination'])} | {pct(r['graders'][n]['recall'])}"
                         for n in names) + " |")
    lines += ["", "## Phương pháp (dán vào báo cáo)", "", methods(key, names, per_draft, by_provider, agreement), ""]
    path.write_text("\n".join(lines))


def methods(key, names, per_draft, by_provider, agreement) -> str:
    meetings = sorted({r["meeting"] for r in per_draft})
    runs = max((r["run"] for r in per_draft), default=0)
    claims = sum(statistics.mean(r["graders"][n]["claims"] for n in names) for r in per_draft)
    providers = ", ".join(f"{p} ({', '.join(s['models']) or 'model không ghi'})" for p, s in by_provider.items())
    text = (
        f"Chúng tôi đo độ chính xác của dự thảo biên bản do AI viết trên {len(meetings)} cuộc họp thật của nhóm, so "
        f"{len(by_provider)} cấu hình: {providers}. Với mỗi cuộc họp, bản gỡ băng được đóng băng trước khi chạy (xuất "
        "nguyên văn từ cơ sở dữ liệu, kèm mã băm SHA-256), mỗi cấu hình viết "
        f"{runs} bản từ cùng bản gỡ băng đó qua chính API của hệ thống, tổng cộng {len(per_draft)} bản. "
        "Căn cứ đúng sai là dữ liệu hệ thống (chương trình, giờ, thành phần và điểm danh, biểu quyết, kết luận, tài liệu) và "
        "bản ghi âm; vì bản gỡ băng cũng có thể sai, mỗi lỗi được quy về nguồn: lỗi gỡ băng (AI chép đúng một câu đã nhận "
        "dạng sai) hay lỗi AI. Trước khi xem dự thảo, BA lập danh sách ý chính của từng cuộc họp (đánh dấu ý Chính/Phụ) "
        "từ bản ghi. Các bản dự thảo được tách thành "
        f"{claims:.0f} ý (claim) theo quy tắc cố định (gạch đầu dòng, câu, dấu chấm phẩy, mệnh đề nối bằng \"và\", "
        "\"đồng thời\" khi hai vế đều là mệnh đề), mang nhãn A/B xáo ngẫu nhiên theo từng cuộc họp để người chấm không biết "
        f"nhà cung cấp. {len(names)} người chấm ({', '.join(names)}) gán độc lập mỗi ý một nhãn Đúng / Sai lệch / Bịa / "
        "Không kiểm được kèm bằng chứng (mốc giờ bản ghi hoặc bản ghi hệ thống) và đánh dấu dự thảo có nêu từng ý chính hay "
        "không. Độ chính xác = Đúng / (tổng ý − Không kiểm được); tỉ lệ bịa = Bịa / tổng ý; độ phủ = ý chính được nêu "
        "(Một phần tính 0,5) / tổng ý chính, tính riêng cho ý Chính. Dao động giữa các lần chạy là độ lệch chuẩn của "
        "các chỉ số trên từng bản.")
    if agreement:
        n, po, kappa = agreement["label"]
        text += (f" Mức đồng thuận giữa hai người chấm trên {n} ý là {pct(po)} (Cohen's kappa {num(kappa, 2)}, mức "
                 f"{kappa_words(kappa)} theo Landis & Koch); các chỗ bất đồng được hai người nghe lại bản ghi để thống "
                 "nhất trước khi tính kết quả cuối.")
    return text


def write_excel(path, names, per_draft, by_provider, by_meeting, agreement, disagreements) -> None:
    from openpyxl import Workbook
    from openpyxl.styles import Alignment, Font

    book = Workbook()
    sheet = book.active
    sheet.title = "Theo nhà cung cấp"
    providers = list(by_provider)
    sheet.append(["Chỉ số", *providers])
    sheet.append(["Model", *(", ".join(by_provider[p]["models"]) for p in providers)])
    for title, fn in METRIC_ROWS:
        sheet.append([title, *(fn(by_provider[p]) for p in providers)])
    sheet.column_dimensions["A"].width = 52

    sheet = book.create_sheet("Theo cuộc họp")
    columns = ["drafts", "claims", *LABELS, "precision", "hallucination", "distortion", "recall", "recallStrict",
               "recallMain", "wrongNames", "wrongNumbers", "errAsr", "errAi", "errUnknown"]
    sheet.append(["Cuộc họp", "Nhà cung cấp", *columns, "Giây/bản TB", "Giây SD", "SD chính xác", "SD bịa", "SD phủ"])
    for (meeting, provider), s in by_meeting.items():
        p = s["pooled"]
        sheet.append([meeting, provider, *(p[c] for c in columns), s["elapsed"][0], s["elapsed"][1],
                      s["runs"]["precision"][1], s["runs"]["hallucination"][1], s["runs"]["recall"][1]])

    sheet = book.create_sheet("Từng bản")
    metric_keys = ["claims", "labelled", *LABELS, "precision", "hallucination", "recall", "recallMain", "wrongNames",
                   "wrongNumbers", "errAsr", "errAi"]
    sheet.append(["Sheet", "Cuộc họp", "Nhà cung cấp", "Lần", "Model", "Giây", "USD", "Người chấm", *metric_keys])
    for r in per_draft:
        for n in names:
            m = r["graders"][n]
            sheet.append([r["sheet"], r["meeting"], r["provider"], r["run"], r["model"],
                          r["elapsedMs"] / 1000 if r["elapsedMs"] else None, r["costUsd"], n, *(m[k] for k in metric_keys)])

    sheet = book.create_sheet("Đồng thuận")
    sheet.append(["Đối tượng", "Số mục", "Đồng ý", "Cohen's kappa", "Mức"])
    if agreement:
        for title, k in (("Nhãn", "label"), ("Nguồn lỗi", "source"), ("Độ phủ ý chuẩn", "coverage")):
            n, po, kappa = agreement[k]
            sheet.append([title, n, po, kappa, kappa_words(kappa)])

    sheet = book.create_sheet("Bất đồng")
    pair = list(agreement["pair"]) if agreement else names[:2]
    sheet.append(["Sheet", "Loại", "#", "Nội dung", *pair, "Khác ở", "Thống nhất"])
    for d in disagreements:
        sheet.append([d["sheet"], d["kind"], d["n"], d["text"], *(d.get(g, "") for g in pair), d["why"], ""])
    for col, width in zip("ABCDEFGH", (12, 9, 6, 70, 34, 34, 14, 20)):
        sheet.column_dimensions[col].width = width
    for row in sheet.iter_rows(min_row=2):
        for c in row:
            c.alignment = Alignment(wrap_text=True, vertical="top")
    for ws in book.worksheets:
        for c in ws[1]:
            c.font = Font(bold=True)
    book.save(path)


if __name__ == "__main__":
    main()
