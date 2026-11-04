# SCRUM-129 — Khách ghi âm theo micro của mình; ERD còn thiếu `speech_clips`

**Ghi ngày:** 03/11/2026. **Nguồn:** tin nhắn bot Slack ngày 03/11/2026, [PR #245](https://github.com/callmehai/FU-FA26-G116/pull/245)
(merged 15:01 03/11/2026), comment Jira `10418` trên [SCRUM-129](https://sep490g116.atlassian.net/browse/SCRUM-129) và
[ADR-0051](../../05-decisions/0051-khach-ghi-theo-micro-cua-minh.md).

Note này gom việc BA. **Ghi note ngày 03/11/2026; sửa tài liệu ngày 04/11/2026** trên nhánh
`docs/notes-scrum-129-erd` (mục 3–4), trừ dòng comment Jira cuối cùng.

## 1. Bot báo gì

- SCRUM-129 (Khách ghi âm theo micro trong phòng họp trực tuyến) đã merge ở #245.
- **7 điểm lệch** so với ticket, ghi đủ ở comment `10418` — **đọc comment này trước khi sửa đặc tả**; note dưới đây chỉ
  chép 3 điểm bot tóm tắt, không thay comment.
- Ba điểm chính:
  1. Khách chỉ được ghi sau khi đã **xác nhận thông báo ghi âm** (`400 MSG380`), và chỉ khi cuộc họp `Online`/`Hybrid`
     đang diễn ra (`409 MSG320`).
  2. Khách **đọc, nghe lại và xoá được câu của mình**; câu của người khác trả `404`.
  3. **Không có mã thông báo mới**; quyết định nằm ở **ADR-0051**.
- Docs cần thêm: `02-erd.md` chưa có bảng `speech_clips` (thiếu từ SCRUM-109).

## 2. Tài liệu đã khớp code (không cần sửa)

- [03-api/12 §Khách](../../03-api/12-ban-ghi-cuoc-hop.md#khách-scrum-129-adr-0051) — bảng endpoint theo token Khách,
  `400 MSG380` / `409 MSG320` / `404`, `speakerIsGuest`, câu gắn với lời mời (`ck_speech_clips_one_speaker`), tệp
  `.txt` ghi "Tên (Khách)", Khách không nhận `speechChanged`.
- [03-api/00 danh mục mã](../../03-api/00-danh-muc-ma-thong-bao.md) — `MSG320` thêm nhánh Khách; `MSG380` dùng lại cho
  câu của Khách khi phiên chưa xác nhận thông báo (cùng một tình huống, đúng quy tắc một mã một tình huống).
- `docs/03-api/09-phong-hop-truc-tuyen.md`, `docs/03-api/16-truy-cap-khach-uc04.md` và `docs/05-decisions/README.md` theo
  cùng PR.

## 3. Đã sửa ngày 04/11/2026 (nhánh `docs/notes-scrum-129-erd`)

### 3.1 `02-erd.md` — thêm `SpeechClip`, `VoiceConsent`, `VoiceSample`

- `SpeechClip` có thật từ SCRUM-109, SCRUM-129 thêm `guest_invitation_id` cùng ràng buộc
  `ck_speech_clips_one_speaker`; `02-erd.md` bỏ sót nên cả bảng trạng thái, mermaid nhóm 7 và số đếm đều thiếu.
- Đã thêm ba dòng vào bảng **Trạng thái triển khai**, ba entity cùng quan hệ vào **mermaid nhóm 7** (kèm đoạn giải
  thích `SpeechClip` và Voice ID), sáu dòng vào **Tổng hợp đầy đủ các quan hệ** và quy tắc 21–22 ở mục ràng buộc.
- Số đếm: **32 → 35 thực thể**; bỏ câu "Bảng mới chưa vẽ vào ERD mục tiêu — BA bổ sung ở BA Final Review" ở mục Ranh
  giới.
- Hình đã vẽ lại: `13-erd/muc-tieu` (41 bảng, vẽ lại theo code bằng `scripts/draw_erd.py` ngày 04/11/2026) và `13-erd/chen` (35 thực thể, 59 quan hệ),
  xuất lại PNG; cập nhật `13-erd/README.md`, `muc-tieu/README.md`, `chen/README.md` theo số mới.

### 3.2 Report 3

- **UC-80** (description, PRE-1, EX-1): Khách có phiên còn hiệu lực cũng dùng được, chỉ thấy và xoá câu của mình;
  câu của người khác trả 404 không mã (như không tồn tại), không hiện MSG323; bước 2 của EX-1 chỉ áp dụng cho người dùng nội bộ.
- **BR-355**: nêu đích danh Khách (thành viên khi đã đăng nhập, Khách khi phiên còn).
- **BR-370**: viết lại theo ADR-0051 — micro của Khách **được ghi**; Khách vẫn không mở bản ghi phòng/bản gỡ băng.
- **UC-39a AF-2 bước 1**: bỏ câu "không áp dụng cho Khách", thêm điều kiện đã xác nhận thông báo ghi âm.
- **§I.3.2**: thêm 5 entity còn thiếu — `TranscriptSpeaker`, `GuestSession`, `SpeechClip`, `VoiceConsent`,
  `VoiceSample` (30 → 35 entity), mỗi entity một bảng field.
- **§I.3.1**: thay ảnh ERD bằng bản `muc-tieu` mới (41 bảng vẽ theo code; thêm câu nêu bốn bảng nối và hai bảng kỹ thuật §I.3.2 không mô tả).
- **BR-275 + UC-32c EX-1** (gộp từ review PR #252, cùng DOCX): tài liệu chỉ gắn biểu quyết nháp bị ẩn với mọi người trừ Chủ toạ/Thư ký hiện tại — kể cả người tổ chức, người duyệt — nên bỏ chữ "a Member or Guest", viết lại thành "anyone except the current Chair and Secretary".
- **Change Log**: dòng `03/11/2026 / M / KhanhPG`.

Kiểm tra: `check_docx.py` OK; `sync_reports.py --report 3` sinh lại snapshot, extract 05/06 và `uc-map.html`.

### 3.3 Còn lại

- **Màn 10.1** đã nêu Khách trong danh sách chọn người nói, nên không phải sửa; **UC-46a/UC-46b** giữ nguyên (việc
  gán tên không đổi theo SCRUM-129).
- **V.2 `MSG380`** vẫn ghi ngữ cảnh UC-37/EX-9; câu của Khách dùng lại đúng mã đó, không thêm mã mới. Nếu PO muốn
  đặc tả nêu cả trường hợp này thì thêm vào cột ngữ cảnh.
- **Jira**: sau khi tài liệu vào `main`, comment kết quả vào SCRUM-129 theo
  [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update); nếu bảng ERD
  được coi là phần của SCRUM-109 thì comment cả SCRUM-109.

## 4. Thứ tự đã làm

1. `02-erd.md`: entity, quan hệ, số đếm, quy tắc. ✔
2. Report 3: UC-80, BR-355, BR-370, UC-39a AF-2, §I.3.1/I.3.2, Change Log. ✔
3. Vẽ lại `13-erd/muc-tieu` và `13-erd/chen`, xuất PNG. ✔
4. Comment Jira. ☐ (chờ PR vào `main`)

## 5. Câu hỏi còn mở

- **ADR-0051** vẫn ghi trạng thái "Đề xuất" dù PO đã chốt 01/11/2026 và code đã merge — có chuyển "Đã chấp nhận" không?
- **BR-355** có cần câu riêng cho Khách (tự xoá câu khi phiên còn, sau đó nhờ Thư ký) hay coi Khách nằm trong "a person"?
- Bốn điểm lệch còn lại trong comment `10418` (ngoài ba điểm bot tóm tắt) có cần vào note này không — BA đọc và bổ sung.
