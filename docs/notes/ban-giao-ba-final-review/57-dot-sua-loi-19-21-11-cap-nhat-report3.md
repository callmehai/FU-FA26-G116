# Đợt sửa lỗi 19–21/11/2026: cập nhật Report 3 theo 10 PR

**Người ghi:** agent, theo 5 tin của bot kênh `note-gửi-ba` (19–20/11/2026, đã thả ❌ ngày 21/11). **Ngày:** 21/11/2026.
**Nhánh / worktree:** `docs/report3-dot-sua-loi-19-21-11` tại `../TrustCabinet-note57`, tách từ **`origin/app`** (`23c41181`; `app` chứa toàn bộ `main` + #448). PR của note này đích **`app`**.
**Trạng thái: 🟡 XONG PHẦN SỬA, CHỜ 2 QUYẾT ĐỊNH (1.4 AF, BR khoá tab) + REVIEW TỔNG THỂ** — DOCX + sequence + snapshot xong hết nhóm 1–10; chưa commit/push/Drive, chưa comment Jira (§2 để sau merge).
**Nguồn chuẩn:** DOCX `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`. Không sửa tay `Report-3_*.md`, `05-use-case-specifications.md`, `06-business-rules.md` (sinh bằng `scripts/sync_reports.py`). Mô tả từng PR trên GitHub là nguồn chi tiết.

> **PR #448 (bản tổng hợp Online + MP3) đã vào `app` (`ccde071a`), chưa lên `main`.** Đặc tả làm bình thường trên nhánh `app`. Khi comment Jira cho nhóm 10, ghi rõ "đã vào `app`, chưa lên `main`", giữ nguyên trạng thái ticket.

## 0. Tình trạng đối chiếu `main` (21/11/2026)

| Đã đúng, không làm lại | Còn sai/thiếu trong Report 3 (đo trên extract) |
|---|---|
| `MSG164`, `MSG288` trong catalogue; `docs/03-api/11-tai-lieu-cuoc-hop.md` (10 MB, 10 tệp, 100 MB) | **BR-214** còn "50 MB"; **BR-319** còn "200 MB" |
| `MSG481` và `MSG482` (503, tải bản ghi MP3) trong catalogue trên `app`; "Mã tiếp theo" = `MSG483` | MSG482 chưa có trong DOCX Report 3 |
| **BR-439** đã nói tìm không phân biệt hoa thường/dấu, `d` = `đ` | BR-439 chỉ áp cho *danh sách cuộc họp* và *danh sách người duyệt*; PR #421 áp **mọi ô tìm kiếm** |
| — | **BR-130** "Only an active current Chair… start or end"; **BR-164** "The Chair who pressed Start is not notified"; **UC-21** Primary Actor chỉ `Chair`, mô tả "assigned Chair"; PRE-1, bước 1–2 chỉ Chair |
| — | **BR-443** "does not show the Meeting… an entry is about" (ngược với #421); **UC-13** bước 4 chỉ ghi "the object" |
| — | **BR-422** "Slots fall Monday to Friday" (đổi theo #422: chọn được T7/CN) |
| — | **UC-17** bước 1 "opens the list with the Today tab selected" (sai theo #430) |

Phần "chưa kiểm" ở checklist: mở file ra đối chiếu trước khi sửa, đừng tin cột này.

## 1. Checklist công việc

Quy ước: ☐ chưa làm · mỗi dòng gồm *sửa gì → ở đâu*. Sửa DOCX xong mới chạy bước 2–3.

### Bước 0 — chuẩn bị

- ☐ 0.1 Vào worktree `../TrustCabinet-note57`; đọc `docs/08-reports/README.md` và `docs/DEVELOPMENT_WORKFLOW.md` §2.1–2.2.
- ☐ 0.2 Đọc mô tả đủ của từng PR (`gh pr view <n>`): #420, #421, #422, #423, #424, #425, #427, #430, #436 (+ #448 trên `app`). Ghi mọi câu chữ UI/mã MSG mới vào bước 1.
- ☐ 0.3 Xin PO/Hải xác nhận 2 điểm mơ hồ (xem §4) trước khi viết UC-23.

### Bước 1 — sửa DOCX Report 3 (làm tuần tự, một người một lúc)

**Nhóm 1 · Thư ký bắt đầu họp (#436)** — SCRUM-69 — ✅ XONG 21/11/2026 (xem "Kết quả nhóm 1" ở cuối note)
- ☑ 1.1 **BR-130**: "active current Chair or Secretary… may explicitly start; only the Chair may end".
- ☑ 1.2 **BR-164**: người bấm Bắt đầu (Chủ toạ **hoặc Thư ký**) không nhận thông báo trong ứng dụng (đã nói BR-322); khi **Thư ký** bắt đầu thì **Chủ toạ** nhận thông báo.
- ☑ 1.3 **UC-21**: Primary Actors `Chair, Secretary`; Description, PRE-1, Normal Flow 1–2, EX-1, POST-2/POST-3 nói cả Thư ký; PRE-4 (có Chair + ≥ 1 Secretary) giữ nguyên.
- ☐ 1.4 **TREO — chờ Hải** (§4): "UC-23 AF-3" (lời bot) — UC-23 là *Manage Agenda Items*, UC-21 không có luồng phụ nào. **Chưa thêm** luồng phụ vào UC-21 (Thư ký bắt đầu → Chủ toạ nhận thông báo); làm sau khi Hải xác nhận.
- ☑ 1.5 **MSG112**: catalogue dòng 127–128 đã đúng từ PR #436 ("…không phải active current Chair hoặc Secretary", `Không thể bắt đầu cuộc họp này.`); mục MSG của DOCX Report 3 (dòng 118, `This meeting cannot be started.`) không nhắc vai trò nên **không cần đổi**.
- ☑ 1.6 Sequence UC-21: `UC21_StartMeeting.txt` (actor `Chair / Secretary`, `startMeeting(meetingId, actorId)`, self-check "starter") + tab draw.io (splice bằng `export_sequence.py`, đúng 1 hunk UC-21) + 2 PNG (`UC21_StartMeeting.png` qua sequencediagram.org, `drawio-png/uc21_startmeeting.drawio.png` qua draw.io CLI, đã xem ảnh).

**Nhóm 2 · Phòng họp (#420)** — SCRUM-41 — ✅ XONG 21/11 (xem "Kết quả nhóm 2" ở cuối note)
- ☑ 2.1 **UC-37** (Join Meeting Room): Normal Flow bước 1 — phòng mở ở **tab mới**, bấm lại khi tab đang mở thì đưa tab lên (không mở thêm); trình duyệt chặn popup thì mở ở tab hiện tại. AF-3 bước 3 "When the Chair or the Secretary starts" (nhất quán với BR-130 mới — để mai review chốt).
- ☑ 2.2 **UC-38** (Leave): bước 1 tách hai đường (Leave để rời phòng / nút back ở header phòng để mở trang chi tiết mà không rời); bước 4 đóng tab phòng; thêm **AF-3** (nút back ở header: đưa tab trang chi tiết lên, không hỏi, không ngắt cuộc gọi).
- ☑ 2.3 Màn §III: P663 (mở phòng ở tab mới), P813 (nút back ở header phòng, không rời cuộc gọi), P818 (Leave đóng tab phòng), P661 (nút End **đỏ**); T191 R6 + cột Status 4.3 (Ended **xanh**, viền/border giữ tone muted); T194 (Start: Chair or Secretary — vét sót nhóm 1; End đỏ). Không chụp ảnh app.

**Nhóm 3 · Nhật ký hệ thống (#421)** — SCRUM-16 — ✅ XONG 21/11 (xem "Kết quả nhóm 3" ở cuối note)
- ☑ 3.1 **BR-443**: viết lại — cột Đối tượng hiện **tên** (tiêu đề họp, tên tệp, họ tên chủ giọng…) + mã; mọi thứ của họp **Mật** hiện `Họp Mật`, **không** tìm được theo tên; xem nhật ký vẫn không cấp quyền đọc.
- ☑ 3.2 **BR-441/BR-442**: "object" → tên đối tượng (không tên thì 8 ký tự cuối mã); từ khoá khớp tên/mã (trừ tên của họp Mật), không phân biệt hoa thường/dấu.
- ☑ 3.3 **UC-13**: bước 4, AF-1 (từ khoá), POST-3 theo 3.1; EX giữ nguyên (MSG469/470 không đổi).
- ☑ 3.4 Màn Nhật ký hệ thống: P486, P488, ô Search T189 R6, cột Đối tượng T189 R10 + ví dụ `Họp Mật`.

**Nhóm 4 · Dự thảo AI và biên bản theo mẫu (#421)** — SCRUM-36, SCRUM-32 — ✅ XONG 21/11 (xem "Kết quả nhóm 4" ở cuối note)
- ☑ 4.1 **UC-47**, **UC-59**: chỉ tạo khi họp **đã kết thúc** (PRE-2, NF bước 2, EX-2); UC-47 thêm EX-9 không câu thoại → `MSG481` (409). Bản nháp cũ vẫn sửa được, chỉ chặn tạo mới.
- ☑ 4.2 UC-58 (xem, không tạo) và UC-60 (sửa bản đã tạo) không nhắc điều kiện InProgress — giữ nguyên. `MSG386`/`MSG398` đổi điều kiện "khi cuộc họp đã kết thúc".
- ☑ 4.3 Mục MSG Report 3: thêm `MSG481` (No 489, Form error, UC-47/EX-9); sửa `MSG386`, `MSG398`.
- ☑ 4.4 BR-375/BR-387 → "sau khi họp đã kết thúc" (+ sửa cột nguồn); BR-379 (ít câu vẫn viết, không câu thì không viết, trỏ BR-462); thêm **BR-462** (AI cần ≥ 1 câu thoại).
- ☑ 4.5 Sequence UC-47 (EX-2 + nhánh EX-9 MSG481 mới) và UC-59 (EX-2) + tab draw.io + 2×2 PNG, đã xem ảnh.

**Nhóm 5 · Tìm kiếm không phân biệt hoa thường và dấu (#421)** — SCRUM-18 — ✅ XONG 21/11 (xem "Kết quả nhóm 5" ở cuối note)
- ☑ 5.1 **BR-439**: bỏ giới hạn Meeting list/eligible Approvers → **mọi** ô tìm kiếm (liệt kê từ code: Meeting list, audit q, user list, roster, invitation picker, Chair handover candidates, calendar-share candidates, personal library + folders, mọi Select có tìm kiếm, Find trong minutes editor).
- ☑ 5.2 Cột "UC" của BR-439: UC-05, UC-13, UC-17, UC-18, UC-27, UC-28a, UC-34, UC-60, UC-73, UC-84; UC-17 AF-1 đã trích; thêm trích dẫn vào UC-05/27/34 AF-1, UC-13 AF-1, UC-28a (picker có ô tìm kiếm), UC-84a (candidate search), UC-60 (Find).

**Nhóm 6 · Danh sách cuộc họp mặc định (#430)** — SCRUM-18 — ✅ XONG 21/11 (xem "Kết quả nhóm 6" ở cuối note)
- ☑ 6.1 **UC-17** Normal Flow bước 1: bỏ "opens the list with the Today tab selected"; mở **không lọc** (mọi trạng thái, mọi thời điểm); thẻ `Họp hôm nay` vẫn bấm để lọc hôm nay (AF-5); AF-3 thêm lời nhắn "Hôm nay không có cuộc họp nào." khi chỉ lọc Hôm nay.
- ☑ 6.2 BR liên quan mặc định/lọc: BR-30 (khung Hôm nay), BR-32 (sort) vẫn đúng — không BR nào ghi "mở mặc định Hôm nay", không sửa.
- ☑ 6.3 Màn 4.3 (gạch Filter Meetings + dòng Time filter: mở với "Mọi thời điểm"); note 40 giữ nguyên lịch sử (đã là bản nháp, PR #430 tuyên bố hết hiệu lực); **note 54 T7**: thêm dòng cập nhật "đã đổi theo #430". `docs/03-api/05-quan-ly-cuoc-hop.md`: đã đúng từ PR #430, chỉ sửa nhầm số PR (#287 → #430).

**Nhóm 7 · Lịch, chia sẻ lịch, tìm giờ (#422, #425, #430)** — SCRUM-137, SCRUM-142 — ✅ XONG 21/11 (xem "Kết quả nhóm 7" ở cuối note)
- ☑ 7.1 **UC-85c / UC-84f**: lịch được chia sẻ hiện đủ **7 ngày, 0–24 giờ, mở ở 08:00**; tab/lists **tự tải lại** khi có người đồng ý/đổi/dừng chia sẻ (AF-2 UC-85c, AF-4 UC-84f; AF-3 UC-84f tải lại một lần trước khi báo mất).
- ☑ 7.2 **UC-85a** + **BR-422**: ngày trong tuần chọn được T7/CN (1–7); **mặc định vẫn T2–T6**, giờ 08:00–17:00 và nghỉ trưa 12:00–13:00 giữ nguyên (đối chiếu `MeetingSlotRules`: weekdays 1–7, default [1..5], lunch không đổi).
- ☑ 7.3 **UC-85a** bước 1: nhãn `Giãn cách họp` (#425; đối chiếu note 54 T8: nhãn cũ `Cách họp khác` → `Cách cuộc họp khác` (#420) → `Giãn cách họp` (#425)). Màn 16.7 **không tồn tại** trong DOCX Report 3 (không có §III cho FE-16) → chỉ sửa UC.
- ☑ 7.4 UC-84f bước 3 (không có màn UC-84 trong DOCX): "Xin đổi" là chip cùng hàng (vd `Xin đổi: …`), mọi dòng chia sẻ một khuôn.
- ☑ 7.5 `docs/03-api/23-chia-se-lich.md`, `24-tim-gio-hop.md`: đã đúng từ PR #422 (7 ngày 00:00–24:00; weekdays 1–7 + cuối tuần từ 19/11) — không sửa.

**Nhóm 8 · Tải lên, đọc tài liệu (#423)** — SCRUM-107, SCRUM-21 — ✅ XONG 21/11 (xem "Kết quả nhóm 8" ở cuối note)
- ☑ 8.1 **BR-214**: 50 MB → **10 MB**; **BR-319**: 200 MB → **100 MB** (10 tệp giữ nguyên).
- ☑ 8.2 **MSG164**, **MSG288** trong mục MSG Report 3: đã sửa 10 MB / 100 MB (DOCX gốc vẫn ghi 50/200 — extract cũ sai; catalogue đã đúng từ PR).
- ☑ 8.3 **UC-33a, UC-33b, UC-36** (AF-1, EX-3/EX-8, NF, EX-1/EX-4: 10 MB / 100 MB / khai báo > 101 MB) + **UC-76a AF-1** (ngoài checklist nhưng PR nêu rõ — đã sửa 10 MB).
- ☑ 8.4 **UC-32c** bước 3: khung xem trên trang, đóng ✕ / Esc / Back; **không mở tab mới** (kể cả panel Tài liệu trong phòng họp — màn 8.6; Ctrl/Cmd-click vẫn mở tab riêng). BR-315 giữ nguyên.
- ☑ 8.5 Sequence + class diagram: 4 txt (UC-33a/33b/36/76a: 10 MB / 100 MB / 101 MB) + tab draw.io + 8 PNG; class diagram `DocumentFileRules` (10/100/10 MB) + PNG; README document-management + README 07-classdiagram.

**Nhóm 9 · Tab Bản ghi (#424) và khoá tab (#427)** — SCRUM-31, SCRUM-29, SCRUM-32 — ✅ XONG PHẦN CHẮC 21/11, **TREO: hỏi BR mới hay không** (xem "Kết quả nhóm 9" + §4.2)
- ☑ 9.1 **UC-43a/43b, UC-45a**: câu liền nhau của một người gộp thành một đoạn; đang lọc theo người thì nghe một câu rồi dừng; khi phát tô từng từ nếu bản ghi có mốc; dấu câu của câu trước có thể được chấm lại khi câu sau tới.
- ☐ 9.2 **UC-43a** và các UC chi tiết cuộc họp (UC-58 biên bản, UC-16 — UC-17 không có tab nên bỏ): **tab Bản ghi khoá đến khi họp bắt đầu** (Thư ký đang thu giọng mẫu thì mở sớm — UC-44/SCRUM-152), **Biên bản khoá đến khi họp kết thúc**, **họp đã huỷ khoá cả hai**; link tới tab đang khoá mở **Thông tin chung**; nút khoá nào cũng có lý do. **TREO: BR mới (số kế tiếp) hay chỉ ghi trong UC? — chờ trả lời, chưa tạo BR.**
- ☑ 9.3 Màn chi tiết cuộc họp trong Report 3 §III (4.5 Tabs): câu chữ lý do khoá đọc từ `MeetingDetailPage.tsx` (không bịa).

**Nhóm 10 · Bản tổng hợp Online + MP3 (#448, đã vào `app`)** — SCRUM-29, SCRUM-153 — ✅ XONG 21/11 (xem "Kết quả nhóm 10" ở cuối note)
- ☑ 10.1 **UC-43c** (Download Meeting Recording): tải về luôn **MP3** (trước là tệp WAV gốc; MP3 có sẵn đi thẳng, còn lại chuyển trên đường tải); không chuyển được → `MSG482` (EX-3 mới).
- ☑ 10.2 **UC-43a/43b**: bản tổng hợp có cả họp **Online** (server ghép lời mọi người theo giờ thật sau khi họp kết thúc; họp Online không trộn bản ghi phòng cũ); tải được (MP3).
- ☑ 10.3 **MSG482** vào mục MSG của Report 3 (No 490, Form error, UC-43c/EX-3; catalogue đã có dòng 552, "Mã tiếp theo" = `MSG483`).
- ☑ 10.4 Sequence dựng bản tổng hợp (SYS, Hybrid-or-Online + điều kiện mix) và sequence tải bản ghi (UC43c: MP3 + EX-3 MSG482) + UC43a view (Online hiện combined) + tab draw.io + 6 PNG, đã xem UC43c.
- ☑ 10.5 SCRUM-153: BR-456 (Hybrid → Hybrid/Online) + UC-43a Description/AF-3.

### Bước 2 — snapshot và docs kèm theo — ✅ XONG 21/11 (chạy một lần cuối)

- ☑ 2.1 `python scripts/check_docx.py docs/08-reports/source/Report_3_Software_Requirement_Specification.docx` (phải OK).
- ☑ 2.2 `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3` (sinh lại `Report-3_*.md`, `05-…`, `06-…`, `docs/uc-map.html`).
- ☑ 2.3 `docs/03-api/00-danh-muc-ma-thong-bao.md`: catalogue đã đúng từ các PR (MSG112, MSG386/398, MSG481, MSG482) — không sửa thêm; DOCX tự thêm dòng MSG tương ứng (No 489 MSG481, No 490 MSG482).
- ☑ 2.4 `docs/03-api/` các file lệch: 05 (sửa nhầm số PR #287 → #430), 24 (nhãn "Cách họp khác" → "Giãn cách họp"); 17/23/11/recording contract đã đúng từ PR — chỉ kiểm.

### Bước 3 — sơ đồ — ✅ XONG 21/11 (làm theo từng nhóm)

- ☑ 3.1 Danh sách sơ đồ cần sửa: UC21 (Thư ký), UC47 + EX-9 mới, UC59, SYS dựng bản tổng hợp (Online), UC43c tải bản ghi (MP3 + EX-3), UC43a view (Online hiện combined — ngoài checklist, code bắt buộc), UC33a/33b/36/76a (10 MB/100 MB/101 MB), class FE-07 (DocumentFileRules). UC-37/38, UC-13, UC-84/85: không đổi (xem Kết quả từng nhóm).
- ☑ 3.2 Sửa `.txt` + tab trong `TrustCabinet_Sequence_Diagrams.drawio` theo `DRAWIO_EDITING_RULES.md`; xuất PNG (sequencediagram.org + draw.io CLI); render xem lại (UC-21, UC-47, UC-59, UC-33a, UC-43c, SYS, UC-76a, UC-43a, class FE-07 — UC-33b/36 xem ở mức txt + XML well-formed vì cùng mẫu); XML well-formed + ID hợp lệ; `git diff --check` sạch.

### Bước 4 — kiểm tra trước PR — ✅ XONG 21/11

- ☑ 4.1 `dotnet test --filter MessageCodeCatalogue` 65/65 + `python scripts/check_docx.py` OK (cảnh báo nền: 8 đoạn thừa dấu cách, 70/76 ảnh thiếu alt text).
- ☑ 4.2 Grep còn số cũ: `50 MB|200 MB` trong `docs/01-requirements docs/03-api docs/06-sequencediagram docs/07-classdiagram` — sạch, trừ ghi chú lịch sử "trước là…" có chủ đích; `Today tab selected`, `Slots fall Monday`, `Cách họp khác` cũ — sạch (chỉ còn trong note lịch sử/workstream khác, ngoài phạm vi).
- ☑ 4.3 Đọc lại từng nhóm so với mô tả PR; ghi vào note 57 mục "Kết quả" khi xong: mỗi nhóm sửa gì, còn gì.

### Bước 5 — PR

- ☐ 5.1 Commit (tiếng Việt, theo lịch sử repo) — **chỉ khi Khánh bảo commit**. Push + mở PR đích `main`, tiêu đề tiếng Anh, mô tả tiếng Anh, nêu: PR nào đã phản ánh, #448 chờ `main`.
- ☐ 5.2 Không đẩy Drive (`push_reports.py`) khi chưa được yêu cầu.

## 2. Sau khi PR sửa được merge — Jira (DEVELOPMENT_WORKFLOW §2.2)

Làm **sau** khi `git log --oneline origin/app` thấy merge commit của PR này (không dựa vào trang PR); ghi rõ nhánh (`app`/`main`) trong comment. Mỗi ticket **một** comment: UC/BR/MSG đổi gì, PR + commit, còn gì. Tất cả ticket dưới đây hiện đã `Done` → **chỉ comment, không đổi trạng thái** (ticket đã `Done` thì giữ `Done`; nếu thấy ticket ở `BA Final Review` thì `→ In Review` cho Tester, ticket chỉ có tài liệu thì `Done`).

| Ticket | Nhóm | Nội dung comment |
|---|---|---|
| SCRUM-69 | 1 | BR-130, BR-164, UC-21 (+ luồng phụ), MSG112, sequence UC21 |
| SCRUM-41 | 2 | UC-37/38 tab mới, nút Trang cuộc họp, rời phòng |
| SCRUM-16 | 3 | BR-441–443, UC-13 hiện tên đối tượng, họp Mật |
| SCRUM-36 | 4 | UC-47 điều kiện đã kết thúc + có câu thoại, MSG481 |
| SCRUM-32 | 4, 9 | UC-58–60 chỉ tạo khi đã kết thúc; tab Biên bản khoá |
| SCRUM-18 | 5, 6 | BR-439 mở rộng, UC-17 mặc định không lọc |
| SCRUM-137 | 7 | UC-85a, BR-422 (T7/CN), nhãn Giãn cách họp |
| SCRUM-142 | 7 | UC-84, UC-85c (7 ngày, tự cập nhật) |
| SCRUM-107 | 8 | BR-214/BR-319 (10 MB, 100 MB), MSG164, MSG288 |
| SCRUM-21 | 8 | UC-32c khung xem trên trang |
| SCRUM-31 | 9 | Bản ghi: đoạn theo người, tô từng từ, nghe một câu |
| SCRUM-29 | 9, 10 | Khoá tab Bản ghi; MP3 + MSG482 (UC-43c) — *ghi "đã vào `app`"* |
| SCRUM-153 | 10 | Bản tổng hợp thêm họp Online — *ghi "đã vào `app`"* |

Quy tắc: ticket nào chỉ một phần vào `main` (code có, tài liệu chưa, hoặc #448 chưa lên) thì để nguyên trạng thái và ghi phần còn thiếu trong comment. Sau comment: cập nhật bảng trạng thái trong README note, đổi dòng đầu note này sang ✅ XONG, rồi **đổi ❌ → ✅** cho 5 tin Discord (id trong §3), nhóm 10: ✅ khi PR đã vào `app`.

## 3. Tin Discord đã thả ❌ (21/11/2026)

Kênh `note-gửi-ba` (`1562073406482948188`):
`1573056876734844941` (#420–#425) · `1573078668077695088` (#422–#427) · `1573308300777492541` (#430) · `1573343903170699335` (#436) · `1573357998242406665` (#448).
Gỡ ❌ bằng REST (`DELETE …/reactions/%E2%9D%8C/@me`), rồi thêm ✅ — xem memory `discord-note-gui-ba-reactions`.

## 4. Điểm cần xác nhận với Hải

1. "UC-23 luồng phụ AF-3": UC-23 là *Manage Agenda Items*, không dính Bắt đầu họp. Có phải ý là UC-21 (chưa có luồng phụ)? → **Nhóm 1 giữ nguyên Alternative Flows = N/A** cho tới khi Hải trả lời.
2. Khoá tab (#427) cần BR mới hay chỉ ghi trong UC chi tiết cuộc họp? (đề xuất: BR mới.)
3. Phạm vi "mọi ô tìm kiếm" của #421 — liệt kê danh sách cụ thể để ghi vào BR-439.

## 5. Kết quả theo nhóm (ghi sau mỗi nhóm, trước khi review diff)

### Kết quả nhóm 1 — Thư ký bắt đầu họp (#436, SCRUM-69) — 21/11/2026

- **Đối chiếu code thật:** `MeetingLifecycleService.TransitionAsync` — Start chấp nhận active current Chair **hoặc** Secretary, End chỉ Chair; history ghi đúng người bấm; `MeetingStarted` gửi cho mọi người trừ người bấm (Thư ký bấm → Chủ toạ nhận). FE `lifecycleAction`: "Bắt đầu họp" hiện cho Chair + Secretary ở Published, "Kết thúc họp" chỉ Chair. Catalogue `MSG112` (403) đã đúng từ PR #436.
- **Sửa trong DOCX** (`docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`): BR-130 (Secretary được bắt đầu, chỉ Chair được kết thúc); BR-164 (người bấm Start không nhận thông báo; Thư ký bấm → Chủ toạ nhận); UC-21 (Primary Actors `Chair, Secretary`; Description, PRE-1, Normal Flow 1–2 + 4, EX-1, POST-2/POST-3; PRE-4 và Alternative Flows = N/A giữ nguyên); nút Start ở màn hình (T198) + 3 đoạn màn hình §III (P564, P616, P655). MSG112 trong DOCX không nhắc vai trò → không đổi.
- **Sơ đồ:** `UC21_StartMeeting.txt` (actor `Chair / Secretary`, `startMeeting(meetingId, actorId)`); tab draw.io splice bằng `export_sequence.py` (đúng 1 hunk UC-21, XML well-formed, PNG đã xem: actor kép + self-check "starter" đúng); 2 PNG xuất lại (`UC21_StartMeeting.png`, `uc21_startmeeting.drawio.png`).
- **Kiểm tra:** `check_docx.py` OK (cảnh báo cũ: 8 đoạn thừa dấu cách, 70/76 ảnh thiếu alt text); `sync_reports.py --report 3` xong (UC-21 + BR-130/BR-164 được làm mới; inventory khớp 100%, catalogue khớp 100%); `MessageCodeCatalogueTests` 65/65 qua.
- **Còn lại:** 1.4 treo (chờ Hải, §4.1); catalogue + `docs/03-api/05-quan-ly-cuoc-hop.md` đã đúng từ PR #436 nên không sửa thêm. Chưa commit/push/Drive, chưa làm phần Jira (§2).

### Kết quả nhóm 2 — Phòng họp, tab mới (#420, SCRUM-41) — 21/11

- **Đối chiếu code thật (PR đã vào `app`):** `features/calls/meetingTabs.ts` + `TabLinks.tsx` mới — mọi điểm vào qua `RoomLink` mở tab mới, bấm lại đưa tab lên không tải lại; popup bị chặn thì về điều hướng cùng-tab; nút `‹ Trang cuộc họp` đưa tab trang họp lên mà không rời cuộc gọi; Rời phòng đóng tab phòng. `MeetingLifecycleActions`: nút kết thúc `btn-danger` (đỏ). `StatusBadge`: Ended tone emerald (xanh, icon double-tick phân biệt với "Đã duyệt"); calendar/dot/border (`STATUS_TONE`) giữ nguyên nên past meeting vẫn muted.
- **Sửa trong DOCX:** UC-37 (bước 1: tab mới + đưa tab lên + fallback chặn popup; AF-3 bước 3 "Chair or Secretary" cho nhất quán BR-130 — để mai review chốt); UC-38 (bước 1 tách hai đường, bước 4 đóng tab phòng, thêm AF-3 nút back ở header); màn §III (P663, P813, P818, P661 nút End đỏ; T191 R6 + cột Status 4.3 Ended xanh + viền/dot muted); T194 vét sót nhóm 1 (Start: Chair or Secretary) + End đỏ.
- **Không đụng:** sequence UC-37/UC-38 (tab là điều hướng boundary, không phải tương tác service — theo convention §2.1 README sequence); catalogue + `docs/03-api/09` (PR #420 không đổi backend/MSG — 09 đã được PR cập nhật).
- **⚠️ Sự cố và cách khắc phục:** script vá lỗi lặp chữ của tôi gán `run.text` vô điều kiện cho mọi run, làm mất 76 hình trong `document.xml` (phát hiện nhờ cảnh báo alt-text biến mất + đếm `w:drawing` 152 → 0). Đã khôi phục DOCX từ backup trước nhóm 1, viết lại script an toàn (mỗi run duy nhất, chỉ gán khi run chứa chuỗi cũ), chạy lại toàn bộ sửa nhóm 1+2, kiểm tra: chuỗi cũ hết, chuỗi mới đủ, drawings 152, paras/tables nguyên, `check_docx` OK đúng cảnh báo nền. Bài học: không bao giờ gán `run.text` vô điều kiện trong DOCX có hình.
- **Còn lại:** item 4 (icon raise-hand) và item 5 (nhãn "Cách họp khác" → "Cách cuộc họp khác") của PR #420 ngoài checklist — item 5 để sang nhóm 7 đối chiếu cùng panel tìm giờ.

### Kết quả nhóm 3 — Nhật ký hệ thống (#421 §2, SCRUM-16) — 21/11

- **Đối chiếu code thật (PR đã vào `app`):** `AuditLogQueryService` đặt tên mọi loại đối tượng (tiêu đề họp, tên tệp, tiêu đề biểu quyết, họ tên chủ giọng, "chủ lịch → người xem", tên được mời…), đọc dòng còn sống trước, dòng đã xoá lấy tên trong `details`/`meetingId`; họp Mật: `objectName: null`, `objectSecret: true`, FE hiện `Họp Mật · #<8 ký tự cuối>`, khoá tên bị bỏ khỏi `details` và `q`. `docs/03-api/21` đã được PR cập nhật đúng.
- **Sửa trong DOCX:** BR-443 (viết lại: hiện tên + mã, họp Mật chỉ `Họp Mật` và không tìm theo tên, xem log không cấp quyền); BR-441 (tên hoặc 8 ký tự cuối mã; họp Mật chỉ `Họp Mật`); BR-442 (từ khoá khớp mã/tên/details, không phân biệt hoa thường/dấu, tên họp Mật không khớp); UC-13 (bước 4, AF-1, POST-3 "dù nêu tên vẫn không cấp quyền"); màn (P486, P488, T189 R6 ô Search, T189 R10 cột Đối tượng).
- **Không đụng:** sequence UC-13 (không đổi luồng, chỉ đổi nội dung hiển thị/tìm kiếm); catalogue (PR §2 không thêm/sửa mã nào); `docs/03-api/21` (PR đã cập nhật).
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings 152 còn nguyên (rút kinh nghiệm nhóm 2: chỉ gán run chứa chuỗi cũ, mỗi run một lần).

### Kết quả nhóm 4 — Dự thảo AI và biên bản theo mẫu (#421 §4, SCRUM-36/32) — 21/11

- **Đối chiếu code thật (PR đã vào `app`):** `SummaryService`/`MinutesService` chặn tạo khi chưa Ended (giữ `MSG386`/`MSG398`, đổi điều kiện "khi cuộc họp đã kết thúc"); AI thêm chặn `readiness.sentences = 0` → `MSG481` (409); bản mẫu không cần câu; bản đã tạo vẫn sửa được. Catalogue đã đúng từ PR (`MSG481` dòng 644, `MSG386` dòng 635, `MSG398` dòng 653, "Mã tiếp theo" `MSG483`) — không sửa thêm.
- **Sửa trong DOCX:** UC-47 (PRE-2 Ended; EX-2 "chưa kết thúc"; AF-4 "ít câu vẫn viết"; thêm EX-9 không câu thoại → MSG481); UC-59 (PRE-2 Ended; EX-2 "chưa kết thúc"); UC-58/60 không nhắc điều kiện InProgress nên giữ nguyên; BR-375/BR-387 → "sau khi họp đã kết thúc" (+ cột nguồn); BR-379 (ít câu vẫn viết, không câu thì không viết, trỏ BR-462); thêm **BR-462** (AI cần ≥ 1 câu thoại, UC-47); MSG Report 3 (sửa `MSG386`/`MSG398`, thêm `MSG481` No 489/Form error/UC-47 EX-9).
- **Sơ đồ:** `UC47_GenerateMeetingSummary.txt` (self-check Ended; EX-2 mới; thêm nhánh EX-9 MSG481) và `UC59_GenerateMeetingMinutes.txt` (self-check + EX-2 mới) + tab draw.io (đúng 2 tab UC-47/UC-59, XML well-formed) + 4 PNG (2 sequencediagram.org + 2 drawio-png, đã xem ảnh: EX-2/EX-9 đúng chữ).
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings 152; text PASS (chuỗi cũ hết, chuỗi mới đủ, BR-462 + MSG481 có mặt).

### Kết quả nhóm 5 — Tìm kiếm không phân biệt hoa thường và dấu (#421 §1, SCRUM-18) — 21/11

- **Đối chiếu code thật (PR đã vào `app`):** SQL `lower(unaccent(...))` strpos tại 5 chỗ — Meeting list (UC-17), audit q (UC-13), personal library (UC-34), participant invitation picker (UC-28a), calendar-share candidates (UC-84); `SearchText.Fold` in-memory tại participant roster (UC-27: tên hiển thị/email/tên được mời, Guest chỉ khớp tên) và Chair handover candidates (UC-73); FE `lib/search.ts` dùng tại user list (UC-05: tên/email), Select dropdowns, library folders, Find trong minutes editor (UC-60, fold từng ký tự giữ vị trí khớp). PR #421 §1 không thêm/sửa MSG.
- **Sửa trong DOCX:** BR-439 viết lại phạm vi (liệt kê đủ mặt trên, giữ câu ví dụ + quy tắc %/_); cột UC: UC-05, UC-13, UC-17, UC-18, UC-27, UC-28a, UC-34, UC-60, UC-73, UC-84; cột nguồn thêm SCRUM-18; cột lĩnh vực để trống như BR-30/31/32. Trích dẫn BR-439 tại UC-05 AF-1, UC-27 AF-1, UC-34 AF-1, UC-13 AF-1, UC-28a bước 2 (picker có ô tìm kiếm thật: `pick-search` + `editor-options?q=`), UC-84a bước 1 (candidate search thật: `share-candidates?q=` + ô "Tìm theo tên hoặc email"), UC-60 bước 2 (Find). UC-17 AF-1 và UC-18 đã trích từ trước.
- **Không đụng:** sequence (không đổi luồng), catalogue, `docs/03-api/*` (PR đã cập nhật).
- **Lưu ý:** UC-73 hiện tại bàn giao qua dòng roster (ChangeRoleDialog, chưa có ô tìm kiếm riêng) nhưng endpoint candidates đã hỗ trợ `Search` nên vẫn liệt kê trong phạm vi BR-439; thư mục (UC-72) gộp vào "personal library" của UC-34 vì ô tìm kiếm thư mục nằm ở màn thư viện.
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings 152.

### Kết quả nhóm 6 — Danh sách cuộc họp mở không lọc (#430, SCRUM-18) — 21/11

- **Đối chiếu code thật (PR đã vào `app`):** `listParams.ts` (`DEFAULT_STATE.when = ''`, bỏ sentinel `all`); thẻ `Họp hôm nay` vẫn lọc hôm nay; link cũ `?when=all`/`?status=all` đọc là bỏ lọc. `docs/03-api/05` đã được PR cập nhật đúng (chỉ sai số PR tham chiếu).
- **Sửa trong DOCX:** UC-17 (bước 1 mở không lọc; bước 3 "tiêu chí đã chọn", bỏ "preset"; AF-3 thêm lời nhắn Hôm nay; AF-5 thẻ Hôm nay lọc hôm nay); màn 4.3 (gạch Filter Meetings + dòng Time filter: mở với "Mọi thời điểm").
- **Không đụng:** BR (BR-30 khung Hôm nay, BR-32 sort vẫn đúng; không BR nào ghi mở mặc định Hôm nay); sequence UC-17 (không đổi luồng); catalogue (PR #430 không thêm/sửa MSG); note 40 (giữ lịch sử bản nháp).
- **Sửa kèm:** note 54 T7 thêm dòng cập nhật "đã đổi theo #430"; `docs/03-api/05` dòng 538: `(PR #287)` → `(PR #430)` (số PR gỡ mặc định Hôm nay).
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings 152.

### Kết quả nhóm 7 — Lịch, chia sẻ lịch, tìm giờ (#422, #425, #430) — 21/11

- **Đối chiếu code thật (các PR đã vào `app`):** `SharedCalendarService` trả 7 ngày 00:00–24:00, cắt busy qua nửa đêm; `SharedWeek` 7 cột 24 giờ mở ở 08:00, tự đọc lại khi có `CalendarShare*` (#430); `CalendarSharingPage` tải lại lists khi có notification + tải lại một lần trước khi kết luận mất + mở lại notification cũ cuộn tới dòng (#422 §2); `ShareRow` một khuôn, chip `Xin đổi` cùng hàng, highlight mờ sau ~3s; slot search mặc định T2–T6 08:00–17:00, `weekdays` 1–7, lunch 12–13 không đổi (`MeetingSlotRules`); nhãn buffer `Giãn cách họp` (#425). Không MSG mới ở các PR này.
- **Sửa trong DOCX:** BR-422 (mặc định T2–T6, chọn được T7/CN); UC-85a bước 1 (weekdays + `Giãn cách họp`); UC-85c bước 2–3 (7 ngày 00–24h, mở ở 08:00) + AF-2 tự tải lại; UC-84f AF-3 (tải lại một lần) + AF-4 (tự tải lại) + bước 3 (một khuôn, chip `Xin đổi`).
- **Không đụng:** sequence (không có trong danh sách Bước 3); catalogue; `docs/03-api/23/24` (PR đã cập nhật); màn 16.7 + màn UC-84 (không có trong DOCX — Report 3 chưa có §III cho FE-16); note 54 T8 (ngoài phạm vi checklist — nhãn cũ ở đó giờ đã thành `Giãn cách họp` theo #425, để workstream note 54 tự xử).
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings 152; text PASS (không còn "Monday to Friday" cũ/"Cách họp khác", AF-2/AF-4 mỗi thứ một bản).

### Kết quả nhóm 8 — Tải lên, đọc tài liệu (#423, SCRUM-107/21) — 21/11

- **Đối chiếu code thật (PR đã vào `app`):** `DocumentFileRules` (10 MB/tệp, 10 tệp, 100 MB/lần; Kestrel 101 MB); `DocumentOverlayProvider` + `<dialog>` + history state (Back đóng, ✕/Esc đóng, Ctrl-click tab riêng; ADR-0066 thay ADR-0011 §6); catalogue `MSG164`/`MSG288` + `docs/03-api/11/14` + inventory đã đúng từ PR. Note 57 §0 nói extract đã 10/100 MB là **sai** — DOCX gốc vẫn 50/200, đã sửa.
- **Sửa trong DOCX:** BR-214 (10 MB), BR-319 (10 tệp/100 MB); MSG164/MSG288 (+ "khai báo quá 101 MB"); UC-33a (AF-1 bước 1+3, EX-3, EX-8), UC-33b AF-1, UC-36 (NF bước 3, EX-1, EX-4), UC-76a AF-1, UC-32c bước 3 (khung xem trên trang), UC inventory UC-36, màn 7.4, File hint/Upload/Upload area + P774 + T239 (10 MB), màn 8.6 (không tab mới). Dòng lịch sử Change Log 22/10 + 27/10 giữ nguyên số cũ (chứng cứ thời điểm).
- **Sơ đồ + README:** 4 txt + tab draw.io (đúng 4 tab mới, XML well-formed, `w:drawing` drawio sạch số cũ) + 8 PNG (4 sequencediagram.org + 4 drawio-png, đã xem UC33a: EX-8 101 MB + MSG đúng); class diagram `DocumentFileRules` (10/100/10 MB) + PNG xuất lại bằng draw.io CLI `--scale 2` + nén palette (413 KB, cùng cỡ các diagram khác) + đã xem ảnh; README document-management (2 chỗ) + README 07-classdiagram (1 chỗ, giữ "trước là…" làm mốc).
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings DOCX 152; text PASS (không còn 50/200/201 MB ngoài 2 dòng lịch sử).

### Kết quả nhóm 9 — Bản ghi + khoá tab (#424, #427) — 21/11 (phần chắc; BR treo §4.2)

- **Đối chiếu code thật (các PR đã vào `app`):** #424 — câu liền kề một người gộp đoạn (tooltip giờ, tool theo câu), lọc theo người thì dừng sau một câu, karaoke khi có `wordStartsMs`, chấm lại câu trước khi câu sau tới (chưa sửa); #427 — `tabLock`: Bản ghi khoá tới khi bắt đầu ("Mở khi cuộc họp bắt đầu"), Biên bản tới khi kết thúc ("Mở khi cuộc họp kết thúc"), huỷ khoá cả hai ("Cuộc họp đã huỷ"), ngoại lệ Thư ký thu giọng mẫu (VoiceCollector) mở sớm Bản ghi, link tab khoá mở Thông tin chung. Không MSG/API/BR mới ở cả hai PR.
- **Sửa trong DOCX:** UC-43a bước 3 (gộp đoạn + tooltip + tool + karaoke + chấm lại câu trước), UC-43b bước 3 (dừng sau một câu khi lọc) + bước 4 (karaoke), UC-45a bước 4 (gộp đoạn + karaoke); UC-16 thêm AF-1 (tab khoá + lý do + ngoại lệ + link mở Thông tin chung); UC-58 + UC-48 bước 1 (tab khoá tới khi Ended); màn 4.5 dòng Tabs (câu chữ lý do khoá). UC-17 không có tab → bỏ.
- **Không đụng:** sequence (không đổi tương tác service; Bước 3 không liệt kê); catalogue; BR — **TREO theo §4.2**: đề xuất BR mới cho mốc mở tab (Bản ghi: InProgress; Biên bản: Ended; huỷ: khoá cả hai) hay chỉ ghi trong UC? Chờ trả lời, chưa tạo BR-463.
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings 152.

### Kết quả nhóm 10 — Bản tổng hợp Online + MP3 (#448, SCRUM-29/153) — 21/11

- **Đối chiếu code thật (PR đã vào `app`, đích `app`):** `CombinedRecordingService` dựng cho Online (trộn câu theo giờ thật, không nguồn phòng; cần ≥ 1 câu giữ lại; Hybrid cần cả hai phía — một phía thì thôi; xoá khi hết nguồn), giữ 8 ngày/1 phút/quyền/sở hữu theo ADR-0062/0067; tải về luôn MP3 (MP3 lưu sẵn đi thẳng, còn lại ffmpeg chuyển trên đường tải; `format="mp3"` trong audit), `MSG482` khi thiếu/hỏng ffmpeg; FE nút "Tải MP3", tab Bản ghi của họp Online hiện bản tổng hợp. Catalogue (`MSG482` dòng 552, "Mã tiếp theo" `MSG483`), `docs/03-api/12`, ADR-0069, ERD note đã đúng từ PR.
- **Sửa trong DOCX:** UC-43c (Description/POST-1/bước 4 MP3 + EX-3 MSG482), UC-43a Description + AF-3 (Hybrid/Online, tải được MP3), BR-456 (Hybrid → Hybrid/Online + Online theo SCRUM-29) + C3 BR-456/BR-458, MSG Report 3 thêm `MSG482` (No 490/Form error/UC-43c EX-3).
- **Sơ đồ:** SYS (Hybrid-or-Online + điều kiện mix đúng code) + UC43c (MP3 + EX-3 MSG482) + UC43a view (Online hiện combined — ngoài checklist nhưng code bắt buộc, đã nêu) + tab draw.io (đúng 3 tab, XML well-formed) + 6 PNG (3 sequencediagram.org + 3 drawio-png, đã xem UC43c: EX-3 MSG482 + MP3 đúng).
- **Còn lại:** ghi "đã vào `app`" khi comment Jira cho SCRUM-29/SCRUM-153 sau merge (§2 note 57 — không làm trong goal này).
- **Kiểm tra:** `check_docx.py` OK đúng cảnh báo nền; drawings 152.
