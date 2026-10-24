# Backend chương trình nghị sự — SCRUM-98

Contract thay thế SCRUM-25 ngày 24/10/2026, theo SCRUM-98 và comment 18/10/2026, Report 3 FE-05, UC-77, UC-28c, BR-306–312 và tám sequence Agenda. Xem [bàn giao](08-scrum98-ban-giao.md) và [kết quả kiểm thử](08-scrum98-ket-qua-kiem-thu.md). Các báo cáo SCRUM-25 giữ nguyên như bằng chứng lịch sử.

## Endpoint và quyền

Prefix `/api/meetings/{meetingId}/agenda`. JWT và user active; backend kiểm lại quyền đọc và meeting role hiện hành dưới meeting lock. Admin, Organizer, Approver, Presenter không tự có quyền ghi. Guest adapter chỉ cho đọc; mọi mutation flag bằng false.

| Method / suffix | Actor | Điều kiện | Thành công |
|---|---|---|---|
| GET | Reader theo MeetingReadPolicy/Guest adapter | Được đọc Meeting | 200 snapshot |
| GET /editor-options | Secretary | Meeting cho planning | 200 options |
| POST | Secretary | Meeting cho planning | 201/MSG60 |
| PUT /{itemId} | Secretary | Meeting cho planning, mọi item status | 200/MSG61 |
| DELETE /{itemId} | Secretary | Meeting cho planning, item Pending | 200/MSG62 |
| PUT /order | Secretary | Meeting cho planning, ít nhất 2 item | 200/MSG63 |
| POST /{itemId}/current | Chair | Meeting InProgress, item Pending bất kỳ, chưa có current | 200/MSG64 |
| POST /{itemId}/complete | Chair | Meeting InProgress, item InProgress | 200/MSG65 |
| PUT /{itemId}/conclusion | Chair hoặc Secretary | Meeting InProgress/Ended, item InProgress/Done, chưa có bất kỳ MeetingMinutes | 200/MSG271 |

Meeting cho planning: Draft, WaitingApproval, Rejected, Approved, Published, InProgress. Ended chỉ cho ghi kết luận khi đủ điều kiện. Không có gate Vote/attendance; hoàn thành item không bắt đầu item tiếp theo hoặc kết thúc Meeting. Kết luận không phải điều kiện complete/end.

## Tạo và sửa

```json
{"title":"Báo cáo tiến độ","description":"Nội dung","presenterId":null,"presenterGuestInvitationId":null,"documentIds":[]}
```

Title trim 1–300; description mặc định rỗng, tối đa 4000. PUT thay toàn bộ trường chỉnh sửa; field tùy chọn thiếu trở về null/rỗng. Internal presenter dùng User ID của participant current và active; Guest dùng invitation ID thuộc Meeting, chưa revoked, không cần OTP/session và không loại Declined. Hai ID không đồng thời có giá trị, không nhận Guid.Empty. Cả hai null nghĩa là bỏ presenter. Không dùng tên/email để gán.

Document IDs là MeetingDocument ID, không rỗng/trùng, cùng Meeting, chưa removed. Một tài liệu gắn nhiều item; PUT chỉ sửa link của item hiện tại. Xóa item không xóa tài liệu/StoredFile hoặc link khác. Mọi reader có quyền Meeting đọc metadata theo ADR-0020; download vẫn kiểm quyền riêng.

JSON phân biệt hoa thường, từ chối field lạ, duplicate property, sai kiểu và field hệ thống; tối đa 64 KiB kể cả chunked. `plannedDurationMinutes`, `presenterName` trong request cũ bị từ chối 400/MSG66; không có compatibility shim.

## Thứ tự

```json
{"orderVersion":"token từ GET gần nhất","orderedItemIds":["item-3","item-1","item-2"]}
```

Gửi toàn bộ ID của Meeting mỗi ID đúng một lần, gồm Pending/InProgress/Done. Token SHA-256 của `(Id,Position,Status)` theo Position rồi Id. Body/tập ID sai: 400/MSG143; token cũ: 409/MSG143. Hoán đổi trên các Position hiện có, giữ gap. Tạo mới max(Position)+1; xóa không renumber. No-op update/reorder thành công nhưng giữ UpdatedAt và không phát event. Không nhận `pendingItemIds`.

## Snapshot và options

Snapshot gồm `meetingId, meetingStatus, plannedStartTime, plannedEndTime, serverTime, orderVersion, permissions:{canCreate,canReorder}, items`.

Mỗi item gồm `id, position, title, description, presenterId, presenterGuestInvitationId, presenterName, legacyPresenterName, status, actualStartedAt, actualEndedAt, conclusion, conclusionUpdatedById, conclusionUpdatedByName, conclusionUpdatedAt, canReadDocuments, documents:[{id,title}], actions`.

