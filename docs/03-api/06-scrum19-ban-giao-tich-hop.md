# SCRUM-19 · Bàn giao FE, Tester và worker

## Phạm vi backend

Backend cung cấp readiness, transitions, history và notification/email jobs theo
[API contract](05-quan-ly-cuoc-hop.md). Frontend tích hợp type/API wrapper và UI ở task riêng;
BA đối chiếu tài liệu sau FE integration theo workflow của nhóm.

POST/PUT chỉ cập nhật bảy field thông tin. Form frontend hiện tại vẫn gửi `assignedApproverId`,
nhưng backend bỏ qua field này; FE cần bỏ dropdown/request field cũ và chọn approver qua
`SubmitForApproval`. Organizer được sửa ở `Draft` hoặc `Rejected`; gửi duyệt lại cần
`ReturnToDraft` trước.

## Tích hợp producer/worker SCRUM-15

Publish chốt recipient theo BR-54, ghi status, history và notifications trong cùng transaction.
Sau SaveChanges, service gọi `QueueForPublishedMeetingAsync(MeetingPublicationInvitation)` bằng
snapshot gồm thông tin meeting, Publish history ID, thời điểm công bố và UserId/FullName/Email.
Producer dùng cùng scoped DbContext/transaction để render và lưu jobs; không query lại participant.

- Recipient là participant có user active và `Response != Delegated`; delegate cần participant row
  riêng hợp lệ. Thay đổi meeting/user/participant sau commit không đổi batch đã tạo.
- `DeduplicationKey` là `meeting:{meetingId}:published:{historyId}:{userId}`.
- Jobs lưu `ToAddress`, `Subject`, `Body`, `CalendarAttachment` và các ID truy vết. Worker gửi
  nội dung đã lưu; `PayloadSnapshot` chỉ giữ cho dữ liệu cũ, job mới không ghi trường này.
- Publish thành công xác nhận jobs `Queued`, `SentAt = null`; không xác nhận email đã được giao.
- Producer không tạo được jobs thì Publish rollback toàn bộ và trả `503 MSG47`.
- SMTP `Enabled=false` dừng worker, nhưng Publish vẫn tạo jobs. URL frontend/sender phải hợp lệ
  để render invitation; cấu hình theo [hướng dẫn email](07-gui-email-thu-moi.md).

## Nâng cấp database có email cũ

`ReliableEmailDelivery` đã vào main, thêm key bắt buộc và được giữ nguyên. Hai migration
`StageLegacyEmailJobsForReliableDelivery` và `RestoreLegacyEmailJobsWithDeliveryKeys` giữ email
trong bảng đệm rồi khôi phục sau upgrade; chỉ bổ sung `legacy-email:{id}` nếu key còn thiếu.
Key, nội dung, trạng thái và lease đã có được giữ nguyên; bảng đệm được xóa sau khi khôi phục.
Chạy upgrade đến migration mới nhất, không dừng ở migration Stage.

`AlignMeetingWorkflowEmailDelivery` đồng bộ metadata của model meeting/email; Up/Down không
thay đổi schema. Xem thêm [hướng dẫn cài đặt](07-gui-email-thu-moi-note-cai-dat-may-moi.md).

## Checklist FE/Tester

- FE bổ sung type/API wrapper, lựa chọn approver khi submit, workflow controls và timeline.
- Kiểm tra Draft → WaitingApproval → Approved → Published; email vẫn Queued tại response.
- Sửa `Rejected` giữ trạng thái, assignment và rejection history; ReturnToDraft trước khi submit lại.
- Reject bắt buộc reason 1–1.000 ký tự sau trim; cấm tự submit/duyệt.
- Organizer active/có `MeetingCreator` được rút WaitingApproval khi assignment thiếu, trỏ về
  Organizer, hoặc approver inactive/mất quyền. Approver khác Organizer còn hợp lệ thì không cho rút.
- Organizer/current approver đọc full history; participant chỉ đọc timeline được ẩn rejection reason.
- Kiểm tra rollback khi lưu lỗi và concurrent Publish chỉ tạo một history/batch.
- Writer participant SCRUM-20 và agenda SCRUM-25 dùng cùng meeting-row lock protocol.
- Xử lý HTTP/message codes theo API contract; trạng thái gửi email theo SCRUM-15.
