# Thành phần tham dự và vai trò trong cuộc họp — FE-06

Các sequence UC-27, UC-28a → UC-28c, UC-29, UC-30, UC-31 tuân theo [convention chung](../README.md). Cùng nằm trong FE-06 không có nghĩa cùng implementation task. Đặc tả của lần chỉnh sửa này nằm tại
[Report 3, FE-06](../../08-reports/Report-3_Software_Requirement_Specification.md#6-fe-06--meeting-participant-management).

- Organizer khởi tạo Secretary đầu tiên (BR-78). Secretary được gán có quyền xem meeting trước `Published` (BR-118)
  và chuẩn bị danh sách, gán Chair và các participant còn lại.
- `Draft`, `Rejected`, `Approved`, `Published`, `InProgress`: được quản lý danh sách theo role (BR-80).
- `WaitingApproval`: chỉ xem, không thêm participant, đổi role hoặc xóa, kể cả màn hình đã mở trước lúc gửi duyệt.
- Organizer công bố từ `Approved` khi có đúng một Chair và ít nhất một Secretary hiện hành có tài khoản hoạt động
  (BR-124, BR-125) — không còn điều kiện Meeting Session. Sequence UC-20 đã đồng bộ điều kiện này.
- Trước công bố, participant đã được gán nhưng chưa nhận lời mời. Khi công bố mới gửi lời mời;
  thêm người ở `Published`/`InProgress` thì gửi lời mời cho người được thêm (BR-90, BR-91).
- Participant phản hồi `Accepted` hoặc `Declined` (không còn uỷ quyền — BR-105) ở `Published`/`InProgress`.
  Từ `Ended` trở đi, hồ sơ chỉ đọc.

## Ghi chú thay đổi để đối chiếu SCRUM-20

Ngày 03/10/2026, review FE-06 (số cũ F08) chốt khóa danh sách khi `WaitingApproval`, mở lại khi `Approved` hoặc
`Rejected`, và làm rõ quyền chuẩn bị cuộc họp của Secretary từ `Draft`. Đã đồng bộ UC-28a → UC-28c (số cũ UC-38–40),
policy BR-29 và điều kiện công bố UC-20. Đây là cập nhật yêu cầu và sơ đồ;
quyền truy cập và điều kiện công bố trong API hiện tại cần được đồng bộ khi triển khai FE-06.
Ghi chú này là nội dung để cập nhật ticket, chưa được đăng lên Jira trong lần chỉnh sửa tài liệu này.

## Xuất ảnh

Từ thư mục gốc repository, dùng công cụ hiện có:

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/meeting-participant-management
node scripts/export-diagrams.js docs/06-sequencediagram/meeting-management/UC20_PublishMeeting.txt
```

Giữ file `.txt` và `.png` đồng bộ. Công cụ cần truy cập `sequencediagram.org`.

Ngày 12/10/2026 đã cập nhật và xuất lại đủ 7 sequence `.txt`/`.png` theo Report 3
bằng script `scripts/export-diagrams.js`, chỉ cho UC-27, UC-28a/b/c, UC-29, UC-30 và UC-31:

- UC-28a thêm nhiều người cùng một vai trò và quyền bổ sung; toàn bộ được thêm hoặc không thêm ai.
- UC-28b quản lý vai trò và quyền bổ sung, thay Chủ trì bằng một lần lưu cho cả hai người;
  giữ tên file cũ để các liên kết vẫn dùng được. Không thay đổi thì kết thúc với MSG154, không báo thành công giả.
- UC-27/UC-30 cho phép Chủ trì hoặc Thành viên hiện hành xem sau công bố khi được cấp đúng quyền xem tương ứng.
  Hai quyền bổ sung không cho phép quản lý thành phần, điểm danh hoặc thay đổi các quyền nghiệp vụ khác.
- UC-31 lưu điểm danh Có mặt/Vắng cho một/nhiều người được chọn cho thành viên và khách khi `InProgress`;
  chưa điểm danh thì để trống, người được gửi thay đổi hoặc lưu lỗi thì giữ nguyên toàn bộ giá trị cũ.
- BR-168–BR-178 và MSG145–MSG154 là các mã mới. MSG141–MSG144 đã được dùng cho nghị sự nên được giữ nguyên.

Đây là cập nhật tài liệu; chưa xác nhận API đã triển khai các quy tắc mới và chưa đăng ghi chú lên Jira.

## PO 13/10/2026

Guest nhận quyền bổ sung như Member, không giới hạn hai quyền xem (UC-27/30); UC-28a cảnh báo mức mật trong form bình thường, UC-28b sửa Guest permissions nhưng không role. UC-31 không đòi đầy đủ roster. UC-22 bỏ BR-179/MSG155. UC-04 hiện có được sửa theo quyết định OTP và thông tin tự khai; các sequence bổ sung cho UC đang có nằm ở [auth/README.md](../auth/README.md). BR-122 đã chốt theo trạng thái; Organizer phục hồi Thư ký khi không còn ai. [ADR-0016](../../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md).

Phục hồi Thư ký có thể chọn một Member hiện hành đang hoạt động để đổi thành Secretary, không tạo participant trùng hoặc reset response/attendance.

## Phạm vi implementation task — rà ngày 13/10/2026

- **SCRUM-20:** roster, lời mời và phản hồi UC-27–UC-30. Khi xoá internal participant hoặc revoke Guest, giữ lịch sử response/attendance; dữ liệu historical không tính vào current roster, access hay số lượng role. UC-28c source đã mô tả việc bảo toàn này.
- **SCRUM-26:** UC-31 ghi/sửa điểm danh. Không đưa implementation điểm danh vào plan SCRUM-20 chỉ vì đặc tả/sơ đồ UC-31 cùng thư mục FE-06.
- Jira live vẫn dùng số UC/BR cũ và có tiêu chí lệch PO: SCRUM-20 còn delegation và giữ Secretary ở mọi trạng thái; SCRUM-26 còn self-attendance, Excused/Delegated và quorum. Khi triển khai phải đối chiếu các quyết định PO/Report 3 hiện tại, không tự khôi phục các ràng buộc đã bỏ.

Đây là ghi nhận phân phạm vi và phần cần đồng bộ Jira; lượt rà này không đăng comment hoặc sửa ticket.
