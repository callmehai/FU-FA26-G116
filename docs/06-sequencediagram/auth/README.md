# Sequence Authentication — UC-04

UC-04 có một sequence đầu-cuối: [source](UC04_AccessMeetingViaGuestInvitation.txt) / [PNG](UC04_AccessMeetingViaGuestInvitation.png). Luồng gồm kiểm tra lời mời, cấp mã tới email được mời, xác minh/tiêu thụ mã, nhập thông tin hiển thị tự khai và vào đúng Meeting; ALT-1 mở lại session đã xác minh mà không cần mã mới.

Đây là ngoại lệ có chủ đích cho convention một Actor → Boundary request: UC-04 mô tả trọn use case qua nhiều tương tác Guest trong cùng luồng. UC-29 chỉ xử lý phản hồi lời mời sau UC-04. Quyền Guest theo [ADR-0016](../../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md).

Xuất ảnh: `node scripts/export-diagrams.js UC04_` từ repository root. Đây là source thiết kế; không khẳng định endpoint Guest đã triển khai.
