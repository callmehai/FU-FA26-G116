# 15 · Package Diagram

Package diagram của TrustCabinet, vẽ theo **code hiện có** trên `main` (09/11/2026, `0eae63bd`), không
phải thiết kế mục tiêu. Backend là ba project `Web` → `Infrastructure` → `Domain`, chưa có tầng
Application; lý do và kế hoạch tách nằm ở [ADR-0059](../05-decisions/0059-giu-ba-project-backend-tach-application-sau-bao-cao.md).
Phụ thuộc lấy từ chính source:

- backend và tests: `ProjectReference` trong `.csproj` và các dòng `using TrustCabinet.*` theo
  namespace (bỏ qua `bin/`, `obj/`, `Migrations/`);
- frontend: các `import` tương đối, kể cả `import()` của route lazy, gom theo thư mục cấp một của
  `frontend/src` (riêng `features/` tách theo từng feature).

Mỗi góc nhìn là một cặp file cùng tên: `.drawio` là source editable, `.png` xuất từ đúng source đó.

| Góc nhìn | Nội dung | Source / Ảnh |
|---|---|---|
| Tổng quan | `frontend/src` theo lớp, ba project backend, project test và phụ thuộc giữa chúng | [drawio](package-tong-quan.drawio) · [PNG](package-tong-quan.png) |
| Backend | Package con của `TrustCabinet.Web`, `.Infrastructure`, `.Domain`; class ở namespace gốc của Web và Infrastructure; mọi phụ thuộc trong Web, trong Infrastructure và từ Web sang Infrastructure | [drawio](package-backend.drawio) · [PNG](package-backend.png) |
| Domain | Phụ thuộc giữa các package nghiệp vụ trong `TrustCabinet.Domain` | [drawio](package-domain.drawio) · [PNG](package-domain.png) |
| Frontend | `frontend/src` với 19 feature trong `features/` | [drawio](package-frontend.drawio) · [PNG](package-frontend.png) |

## Quy ước

- Mũi tên nét đứt đầu mở là phụ thuộc UML. `«import»` = `ProjectReference`; `«use»` = dùng trực
  tiếp namespace của project khác mà không tham chiếu project đó (ví dụ Web → Domain đi qua
  Infrastructure); `«use» HTTP · SignalR` = `api/` gọi `/api` và `/hubs` của backend.
- Mũi tên không nhãn là phụ thuộc giữa các package trong cùng một project/thư mục.
- Mũi tên gắn vào khung một package bao gồm mọi package con bên trong.
- Package vẽ ngoài package chứa nó thì ghi tên đầy đủ `chứa::tên` (ví dụ `backend::TrustCabinet.Web`
  trên trang Frontend).
- Mũi tên kết thúc ở thân package, không cắm vào tab.

## Phụ thuộc không vẽ thành mũi tên

Mũi tên `«import»` giữa hai khung là `ProjectReference`, không thay cho phụ thuộc của từng package.
Những phụ thuộc dưới đây có trong code nhưng nếu vẽ thành mũi tên thì hình không còn đọc được, nên
liệt kê đủ ở đây.

**Trang Domain — phụ thuộc tới `Entities` và `Enums`:**

| Tới | Package dùng |
|---|---|
| `Entities` | Access, Agenda, Authentication, Calls, Documents, Meetings, Notes, Notifications, Participants, Recordings, Transcription, Users, VoiceId, Votes |
| `Enums` | Access, Agenda, Ai, Attendance, Authentication, Calls, Documents, Entities, GuestAccess, Meetings, Notes, Participants, Recordings, Users, Votes |

`Audit`, `Summaries` và `Enums` không phụ thuộc package nào.

**Trang Backend — Infrastructure → Domain** (ngoài `Entities`, `Enums`, mà gần như package nào cũng dùng):

