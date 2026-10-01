# SCRUM-19 · Bàn giao FE, Tester và worker

## Phạm vi backend

SCRUM-19 cung cấp readiness, transitions, history, authorization, immutable audit evidence và
notification/email jobs nguyên tử theo [API contract](05-quan-ly-cuoc-hop.md). PR #41 không có
thay đổi code frontend so với nhánh đích. Type/API wrapper và UI chọn approver lúc submit,
review/publish, readiness và timeline được bàn giao để FE triển khai riêng.

Form frontend hiện tại vẫn có dropdown và gửi `assignedApproverId` khi tạo/sửa. POST/PUT bỏ qua
field này và chỉ cập nhật bảy field thông tin; người duyệt chỉ được gán bởi action
`SubmitForApproval`. FE cần bỏ dropdown/field request cũ khỏi form và đưa lựa chọn approver vào
flow gửi duyệt. Cho tới khi FE tích hợp, lựa chọn ở dropdown cũ không được backend áp dụng.

`Backend Ready` chỉ xác nhận API và unit/integration tests backend. Story chưa Done cho tới khi FE,
worker/realtime cần thiết, BA Final Review và Tester hoàn tất. Không coi Publish thành công là email
đã gửi; delivery phải xem `EmailStatus`/`SentAt` sau khi SCRUM-15 tích hợp.

## Quyết định sau review PR #41

- Quyết định mới ngày 01/10/2026 thay rule không cho rút trước đó: Organizer active/có
  `MeetingCreator` được rút `WaitingApproval` về Draft khi approver inactive/mất `MeetingApprover`,
  hoặc assignment cũ bị thiếu/trỏ về chính Organizer.
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

PR #49 đã vào main (`1d0b954`). Code tích hợp nằm trên branch SCRUM-19 và trong PR #41.
Contract producer dưới đây đã được triển khai và kiểm thử. Ghi chú Jira dùng để bàn giao cho
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

- FE cập nhật type/API wrapper theo contract, bỏ dropdown và `assignedApproverId` khỏi request
  POST/PUT của form tạo/sửa; bổ sung lựa chọn approver và gọi `SubmitForApproval` trong flow riêng.
  Các thay đổi frontend này không được cung cấp bởi PR #41.
- Happy path: Draft → WaitingApproval → Approved → Published; email vẫn Queued tại response.
- Reject thiếu/null/whitespace reason: `400 MSG53`; reason hợp lệ trim và giữ bất biến trong history.
- Rejected phải ReturnToDraft trước submit mới; history riêng, clear assignment, không invitation.
- Current approver phải active/có capability; former approver không có quyền nhờ history cũ.
- Organizer/current approver xem full reason; participant chỉ xem timeline redacted sau Published.
- Self-submit/legacy self-review bị từ chối. WaitingApproval ReturnToDraft chỉ thành công khi
  assignment bị thiếu, trỏ về Organizer hoặc approver inactive/mất quyền; sai quyền hoặc approver
  là người khác Organizer, active và có quyền bị từ chối, không ghi side effects.
- Approver list database/timeout ở capability gate hoặc repository đều trả `503 MSG61`;
  request cancellation vẫn được propagate, không đổi thành 503.
- History đọc khi meeting đang bị writer khóa; concurrent Publish chỉ commit một history/batch.
- Participant writer SCRUM-20 và agenda writer SCRUM-25 dùng cùng meeting-row lock protocol.
- FE xử lý mã HTTP/MSG, conflict bằng reload, lỗi tạm thời bằng retry; không báo email đã gửi.

## Tách thay đổi ngoài ticket

