# SCRUM-19 · Bàn giao FE, Tester và worker

## Phạm vi backend

SCRUM-19 cung cấp readiness, transitions, history, authorization, immutable audit evidence và
notification/email jobs nguyên tử theo [API contract](05-quan-ly-cuoc-hop.md). FE có type/API wrapper;
UI chọn approver lúc submit, review/publish và timeline thật là phần tích hợp của FE. Form tạo/sửa
chỉ bỏ dropdown approver vì POST/PUT không nhận assignment; không thêm widget workflow vào form.

`Backend Ready` chỉ xác nhận API và unit/integration tests backend. Story chưa Done cho tới khi FE,
worker/realtime cần thiết, BA Final Review và Tester hoàn tất. Không coi Publish thành công là email
đã gửi; delivery phải xem `EmailStatus`/`SentAt` sau khi SCRUM-15 tích hợp.

## Quyết định sau review PR #41

- Quyết định mới ngày 01/10/2026 thay rule không cho rút trước đó: Organizer active/có
  `MeetingCreator` được rút `WaitingApproval` về Draft khi approver inactive/mất `MeetingApprover`.
  Dùng action `ReturnToDraft`, ghi một history, xóa
  assignment, không tạo notification/invitation; sau đó chọn người duyệt mới qua submit bình thường.
  Nếu approver vẫn hợp lệ thì không cho rút; không gán lại trực tiếp trong WaitingApproval.
- Cấm tự duyệt mọi mức độ mật. Submit cho Organizer trả `400 MSG52`; Approve/Reject trên assignment
  cũ trỏ về Organizer trả `403 MSG55`. Có hai capability hoặc quan hệ Chair không bypass được.
- Readiness vẫn cần agenda không rỗng và title/giới hạn field theo schema. Thứ tự liên tục, duration
  dương, presenter bắt buộc và chỉ cho Pending chưa được chốt bởi SCRUM-25, nên không áp ở đây.
- GET history không khóa ghi. Development seed có Draft hoàn chỉnh để test trước khi API agenda/
  participant được tích hợp; seeder không reset demo đã thao tác.

## Contract SCRUM-19 → producer/worker SCRUM-15

PR #49 đã vào main (`1d0b954`). Code tích hợp hiện nằm trong checkout của branch SCRUM-19 gốc,
cùng phần đồng bộ main đang chờ người dùng commit; chưa push hoặc cập nhật PR #41.
Contract producer dưới đây đã được triển khai và kiểm thử local. Ghi chú Jira dùng để bàn giao cho
owner SCRUM-15 review thay đổi contract, không phải xác nhận owner đã đồng ý.
Chỉ có một producer `IMeetingInvitationQueue`; SCRUM-19 không tạo Subject/Body hoặc `.ics` riêng.

Publish khóa meeting, kiểm tra quyền/state, chốt recipient theo BR-54, tạo history và notifications.
Sau khi SaveChanges trong transaction còn mở, service gọi
`QueueForPublishedMeetingAsync(MeetingPublicationInvitation)`. DTO truyền giá trị bất biến của meeting,
Publish history ID, thời điểm Publish và danh sách UserId/FullName/Email đã snapshot. Producer SCRUM-15
sử dụng cùng scoped DbContext/transaction để render và persist jobs; không query lại participant.
Chỉ commit khi mọi bước thành công. Persistence/timeout hoặc cấu hình producer không tạo được job
trả `503 MSG47` và rollback status/history/notifications/jobs; cancellation của request vẫn propagate.

| Trường persisted | Ý nghĩa/ownership |
|---|---|
| `DeduplicationKey` | Producer SCRUM-15 gán `meeting:{meetingId}:published:{historyId}:{userId}`; unique index chống lặp cùng publication/recipient |
| `MeetingId`, `OriginHistoryId`, `RecipientUserId`, `MessageType = MeetingInvitation` | Truy vết batch Publish và recipient theo snapshot |
| `ToAddress` | Địa chỉ tại transaction Publish; worker không thay bằng email user hiện tại |
| `Subject`, `Body`, `CalendarAttachment` | Nội dung và `.ics` đã render bởi SCRUM-15 từ snapshot; worker gửi đúng các giá trị persisted |
| `Status = Queued`, `SentAt = null` | Chưa gửi; SCRUM-15 xử lý delivery/retry/failure sau commit |
| `PayloadSnapshot` | Chỉ giữ nullable cho dữ liệu cũ; job mới không ghi trường này, worker không đọc JSON để render `.ics` |

BR-54 chỉ chọn participant có user active và `Response != Delegated`; delegate chỉ nhận nếu có
participant row riêng hợp lệ. Thay đổi participant/user/meeting sau commit không đổi batch invitation.
SMTP `Enabled=false` chỉ dừng worker; jobs vẫn được tạo ở trạng thái Queued. URL frontend và sender
phải hợp lệ để render invitation kể cả khi worker tắt. Development dùng sender không bí mật
`no-reply@trustcabinet.local`; production phải cấu hình URL/sender thực. Không gọi SMTP trong Publish.

Migration `AlignMeetingWorkflowEmailDelivery` chỉ đồng bộ ModelSnapshot hợp nhất của hai story;
Up/Down không lặp DDL. Migration workflow SCRUM-19 đang có trên branch giữ nguyên các cột truy vết
email; migration `ReliableEmailDelivery` đã vào main giữ nguyên hoàn toàn.

