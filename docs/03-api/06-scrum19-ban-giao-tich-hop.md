# SCRUM-19 · Bàn giao FE, Tester và worker

> **Một cuộc họp là một buổi họp ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).** Từ migration
> `MeetingOwnsScheduleDropSessions`, lịch nằm trên Meeting (`plannedStartTime`, `plannedEndTime`, `onlineLink`) và code
> không còn `MeetingSession`. Các đoạn lịch sử bên dưới nhắc Meeting Session là mô tả thời điểm viết.
> Số UC đã đổi theo baseline 70 UC ([bảng đổi số](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới));
> các mã `BR-54` → `BR-61` nhắc trong file là số BR cũ đã ngừng dùng ([danh mục BR](../01-requirements/06-business-rules.md)).

## Phạm vi backend

Backend cung cấp readiness, transitions, history và notification/email jobs theo
[API contract](05-quan-ly-cuoc-hop.md). Frontend tích hợp type/API wrapper và UI ở task riêng;
BA đối chiếu tài liệu sau FE integration theo workflow của nhóm.

POST/PUT cập nhật thông tin cấp Meeting, gồm lịch dự kiến và link trực tuyến. Form frontend hiện tại vẫn gửi `assignedApproverId`,
nhưng backend bỏ qua field này; FE cần bỏ dropdown/request field cũ và chọn approver qua
`SubmitForApproval`. Organizer được sửa ở `Draft` hoặc `Rejected`; gửi duyệt lại trực tiếp từ
Rejected, không bắt buộc `ReturnToDraft`. Readiness chỉ kiểm tra thông tin cấp meeting;
Agenda không bắt buộc lúc gửi duyệt; lịch dự kiến thì bắt buộc.

## Tích hợp producer/worker SCRUM-15

Publish ghi status, history và notifications theo BR-54 trong cùng transaction. Sau SaveChanges,
service commit publication trước rồi gọi `QueueForPublishedMeetingAsync(meetingId)` bằng cùng scoped DbContext.
Producer SCRUM-15 đọc meeting Published cùng participants, render và lưu nội dung job một lần.

- Notification chỉ chọn participant có user active và `Response != Delegated`; delegate cần
  participant row riêng hợp lệ. Email chọn participant có user active theo producer SCRUM-15,
  không lọc `Response` và không tự thêm `DelegatedToUserId`.
- `DeduplicationKey` là `meeting:{meetingId}:published:{userId}`.
- Jobs Publish lưu `ToAddress`, `Subject`, `Body` (có giờ họp) và `CalendarAttachment` (`.ics` theo lịch dự kiến).
  Bốn cột email không dùng `OriginHistoryId`, `RecipientUserId`, `MessageType`, `PayloadSnapshot`
  được bỏ bằng migration mới. Thay đổi dữ liệu sau commit không sửa nội dung job đã lưu.
- Khi bật SMTP, job mới có `Queued`, `SentAt = null`; Publish không xác nhận email đã được giao.
- Producer không tạo được jobs thì Publish vẫn trả `200 MSG45`; meeting/history giữ Published,
  backend ghi log và email worker tự đối soát để thử lại. Retry dùng deduplication key nên không tạo
  thêm publication history hoặc email trùng.
- Đối soát và xử lý email đã Queued dùng hai scope/DbContext độc lập. Lỗi đối soát chỉ được log;
  processor vẫn chạy trong lượt đó để một meeting lỗi không chặn email của meeting khác.
- Chỉ lỗi lưu status/history/notification của publication mới rollback và trả `503 MSG47`.
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
- Publish yêu cầu đúng một Chair active và ít nhất một Secretary active; invitation response không
  ảnh hưởng readiness. Roster không đạt trả `409 MSG137`, giữ Approved và không ghi history,
  notification hoặc email job. `MSG46` chỉ còn cho meeting không còn Approved hoặc Organizer mất
  `MeetingCreator`.
- Hiện chưa có endpoint gán participant (SCRUM-20 chưa hoàn tất), nên meeting tạo mới chưa thể chạy
  positive flow Publish; Tester dùng dữ liệu seed cho đến khi SCRUM-20 được tích hợp.
- Organizer còn đọc được meeting Approved nhưng mất `MeetingCreator` nhận `403 MSG46`; caller không
  được đọc tài nguyên vẫn nhận `404`.
- BA còn đổi câu `MSG39` của notification pagination thành `Trang thông báo được yêu cầu không khả dụng.`;
  endpoint notification nằm ngoài phạm vi PR meeting này và cần được đồng bộ ở task notification.
