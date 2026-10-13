# ADR-0018: Tài liệu không có phiên bản — kho cá nhân và cuộc họp chỉ giữ tham chiếu tới file

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-10-13
- **Người quyết định:** PO (HaiTV)
- **Thay thế:** phần *phiên bản* và *ghim phiên bản khi gắn* của [ADR-0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md).
  Kho cá nhân, chỉ gắn tài liệu của chính mình, quy tắc đọc ([ADR-0016](0016-khach-tham-gia-nhu-thanh-vien.md) cho
  Guest) và vai trò của mức mật trong ADR-0006 vẫn giữ.

## Bối cảnh

ADR-0006 tách kho cá nhân khỏi cuộc họp và giữ **phiên bản** tài liệu: tải bản mới là thêm một `DocumentVersion`,
mỗi lần gắn vào cuộc họp ghim đúng một phiên bản. Kéo theo hai UC (UC-32b Xem lịch sử phiên bản, UC-33b Thêm phiên
bản mới), tab *Phiên bản* trong trình xem, cột *Phiên bản* ở mọi danh sách, và câu hỏi chưa có lời đáp: thư ký tải v2
cho tài liệu thuộc kho của người khác thì v2 nằm ở kho ai.

Code AI-eCabinet (đồng bộ ngày 13/10/2026) đã bỏ phiên bản: migration `0032_drop_old_document_versions` xoá các bản cũ
và ép mỗi tài liệu đúng một bản; gắn file từ kho vào cuộc họp chỉ tạo một dòng mới trỏ vào **cùng object** trong kho
đối tượng, không chép byte; xoá file khỏi kho không đụng tới tài liệu cuộc họp. Muốn đổi nội dung thì tải file mới,
gắn vào, gỡ file cũ. TrustCabinet đi theo hướng AI-eCabinet cộng họp trực tuyến qua LiveKit.

## Quyết định

1. **Không có phiên bản tài liệu.** File đã tải lên là bất biến. Đổi nội dung = tải file mới (UC-36 hoặc tải từ máy
   trong UC-33a), gắn vào cuộc họp, gỡ file cũ (UC-33c). Bỏ UC-32b và UC-33b; không tái cấp hai số này.
2. **Byte nằm trong kho đối tượng, mỗi lần tải lên là một `StoredFile`** (khoá object, loại nội dung xác định theo
   nội dung thật, dung lượng, SHA-256, số trang của PDF, người tải, thời điểm). `StoredFile` thay chỗ `DocumentVersion` trong
   mô hình đích, nên số thực thể không đổi.
3. **Kho cá nhân và cuộc họp chỉ giữ tham chiếu.**
   - `Document` là một dòng trong kho cá nhân: chủ sở hữu, thư mục (null = gốc kho), tên file, trỏ tới một `StoredFile`.
   - `MeetingDocument` là một dòng trong cuộc họp: trỏ tới cùng `StoredFile`, giữ tên file, mục nghị sự (tuỳ chọn),
     người thêm, thời điểm thêm, và `Document` nguồn (null khi dòng kho đã bị xoá).
   - Gắn từ kho không chép byte. Tải thẳng từ máy trong UC-33a tạo `StoredFile`, một dòng trong kho của người tải
     (như AI-eCabinet) và dòng `MeetingDocument` — hai dòng cùng trỏ vào một `StoredFile`, không có bản sao nào.
     Dòng kho đó nằm trong thư mục mang tên cuộc họp (mục 9).
   - Một file chỉ có tối đa một dòng `MeetingDocument` đang hiệu lực trong mỗi cuộc họp.
4. **Xoá khỏi kho cá nhân (UC-71)** chỉ xoá dòng `Document` của chủ sở hữu. Tài liệu đã gắn vào cuộc họp vẫn đọc được
   bình thường, vì cuộc họp giữ tham chiếu riêng tới cùng `StoredFile`.
5. **Gỡ khỏi cuộc họp (UC-33c)** do Chủ toạ hoặc Thư ký của cuộc họp làm, và **bị chặn khi cuộc họp đã có biên bản**
   (biên bản dẫn chiếu tới tài liệu đó). Gỡ là đánh dấu `RemovedAt`/`RemovedById` trên dòng `MeetingDocument`; góp ý
   của tài liệu đó vẫn còn làm bằng chứng nhưng không hiện trong danh sách.
6. **Thêm và gỡ tài liệu cuộc họp chỉ dành cho Chủ toạ và Thư ký** của chính cuộc họp — như host/secretary của
   AI-eCabinet và ADR-0006. Meeting Organizer không có quyền này nếu không giữ một trong hai vai trò đó.
