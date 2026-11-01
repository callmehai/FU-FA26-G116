#!/usr/bin/env python3
"""Xuất SequenceDiagram.org .txt → tab draw.io (chuẩn agenda) → .drawio.png.

Một pipeline, nhanh và ổn định:
  1. .txt → Mermaid
  2. Mermaid → XML qua Draw.io CLI
  3. Chuẩn hoá style theo tab agenda (UC-23a): participant shape, màu, font,
     tiêu đề UC, message 13px
  4. (tuỳ chọn) ghép model vào TrustCabinet_Sequence_Diagrams.drawio
  5. Xuất PNG từ file 1 trang — không --embed-diagram, không --page-index trên
     file gộp (hai cách đó khiến CLI treo / PNG phình to / cắt trắng)

Ví dụ:
  python scripts/txt_to_sequence_drawio_png.py \\
    docs/06-sequencediagram/document-management/UC33a_UploadDocumentsToMeeting.txt

  python scripts/txt_to_sequence_drawio_png.py --no-splice \\
    docs/06-sequencediagram/document-management/UC33a_*.txt
"""

from __future__ import annotations

import argparse
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMBINED = ROOT / "docs/06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio"
PNG_DIR = ROOT / "docs/06-sequencediagram/drawio-png"

PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
PNG_IEND = b"\x00\x00\x00\x00IEND\xaeB`\x82"
# Draw.io CLI 32.6.1 cắt phần dưới ảnh khi ảnh vượt khoảng 29 triệu pixel (đo 30/10/2026: 5238x5538 trọn,
# 7481x7910 bị cắt ở ~3970 px). Giới hạn theo diện tích thay vì theo cạnh để trang dài vẫn được scale lớn như tab cũ.
MAX_PNG_PIXELS = 28_000_000.0
DEFAULT_SCALE = 2.0

DECL_RE = re.compile(
    r'^(actor|boundary|control|participant|database)\s+"([^"]+)"\s+as\s+(\w+)\s*$',
    re.I,
)
MSG_RE = re.compile(r"^(\w+)\s*(-->|->)\s*(\w+)\s*:\s*(.*)$")
ACT_RE = re.compile(r"^(activate|deactivate)\s+(\w+)\s*$", re.I)
FRAG_RE = re.compile(r"^(alt|else|opt|loop|end)\b(.*)$", re.I)

# Style lifeline khớp tab UC-23a (agenda) trong file gộp.
LIFELINE_BASE = (
    "html=1;shape=umlLifeline;perimeter=lifelinePerimeter;whiteSpace=wrap;"
    "container=1;dropTarget=0;collapsible=0;recursiveResize=0;outlineConnect=0;"
    "portConstraint=eastwest;"
    'newEdgeStyle={"edgeStyle":"sequenceEdgeStyle","libavoidRouting":0,"curved":0,"rounded":0};'
    "lifelineDashed=0;rounded=1;absoluteArcSize=1;arcSize=6;lifelineColor=#000000;"
    "lifelineMirror=0;fillColor=#ffffff;strokeColor=#000000;fontColor=#000000;"
    "fontFamily=Trebuchet MS,Verdana,Arial,sans-serif;fontSize=16;"
)
KIND_PARTICIPANT = {
    "actor": ("seqActorStick", "90", "1.25"),
    "boundary": ("seqBoundary", "80", "2"),
    "control": ("seqControl", "80", "2"),
    "participant": (None, "65", "2"),
    "database": ("seqDatabase", "80", "2"),
}

EDGE_FONT = "fontSize=13;labelBackgroundColor=#ffffff;strokeWidth=1;strokeColor=#000000;fontColor=#000000;fontFamily=Trebuchet MS,Verdana,Arial,sans-serif;"
TITLE_STYLE = "text;html=1;align=center;verticalAlign=middle;fontSize=18;fontStyle=1;fontColor=#000000;"


