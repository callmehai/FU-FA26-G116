# SCRUM-20 — bàn giao tích hợp Guest cho UC-04

**Cập nhật SCRUM-116 (30/10/2026):** UC-04 đã cung cấp `IParticipantGuestAccess`, `IGuestInvitationLinkProvider` và resolver production ([contract UC-04](16-truy-cap-khach-uc04.md)); "Blocked by UC-04" bên dưới không còn đúng. PO đã chốt 30/10/2026 (Jira SCRUM-116), gate 6 và mục consumer bên dưới đã sửa theo: Khách **không** đọc được gì khi cuộc họp `Ended` (roster, response, attendance, realtime đều `404`/không gửi), dùng rule riêng `ParticipantRules.GuestCanView`; rule của người nội bộ không đổi.

Đã thống nhất tách OTP, profile tự khai và session production khỏi SCRUM-20. Backend SCRUM-20 lưu lời mời và tiêu thụ kết quả xác thực, **chưa cung cấp Guest login thật**. Không dùng test JWT/adapters để demo nghiệm thu production.

Change note đã ghi tại [SCRUM-20 comment 10221](https://sep490g116.atlassian.net/browse/SCRUM-20?focusedCommentId=10221). Ticket Guest verification/session hiện có: [SCRUM-59](https://sep490g116.atlassian.net/browse/SCRUM-59), assignee Nguyễn Quang Huy tại ngày 15/10/2026. Owner cần chốt chi tiết profile tự khai; dùng chung invitation SCRUM-20 và nối adapters bên dưới, không tạo luồng invitation trùng.

## Ownership và các port

| Port | Chủ triển khai production | Nghĩa vụ |
|---|---|---|
| `IMeetingGuestIdentityResolver` (Web) | UC-04 | Chỉ resolve trusted principal được authentication handler xác thực thành MeetingId/InvitationId/SessionId. Không đọc ID tùy ý từ query/body/header |
| `IParticipantGuestAccess` (Domain) | UC-04 | CanAccessAsync đọc trạng thái session thật mỗi request: đúng binding, verified email, chưa hết hạn/revoke; không cache quyền vào JWT rồi tin mãi |
| `ListReadersAsync` | UC-04 | Trả session verified/current/non-expired/non-revoked cho realtime participantsChanged; dùng chung identity binding với Agenda hub |
| `GetProfilesAsync` | UC-04 | Một batch invitation IDs; trả DisplayName/JobTitle tự khai. Email và invitedName evidence vẫn thuộc invitation; không ghi đè bằng self-declared profile |
| `IGuestInvitationLinkProvider` | UC-04 | IsAvailableAsync là readiness probe read-only không tạo credential, không khóa meeting; PrepareAsync tạo/reuse link xác minh email, không tạo authenticated session hoặc gửi SMTP. Ready/Unavailable/Invalid. URL cùng application origin, tuyệt đối http(s) |
| `IMeetingParticipantAccessPublisher` | Room owner | Best effort sau commit để disconnect transport tương lai; không dùng thay authorization. Mặc định no-op vì chưa có room SDK |

Default implementations: guest access false, identity null, profile batch rỗng, verification link Unavailable. Guest invitation vẫn lưu được và dispatch giữ BlockedDependency; internal tiếp tục dùng bình thường.

UC-04 đăng ký adapter scoped thay các default (`Replace` hoặc đăng ký concrete sau participants). Không cần sửa participant endpoints. Resolver phải dùng claim namespace riêng, không nhận token internal đồng thời là Guest: service từ chối actor có cả UserId và Guest identity. Claim `sub` Guest không được là một User GUID.

Readiness probe chạy trước meeting lock; unavailable retry 1m/5m/30m/6h. PrepareAsync chạy bên trong transaction materialize: meeting lock → dispatch lock → provider → EmailMessage + dispatch SaveChanges. Dùng cùng DbContext/unit of work; **không commit riêng**, không gọi email/SMS/HTTP trong lock. Nếu kiến trúc UC-04 cần external persistence phải chốt recovery/idempotency contract và test riêng trước tích hợp. Provider không rotate secret mỗi retry; key/expiry/hash do UC-04 quyết định, không lưu raw OTP/token trong payload/audit/log. EmailMessage chứa link để gửi, quyền đọc bảng này phải giới hạn như email outbox hiện có.

Consumer kiểm thêm meeting Published/InProgress (Khách không đọc gì khi `Ended`, PO 30/10/2026) và invitation chưa revoked. Respond chỉ Published/InProgress. Revoke invitation làm mọi session của invitation không còn hiệu lực ở consumer; re-invite tạo ID mới. Profile/GetProfiles lỗi kỹ thuật trả lỗi đọc, không giả danh sách rỗng thành thành công.

## Agenda, hub và room

UC-04 phải cung cấp cả adapters Agenda: [contract SCRUM-25](08-agenda-contract-tich-hop-guest-uc04.md). Chia sẻ cùng invitation/session data; `ListReadersAsync` chỉ trả session verified/current/non-expired/non-revoked. Hub kiểm lại lúc join và khi phát event. SCRUM-20 không tự nối một Guest giả vào Agenda. Room/vote/document sẽ tiêu thụ cùng current invitation policy theo role; chưa có room disconnect production trong task này.

## Gate nghiệm thu Guest thật

1. Mail sandbox → verification link thật → OTP đúng email → profile → session thật; không tăng User rows.
2. Own response Accepted/Declined; Guest không đọc nội bộ hoặc cuộc họp khác; ID đoán được không cấp quyền.
3. Guest hợp lệ đọc roster/response/totals; không có grants. Kiểm REST và hub/event recipients, session expiry/revoke theo TimeProvider trong test.
4. Hai session một invitation; revoke riêng session và revoke invitation toàn bộ. Link cũ không hồi sinh invitation; re-invite có ID khác.
5. Retry/crash provider trước/sau persist cùng transaction; không duplicate credential rotate hoặc EmailMessage. Link Ready đúng origin, không lộ room URL trước verify.
6. Cuộc họp `Ended`: Guest không đọc được roster, response, attendance hay realtime (`404`/không gửi) và không respond; inactive internal không thể dùng đường Guest để vượt account policy.

Hiện có consumer integration tests với test-only adapter cho verified/unverified/expired/mixed identity/revoked invitation và delivery dependency. **Guest E2E production: Blocked by UC-04**, không tính các test đó là đã nghiệm thu OTP/session thật.

Cập nhật review 21/10/2026: Agenda, Participants và MeetingHub dùng chung `MeetingGuestSession` và `IMeetingGuestIdentityResolver`; UC-04 chỉ đăng ký một resolver. Các port kiểm quyền theo feature vẫn tách riêng, dùng cùng invitation/session storage. Tên group Guest được tạo duy nhất bằng `MeetingHub.GuestGroup`.
