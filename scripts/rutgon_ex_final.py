"""Write <group>-ex-final.md: the EX each changed sequence diagram ends with (label, MSG, situation).
The diagrams are the truth; the spec patch must match this list."""
import re, subprocess, sys
from pathlib import Path

def parse(path):
    ex = {}
    lines = Path(path).read_text(encoding="utf-8").splitlines()
    for i, l in enumerate(lines):
        m = re.match(r"\s*(?:alt|else|opt)\s+(.*?)\s+-\s+(EX-\d+)\s*$", l)
        if not m: continue
        sit, label = m.group(1), m.group(2)
        msg = ""
        for nxt in lines[i + 1:i + 4]:
            mm = re.search(r"(MSG\d+)", nxt)
            if mm: msg = mm.group(1); break
        ex.setdefault(label, (msg, sit))
    return ex

def main(repo, group):
    files = subprocess.run(["git", "-C", repo, "diff", "--name-only", "--", "*.txt"], capture_output=True, text=True).stdout.split()
    out = [f"# EX cuối cùng — nhóm {group}", "",
           "Sinh tự động từ các `.txt` sequence (script `scripts/rutgon_ex_final.py`). Đây là bản chuẩn: UC Spec phải khớp.", ""]
    for f in sorted(files, key=lambda x: Path(x).name):
        name = Path(f).stem
        ex = parse(Path(repo) / f)
        out.append(f"## {name}")
        if not ex: out.append("- Không có EX."); out.append(""); continue
        for k in sorted(ex, key=lambda s: int(s.split('-')[1])):
            msg, sit = ex[k]
            out.append(f"- {k} ({msg or 'không MSG'}): {sit}")
        out.append("")
    p = Path(repo) / "docs/notes/rutgon-ex" / f"{group}-ex-final.md"
    p.write_text("\n".join(out), encoding="utf-8"); print(p)

if __name__ == "__main__": main(sys.argv[1], sys.argv[2])