def resolve_drawio() -> Path:
    candidates: list[Path] = []
    configured = os.environ.get("DRAWIO_EXECUTABLE")
    if configured:
        candidates.append(Path(configured))
    if os.name == "nt":
        local = os.environ.get("LOCALAPPDATA")
        if local:
            candidates.append(Path(local) / "Programs" / "draw.io" / "draw.io.exe")
        program_files = os.environ.get("ProgramFiles")
        if program_files:
            candidates.append(Path(program_files) / "draw.io" / "draw.io.exe")
    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    for name in ("draw.io.exe", "drawio.exe", "drawio", "draw.io"):
        found = shutil.which(name)
        if found and Path(found).suffix.lower() not in {".cmd", ".bat"}:
            return Path(found).resolve()
    raise RuntimeError(
        "Không tìm thấy Draw.io CLI. Cài draw.io hoặc đặt DRAWIO_EXECUTABLE."
    )


def kill_drawio() -> None:
    if os.name != "nt":
        return
    subprocess.run(
        ["taskkill", "/F", "/IM", "draw.io.exe"],
        capture_output=True,
        check=False,
    )
    time.sleep(0.8)


def stem_to_page_id(stem: str) -> str:
    """UC33a_UploadDocumentsToMeeting → uc33a_uploaddocumentstomeeting."""
    return stem.lower().replace("-", "").replace(" ", "")


def parse_decls(txt: str) -> tuple[dict[str, str], dict[str, str]]:
    """alias → kind, alias → label."""
    kinds: dict[str, str] = {}
    labels: dict[str, str] = {}
    for line in txt.splitlines():
        match = DECL_RE.match(line.strip())
        if match:
            kinds[match.group(3)] = match.group(1).lower()
            labels[match.group(3)] = match.group(2)
    return kinds, labels


def read_title(txt: str) -> str:
    first = txt.splitlines()[0] if txt.splitlines() else ""
    if first.startswith("title "):
        return first[len("title ") :].strip()
    return first.strip()


def txt_to_mermaid(text: str) -> str:
    lines = ['%%{init: {"sequence": {"mirrorActors": false}}}%%', "sequenceDiagram"]
    for raw in text.splitlines():
        line = raw.strip()
        if not line or line.startswith("title "):
            continue
        match = DECL_RE.match(line)
        if match:
            kind, label, alias = match.group(1).lower(), match.group(2), match.group(3)
            mermaid_kind = "actor" if kind == "actor" else "participant"
            safe = label.replace('"', "'")
            lines.append(f"    {mermaid_kind} {alias} as {safe}")
            continue
        match = MSG_RE.match(line)
        if match:
            src, arrow, dst, msg = match.groups()
            # Không bọc nhãn trong dấu nháy: Mermaid của draw.io giữ dấu nháy thành chữ trên sơ đồ
            # (mermaidData của các tab biểu quyết, agenda không có dấu nháy).
            msg = msg.replace("\\n", "<br/>").replace('"', "'")
            op = "-->>" if arrow == "-->" else "->>"
            lines.append(f"    {src}{op}{dst}: {msg}")
            continue
        match = ACT_RE.match(line)
        if match:
            lines.append(f"    {match.group(1).lower()} {match.group(2)}")
            continue
        match = FRAG_RE.match(line)
        if match:
            keyword, rest = match.group(1).lower(), match.group(2).strip().replace('"', "'")
            if keyword == "end":
                lines.append("    end")
            elif rest:
                lines.append(f"    {keyword} {rest}")
            else:
                lines.append(f"    {keyword}")
    return "\n".join(lines) + "\n"


def normalize_colors(xml: str) -> str:
    xml = re.sub(r"light-dark\(#9370DB,#cccccc\)", "#000000", xml)
    xml = re.sub(r"light-dark\(#ECECFF,#1f2020\)", "#ffffff", xml)
    xml = re.sub(r"light-dark\(#333333,#cccccc\)", "#000000", xml)
    xml = xml.replace("lifelineMirror=1", "lifelineMirror=0")
    return xml


