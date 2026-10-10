# Thành phần tham dự và vai trò trong cuộc họp — FE-06

Các sequence UC-27, UC-28a → UC-28c, UC-29, UC-30 tuân theo [convention chung](../README.md). Đặc tả nằm tại
[FE-06 trong UC Specification](../../01-requirements/05-use-case-specifications.md#fe-06--meeting-participant-management).

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

Ngày 10/10/2026 source `.txt` đã bỏ nhánh uỷ quyền và đổi số BR theo Report 3 mới; khi PNG lệch với `.txt`,
`.txt` là bản đúng.
