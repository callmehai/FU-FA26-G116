# Sequence Authentication — UC-04

UC-04 đã có đặc tả; các source sau bổ sung đúng phần còn thiếu, không mở rộng UC placeholder:

- [UC04_RequestGuestVerificationCode.txt](UC04_RequestGuestVerificationCode.txt) / [ảnh](UC04_RequestGuestVerificationCode.png): cấp mã tới email được mời, kiểm tra hiệu lực lời mời.
- [UC04_VerifyGuestInvitation.txt](UC04_VerifyGuestInvitation.txt) / [ảnh](UC04_VerifyGuestInvitation.png): kiểm tra/tiêu thụ OTP, xác minh quyền kiểm soát email.
- [UC04_AccessMeetingViaGuestInvitation.txt](UC04_AccessMeetingViaGuestInvitation.txt) / [ảnh](UC04_AccessMeetingViaGuestInvitation.png): thông tin hiển thị tự khai, quyền vào đúng Meeting; mở lại verified session không cần mã mới.

Tách ba lần thực thi để tuân thủ [convention](../README.md): một request Actor → Boundary cho mỗi sequence. Cùng một UC-04, không thêm use case hay gate validation mới. Quyền Guest theo [ADR-0016](../../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md); UC-29 chỉ xử lý phản hồi lời mời sau UC-04.

Xuất ảnh: `node scripts/export-diagrams.js UC04_` từ repository root. Đây là source thiết kế; không khẳng định endpoint Guest đã triển khai.