def label_kind(label: str, kinds: dict[str, str], labels: dict[str, str]) -> str | None:
    for alias, kind in kinds.items():
        if labels.get(alias) == label:
            return kind
    lower = label.lower()
    if lower in {"database", ":database"} or label.endswith("Database"):
        return "database"
    if "Controller" in label or "Service" in label:
        return "control"
    if any(token in label for token in ("Screen", "Tab", "Page", "Viewer", "Panel")):
        return "boundary"
    if label.startswith(":") and any(
        token in label for token in ("Repository", "Policy", "Storage", "Hub")
    ):
        return "participant"
    return None


def style_lifeline(kind: str) -> str:
    participant, size, stroke = KIND_PARTICIPANT[kind]
    style = LIFELINE_BASE + f"strokeWidth={stroke};size={size};"
    if participant:
        style += (
            f"participant={participant};verticalAlign=bottom;"
            "align=center;spacingBottom=4;"
        )
    return style


def apply_agenda_styles(model: ET.Element, kinds: dict[str, str], labels: dict[str, str]) -> None:
    for element in model.iter():
        if element.tag == "UserObject":
            label = element.get("label") or element.get("mermaidBaseValue") or ""
            cell = element.find("mxCell")
            if cell is None:
                continue
            style = cell.get("style") or ""
            if "umlLifeline" not in style:
                continue
            kind = label_kind(label, kinds, labels)
            if not kind:
                continue
            new_style = style_lifeline(kind)
            cell.set("style", new_style)
            if element.get("mermaidBaseStyle") is not None:
                element.set("mermaidBaseStyle", new_style)
            continue

        if element.tag != "mxCell":
            continue
        style = element.get("style") or ""
        # Khung alt/opt/loop và guard như tab biểu quyết: viền mảnh, guard 13px căn trái ngay sau marker.
        if "shape=umlFrame" in style:
            style = re.sub(r"strokeWidth=[^;]*;", "strokeWidth=1;", style)
            style = re.sub(r"fillColor=[^;]*;", "fillColor=none;", style)
            style = re.sub(r"fontSize=[^;]*;", "fontSize=13;", style)
            element.set("style", style)
            continue
        if style.startswith("text;"):
            # Guard (con của khung) căn trái; nhãn self-message (cấp trên cùng) giữ căn giữa. Cả hai 13px như message.
            if element.get("parent") not in (None, "1"):
                style = re.sub(r"align=[^;]*;", "align=left;", style)
            style = re.sub(r"fontSize=[^;]*;", "fontSize=13;", style)
            if "labelBackgroundColor=" not in style:
                style += "labelBackgroundColor=#ffffff;"
            element.set("style", style)
            continue
        if "edgeStyle=sequenceEdgeStyle" not in style and "endArrow=" not in style:
            continue
        if element.get("edge") != "1" and "edgeStyle=sequenceEdgeStyle" not in style:
            continue
        # Chuẩn hoá font message / return / self-message theo agenda (13px).
        style = re.sub(r"fontSize=[^;]*;", "fontSize=13;", style)
        if "fontSize=" not in style:
            style += "fontSize=13;"
        if "fontFamily=" not in style:
            style += "fontFamily=Trebuchet MS,Verdana,Arial,sans-serif;"
        else:
            style = re.sub(
                r"fontFamily=[^;]*;",
                "fontFamily=Trebuchet MS,Verdana,Arial,sans-serif;",
                style,
            )
        style = re.sub(r"labelBackgroundColor=[^;]*;", "", style) + "labelBackgroundColor=#ffffff;"
        for key, value in (
            ("strokeColor", "#000000"),
            ("fontColor", "#000000"),
            ("strokeWidth", "1"),
        ):
            if f"{key}=" in style:
                style = re.sub(rf"{key}=[^;]*;", f"{key}={value};", style)
            else:
                style += f"{key}={value};"
        element.set("style", style)


