# SCRUM-20 / SCRUM-97 — API thành phần tham dự

Baseline PO Hải ngày 20/10/2026, thay thế các quy tắc roster cũ của PR #92. UC-73 bàn giao Chủ tọa được thực hiện ngay trong SCRUM-20. UC/BR/sequence được BA đồng bộ tại Final Review; không dùng các mô tả grants/recovery cũ để tích hợp API này.

## 1. Quyền và bất biến

Mọi cuộc họp luôn có đúng một current Chair. Draft/Rejected/Approved cho phép 0..n Secretary; Published/InProgress phải giữ ít nhất một current Secretary. Publish vẫn yêu cầu Chair và Secretary active. WaitingApproval/Ended khóa thay đổi roster.

Người quản lý là current participant active có role Chair hoặc Secretary. Organizer, approver và admin không có quyền quản lý roster riêng. Ai qua MeetingReadPolicy hoặc Guest access hợp lệ đều đọc roster và phản hồi. Chair và Secretary đọc meeting mọi trạng thái; Member từ Published. Organizer/current approver giữ quyền đọc độc lập của workflow. Chair/Secretary được xem rejection reason, nhưng không tự có quyền đọc organizer-transfer metadata.

Không gỡ Chair, kể cả tự gỡ. Không hạ Chair nếu không có người thay trong cùng batch. Preparation: cả Chair/Secretary được phân công mọi role. Published/InProgress: Chair tự bàn giao; Secretary chỉ thay khi tài khoản Chair hiện tại inactive. Người nhận Chair phải current internal active và không đang là Secretary. Batch phải hạ Chair cũ xuống Member, nâng người mới lên Chair và có confirmation khớp cả hai assignment ID. Thao tác nguyên tử, không gọi hai request liên tiếp.

Không có per-participant grants, organizer secretary-recovery hoặc endpoint tổng hợp phản hồi riêng. Guest không có internal role; OTP/profile/session production thuộc [SCRUM-59](09-thanh-phan-tham-du-guest-uc04.md).

## 2. Transport

Base: `/api/meetings/{meetingId}`. Authentication bắt buộc. Không đủ quyền đọc: 404 rỗng; inactive internal: 403 rỗng; readable non-manager thực hiện quản lý: 403/MSG85. Mọi response của nhóm participants có `Cache-Control: no-store`.

Roster/context/options trả ETag `"participants-N"`. Mutations trừ own response bắt buộc `If-Match`; thiếu/sai cú pháp: 400/MSG276; revision cũ: 409/MSG275. Revision truyền dưới dạng string. Own response không tăng roster revision, nhưng vẫn phát event để FE tải phản hồi mới. No-op không phát event.

JSON mutation strict, phân biệt tên property, không nhận property lạ/trùng, enum sai hoa thường, giá trị null không hợp lệ; `additionalPermissions` bị từ chối. Tối đa 64 KiB. DELETE không body. Mutation không nhận query; reads chỉ nhận query được ghi rõ bên dưới.

## 2.1. Chọn người trước khi tạo cuộc họp

`GET /api/meetings/participant-options?q=&page=1&pageSize=20` dùng capability `MeetingCreator`, kiểm tra lại quyền và tài khoản active trong database. Không cần meeting ID hoặc `AdminUsers`. Chưa đăng nhập: 401; thiếu quyền: 403. Không mở rộng quyền `/api/users`.

Trả 200: `{items:[{id,fullName,email}],total,page,pageSize,roles:["Chair","Secretary","Member"]}`. Chỉ user active; bao gồm chính người tạo và người đang tham dự cuộc họp khác. Không trả thông tin quản trị, revision hoặc ETag; `Cache-Control: no-store`. Sort fullName rồi ID; `q` trim, tối đa 200 ký tự, tìm tên/email không phân biệt hoa thường và xử lý `%`/`_` như ký tự thường. Page mặc định 1, pageSize mặc định 20 (1..100), page vượt cuối trả items rỗng. Query lạ/trùng/sai: 400/MSG276; lỗi đọc: 500/MSG95. `total` tính sau search, trước paging.

FE dùng ID từ endpoint này để xây `participants` khi POST tạo họp. Backend vẫn kiểm tra lại user active và đúng một Chair khi lưu; kết quả lookup không bảo đảm trạng thái tài khoản giữ nguyên đến lúc POST. Editor-options theo meeting bên dưới tiếp tục dành cho quản lý roster đã tồn tại.

## 3. Đọc

