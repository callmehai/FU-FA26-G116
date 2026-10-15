# ADR-0019 — Lịch sử thành phần và ý định gửi thư cùng transaction

- Ngày: 14/10/2026
- Trạng thái: Đề xuất, đã hiện thực trên branch SCRUM-20; chờ team review
- Liên quan: ADR-0016, ADR-0017, UC-27–30, SCRUM-15/19/20, UC-04

## Bối cảnh

Gỡ rồi mời lại cần giữ phản hồi/điểm danh của lần cũ nhưng không khôi phục quyền cũ. Gửi email sau domain commit bằng việc dựng lại roster hiện tại có thể mất sự kiện role/grants/remove khi process chết. SMTP disabled không nên làm mất yêu cầu thông báo đã lưu.

## Quyết định

MeetingParticipant có Current/Removed, thời điểm/người gỡ, partial unique current(meeting,user), tối đa một current Chair. Re-add tạo ID mới. Guest là MeetingGuestInvitation riêng, không User/role nội bộ. Grant gắn với đúng assignment hoặc invitation bằng XOR owner, không theo UserId chung. Giữ evidence khi remove/revoke.

Mọi roster mutation khóa meeting, kiểm trạng thái/quyền và revision trong transaction. Revision bigint truyền qua JSON string, If-Match strong ETag giúp không ghi đè lựa chọn cũ. Chair swap demote/flush rồi promote trong cùng transaction. Roster readers dùng RepeatableRead để các page DTO và totals nhất quán trong một request.

MeetingInvitationDispatch giữ versioned snapshot và deduplication key trong transaction cùng domain/audit/notification. Worker materialize EmailMessage sau commit, khóa meeting trước dispatch; SMTP processor/lease hiện có đảm nhiệm delivery. Initial key theo assignment mới; migration backfill key cũ để tương thích. SMTP disabled vẫn giữ intent; re-enable xử lý sau khi kiểm current/state.

UC-04 sở hữu verification credential, OTP, profile, session production qua port. Default deny/unavailable cho đến khi tích hợp. Room callback chỉ là best effort transport hook; fresh authorization ở server luôn bắt buộc.

## Hệ quả và giới hạn

Cần ba bảng mới, index và migration preflight; dữ liệu legacy Guest/delegation/multiple Chair cần mapping được review. Down không được âm thầm làm mất lịch sử. Email có thể trùng nếu crash sau SMTP accept, trước mark Sent; không cam kết exactly-once mailbox. Room transport chưa triển khai; Guest E2E chờ UC-04. Chi tiết wire contract và các gate: [API](../03-api/09-thanh-phan-tham-du.md).
