# 15 · Package Diagram

Package diagram của TrustCabinet, vẽ theo **code hiện có** trên `refactor/clean-architecture` (17/11/2026, `e2c23dd0`),
không phải thiết kế mục tiêu. Backend là bốn project: `Web` và `Infrastructure` cùng tham chiếu `Application`,
`Application` tham chiếu `Domain` ([ADR-0064](../05-decisions/0064-backend-bon-project-clean-architecture.md), quy
chuẩn ở [11-quy-chuan-clean-architecture.md](../02-design/11-quy-chuan-clean-architecture.md)). Bản ba project trước đó
(09/11/2026, `0eae63bd`, [ADR-0059](../05-decisions/0059-giu-ba-project-backend-tach-application-sau-bao-cao.md)) nằm
trong lịch sử git. Phụ thuộc lấy từ chính source:

- backend và tests: `ProjectReference` trong `.csproj` và các dòng `using TrustCabinet.*` theo
  namespace (bỏ qua `bin/`, `obj/`, `Migrations/`);
- frontend: các `import` tương đối, kể cả `import()` của route lazy, gom theo thư mục cấp một của
  `frontend/src` (riêng `features/` tách theo từng feature).

Mỗi góc nhìn là một cặp file cùng tên: `.drawio` là source editable, `.png` xuất từ đúng source đó.

| Góc nhìn | Nội dung | Source / Ảnh |
|---|---|---|
| Tổng quan | `frontend/src` theo lớp, bốn project backend, hai project test và phụ thuộc giữa chúng | [drawio](package-tong-quan.drawio) · [PNG](package-tong-quan.png) |
| Backend | Package con của `TrustCabinet.Web`, `.Application`, `.Infrastructure`, `.Domain`; class ở namespace gốc của Web, Application và Infrastructure; phụ thuộc trong Web, trong Infrastructure, `«import»` giữa bốn project | [drawio](package-backend.drawio) · [PNG](package-backend.png) |
| Application | Phụ thuộc giữa các module trong `TrustCabinet.Application` | [drawio](package-application.drawio) · [PNG](package-application.png) |
| Domain | Phụ thuộc giữa các package nghiệp vụ trong `TrustCabinet.Domain` | [drawio](package-domain.drawio) · [PNG](package-domain.png) |
| Frontend | `frontend/src` với 24 feature trong `features/` | [drawio](package-frontend.drawio) · [PNG](package-frontend.png) |

## Quy ước

- Mũi tên nét đứt đầu mở là phụ thuộc UML. `«import»` = `ProjectReference`; `«use»` = dùng trực
  tiếp namespace của project khác mà không tham chiếu project đó (ví dụ Web → Domain đi qua
  Application); `«use» HTTP · SignalR` = `api/` gọi `/api` và `/hubs` của backend.
- Mũi tên không nhãn là phụ thuộc giữa các package trong cùng một project/thư mục.
- Mũi tên gắn vào khung một package bao gồm mọi package con bên trong.
- Package vẽ ngoài package chứa nó thì ghi tên đầy đủ `chứa::tên` (ví dụ `backend::TrustCabinet.Web`
  trên trang Frontend).
- Mũi tên kết thúc ở thân package, không cắm vào tab.
- Hai mũi tên phải cắt nhau thì chỗ cắt vẽ nét nhảy (cung), để không đọc nhầm thành rẽ nhánh: trang Application
  (3 chỗ) và trang Tổng quan (2 chỗ, ở các mũi tên từ project test).

## Phụ thuộc không vẽ thành mũi tên

Mũi tên `«import»` giữa hai khung là `ProjectReference`, không thay cho phụ thuộc của từng package.
Những phụ thuộc dưới đây có trong code nhưng nếu vẽ thành mũi tên thì hình không còn đọc được, nên
liệt kê đủ ở đây.

**Trang Domain — phụ thuộc tới `Entities` và `Enums`:**

| Tới | Package dùng |
|---|---|
| `Entities` | Agenda, CalendarSharing, Documents, Meetings, Participants, Recordings, Transcription, Votes |
| `Enums` | Access, Agenda, Attendance, Audit, CalendarSharing, Documents, Entities, GuestAccess, Meetings, Participants, Recordings, Votes |

`Calls`, `Summaries`, `VoiceId` và `Enums` không phụ thuộc package nào. `Entities` chứa `Common` (lớp gốc `Entity`).
Ai, Authentication, Notes, Notifications và Users không còn package Domain: chúng chỉ có interface và DTO, nay ở
Application (CA-02).

**Trang Application — phụ thuộc tới năm module dùng chung:**

| Tới | Module dùng |
|---|---|
| `Abstractions` | cả 23 module |
| `Audit` | Access, Ai, Attendance, Authentication, CalendarSharing, Calls, Documents, GuestAccess, Participants, Recordings, Reports, Summaries, Transcription, Users, VoiceId, Votes |
| `Meetings` | Agenda, CalendarSharing, Documents, Notes, Notifications, Participants, Recordings, Summaries, Transcription |
| `Participants` | Agenda, Attendance, Calls, Documents, Email, Meetings, Speaking, Votes |
| `Notifications` | Agenda, CalendarSharing, Email, Meetings, Summaries, VoiceId |

Vì vậy `Abstractions`, `Attendance`, `Audit`, `Notifications`, `Reports` đứng riêng trên hình. Class
`DependencyInjection` (`AddApplication()`) ở namespace gốc dùng cả 23 module.

**Trang Backend — Application → Domain** (ngoài `Entities`, `Enums`):