| Package Infrastructure | Package Domain |
|---|---|
| Access | Access, Audit |
| Agenda | Agenda, GuestAccess, Meetings, Notes |
| Ai | Ai, Audit, Summaries |
| Attendance | Agenda, Attendance, Audit, GuestAccess, Meetings, Participants |
| Audit | Audit |
| Authentication | Audit, Authentication |
| Calls | Audit, Calls, Participants |
| Configurations | Attendance, Participants, Recordings, Summaries |
| Documents | Agenda, Audit, Documents, GuestAccess, Meetings, Participants |
| Email | Notifications, Participants, Users |
| GuestAccess | Agenda, Documents, GuestAccess, Meetings, Participants |
| Meetings | Access, Agenda, Calls, Meetings, Notifications, Participants |
| Notes | Meetings, Notes, Participants |
| Notifications | Meetings, Notifications |
| Participants | Agenda, Audit, GuestAccess, Meetings, Participants |
| Recordings | Audit, Meetings, Recordings, Transcription |
| Seed | Access, Documents |
| Summaries | Ai, Audit, Meetings, Notes, Participants, Summaries |
| Transcription | Audit, Meetings, Participants, Recordings, Transcription, VoiceId |
| Users | Access, Audit, Authentication, Users, VoiceId |
| VoiceId | Audit, Transcription, VoiceId |
| Votes | Agenda, Documents, GuestAccess, Meetings, Participants, Votes |

**Trang Backend — Web → Domain:**

| Web | Package Domain |
|---|---|
| `Endpoints` | mọi package trừ `Audit` |
| `AttendanceChangePublisher` | Agenda, Attendance, GuestAccess, Meetings, Participants |
| `CallRoomJanitor` | Calls |
| `CapabilitiesChangePublisher` | Access |
| `HttpCallerContext` | Audit |
| `MeetingGuestIdentity` | Agenda, Meetings |
| `MeetingHub` | Agenda, GuestAccess, Meetings |
| `MeetingOverdueWorker` | Meetings |
| `NotesChangePublisher` | Meetings, Notes |
| `NotificationHub` | Users |
| `NotificationWorker` | Notifications, Users |
| `ParticipantsChangePublisher` | Meetings, Participants |
| `PdfPageCounter` | Documents |
| `Program` | Agenda, Audit, Enums |
| `RecordingRetentionWorker` | Recordings |
| `SpeechChangePublisher` | Meetings, Recordings |
| `SummaryWorker` | Summaries |
| `TranscriptionWorker` | Transcription |
| `VoteChangePublisher` | Agenda, Meetings, Participants, Votes |

**Trang Backend — trong Infrastructure và từ `Program`:**

- `Program` dùng `DependencyInjection`, `TrustCabinetDbContext` và 18 package Infrastructure: Access,
  Agenda, Attendance, Authentication, Calls, Documents, Email, GuestAccess, Meetings, Notes,
  Notifications, Participants, Recordings, Summaries, Transcription, Users, VoiceId, Votes.
- Mọi package nghiệp vụ trong Infrastructure (trừ `Configurations`) dùng `TrustCabinetDbContext`, cùng
  `DependencyInjection` và `TrustCabinetDbContextFactory`. `TrustCabinetDbContext` áp mọi lớp trong
  `Configurations`; `Migrations` sinh từ nó.

**Trang Frontend** không vẽ phụ thuộc giữa các feature với nhau (`meetings`, `calls` dùng gần như mọi
feature khác; mọi feature dùng `auth`). `main.tsx` và `styles/` (CSS) không có trên hình.

Thư viện ngoài (NuGet, npm) không có trên hình; danh sách ở các `.csproj` và `frontend/package.json`.

## Sửa và xuất lại

Sửa trực tiếp trên `.drawio` theo [quy tắc chỉnh draw.io](../../DRAWIO_EDITING_RULES.md) và
[drawio-skill](../../.agents/skills/drawio-skill/SKILL.md). Khi thêm hoặc bỏ project, namespace hay
thư mục `features/` thì cập nhật hình và mục "Phụ thuộc không vẽ thành mũi tên" ở trên. Xuất lại PNG của
đúng file vừa sửa:

```bash
draw.io -x -f png -s 2 -o package-backend.png package-backend.drawio
```

Bản trên Drive nằm ở thư mục `Package diagram`, được tải tay lên; `scripts/push_diagrams.py` chưa
có nhóm cho thư mục này (xem [DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md)).
