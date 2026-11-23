# Thay đổi yêu cầu sau buổi thử 21/11/2026: cập nhật Report 3 theo 9 PR

**Người ghi:** agent, theo 3 tin của bot kênh `note-gửi-ba` ngày 22/11/2026 (06:27, 08:09, 08:50; chưa thả reaction). **Ngày:** 22/11/2026.
**Trạng thái: 🔴 CHƯA LÀM** — mới gom việc. Cả 9 PR đã merge vào **`app`** ngày 22/11 (#466–#471, #473, #474, #476).
**Nguồn chuẩn:** DOCX `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`, sinh lại bằng `scripts/sync_reports.py`. Không sửa tay `Report-3_*.md`, `05-use-case-specifications.md`, `06-business-rules.md`. Mô tả PR trên GitHub (mục "For BA") là nguồn chi tiết.

> Nhánh làm: tách từ `origin/app`, PR đích `app`. Note 57 (PR #465) còn mở và cũng sửa DOCX Report 3 — merge #465 trước hoặc tách nhánh từ nó để tránh xung đột DOCX.

## 1. Việc cần làm theo nhóm

### Nhóm A — Vai trò hệ thống (PR #473, ADR-0075) — lớn nhất, làm trước

- [ ] **Actor / ma trận quyền (Report 3 §I.4.4):** chỉ còn 3 vai trò hệ thống: Thành viên, Phê duyệt viên, Quản trị viên. Chủ toạ, Thư ký chỉ còn là vai trò *trong cuộc họp*. Bỏ cột "Soạn biên bản" (MinutesEditor).
- [ ] **UC-08** gán vai trò: lựa chọn = 3 vai trò + vai trò tuỳ chỉnh.
- [ ] **UC-10 / UC-11 / UC-12:** vai trò và quyền mặc định, tên vai trò trong ví dụ.
- [ ] **UC-58–UC-61, UC-64, UC-65, UC-83** (biên bản): không đổi hành vi; mọi chỗ "quyền Soạn biên bản / MinutesEditor" → "Thư ký (hoặc Chủ toạ) cuộc họp".
- [ ] BR liệt kê quyền mặc định của vai trò (ma trận seed FE-03).
- [ ] Sequence: các diagram ma trận vai trò.
- [ ] `docs/01-requirements/00-master-use-case-inventory.md`: §2 ghi chú actor.

### Nhóm B — Thu giọng mẫu hộ (PR #470 ADR-0073, PR #473)

- [ ] **UC-86a–86d:** actor = người có quyền "Thu giọng mẫu hộ" (mặc định Quản trị viên), thu ở màn *Quản trị → Người dùng*. Bỏ đường Thư ký trong cuộc họp, bỏ tiền điều kiện "Đã công bố/Đang họp", bỏ giới hạn 5 mẫu mỗi người thu và `MSG477` (giữ giới hạn 50 mẫu mỗi người). Cân nhắc gộp 86a–86d với đường thu theo người (ADR-0071) thành một UC.
- [ ] Bước mới: người thu tick xác nhận "đã hỏi, người này đồng ý lần thu này"; EX mới **`400 MSG483`** khi thiếu xác nhận.
- [ ] Đồng ý thường trực cần điều khoản **≥ 1.4** cả hai đường (trước là 1.3); điều khoản hiện tại **1.5** (bỏ nhắc Thư ký).
- [ ] **UC-81a/81b:** chữ điều khoản 1.5; tab "Người khác thu hộ"; "cho phép người khác thu" = đồng ý ≥ 1.4.
- [ ] **BR-448–BR-454, BR-449:** đổi theo trên + bằng chứng audit (`personAgreed`) và thông báo cho chủ giọng.
- [ ] Sequence UC-86b: thêm `personAgreed`, nhánh `MSG483`.
- [ ] Inventory: dòng UC-86 (actor Secretary, notice 1.3).
- [ ] MSG trong DOCX: thêm `MSG483`, đánh dấu `MSG477` ngừng dùng, sửa chữ `MSG476`.

### Nhóm C — Gỡ băng và nhận diện giọng (PR #467, #468, #471, #474, #476)

- [ ] **SCRUM-31 (gỡ băng):** câu mic phòng bị bỏ khi ngắn (< 10 ký tự, hoặc < 3 s và < 20 ký tự) và không khớp giọng nào hoặc toàn tiếng đệm; giọng không còn câu thì ẩn, đánh số lại liền (#467).
- [ ] **SCRUM-109 (mic riêng):** câu toàn tiếng đệm không lưu (`204`, như im lặng) (#467).
- [ ] **SCRUM-31 / SCRUM-109:** tô vàng nhạt từ máy không chắc (< 40 %), mất khi Thư ký sửa câu (#468).
- [ ] **SCRUM-35 ngưỡng (#471, #474, ADR-0074):** Gợi ý 0,6; chênh lệch 0,05; cùng giọng 0,7; giọng thứ hai 0,7; kế thừa 0,8; **tự gán = 0,6** (bằng gợi ý, theo #474 — không phải 0,7 như mô tả #471, không phải 0,75 ADR-0045, không phải 0,5 PR #463). Sửa mọi UC/BR/diagram còn số cũ. Bảng ngưỡng đầy đủ ở kênh `note-chung`.
- [ ] Bản gỡ băng viết số bằng chữ số (2026, 15%, ngày 22 tháng 11); số một từ trong lời thường giữ chữ (#476, ADR-0076). Bản cũ không đổi tới khi gỡ băng lại.
- [ ] Sequence gỡ băng lại sau lượt ghi: có thể ghi "dừng lượt chạy cũ" (ADR-0041 điểm 5) — tuỳ chọn.

### Nhóm D — Họp kết hợp và gán tên (PR #469 ADR-0072, PR #466)

- [ ] **SCRUM-29:** họp kết hợp — Thư ký bật mic phòng *trong phòng họp online*, một công tắc; không còn thẻ nổi trên trang cuộc họp (trừ khi LiveKit lỗi). Mic phòng tự cắt lượt mỗi 5 phút ("Lượt N" trong tab Bản ghi). Sửa UC + wireframe/màn hình liên quan.
- [ ] Gán tên người nói thì máy khác thấy ngay (#466) — không đổi yêu cầu; chỉ thêm câu nếu UC có nói "tải lại".

## 2. Cần PO quyết

- [ ] **ADR-0074** (ngưỡng) và **ADR-0076** (viết số) đang "Đề xuất" — xin PO duyệt trước khi chốt số vào BR.
- [ ] Câu hỏi mở #473: vai trò tuỳ chỉnh có `VoiceCollector` nhưng không có `AdminUsers` gọi được API mà không có màn chọn người — giữ hay thêm màn?

## 3. Việc khác trong kênh (không phải tài liệu)

- [x] Tin 14/11: dựng **máy AI local dự phòng** cho hôm bảo vệ (Ollama + `qwen2.5:7b-instruct`, Tailscale) — Khánh xác nhận đã làm (22/11).

## 4. Sau khi xong

- Sinh lại Report 3, PR đích `app`; khi PR vào `app`: comment Jira từng ticket (SCRUM-31, 35, 109, 29, ticket vai trò FE-03, SCRUM thu giọng hộ) theo [§2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update).
- Thả ✅ lên 3 tin bot ngày 22/11 trong `note-gửi-ba` (lúc bắt đầu làm thả ❌).
- `tests/qa/plans/fe-10.mjs` còn tiêu chí cho panel thu giọng trong cuộc họp đã bỏ — báo tester.
