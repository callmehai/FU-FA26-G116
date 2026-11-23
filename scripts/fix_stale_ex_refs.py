"""One-off: rewrite references to EX that the EX trim removed (Report 3 DOCX)."""
import re
import docx

P = "docs/08-reports/source/Report_3_Software_Requirement_Specification.docx"
RULES = [
    (r"; otherwise EX-4 applies\.", "."),
    (r"; otherwise EX-2 applies\.", "."),
    (r" and EX-5 applies; the other files", "; the other files"),
    (r"the system displays MSG249, MSG165 or MSG166 as in UC-33a EX-2, EX-4 and EX-5", "the system displays MSG249 as in UC-33a EX-2"),
    (r"the system displays MSG164, MSG288, MSG346 or MSG166 as in UC-33a EX-3, EX-8, EX-9 and EX-5", "the system displays MSG164 as in UC-33a EX-2"),
    (r"the system displays MSG164, MSG288, MSG346 or MSG166 as in UC-33a EX-3 and EX-5", "the system displays MSG164 as in UC-33a EX-2"),
    (r"UC-38 EX-4", "UC-38"),
    (r"UC-84c EX-5", "UC-84c EX-2"),
    (r" \(else EX-4\), that no Pending", ", that no Pending"),
]
d = docx.Document(P)
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t"
hits = {}
for t in d.element.body.iter(W):
    if not t.text:
        continue
    new = t.text
    for pat, rep in RULES:
        new, n = re.subn(pat, rep, new)
        if n:
            hits[pat] = hits.get(pat, 0) + n
    if new != t.text:
        t.text = new
for k, v in hits.items():
    print(v, k)
print("rules without hit:", [p for p, _ in RULES if p not in hits])
d.save(P)
