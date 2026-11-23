# Spec patch — nhóm lich-phieu
n> **Lưu ý:** nếu mục này lệch với `lich-phieu-spec-patch.md` thì theo file `lich-phieu-ex-final.md` (sinh từ diagram sau lượt chọn cuối của BA).

Nhóm: calendar-sharing, voting-management, minutes-management. Áp dụng vào Report 3 (DOCX) ở bước 5.

## Quy ước

- EX giữ lại: ghi số mới và MSG.
- EX bỏ/gộp: ghi số cũ, lý do (hạ tầng / trùng MSG / vượt trần).
- Các bước "flow resumes at…" hoặc tham chiếu EX cũ trong spec phải sửa theo nhãn mới.

## calendar-sharing

### UC-84a – Request Calendar Share
- Giữ: EX-1 (MSG448, quyền), EX-2 (MSG449, đã có share), EX-3 (MSG443, khoảng ngày không hợp lệ).
- Bỏ: EX-4 (chủ lịch không còn active — vượt trần), EX-5 (MSG447, không lưu được — hạ tầng), EX-6 (MSG467, không đọc được danh sách — hạ tầng).
- Sửa spec: bỏ EX-4, EX-5, EX-6; đánh lại EX-1..EX-3.

### UC-84b – Share Calendar Directly
- Giữ: EX-1 (MSG455, người nhận không nhận được share), EX-2 (MSG449, đã có share), EX-3 (MSG443, khoảng ngày không hợp lệ).
- Bỏ: EX-4 (MSG447, hạ tầng), EX-5 (tài khoản owner không active — vượt trần), EX-6 (MSG467, hạ tầng).

### UC-84c – Respond to Calendar Share
- Giữ: EX-1 (không phải bên được hỏi), EX-2 (MSG450, không còn Pending), EX-3 (MSG443, khoảng ngày không hợp lệ).
- Bỏ: EX-4 (MSG447, hạ tầng), EX-5 (MSG468, tài khoản người gửi bị vô hiệu hoá — vượt trần; gộp vào EX-2).

### UC-84d – Change Calendar Share
- Giữ: EX-1 (không phải một trong hai người), EX-2 (MSG450, không cho phép thay đổi), EX-3 (MSG443, khoảng ngày không hợp lệ).
- Bỏ: EX-4 (MSG447, hạ tầng).

### UC-84e – Withdraw or End Calendar Share
- Giữ: EX-1 (không phải một trong hai người), EX-2 (MSG450, không còn gì để rút/kết thúc).
- Bỏ: EX-3 (MSG447, hạ tầng).

### UC-84f – View Calendar Shares
- Giữ: (không còn EX nghiệp vụ).
- Bỏ: EX-1 (MSG453, hạ tầng).

### UC-85a – Find Meeting Time Slots
- Giữ: EX-1 (MSG444, tham số không hợp lệ), EX-2 (MSG448, không có quyền tạo cuộc họp).
- Bỏ: EX-3 (MSG453, hạ tầng).

### UC-85b – Create Draft Meeting from Slot
- Giữ: EX-1 (MSG446, khung giờ không còn trống), EX-2 (MSG448, không có quyền tạo cuộc họp).
- Bỏ: EX-3 (MSG453, hạ tầng), EX-4 (MSG35, hạ tầng).

### UC-85c – View Shared Calendar
- Giữ: EX-1 (MSG448, không có quyền tạo cuộc họp) — đánh lại từ EX-2.
- Bỏ: EX-1 (MSG453, hạ tầng).

## voting-management

### UC-52a – View Meeting Votes
- Giữ: EX-1 (MSG205, không truy cập được biểu quyết).
- Bỏ: EX-2 (MSG206, hạ tầng).

### UC-52b – View Vote Status
- Giữ: EX-1 (MSG205, không truy cập được biểu quyết).
- Bỏ: EX-2 (MSG208, hạ tầng).

### UC-53 – Create / Configure Vote
- Giữ: EX-1 (MSG209, thông tin biểu quyết không hợp lệ), EX-2 (MSG213, thao tác nháp không còn cho phép), EX-3 (MSG205, không truy cập được) — đánh lại từ EX-6.
- Bỏ: EX-3 (MSG214, hạ tầng), EX-4 (MSG215, hạ tầng), EX-5 (MSG216, hạ tầng).

### UC-54 – Open Vote
- Giữ: EX-1 (MSG209, cấu hình không hợp lệ), EX-2 (MSG218, không còn cho phép mở), EX-3 (MSG219, đã có biểu quyết khác đang mở).
- Bỏ: EX-4 (MSG220, không có cử tri hợp lệ — vượt trần), EX-5 (MSG221, hạ tầng), EX-6 (MSG205, vượt trần).