| Method/path | Contract |
|---|---|
| GET /participant-context | `{meetingId,status,revision,myRole,permissions:{canManageParticipants,canReplaceChair,canRespond}}` |
| GET /participants?q=&page=1&pageSize=20 | `{items,total,page,pageSize,revision}` |
| GET /participants/editor-options?q=&page=1&pageSize=20 | Manager ở trạng thái editable; `{items:[{id,fullName,email}],total,page,pageSize,roles:["Chair","Secretary","Member"],revision}` |
| GET /invitation-response | Own current invitation: `{id,kind,response,respondedAt,canRespond}`, từ Published |

`myRole` là Chair/Secretary/Member/Guest/null. `canManageParticipants` = manager + editable. Preparation: `canReplaceChair` bằng manage; Published/InProgress: current Chair hoặc Secretary có Chair inactive; locked states false. `canRespond` chỉ current internal active (không phải Chair) hoặc Guest, ở Published/InProgress, **chưa trả lời** — xem §4.1.

Roster item:

```json
{
  "id":"assignment-or-invitation-guid","kind":"Internal","displayName":"Nguyễn An",
  "email":"an@example.test","role":"Member","userId":"user-guid","isActive":true,
  "invitedName":null,"selfDeclaredDisplayName":null,"selfDeclaredJobTitle":null,
  "response":"Pending","respondedAt":null
}
```

Guest có role/userId/isActive null; displayName ưu tiên profile tự khai, invitedName/email giữ evidence lời mời. Chỉ current internal và Guest chưa revoke. Sort displayName không phân biệt hoa thường, kind, id. `q` trim, tối đa 200 ký tự, tìm displayName/email/invitedName; page >=1, pageSize 1..100. `total` tính sau search. Không có bộ đếm phản hồi (`totals` đã bỏ, xem §4.1). Options loại mọi current internal và email trùng current Guest.

## 4. Ghi

| Method/path | Body | Thành công |
|---|---|---|
| POST /participants | `{userIds:["guid"],role:"Member"}` (1..100 unique active users) | 201/MSG96 + createdIds + revision |
| POST /guest-invitations | `{invitedName:"Khách",email:"guest@example.test"}` | 201/MSG96 + createdIds + revision |
| PUT /participants/assignments | `{changes:[{participantId,role}],chairReplacement?:{outgoingParticipantId,incomingParticipantId,confirmed:true}}` | 200/MSG100; Chair bàn giao sau Published: MSG257 |
| DELETE /participants/{participantId} | Không body | 200/MSG103 |
| DELETE /guest-invitations/{invitationId} | Không body | 200/MSG103 |
| PUT /invitation-response | `{response:"Accepted"}` hoặc Declined | 200/MSG105 + response |

Guest name trim 1..200, email trim 1..256 và đúng format; email current unique không phân biệt hoa thường và không trùng current internal. Changes 1..100 assignment ID không trùng. Reinvite tạo ID mới, không kế thừa response/attendance của assignment cũ. No-op: 200/MSG154.

Ví dụ bàn giao (gọi bằng token Chair ở Published/InProgress):

```json
{
  "changes":[
    {"participantId":"outgoing-assignment-guid","role":"Member"},
    {"participantId":"incoming-assignment-guid","role":"Chair"}
  ],
  "chairReplacement":{
    "outgoingParticipantId":"outgoing-assignment-guid",
    "incomingParticipantId":"incoming-assignment-guid",
    "confirmed":true
  }
}
```

### 4.1. Trả lời lời mời — theo AI-eCabinet (PO Hải 21/10/2026)

Câu trả lời chỉ là xác nhận trước, giống AI-eCabinet (`migrations/0038_meeting_responses.sql`, `modules/meetings/internal/respond.go`, commit `8108e91f`):

- **Trả lời một lần.** Gửi lại đúng câu đã ghi: 200/MSG154. Gửi câu khác: 409/MSG285, không ghi gì.
- **Chủ tọa không trả lời** (Chủ tọa điều hành cuộc họp): 409/MSG286. Response của Chủ tọa giữ `Pending` và không hiển thị.
- **Chưa trả lời coi như tham dự.** Không có bộ đếm Chờ/Chấp nhận/Từ chối.
- **Không đổi quyền gì.** Từ chối vẫn xem được cuộc họp, tài liệu và vào phòng họp; tác dụng duy nhất là cuộc họp biến khỏi lịch của người đó (`GET /api/meetings?excludeDeclined=true`, xem [05-quan-ly-cuoc-hop.md](05-quan-ly-cuoc-hop.md)). Chủ tọa không bị ẩn kể cả còn câu từ chối từ trước khi nhận vai trò.
- FE hỏi ở đầu màn chi tiết và trên thẻ cuộc họp ở lịch dạng danh sách, theo `canRespond` có sẵn trong dữ liệu cuộc họp; không hỏi trong tab thành phần. Tab chỉ hiện nhãn cạnh tên người đã trả lời.

