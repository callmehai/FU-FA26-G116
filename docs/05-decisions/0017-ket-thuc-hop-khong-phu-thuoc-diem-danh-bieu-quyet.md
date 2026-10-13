# ADR-0017: Kết thúc họp độc lập với biểu quyết; phục hồi Thư ký khi chuẩn bị

- **Trạng thái:** Đã chấp nhận theo quyết định PO ngày 13/10/2026
- **Ngày:** 2026-10-13
- **Người quyết định:** PO
- **Thay thế:** phần bắt đóng Vote trước End Meeting của ADR-0014; phần giữ BR-122/BR-159 chưa sửa của ADR-0016. Các quyết định khác vẫn giữ nguyên.

## Quyết định

Chair kết thúc cuộc họp đang InProgress bất cứ lúc nào, không phụ thuộc Vote còn Open, điểm danh đủ/thiếu, còn người chưa phản hồi hay agenda Pending. Chỉ kiểm tra đúng Chair, đúng trạng thái cuộc họp và lưu kết quả thành công; các kiểm tra này là phân quyền và bảo toàn dữ liệu, không phải điều kiện hoàn tất nghiệp vụ.

End Meeting không tự đóng Vote, không thay đổi hoặc xóa các phiếu đã lưu. Meeting chuyển Ended thì ngừng nhận phiếu mới, kể cả Vote vẫn mang trạng thái Open. Chair có thể đóng/kiểm phiếu còn mở sau Ended và ghi kết luận; đây là hoàn tất kết quả của phiếu cũ, không phải mở biểu quyết hay nhận phiếu sau họp. BR-159/160/165 và UC-22 hiện có được đồng bộ; quyết định đóng/kiểm phiếu sau Ended được lưu để áp dụng khi triển khai UC-52b/56. Các UC placeholder chưa được đặc tả mới. MSG139 ngừng dùng, không tái cấp.

Giữ thao tác kết thúc Meeting, giờ kết thúc, lịch sử và hoàn tất Agenda Item đang InProgress trong cùng lần lưu; Item Pending giữ nguyên. Đóng room sau khi lưu thành công. Điểm danh chưa ghi nhận giữ nguyên, không tự coi là Vắng.

BR-122 áp dụng theo trạng thái: Draft/Rejected/Approved được tạm không có Thư ký. Khi không còn Thư ký, Organizer có thể gán một tài khoản hoạt động làm Thư ký, kể cả đã từng có Thư ký trước đó. Có thể chọn Member hiện hành để đổi thành Secretary mà không tạo người tham dự trùng hoặc reset phản hồi/điểm danh. Ngoài thao tác khởi tạo/phục hồi này, Organizer không có quyền quản lý roster. Published/InProgress giữ ít nhất một Thư ký hoặc thay người cuối cùng cùng lần lưu; WaitingApproval vẫn khóa roster. BR-78/79/122 cùng UC-28a/b/c được đồng bộ; các điều kiện Thư ký khi công bố/bắt đầu BR-125/127 vẫn áp dụng.

## Hệ quả

Report 3, UC spec, BR, màn hình, catalogue MSG và sequence phản ánh cùng quyết định. Đây là cập nhật yêu cầu đích; không khẳng định endpoint runtime đã đổi. Không bổ sung quorum, tự động kết luận, tự đóng phiếu hoặc một gate hoàn tất mới ở End Meeting.
