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

- Giữ rule không rút `WaitingApproval` về Draft và không đổi assignment trong trạng thái này.
  Approver inactive/mất quyền khiến meeting tiếp tục chờ. Khôi phục quyền/active qua quản trị hoặc
  chốt một story xử lý khác; không mở transition mới trong SCRUM-19.
- Cấm tự duyệt mọi mức độ mật. Submit cho Organizer trả `400 MSG52`; Approve/Reject trên assignment
  cũ trỏ về Organizer trả `403 MSG55`. Có hai capability hoặc quan hệ Chair không bypass được.
- Readiness vẫn cần agenda không rỗng và title/giới hạn field theo schema. Thứ tự liên tục, duration
  dương, presenter bắt buộc và chỉ cho Pending chưa được chốt bởi SCRUM-25, nên không áp ở đây.
- GET history không khóa ghi. Development seed có Draft hoàn chỉnh để test trước khi API agenda/
  participant được tích hợp; seeder không reset demo đã thao tác.

## Contract producer SCRUM-19 → consumer SCRUM-15

Publish commit status, một history, notification rows và email jobs cùng transaction. Worker chỉ
được đọc jobs đã commit. Không gọi SMTP, không render template HTML, không tạo `.ics` trong transition.
Các trường dưới đây là dữ liệu publication đã snapshot, không phải thiết kế template email cuối cùng.

| Trường persisted | Ý nghĩa/ownership |
|---|---|
| `MessageType = MeetingInvitation` | Phân biệt invitation job với email khác; consumer kiểm tra trước khi xử lý |
| `MeetingId`, `OriginHistoryId` | Meeting và history Publish tạo job; dùng cho truy vết và chống tạo batch lặp |
| `RecipientUserId`, `ToAddress` | Recipient/address tại transaction Publish; không query participant hiện tại để thay recipient |
| `Status = Queued`, `SentAt = null` | Chưa gửi; worker SCRUM-15 chịu trách nhiệm delivery/retry/failure |
| `PayloadSnapshot` | JSON snapshot nội dung công bố; không đọc lại meeting để thay nội dung batch đã commit |
| `Subject`, `Body` | Giá trị text ban đầu theo schema email hiện có; template và `.ics` thuộc SCRUM-15 |

Payload hiện tại dùng tên property PascalCase và ngày giờ ISO 8601 UTC:

```json
{
  "MeetingId": "0199ac54-d000-7000-8000-000000000019",
  "Title": "Cuộc họp demo",
  "Description": "Nội dung công bố",
  "StartsAt": "2026-10-01T02:00:00+00:00",
  "EndsAt": null,
  "Location": "Phòng A",
  "Sensitivity": "Internal",
  "DetailPath": "/mgmt/meetings/0199ac54-d000-7000-8000-000000000019"
}
```

BR-54 chỉ chọn participant có user active và `Response != Delegated`. Người được ủy quyền chỉ nhận
nếu có participant row riêng đủ điều kiện. Mọi thay đổi meeting/participant/email sau commit không
sửa snapshot của batch cũ. Worker phải dùng chính payload/address persisted để tạo invitation/`.ics`,
và đặt policy xử lý job cũ thiếu payload trước khi bật consumer. Không đổi shape payload trong story
khác mà chưa cập nhật contract producer/consumer và test tương thích.

## Checklist handoff cho FE/Tester

- Happy path: Draft → WaitingApproval → Approved → Published; email vẫn Queued tại response.
- Reject thiếu/null/whitespace reason: `400 MSG53`; reason hợp lệ trim và giữ bất biến trong history.
- Rejected phải ReturnToDraft trước submit mới; history riêng, clear assignment, không invitation.
- Current approver phải active/có capability; former approver không có quyền nhờ history cũ.
- Organizer/current approver xem full reason; participant chỉ xem timeline redacted sau Published.
- Self-submit/legacy self-review và WaitingApproval ReturnToDraft bị từ chối, không ghi side effects.
- Approver list database/timeout ở capability gate hoặc repository đều trả `503 MSG61`;
  request cancellation vẫn được propagate, không đổi thành 503.
- History đọc khi meeting đang bị writer khóa; concurrent Publish chỉ commit một history/batch.
- Participant writer SCRUM-20 và agenda writer SCRUM-25 dùng cùng meeting-row lock protocol.
- FE xử lý mã HTTP/MSG, conflict bằng reload, lỗi tạm thời bằng retry; không báo email đã gửi.

## Tách thay đổi ngoài ticket

Fix `RoleService` cho PostgreSQL RESTRICT (`23001`) và refactor các fixture Access/Authentication
được chuẩn bị riêng trên branch `codex/role-postgres-test-fixes` từ `origin/main`, chưa commit.
Đây là dependency sửa lỗi full suite, không phải hành vi meeting. `PostgreSqlTestDatabase` là helper
chung cần cho fixture meeting chạy PostgreSQL thật với database riêng.

Vì PR #41 đã chứa các fix đó từ commit trước, phải đưa dependency lên main trước rồi cập nhật base
của SCRUM-19 để chúng tự hết xuất hiện trong diff PR. Không revert chúng ngay khỏi feature khi chưa
có dependency: việc đó sẽ đưa lại lỗi Role/cleanup và có thể làm fixture meeting mất helper. Việc
commit, tạo PR dependency và cập nhật branch do người dùng thực hiện; agent không commit/push/merge.
