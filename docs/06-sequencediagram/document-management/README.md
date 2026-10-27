# Sequence Diagram — Tài liệu cuộc họp (FE-07)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-07 đã có sequence.
Từ 27/10/2026 mọi UC của FE-07 đều có sequence. Cột "Loại": **as-built** vẽ theo code đã merge; **PROPOSED** vẽ theo
đặc tả khi chưa có code, lifeline và tên hàm là đề xuất, phải đổi sang as-built ở BA Final Review của ticket.

| Use Case | Source | Ticket | Loại |
|---|---|---|---|
| UC-32a View Meeting Documents | [UC32a_ViewMeetingDocuments.txt](UC32a_ViewMeetingDocuments.txt) | SCRUM-21, SCRUM-22 | as-built |
| UC-32c Preview Meeting Document | [UC32c_PreviewMeetingDocument.txt](UC32c_PreviewMeetingDocument.txt) | SCRUM-21, SCRUM-22 | as-built |
| UC-33a Add documents from the personal library (luồng chính) | [UC33a_AddDocumentsFromLibrary.txt](UC33a_AddDocumentsFromLibrary.txt) | SCRUM-112 | PROPOSED |
| UC-33a AF-1 Upload files from the computer | [UC33a_UploadDocumentsToMeeting.txt](UC33a_UploadDocumentsToMeeting.txt) | SCRUM-107 | as-built |
| UC-33b Attach Documents to Agenda Item | [UC33b_AttachDocumentsToAgendaItem.txt](UC33b_AttachDocumentsToAgendaItem.txt) | SCRUM-112 | PROPOSED |
| UC-33c Remove Document from Meeting | [UC33c_RemoveDocumentFromMeeting.txt](UC33c_RemoveDocumentFromMeeting.txt) | SCRUM-112 | PROPOSED |
| UC-33d Detach Document from Agenda Item | [UC33d_DetachDocumentFromAgendaItem.txt](UC33d_DetachDocumentFromAgendaItem.txt) | SCRUM-112 | PROPOSED |
| UC-34 View Personal Document Library | [UC34_ViewPersonalLibrary.txt](UC34_ViewPersonalLibrary.txt) | SCRUM-111 | PROPOSED |
| UC-34b View a Locked Account's Personal Library | [UC34b_ViewLockedAccountLibrary.txt](UC34b_ViewLockedAccountLibrary.txt) | SCRUM-111 (hoặc ticket tách) | PROPOSED |
| UC-35 Comment on Meeting Document | [UC35_CommentOnMeetingDocument.txt](UC35_CommentOnMeetingDocument.txt) | SCRUM-23 | PROPOSED |
| UC-36 Upload Document to Personal Library | [UC36_UploadToPersonalLibrary.txt](UC36_UploadToPersonalLibrary.txt) | SCRUM-111 | PROPOSED |
| UC-71a Delete Document from Personal Library | [UC71a_DeleteLibraryDocument.txt](UC71a_DeleteLibraryDocument.txt) | SCRUM-111 | PROPOSED |
| UC-71b Rename Document | [UC71b_RenameLibraryDocument.txt](UC71b_RenameLibraryDocument.txt) | SCRUM-111 | PROPOSED |
| UC-71c Move Document to Folder | [UC71c_MoveLibraryDocument.txt](UC71c_MoveLibraryDocument.txt) | SCRUM-111 | PROPOSED |
| UC-72a Create Folder | [UC72a_CreateFolder.txt](UC72a_CreateFolder.txt) | SCRUM-110 | PROPOSED |
| UC-72b Rename Folder | [UC72b_RenameFolder.txt](UC72b_RenameFolder.txt) | SCRUM-110 | PROPOSED |
| UC-72c Move Folder | [UC72c_MoveFolder.txt](UC72c_MoveFolder.txt) | SCRUM-110 | PROPOSED |
| UC-72d Delete Folder | [UC72d_DeleteFolder.txt](UC72d_DeleteFolder.txt) | SCRUM-110 | PROPOSED |
| UC-74 Confirm Document Read | [UC74_ConfirmDocumentRead.txt](UC74_ConfirmDocumentRead.txt) | SCRUM-96 | PROPOSED |

