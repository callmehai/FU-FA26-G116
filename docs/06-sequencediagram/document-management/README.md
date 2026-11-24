# Sequence Diagram — Tài liệu cuộc họp (FE-07)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-07 đã có sequence.
Từ 27/10/2026 mọi UC của FE-07 đều có sequence. Cột "Loại": **as-built** vẽ theo code đã merge; **PROPOSED** vẽ theo
đặc tả khi chưa có code, lifeline và tên hàm là đề xuất, phải đổi sang as-built ở BA Final Review của ticket.

| Use Case | Source | Ticket | Loại |
|---|---|---|---|
| UC-35a View Meeting Documents | [UC35a_ViewMeetingDocuments.txt](UC35a_ViewMeetingDocuments.txt) | SCRUM-21, SCRUM-22 | as-built |
| UC-35b Preview Meeting Document | [UC35b_PreviewMeetingDocument.txt](UC35b_PreviewMeetingDocument.txt) | SCRUM-21, SCRUM-22 | as-built |
| UC-36a Add documents from the personal library (luồng chính) | [UC36a_AddDocumentsFromLibrary.txt](UC36a_AddDocumentsFromLibrary.txt) | SCRUM-112 | as-built (`.txt` + `MSG349`; PNG/drawio chờ xuất lại) |
| UC-36a AF-1 Upload files from the computer | [UC36a_UploadDocumentsToMeeting.txt](UC36a_UploadDocumentsToMeeting.txt) | SCRUM-107 | as-built (`.txt` + `MSG349`; PNG/drawio chờ xuất lại) |
| UC-36b Attach Documents to Agenda Item | [UC36b_AttachDocumentsToAgendaItem.txt](UC36b_AttachDocumentsToAgendaItem.txt) | SCRUM-112 | as-built (`.txt` + `MSG349`; PNG/drawio chờ xuất lại) |
| UC-36c Remove Document from Meeting | [UC36c_RemoveDocumentFromMeeting.txt](UC36c_RemoveDocumentFromMeeting.txt) | SCRUM-112 | as-built (`.txt` + `MSG349`; PNG/drawio chờ xuất lại) |
| UC-36d Detach Document from Agenda Item | [UC36d_DetachDocumentFromAgendaItem.txt](UC36d_DetachDocumentFromAgendaItem.txt) | SCRUM-112 | as-built (`.txt` + `MSG349`; PNG/drawio chờ xuất lại) |
| UC-37 View Personal Document Library | [UC37_ViewPersonalLibrary.txt](UC37_ViewPersonalLibrary.txt) | SCRUM-111 | PROPOSED |
| UC-37a View a Locked Account's Personal Library | [UC37a_ViewLockedAccountLibrary.txt](UC37a_ViewLockedAccountLibrary.txt) | SCRUM-111 (PR #169) | as-built |
| UC-38 Comment on Meeting Document | [UC38_CommentOnMeetingDocument.txt](UC38_CommentOnMeetingDocument.txt) | SCRUM-23 | as-built (`.txt` + `MSG348`/`MSG349`; PNG/drawio chờ xuất lại) |
| UC-39 Upload Document to Personal Library | [UC39_UploadToPersonalLibrary.txt](UC39_UploadToPersonalLibrary.txt) | SCRUM-111 | PROPOSED |
| UC-40a Delete Document from Personal Library | [UC40a_DeleteLibraryDocument.txt](UC40a_DeleteLibraryDocument.txt) | SCRUM-111 | PROPOSED |
| UC-40b Rename Document | [UC40b_RenameLibraryDocument.txt](UC40b_RenameLibraryDocument.txt) | SCRUM-111 | as-built (`.txt` + `MSG347`; PNG/drawio chờ xuất lại) |
| UC-40c Move Document to Folder | [UC40c_MoveLibraryDocument.txt](UC40c_MoveLibraryDocument.txt) | SCRUM-111 | PROPOSED |
| UC-41a Create Folder | [UC41a_CreateFolder.txt](UC41a_CreateFolder.txt) | SCRUM-110 | as-built (`.txt` + `MSG348`; PNG/drawio chờ xuất lại) |
| UC-41b Rename Folder | [UC41b_RenameFolder.txt](UC41b_RenameFolder.txt) | SCRUM-110 | as-built (`.txt` + `MSG348`; PNG/drawio chờ xuất lại) |
| UC-41c Move Folder | [UC41c_MoveFolder.txt](UC41c_MoveFolder.txt) | SCRUM-110 | PROPOSED |
| UC-41d Delete Folder | [UC41d_DeleteFolder.txt](UC41d_DeleteFolder.txt) | SCRUM-110 | PROPOSED |
| UC-42 Confirm Document Read | [UC42_ConfirmDocumentRead.txt](UC42_ConfirmDocumentRead.txt) | SCRUM-96 | PROPOSED |