`ReliableEmailDelivery` thêm cột key bắt buộc nên thất bại nếu database còn email cũ. Hai migration
mới xử lý tương thích mà không sửa migration đã phát hành:

- `StageLegacyEmailJobsForReliableDelivery` được sắp ngay trước migration đó: khóa bảng và giữ
  nguyên các dòng email trong bảng đệm để schema upgrade chạy được.
- `RestoreLegacyEmailJobsWithDeliveryKeys` khôi phục các dòng sau upgrade, bổ sung key
  `legacy-email:{id}` nếu chưa có; giữ nguyên key, nội dung, trạng thái và lease nếu đã tồn tại,
  rồi xóa bảng đệm. Các bước thuộc transaction migration PostgreSQL.

Test PostgreSQL kiểm tra database có email trước upgrade, database đã áp dụng migration của main,
và rollback schema. Chạy upgrade đến migration mới nhất; không dừng triển khai ở migration đệm.

## Checklist handoff cho FE/Tester

- Happy path: Draft → WaitingApproval → Approved → Published; email vẫn Queued tại response.
- Reject thiếu/null/whitespace reason: `400 MSG53`; reason hợp lệ trim và giữ bất biến trong history.
- Rejected phải ReturnToDraft trước submit mới; history riêng, clear assignment, không invitation.
- Current approver phải active/có capability; former approver không có quyền nhờ history cũ.
- Organizer/current approver xem full reason; participant chỉ xem timeline redacted sau Published.
- Self-submit/legacy self-review bị từ chối. WaitingApproval ReturnToDraft chỉ thành công khi
  approver không còn hợp lệ; sai quyền hoặc approver hợp lệ bị từ chối, không ghi side effects.
- Approver list database/timeout ở capability gate hoặc repository đều trả `503 MSG61`;
  request cancellation vẫn được propagate, không đổi thành 503.
- History đọc khi meeting đang bị writer khóa; concurrent Publish chỉ commit một history/batch.
- Participant writer SCRUM-20 và agenda writer SCRUM-25 dùng cùng meeting-row lock protocol.
- FE xử lý mã HTTP/MSG, conflict bằng reload, lỗi tạm thời bằng retry; không báo email đã gửi.

## Tách thay đổi ngoài ticket

Fix `RoleService` cho PostgreSQL RESTRICT (`23001`) và refactor các fixture Access/Authentication/Notifications
được chuẩn bị riêng trên branch `codex/role-postgres-test-fixes` từ `origin/main`, chưa commit.
Đây là dependency sửa lỗi full suite, không phải hành vi meeting. `PostgreSqlTestDatabase` là helper
chung cần cho fixture meeting chạy PostgreSQL thật với database riêng.

Vì PR #41 đã chứa các fix đó từ commit trước, phải đưa dependency lên main trước rồi cập nhật base
của SCRUM-19 để chúng tự hết xuất hiện trong diff PR. Không revert chúng ngay khỏi feature khi chưa
có dependency: việc đó sẽ đưa lại lỗi Role/cleanup và có thể làm fixture meeting mất helper. Việc
commit, tạo PR dependency và cập nhật branch do người dùng thực hiện; agent không commit/push/merge.

Các worktree cũ chỉ giữ bản thử nghiệm để đối chiếu. Người dùng tiếp tục trên branch SCRUM-19 gốc;
không cần chuyển sang chúng để lấy fix. Patch producer cũ sửa migration PR #49 trước khi merge
đã lỗi thời và không được áp lên main hiện tại. Việc tách Role/test ra khỏi diff PR vẫn phụ thuộc
vào việc đưa dependency đó lên main; chạy test xanh không đồng nghĩa đã hoàn tất tách phạm vi.

## Kiểm chứng checkout gốc ngày 01/10/2026

- `dotnet build TrustCabinet.sln --no-restore -warnaserror`: pass, 0 warning/error.
- `dotnet test TrustCabinet.sln --no-build`: 718/718 pass, 0 skipped, dùng PostgreSQL thật với
  database riêng; bao gồm HTTP → Publish → producer → worker giả lập gửi, không gửi SMTP thật.
- Full suite gồm các test PostgreSQL meeting/migration: append-only, concurrent Publish, snapshot
  không đổi sau commit, rollback khi producer lỗi và giữ email cũ khi upgrade. Bổ sung HTTP thật
  kiểm tra rút khi approver inactive/mất quyền, một history riêng và submit lại cho approver mới;
  kiểm tra API/unit cho approver hợp lệ, sai quyền, sai trạng thái và rollback khi lưu thất bại.
- `dotnet ef migrations has-pending-model-changes ... --no-build`: không có thay đổi model còn thiếu.
- Frontend `npm run lint` và `npm run build`: pass; đây là kiểm tra CI, không thêm task UI backend.

Kết quả trên là kiểm thử local, chưa phải CI của PR sau khi push. Không chạy lại Postman CLI/Newman
trong lượt này. Tài liệu, code và phần đồng bộ main chưa được agent commit/push; không đổi trạng
thái Jira sang Done và không merge PR GitHub.
