# ADR-0024: Version riêng cho điểm danh

- **Trạng thái:** Đã chấp nhận trong phạm vi triển khai SCRUM-26; chờ team review branch.
- **Ngày:** 2026-10-20
- **Người quyết định:** Người phụ trách Backend SCRUM-26, xác nhận trong phiên triển khai.

## Bối cảnh

Hai Secretary có thể sửa cùng người từ cùng snapshot. Chỉ khóa lúc ghi không đủ phát hiện draft cũ nếu không có token. PostgreSQL xmin tự đổi với mọi UPDATE, gây xung đột điểm danh khi chỉ phản hồi lời mời hoặc đổi vai trò. Yêu cầu là chỉ sửa database khi có lý do, không cấm migration.

## Quyết định

Thêm AttendanceVersion bigint NOT NULL DEFAULT 0 vào MeetingParticipant và MeetingGuestInvitation. Không thêm bảng/index/trigger. Tăng checked đúng một lần khi attendance thực sự đổi, cùng recorder/time và audit trong transaction. No-op giữ version. Role/response/grants không tăng version. Tất cả writer điểm danh phải dùng AttendanceService và giao thức khóa meeting rồi target.

Không dùng IsRowVersion/xmin/EF concurrency token toàn entity. Writer khóa meeting, kiểm actor/state, khóa target có thứ tự, đọc phiên bản mới nhất sau khóa và so token trước khi sửa. Batch có một stale entry rollback toàn bộ. Token SHA256 gắn meeting/kind/entry/version, truyền opaque string để FE không gặp giới hạn số nguyên JavaScript. Token không phải secret hoặc authorization.

## Phương án đã cân nhắc

| Phương án | Đánh giá |
|---|---|
| xmin | Không thêm cột nhưng UPDATE không liên quan cũng stale; không chọn |
| UpdatedAt | Cùng vấn đề phạm vi và phụ thuộc độ chính xác thời gian; không chọn |
| Hash trạng thái attendance | Không nhận ra Present → Absent → Present; không chọn |
| AttendanceVersion | Hai cột nhỏ, đúng phạm vi xung đột, dễ kiểm thử; chọn |

## Hệ quả

Phải giữ discipline mọi writer attendance tăng version; test khóa, ABA, no-op và response/role regression bảo vệ quy tắc. Cột/version lịch sử khởi tạo 0, không viết lại attendance. Migration Down chỉ dành DB dùng một lần vì xóa token history. Hai người lưu cùng token: transaction thay đổi commit thành công trước thắng, không cam kết thứ tự bấm nút.

Xem [API](../03-api/10-diem-danh.md) và [kiểm thử](../03-api/10-diem-danh-ket-qua-kiem-thu.md).
