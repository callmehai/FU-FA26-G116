# SCRUM-20 — API thành phần tham dự và phản hồi lời mời

Backend theo [kế hoạch đã duyệt](../02-design/09-scrum20-ke-hoach-backend.md), baseline PO 13/10/2026, UC-27/28a/28b/28c/29/30. Frontend và UC-04 production triển khai riêng. Các request dưới đây dùng relative path, Bearer authentication hiện có, JSON camelCase; enum PascalCase. Mọi response của nhóm endpoint có `Cache-Control: no-store`.

## 1. Quyền và trạng thái

- Quyền quản lý thuộc **User active đang được phân công Secretary** trong cuộc họp; không suy từ system role, capability claim hoặc body.
- Organizer active được recovery một Secretary khi **không còn current Secretary**, chỉ ở Draft/Rejected/Approved. Secretary inactive vẫn là current, không mở recovery. Recovery Member giữ nguyên assignment, phản hồi, điểm danh và grants; tạo assignment mới nếu chưa có.
- Sửa thành phần ở Draft/Rejected/Approved/Published/InProgress. WaitingApproval và Ended khóa sửa. Luôn tối đa một Chair; Published/InProgress phải giữ đúng một Chair và ít nhất một Secretary. Công bố cần Chair và Secretary active.
- Secretary đọc roster mọi trạng thái; Chair có quyền mặc định từ Published. Member/Guest cần grant `ViewMeetingParticipants`. Quyền summary `ViewInvitationResponses` độc lập và chỉ có hiệu lực Published/InProgress/Ended; Chair/Secretary mặc định có quyền này ở các trạng thái đó.
- Organizer/Approver có quyền đọc cuộc họp không đồng nghĩa có quyền roster/summary. Người chỉ có quyền summary thấy tên/email trong summary nhưng không có search/options/roster.
- Phản hồi của chính mình: Accepted/Declined trong Published/InProgress. Không delegation. Không cập nhật attendance, role, grants hoặc trạng thái cuộc họp. Cùng giá trị là no-op, giữ RespondedAt.
- Gỡ mềm/revoke giữ lịch sử. Mời lại tạo ID mới, Pending, không kế thừa grants. Quyền đọc độc lập của Organizer/Approver vẫn còn. Các API Meeting/Agenda, presenter options, notification target và Agenda hub dùng current assignment.
- Guest không tạo User và không giữ role nội bộ. Guest production mặc định bị từ chối, xem [hợp đồng UC-04](09-thanh-phan-tham-du-guest-uc04.md).

Không đăng nhập → 401; account inactive → 403; không được đọc tài nguyên/view cụ thể → 404; có quyền đọc meeting nhưng thiếu quyền quản lý → 403. Body sai không làm lộ sự tồn tại của meeting bị cấm: service kiểm quyền trước khi trả validation.

## 2. Endpoint

Prefix: `/api/meetings/{meetingId}`. Tất cả ID là GUID; `participantId` là ID assignment, **không phải UserId**; `guestInvitationId` là ID lời mời Guest.

| Method/path | Input | Kết quả/quyền |
|---|---|---|
| GET `/participant-context` | Không query | Context không chứa roster/PII; các flags quyền, status, revision |
| GET `/participants` | `q`, `page`, `pageSize` | Roster; quyền ViewMeetingParticipants |
| GET `/participants/editor-options` | `q`, `page`, `pageSize` | User active hợp lệ, roles/catalogue; Secretary hoặc Organizer recovery |
| GET `/invitation-response` | Không query | Phản hồi của chính actor; từ Published |
| GET `/invitation-responses` | `page`, `pageSize`; **không q** | Summary toàn bộ current invitees, tổng trạng thái độc lập trang |
| POST `/participants/secretary-recovery` | `{ "userId": "GUID" }` | 201/MSG96 tạo hoặc 200/MSG100 promote Member |
| POST `/participants` | `{ "userIds": ["GUID"], "role": "Member", "additionalPermissions": [] }` | 201/MSG96; cả batch atomic, 1–100 user |
| POST `/guest-invitations` | `{ "invitedName": "Tên khách", "email": "guest@example.test", "additionalPermissions": [] }` | 201/MSG96, không tạo User |
| PUT `/participants/assignments` | `changes` và tùy trường hợp `chairReplacement`, xem bên dưới | 200/MSG100 hoặc MSG153/MSG154 |
| PUT `/guest-invitations/{guestInvitationId}/permissions` | `{ "additionalPermissions": [] }` | 200/MSG153 hoặc MSG154 |
| DELETE `/participants/{participantId}` | Không body | 200/MSG103 |
| DELETE `/guest-invitations/{guestInvitationId}` | Không body | 200/MSG103 |
| PUT `/invitation-response` | `{ "response": "Accepted" }` hoặc Declined | 200/MSG105; no-op MSG154 |