Đã bỏ routes `POST /participants/secretary-recovery`, `PUT /guest-invitations/{id}/permissions`, `GET /invitation-responses`; client không gọi nữa.

## 5. Lỗi và realtime

| HTTP/code | Tình huống |
|---|---|
| 400/MSG97 | Add/Guest/DELETE payload không hợp lệ |
| 400/MSG101 | Changes/role/confirmation không hợp lệ |
| 400/MSG106 | Response không phải Accepted/Declined |
| 400/MSG259 | Recipient Chair không hợp lệ từ Published |
| 400/MSG276 | Query/If-Match sai |
| 403/MSG85 | Readable caller không phải manager |
| 409/MSG98 | WaitingApproval hoặc Ended khóa roster |
| 409/MSG107 | Không còn cho respond |
| 409/MSG109 | Mất Chair hoặc Secretary bắt buộc |
| 409/MSG256 | Secretary thay Chair active từ Published |
| 409/MSG275 | Stale revision |
| 409/MSG285 | Đã trả lời lời mời, gửi câu trả lời khác |
| 409/MSG286 | Chủ tọa trả lời lời mời |
| 413/MSG87; 415/MSG34 | Body quá lớn; content type sai |
| 500/MSG95 | Đọc roster thất bại |
| 503/MSG99, MSG102, MSG104, MSG108 | Lưu add, assignments, remove, response thất bại |

MSG152/153 ngừng dùng, không tái cấp. MSG258/260 vẫn dành sẵn.

Hub `/hubs/meetings`: join bằng `JoinMeeting(meetingId)`, nghe `participantsChanged` payload `{eventId,meetingId,revision,occurredAt}`. Sau commit có thay đổi, kể cả response, gửi đến hợp readers trước/sau: active participants, creator, approver lọc MeetingReadPolicy, Guest sessions hợp lệ qua adapter. Người vừa bị gỡ vẫn nhận invalidation, reload nhận 404. Payload không mang roster/PII. Lỗi transport ghi log, không rollback; hook disconnect room là port riêng.

## 6. Migration và delivery

Migration chưa lên main `20261014114845_MeetingParticipantLifecycleAndGuestInvitations` đã được sửa trực tiếp. Bỏ bảng grants; thêm Guest invitation/dispatch, assignment history và revision. SQL preflight vẫn chặn legacy Guest/delegation/multiple Chair, không xóa dữ liệu. Với meeting thiếu Chair: promote current creator nếu có; nếu chưa có thì insert Chair Pending/Unknown bằng UUID PostgreSQL. Backfill không ghi đè response/attendance cũ.

Initial delivery key của dòng cũ/backfill: `meeting:{meetingId}:published:{userId}`; dòng API mới: `participant:{assignmentId}:invited`. Worker không còn reconcile toàn bộ meeting Published cũ, nên không tự gửi lại lời mời cũ. Publish và mọi thay đổi cần thông báo stage durable dispatch cùng transaction; Guest reschedule dedup theo history event + invitation. SMTP disabled giữ intent.

Guest unavailable: probe readiness không khóa meeting; retry 1 phút, 5 phút, 30 phút rồi 6 giờ, không tiêu hao retry kỹ thuật. Khi Ready, khóa meeting → dispatch, kiểm current/status và prepare link trong transaction, materialize EmailMessage nguyên tử. Không gửi SMTP trong lock. Audit chỉ ghi các dòng đổi. Down chặn khi có Guest/dispatch/removed history; sau khi dùng tính năng không mặc định rollback bằng Down.

Xem [bàn giao FE/test thủ công](09-thanh-phan-tham-du-ban-giao.md) và [ADR-0021](../05-decisions/0021-vong-doi-thanh-phan-va-y-dinh-gui-thu.md).

### Sửa review 21/10/2026

- Dispatch Removed chỉ tạo thư khi Invitation của cùng assignment/invitation đã Materialized, hoặc có EmailMessage khớp initial delivery key (hỗ trợ dữ liệu cũ chưa có dispatch). Nếu chưa thì Suppressed. Materialized nghĩa là email đã được xếp hàng, không phải SMTP đã gửi thành công. Không dùng invitation cũ của lần mời trước để cho phép thư thu hồi của lần mới.
- Mọi exception kỹ thuật từ readiness probe, prepare và materialize đều tăng AttemptCount, backoff và Failed khi hết MaxAttempts; cancellation từ host được propagate, không tính thất bại. Log chỉ ghi loại exception, không ghi message/payload/token.
- Worker cô lập lỗi dispatch, kể cả lỗi khi truy vấn hoặc ghi trạng thái retry; hàng đợi email đã có (gồm reset mật khẩu) vẫn được xử lý ở scope riêng.
- Add internal trùng email current Guest trả 400/MSG97 với errors.userIds.
