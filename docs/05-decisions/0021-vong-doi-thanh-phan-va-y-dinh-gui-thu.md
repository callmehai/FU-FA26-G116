# ADR-0021 — Lịch sử thành phần và ý định gửi thư cùng transaction

- Ngày: 14/10/2026; cập nhật theo PO Hải 20/10/2026.
- Trạng thái: Đề xuất trong PR #92, chưa accepted trên main.
- Liên quan: SCRUM-20, SCRUM-97/UC-73, SCRUM-59/UC-04, SCRUM-41.

## Quyết định

Quyền roster chỉ theo current role Chair/Secretary của tài khoản active. Không có grants cá nhân hoặc organizer secretary-recovery. Mỗi meeting luôn đúng một current Chair từ tạo Draft; Published/InProgress giữ ít nhất một Secretary. Chair/Secretary đọc meeting mọi trạng thái. Workflow quyền creator/approver giữ độc lập.

MeetingParticipant có Current/Removed và evidence phản hồi/điểm danh. Reinvite tạo assignment mới. Guest lưu riêng trong MeetingGuestInvitation, không User/internal role. Hai bảng mới là GuestInvitation và InvitationDispatch; dispatch có XOR owner internal/Guest. Partial unique bảo vệ current(meeting,user), current Chair, current Guest email không phân biệt hoa thường.

Tạo meeting nhận roster internal, khóa các user theo thứ tự bằng FOR SHARE, lưu meeting/history/roster cùng transaction, không tạo thông báo Draft. Migration chưa lên main được sửa trực tiếp: preflight giữ dữ liệu lạ; thiếu Chair thì promote current creator hoặc insert Chair, giữ response/attendance cũ.

Mutation khóa meeting, kiểm quyền/trạng thái/revision. Chair swap demote/flush rồi promote nguyên tử. Published/InProgress chỉ Chair bàn giao, ngoại lệ Secretary thay Chair inactive; recipient active current internal không phải Secretary. If-Match và revision string chống ghi đè roster cũ. Response không tăng roster revision nhưng vẫn invalidation.

Trả lời lời mời theo AI-eCabinet (PO Hải 21/10/2026; eCab `migrations/0038_meeting_responses.sql`, `modules/meetings/internal/respond.go`, commit `8108e91f`): chỉ là xác nhận trước, trả lời một lần, Chủ tọa không trả lời, chưa trả lời coi như tham dự, không có bộ đếm. Từ chối không đổi quyền; chỉ ẩn cuộc họp khỏi lịch của người đó.

Audit chỉ ghi dòng thay đổi trước/sau. Sau commit phát participantsChanged tới hợp readers trước/sau; payload chỉ invalidation. Best effort không rollback domain. Hook room access riêng giữ nguyên.

Dispatch snapshot và dedup key lưu cùng domain/notification. Chỉ worker dispatch materialize EmailMessage, không còn legacy publication reconciliation. Guest readiness probe không khóa meeting khi unavailable, backoff 1m/5m/30m/6h không tiêu hao technical retries. Ready: meeting lock → dispatch lock → prepare credential trong cùng unit of work → EmailMessage; SMTP chạy sau commit.

## Hệ quả

Delivery key cũ/backfill giữ định dạng meeting:...:published:..., API assignment mới dùng participant:...:invited. Meeting đã Published trước migration không tự được gửi lại. Down chặn Guest/dispatch/removed history để tránh mất dữ liệu. SMTP crash sau accept có thể gửi trùng; không cam kết exactly-once mailbox.

Guest credential/OTP/profile/session do SCRUM-59 cung cấp, mặc định deny/unavailable. Room disconnect do SCRUM-41. API và bước test: [contract](../03-api/09-thanh-phan-tham-du.md), [handoff](../03-api/09-thanh-phan-tham-du-ban-giao.md).