def add_title(model: ET.Element, page_id: str, title_text: str) -> None:
    root_cell = model.find("root")
    if root_cell is None:
        return
    if any((cell.get("id") or "").startswith("title-") for cell in root_cell.iter("mxCell")):
        return

    max_right = 800.0
    for element in model.iter():
        style = ""
        geometry = None
        if element.tag == "UserObject":
            cell = element.find("mxCell")
            if cell is None:
                continue
            style = cell.get("style") or ""
            geometry = cell.find("mxGeometry")
        elif element.tag == "mxCell":
            style = element.get("style") or ""
            geometry = element.find("mxGeometry")
        if geometry is None or "umlLifeline" not in style:
            continue
        max_right = max(
            max_right,
            float(geometry.get("x") or 0) + float(geometry.get("width") or 0),
        )

    # Dời xuống nhường chỗ tiêu đề: chỉ cell cấp trên cùng và điểm của edge cấp trên cùng. Dời cả cell con
    # (activation trong lifeline, guard trong khung) làm chúng lệch 50 px khỏi cha; không dời điểm edge thì mọi
    # message nằm cao hơn activation của nó 50 px — mũi tên gọi/trả về chồng lên nhau.
    for cell in model.iter("mxCell"):
        if cell.get("parent") != "1":
            continue
        geometry = cell.find("mxGeometry")
        if geometry is None:
            continue
        if geometry.get("relative") == "1":
            for point in geometry.iter("mxPoint"):
                if point.get("y") is not None:
                    point.set("y", str(float(point.get("y")) + 50))
            continue
        if geometry.get("y") is not None:
            geometry.set("y", str(float(geometry.get("y")) + 50))
        elif geometry.get("width") is not None and geometry.get("height") is not None:
            geometry.set("y", "50")

    title = ET.Element(
        "mxCell",
        {
            "id": f"title-{page_id}",
            "value": title_text,
            "style": TITLE_STYLE,
            "vertex": "1",
            "parent": "1",
        },
    )
    ET.SubElement(
        title,
        "mxGeometry",
        {
            "x": "0",
            "y": "0",
            "width": str(int(max_right)),
            "height": "42",
            "as": "geometry",
        },
    )
    insert_at = 2
    for index, child in enumerate(list(root_cell)):
        if child.tag == "mxCell" and child.get("id") == "1":
            insert_at = index + 1
            break
    root_cell.insert(insert_at, title)


def diagram_extent(model: ET.Element) -> tuple[float, float]:
    width = float(model.get("pageWidth") or 0)
    height = float(model.get("pageHeight") or 0)
    max_x = width
    max_y = height
    for geometry in model.iter("mxGeometry"):
        if geometry.get("relative") == "1":
            continue
        x = float(geometry.get("x") or 0)
        y = float(geometry.get("y") or 0)
        w = float(geometry.get("width") or 0)
        h = float(geometry.get("height") or 0)
        max_x = max(max_x, x + w)
        max_y = max(max_y, y + h)
    return max(max_x, 1.0), max(max_y, 1.0)


def choose_scale(model: ET.Element, requested: float | None) -> str:
    if requested is not None:
        return f"{requested:g}"
    width, height = diagram_extent(model)
    scale = min(DEFAULT_SCALE, math.sqrt(MAX_PNG_PIXELS / (width * height)))
    # Làm tròn xuống 1 chữ số thập phân để ổn định giữa các lần chạy và không vượt giới hạn.
    scale = max(0.4, math.floor(scale * 10 + 1e-9) / 10)
    return f"{scale:g}"


