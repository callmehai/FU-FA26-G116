# Tài liệu cuộc họp — SCRUM-21, SCRUM-22, SCRUM-23

**Ngày:** 22/10/2026. **Người viết:** BA (Lộc). **Nguồn:** Jira SCRUM-21/22/23, ADR-0011, ADR-0018, ADR-0020, và
nghiệp vụ tab Tài liệu của AI-eCabinet (bản dev, xem ngày 22/10/2026).

## Đã cập nhật trong đợt này

| Loại | File |
|---|---|
| UC Specification | [UC-32a, UC-32c, UC-35](../01-requirements/05-use-case-specifications.md#fe-07--personal-document-library--meeting-document-management) |
| Business Rules | BR-315 → BR-319 trong [06-business-rules.md](../01-requirements/06-business-rules.md) |
| UC-33a | EX-8: mỗi lần tải tối đa 10 tệp, tổng 200 MB (BR-319, MSG288 dành sẵn) |
| Sequence | [UC32a, UC32c, UC35](../06-sequencediagram/document-management/README.md) (`.txt` + `.png`) |
| Class diagram | [FE-07 (một phần)](../07-classdiagram/feature-07-document-management-class-diagram.drawio), bố cục lại theo lưới: không còn đường cắt nhau |
| ERD | `MeetingDocument.AgendaItemId` → bảng nối `MeetingDocumentAgendaItem` theo ADR-0020; `DocumentComment.GuestInvitationId` ([02-erd.md](../02-design/02-erd.md), hình [ERD mục tiêu](../assets/erd-muc-tieu/README.md) xếp lại theo bố cục Meeting ở giữa) |
| Wireframe | [v7](../wireframe/TrustCabinet-wireframe-v7.html): sửa 7.3, 7.5; vẽ lại phòng họp 8.1–8.5 theo bản đã chạy trên server test; thêm 8.6 |
| Report 3 | DOCX + Markdown: UC-32a, UC-32c, UC-33a, UC-35, BR-315 → BR-319, MSG288, Change Log, ảnh ERD mục 3.1; 23/10/2026 viết lại đặc tả màn hình 7.3–7.5, 8.1–8.5, thêm 8.6, thay ảnh màn |
| Sequence draw.io gộp | Tab UC-32a, UC-32c, UC-35 và ảnh `drawio-png/` (23/10/2026) |
| Jira | SCRUM-21/22/23 viết lại; comment quyết định PO ở SCRUM-21, SCRUM-23, SCRUM-58; tạo **SCRUM-106** (gom góp ý, Sprint sau) |

Trả lời góp ý dùng lại MSG199/MSG200/MSG202. Mã mới duy nhất: MSG288 (giới hạn mỗi lần tải lên).

## Lấy gì từ AI-eCabinet, khác gì

| AI-eCabinet | TrustCabinet | Lý do |
|---|---|---|
| Tab Tài liệu chia ba nhóm: nội dung họp (theo từng mục), biểu quyết, chung | Giữ ba nhóm (UC-32a AF-1) | Dễ tìm hơn bảng phẳng có bộ lọc |
| Ghim ở mỗi mục: tải từ máy / chọn từ tài liệu cuộc họp / chọn từ kho | Giữ (UC-33a, UC-33b) | Khớp ADR-0020: một tệp gắn nhiều mục |
| Menu dòng: Gỡ khỏi mục này / Gỡ khỏi cuộc họp | Giữ (UC-33d, UC-33c) | Khớp ADR-0020 |
| DOCX đổi sang PDF trên server | Trình duyệt tự dựng bản xem | ADR-0018: server 1 GB |
| Mỗi đợt tải tối đa 10 tệp, 200 MB | Giữ: 10 tệp, tổng 200 MB, 50 MB mỗi tệp (BR-319) | PO 22/10/2026 |
| Trường "Phân loại" khi tải lên | Không làm ở cuộc họp | PO 22/10/2026 |
| Xác nhận đã đọc cho mọi tài liệu | Chỉ tài liệu biểu quyết (UC-74) | Quyết định PO 16/10/2026 |
| Không có góp ý trên tài liệu | Có góp ý, trả lời một cấp (UC-35) | SCRUM-23 |
| Không có phòng họp video | Panel Tài liệu trong phòng (8.6), mở tài liệu ở tab mới | ADR-0011; BR-315, BR-316 |

Trình bày chung cho cả phòng: dùng chia sẻ màn hình sẵn có (UC-39c), **không** làm trình xem đồng bộ trang.

## PO đã trả lời (22/10/2026)

1. BR-315, BR-316 (tài liệu trong phòng chỉ đọc, mở tab mới) và BR-317 (trả lời một cấp): **duyệt**.
2. "Thư ký gom góp ý thành danh sách để đưa vào nghị sự": **để Sprint sau**, tách thành SCRUM-106 (chưa có UC).
3. Giới hạn mỗi lần tải như AI-eCabinet: **có** — 10 tệp, tổng 200 MB (BR-319, UC-33a EX-8, MSG288).
4. Trường "Phân loại" cho tài liệu cuộc họp: **không**.

## Còn phải làm

- **Report 3 trên Drive**: chưa thay (chỉ sửa DOCX local).
- **Figma**: chạy plugin `figma-v7` để dựng lại 5.3, 7.3, 7.4, 7.4b, 7.5, 8.1–8.5 và thêm 8.6.
- ERD mục tiêu vẫn có quan hệ `AgendaItem` → `VotingSession` (ADR-0014) trong khi BR-285 nói biểu quyết không gắn mục
  nghị sự; cần BA xem lại ở đợt biểu quyết.
