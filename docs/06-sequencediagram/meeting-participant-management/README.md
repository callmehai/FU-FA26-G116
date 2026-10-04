# Thành phần tham dự và vai trò trong cuộc họp — F08

Các sequence UC-37–42 tuân theo [convention chung](../README.md). Đặc tả và bảng vòng đời đã chốt nằm tại
[F08 trong UC Specification](../../01-requirements/05-use-case-specifications.md#vòng-đời-quản-lý-thành-phần-tham-dự-đã-chốt).

- Organizer khởi tạo Secretary đầu tiên. Secretary được gán có quyền xem meeting trước `Published`
  và chuẩn bị danh sách, gán Chair và các participant còn lại.
- `Draft`, `Rejected`, `Approved`, `Published`, `InProgress`: được quản lý danh sách theo role.
- `WaitingApproval`: chỉ xem, không thêm participant, đổi role hoặc xóa, kể cả màn hình đã mở trước lúc gửi duyệt.
- Organizer công bố từ `Approved` khi có Planned Meeting Session hợp lệ và đủ Chair/Secretary
  hiện hành có tài khoản hoạt động. Sequence UC-21 cũng được đồng bộ điều kiện này.
- Trước công bố, participant đã được gán nhưng chưa nhận lời mời. Khi công bố mới gửi lời mời;
  thêm người ở `Published`/`InProgress` thì gửi lời mời cho người được thêm.
- Participant phản hồi hoặc đổi phản hồi ở `Published`/`InProgress`. Từ `Ended` trở đi, hồ sơ chỉ đọc.

## Ghi chú thay đổi để đối chiếu SCRUM-20

Ngày 03/10/2026, review F08 chốt khóa danh sách khi `WaitingApproval`, mở lại khi `Approved` hoặc
`Rejected`, và làm rõ quyền chuẩn bị cuộc họp của Secretary từ `Draft`. Đã đồng bộ UC-38–40,
BR-78/84/88 dự thảo, policy BR-29 và điều kiện công bố UC-21. Đây là cập nhật yêu cầu và sơ đồ;
quyền truy cập và điều kiện công bố trong API hiện tại cần được đồng bộ khi triển khai F08.
Các BR/MSG dự thảo và thay đổi quyền ủy quyền liên quan ADR-0010 vẫn theo ghi chú trong đặc tả.
Ghi chú này là nội dung để cập nhật ticket, chưa được đăng lên Jira trong lần chỉnh sửa tài liệu này.

## Xuất ảnh

Từ thư mục gốc repository, dùng công cụ hiện có:

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/meeting-participant-management
node scripts/export-diagrams.js docs/06-sequencediagram/meeting-management/UC21_PublishMeeting.txt
```

Giữ file `.txt` và `.png` đồng bộ. Công cụ cần truy cập `sequencediagram.org`.

**Trạng thái lần cập nhật 03/10/2026:** source `.txt` đã đồng bộ; PNG F08 và UC-21 chưa được
xuất lại vì quyền truy cập dịch vụ export bị chặn. Các PNG hiện có là bản trước thay đổi lifecycle;
đọc source `.txt` để review quyết định mới. Cần xuất lại ảnh trước khi chốt bộ sơ đồ.
