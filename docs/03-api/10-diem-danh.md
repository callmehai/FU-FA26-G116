# Điểm danh — Backend SCRUM-26 / UC-31

Ngày 20/10/2026. Nguồn nghiệp vụ: Jira SCRUM-26, UC-31, BR-116/170/171/172/178, ADR-0016/0017.
Quyết định concurrency: [ADR-0025](../05-decisions/0025-version-rieng-cho-diem-danh.md).
[Bàn giao FE/BA](10-diem-danh-ban-giao.md), [kiểm thử](10-diem-danh-ket-qua-kiem-thu.md).

## 1. Quyền và phạm vi

- Internal dùng JWT hiện có; Guest đi qua `IMeetingGuestIdentityResolver` và `IParticipantGuestAccess` của UC-04. Adapter mặc định từ chối Guest, không phát session giả.
- GET theo quyền đọc meeting/roster UC-27 trên main: owner, approver và thành phần hiện hành theo MeetingReadPolicy; Guest phải có invitation current, meeting đã công bố và session đã xác minh, chưa hết hạn/revoke. Internal phải active.
- Mọi người đọc roster được xem invitationResponse theo chính sách SCRUM-20 mới; không còn grants riêng.
- POST chỉ Secretary hiện hành, tài khoản active, Meeting InProgress. Re-authorize ở mỗi lần lưu; token không thay thế phân quyền.
- API che tài nguyên không được biết bằng 404; người được đọc meeting nhưng không được ghi nhận 403. User inactive nhận 403, chưa xác thực 401.
- Chỉ ghi Present/Absent cho entry được gửi. Không suy ra attendance từ response hoặc phòng gọi; thiếu attendance không chặn End Meeting.

## 2. GET `/api/meetings/{meetingId}/attendance`

Query duy nhất: `page` mặc định 1, tối thiểu 1; `pageSize` mặc định 20, từ 1 đến 100. Query lạ/lặp/sai trả 400/MSG278 sau kiểm quyền. Trang vượt phạm vi trả items rỗng. Header `Cache-Control: no-store`.

```json
{
  "meetingId": "<guid>",
  "meetingStatus": "InProgress",
  "serverTime": "2026-10-20T03:00:00+00:00",
  "permissions": { "canRecordAttendance": true, "canViewInvitationResponses": true },
  "items": [{
    "id": "<assignment-or-invitation-guid>",
    "kind": "Internal",
    "displayName": "Nguyễn Văn A",
    "role": "Member",
    "isActive": true,
    "invitationResponse": "Accepted",
    "attendance": "Present",
    "legacyAttendance": null,
    "rowVersion": "<64 lowercase hex characters>",
    "recordedBy": { "id": "<secretary-user-guid>", "displayName": "Thư ký" },
    "recordedAt": "2026-10-20T03:00:00+00:00"
  }],
  "total": 1, "page": 1, "pageSize": 20,
  "totals": { "present": 1, "absent": 0, "unmarked": 0, "legacy": 0, "total": 1 }
}
```

`kind` là Internal hoặc Guest. Guest có role/isActive null, displayName từ profile được adapter trả hoặc fallback InvitedName. Internal inactive vẫn xuất hiện nếu assignment current. Không trả email hay credential. Sắp xếp tên (ordinal ignore-case), kind, ID; tổng tính trên cả roster, không chỉ trang.

| Dữ liệu lưu | attendance | legacyAttendance | Bộ đếm |
|---|---|---|---|
| Unknown | null | null | unmarked |
| Present | Present | null | present |
| Absent | Absent | null | absent |
| Excused/Delegated | null | Excused/Delegated | legacy |

Recorder/time thiếu trong lịch sử vẫn null. GET dùng RepeatableRead, profile/recorder đọc theo batch. Không tự sửa lịch sử khi GET.

## 3. POST cùng URL

```json
{
  "entries": [{
    "id": "<assignment-or-invitation-guid>",
    "kind": "Internal",
    "attendance": "Present",
    "expectedVersion": "<rowVersion from GET>"
  }]
}
```