Không còn plannedDurationMinutes/actualDurationSeconds. Chỉ trả timestamp thực tế, không tính elapsed duration. Presenter name resolve từ User.FullName/Guest.InvitedName khi assignment hiện tại hợp lệ; ID/name hiện tại null nếu không hợp lệ. Tên tự nhập cũ hoặc tên internal không còn hợp lệ hiển thị ở legacyPresenterName, không là assignment và không gửi thông báo. PUT thành công thay assignment và xóa legacy name. Conclusion cùng ba metadata null khi chưa ghi.

Actions: `canEdit`, `canEditPlanningFields` (alias của canEdit), `canDelete`, `canSetCurrent`, `canComplete`, `canRecordConclusion`. CanReorder cần ít nhất hai item. CanSetCurrent áp dụng mọi Pending khi Chair và không current khác. FE dùng flags; không suy quyền từ vai trò hệ thống.

```json
{"presenters":[{"id":"user-id","kind":"Internal","fullName":"Nguyễn Văn A"},{"id":"invitation-id","kind":"Guest","fullName":"Trần Văn B"}],"documents":[{"id":"document-id","title":"Báo cáo.pdf","agendaItemIds":["item-1","item-3"]}]}
```

DTO không trả email/token/storage key. Snapshot rỗng trả items:[]; lỗi đọc 500/MSG128.

## Kết luận UC-77

```json
{"conclusion":"Thống nhất phương án triển khai."}
```

Trim đầu/cuối, giữ newline bên trong, bắt buộc 1–2000 theo `.NET string.Length` (UTF-16 code units; JavaScript string.length tương ứng). Không cho xóa bằng chuỗi rỗng/null. Không nhận actor/time từ body. Mỗi save hợp lệ ghi lại toàn khối nội dung, actor, UTC time, kể cả cùng nội dung. Meeting lock serialize; lần save thành công sau cùng thắng, không có ETag riêng.

| Tình huống | Response |
|---|---|
| Thành công | 200/MSG271 |
| Invalid body/conclusion | 400/MSG272 |
| Reader thiếu Chair/Secretary | 403/MSG273 |
| Trạng thái hoặc đã có biên bản | 409/MSG273 |
| Meeting/item không có hoặc không được đọc | 404/MSG174 cùng body |
| Persistence thất bại | 503/MSG274 |
| Content-Type sai / quá 64 KiB | 415/MSG34 / 413/MSG87 |
| Anonymous / inactive / Guest write | 401 / 403 / 403 theo gate hiện tại |

Mutation response `{code,message,meetingId,agendaItemId?}`. Conclusion success message: `Lưu kết luận mục nghị sự thành công.`; 404 dùng `Không truy cập được cuộc họp này.`. Các mutation khác giữ 404 body rỗng; validation 400/MSG66; planning denied MSG67; delete non-Pending MSG142; start/complete denied MSG73/74; current conflict MSG72; persistence MSG68–71/75–76. MSG141/144/250 ngừng dùng.

## Transaction, notification và tích hợp

Mỗi mutation khóa Meeting, kiểm lại active/read/role/state, validate rồi save atomic và commit. FK/check/index bảo vệ loại presenter, metadata conclusion và tối đa một current. End Meeting hoàn tất current cùng Meeting/history, Pending giữ nguyên.

Remove internal/revoke Guest clear assignment presenter và legacy trên mọi item cùng transaction removal/audit; không đổi status/position/actual/conclusion/documents. Agenda mutation không tăng roster revision.

Chỉ gán/đổi presenter ở Published/InProgress mới enqueue sau commit: Internal nhận Notification type AgendaPresenterAssigned, target Meeting; Guest nhận EmailMessage qua worker hiện có. Không gửi bù khi publish, bỏ presenter không thông báo. Event ID và recipient tạo dedup key; A→B→A là ba assignment riêng. Notifier dùng scope/DbContext riêng, khóa Meeting và kiểm lại assignment/recipient. Lỗi enqueue/push/SMTP không thay kết quả đã commit; log ID, không log conclusion/token. Enqueue best effort, không cam kết phục hồi event chưa lưu; SMTP tắt thì email Queued chờ worker.

Hub `/hubs/meetings`: JoinMeeting/LeaveMeeting; event `agendaChanged` gồm eventId,meetingId,changeType,agendaItemId?,title?,occurredAt. Types: Created/Updated/Deleted/Reordered/Started/Completed/MeetingEnded/ConclusionUpdated/PresenterCleared. Không chứa conclusion/email. Publisher kiểm lại từng recipient, không broadcast dữ liệu cho cả Meeting. Sau commit mới gửi, yêu cầu hủy sau 2 giây và chờ scoped task kết thúc. FE refetch sau mutation/event; reconnect phải join lại và GET. Không bảo đảm durable realtime.

Guest adapter mặc định vẫn từ chối; [UC-04 contract](08-agenda-contract-tich-hop-guest-uc04.md) là phụ thuộc riêng. UC-59 writer phải dùng cùng meeting lock, đọc conclusions/presenter và tạo MeetingMinutes trong một transaction. Có bất kỳ biên bản, bất kể status/content, khóa conclusion; không tạo/xóa nháp tạm gây mở khóa. SCRUM-98 không thực hiện Guest login hoặc sinh nháp AI.
