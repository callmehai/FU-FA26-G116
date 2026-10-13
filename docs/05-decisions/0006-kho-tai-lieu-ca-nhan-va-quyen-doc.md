# ADR-0006: Kho tài liệu cá nhân và quy tắc đọc tài liệu

- **Trạng thái:** Đã chấp nhận — phần loại Guest thay thế bởi ADR-0016; phần phiên bản và ghim phiên bản thay thế bởi ADR-0018
- **Ngày:** 2026-09-20
- **Người quyết định:** Nhóm SEP490-G116

## Bối cảnh

Mô hình hiện tại buộc tài liệu sinh ra là đã thuộc về một cuộc họp:
[`Document.MeetingId`](../../backend/TrustCabinet.Domain/Entities/Document.cs) là khoá ngoại **bắt
buộc**. Không có chỗ nào để một người giữ tài liệu của riêng mình trước khi quyết định đưa nó vào
cuộc họp nào.

Khi rà lại danh sách actor cho `SCRUM-52`, BA đề xuất một actor `Document Manager` "quản lý kho tài
liệu nội bộ". Bàn ra thì nhu cầu không phải là một thủ thư của cả tổ chức, mà là:

- mỗi người có **kho riêng**, tự tải tài liệu của mình lên;
- muốn đưa tài liệu vào một cuộc họp thì phải **có quyền ở cuộc họp đó**.

Nghĩa là không có actor thủ thư nào cả — mọi người dùng nội bộ đều là chủ kho của chính mình.

## Quyết định

Chúng tôi sẽ tách **tài liệu** khỏi **cuộc họp**:

- `Document` trở thành món trong **kho cá nhân**, có chủ sở hữu là người tải lên.
- Gắn tài liệu vào cuộc họp là một hành động riêng, ghi ở **bảng nối** `MeetingDocument`
  *(MeetingId, DocumentId, AgendaItemId, VersionId, AddedById, AddedAt)*. Một tài liệu gắn được
  vào nhiều cuộc họp mà kho vẫn nguyên.
- Chỉ gắn được **tài liệu của chính mình**, và chỉ khi có quyền ở cuộc họp đó — **thư ký hoặc chủ
  toạ của chính cuộc họp** (`MeetingRole`, không phải `Capability` toàn hệ thống). Gắn theo vai trò
  của từng cuộc họp thì không ai thêm được tài liệu vào cuộc họp mình không dự.

### Quy tắc đọc

| Tài liệu ở đâu | Ai đọc được |
|---|---|
| Trong kho, chưa gắn cuộc họp nào | **Chỉ chủ sở hữu** |
| Đã gắn vào một cuộc họp | **Thành viên của cuộc họp đó** |

"Thành viên cuộc họp" tính theo dòng `meeting_participants` — **gồm cả** người đã trả lời không
tham dự, và **không gồm** khách ngoài vào bằng link mời
([ADR-0005](0005-khach-ngoai-tham-du-bang-link-moi.md): khách chỉ nghe, không đọc tài liệu).

Một tài liệu nằm ở hai cuộc họp thì thành viên của cả hai đều đọc được. Đó **không** phải lỗ rò,
vì chỉ chủ sở hữu mới gắn được tài liệu của mình — việc mở rộng người đọc là lựa chọn có ý thức
của chính người đó, giống như họ tải về rồi gửi đi.

### Mức mật không còn quyết định *ai* đọc

`SCRUM-22` viết "mức mật của cuộc họp quyết định **ai** đọc được tài liệu". Với quy tắc trên thì
**ai** luôn là "thành viên cuộc họp", không phụ thuộc mức mật nữa. `Sensitivity` còn chi phối
những thứ khác:

- `Secret`: chặn tải về, chỉ cho xem trực tuyến;
- `Confidential` và `Secret`: ghi nhật ký kiểm toán dày hơn cho mỗi lượt xem/tải;
- cảnh báo trên màn chi tiết cuộc họp.

Nói cách khác, hành vi trước đây chỉ áp cho `Confidential` nay thành mặc định cho mọi mức. Đơn
giản hơn, và tester kiểm được bằng một câu thay vì một bảng bốn dòng.

### Hai quy tắc chống mất bằng chứng

**Gắn thì ghim phiên bản.** File không bao giờ bị ghi đè — tải bản mới là thêm một
`DocumentVersion`. Nếu dòng gắn kết chỉ trỏ vào `Document`, chủ sở hữu tải v2 lên kho sau cuộc họp
là cuộc họp đã diễn ra âm thầm hiển thị nội dung khác với cái đã phát cho mọi người đọc. Dòng gắn
kết vì vậy ghi **đúng phiên bản được gắn**; muốn đổi sang v2 là một hành động có chủ ý, có dấu vết.

**Xoá khỏi kho không đụng tới bản đã gắn.** Chủ sở hữu xoá tài liệu khỏi kho cá nhân, hoặc rời tổ
chức và bị vô hiệu hoá, thì tài liệu đã gắn vào cuộc họp vẫn còn nguyên. Cùng lý do với
`MeetingStatusHistory` chỉ ghi thêm: đó là thứ cuộc họp đã thực sự dùng, và biên bản dẫn chiếu tới
nó. Xoá khỏi kho chỉ gỡ khỏi danh sách cá nhân.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ nguyên: tài liệu luôn thuộc một cuộc họp | Không phải sửa gì | Không có kho cá nhân; muốn dùng lại một tài liệu ở cuộc họp khác thì phải tải lên lần nữa, thành hai bản không liên quan gì nhau | Không đáp ứng được nhu cầu đã chốt |
| Cho `Document.MeetingId` nullable, null = còn trong kho | Sửa ít nhất | Một tài liệu chỉ gắn được vào **một** cuộc họp, và gắn xong là rời kho | Bỏ mất chính cái lợi của việc tách kho |
| Bảng nối `MeetingDocument` (đã chọn) | Tài liệu gắn nhiều cuộc họp, kho vẫn nguyên; ghim được phiên bản theo từng lần gắn | Thêm một bảng; góp ý và mục nghị sự phải chuyển chỗ | — |

## Hệ quả

**Phải làm khi story này tới:**

- `Document`: bỏ `MeetingId`, thêm chủ sở hữu. Thêm bảng `MeetingDocument`.
- **`AgendaItemId` chuyển từ `Document` sang dòng gắn kết** — cùng một tài liệu có thể ở mục 3 của
  cuộc họp này và mục 1 của cuộc họp kia.
- [`DocumentComment`](../../backend/TrustCabinet.Domain/Entities/DocumentComment.cs) đang trỏ thẳng
  vào `Document`. Một tài liệu ở hai cuộc họp thì góp ý của hai nơi trộn vào nhau — phải trỏ vào
  **dòng gắn kết**.
- Sửa tiêu chí nghiệm thu của `SCRUM-22` theo quy tắc đọc ở trên, **trước khi** BE làm story đó.

**Với `SCRUM-52`:** bỏ actor `Document Manager`. "Tải tài liệu lên kho cá nhân" là use case của
actor cơ sở (Thành viên); "Gắn tài liệu vào cuộc họp" thuộc thư ký/chủ toạ của cuộc họp đó.

**Chưa quyết:** hạn mức dung lượng kho cá nhân, và ai đọc được kho của người đã rời tổ chức (tài
liệu đã gắn vào cuộc họp thì vẫn đọc bình thường — câu hỏi chỉ còn cho phần chưa gắn). Chốt khi
làm story.