def convert_txt(
    drawio: Path,
    txt_path: Path,
    page_id: str,
    work_dir: Path,
) -> tuple[ET.Element, str, str]:
    raw = txt_path.read_text(encoding="utf-8")
    kinds, labels = parse_decls(raw)
    title_text = read_title(raw)

    mmd_path = work_dir / f"{page_id}.mmd"
    out_drawio = work_dir / f"{page_id}.drawio"
    mmd_path.write_text(txt_to_mermaid(raw), encoding="utf-8")

    kill_drawio()
    result = subprocess.run(
        [str(drawio), "-x", "-f", "xml", "-o", str(out_drawio), str(mmd_path)],
        capture_output=True,
        text=True,
        timeout=180,
    )
    kill_drawio()
    if result.returncode != 0 or not out_drawio.is_file():
        detail = (result.stderr or result.stdout or "").strip()
        raise RuntimeError(f"Mermaid→draw.io thất bại {txt_path.name}: {detail}")

    xml_text = normalize_colors(out_drawio.read_text(encoding="utf-8"))
    root = ET.fromstring(xml_text)
    diagram = root.find("diagram")
    model = diagram.find("mxGraphModel") if diagram is not None else None
    if model is None:
        raise RuntimeError(f"Không có mxGraphModel trong {txt_path.name}")

    apply_agenda_styles(model, kinds, labels)
    add_title(model, page_id, title_text)

    # Ghi lại file 1 trang đã chuẩn hoá — dùng để xuất PNG.
    if diagram is not None:
        diagram.set("id", page_id)
        diagram.set("name", title_text)
    # Xoá children cũ rồi gắn model đã sửa.
    # (model vẫn nằm trong tree root)
    single = (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<mxfile host="Electron">\n'
        f'  <diagram id="{page_id}" name="{_xml_escape(title_text)}">\n'
        f"    {ET.tostring(model, encoding='unicode')}\n"
        "  </diagram>\n"
        "</mxfile>\n"
    )
    out_drawio.write_text(single, encoding="utf-8")
    return model, str(out_drawio), title_text


