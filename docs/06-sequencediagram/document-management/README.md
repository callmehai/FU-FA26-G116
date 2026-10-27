# Sequence Diagram — Tài liệu cuộc họp (FE-07)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-07 đã có sequence.
Các UC tài liệu khác (luồng chính UC-33a, UC-33b → UC-33d, UC-34, UC-36, UC-71, UC-72) chưa có sequence.

| Use Case | Source | Ticket |
|---|---|---|
| UC-32a View Meeting Documents | [UC32a_ViewMeetingDocuments.txt](UC32a_ViewMeetingDocuments.txt) | SCRUM-21, SCRUM-22 |
| UC-32c Preview Meeting Document | [UC32c_PreviewMeetingDocument.txt](UC32c_PreviewMeetingDocument.txt) | SCRUM-21, SCRUM-22 |
| UC-33a AF-1 Upload files from the computer | [UC33a_UploadDocumentsToMeeting.txt](UC33a_UploadDocumentsToMeeting.txt) | SCRUM-107 |
| UC-35 Comment on Meeting Document | [UC35_CommentOnMeetingDocument.txt](UC35_CommentOnMeetingDocument.txt) | SCRUM-23 |
| UC-74 Confirm Document Read | [UC74_ConfirmDocumentRead.txt](UC74_ConfirmDocumentRead.txt) | SCRUM-96 |

UC-32a, UC-32c, UC-35 vẽ ngày 22/10/2026 theo đặc tả sửa cùng ngày (nhóm theo mục nghị sự, panel Tài liệu
trong phòng họp, trả lời góp ý, BR-315 → BR-318). Boundary của UC-32a gộp tab Tài liệu và panel trong phòng vì
cùng một request; nhánh AF-3 chỉ đổi thứ tự hiển thị. Source, PNG, tab trong file draw.io gộp và ảnh
`drawio-png/` đã có (tab thêm 23/10/2026 bằng bộ chuyển Mermaid của draw.io, style chuẩn hoá như tab UC-74).
**Cập nhật 27/10/2026 theo code SCRUM-21**: bỏ tham số `fromRoom` (máy chủ trả mục đang bàn, trình duyệt đưa lên
đầu), bỏ nhóm Biểu quyết và kiểm lời mời khách (chưa xây — UC-74, UC-04), máy chủ trả thêm số trang, thời điểm
thêm và **mọi** mục nghị sự kể cả mục chưa có tài liệu; UC-32c bỏ "dịch vụ trả độ mật" (trình duyệt lấy từ màn
chi tiết cuộc họp) và thêm nhánh ghi kiểm toán lỗi thì không gửi tệp.

UC-33a AF-1 vẽ ngày 23/10/2026 cho SCRUM-107, chỉ phủ nhánh tải từ máy; boundary là tab Tài liệu, nơi đặt hộp thoại tải lên (khớp `MeetingDocumentsTab` ở class diagram FE-07); luồng chính (chọn từ kho) thuộc SCRUM-58 và sẽ
thêm vào cùng UC sau. **Cập nhật 27/10/2026 theo code**: kiểm quyền và trạng thái cuộc họp trước (EX-7, EX-1, EX-6), rồi chặn theo
kích thước khai báo trước khi đọc thân request (EX-8), đọc request rồi kiểm một lượt (đúng một tệp trở lên, ≤ 10 tệp và ≤ 200 MB,
từng tệp có tên dùng được, đuôi khớp nội dung và ≤ 50 MB — EX-3/EX-8), ghi dòng tệp lưu trữ trước khi đẩy byte,
đẩy byte lên kho, rồi tìm/tạo thư mục và ghi tài liệu trong **một** transaction có khoá cuộc họp. Trình duyệt gửi kèm số trang của
từng PDF (`pageCounts`); sau khi lưu xong máy chủ thông báo danh sách tài liệu đã đổi cho người trong cuộc họp qua SignalR (theo §10 README chung, sự kiện SignalR không vẽ vào sequence diagram).
`:File Storage` là kho đối tượng R2 ([ADR-0024](../../05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md));
lỗi ở bước nào thì xoá các tệp đã đẩy để giữ "tất cả hoặc không" (BR-286). Job dọn tệp mồ côi (BR-213) là job nền nên không vẽ
sequence, theo §10 của [README chung](../README.md).

**Tab trong file draw.io gộp chưa cập nhật (27/10/2026).** Ba tab `uc32a_viewmeetingdocuments`,
`uc32c_previewmeetingdocument`, `uc33a_uploaddocumentstomeeting` vẫn là bản 22–23/10/2026 (dựng bằng bộ chuyển Mermaid của
draw.io), khác bản as-built ở trên; ảnh trong `drawio-png/` cũng theo bản cũ. Bản đúng của đợt này là `.txt` và `.png` trong
thư mục này. Khi dựng lại tab, dùng bộ chuyển Mermaid của draw.io như các tab cũ nên để lượt sau, cùng lúc với việc bổ sung UC-33a luồng chính
(SCRUM-58).

Xác nhận đã đọc chỉ áp dụng cho tài liệu gắn với một biểu quyết của cuộc họp; checkbox chỉ bật khi người dùng đã cuộn
tới trang cuối (BR-290). Một xác nhận tính cho mọi biểu quyết cùng gắn tài liệu đó, không rút lại được và không gắn
với lựa chọn trong phiếu (BR-291, BR-293). Đặc tả: [Report 3, UC-74](../../08-reports/Report-3_Software_Requirement_Specification.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/document-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