**Kho cá nhân và gắn/gỡ (vẽ 27/10/2026, SCRUM-91; `.txt` as-built một phần 28/10).**

- Hạn mức toàn hệ thống (ADR-0034, PO 27/10/2026; 250 GB từ 24/11/2026, ADR-0084) và giới hạn 10 tệp / 100 MB (UC-36a, UC-36b, UC-39; họp 19/11/2026, trước là 200 MB): UC-39, UC-36b AF-1 và UC-36a AF-1
  (tải từ máy) cùng kiểm tổng dung lượng đã lưu (UC-36a, UC-39, NFR) và trả `MSG346` khi đầy; UC-39, UC-36b AF-1 thêm nhánh `MSG288`.
  Sequence vẽ một lần gửi; hàng đợi ở trình duyệt gửi từng tệp một request (ADR-0033, UC-36a, UC-36b, UC-39), không vẽ lại vòng lặp.
- UC-37a (Quản trị viên đọc kho của tài khoản đã khoá, ADR-0035) vẽ theo code PR #169: kiểm tài khoản đã khoá, ghi nhật ký kiểm toán
  trước khi trả dữ liệu; không ghi được nhật ký thì từ chối (`MSG176` khi tải danh sách, `MSG158` khi mở tệp); ẩn tệp của cuộc họp
  Secret và thư mục tải lên của cuộc họp đó (BR-085, BR-011). Dùng `:Document Controller/Service` và
  `:Document Repository` của kho cá nhân, cùng `:User Repository` và `:Audit Log` ngoài feature; code đặt trong
  `PersonalLibraryEndpoints` / `PersonalLibraryService`.
- **28/10/2026 (BA Final Review):** giữ khung Controller/Service như agenda (không `Endpoints`, không HTTP/status,
  không SignalR hub). Chỉ bổ sung nhánh as-built: `MSG349` (UC-36a cả hai, UC-36b–d, UC-38), `MSG348` (UC-38, UC-41a/b),
  `MSG347` (UC-40b). PNG SequenceDiagram.org và tab/`drawio-png` xuất lại sau khi chốt `.txt`.
- UC-36a luồng chính tách file riêng (`UC36a_AddDocumentsFromLibrary`) vì là request khác với AF-1 tải từ máy.

UC-35a, UC-35b, UC-38 vẽ ngày 22/10/2026 theo đặc tả sửa cùng ngày (nhóm theo mục nghị sự, panel Tài liệu
trong phòng họp, trả lời góp ý, UC-35a, UC-35b, UC-38, BR-011). Boundary của UC-35a gộp tab Tài liệu và panel trong phòng vì
cùng một request; nhánh AF-3 chỉ đổi thứ tự hiển thị. Source, PNG, tab trong file draw.io gộp và ảnh
`drawio-png/` đã có (tab thêm 23/10/2026 bằng bộ chuyển Mermaid của draw.io, style chuẩn hoá như tab UC-42).
**Cập nhật 27/10/2026 theo code SCRUM-21**: bỏ tham số `fromRoom` (máy chủ trả mục đang bàn, trình duyệt đưa lên
đầu), bỏ nhóm Biểu quyết và kiểm lời mời khách (chưa xây — UC-42, UC-04), máy chủ trả thêm số trang, thời điểm
thêm và **mọi** mục nghị sự kể cả mục chưa có tài liệu; UC-35b bỏ "dịch vụ trả độ mật" (trình duyệt lấy từ màn
chi tiết cuộc họp) và thêm nhánh ghi kiểm toán lỗi thì không gửi tệp.
**Cập nhật 02/11/2026 theo code PR #224 (sau test E2E):** UC-35a có lại nhóm Biểu quyết (AF-1): biểu quyết có tài liệu mà
người gọi xem được (nháp chỉ Chủ toạ/Thư ký, BR-086, BR-087), mỗi tài liệu kèm xác nhận đã đọc của chính người gọi và cờ còn xác
nhận được (người bỏ phiếu của biểu quyết đang Open, cuộc họp InProgress); kiểm quyền ghi rõ cả phiên Khách (BR-055).
UC-35b thêm EX-5 `MSG382` (tệp đã mất khỏi kho, không thử lại) và EX-6 `MSG383` (trình duyệt không vẽ được tệp).
UC-36a AF-1, UC-36b AF-1, UC-39, UC-68a: trình duyệt không gửi `pageCounts` nữa; server kiểm cấu trúc PDF và tự đếm trang trong một
tiến trình con ([ADR-0044](../../05-decisions/0044-server-kiem-cau-truc-va-dem-trang-pdf.md)). PNG, tab draw.io gộp và
`drawio-png/` của các UC này đã xuất lại cùng ngày. `drawio-png/uc33b_…` xuất ở `--scale 1` (03/11/2026): tab cao 11.240 px,
ở `--scale 2` ảnh cao quá giới hạn canvas của draw.io nên ra trắng.