- `Content-Type: application/json`; tối đa 64 KiB kể cả chunked. Không có query POST.
- entries bắt buộc, không rỗng, không null item, không trùng `(kind,id)`; GUID không rỗng.
- Tên field phân biệt hoa/thường; từ chối field lạ/trùng, enum số, giá trị không đúng Present/Absent và Internal/Guest. Không nhận actor/time/response/role từ client.
- id là assignment/invitation ID, không phải UserId. Người không gửi giữ nguyên.
- expectedVersion bắt buộc là 64 ký tự hex chữ thường. Token đúng định dạng nhưng sai phiên bản/entry trả 409/MSG151.

```json
{
  "code": "MSG147", "message": "Lưu điểm danh thành công.",
  "meetingId": "<guid>", "changedCount": 1,
  "entries": [{
    "id": "<guid>", "kind": "Internal", "attendance": "Present", "legacyAttendance": null,
    "rowVersion": "<new token>",
    "recordedBy": { "id": "<guid>", "displayName": "Thư ký" },
    "recordedAt": "2026-10-20T03:00:00+00:00"
  }]
}
```

Response giữ thứ tự entry request, gồm cả no-op. No-op chỉ hợp lệ khi token còn đúng; không tăng version/đổi metadata/audit/event.

### Concurrency và transaction

Mỗi bảng có `AttendanceVersion: bigint NOT NULL DEFAULT 0`. Token = SHA256 UTF-8 của `attendance-v1|meetingId|kind|entryId|version`; GUID D chữ thường, version invariant decimal, output lowercase hex. FE chỉ giữ token opaque, không tự tính.

ReadCommitted → khóa meeting FOR UPDATE → khóa actor User FOR SHARE → đọc lại quyền/state → kiểm body → khóa target Internal trước Guest, ID tăng dần → đọc version sau khóa → kiểm toàn bộ entry → cập nhật các trường điểm danh/version → audit → SaveChanges → commit → publish.

Không tăng ParticipantsRevision. Không dùng xmin hoặc If-Match roster. Role/response không đổi version; mất quyền hoặc removed/revoked vẫn bị chặn. Hai request cùng token có thay đổi: một commit thắng, request sau stale. Hai target khác nhau đều có thể thành công. Batch một entry stale rollback toàn bộ. Tăng version bằng checked; overflow rollback, không quay vòng. Các writer mới phải tuân thủ cùng giao thức khóa meeting và chỉ sửa attendance qua service này.

Audit `meeting.attendance.recorded`: một dòng/batch thay đổi, object Meeting, Actor/IP server, before/after status/version/recorder/time theo entry; cùng transaction. Không có bảng audit riêng. Dữ liệu legacy được giữ; chỉ Secretary sửa tường minh ở InProgress mới đổi sang Present/Absent.

### Lỗi

| HTTP/code | Xử lý |
|---|---|
| 400/MSG148 | Sửa body/entry/token format |
| 409/MSG149 | Không còn InProgress; tải lại và khóa thao tác |
| 409/MSG151 | Tải lại, so sánh draft, yêu cầu người dùng quyết định; không auto retry |
| 503/MSG150 | Lỗi lưu hoặc commit chưa xác định; GET lại trước khi thử tiếp |
| 500/MSG289 | GET lỗi đọc |
| 400/MSG278 | Query GET sai |
| 415/MSG34, 413/MSG87 | Sửa content-type/kích thước body |
| 401, 403, 404 | Theo authentication/quyền tài nguyên; không có code mới |

Lỗi trước commit rollback toàn bộ. Mất kết nối lúc commit có thể không xác định được kết quả; MSG150 không hứa rằng chưa lưu. Lỗi publication sau commit được log, response lưu vẫn thành công.

## 4. SignalR và Guest integration