Fix `RoleService` cho PostgreSQL RESTRICT (`23001`) và hạ tầng cô lập PostgreSQL integration tests
được tách sang [draft PR #50](https://github.com/callmehai/TrustCabinet/pull/50), branch
`codex/role-postgres-isolation` từ main. Dependency chứa helper
`PostgreSqlTestDatabase`, chuyển các fixture Access/Authentication/Notifications/Email sang database
riêng và sửa cleanup pool của fixture meeting; không chứa workflow SCRUM-19 hoặc thay đổi producer.

PR #41 dùng branch dependency làm base để diff review chỉ còn phần SCRUM-19. Hai branch được
ghép bằng merge thông thường, không viết lại history của feature. Review và merge dependency vào
main trước, đồng bộ main mới vào feature nếu dependency được squash/rebase merge, rồi chuyển base
PR #41 về main; các fix độc lập không trở lại diff SCRUM-19.

Ba thay đổi frontend (type, API wrapper và form) đã được hoàn nguyên về nhánh đích để PR #41 chỉ
bàn giao contract cho FE. Readiness giữ validation tối thiểu đã ghi Jira; các rule nghiệp vụ agenda
chưa chốt thuộc SCRUM-25. Producer email nhận snapshot
trong transaction Publish và worker SCRUM-15 giữ nguyên; phần tích hợp này tiếp tục nằm trong #41.
Các worktree thử nghiệm cũ được giữ nguyên, không dùng làm nguồn dependency đã tách.

## Kiểm chứng checkout gốc ngày 01/10/2026

Kết quả 718/724 tests trong mục này và mục review bổ sung dưới đây được ghi nhận trước khi hoàn
nguyên frontend. Kiểm chứng sau khi hoàn nguyên được ghi riêng ở cuối tài liệu.

- `dotnet build TrustCabinet.sln --no-restore -warnaserror`: pass, 0 warning/error.
- `dotnet test TrustCabinet.sln --no-build`: 718/718 pass, 0 skipped, dùng PostgreSQL thật với
  database riêng; bao gồm HTTP → Publish → producer → worker giả lập gửi, không gửi SMTP thật.
- Full suite gồm các test PostgreSQL meeting/migration: append-only, concurrent Publish, snapshot
  không đổi sau commit, rollback khi producer lỗi và giữ email cũ khi upgrade. Bổ sung HTTP thật
  kiểm tra rút khi approver inactive/mất quyền, một history riêng và submit lại cho approver mới;
  kiểm tra API/unit cho approver hợp lệ, sai quyền, sai trạng thái và rollback khi lưu thất bại.
- `dotnet ef migrations has-pending-model-changes ... --no-build`: không có thay đổi model còn thiếu.
- Frontend `npm run lint` và `npm run build`: pass; đây là kiểm tra CI, không thêm task UI backend.

Kết quả trên là kiểm thử local. CI kiểm chứng từng head sau khi push; không chạy lại Postman
CLI/Newman hoặc gửi SMTP thật trong lượt tách phạm vi. Không đổi trạng thái Jira sang Done;
dependency và PR #41 vẫn cần review trước khi merge vào main.

## Review bổ sung ngày 01/10/2026

Khắc phục trường hợp dữ liệu cũ `WaitingApproval` không có Assigned Approver hoặc gán chính
Organizer: Approve/Reject không hợp lệ, nhưng ReturnToDraft trước đó cũng trả conflict, làm meeting
bị kẹt. ReturnToDraft hiện cho Organizer active/có `MeetingCreator` rút hai trường hợp này dưới
meeting-row lock. Người duyệt khác Organizer còn active/có quyền vẫn giữ cycle hiện tại và không
cho rút. Không đổi schema, không sửa history cũ hoặc thêm thao tác UI.

Sáu regression cases bổ sung ở unit, HTTP và PostgreSQL xác nhận clear assignment, giữ history cũ,
thêm đúng một history bất biến, không tạo notification/email khi rút và gửi duyệt lại cho người khác.
Bốn case unit/HTTP đã thất bại trước sửa với StatusConflict/409, rồi pass sau sửa.
Ghi chú triển khai và trạng thái commit/dependency đã lưu ở
[SCRUM-19 comment 10115](https://sep490g116.atlassian.net/browse/SCRUM-19?focusedCommentId=10115).

- Release build `-warnaserror`: 0 warning/error.
- Full solution: 724/724 test pass, 0 failed, 0 skipped; dùng PostgreSQL thật với database test riêng.
- Frontend lint/build pass; ModelSnapshot không có pending model changes; `git diff --check` sạch.
- Các thay đổi mới cần commit/push để CI kiểm tra head mới. PR #41 hiện còn đặt trên dependency
  PR #50; review/merge #50 vào main trước rồi đồng bộ base #41 về main, tiếp tục review trước merge.

Không coi kết quả local là CI của head mới hoặc đánh dấu SCRUM-19 Done. FE workflow integration,
BA Final Review và Tester vẫn theo pipeline đã ghi.

## Hoàn nguyên frontend theo phạm vi backend ngày 01/10/2026

Khôi phục `frontend/src/api/meetings.ts`, `frontend/src/types/meeting.ts` và
`frontend/src/features/meetings/MeetingForm.tsx` về đúng phiên bản ở nhánh đích PR #41.
Đây là hoàn nguyên thay đổi của PR, không xóa file khỏi dự án. API và code backend không đổi;
phần tích hợp frontend được bàn giao theo checklist ở trên.

- So sánh toàn bộ `frontend/` với nhánh đích: không còn diff trong working tree.
- Frontend `npm run lint` và `npm run build`: pass.
- `MessageCodeCatalogueTests`: 11/11 pass, 0 failed, 0 skipped.
- Chưa chạy lại toàn bộ 724 tests hoặc kiểm thử browser trong lượt hoàn nguyên này.
- Cần commit/push phần hoàn nguyên để GitHub loại ba file frontend khỏi diff PR #41.
