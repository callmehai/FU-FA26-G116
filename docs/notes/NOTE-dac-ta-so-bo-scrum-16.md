# Đặc tả sơ bộ SCRUM-16 (nhật ký kiểm toán)

**Ngày:** 2026-11-10 · **Người viết:** BE (Hoàng Đức Lộc) · **Trạng thái:** đã chốt, đã đưa vào Report 3 ngày 10/11/2026

| Jira | Tên | Trạng thái lúc viết |
|---|---|---|
| [SCRUM-16](https://sep490g116.atlassian.net/browse/SCRUM-16) | Nhật ký kiểm toán các hành động quan trọng | Frontend, Sprint 4 |

Đây là bước *BE: basic UC Specification + Sequence Diagram* của
[DEVELOPMENT_WORKFLOW.md §2](../DEVELOPMENT_WORKFLOW.md). Ticket viết ngày 18/09/2026 và chưa sửa từ đó. Phía **ghi**
nhật ký đã được làm rải trong các ticket khác; phía **đọc** (UC-13) chưa có dòng code nào. UC và BR ở đây là **đề
xuất**: sau khi duyệt mới đưa vào DOCX ở `08-reports/source/` rồi sinh lại `05-use-case-specifications.md` và
`06-business-rules.md`. Không chép tay sang hai file đó.

**10/11/2026 — đã chốt** cả bốn câu hỏi ở §9 theo đề xuất, và **đã đưa vào Report 3 DOCX** nguyên văn §3–§5, giữ nguyên
số: UC-13 viết lại; UC-13 thêm vào cột *Applies to* của BR-11, BR-12, BR-31; BR-440 → BR-446 (cột *Rationale* lấy từ cột
*Nguồn*, nhóm *Audit*); `MSG469`–`MSG470` ở §V.2, loại *Dành sẵn, chưa triển khai*; một dòng Change Log. Chưa sửa (theo
yêu cầu): đặc tả màn 3.3 và wireframe. Cũng chưa sửa: bước trong UC-01, UC-02, UC-04, UC-18 → UC-22, UC-75, UC-64 nhắc tới
BR-444 → BR-446 — BA thêm ở BA Final Review khi phía ghi đã có code.

- Không thêm số UC: SCRUM-16 là **UC-13 View Audit Log** (FE-03), đã có đặc tả từ 05/10/2026 nhưng viết trước khi có
  dữ liệu thật.
- BR-440 → BR-446 là số đề xuất. BR-439 đã do PR #308 (BA Final Review SCRUM-147–151) nhận.
- `MSG469`, `MSG470` đã **dành sẵn** trong [danh mục](../03-api/00-danh-muc-ma-thong-bao.md) cùng commit với note này;
  mã tiếp theo là `MSG471`.

## 1. Hiện trạng code (`main` ddbd59b5, 10/11/2026)

| Phần | Đã có | Chưa có |
|---|---|---|
| Lưu trữ | Bảng `audit_log` (`AuditLogEntry`): `actor_id` (null được), `action`, `object_type`, `object_id`, `ip_address`, `details` (jsonb), `occurred_at`. Index theo `occurred_at`, (`object_type`, `object_id`), (`action`, `occurred_at`) | — |
| Ghi | `IAuditService.RecordAsync` (`AuditService` tự gắn IP của request). 37 mã trong `AuditActions`; thêm `meeting.attendance.recorded` và `meeting.participants.<thao tác>` ghi thẳng vào `db.AuditLog` | Ba loại sự kiện ticket yêu cầu — xem §1.1 |
| Chỉ ghi thêm | Không code nào sửa hay xoá dòng `audit_log` | Chốt chặn: `TrustCabinetDbContext.EnsureMeetingHistoryIsAppendOnly` chỉ canh `MeetingStatusHistory`, chưa canh `AuditLogEntry` |
| Đọc | — | Endpoint `GET /api/audit-log`, service truy vấn, test, contract trong `docs/03-api/` |
| Giao diện | — | Màn, route, mục sidebar, `api/`, `types/` |

Các nhóm sự kiện đang được ghi: tài khoản (`user.*`, 7 mã), vai trò và quyền (`role.*`, 4), thành phần và điểm danh
(`meeting.participants.*`, `meeting.attendance.recorded`), xem tài liệu của cuộc họp Mật (`document.viewed`), Quản trị
đọc kho của tài khoản đã khoá (`library.read`), bản ghi (`recording.*`, 6), gỡ băng (`transcript.*`, 5), câu nói theo
micro (`speech_clip.*`, 5), Voice ID (`voice.*`, 5), xoá nháp biểu quyết khi kết thúc họp
(`vote.draft_deleted_on_meeting_end`), Khách vào phòng họp (`call.guest_admitted`), cấu hình AI (`ai_settings.updated`).

### 1.1 Đối chiếu tiêu chí nghiệm thu của ticket

| Tiêu chí | Hiện trạng |
|---|---|
| Ghi lại đăng nhập | **Chưa.** `AuthenticationService` chỉ ghi `user.google_linked`. Đăng nhập thành công, thất bại và Khách xác minh OTP đều không có dòng nào |
| Ghi lại chuyển trạng thái cuộc họp | **Chưa vào `audit_log`.** Có ở `meeting_status_history` (ai, lúc nào, lý do; không có IP), nhưng Quản trị không phải thành phần thì không mở được cuộc họp nên không đọc được |
| Ghi lại tải tài liệu | **Không ghi, có chủ đích.** [ADR-0057](../05-decisions/0057-ba-muc-mat-nhu-ai-ecabinet.md): chỉ ghi lượt xem tài liệu của cuộc họp Mật, và cuộc họp Mật không tải về được (BR-188). Tải bản ghi và xuất bản gỡ băng thì có ghi (BR-351) |
| Ghi lại xem tài liệu mật | Có — `document.viewed`, cả người dùng và Khách (BR-318) |
| Ghi lại ban hành biên bản | **Chưa.** `MinutesService.ApproveAsync` đặt `IssuedAt` và gửi thông báo, không ghi audit |
| Ghi lại đổi quyền người dùng | Có — `user.updated` (vai trò trước/sau), `role.permissions.updated`, `role.created/updated/deleted` |
| Ai, làm gì, tài nguyên nào, lúc nào, IP nào | Có đủ cột. Dòng do hệ thống tự ghi thì IP rỗng |
| Chỉ ghi thêm | Đúng trong thực tế, chưa có chốt chặn (bảng trên) |
| Không trộn vào log ứng dụng | Đúng — bảng riêng, service riêng |
| `GET /api/audit-log`, chỉ quản trị viên | **Chưa có** |

Hai điểm phía ghi ảnh hưởng tới phía đọc:

- **Khách được ghi theo bốn cách.** Khách không có dòng `users` nên `actor_id = null`, và lời mời nằm trong `details`
  dưới bốn tên khác nhau: `guestInvitationId` (`document.viewed`), `invitationId` (`call.guest_admitted`),
  `GuestInvitationId` (`speech_clip.deleted` do Khách tự xoá), `guest` (`meeting.participants.*`). `actor_id = null`
  mà không có khoá nào trong số đó là hệ thống tự làm.
- **Hai chỗ ghi dùng mã gõ tay** thay cho hằng số trong `AuditActions`: thành phần (`ParticipantService`) và điểm danh
  (`AttendanceService`). Danh mục tên sự kiện ở §6 phải kể cả hai.

### 1.2 Tài liệu hiện có

| Tài liệu | Có chưa | Nhận xét |
|---|---|---|
| UC Specification UC-13 (Report 3 §3.4) | Có | Chung chung: không BR, không MSG, không nói bộ lọc nào, có "outcome" mà dữ liệu không lưu, chưa có Khách và hệ thống. Bản đề xuất ở §3 |
| Sequence Diagram UC-13 | Có | Đã vẽ lại theo §3 trong commit này (source, PNG, tab draw.io, ảnh `drawio-png/`) |
| Class Diagram FE-03 | Có | Đã có `AuditLogPage`, `AuditLogController`, `AuditLogQueryService`, `AuditLogRepository`, `AuditLogEntry`. Đủ để làm; sửa as-built sau khi có code (§7) |
| Contract API | Chưa | Dự kiến ở §6; file `docs/03-api/` viết cùng commit với code |
| Đặc tả màn 3.3 và wireframe | Có | Không sửa trong đợt này; chỗ lệch ghi ở §2 để người phụ trách màn xử lý |

## 2. Chỗ lệch khỏi ticket và Report 3, cần ghi lên Jira

| # | Nguồn | Đặc tả này | Lý do |
|---|---|---|---|
| D1 | SCRUM-16: ghi lại "tải tài liệu" | Không ghi lượt tải tài liệu | ADR-0057 (PO 07/11/2026) đã quyết: chỉ ghi lượt xem tài liệu của cuộc họp Mật |
| D2 | SCRUM-16: "chỉ quản trị viên" · UC-13 PRE-1: "has permission to view the audit log" | Dùng lại quyền *Quản trị người dùng* (`AdminUsers`), không thêm quyền mới | Cùng cổng với Người dùng, Vai trò, Cấu hình AI. Xem Q2 |
| D3 | Report 3 màn 3.3: "Export CSV" | Không xuất CSV | Ticket và UC-13 không có. Tệp xuất mang bằng chứng ra khỏi hệ thống, phải thêm luật riêng (như BR-438). Xem Q3 |
| D4 | Report 3 màn 3.3: bộ lọc người thực hiện "lists users" | Thêm hai lựa chọn Khách và Hệ thống | Comment 31/10/2026 trên ticket: dòng của Khách phải lọc ra được và không hiện là "Hệ thống" |
| D5 | Report 3 màn 3.3: "Failed sign-in … 5 consecutive attempts" | Mỗi lần đăng nhập thất bại là một dòng; không có "5 lần liên tiếp" | Hệ thống không khoá tài khoản theo số lần sai. Chỉ áp dụng nếu Q1 được duyệt |
| D6 | UC-13 bước 3: "outcome where recorded" | Bỏ "outcome" | `audit_log` không có cột kết quả; mỗi dòng là một việc đã xảy ra |
| D7 | Report 3 màn 3.3: cột Đối tượng hiện tên đối tượng | Hiện loại và mã đối tượng; chỉ tài khoản và vai trò có tên. Không tra tiêu đề cuộc họp | Quản trị không có quyền đọc thêm cuộc họp (`MeetingReadPolicy`); nhật ký không được thành đường vòng để đọc tiêu đề cuộc họp Mật. Xem Q4 |

## 3. Use Case đề xuất

### UC-13 – View Audit Log (Xem nhật ký hệ thống) — SCRUM-16

Thuộc FE-03. Sequence Diagram: [UC13_ViewAuditLog.txt](../06-sequencediagram/role-management/UC13_ViewAuditLog.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | System Admin |
| **Secondary Actors** | N/A |
| **Description** | Allows the System Admin to review the recorded business and security events — who did what, to which object, when and from which IP address — for accountability and investigation. The audit log is read-only. (BR-440, BR-441) |
| **Preconditions** | ● PRE-1: The System Admin is signed in.<br>● PRE-2: The System Admin's role holds the User administration permission. (BR-440) |
| **Postconditions** | ● POST-1: The audit entries matching the chosen period and filters are displayed, newest first. (BR-442)<br>● POST-2: No audit entry is changed or deleted. (BR-440)<br>● POST-3: The System Admin gains no access to the Meetings, documents or minutes the entries are about. (BR-443) |
| **Normal Flow** | View Audit Log<br>1. The System Admin opens Nhật ký hệ thống [Audit Log] from the Administration menu.<br>2. The system checks that the System Admin's role holds the User administration permission. (BR-440)<br>3. The system retrieves the audit entries, newest first, one page at a time. (BR-442, BR-11)<br>4. The system displays, for each entry, the time, the actor, the event, the object and the IP address. An actor is shown as a user account, a Guest or the system. (BR-441)<br>5. The System Admin browses the pages; the chosen period and filters are kept across pages. |
| **Alternative Flows** | AF-1 – Filter the audit log<br>1. At Step 4 of the Main Flow, the System Admin chooses a period, an actor (a user account, Guests or the system), an event type, or enters a keyword.<br>2. The system retrieves the entries that match all the chosen criteria. (BR-31, BR-442)<br>3. The flow resumes at Step 4 of the Main Flow.<br>AF-2 – View the details of an entry<br>1. At Step 4 of the Main Flow, the System Admin selects an entry.<br>2. The system displays the details recorded with that entry, read-only — for a change, the values before and after it. (BR-441, BR-443)<br>3. The flow resumes at Step 5 of the Main Flow.<br>AF-3 – No entry matches<br>1. At Step 3 of the Main Flow, no audit entry matches the period and filters.<br>2. The system displays an empty list and a no-results message. (BR-12)<br>3. The System Admin may change the period or the filters; the flow resumes at Step 1 of AF-1. |
| **Exception Flows** | EX-1 – The period or a filter is not valid<br>1. At Step 3 of the Main Flow, the start of the period is after its end, or a filter value is not valid. (BR-442)<br>2. No entry is retrieved. The system displays MSG469 – “The audit log filter is invalid. Check the period and the selected values.”<br>3. The flow resumes at Step 1 of AF-1.<br>EX-2 – The audit log cannot be loaded<br>1. At Step 3 of the Main Flow, the system cannot read the audit entries.<br>2. No entry is displayed. The system displays MSG470 – “Unable to load the audit log. Please try again.”<br>3. The use case ends.<br>EX-3 – The permission is not held<br>1. At Step 2 of the Main Flow, the System Admin's role does not hold the User administration permission, or no longer holds it.<br>2. The system returns no audit entry and the Audit Log page is not shown.<br>3. The use case ends. |

## 4. Business Rules đề xuất

Dùng lại BR-11 (phân trang), BR-12 (không có kết quả), BR-31 (mọi tiêu chí lọc cùng phải khớp): thêm UC-13 vào cột
*Applies to* của ba luật đó.

| ID | Rule | Applies to | Nguồn |
|---|---|---|---|
| `BR-440` | Only a signed-in user whose role holds the User administration permission may view the audit log. The audit log is read-only: no function of the application changes or deletes an audit entry. | UC-13 | SCRUM-16 — "chỉ quản trị viên", "chỉ ghi thêm" |
| `BR-441` | Each audit entry shows when it happened, who acted, the event, the object acted on, the IP address of the request and the details recorded with it. The actor is a user account, a Guest — shown with the invited name and email — or the system when no person acted. | UC-13 | SCRUM-16; SCRUM-123 — dòng do Khách tạo |
| `BR-442` | Audit entries are listed newest first. They may be narrowed by a period, by actor (one user account, Guests, or the system), by event type and by a keyword matched against the object and the recorded details. A period is valid when its start is not after its end. | UC-13 | SCRUM-16 — màn 3.3 |
| `BR-443` | The audit log shows only what was recorded with each entry. It does not show the Meeting, document or minutes an entry is about, and viewing it gives the System Admin no access to them. | UC-13 | Quản trị không có quyền đọc thêm cuộc họp; ADR-0035 |

Ba luật dưới đây là phía **ghi** (Q1 đã chốt: làm trong SCRUM-16):

| ID | Rule | Applies to | Nguồn |
|---|---|---|---|
| `BR-444` | Each successful sign-in, and each failed sign-in to an existing account, is recorded in the audit log with the sign-in method and, for a failure, the reason. A password or a token is never recorded. A Guest's successful verification is recorded as well. | UC-01, UC-02, UC-04 | SCRUM-16 — "ghi lại đăng nhập" |
| `BR-445` | Each change of a Meeting's status, including a cancellation made by the system, is recorded in the audit log with who made it, the status before and after and the reason given, in addition to the Meeting's own history. | UC-18, UC-19, UC-20, UC-21, UC-22, UC-75 | SCRUM-16 — "chuyển trạng thái cuộc họp" |
| `BR-446` | Each issue of minutes, including a correction, is recorded in the audit log with who issued it. | UC-64 | SCRUM-16 — "ban hành biên bản" |

## 5. Mã thông báo

| Mã | HTTP dự kiến | Tình huống | `message` |
|---|---|---|---|
| `MSG469` | 400 | UC-13 EX-1: khoảng thời gian hoặc bộ lọc sai; kèm `errors` theo tham số | `Bộ lọc nhật ký không hợp lệ. Kiểm tra khoảng thời gian và các giá trị đã chọn.` |
| `MSG470` | 500 | UC-13 EX-2: không đọc được nhật ký | `Không tải được nhật ký hệ thống. Vui lòng thử lại.` |

Không có kết quả (AF-3) là câu của màn hình theo BR-12, không có mã — như UC-05. Thiếu quyền trả `403` không kèm mã,
như mọi endpoint sau cổng `AdminUsers`.

## 6. API dự kiến

Mọi endpoint nằm sau `RequireAdminUsers()`: `401` khi chưa đăng nhập, `403` khi vai trò không có quyền *Quản trị người
dùng* (endpoint cố định, không giấu bằng `404`). Route giao diện thiếu quyền vẫn hiện `404`.

### `GET /api/audit-log`

| Tham số | Kiểu | Ý nghĩa |
|---|---|---|
| `from`, `to` | ISO 8601 có múi giờ, tuỳ chọn | Khoảng `occurred_at`, tính cả hai đầu; `from` không sau `to`. Bỏ trống là không giới hạn |
| `actor` | Guid, `guest` hoặc `system`, tuỳ chọn | Một tài khoản; mọi dòng của Khách; mọi dòng hệ thống tự ghi |
| `action` | mã sự kiện, tuỳ chọn | Phải là một mã của `GET /api/audit-log/event-types` |
| `q` | chuỗi 1–100 ký tự, tuỳ chọn | Tìm không phân biệt hoa thường trong mã đối tượng và `details` (dán mã cuộc họp, tên tệp, email…) |
| `page`, `pageSize` | số nguyên | Mặc định `1` và `20`; `pageSize` tối đa `100` |

Tham số lạ, sai kiểu hoặc ngoài khoảng trả `400 MSG469` kèm `errors` theo tên tham số, như danh sách cuộc họp. Thứ tự:
`occurred_at` giảm dần, rồi `id` giảm dần.

```json
{
  "items": [
    {
      "id": "0192f3a1-…",
      "occurredAt": "2026-11-10T03:12:45.123+00:00",
      "actor": { "kind": "user", "id": "0192…", "name": "Nguyễn Văn A", "email": "a@example.com" },
      "action": "user.deactivated",
      "actionLabel": "Vô hiệu hoá tài khoản",
      "objectType": "User",
      "objectId": "0192…",
      "objectName": "Trần Thị B",
      "ipAddress": "203.0.113.7",
      "details": { "Email": "b@example.com", "IsActive": false }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1284,
  "totalPages": 65
}
```

- `actor.kind` là `user`, `guest` hoặc `system`. Với `guest`, `id` là mã lời mời, `name` và `email` lấy từ lời mời.
  Với `system`, ba trường còn lại là `null`.
- `objectName` chỉ có với `User` (họ tên) và `Role` (tên vai trò; `null` nếu vai trò đã xoá). Các loại khác là `null`.
- `details` trả nguyên như đã ghi. Không có endpoint chi tiết riêng: màn hình mở ngăn chi tiết từ chính dòng đã tải.
- `actionLabel` là tên tiếng Việt của sự kiện; mã chưa có tên thì trả chính mã đó.

### `GET /api/audit-log/event-types`

Danh sách cho ô lọc loại sự kiện: `[{ "code": "user.created", "label": "Tạo tài khoản", "group": "Tài khoản" }, …]`.
Tên nằm ở backend, cùng lý do với `PermissionCatalog`: một mã sự kiện mới không thể lên màn hình mà thiếu tên, và
Tester đọc đúng chữ Quản trị đọc. Phương án ít code hơn: FE giữ bảng tên, bỏ endpoint này — đổi lại bảng lệch dần
mỗi khi BE thêm mã.

## 7. Sequence và class diagram

- **Sequence UC-13** đã vẽ lại theo §3: một request, kiểm quyền (EX-3), kiểm bộ lọc (EX-1, `MSG469`), đọc trang
  (EX-2, `MSG470`), tra tên tài khoản và lời mời Khách, AF-3. Đã xuất `UC13_ViewAuditLog.png`, tab `uc13_viewauditlog`
  trong `TrustCabinet_Sequence_Diagrams.drawio` (128 tab còn lại không đổi) và `drawio-png/uc13_viewauditlog.drawio.png`.
- **Class diagram FE-03** chưa sửa. Bốn lớp UC-13 đang có khớp sequence mới về lifeline và operation chính
  (`getAuditEntries`, `findReadOnlyPage`). Việc để lại cho đợt as-built sau khi có code: `criteria` thành kiểu có tên
  thay cho `Map<string, string>`; `actorId`, `objectId` của `AuditLogEntry` là `UUID?`; thêm operation lấy danh mục loại
  sự kiện và tra tên người thực hiện; bỏ tham số `callerId` nếu code không dùng.

## 8. Việc còn lại

**Docs:** ~~đưa §3–§5 vào Report 3 DOCX~~ — xong 10/11/2026 (`check_docx.py` OK, `sync_reports.py` khớp 100% danh mục).

**BE** (không cần migration cho phía đọc):

1. `IAuditLogQueryService` trong `Domain/Audit`, cài đặt trong `Infrastructure/Audit`; `AuditLogEndpoints` và bộ đọc
   tham số trong `Web/Endpoints`, sau `RequireAdminUsers()`.
2. Danh mục tên sự kiện; đưa hai nhóm mã gõ tay (thành phần, điểm danh) về `AuditActions`; test bắt mã chưa có tên.
3. Nhận diện Khách lúc đọc bằng bốn khoá ở §1.1; từ nay chỗ ghi mới dùng một khoá `guestInvitationId`. Phương án
   chắc hơn: thêm cột `guest_invitation_id` và migration chuyển dữ liệu cũ — làm khi cần lọc Khách theo từng người.
4. Thêm `AuditLogEntry` vào chốt chặn chỉ-ghi-thêm của `TrustCabinetDbContext`.
5. Phía ghi (Q1): ghi `auth.signed_in`, `auth.sign_in_failed`, Khách xác minh; mỗi dòng `meeting_status_history`
   kèm một dòng audit trong cùng transaction; `minutes.issued`. Email không khớp tài khoản nào thì không ghi — người
   dùng có thể đã gõ nhầm mật khẩu vào ô email.
6. Test endpoint (quyền, lọc, phân trang, Khách và hệ thống), `docs/03-api/21-nhat-ky-kiem-toan.md`, chuyển
   `MSG469`–`MSG470` sang trạng thái BE trong danh mục.

**FE:** `features/audit/`, `api/auditLog.ts`, `types/auditLog.ts`, route `/mgmt/admin/audit-log` trong `routes.ts`,
mục sidebar dưới Quản trị (chỉ hiện khi có `AdminUsers`), theo đặc tả màn 3.3 trừ các điểm D3–D7.

Không làm: ghi lại việc *xem* nhật ký; chuỗi hash chống sửa (đã loại ở [02-erd.md](../02-design/02-erd.md)); chuyển
dữ liệu `meeting_status_history` cũ sang `audit_log`.

## 9. Câu hỏi cần chốt

**10/11/2026: chốt cả bốn theo cột Đề xuất.**

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q1 | Có bổ sung phía ghi cho ba loại sự kiện ticket yêu cầu mà code chưa ghi (đăng nhập, chuyển trạng thái cuộc họp, ban hành biên bản) ngay trong SCRUM-16 không? | Có — thiếu chúng thì câu "ai đã làm gì với cuộc họp mật và biên bản" của ticket không trả lời được. BR-444 → BR-446 |
| Q2 | Quyền xem nhật ký: dùng lại *Quản trị người dùng* hay thêm một quyền riêng trong ma trận? | Dùng lại. Quyền riêng chỉ cần khi có vai trò kiểm toán không quản trị tài khoản |
| Q3 | Có làm Xuất CSV như đặc tả màn 3.3 không? | Không trong SCRUM-16 |
| Q4 | `details` của sự kiện thuộc cuộc họp Mật (tên tệp đã xem, nội dung nháp biểu quyết bị xoá, thay đổi thành phần) có hiện nguyên văn cho Quản trị không? | Có: đó là bằng chứng và người xem là người được giao kiểm toán. Không tra thêm tiêu đề cuộc họp (BR-443). Nếu PO không đồng ý thì ẩn `details` của các dòng đó |

## 10. Comment trên Jira

Đã đăng lên SCRUM-16 ngày 10/11/2026: D1–D7 ở §2, kết quả Q1–Q4, BR-440 → BR-446, `MSG469`–`MSG470`, và việc tiêu chí
"tải tài liệu" được thay bằng ADR-0057.