Query phân trang mặc định `page=1`, `pageSize=20`, pageSize tối đa100, q tối đa200 ký tự. Không chấp nhận query lạ/trùng key, query trên mutation. Roster sort displayName rồi kind/ID ổn định; options sort FullName/ID. Search không coi `%` hoặc `_` là wildcard SQL. Total luôn tính sau lọc.

## 3. ETag và validation

GET context/roster/options và mutation roster thành công trả `ETag: "participants-0"` (tăng sau mỗi thay đổi thật). **Mọi mutation roster**, kể cả recovery và DELETE, bắt buộc gửi `If-Match` bằng ETag vừa đọc. Riêng PUT own response không yêu cầu If-Match và không tăng roster revision.

`revision` trong JSON là chuỗi số thập phân; không chuyển sang JavaScript Number vì DB dùng bigint. Header là strong ETag, không chấp nhận wildcard, weak tag hoặc nhiều giá trị. Thiếu/sai → 400/MSG239; revision cũ → 409/MSG238. Reload rồi để người dùng xác nhận lại, không tự replay thao tác cũ.

JSON tối đa64KiB kể cả chunked. Chỉ `application/json`/JSON media type; không chấp nhận property lạ, duplicate ở mọi cấp, sai casing, null (ngoại trừ `chairReplacement: null`), item null hoặc enum dạng số. IDs phải non-empty; role chỉ Chair/Secretary/Member. Email Guest trim, tối đa256; invitedName trim, 1–200 ký tự. Email trùng current Guest hoặc email current internal bị từ chối không phân biệt hoa thường. Một user invalid làm cả batch thất bại.

Catalogue hiện có hai grant, trả qua editor-options dưới dạng `{code,name}`. New grants chỉ cấp cho Member/Guest. Khi đổi role, omit `additionalPermissions` giữ nguyên; `[]` xóa; array là tập cuối. Các grants hợp lệ sẵn có được giữ khi promote Chair/Secretary, không phát sinh grant mới cho hai role đó. Không có quyền quản lý thành phần trong catalogue.

Ví dụ đổi Chair atomic, đồng thời thay Secretary nếu cần:

```json
{
  "changes": [
    { "participantId": "OUTGOING_CHAIR_ID", "role": "Member" },
    { "participantId": "INCOMING_CHAIR_ID", "role": "Chair" }
  ],
  "chairReplacement": {
    "outgoingParticipantId": "OUTGOING_CHAIR_ID",
    "incomingParticipantId": "INCOMING_CHAIR_ID",
    "confirmed": true
  }
}
```

IDs ví dụ phải thay bằng GUID thật. Outgoing Chair bắt buộc xuống Member; kể cả tài khoản Chair cũ inactive vẫn được hạ xuống Member trong batch thay Chair đã xác nhận. Incoming Chair phải active; ngoại lệ này không cho phép đổi role khác của người inactive. Nếu incoming là Secretary cuối, thêm thay đổi một Member → Secretary trong cùng batch, hoặc phân công Secretary khác trước. Không dùng hai request tuần tự demote/promote. `changes` 1–100 phần tử, không trùng participantId. Confirmation dư/sai/thiếu đều invalid.

## 4. Response shapes

Context:

```json
{
  "meetingId": "GUID", "status": "Published", "revision": "3",
  "permissions": {
    "canViewParticipants": true, "canViewResponses": true,
    "canManageParticipants": true, "canRecoverSecretary": false, "canRespond": true
  }
}
```

Roster: `{items,total,page,pageSize,revision}`. Mỗi item có `id`, `kind` (Internal/Guest), `displayName`, `email`, `role` (null với Guest), `userId`, `isActive` (null với Guest), `invitedName`, `selfDeclaredDisplayName`, `selfDeclaredJobTitle`, `additionalPermissions`. Các trường không phù hợp là null. **Chỉ Secretary nhận additionalPermissions**; viewer khác nhận null. Không có Response/Attendance trong roster.

Editor-options: `{items:[{id,fullName,email}],total,page,pageSize,mode,roles,permissionCatalog,revision}`. Mode `Manage`/`RecoverSecretary`; recovery chỉ role Secretary, catalogue rỗng; Member current có thể xuất hiện để promote.

Own response: `{id,kind,response,respondedAt,canRespond}`. Summary: `{items:[{id,kind,displayName,email,response,respondedAt}],total,page,pageSize,totals:{pending,accepted,declined,total}}`. Counts gồm current internal và Guest, kể cả internal inactive còn current. Không có attendance/grants/job title trong summary.

Mutation thành công: `{code,message,revision}`; tạo mới thêm `createdIds`. PUT response: `{code,message,response:{id,kind,response,respondedAt,canRespond}}`; không trả roster. Sau self-removal/self-demotion, response thành công vẫn trả metadata tối thiểu, request kế tiếp kiểm quyền mới.