### UC-55 – Cast Vote
- Giữ: EX-1 (MSG222, chưa chọn phương án), EX-2 (MSG223, phương án không hợp lệ), EX-3 (MSG224, không còn đủ điều kiện).
- Bỏ: EX-4 (MSG225, bỏ phiếu đã kết thúc — vượt trần), EX-5 (MSG226, đã bỏ phiếu — vượt trần), EX-6 (MSG228, hạ tầng), EX-7 (MSG205, vượt trần), EX-8 (MSG252, chưa xác nhận đọc tài liệu — vượt trần).

### UC-56 – Close Vote
- Giữ: EX-1 (MSG230, không còn cho phép đóng), EX-2 (MSG205, không truy cập được) — đánh lại từ EX-3.
- Bỏ: EX-2 (MSG231, hạ tầng).

### UC-57 – Record Vote Conclusion
- Giữ: EX-1 (MSG232, kết luận không hợp lệ), EX-2 (MSG233, đã ghi kết luận), EX-3 (MSG234, không còn cho phép ghi).
- Bỏ: EX-4 (MSG236, hạ tầng), EX-5 (MSG205, vượt trần).

### UC-76a – Attach Documents to Vote
- Giữ: EX-1 (MSG213, không còn cho phép thay đổi), EX-2 (MSG170, tài liệu không còn trong cuộc họp), EX-3 (MSG266, đã gắn rồi).
- Bỏ: EX-4 (MSG267/MSG166, hạ tầng), EX-5 (MSG205, vượt trần).

### UC-76b – Detach Document from Vote
- Giữ: EX-1 (MSG213, không còn cho phép thay đổi), EX-2 (MSG269, không còn gắn), EX-3 (MSG205, không truy cập được) — đánh lại từ EX-4.
- Bỏ: EX-3 (MSG270, hạ tầng).

## minutes-management

### UC-58 – View Meeting Minutes
- Giữ: EX-1 (không đọc được bản ghi — đánh lại từ EX-2), EX-2 (cuộc họp không truy cập được — đánh lại từ EX-3), EX-3 (MSG405, biên bản mật).
- Bỏ: EX-1 (MSG403, hạ tầng).

### UC-59 – Generate Meeting Minutes
- Giữ: EX-1 (MSG397, không phải Chủ tọa/Thư ký), EX-2 (MSG398, cuộc họp chưa kết thúc), EX-3 (MSG399, đã có bản nháp).
- Bỏ: EX-4 (MSG400, không còn thay đổi được — vượt trần), EX-5 (MSG404, hạ tầng), EX-6 (cuộc họp không truy cập được — vượt trần), EX-7 (MSG456, không xây lại từ bản sửa — vượt trần).

### UC-60 – Edit Meeting Minutes
- Giữ: EX-1 (MSG401, người khác vừa lưu), EX-2 (MSG402, nội dung không hợp lệ), EX-3 (MSG400, không còn thay đổi được).
- Bỏ: EX-4 (MSG397, vượt trần), EX-5 (MSG404, hạ tầng), EX-6 (MSG405, vượt trần), EX-7 (cuộc họp không truy cập được — vượt trần), EX-8 (MSG406, vượt trần).

### UC-61 – Submit Meeting Minutes for Approval
- Giữ: EX-1 (MSG425, chỉ Thư ký mới gửi), EX-2 (MSG426, cuộc họp chưa kết thúc), EX-3 (MSG400, đã gửi/ban hành).
- Bỏ: EX-4 (MSG401, vượt trần), EX-5 (MSG404, hạ tầng), EX-6 (không truy cập được — vượt trần).

### UC-64 – Approve and Issue Meeting Minutes
- Giữ: EX-1 (MSG428, chỉ Chủ tọa mới duyệt), EX-2 (MSG429, không chờ duyệt), EX-3 (MSG401, thay đổi sau khi mở).
- Bỏ: EX-4 (MSG404, hạ tầng), EX-5 (không truy cập được — vượt trần).

### UC-65 – Return Meeting Minutes for Revision
- Giữ: EX-1 (MSG428, chỉ Chủ tọa mới trả lại), EX-2 (MSG429, không chờ duyệt), EX-3 (MSG431, lý do không hợp lệ).
- Bỏ: EX-4 (MSG404, hạ tầng), EX-5 (không truy cập được — vượt trần), EX-6 (MSG401, vượt trần).

### UC-83 – Correct Issued Meeting Minutes
- Giữ: EX-1 (MSG397, không phải Chủ tọa/Thư ký), EX-2 (MSG433, không phải bản đã ban hành), EX-3 (MSG434, đang có bản sửa).
- Bỏ: EX-4 (MSG404, hạ tầng), EX-5 (không truy cập được — vượt trần).
