# SCRUM-19 · Bàn giao FE, Tester và worker

## Phạm vi backend

Backend cung cấp readiness, transitions, history và notification/email jobs theo
[API contract](05-quan-ly-cuoc-hop.md). Frontend tích hợp type/API wrapper và UI ở task riêng;
BA đối chiếu tài liệu sau FE integration theo workflow của nhóm.

POST/PUT chỉ cập nhật bảy field thông tin. Form frontend hiện tại vẫn gửi `assignedApproverId`,
nhưng backend bỏ qua field này; FE cần bỏ dropdown/request field cũ và chọn approver qua
`SubmitForApproval`. Organizer được sửa ở `Draft` hoặc `Rejected`; gửi duyệt lại trực tiếp từ
Rejected, không bắt buộc `ReturnToDraft`. Readiness chỉ kiểm tra thông tin cấp meeting;
Agenda và Meeting Session không bắt buộc lúc gửi duyệt.

## Tích hợp producer/worker SCRUM-15

Publish ghi status, history và notifications theo BR-54 trong cùng transaction. Sau SaveChanges,
service gọi `QueueForPublishedMeetingAsync(meetingId)` bằng cùng scoped DbContext/transaction.
Producer SCRUM-15 đọc meeting Published cùng participants, render và lưu nội dung job một lần.

- Notification chỉ chọn participant có user active và `Response != Delegated`; delegate cần
  participant row riêng hợp lệ. Email chọn participant có user active theo producer SCRUM-15,
  không lọc `Response` và không tự thêm `DelegatedToUserId`.
- `DeduplicationKey` là `meeting:{meetingId}:published:{userId}`.
- Jobs lưu `ToAddress`, `Subject`, `Body`, `CalendarAttachment`; worker gửi nội dung đã lưu.
  Bốn cột email không dùng `OriginHistoryId`, `RecipientUserId`, `MessageType`, `PayloadSnapshot`
  được bỏ bằng migration mới. Thay đổi dữ liệu sau commit không sửa nội dung job đã lưu.
- Khi bật SMTP, job mới có `Queued`, `SentAt = null`; Publish không xác nhận email đã được giao.
- Producer không tạo được jobs thì Publish rollback toàn bộ và trả `503 MSG47`.
- SMTP `Enabled=false` dừng worker và không tạo job mới; Publish vẫn thành công và giữ nguyên
  jobs cũ. Cấu hình theo [hướng dẫn email](07-gui-email-thu-moi.md).

## Nâng cấp database có email cũ

`ReliableEmailDelivery` đã vào main, thêm key bắt buộc và được giữ nguyên. Hai migration
`StageLegacyEmailJobsForReliableDelivery` và `RestoreLegacyEmailJobsWithDeliveryKeys` giữ email
trong bảng đệm rồi khôi phục sau upgrade; chỉ bổ sung `legacy-email:{id}` nếu key còn thiếu.
Key, nội dung, trạng thái và lease đã có được giữ nguyên; bảng đệm được xóa sau khi khôi phục.
Chạy upgrade đến migration mới nhất, không dừng ở migration Stage.

`AlignMeetingWorkflowEmailDelivery` đồng bộ metadata của model meeting/email; Up/Down không
thay đổi schema. Xem thêm [hướng dẫn cài đặt](07-gui-email-thu-moi-note-cai-dat-may-moi.md).

Migration mới `MeetingWorkflowReviewCorrections` bỏ bốn cột email trên, hai FK, composite unique
index và index phụ của recipient; không đụng `Notification.OriginHistoryId` hoặc bảo vệ append-only.
Migration giữ nguyên jobs, key, nội dung, delivery state và lease. Down khôi phục schema nullable,
không khôi phục giá trị metadata cũ đã bị bỏ. Nếu đã có history TransferOrganizer, Down bị chặn
để không mất bằng chứng Organizer cũ/mới hoặc để lại action mà model cũ không đọc được.
Cùng migration bổ sung ID Organizer cũ/mới cho history
`TransferOrganizer`; không sửa các migration đã phát hành.

## Checklist FE/Tester

- FE bổ sung type/API wrapper, lựa chọn approver khi submit, workflow controls và timeline.
- Kiểm tra Draft → WaitingApproval → Approved → Published; bật SMTP thì job mới là Queued,
  tắt SMTP thì không thêm job mới.
- Sửa `Rejected` giữ trạng thái, assignment và rejection history; Submit trực tiếp tạo một event Rejected → WaitingApproval, thay assignment và giữ rejection history cũ.
- Reject bắt buộc reason 1–1.000 ký tự sau trim; không cho Organizer chọn chính mình làm approver hoặc tự Approve/Reject.
- Organizer active/có `MeetingCreator` được rút WaitingApproval khi assignment thiếu, trỏ về
  Organizer, hoặc approver inactive/mất quyền. Approver khác Organizer còn hợp lệ thì không cho rút.
