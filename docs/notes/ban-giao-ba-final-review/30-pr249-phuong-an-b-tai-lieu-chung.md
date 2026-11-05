# PR #249 — "Tài liệu chung" theo phương án B (SCRUM-21): BR-280 và BR-275

**Người ghi:** KhanhPG (BA), 03/11/2026. **Nguồn:** PO chốt 03/11/2026 (comment 10416 trên
[SCRUM-21](https://sep490g116.atlassian.net/browse/SCRUM-21)) và code đã lên `main` cùng ngày
(comment 10419, [PR #249](https://github.com/callmehai/FU-FA26-G116/pull/249), commit `45ae5e36`).
Note này gom việc BA; note 22 mục 6 là bản trước của cùng câu chuyện (`#235` còn theo phương án A).

## 1. Quyết định

**Tài liệu chung** (general) = tài liệu **không gắn mục nghị sự nào và không gắn biểu quyết nào**. Tài liệu của
một biểu quyết **chỉ hiện ở nhóm Biểu quyết**, không hiện thêm ở nhóm chung.

**Hệ quả PO đã chấp nhận:**

- Tài liệu **chỉ** gắn biểu quyết **nháp** (không gắn mục nghị sự, không gắn biểu quyết đã mở/đã đóng): Thành viên
  và Khách không thấy ở nhóm nào; mở nội dung, tải về, xác nhận đã đọc và viết góp ý đều trả **404 MSG156**, giống
  tài liệu không tồn tại. **Chủ toạ và Thư ký vẫn thấy.**
- Biểu quyết mở ra thì mọi người xem được cuộc họp đọc được tài liệu đó.
- Ô đếm "Tài liệu (N)" trong phòng họp không tính những tài liệu đó cho Thành viên/Khách.

## 2. Code đang làm gì

- `MeetingDocumentService.cs:48` — `general = rows.Where(r => r.ItemIds.Count == 0 && !r.OnVote)`: nhóm chung đã
  loại tài liệu có liên kết biểu quyết; tài liệu của biểu quyết chỉ nằm trong nhóm của biểu quyết đó.
- `MeetingDocumentVisibility.cs` — luật "chỉ gắn biểu quyết nháp thì người ngoài Chủ toạ/Thư ký coi như không
  tồn tại" (404 MSG156) cho xem, tải và góp ý.
- FE: ô "Tài liệu (N)", trình xem và danh sách chọn tài liệu gộp cả nhóm Biểu quyết, nên tài liệu không bị mất khỏi
  những chỗ đó.
- `docs/03-api/11-tai-lieu-cuoc-hop.md` đã sửa theo code trong cùng PR.

## 3. Đã sửa trong Report 3

| Chỗ | Trước | Sau |
|---|---|---|
| **BR-280** | "…with no Item links left it is a general Meeting document, **even while it is still linked to a Vote**" (phương án A) | Tài liệu chỉ là tài liệu chung khi **không gắn mục nghị sự và không gắn biểu quyết**; tài liệu của biểu quyết chỉ hiện ở nhóm Biểu quyết; gỡ hết liên kết mục thì thành chung **chỉ khi** không biểu quyết nào gắn |
| **BR-275** | "Anyone who can view a Meeting can read its documents." | Thêm ngoại lệ: tài liệu **chỉ gắn biểu quyết nháp** — Thành viên/Khách không thấy ở nhóm nào, mở/tải/đọc/góp ý đều như không tồn tại (MSG156); Chủ toạ/Thư ký vẫn thấy; biểu quyết mở thì mọi người đọc được |
| Ô rationale của 2 luật | `ADR-0020 / PO 15/10/2026` | thêm `PO 03/11/2026 — option B (SCRUM-21)` |
| Đặc tả màn 7.3 (đoạn mở đầu) | "a Vote's document with no Agenda Item link also appears among the general documents" | Tài liệu của biểu quyết chỉ ở nhóm Biểu quyết; tài liệu chỉ gắn biểu quyết nháp không hiện với Thành viên/Khách (BR-275, BR-280) |
| Bảng 8.6 (panel tài liệu trong phòng) | "Documents linked to no Agenda Item." | "…and to no Vote; a document of a Vote is listed in the Votes group only." |
| **UC-32a** (luồng chính bước 3) | "a document with no Agenda Item link appears as a general Meeting document" | Tài liệu chỉ là tài liệu chung khi **không gắn mục và không gắn biểu quyết**; tài liệu gắn biểu quyết chỉ nằm ở nhóm Biểu quyết |
| **UC-32a** (AF-1 bước 2) | "the general documents, which are all documents with no Agenda Item link, **a Vote's documents included**" | Nhóm chung là tài liệu không gắn mục **và** không gắn biểu quyết, vì tài liệu của biểu quyết chỉ ở nhóm Biểu quyết |
| **UC-32c** (EX-1) | "the document was removed or the user can no longer view the Meeting" | Thêm trường hợp: tài liệu chỉ gắn biểu quyết nháp và không gắn mục — Thành viên/Khách nhận MSG156 như tài liệu không tồn tại (BR-275) |
| Change Log | — | dòng `03/11/2026 / M / KhanhPG` |

Đã chạy `check_docx.py` (OK) và `sync_reports.py --report 3` (snapshot, extract 05/06, `uc-map.html`).

## 4. Còn lại

- [ ] Ảnh wireframe 7.3, 7.4 và 8.6 trong Report 3 vẫn là bản cũ — cập nhật cùng đợt đặc tả màn hình
      (chưa làm: đợt này chỉ sửa chữ Report 3, không đụng wireframe hay đặc tả màn).
- [x] UC-32a và UC-32c đã nêu đúng phương án B (03/11/2026, nhánh `docs/scrum-21-uc32-bieu-quyet-nhap`):
      UC-32a luồng chính bước 3 và AF-1 bước 2 bỏ câu "a Vote's documents included", UC-32c EX-1 thêm
      đích danh trường hợp tài liệu chỉ gắn biểu quyết nháp.
- [x] Comment kết quả vào SCRUM-21 **sau khi** PR tài liệu merge vào `main` — đã đăng 04/11/2026, comment `10434`, PR #253 và #254 (Điều 2.2 trong
      [DEVELOPMENT_WORKFLOW](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update)).