Hub `/hubs/meetings`, JoinMeeting/LeaveMeeting hiện có. Event `attendanceChanged` chỉ chứa `{eventId,meetingId,occurredAt}`, không chứa roster/PII. Event là tín hiệu GET lại, không phải nguồn dữ liệu.

Publisher đọc lại quyền meeting, current assignments và active users, bao gồm owner/approver đủ quyền. Chỉ group đã join và còn quyền đọc nhận event. Guest cần cả participant adapter và agenda/hub adapter xác thực cùng meeting/invitation/session. `IParticipantGuestAccess.ListReadersAsync` mặc định rỗng; UC-04 cung cấp session còn hiệu lực, CanAccessAsync/CanReadAsync vẫn kiểm lại mỗi session. Dùng nhóm Guest theo session hiện có, không gom chỉ theo invitation.

Publication best effort sau commit, awaited với timeout 5 giây, không fire-and-forget scoped DbContext, không thêm outbox. GET khi reconnect/focus và polling dự phòng để phục hồi event bị mất. Test adapter không thay thế production UC-04.

## 5. Database và vận hành

Dependency SCRUM-20 giữ nguyên migration riêng. Migration mới `AddAttendanceVersions` chỉ thêm `attendance_version` vào `meeting_participants` và `meeting_guest_invitations`. Không table/index/trigger/check constraint mới. Default 0, giữ nguyên attendance/recorder/time/response/current-history. Không cấu hình concurrency token toàn entity để tránh làm response writer thất bại vì điểm danh thay đổi.

Chỉ áp dụng/rollback trong DB test riêng trong lần triển khai này. Không tự migrate server chung. Down xóa hai cột, vì vậy không downgrade database đang phục vụ attendance; test Down chỉ chứng minh dữ liệu nghiệp vụ khác được giữ, version bị reset khi Up lại. Snapshot/model phải khớp, migration SCRUM-20 bất biến.

## 6. Tích hợp FE

Màn hình theo AI-eCabinet: điểm danh là một cột trong bảng thành phần, chọn là lưu, không có nút Lưu.

- **Trang chi tiết**, tab "Thành phần tham dự": cột "Điểm danh" chỉ hiện khi Meeting `InProgress` hoặc `Ended`; trước đó FE không gọi GET. Thư ký (`canRecordAttendance`) thấy hai nút "Có mặt" / "Vắng"; người khác thấy chip "Có mặt" / "Vắng" hoặc chữ "Chưa điểm danh". Người ghi và thời điểm nằm trong tooltip. Chip "Có mặt x/y" trên thanh công cụ lấy từ `totals`.
- **Phòng họp**: nút "Điểm danh" cạnh "Người dự" mở panel 360px cùng dữ liệu, cùng ô điều khiển; Thư ký điểm danh mà không rời phòng.
- Hai nơi dùng chung `useAttendance` và `AttendanceCell` (ADR-0011). FE đọc cả roster bằng `pageSize=100`, nối trang khi dài hơn, ghép vào bảng theo `id` (assignment hoặc invitation ID).
- Mỗi lần bấm gửi đúng một entry với `rowVersion` đang hiển thị. Không có bản nháp nên không có gì để giữ khi `409/MSG151`: FE hiện thông báo của BE, đọc lại, người dùng bấm lại nếu vẫn muốn. `409/MSG149`, `503/MSG150` và `403` (không còn là Thư ký) cũng hiện thông báo rồi đọc lại. Không tự thử lại.
- Đọc lại khi có `attendanceChanged` hoặc `participantsChanged`, khi hub kết nối lại, khi cửa sổ được focus, và mỗi 15 giây khi Meeting `InProgress` và tab đang hiển thị; không bao giờ hai GET chồng nhau. Kết quả GET cũ hơn một lần lưu bị bỏ.

Khác ticket: SCRUM-26 ghi "AttendancePanel dùng chung, variant page/room". Trang chi tiết đặt điểm danh vào bảng thành phần như AI-eCabinet thay vì một panel riêng; phòng họp dùng `AttendancePanel`. Logic và ô điều khiển vẫn viết một lần.
