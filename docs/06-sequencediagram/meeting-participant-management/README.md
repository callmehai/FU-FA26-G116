# Thành phần tham dự và vai trò trong cuộc họp — FE-06

Các sequence UC-29, UC-30a → UC-30c, UC-31, UC-32, UC-33, UC-34 tuân theo [convention chung](../README.md). Cùng nằm trong FE-06 không có nghĩa cùng implementation task. Đặc tả của lần chỉnh sửa này nằm tại
[Report 3, FE-06](../../08-reports/Report-3_Software_Requirement_Specification.md#6-fe-06--meeting-participant-management).

- Secretary được gán quản lý danh sách (BR-036, BR-037). Organizer không còn quyền khởi tạo/phục hồi Secretary ( ngừng dùng).
  Secretary được gán có quyền xem meeting trước `Published` (BR-016, BR-017, BR-030) và chuẩn bị danh sách, gán Chair và các participant còn lại.
- `Draft`, `Rejected`, `Approved`, `Published`, `InProgress`: được quản lý danh sách theo role (BR-036, BR-037).
- `WaitingApproval`: chỉ xem, không thêm participant, đổi role hoặc xóa, kể cả màn hình đã mở trước lúc gửi duyệt.
- Organizer công bố từ `Approved` khi có đúng một Chair và ít nhất một Secretary hiện hành có tài khoản hoạt động
  (BR-021) — không còn điều kiện Meeting Session. Sequence UC-19 đã đồng bộ điều kiện này.
- Trước công bố, participant đã được gán nhưng chưa nhận lời mời. Khi công bố mới gửi lời mời;
  thêm người ở `Published`/`InProgress` thì gửi lời mời cho người được thêm (BR-031, BR-032, UC-30a).
- Participant phản hồi `Accepted` hoặc `Declined` (không còn uỷ quyền — BR-046) ở `Published`/`InProgress`.
  Từ `Ended` trở đi, hồ sơ chỉ đọc.

## Thay Chủ toạ — 18/10/2026

Theo quyết định PO 16/10/2026: Secretary chỉ gán hoặc thay Chair **trước khi công bố** (BR-041, BR-042); từ `Published`, sequence
UC-30b dừng ở EX-5 với MSG256. Từ `Published`/`InProgress`, Chair hiện hành tự giao vai trò cho một thành phần tham dự
nội bộ khác qua UC-34 (BR-041, BR-042): hai vai trò đổi cùng lúc, Chair cũ thành Member. Gỡ một người khỏi cuộc họp thì
các mục nghị sự người đó trình bày về "Chưa có người trình bày" trong cùng lần lưu (UC-30c, BR-064, BR-065).

## Ghi chú thay đổi để đối chiếu SCRUM-20

Ngày 03/10/2026, review FE-06 (số cũ F08) chốt khóa danh sách khi `WaitingApproval`, mở lại khi `Approved` hoặc
`Rejected`, và làm rõ quyền chuẩn bị cuộc họp của Secretary từ `Draft`. Đã đồng bộ UC-30a → UC-30c (số cũ UC-44–UC-46),
policy BR-016, BR-017 và điều kiện công bố UC-19. Đây là cập nhật yêu cầu và sơ đồ;
quyền truy cập và điều kiện công bố trong API hiện tại cần được đồng bộ khi triển khai FE-06.
Ghi chú này là nội dung để cập nhật ticket, chưa được đăng lên Jira trong lần chỉnh sửa tài liệu này.

## Xuất ảnh

Từ thư mục gốc repository, dùng công cụ hiện có:

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/meeting-participant-management
node scripts/export-diagrams.js docs/06-sequencediagram/meeting-management/UC19_PublishMeeting.txt
```

Giữ file `.txt` và `.png` đồng bộ. Công cụ cần truy cập `sequencediagram.org`.

Ngày 12/10/2026 đã cập nhật và xuất lại đủ 7 sequence `.txt`/`.png` theo Report 3
bằng script `scripts/export-diagrams.js`, chỉ cho UC-29, UC-30a/b/c, UC-31, UC-32, UC-33:

- UC-30a thêm nhiều người cùng một vai trò và quyền bổ sung; toàn bộ được thêm hoặc không thêm ai.
- UC-30b quản lý vai trò và quyền bổ sung, thay Chủ trì bằng một lần lưu cho cả hai người;
  giữ tên file cũ để các liên kết vẫn dùng được. Không thay đổi thì kết thúc với MSG154, không báo thành công giả.
- UC-29, UC-32 cho phép Chủ trì hoặc Thành viên hiện hành xem sau công bố khi được cấp đúng quyền xem tương ứng.
  Hai quyền bổ sung không cho phép quản lý thành phần, điểm danh hoặc thay đổi các quyền nghiệp vụ khác.
- UC-33 lưu điểm danh Có mặt/Vắng cho một/nhiều người được chọn cho thành viên và khách khi `InProgress`;
  chưa điểm danh thì để trống, người được gửi thay đổi hoặc lưu lỗi thì giữ nguyên toàn bộ giá trị cũ.
- BR-041, BR-042, BR-056, BR-057, BR-039, BR-016, BR-017 và MSG145–MSG154 là các mã mới. MSG141–MSG144 đã được dùng cho nghị sự nên được giữ nguyên.

Đây là cập nhật tài liệu; chưa xác nhận API đã triển khai các quy tắc mới và chưa đăng ghi chú lên Jira.

## PO 13/10/2026

Guest nhận quyền bổ sung như Member, không giới hạn hai quyền xem (UC-29, UC-32); UC-30a cảnh báo mức mật trong form bình thường, UC-30b sửa Guest permissions nhưng không role. UC-33 không đòi đầy đủ roster. UC-21 bỏ MSG155. UC-04 hiện có được sửa theo quyết định OTP và thông tin tự khai; các sequence bổ sung cho UC đang có nằm ở [auth/README.md](../auth/README.md). BR-040 đã chốt theo trạng thái; Organizer phục hồi Thư ký khi không còn ai. [ADR-0016](../../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md).

Phục hồi Thư ký có thể chọn một Member hiện hành đang hoạt động để đổi thành Secretary, không tạo participant trùng hoặc reset response/attendance.

## Phạm vi implementation task — rà ngày 13/10/2026

- **SCRUM-20:** roster, lời mời và phản hồi UC-29–UC-32. Khi xoá internal participant hoặc revoke Guest, giữ lịch sử response/attendance; dữ liệu historical không tính vào current roster, access hay số lượng role. UC-30c source đã mô tả việc bảo toàn này.
- **SCRUM-26:** UC-33 ghi/sửa điểm danh. Không đưa implementation điểm danh vào plan SCRUM-20 chỉ vì đặc tả/sơ đồ UC-33 cùng thư mục FE-06.
- Jira live vẫn dùng số UC/BR cũ và có tiêu chí lệch PO: SCRUM-20 còn delegation và giữ Secretary ở mọi trạng thái; SCRUM-26 còn self-attendance, Excused/Delegated và quorum. Khi triển khai phải đối chiếu các quyết định PO/Report 3 hiện tại, không tự khôi phục các ràng buộc đã bỏ.

Đây là ghi nhận phân phạm vi và phần cần đồng bộ Jira; lượt rà này không đăng comment hoặc sửa ticket.