- Kiểm tra rollback khi lưu lỗi và concurrent Publish chỉ tạo một history/batch.
- Writer participant SCRUM-20 khóa meeting cha trong cùng transaction trước khi sửa để bảo vệ
  snapshot người nhận khi Publish đồng thời.
- Xử lý HTTP/message codes theo API contract; trạng thái gửi email theo SCRUM-15.

## Khôi phục meeting Approved và phạm vi PR

Theo quyết định người dùng ngày 01/10/2026, quản trị active/có AdminUsers được TransferOrganizer
khi Organizer cũ inactive và meeting còn Approved. Người nhận active/có MeetingCreator, khác
người cũ/current approver. History lưu Organizer cũ/mới và actor/time; trạng thái, approval và
notification/email giữ nguyên. FE/Tester nhận body organizerId, MSG89/MSG134/MSG135 cùng lỗi shape,
quyền và persistence theo [API contract](05-quan-ly-cuoc-hop.md). Organizer mới dùng Publish bình thường.
Response transfer chỉ có `code`, `message`, `meetingId`, `status`, `organizerId` và history của
thao tác vừa tạo; không trả nội dung meeting cho người có quyền recovery. FE tích hợp type/API
wrapper và UI ở task riêng.

PR #50 tách RoleService/fixture đã đóng mà chưa merge. Vì thế branch này vẫn chứa fix RESTRICT của
RoleService và helper/fixture PostgreSQL; các fix này chưa vào main. PR #41 hiện dùng dependency
branch làm base nên diff review không thể hiện toàn bộ thay đổi sẽ vào main. Mô tả PR phải ghi rõ
phần prerequisite này; không gọi việc tách phạm vi là đã hoàn tất hoặc tạo branch mới để che diff.

## Đồng bộ main ngày 02/10/2026

Use Case Meeting Management dùng UC-14–21 theo bộ tài liệu BA đã vào main.
Các mã workflow cũ bị trùng chuyển sang MSG78–MSG89 theo
[bảng đổi mã](00-danh-muc-ma-thong-bao.md).
MSG48–MSG59/MSG77 từng dành cho Meeting Session (nay ngừng dùng). Phòng chưa mở dùng MSG117 theo catalogue (ADR-0010 ghi MSG65 là nhầm).
FE/Tester dùng API contract sau đồng bộ, gồm MSG89 cho TransferOrganizer thành công.

## Sửa alignment theo review ngày 02/10/2026

Backend hỗ trợ direct resubmit Rejected, Secretary đọc rejection reason và bỏ Agenda/Meeting Session
khỏi điều kiện gửi duyệt. Meeting Session cũng không bắt buộc khi Publish theo BR-56. `ReturnToDraft` là tùy chọn
cho Rejected; recovery WaitingApproval, TransferOrganizer, cấm self-approval và giới hạn reason
1–1.000 ký tự được ghi thành PRE/AF/EX tại UC-18–21 để BA Final Review đối chiếu.

Migration mới `MeetingSessionsForPublication` chỉ thêm bảng `meeting_sessions`: `id`, `meeting_id`,
`title` (tối đa 300), `planned_starts_at`, `planned_ends_at`, `status` (chuỗi), `created_at`, `updated_at`;
FK tới meetings và index `(meeting_id, status)`. Không sửa/xóa cột hoặc dữ liệu bảng cũ, không tự tạo
phiên từ lịch cấp meeting. Down xóa bảng mới cùng dữ liệu phiên; môi trường có dữ liệu phiên dùng
migration sửa tiếp, không downgrade để tránh mất lịch. Nâng cấp code và schema cùng nhau.

Model Meeting Session đã có; API lập lịch Meeting Session cũ (đã bỏ) thuộc phần quản lý phiên. Meeting chưa có phiên
vẫn Publish được; `MSG90` giữ trạng thái ngừng dùng và không tái cấp. Các mã workflow bổ sung dùng
`MSG130`–`MSG136` để không trùng dải Agenda, F08/GOF09 và `MSG129` của Reset Password trên main.

PR41 đã merge vào `codex/role-postgres-isolation` ở commit `e27e003`, không phải main. Main mới nhất
đã fetch là `779d14e` và đã là ancestor của checkout `5238f85`, nhưng workflow PR41 chưa có trên main.
Patch alignment được kiểm chứng ở working tree này; CI của head cũ không xác nhận patch chưa push.
Người dùng đưa commit sửa cùng prerequisite vào integration/main rồi chạy CI/reviewer. FE integration,
BA Final Review và Tester sign-off tiếp tục theo pipeline; bàn giao backend không đánh dấu Done.