def _xml_escape(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace('"', "&quot;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def splice_into_combined(page_id: str, model: ET.Element, title_text: str = "") -> None:
    text = COMBINED.read_text(encoding="utf-8")
    pattern = re.compile(
        rf'(<diagram\b[^>]*\bid="{re.escape(page_id)}"[^>]*>)(.*?)(</diagram>)',
        re.DOTALL,
    )
    match = pattern.search(text)
    model_xml = ET.tostring(model, encoding="unicode").strip()
    if match:
        COMBINED.write_text(
            text[: match.start()]
            + match.group(1)
            + "\n    "
            + model_xml
            + "\n  "
            + match.group(3)
            + text[match.end() :],
            encoding="utf-8",
        )
    else:
        end_tag = "</mxfile>"
        idx = text.rfind(end_tag)
        if idx == -1:
            raise RuntimeError(f"Không tìm thấy {end_tag} trong {COMBINED.name}")
        name = _xml_escape(title_text or page_id)
        new_tab = f'  <diagram id="{page_id}" name="{name}">\n    {model_xml}\n  </diagram>\n'
        COMBINED.write_text(text[:idx] + new_tab + text[idx:], encoding="utf-8")


def repair_png(path: Path) -> None:
    data = path.read_bytes()
    if not data.startswith(PNG_SIGNATURE):
        raise RuntimeError(f"Không phải PNG: {path.name}")
    if not data.endswith(PNG_IEND):
        if data.endswith(b"\x00\x00\x00\x00"):
            data = data[:-4]
        data += PNG_IEND
        path.write_bytes(data)
    if not path.read_bytes().endswith(PNG_IEND):
        raise RuntimeError(f"PNG chưa hoàn chỉnh: {path.name}")


def _replace_with_retry(src: Path, dest: Path, attempts: int = 8) -> None:
    """Windows hay giữ khóa PNG vừa mở/xem — thử lại rồi fallback copy+xóa."""
    last_error: OSError | None = None
    for attempt in range(attempts):
        try:
            os.replace(src, dest)
            return
        except OSError as exc:
            last_error = exc
            kill_drawio()
            time.sleep(0.4 * (attempt + 1))
    try:
        shutil.copyfile(src, dest)
        src.unlink(missing_ok=True)
        return
    except OSError as exc:
        raise RuntimeError(
            f"Không ghi đè được {dest.name} (file đang bị khóa?). {exc}"
        ) from (last_error or exc)


def export_png(drawio: Path, single_drawio: Path, page_id: str, scale: str) -> Path:
    PNG_DIR.mkdir(parents=True, exist_ok=True)
    out = PNG_DIR / f"{page_id}.drawio.png"
    # Temp nằm ngoài PNG_DIR để antivirus/viewer ít giữ khóa khi replace.
    with tempfile.TemporaryDirectory(prefix="seq-png-") as tmp:
        temp = Path(tmp) / out.name
        # Không --embed-diagram: nhanh, ổn; source nằm ở file gộp / .txt.
        command = [
            str(drawio),
            "--disable-gpu",
            "--disable-software-rasterizer",
            "--export",
            "--format",
            "png",
            "--scale",
            scale,
            "--output",
            str(temp),
            str(single_drawio),
        ]
        kill_drawio()
        result = subprocess.run(command, capture_output=True, text=True, timeout=180)
        kill_drawio()
        if result.returncode != 0 or not temp.is_file():
            detail = (result.stderr or result.stdout or "").strip()
            raise RuntimeError(f"Xuất PNG thất bại {page_id}: {detail}")
        repair_png(temp)
        _replace_with_retry(temp, out)
    return out

def process_one(
    drawio: Path,
    txt_path: Path,
    *,
    splice: bool,
    scale: float | None,
    work_dir: Path,
) -> Path:
    page_id = stem_to_page_id(txt_path.stem)
    print(f"→ {txt_path.name}  ({page_id})", flush=True)
    model, single_path, title_text = convert_txt(drawio, txt_path, page_id, work_dir)
    chosen_scale = choose_scale(model, scale)
    if splice:
        splice_into_combined(page_id, model, title_text)
        print(f"  spliced vào {COMBINED.name}", flush=True)
    out = export_png(drawio, Path(single_path), page_id, chosen_scale)
    print(f"  PNG scale={chosen_scale} → {out.name} ({out.stat().st_size} bytes)", flush=True)
    return out


def expand_inputs(paths: list[str]) -> list[Path]:
    result: list[Path] = []
    for raw in paths:
        path = Path(raw)
        if not path.is_absolute():
            path = (ROOT / path).resolve() if not path.exists() else path.resolve()
            if not path.exists():
                path = Path(raw).resolve()
        if path.is_dir():
            result.extend(sorted(path.glob("*.txt")))
        elif path.is_file():
            result.append(path)
        else:
            # glob từ cwd / ROOT
            matches = sorted(Path().glob(raw)) or sorted(ROOT.glob(raw))
            if not matches:
                raise RuntimeError(f"Không tìm thấy: {raw}")
            result.extend(p.resolve() for p in matches if p.suffix.lower() == ".txt")
    # bỏ trùng, giữ thứ tự
    seen: set[Path] = set()
    unique: list[Path] = []
    for path in result:
        if path in seen:
            continue
        seen.add(path)
        unique.append(path)
    return unique


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Xuất .txt SequenceDiagram.org → drawio.png (chuẩn agenda)."
    )
    parser.add_argument(
        "inputs",
        nargs="+",
        help="File .txt, thư mục, hoặc glob (vd. docs/.../UC33a_*.txt)",
    )
    parser.add_argument(
        "--no-splice",
        action="store_true",
        help="Chỉ xuất PNG, không ghi vào TrustCabinet_Sequence_Diagrams.drawio",
    )
    parser.add_argument(
        "--scale",
        type=float,
        default=None,
        help="Ép scale PNG (mặc định tự chọn ≤2 để không cắt ảnh)",
    )
    args = parser.parse_args(argv)

    drawio = resolve_drawio()
    files = expand_inputs(args.inputs)
    if not files:
        raise RuntimeError("Không có file .txt nào để xử lý")

    print(f"Draw.io: {drawio}", flush=True)
    print(f"Số file: {len(files)}", flush=True)

    with tempfile.TemporaryDirectory(prefix="seq-txt-") as tmp:
        work = Path(tmp)
        for txt_path in files:
            process_one(
                drawio,
                txt_path,
                splice=not args.no_splice,
                scale=args.scale,
                work_dir=work,
            )

    print("xong", flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001 — CLI: in lỗi rõ rồi thoát
        print(f"lỗi: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