Lỗi có mã: `{code,message,errors}`; errors là dictionary field → string[] khi service xác định được trường lỗi, còn lại null. FE match code, không match message. Không phải mọi lỗi validation đều có field errors.

| HTTP | Code | Tình huống |
|---|---|---|
| 500 | MSG95 | Lỗi đọc roster/response/context/options |
| 400 | MSG97 | Dữ liệu mời/thành phần sai |
| 409 | MSG98 | Trạng thái khóa quản lý |
| 503 | MSG99 | Không lưu được thêm/recovery |
| 400 | MSG101 | Role/batch/confirmation sai |
| 503 | MSG102 | Không lưu được role/grants |
| 503 | MSG104 | Không lưu được gỡ/revoke |
| 400 | MSG106 | Phản hồi sai |
| 409 | MSG107 | Không còn được phản hồi |
| 503 | MSG108 | Không lưu được phản hồi |
| 409 | MSG109 | Vi phạm Chair/Secretary bắt buộc |
| 400 | MSG152 | Grant sai/không được cấp |
| 409 | MSG238 | Revision cũ |
| 400 | MSG239 | Query/If-Match sai |
| 415 / 413 | MSG34 / MSG87 | Media type / payload size |

## 5. Transaction và delivery

Writer khóa meeting trước, rồi đọc quyền/state/current assignments lại trong transaction. Selected users được khóa chia sẻ để không đổi active/email trong lúc ghi. Chair cũ được demote và flush trong cùng transaction trước promote, để không vi phạm unique index; mọi lỗi rollback role/grant/revision/audit/notification/dispatch.

Trước Publish không tạo dispatch. Publish ghi dispatch cho toàn bộ current active internal và current Guest; add Published/InProgress chỉ ghi người mới. Role/grants thông báo người bị ảnh hưởng; đổi Chair thông báo cả hai. Remove có thông báo không kèm link. HTTP success chỉ nói đã lưu, không bảo đảm email đã giao.

Dời lịch Published (UC-15/BR-76) giữ notification nội bộ hiện có và tạo dispatch `Rescheduled` cho từng Guest chưa revoke, cùng transaction với schedule/history. `EventId` là ID history Reschedule; key `roster:{historyId}:Rescheduled:{guestInvitationId}` duy nhất theo sự kiện và invitation. Gửi lại cùng lịch là no-op; lần dời lịch khác có event mới. Email chứa giờ mới và link xác minh, không lộ phòng/link vào họp; không đính ICS cho sự kiện dời lịch. UC-04 chưa sẵn sàng thì dispatch chờ dependency; lỗi gửi không hoàn tác lịch đã lưu.

SMTP disabled vẫn giữ dispatch. Worker khi enabled tạo EmailMessage idempotent, rồi SMTP processor hiện có gửi với lease/retry. Guest chưa có UC-04 → BlockedDependency, retry sau60s, không tiêu thụ attempt kỹ thuật. Technical retry theo RetryDelaySeconds nhân đôi, trần900s, MaxAttempts; quá giới hạn Failed. Removed/revoked/inactive được kiểm trước materialize; initial/role/grants bị suppress nếu không còn current hoặc meeting Ended. Không giữ DB lock trong SMTP. Email đã tạo/gửi không thể bảo đảm thu hồi; server phải kiểm quyền khi mở link.

SMTP là at-least-once: crash sau mail server nhận nhưng trước ghi Sent có thể gửi trùng. DB đảm bảo một EmailMessage/event key, không hứa exactly-once ở mailbox. Legacy reconcile theo từng recipient ở Published/InProgress; không gửi lại key đã có EmailMessage/dispatch.

## 6. Migration

`20261014114845_MeetingParticipantLifecycleAndGuestInvitations` thêm lifecycle/revision, Guest, grants và durable dispatch. Giữ response/attendance/history; backfill legacy delivery key để không gửi lại. Partial unique current(meeting,user), current Chair, current Guest email; grants XOR owner và unique owner/key.

Preflight **dừng** nếu có legacy Guest trong MeetingParticipant, Delegated/delegated_to hoặc nhiều Chair. Không xóa/chuyển dữ liệu ngầm. Cần người sở hữu dữ liệu quyết định mapping trước upgrade. Down bị chặn khi đã có Guest/grant/dispatch/removed history; dùng forward fix hoặc restore đã chuẩn bị. Không sửa migration đã lên main.

Tài liệu liên quan: [FE/manual](09-thanh-phan-tham-du-ban-giao.md), [UC-04](09-thanh-phan-tham-du-guest-uc04.md), [kết quả test](09-thanh-phan-tham-du-ket-qua-kiem-thu.md), [ADR-0019](../05-decisions/0019-vong-doi-thanh-phan-va-y-dinh-gui-thu.md).