- Organizer/current approver đọc full history; Secretary được gán, active/chưa ủy quyền đọc rejection reason qua history cả trước Publish, nhưng snapshot chuyển Organizer vẫn ẩn. Participant khác chỉ đọc timeline đã redacted sau Publish.
- Publish thiếu phiên Planned hợp lệ trả `400 MSG90` với `errors.meetingSessions`; Approved/assignment/history/notification/email giữ nguyên, producer không được gọi. SMTP tắt vẫn kiểm tra BR-56.
- Kiểm tra rollback khi lưu lỗi và concurrent Publish chỉ tạo một history/batch.
- Writer participant SCRUM-20 và Meeting Session UC-26–30 khóa meeting cha trong cùng transaction trước khi sửa, để bảo vệ snapshot người nhận và BR-56 khi Publish đồng thời.
- Xử lý HTTP/message codes theo API contract; trạng thái gửi email theo SCRUM-15.

## Khôi phục meeting Approved và phạm vi PR

Theo quyết định người dùng ngày 01/10/2026, quản trị active/có AdminUsers được TransferOrganizer
khi Organizer cũ inactive và meeting còn Approved. Người nhận active/có MeetingCreator, khác
người cũ/current approver. History lưu Organizer cũ/mới và actor/time; trạng thái, approval và
notification/email giữ nguyên. FE/Tester nhận body organizerId, MSG89/MSG66/MSG67 cùng lỗi shape,
quyền và persistence theo [API contract](05-quan-ly-cuoc-hop.md). Organizer mới dùng Publish bình thường.
Response transfer chỉ có `code`, `message`, `meetingId`, `status`, `organizerId` và history của
thao tác vừa tạo; không trả nội dung meeting cho người có quyền recovery. FE tích hợp type/API
wrapper và UI ở task riêng.

PR #50 tách RoleService/fixture đã đóng mà chưa merge. Vì thế branch này vẫn chứa fix RESTRICT của
RoleService và helper/fixture PostgreSQL; các fix này chưa vào main. PR #41 hiện dùng dependency
branch làm base nên diff review không thể hiện toàn bộ thay đổi sẽ vào main. Mô tả PR phải ghi rõ
phần prerequisite này; không gọi việc tách phạm vi là đã hoàn tất hoặc tạo branch mới để che diff.

## Đồng bộ main ngày 02/10/2026

Use Case Meeting Management dùng UC-15–21 theo bộ tài liệu BA đã vào main.
Các mã workflow cũ bị trùng chuyển sang MSG78–MSG89 theo
[bảng đổi mã](00-danh-muc-ma-thong-bao.md#đồng-bộ-với-main-ngày-02102026).
Giữ MSG48–MSG59/MSG77 cho Meeting Session và MSG65 cho phòng chưa mở (ADR-0010).
FE/Tester dùng API contract sau đồng bộ, gồm MSG89 cho TransferOrganizer thành công.

## Sửa alignment theo review ngày 02/10/2026

Backend hỗ trợ direct resubmit Rejected, Secretary đọc rejection reason, guard Planned Meeting Session
trước Publish (BR-56) và bỏ Agenda-required rule chưa có trong UC-19/BR-33. `ReturnToDraft` là tùy chọn
cho Rejected; recovery WaitingApproval, TransferOrganizer, cấm self-approval và giới hạn reason
1–1.000 ký tự được ghi thành PRE/AF/EX tại UC-19–21 để BA Final Review đối chiếu.

Migration mới `MeetingSessionsForPublication` chỉ thêm bảng `meeting_sessions`: `id`, `meeting_id`,
`title` (tối đa 300), `planned_starts_at`, `planned_ends_at`, `status` (chuỗi), `created_at`, `updated_at`;
FK tới meetings và index `(meeting_id, status)`. Không sửa/xóa cột hoặc dữ liệu bảng cũ, không tự tạo
phiên từ lịch cấp meeting. Down xóa bảng mới cùng dữ liệu phiên; môi trường có dữ liệu phiên dùng
migration sửa tiếp, không downgrade để tránh mất lịch. Nâng cấp code và schema cùng nhau.

Model/guard đã có; API lập lịch UC-26–30 thuộc phần quản lý phiên. Meeting chưa có phiên hợp lệ sẽ
bị chặn Publish cho tới khi phần đó tạo lịch; không seed lịch giả để bỏ qua BR-56.
MSG90 dành cho guard, mã tiếp theo MSG91. Catalogue/API/Mermaid được cập nhật cùng patch.

PR41 đã merge vào `codex/role-postgres-isolation` ở commit `e27e003`, không phải main. Main mới nhất
đã fetch là `779d14e` và đã là ancestor của checkout `5238f85`, nhưng workflow PR41 chưa có trên main.
Patch alignment được kiểm chứng ở working tree này; CI của head cũ không xác nhận patch chưa push.
Người dùng đưa commit sửa cùng prerequisite vào integration/main rồi chạy CI/reviewer. FE integration,
BA Final Review và Tester sign-off tiếp tục theo pipeline; bàn giao backend không đánh dấu Done.