7. **Job dọn file mồ côi.** Định kỳ, hệ thống tìm `StoredFile` không còn dòng `Document` nào và không còn dòng
   `MeetingDocument` đang hiệu lực, tải lên đã quá thời gian chờ (để không xoá nhầm file đang được gắn dở), rồi xoá
   object trong kho đối tượng và đánh dấu `PurgedAt`. Dòng `StoredFile` giữ lại (tên, dung lượng, SHA-256) để dòng
   gắn kết đã gỡ và nhật ký kiểm toán vẫn dẫn chiếu được. AI-eCabinet chưa có job này; TrustCabinet làm.
8. **Loại file theo AI-eCabinet, xem trước trên trình duyệt:** PDF và DOCX, tối đa 50 MB, kiểm tra theo nội dung
   thật. **Server không chuyển đổi gì** (khác AI-eCabinet, vốn đổi DOCX sang PDF): file lưu đúng như tải lên, trình
   duyệt tự dựng bản xem — PDF từng trang bằng pdf.js, DOCX thành một văn bản liền bằng `docx-preview`. Vì vậy góp ý
   trên PDF gắn được số trang, trên DOCX thì không. Lý do: máy chủ test 1 GB không gánh được LibreOffice/Gotenberg.
   Cuộc họp `Secret` chỉ cho xem trực tuyến, không cho tải về (ADR-0006).
9. **Thư mục trong kho cá nhân, như AI-eCabinet.** `Folder` thuộc một chủ sở hữu, lồng tối đa 5 cấp; tạo, đổi tên,
   chuyển (không chuyển vào chính nó hay thư mục con của nó), xoá khi rỗng; tên không trống, không bắt duy nhất. File
   trong kho đổi tên và chuyển giữa các thư mục được. Tải thẳng từ máy vào cuộc họp thì dòng kho nằm trong thư mục
   `<tên cuộc họp> - dd/mm/yyyy` của người tải, tự tạo một lần cho mỗi người và mỗi cuộc họp, vẫn tìm ra kể cả khi
   thư mục đã bị đổi tên hay chuyển chỗ (`Folder.MeetingId`).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ phiên bản + ghim phiên bản (ADR-0006) | Có lịch sử bản thảo trong một tài liệu | Thêm 2 UC, tab, cột, và câu hỏi v2 thuộc kho ai; AI-eCabinet đã bỏ | Phức tạp hơn nhu cầu; đổi nội dung bằng tải file mới là đủ |
| Chép byte khi gắn vào cuộc họp | Kho và cuộc họp độc lập tuyệt đối | Nhân đôi dung lượng; file bất biến nên chép không thêm gì | Tham chiếu tới object bất biến đã cho cùng hành vi |
| **Tham chiếu tới `StoredFile` bất biến + job dọn file mồ côi** | Đơn giản, khớp AI-eCabinet, không rò dung lượng | Cần một job nền | **Chọn** |
| Đổi DOCX sang PDF trên server (như AI-eCabinet) | Xem trước DOCX chia trang, góp ý gắn trang | LibreOffice/Gotenberg tốn vài trăm MB RAM | Máy chủ 1 GB; dựng trên trình duyệt là đủ |
| Kho phẳng, không thư mục | Ít UC hơn | Lệch AI-eCabinet; khó sắp xếp khi nhiều file | PO chọn làm thư mục như AI-eCabinet |

## Hệ quả

- Mô hình đích: `DocumentVersion` đổi thành `StoredFile`; `Document` bỏ `CurrentVersionId`, thêm `StoredFileId`,
  `FolderId`; thêm thực thể `Folder` (OwnerId, ParentId, Name, MeetingId);
  `MeetingDocument` đổi `DocumentVersionId` thành `StoredFileId`, `DocumentId` thành nullable, thêm `FileName`,
  `RemovedAt`, `RemovedById`. Code hiện tại vẫn là mô hình cũ (`Document.MeetingId`, `DocumentVersion`); migration làm
  khi triển khai SCRUM-21 / SCRUM-58.
- Danh mục UC: bỏ UC-32b, UC-33b; thêm UC-33c *Remove Document from Meeting*, UC-71 *Manage Personal Documents*
  (UC-71a xoá, UC-71b đổi tên, UC-71c chuyển thư mục) và UC-72 *Manage Library Folders* (UC-72a tạo, UC-72b đổi tên,
  UC-72c chuyển, UC-72d xoá thư mục rỗng). Báo cáo, màn hình 7.x, ma trận quyền, ERD, class diagram, use case diagram và Jira SCRUM-21/22/23/58 cập
  nhật cùng đợt.
- **Chưa quyết** (giữ từ ADR-0006): hạn mức dung lượng kho cá nhân; ai đọc được phần chưa gắn trong kho của người đã
  rời tổ chức. Chu kỳ chạy và thời gian chờ của job dọn file chốt khi làm story.
