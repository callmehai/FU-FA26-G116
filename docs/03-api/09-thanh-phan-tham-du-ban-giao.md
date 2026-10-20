# SCRUM-20 / SCRUM-97 — bàn giao Frontend và test thủ công

Baseline PO 20/10/2026; tài liệu do AI hỗ trợ cập nhật ngày 21/10/2026. Backend bao gồm UC-73 bàn giao Chủ tọa trong PR #92. FE do Hải triển khai; UC/BR/sequence được BA chốt tại Final Review.

## Contract FE cần đổi

1. Form tạo họp gửi `participants:[{userId,role}]`, 1..100 current internal active, đúng một Chair. Người tạo không bắt buộc tham dự; không bắt buộc Secretary khi tạo. Thiếu/không hợp lệ: 400/MSG277, `errors` là dictionary string theo key participants hoặc participants[i].userId/.role. PUT meeting không nhận participants.
2. Chi tiết/create trả `chair:{userId,fullName}|null`, `secretaries:[]`, `myMeetingRole`. Dùng để hiện vai trò và trạng thái sẵn sàng; không thay authorization server.
3. Context có `myRole` và đúng ba flags `canManageParticipants,canReplaceChair,canRespond`. Bỏ recovery UI và mọi checkbox quyền bổ sung.
4. Roster gồm response/respondedAt và totals toàn bộ current roster; bỏ gọi invitation-responses. Options bỏ mode/permissionCatalog, roles luôn Chair/Secretary/Member.
5. Bàn giao dùng một PUT assignments có batch demote/promote và confirmation khớp hai assignment IDs. Published/InProgress: chỉ Chair bàn giao, hoặc Secretary thay Chair inactive; recipient không phải Secretary và phải active/current. Thành công Chair bàn giao: MSG257; đổi role khác: MSG100.
6. Giữ ETag/If-Match và revision dạng string. 409/MSG275 phải tải lại và để người dùng xác nhận lại. MSG276 query/header sai. Không dùng MSG252/253 (SCRUM-96).
7. Sau save hoặc `participantsChanged`, reload context/roster/detail. Event có thể cùng revision khi chỉ thay response; không bỏ event chỉ vì revision không tăng. Dedupe eventId. Người bị gỡ nhận invalidation và reload404 thì rời màn chi tiết. Không báo “đã gửi email” chỉ từ save thành công.
8. Các mã MSG256/257/259 đã triển khai trong SCRUM-20/SCRUM-97; MSG258/260 còn dành sẵn. MSG152/153 ngừng dùng. SCRUM-26 phải remap MSG277/278 trước tích hợp.
9. Guest OTP/profile/session thật còn chờ SCRUM-59; room disconnect chờ SCRUM-41. Không nghiệm thu Guest production bằng test JWT/adapters.

## Test thủ công

Dùng database Development riêng, đăng nhập qua API; giữ token trong Postman environment, không export secrets. Import [collection](postman/scrum20-participants.postman_collection.json), điền token đúng actor, meetingId, userId, outgoingParticipantId, incomingParticipantId. GET context tự lưu ETag. API trực tiếp mặc định localhost:5225; FE vẫn relative URL qua Vite.

Seed `[SCRUM20-DEV]` có 7 trạng thái, ID prefix `20000000-0000-7000-8000-`, suffix 1..7 tương ứng Draft, Rejected, Approved, WaitingApproval, Published, InProgress, Ended. Mỗi roster có Chair/Secretary/Member. Seed insert-only không reset scenario đã chỉnh.

| Bước | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Tạo họp có Chair + Member; rồi thử bỏ Chair, trùng user, inactive user | 201/MSG29 hoặc 400/MSG277; lỗi không để lại meeting/history/roster |
| 2 | Chair đọc Draft và WaitingApproval; Member đọc cùng ID | Chair200; Member404 |
| 3 | Chair/Secretary thêm, đổi, gỡ Member/Secretary ở Draft | Thành công; có đúng một Chair; chưa có dispatch |
| 4 | Gỡ Chair hoặc Chair tự hạ mà không thay | 409/MSG109 |
| 5 | Secretary thay Chair active ở Published/InProgress | 409/MSG256 |
| 6 | Chair bàn giao Member, đúng confirmation | 200/MSG257; Chair cũ Member, người mới Chair; 2 role-change notifications/dispatch |
| 7 | Bàn giao cho Secretary/inactive/removed/nonexistent | 400/MSG259 |
| 8 | Deactivate Chair qua quản lý users, Secretary thay bằng Member active | 200/MSG100; Secretary cuối vẫn được giữ |
| 9 | Chair gỡ/hạ Secretary cuối ở Published/InProgress | 409/MSG109; có Secretary khác thì được |
| 10 | Member/Organizer không role/current approver đọc roster và thử sửa | GET200; mutation403/MSG85 |
| 11 | Accept/Decline bằng người được mời; search không khớp và paging roster | MSG105; attendance không đổi; totals vẫn tính toàn roster |
| 12 | Hai tab cùng ETag sửa roster | Một thành công; request cũ409/MSG275 |
| 13 | Hai kết nối hub, gỡ một participant | Cả người quản lý và người bị gỡ nhận participantsChanged; người bị gỡ reload404 |
| 14 | Mời/revoke Guest, mời lại | ID mới, lịch sử cũ giữ; Guest không được cấp internal role |
| 15 | Dời lịch Published có Guest | Durable dispatch Rescheduled theo event/invitation, kể cả dependency chưa Ready |

SMTP thật không thuộc kết quả automated tests. Khi kiểm mail dùng SMTP sandbox, kiểm lịch/role/link và không gửi tới người thật. Guest default giữ BlockedDependency với backoff 1m/5m/30m/6h. Integration thật theo [contract UC-04](09-thanh-phan-tham-du-guest-uc04.md). Migration không tự chạy lại trên database đã ghi nhận cùng ID cũ: môi trường thử đã apply bản PR cũ cần dựng database thử mới hoặc owner quản lý nâng cấp riêng; không sửa schema bằng tay.

Kết quả kiểm thử hiện tại ở [test report](09-thanh-phan-tham-du-ket-qua-kiem-thu.md).
