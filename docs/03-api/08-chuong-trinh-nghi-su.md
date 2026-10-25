# Backend chương trình nghị sự — SCRUM-25

Phạm vi UC-23a–d, UC-24, UC-25, UC-26; chỉ Backend. Baseline nghiệp vụ: đặc tả/BR và bảy sequence Agenda ngày 11/10/2026, đã đối chiếu cập nhật tài liệu trên `main` tại `729c1e0` ngày 13/10/2026. Không có `needsVote`, Meeting Session hoặc API Start/End Meeting.

## Contract triển khai

Prefix `/api/meetings/{meetingId}/agenda`, JWT Bearer và user active. GET trả snapshot; GET `/editor-options` trả lựa chọn cho Secretary. POST tạo (201/MSG60); PUT `/{itemId}` sửa (200/MSG61); DELETE `/{itemId}` xóa (200/MSG62); PUT `/order` sắp xếp (200/MSG63); POST `/{itemId}/current` bắt đầu (200/MSG64); POST `/{itemId}/complete` hoàn thành (200/MSG65).

POST/PUT nhận `title` (trim, 1–300), `description` (tối đa 4000, mặc định rỗng), `plannedDurationMinutes` (Int32 dương bắt buộc), `presenterId` (nullable), `presenterName` (trim, tối đa 200, không đồng thời với ID), `documentIds` (GUID không rỗng/không trùng, mặc định []). PUT thay thế toàn bộ trường planning. Không nhận trường hệ thống. JSON tối đa 64 KiB, kể cả chunked.

Secretary được planning trong Draft, WaitingApproval, Rejected, Approved, Published, InProgress. Ended chỉ đọc. Item đã bắt đầu chỉ sửa title/description; các trường planning gửi lại không đổi được chấp nhận. Chair chỉ start/complete trong meeting InProgress; chỉ Pending đầu tiên được start và tối đa một current item. Admin, Organizer, Approver, Presenter không tự có quyền Secretary/Chair.

GET trả meetingId, meetingStatus, plannedStartTime, plannedEndTime, serverTime, orderVersion, permissions {canCreate, canReorder}, items[]. Item có id, position, title, description, plannedDurationMinutes, presenterId, presenterName, status, actualStartedAt, actualEndedAt, actualDurationSeconds, canReadDocuments, documents [{id,title}], actions {canEdit,canEditPlanningFields,canDelete,canSetCurrent,canComplete}. Status PascalCase; timestamp UTC. Duration tính từ timestamp; chưa hoàn thành null. Ai xem được cuộc họp (`MeetingReadPolicy`, kể cả Người tổ chức/Người duyệt không có tên trong thành phần, như `GET …/documents`) hoặc Guest được adapter UC-04 xác nhận nhận metadata tài liệu, `canReadDocuments` luôn `true`; Guest luôn có tất cả cờ mutation false. Snapshot rỗng là 200/items:[]; lỗi đọc là 500/MSG128.

Reorder nhận `{orderVersion,pendingItemIds}`. Token SHA-256 của danh sách `(Id,Position,Status)` sắp Position rồi Id; token cũ trả 409/MSG143. Danh sách phải chứa đúng mỗi Pending item một lần. Chỉ hoán đổi các vị trí Pending; không renumber hoặc đổi actual timestamps. Tạo nối max(Position)+1; xóa giữ gap. Mutation/no-op trả `{code,message,meetingId,agendaItemId?}`, FE GET lại; no-op không đổi UpdatedAt hoặc phát event.

401 chưa đăng nhập; 403 inactive hoặc thiếu meeting role; 404 body rỗng cho meeting/item không tồn tại hoặc không được đọc; 409 conflict nghiệp vụ; 400/MSG66 input; 415/MSG34 Content-Type; 413/MSG87 body lớn; 503/MSG68–71,75–76 persistence. Chi tiết mã xem catalogue.

Mỗi mutation khóa meeting trước, kiểm lại actor/assignment/status dưới lock, lưu item/document trong một transaction ReadCommitted. Các writer participant/document/lifecycle phải dùng cùng khóa. Current model loại assignment Delegated; SCRUM-20 phải cập nhật policy khi thêm Current/Removed. Từ SCRUM-107, document IDs là **MeetingDocument ID** (tài liệu đang hiệu lực của cuộc họp, xem [11-tai-lieu-cuoc-hop.md](11-tai-lieu-cuoc-hop.md)); liên kết nằm ở bảng `meeting_document_agenda_items`. Agenda vẫn giữ mỗi tài liệu tối đa một mục; không chuyển tài liệu từ item khác; bỏ chọn chỉ xoá dòng liên kết, tài liệu vẫn trong cuộc họp.

## Ví dụ response

Ví dụ snapshot (các ID minh họa; `orderVersion` luôn lấy nguyên token Backend trả về):

