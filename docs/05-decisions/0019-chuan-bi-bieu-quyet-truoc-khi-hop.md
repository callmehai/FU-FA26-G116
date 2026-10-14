# ADR-0019: Chuẩn bị biểu quyết trước khi họp và chốt điều kiện mở phiếu

- **Trạng thái:** Đã chấp nhận theo xác nhận của người dùng trong phiên làm việc ngày 14/10/2026
- **Ngày:** 2026-10-14
- **Người quyết định:** Người dùng phụ trách đặc tả trong phiên làm việc
- **Thay thế:** phần chỉ cho tạo/cấu hình Vote khi Meeting `InProgress` của ADR-0014. Quyền Guest theo ADR-0016 và quy tắc kết thúc cuộc họp theo ADR-0017 vẫn áp dụng.

## Bối cảnh

SCRUM-28 và ADR-0014 trước đây yêu cầu cả tạo, mở và bỏ phiếu trong lúc họp. Chủ toạ cần chuẩn bị
nội dung và phương án trước khi bắt đầu; chuẩn bị nháp chưa phải mở biểu quyết. Khi viết đặc tả
FE-11, người dùng xác nhận các trạng thái chuẩn bị nháp và các điều kiện mở còn thiếu.

## Quyết định

1. Chair hiện hành được tạo, cấu hình và xoá Vote `Draft` khi Meeting `Approved`, `Published`
   hoặc `InProgress`. Vote nháp chỉ Chair hiện hành xem được. Quyền chuẩn bị ở `Approved` áp dụng
   riêng cho biểu quyết, không mở quyền xem các hồ sơ khác đang bị hạn chế. Vote đã mở hoặc đã đóng
   không sửa/xoá.
2. Chỉ mở Vote và nhận phiếu khi Meeting `InProgress`. Một Meeting chỉ có tối đa một Vote `Open`
   tại một thời điểm. Không tự đóng Vote trước để mở Vote khác.
3. Khi mở, hệ thống xác định người đang giữ các vai trò Chair đã chọn và có quyền truy cập hiện
   hành; Guest phải xác minh email và lời mời còn hiệu lực. Không có người đủ điều kiện thì từ chối
   mở. Điểm danh không quyết định quyền bỏ phiếu.
4. Danh sách và số người đủ điều kiện chốt lúc mở. Đổi role sau đó không làm thay đổi danh sách
   hoặc mẫu số; người còn trong danh sách vẫn phải có quyền truy cập hiện hành để bỏ phiếu. Thu hồi
   hoặc hết hạn quyền truy cập chặn phiếu mới, giữ nguyên phiếu đã ghi nhận.
5. Đặc tả biểu quyết thuộc trực tiếp Meeting, không yêu cầu chọn hay gắn Agenda Item. Phiên làm
   việc này không sửa đặc tả, luồng hoặc dữ liệu Agenda.
6. Giữ một người một phiếu, chọn đúng một phương án, gửi rồi không đổi/rút. Phiếu kín không liên
   kết danh tính với lựa chọn. UC-57 do Chair thực hiện: hệ thống kiểm phiếu, Chair ghi kết luận
   `Passed / NotPassed / Deferred` và ghi chú tuỳ chọn; ghi rồi không sửa.

## Phương án đã cân nhắc

- Chỉ chuẩn bị nháp khi `InProgress`: buộc Chair soạn nội dung trong lúc họp; bỏ giới hạn này.
- Chuẩn bị ở mọi trạng thái: chưa được xác nhận cho `Draft`, `Rejected` hoặc `WaitingApproval`;
  chọn phạm vi đã xác nhận là `Approved`, `Published`, `InProgress`.
- Mở nhiều Vote đồng thời: người dùng xác nhận chỉ một Vote `Open` mỗi Meeting.
- Tính lại danh sách khi role thay đổi: làm thay đổi đối tượng và mẫu số của một Vote đã mở;
  giữ danh sách chốt và kiểm quyền truy cập hiện hành.

## Hệ quả

- Report 3 DOCX local có đặc tả đầy đủ UC-52a, UC-52b và UC-53–UC-57; BR tương ứng được chỉnh
  cùng nguồn. Markdown snapshot và bản trích UC/BR được đồng bộ từ nguồn local. Đặc tả màn hình
  giữ nguyên để người phụ trách sửa theo [note bàn giao](../notes/NOTE-cap-nhat-man-hinh-fe11.md).
- BR-160, BR-161, BR-165, BR-189, BR-190 được làm rõ; BR-252–BR-274 bổ sung các nguyên tắc độc
  lập. MSG205–MSG237 được dành sẵn trong catalogue, chưa xác nhận đã có endpoint triển khai.
- Mô tả SCRUM-28 và comment 10218 vẫn là nguồn cho quyết định trước đó; giới hạn tạo Vote trong
  mô tả ticket cần được đối chiếu với quyết định mới này. Phiên làm việc này không đăng comment
  hoặc sửa Jira, không thay file Drive, không khẳng định runtime hoặc diagram đã được cập nhật.
