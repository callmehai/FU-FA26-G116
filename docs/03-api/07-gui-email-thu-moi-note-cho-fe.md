# Note cho FE — email thư mời cuộc họp (SCRUM-15)

> **Cảnh báo — nghiệp vụ đã đổi (10/10/2026).** File này mô tả **code đang chạy**, vẫn có `MeetingSession`.
> PO đã bỏ Meeting Session và biểu quyết đặc biệt ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md),
> [ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)): lịch dự kiến sẽ chuyển về Meeting
> (`plannedStartTime`, `plannedEndTime`, `onlineLink`). Phần hợp đồng API dưới đây chỉ đổi **cùng commit với migration**.
> Số UC đã đổi theo baseline 70 UC ([bảng đổi số](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới));
> các mã `BR-54` → `BR-61` nhắc trong file là số BR cũ đã ngừng dùng ([danh mục BR](../01-requirements/06-business-rules.md)).

## Kết luận nhanh

SCRUM-15 là hạ tầng backend, không thêm HTTP endpoint và không có màn hình riêng cho hàng đợi
email. FE không gọi queue hoặc SMTP trực tiếp. Điểm tích hợp nằm ở thao tác **Công bố cuộc họp** của
SCRUM-19 khi endpoint đó được triển khai.

## Xử lý kết quả công bố

- Gọi endpoint công bố qua module trong `frontend/src/api/`; không gọi `fetch` trực tiếp từ
  component.
- `MSG45` xác nhận meeting đã chuyển sang `Published` và transaction công bố đã commit.
  Khi SMTP được bật, email mới đã được xếp hàng. Mã này **không xác nhận SMTP đã gửi thư đến người nhận**.
- Khi nhận `MSG45`, chỉ hiển thị nội dung theo nghĩa "Đã công bố cuộc họp"; không hiển thị "Đã gửi
  email thành công" hoặc số lượng email đã gửi.
- `MSG46` nghĩa là meeting không còn ở trạng thái cho phép công bố. FE cần hiển thị lỗi và tải lại
  dữ liệu meeting thay vì tiếp tục giữ nút Công bố ở trạng thái cũ.
- `MSG47` là lỗi công bố chung. Giữ meeting trên màn hình để người dùng có thể thử lại.
- Lỗi SMTP xảy ra bất đồng bộ sau request công bố. Lỗi đó không đổi meeting khỏi trạng thái
  `Published` và không phải lý do để FE rollback giao diện.

Hiện chưa có API xem trạng thái `email_messages`, vì vậy FE không poll bảng queue và không tự suy
đoán trạng thái gửi thư.

## Khi email bị tắt

`Email:Smtp:Enabled=false` là cấu hình backend. Khi đó thao tác công bố vẫn có thể thành công nhưng
backend không tạo thư mời mới; các job cũ vẫn được giữ. FE không cần và không nên hiển thị cấu hình SMTP, trạng thái worker
hoặc cảnh báo dựa trên giá trị cấu hình này.

## Đường dẫn trong email

Email dẫn tới `/mgmt/meetings/{id}`, trùng với `routes.meeting(id)` trong
`frontend/src/lib/routes.ts`. Màn hình đích phải:

- yêu cầu đăng nhập theo luồng hiện có;
- gọi API để kiểm tra quyền đọc meeting ở phía server;
- hiển thị cùng trang `404` như tài nguyên không tồn tại nếu người dùng không được phép xem.

Không thêm `VITE_API_BASE_URL` hoặc hardcode địa chỉ backend. `Email:ApplicationBaseUrl` là cấu hình
riêng của backend để tạo absolute URL trong email.

## Thời gian và người nhận

- API Meeting không còn trả Planned Start/End Time; FE lấy lịch từ Meeting Session ở story tương ứng.
- Email Publish không hiển thị lịch và không đính kèm `.ics`; FE chỉ dùng link chi tiết Meeting.
- Backend tự loại tài khoản đã vô hiệu hóa khỏi danh sách nhận thư. FE không lặp lại luật này như
  một biện pháp bảo mật và không thông báo "đã gửi cho N người" vì response công bố không xác nhận
  từng lần gửi.

## Chưa thuộc phạm vi SCRUM-15

Mô hình queue đã cho phép một meeting gửi các loại sự kiện khác nhau, chẳng hạn đổi lịch, hủy họp
hoặc phát hành biên bản. SCRUM-15 chưa cung cấp endpoint hoặc UI cho các luồng đó; FE không tự thêm
luồng mới cho đến khi story tương ứng có contract.

## Checklist bàn giao FE

- Dùng `routes.meeting(id)` cho link nội bộ tới chi tiết cuộc họp.
- Đặt lời gọi công bố trong `frontend/src/api/`, không gọi `fetch` trong component.
- Xử lý loading, `MSG45`, `MSG46`, `MSG47` và lỗi mạng.
- Nội dung thành công chỉ xác nhận đã công bố, không khẳng định email đã được giao.
- Giữ kiểm tra đăng nhập, quyền và hành vi `404` trên route chi tiết.
- Không đọc queue, không poll trạng thái email và không đưa cấu hình SMTP vào frontend.

## Trạng thái tích hợp FE

Đã tích hợp trên trang chi tiết cuộc họp, cùng thanh luồng duyệt của SCRUM-19
(`src/features/meetings/workflow/`):

- Người tổ chức thấy "Công bố" khi cuộc họp ở `Approved`. Dialog xác nhận nói thành phần tham dự sẽ
  thấy cuộc họp và nhận lời mời họp, và thông tin không sửa được sau khi công bố. Dialog không nhắc
  tới email, vì thư có thể đang bị tắt hoặc chưa gửi.
- `MSG45`: toast đúng `message` của backend ("Đã công bố cuộc họp."), cập nhật trạng thái bằng meeting
  trong response, thêm dòng "Công bố" vào lịch sử; thanh luồng duyệt biến mất.
- `MSG46`: đóng dialog, toast lý do, tải lại meeting và lịch sử. `409` là meeting không còn
  `Approved`; `403` là người tổ chức mất `MeetingCreator`, nên tải lại cả phiên.
- `MSG47` và lỗi mạng: lỗi hiện trong dialog, cuộc họp giữ nguyên trên màn hình, bấm "Công bố" để
  thử lại.
- Link trong email: người chưa đăng nhập được đưa tới trang đăng nhập với dòng "Đăng nhập để mở cuộc
  họp." (không nêu tên cuộc họp), đăng nhập xong quay về đúng `/mgmt/meetings/{id}`. Người không có
  quyền xem gặp trang không tìm thấy chung.
