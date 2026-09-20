# ADR-0005: Khách ngoài hệ thống tham dự bằng link mời

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-20
- **Người quyết định:** Nhóm SEP490-G116

## Bối cảnh

Khi BA dựng danh sách actor cho `SCRUM-52`, `Guest` được hiểu là "người chưa xác thực". Điều đó
không khớp với hệ thống đang có ở hai chỗ: `MeetingRole.Guest` vốn nghĩa là **khách mời đã có tài
khoản**, còn người chưa đăng nhập thì không có use case nào — mọi route đều đòi đăng nhập.

Rà lại thì nhu cầu thật là thứ khác: **người ngoài tổ chức, không có tài khoản, vào nghe một phiên
họp cụ thể bằng link mời hoặc mã**. Chuyên gia được mời góp ý, đối tác, đại diện đơn vị bạn. Họ
không cần tài khoản, và tạo tài khoản cho một lần dự họp là thủ tục thừa.

[02-erd.md](../02-design/02-erd.md) trước đó đã cắt `meeting_external_participants` với lý do
"khách mời ngoài hệ thống chưa nằm trong `SCRUM-20`". ADR này lật lại phần đó.

## Quyết định

Chúng tôi sẽ cho phép **khách ngoài hệ thống** tham dự một phiên họp mà không cần xác thực:

- Vào bằng **link mời hoặc mã**, gắn với **đúng một cuộc họp**.
- **Chỉ nghe.** Không xem tài liệu, không biểu quyết, không đăng ký phát biểu, không ghi chú,
  không thấy danh sách người dự, không tải gì về.
- **Không tính vào quorum.** Khách không sinh dòng `meeting_participants` — đó là bảng mà
  `Attendance` của nó quyết định quorum.
- Khách tự khai tên hiển thị. Hệ thống không xác minh danh tính, và không được trình bày như thể
  có xác minh.

### Không chặn theo mức mật — người sinh link chịu trách nhiệm

Hệ thống **không** cấm mời khách vào cuộc họp `Confidential` hay `Secret`. Người điều hành cuộc
họp hiểu bối cảnh rõ hơn một luật cứng trong code, và cấm cứng chỉ đẩy người ta sang cách khác tệ
hơn: bật loa ngoài, chia sẻ màn hình, hoặc gửi file qua ứng dụng nhắn tin.

Đổi lại, "chịu trách nhiệm" phải **quy kết được**, nếu không nó chỉ là một chữ trong tài liệu:

- Sinh link là quyền của **thư ký hoặc chủ toạ của chính cuộc họp đó** (`MeetingRole`, không phải
  quyền toàn hệ thống) — cùng người đang mời thành phần tham dự ở `SCRUM-20`.
- Nhật ký kiểm toán ghi: ai sinh link, cho cuộc họp nào, **ở mức mật nào**, lúc nào, hạn bao lâu.
  Và ghi từng lượt khách vào, kèm tên tự khai cùng địa chỉ IP.
- Cuộc họp `Confidential` và `Secret`: hiện cảnh báo rõ ràng và bắt xác nhận thêm một bước trước
  khi sinh link, để đó là một quyết định có ý thức chứ không phải một cú bấm nhầm.
- Link **có hạn, thu hồi được, giới hạn số lượt dùng**, và token không đoán được.
- Kiểm tra lại hiệu lực ở **mỗi request**, không chỉ lúc đổi token lấy phiên — cùng nguyên tắc mà
  `SCRUM-22` đã đặt ra cho link tải tài liệu.

### Nghĩa vụ theo Nghị định 13/2023/NĐ-CP

Giọng của khách cũng bị ghi âm và đưa vào luồng ASR. Đó là dữ liệu cá nhân của một người **không
có tài khoản và chưa ký thoả thuận nào với tổ chức**, nên `Recording.ConsentObtainedAt` hiện tại —
phản ánh sự đồng ý của người dự nội bộ — không phủ được họ.

Trước khi khách vào phòng, hệ thống phải hiện thông báo phiên họp đang/sẽ được ghi âm kèm mục
đích, và **lưu lại bằng chứng đã hiện thông báo đó** trên dòng lượt khách. Nghĩa vụ xoá theo yêu
cầu áp dụng với khách y như với người dùng nội bộ.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Bắt khách tạo tài khoản | Mô hình hiện tại không phải sửa gì | Thủ tục thừa cho một lần dự họp; sinh ra tài khoản rác không ai vô hiệu hoá | Đúng cái ma sát mà TrustCabinet muốn bỏ |
| Chặn cứng theo mức mật (chỉ `Normal`) | An toàn mặc định, không phụ thuộc phán đoán người dùng | Đẩy người ta sang loa ngoài và gửi file ngoài hệ thống — rò rỉ thật mà hệ thống không thấy | Cấm cứng không làm mất nhu cầu, chỉ làm mất dấu vết |
| Không chặn, quy trách nhiệm người sinh link (đã chọn) | Linh hoạt; mọi lượt mời đều có dấu vết quy kết được | Phụ thuộc phán đoán của thư ký | — |

## Hệ quả

**Mô hình dữ liệu phải sửa khi story này làm:**

- Thêm bảng link mời: token, cuộc họp, người sinh, hạn, số lượt tối đa, trạng thái thu hồi.
- Thêm bảng lượt khách: tên tự khai, token đã dùng, thời điểm vào/ra, IP, bằng chứng đã hiện thông
  báo ghi âm.
- [`CallParticipant.UserId`](../../backend/TrustCabinet.Domain/Entities/CallParticipant.cs) đang
  **bắt buộc** — phải cho nullable, thêm tham chiếu lượt khách, kèm ràng buộc **đúng một trong
  hai**.
- `meeting_participants` **không đổi**: khách không vào bảng đó.
- [`AuditLogEntry.ActorId`](../../backend/TrustCabinet.Domain/Entities/AuditLogEntry.cs) vốn đã
  nullable cho hành động không có người đăng nhập — định danh khách nằm ở `Details`.

**Tên gọi:** `MeetingRole.Guest` giữ nguyên nghĩa **khách mời có tài khoản**. Khách ngoài hệ thống
là khái niệm khác, không phải một giá trị của enum đó. Tài liệu BA phải gọi hai thứ bằng hai tên
khác nhau, nếu không sẽ code nhầm.

**Với `SCRUM-52`:** actor `Guest` trong bảng actor là **khách ngoài hệ thống**, và use case duy
nhất của họ là "vào nghe phiên họp bằng link mời". Không phải "người chưa xác thực" nói chung —
người chưa đăng nhập vẫn chỉ có màn đăng nhập.