| Module Application | Package Domain |
|---|---|
| Access | Access, Audit |
| Agenda | Agenda, GuestAccess, Meetings |
| Ai, Authentication, Users | Audit |
| Attendance | Agenda, Attendance, Audit, GuestAccess, Meetings, Participants |
| Audit | Audit |
| CalendarSharing | Audit, CalendarSharing, Meetings, Participants |
| Calls | Audit, Calls, Meetings |
| Documents | Audit, Documents, GuestAccess, Meetings, Participants |
| Email | Meetings, Participants |
| GuestAccess | Audit, Documents, Meetings |
| Meetings | Access, Agenda, GuestAccess, Meetings, Participants |
| Notes | Meetings, Participants |
| Participants | Agenda, Audit, GuestAccess, Meetings, Participants |
| Recordings | Audit, Meetings, Participants, Recordings, Transcription |
| Reports | Audit, Meetings |
| Speaking | Agenda, GuestAccess, Meetings, Participants |
| Summaries | Audit, Meetings, Participants, Summaries |
| Transcription | Audit, Participants, Transcription, VoiceId |
| VoiceId | Audit, Participants, VoiceId |
| Votes | Agenda, Audit, Documents, GuestAccess, Meetings, Votes |

`Abstractions` và `Notifications` không dùng package Domain nào ngoài `Entities`, `Enums`.

**Trang Backend — Infrastructure → Application:** mỗi package cài port của module cùng tên, cộng thêm:

| Package Infrastructure | Module Application |
|---|---|
| Agenda | Agenda, Notes |
| Ai, Summaries | Ai, Summaries |
| Documents | Abstractions, Documents, Meetings |
| Email | Email, Notifications, Users |
| GuestAccess | Agenda, Email, GuestAccess, Participants |
| Persistence | Abstractions (`IUnitOfWork`, exception lưu trữ) |
| Speaking, Votes | Participants và module cùng tên |
| Transcription | Transcription, VoiceId |
| Users | Audit, Users |
| `DependencyInjection` | Abstractions |
| `TrustCabinetDbContext` | Audit |

`Configurations`, `Migrations`, `Seed`, `TrustCabinetDbContextFactory` không dùng Application.

**Trang Backend — Infrastructure → Domain** (ngoài `Entities`, `Enums`):

| Package Infrastructure | Package Domain |
|---|---|
| Access | Access |
| Agenda | Agenda |
| Audit, Recordings, `TrustCabinetDbContext` | Audit |
| CalendarSharing | CalendarSharing, Participants |
| Calls | Agenda, Participants |
| Configurations | Attendance, Participants, Recordings, Summaries |
| Documents | Documents, Meetings, Participants |
| Email | Participants |
| GuestAccess | GuestAccess, Meetings |
| Meetings, Reports | Meetings, Participants |
| Participants | Agenda, Participants |
| Seed | Access, Documents |
| Summaries | Audit, Participants |
| Votes | Votes |

**Trang Backend — Web → Application và Web → Domain:**

| Web | Module Application | Package Domain |
|---|---|---|
| `Endpoints` | cả 23 module trừ `Abstractions` | Access, Agenda, Attendance, Audit, CalendarSharing, Documents, Entities, Enums, Meetings, Participants, Recordings, Transcription, VoiceId, Votes |
| `AttendanceChangePublisher` | Attendance, Meetings | — |
| `CallRoomJanitor` | Calls | — |
| `CapabilitiesChangePublisher` | Access | — |
| `CombinedRecordingWorker`, `RecordingRetentionWorker` | Recordings | — |
| `HttpCallerContext` | Audit | — |
| `MeetingGuestIdentity` | Agenda, GuestAccess | Meetings |
| `MeetingHub` | Agenda, Meetings | Meetings |
| `MeetingOverdueWorker` | Meetings | — |
| `NotesChangePublisher` | Meetings, Notes | — |
| `NotificationHub` | Users | — |
| `NotificationWorker` | Notifications, Users | — |
| `ParticipantsChangePublisher` | Participants | Meetings |
| `PdfPageCounter` | — | Documents |
| `Program` | `DependencyInjection` (gốc), Abstractions, Agenda, Audit | — |
| `SpeakingChangePublisher` | Agenda, Meetings, Speaking | — |
| `SpeechChangePublisher` | Meetings, Recordings | — |
| `SummaryWorker` | Summaries | — |
| `TranscriptionWorker` | Transcription | — |
| `VoteChangePublisher` | Meetings, Votes | — |

**Trang Backend — Web → Infrastructure:** chỉ hai class, đúng luật R1/A6 của quy chuẩn. `Program` dùng
`DependencyInjection` (`AddTrustCabinetDatabase()`) và hai package Authentication, Documents; `DatabaseMigrationCommand`
dùng `DependencyInjection` (`MigrateForDeploymentAsync`, mũi tên duy nhất vẽ từ Web sang Infrastructure).

**Trang Backend — trong Infrastructure:** `DependencyInjection` dùng mọi package trừ Ai, Configurations, Migrations
(chỉ vẽ mũi tên tới `Seed`). Mọi package nghiệp vụ dùng `TrustCabinetDbContext` ở namespace cha; `TrustCabinetDbContext`
áp mọi lớp trong `Configurations`; `Migrations` sinh từ nó.

**Trang Tổng quan:** `TrustCabinet.Web.Tests` tham chiếu Web và dùng namespace của ba project còn lại;
`TrustCabinet.Application.Tests` tham chiếu Application và Domain (hai `ProjectReference`).

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

Bản trên Drive nằm ở thư mục `Package diagram`; đẩy lên bằng `python scripts/push_diagrams.py package --confirm`
(xem [DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md)).
