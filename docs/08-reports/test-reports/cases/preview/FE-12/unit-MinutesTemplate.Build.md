# MinutesTemplate.Build — Unit (FE-12, MIN)

Description: Fixed minutes template: renders sections I-III from the reviewed draft or from agenda, conclusions, votes and notes.

Precondition: Minutes records with agenda, people, conclusions, votes and notes; optional reviewed draft.

## MinutesTemplate.Build

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesTemplate.cs:32`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Records of a meeting titled "Họp quý 3" started 2026-11-10T02:00Z (Hanoi 09:00), place "Phòng A", Chair "Nguyễn An", Secretary "Lê Chi", two people present (Chair as Chủ trì, "Trần Bình" as Thành viên).<br>2. No reviewed draft. | records as described; draft: null; zone: Hanoi. | Return: Line-marked text with: the two header rows, "# BIÊN BẢN", "^ **Kết quả cuộc họp Họp quý 3**", "**Thời gian bắt đầu:** 09 giờ 00 phút, ngày 10/11/2026.", "**Địa điểm:** Phòng A.", "**Thành phần tham dự:** 02 người có mặt, gồm:" then "- Nguyễn An, Chủ trì;" and "- Trần Bình, Thành viên.", Chủ trì and Thư ký lines, the three section headings I-III and the signature block; ends with one newline.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · UC-60 · BR-264 |
| UTC002 | Secretary name contains a vertical bar. | records.Secretary: "Lê \| Chi". | Return: Signature row shows "Lê / Chi" so the bar does not split the column.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 |
| UTC003 | No draft; agenda item 1 "Báo cáo" presented by "Phạm Dũng" and item 2 "Khác" with no presenter; no notes. | draft: null; records.Agenda: 2 items. | Return: Section II lists "### 1. Báo cáo" with "Phạm Dũng trình bày. ……", then "### 2. Khác" with "……".<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 |
| UTC004 | No draft; agenda items with recorded conclusions "Duyệt ngân sách" (item "Ngân sách") and none for the other item; two vote result lines. | draft: null; records.Votes: ["Chọn địa điểm: Thông qua", "Mua máy: Hoãn"]. | Return: Section III shows "### 1. Kết luận của chủ trì", "- Ngân sách: Duyệt ngân sách", then "Kết quả biểu quyết:" with one "- " line per vote, then "### 2. Nhiệm vụ, sản phẩm và thời hạn" with "……".<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · BR-264 · BR-392 |
| UTC005 | No draft; the Secretary's notes: an unmarked note on item 1 and a note "Kết luận: Duyệt" and "Nhiệm vụ: Gửi báo cáo" on the same item. | records.Notes: two notes for item "Báo cáo". | Return: Unmarked line sits under the item in section II; "- Báo cáo: Duyệt" goes to the Chair's conclusions and "- Báo cáo: Gửi báo cáo" under "Nhiệm vụ, sản phẩm và thời hạn".<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · UC-82 · BR-392 |
| UTC006 | A reviewed or unreviewed draft with Objectives, Discussion and Conclusions text. | draft: Objectives "Mục tiêu A"; Discussion "1. Báo cáo\nNội dung thảo luận"; Conclusions "1. Kết luận của chủ trì\nDuyệt". | Return: Section I shows "Mục tiêu A"; in section II "1. Báo cáo" becomes "### 1. Báo cáo" followed by the plain line; in section III the sub-heading becomes "### 1. Kết luận của chủ trì"; agenda, notes and conclusions of the records are not used.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-59 · UC-49 |
| UTC007 | Draft whose text imitates the template's marks. | draft: Discussion "# BIÊN BẢN GIẢ\n@@ A \| B\n**đậm**". | Return: Leading "#" and "@" marks and "**" are stripped ("BIÊN BẢN GIẢ", "A \| B", "đậm"); no heading, table or bold results.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-59 · BR-385 |
| UTC008 | Draft discussion with a numbered line of 152 characters after "1. " (too long to be a title). | draft: Discussion "1. " + 152 characters. | Return: Stays a plain line; only numbered lines of at most 151 characters after the number are made sub-headings.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-59 |