**Kho cá nhân và gắn/gỡ (vẽ 27/10/2026, SCRUM-91).**

- Hạn mức 10 GB toàn hệ thống và giới hạn 10 tệp / 200 MB (PO 27/10/2026, ADR-0031): UC-36, UC-33b AF-1 và UC-33a AF-1
  (upload từ máy) cùng kiểm tổng dung lượng đã lưu (BR-333, `MSG345`); UC-36 và UC-33b AF-1 thêm nhánh `MSG288` (BR-319).
  UC-33a AF-1 đã code ở SCRUM-107 nhưng chưa kiểm 10 GB: sequence vẽ theo đặc tả mới, code phải bổ sung cùng SCRUM-111.
- UC-34b (Quản trị viên đọc kho của tài khoản đã khoá, ADR-0031) vẽ PROPOSED: kiểm tài khoản đã khoá, ghi nhật ký kiểm toán
  trước khi trả dữ liệu, không ghi được nhật ký thì từ chối (BR-334, BR-335). Lifeline `:Library Review Controller/Service`
  và `:Audit Log` do người code đổi tên theo thiết kế thật.
- UC-72a–d, UC-34, UC-36, UC-71a–c: PROPOSED theo Report 3 §II.7.3–7.7. Lifeline `Personal Library Screen`,
  `:Folder Controller/Service`, `:Document Controller/Service`, `:Folder Repository`, `:Document Repository`. AF-1 của
  UC-34 (tìm, lọc), UC-71c và UC-72c (chuyển ra gốc thư viện) nằm trong câu của request, không thành nhánh riêng. UC-71a,
  UC-72d bắt đầu từ lúc người dùng xác nhận xoá. UC-72d không vẽ BR-228: thư mục cuộc họp bị xoá sẽ được tạo lại ở lần
  tải lên sau (UC-33a AF-1). UC-36 kiểm tệp, ghi tệp lưu trữ và đẩy R2 theo đúng thứ tự UC-33a AF-1 (ADR-0024 §4); đặc
  tả UC-36 không có giới hạn 10 tệp / 200 MB nên sơ đồ không có `MSG288` (câu hỏi mở cho PO). Đặc tả chưa có nhánh lỗi
  cho Guest (không có kho), sơ đồ chỉ vẽ bước kiểm quyền.
- UC-33a luồng chính tách file riêng (`UC33a_AddDocumentsFromLibrary`) vì là request khác với AF-1 tải từ máy.
- UC-33b, UC-33d vẽ **PROPOSED theo đặc tả** (quyết định BA 27/10/2026, phương án (b) ở SCRUM-112): gắn và gỡ là
  thao tác riêng, Chủ toạ và Thư ký đều làm được, dùng mã `MSG238`–`MSG247`; UC-33b có đủ ba nguồn tài liệu (tài liệu
  cuộc họp, tải từ máy, chọn từ kho). Code hiện vẫn gắn/gỡ qua `PUT …/agenda/{itemId}` với danh sách tài liệu (chỉ Thư
  ký, mã agenda `MSG61`/`MSG66`/`MSG67`/`MSG69`); SCRUM-112 làm chức năng riêng, xong thì đổi hai sơ đồ sang as-built ở
  BA Final Review.

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

**Tab draw.io gộp và PNG (27/10/2026).** Mọi UC của FE-07 đều có tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/`; các tab này được dựng lại ngày 27/10/2026 từ các file `.txt` trong thư mục này, thay cho bản Mermaid cũ của UC-32a, UC-32c, UC-33a (tải lên).

Xác nhận đã đọc chỉ áp dụng cho tài liệu gắn với một biểu quyết của cuộc họp; checkbox chỉ bật khi người dùng đã cuộn
tới trang cuối (BR-290). Một xác nhận tính cho mọi biểu quyết cùng gắn tài liệu đó, không rút lại được và không gắn
với lựa chọn trong phiếu (BR-291, BR-293). Đặc tả: [Report 3, UC-74](../../08-reports/Report-3_Software_Requirement_Specification.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/document-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