UC-36a AF-1 vẽ ngày 23/10/2026 cho SCRUM-107, chỉ phủ nhánh tải từ máy; boundary là tab Tài liệu, nơi đặt hộp thoại tải lên (khớp `MeetingDocumentsTab` ở class diagram FE-07); luồng chính (chọn từ kho) thuộc SCRUM-58 và sẽ
thêm vào cùng UC sau. **Cập nhật 27/10/2026 theo code**: kiểm quyền và trạng thái cuộc họp trước (EX-7, EX-1, EX-6), rồi chặn theo
kích thước khai báo trước khi đọc thân request (EX-8), đọc request rồi kiểm một lượt (đúng một tệp trở lên, ≤ 10 tệp và ≤ 100 MB,
từng tệp có tên dùng được, đuôi khớp nội dung và ≤ 10 MB — EX-3/EX-8; họp 19/11/2026, trước là 200 MB / 50 MB), ghi dòng tệp lưu trữ trước khi đẩy byte,
đẩy byte lên kho, rồi tìm/tạo thư mục và ghi tài liệu trong **một** transaction có khoá cuộc họp. Số trang PDF do server đếm (trước
02/11/2026 là trình duyệt gửi kèm `pageCounts`); sau khi lưu xong máy chủ thông báo danh sách tài liệu đã đổi cho người trong cuộc họp qua SignalR (theo §10 README chung, sự kiện SignalR không vẽ vào sequence diagram).
`:File Storage` là kho đối tượng S3 tự host (SeaweedFS, [deploy/README.md §9](../../../deploy/README.md)); file `.txt`, tab draw.io và PNG của UC-36a, UC-36b, UC-39 còn ghi tên cũ `:Object Storage (R2)`, chờ vẽ lại;
lỗi ở bước nào thì xoá các tệp đã đẩy để giữ "tất cả hoặc không" (UC-36a, UC-36b, UC-39). Job dọn tệp mồ côi (BR-077) là job nền nên không vẽ
sequence, theo §10 của [README chung](../README.md).

**Tab draw.io gộp và PNG (27/10/2026).** Mọi UC của FE-07 đều có tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/`; các tab này được dựng lại ngày 27/10/2026 từ các file `.txt` trong thư mục này, thay cho bản Mermaid cũ của UC-35a, UC-35b, UC-36a (tải lên).

Xác nhận đã đọc chỉ áp dụng cho tài liệu gắn với một biểu quyết của cuộc họp; checkbox chỉ bật khi người dùng đã cuộn
tới trang cuối (UC-42). Một xác nhận tính cho mọi biểu quyết cùng gắn tài liệu đó, không rút lại được và không gắn
với lựa chọn trong phiếu (UC-42, BR-096, BR-097). Đặc tả: [Report 3, UC-42](../../08-reports/Report-3_Software_Requirement_Specification.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/document-management`
và cập nhật tab UC tương ứng trong file draw.io gộp theo [README chung](../README.md).