```json
{
  "meetingId": "25000000-0000-7000-8000-000000000002",
  "meetingStatus": "InProgress",
  "plannedStartTime": "2026-10-12T01:00:00+00:00",
  "plannedEndTime": "2026-10-12T02:00:00+00:00",
  "serverTime": "2026-10-12T01:10:00+00:00",
  "orderVersion": "<opaque SHA-256 token>",
  "permissions": { "canCreate": true, "canReorder": true },
  "items": [{
    "id": "25000000-0000-7000-8000-000000000010",
    "position": 4,
    "title": "Thảo luận kế hoạch",
    "description": "",
    "plannedDurationMinutes": 20,
    "presenterId": null,
    "presenterName": "Báo cáo viên",
    "status": "Pending",
    "actualStartedAt": null,
    "actualEndedAt": null,
    "actualDurationSeconds": null,
    "canReadDocuments": true,
    "documents": [],
    "actions": { "canEdit": true, "canEditPlanningFields": true, "canDelete": true, "canSetCurrent": false, "canComplete": false }
  }]
}
```

`editor-options` trả `{presenters:[{id,fullName}],documents:[{id,title,agendaItemId}]}`. `presenterName` ở snapshot là tên hiển thị đã resolve; khi PUT với presenterId, gửi presenterName rỗng hoặc bỏ trường này, không gửi lại tên tài khoản như tên tự nhập. `errors` là map tên field → mảng câu tiếng Việt; JSON sai cấu trúc dùng key `body`.

## Realtime và điểm tích hợp

Theo ADR-0016, Guest đã xác minh và còn quyền theo lời mời đọc tài liệu cuộc họp như Member. SCRUM-25 đã có đường đọc snapshot và hub qua `IMeetingGuestIdentityResolver` / `IAgendaGuestAccess`, group tách theo invitation/session và kiểm quyền lại mỗi request/lần phát. Adapter mặc định từ chối; UC-04/SCRUM-20 cần cung cấp OTP/session/repository thật và thay đăng ký DI theo [contract tích hợp Guest](08-agenda-contract-tich-hop-guest-uc04.md). Không dùng `MeetingRole.Guest` của model User cũ hoặc tên presenter tự nhập làm bằng chứng xác minh. Các test adapter chứng minh phía Agenda, chưa phải nghiệm thu UC-04 end-to-end.

Hub `/hubs/meetings`: `JoinMeeting(meetingId)`, `LeaveMeeting(meetingId)`. Event `agendaChanged`: eventId, meetingId, changeType (Created/Updated/Deleted/Reordered/Started/Completed/MeetingEnded), agendaItemId?, title?, occurredAt. Publisher kiểm lại active user và MeetingReadPolicy trước mỗi lần gửi tới group meeting/user. Sau commit mới gửi; lỗi push không đổi thành công lưu. Không bảo đảm durable delivery: FE phải resync.

SCRUM-69 gọi helper hoàn thành current item trong transaction đã khóa meeting, dùng cùng thời điểm End Meeting; helper không SaveChanges/Commit/publish. Pending giữ nguyên. Endpoint End Meeting và đóng room chưa nằm trong task này.

ADR-0017/BR-159 mới không yêu cầu đóng Vote hoặc điểm danh đủ trước End Meeting. Helper Agenda hiện không có các gate này và không sửa Vote/attendance; SCRUM-69 phải giữ nguyên trạng thái Vote/ballot, hoàn tất current agenda cùng Meeting/history trong một transaction, rồi đóng room sau commit. Không thêm kiểm tra tất cả agenda phải Done.

Publisher nhận cancellation sau 2 giây, nhưng request vẫn chờ task publish kết thúc để scoped DbContext không bị dispose khi task còn dùng. Đây là thời hạn yêu cầu hủy hợp tác, không phải giới hạn cứng thời gian response; provider chậm hoàn tất cancellation có thể làm response chậm hơn. Lỗi publish, kể cả lỗi phát sinh sau thời hạn này, được log và không đổi kết quả mutation đã commit.

## Mapping nghiệm thu

| UC | API | MSG thành công | Nhóm test |
|---|---|---|---|
| 23a | POST | 60 | Validation, append, document atomicity, concurrent create |
| 23b | PUT item | 61 | Locked fields, no-op, fresh state after lock |
| 23c | DELETE item | 62 | Pending only, gap, document/version preservation |
| 23d | PUT order | 63 | Token, fixed positions, concurrent order |
| 24 | POST current | 64 | Chair, first Pending, single current, timestamp |
| 25 | POST complete | 65 | Chair, actual duration, concurrent complete, helper rollback |
| 26 | GET/hub | — | Visibility, response shape, event isolation/revocation/reconnect |

Minimal API AgendaEndpoints tương ứng Controller trong diagram; AgendaService điều phối; IMeetingRepository/MeetingReadPolicy được tái sử dụng. AgendaDocumentRepository là adapter model tài liệu hiện tại. Không thêm Application project.

Cập nhật review 21/10/2026: Agenda, Participants và MeetingHub dùng chung `MeetingGuestSession` và `IMeetingGuestIdentityResolver`; UC-04 chỉ đăng ký một resolver. Các port kiểm quyền theo feature vẫn tách riêng, dùng cùng invitation/session storage. Tên group Guest được tạo duy nhất bằng `MeetingHub.GuestGroup`.

## Tích hợp End Meeting — SCRUM-69

Lifecycle End gọi IAgendaMeetingEnd trong cùng transaction và khóa Meeting. Current item thành Done với mốc End; Pending/Done khác giữ nguyên. Không có current item vẫn End được. Sau commit luôn phát agendaChanged/MeetingEnded, kể cả agendaItemId/title null; FE GET lại Meeting và Agenda để làm mới status/quyền. Lỗi publish không rollback End. [Bàn giao UC-21/22](10-scrum69-ban-giao.md).
