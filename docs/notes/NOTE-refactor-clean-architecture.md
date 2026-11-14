# Refactor backend sang Clean Architecture

- **Ngày:** 08/11/2026; **cập nhật 14/11/2026**: đo lại số liệu, thêm bảng lớp đang đọc `DbContext` (§3.1),
  bổ sung 3 module lộ trình cũ bỏ sót (CalendarSharing, Speaking, Reports) vào giai đoạn 2
- **Trạng thái:** đã chốt thời điểm ở [ADR-0059](../05-decisions/0059-giu-ba-project-backend-tach-application-sau-bao-cao.md)
  (09/11/2026): giữ ba project đến buổi báo cáo giảng viên 13/11/2026, bắt đầu giai đoạn 0 ngay sau đó.
  Tạo ticket Jira cho từng giai đoạn khi bắt đầu.
- **Phạm vi:** chỉ `backend/` và `tests/`. Không đổi endpoint, response, mã `MSGnn`, schema hay migration.
  FE không bị ảnh hưởng.

## 1. Vì sao phải refactor

Backend hiện có ba project: `Web` → `Infrastructure` → `Domain`. Không có tầng Application. Vì vậy logic
nghiệp vụ (use case) đang nằm trong Infrastructure và dính chặt vào EF Core/PostgreSQL.

Số liệu đo lại trên nhánh `docs/guest-e2e-docs` (`2620854b`) ngày 14/11/2026 bằng `grep`, nên là số xấp xỉ;
số trong ngoặc là lần đo đầu trên nhánh `docs/dac-ta-so-bo-scrum-137-142` ngày 08/11/2026:

| Vấn đề | Bằng chứng |
|---|---|
| Use case nằm trong Infrastructure | 12.188 dòng (9.800) trong 35 file `*Service.cs` ở `TrustCabinet.Infrastructure/`; lớn nhất: `ParticipantService` 1.116 dòng, `MeetingService` 868, `UserService` 792, `SpeechClipService` 674, `VoteService` 552, `TranscriptionService` 511, `CalendarSharingService` 506 |
| Lớp gọi thẳng `DbContext` | 39 lớp trong Infrastructure (12 service được liệt kê lúc đầu) ngoài repository, seed và `DbContext` nhận `TrustCabinetDbContext` qua constructor, tham số method static hoặc `GetRequiredService`; khoảng 380 lời gọi `db.` cần chuyển vào repository hoặc query port. Danh sách đầy đủ ở §3.1 |
| Service biết chi tiết của Postgres | 47 file ngoài repository/seed/migration vẫn `using Npgsql` hoặc `Microsoft.EntityFrameworkCore`, bắt `DbUpdateException`/`PostgresException` theo `SqlState` (`23505`, `23503`), so tên unique index (`"ix_voting_sessions_meeting_id"`) |
| Domain chứa thứ của Application | Domain có 86 interface (73) gồm service, repository và port, và 218 record DTO (194) trong `*Contracts.cs`. Interface repository để lộ cơ chế lưu trữ: `SaveChangesAsync`, `InTransactionAsync`, `FindOwnedForUpdateAsync` |
| Domain phụ thuộc thư viện ngoài | `TrustCabinet.Domain.csproj` reference `PdfPig` (dùng trong `Documents/DocumentFileRules.cs`) — **đã sửa ở SCRUM-144** (`0eae63bd`): đọc PDF qua `IPdfInspector`, `DomainDependencyTests` chặn package quay lại |
| Web đi tắt qua Application | 7 publisher dùng thẳng `TrustCabinetDbContext`: `AgendaChangePublisher`, `MeetingDocumentChangePublisher` (cùng nằm trong `MeetingHub.cs`), `VoteChangePublisher`, `AttendanceChangePublisher`, `NotesChangePublisher`, `SpeechChangePublisher`, `SpeakingChangePublisher`; `/api/health` gọi `db.Database.CanConnectAsync`. Repository còn bị inject ở `MeetingHub` (`IMeetingRepository`), `NotificationHub`, `NotificationWorker`, `AuthEndpoints` và `CapabilityGate` (`IUserAccountRepository`). Endpoint trong `Endpoints/` đã thôi inject `IMeetingRepository` (lần đo đầu: 8 chỗ) |

Hệ quả:

- Không unit test được nghiệp vụ mà không có Postgres.
- Đổi cách lưu trữ là phải sửa logic nghiệp vụ.
- Hội đồng hỏi "kiến trúc gì" thì không trả lời được là Clean.

[03-co-so-du-lieu.md](../02-design/03-co-so-du-lieu.md) đã dự tính tách `TrustCabinet.Application`, nhưng chưa làm.

## 2. Kiến trúc đích

```
backend/
  TrustCabinet.Domain/          entity, enum, value object, rule và policy thuần
                                không reference project hay package nào
  TrustCabinet.Application/     use case service, DTO, port (interface) cho mọi thứ bên ngoài
                                chỉ reference Domain
  TrustCabinet.Infrastructure/  EF Core, migration, seed, repository, unit of work,
                                Redis, LiveKit, S3/R2, email, LLM, speech worker, PDF/DOCX
                                reference Application + Domain
  TrustCabinet.Web/             Minimal API endpoint, SignalR hub và publisher, hosted worker,
                                composition root
                                reference Application; Infrastructure chỉ dùng trong Program.cs để đăng ký DI
```

Chiều phụ thuộc luôn hướng vào trong: `Web` → `Application` → `Domain`, và `Infrastructure` → `Application` → `Domain`.

### 2.1 Quy tắc của từng tầng

**Domain**

- Chỉ chứa entity, enum, value object, rule và policy thuần: `MeetingRules`, `VoteRules`, `MeetingReadPolicy`,
  `MeetingSubmissionReadinessRules`…
- Không có `PackageReference` nào. Bỏ `PdfPig`; phần đọc PDF chuyển ra Infrastructure qua port `IPdfInspector`.
- Không chứa interface repository/service, không chứa DTO request/response.

**Application**

- Chứa use case: `MeetingService`, `VoteService`… chuyển từ Infrastructure sang, cùng các `I*Service` và
  `*Contracts.cs` chuyển từ Domain sang.
- Chỉ được dùng BCL (`TimeProvider`, `System.*`) và các gói `Microsoft.Extensions.*.Abstractions`
  (`ILogger`, `IOptions`). **Cấm** `Microsoft.EntityFrameworkCore*`, `Npgsql`, `StackExchange.Redis`,
  `AWSSDK.*`, `MailKit`, `Livekit.*`, `QuestPDF`, `Microsoft.AspNetCore.*`.
- Application **sở hữu** port: interface repository, unit of work, lưu trữ file, email, LLM, speech, publisher
  realtime. Nó chỉ biết interface, không biết lớp cài đặt, không biết bảng, index, SQL, `SqlState`,
  hay việc lưu trữ là Postgres.
- Port nói bằng ngôn ngữ nghiệp vụ:
  - không `IQueryable`;
  - không `SaveChangesAsync`;
  - không `DbSet`;
  - không entity tracking;
  - không tên index.

**Infrastructure**

- Cài đặt port: repository, `EfUnitOfWork` (execution strategy, isolation level, retry), dịch exception.
- Đây là nơi duy nhất biết tên index, `SqlState`, `FOR UPDATE`, `DbUpdateConcurrencyException`.
- Không chứa use case. Một lớp ở đây có `if` nghiệp vụ (ai được làm gì, trạng thái nào được chuyển) là sai tầng.

**Web**

- Endpoint chỉ làm việc với HTTP: đọc request, lấy caller, gọi một service Application, map kết quả ra HTTP và `MSGnn`.
- Hub, publisher, worker gọi Application service hoặc port, **không** dùng `TrustCabinetDbContext`, không inject repository.

### 2.2 Ba port nền tảng thay cho EF trong Application

**Unit of work** thay cho `IMeetingRepository.InTransactionAsync` / `SaveChangesAsync` rải trên từng repository:

```csharp
// Application/Abstractions/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
        TransactionMode mode = TransactionMode.Default, CancellationToken ct = default);
    Task CommitChangesAsync(CancellationToken ct = default);
}

public enum TransactionMode { Default, Serializable }
```

Infrastructure cài đặt bằng `Database.CreateExecutionStrategy()` và `BeginTransactionAsync(IsolationLevel…)`,
giữ đúng hành vi retry và isolation đang có.

**Exception lưu trữ** thay cho việc bắt `DbUpdateException`/`PostgresException` trong service:

```csharp
// Application/Abstractions/PersistenceExceptions.cs
public sealed class UniqueConflictException(UniqueConflict conflict) : Exception { public UniqueConflict Conflict { get; } = conflict; }
public sealed class ReferenceMissingException : Exception { }      // 23503
public sealed class ConcurrencyConflictException : Exception { }   // DbUpdateConcurrencyException
public sealed class PersistenceUnavailableException : Exception { } // NpgsqlException, timeout, transient

public enum UniqueConflict { OpenVoteInMeeting, BallotAlreadyCast, DocumentAlreadyConfirmed, EmailTaken, RoleNameTaken /* … */ }
```

Infrastructure có một `PersistenceExceptionTranslator` duy nhất, tra bảng `tên index → UniqueConflict`.
Service chỉ còn `catch (UniqueConflictException { Conflict: UniqueConflict.BallotAlreadyCast })`.

**Read model cho truy vấn danh sách:** những truy vấn nặng (danh sách cuộc họp có lọc và phân trang,
lịch sử trạng thái, danh sách thông báo) dùng port query riêng, ví dụ `IMeetingListQuery`, trả DTO
thẳng từ Infrastructure. Lọc theo quyền vẫn làm server-side trong query, đúng quy tắc của AGENTS.md.

### 2.3 Trước và sau, ví dụ `VoteService`

Trước, nằm trong `Infrastructure/Votes/VoteService.cs`:

```csharp
public sealed class VoteService(TrustCabinetDbContext db, VoteRepository votes, ...) : IVoteService
{
    private const string OpenVoteIndex = "ix_voting_sessions_meeting_id";
    private static bool Persistence(Exception ex) =>
        ex is DbUpdateException or NpgsqlException or TimeoutException ...;
```

Sau, nằm trong `Application/Votes/VoteService.cs`:

```csharp
public sealed class VoteService(IVoteRepository votes, IUnitOfWork unitOfWork, IParticipantRepository participants,
    IParticipantGuestAccess guests, IMeetingDocumentRepository documents, IStoredFileWriter writer,
    IVoteChangePublisher publisher, TimeProvider clock, ILogger<VoteService> logger) : IVoteService
{
    // catch (UniqueConflictException { Conflict: UniqueConflict.OpenVoteInMeeting }) => MSG… như cũ
```

`IVoteRepository` khai báo trong `Application/Votes/`. Lớp cài đặt `EfVoteRepository` nằm trong
`Infrastructure/Persistence/Votes/`.

## 3. Phân loại file hiện có

| Hiện nằm ở | Chuyển tới |
|---|---|
| `Domain/Entities`, `Domain/Enums`, `*Rules.cs`, `*Policy.cs` | giữ ở Domain |
| `Domain/**/I*Service.cs`, `Domain/**/*Contracts.cs` | `Application/<Module>/` |
| `Domain/**/I*Repository.cs` | `Application/<Module>/`, bỏ `SaveChangesAsync`/`InTransactionAsync`, đổi tên các method đang lộ cơ chế lưu trữ |
| `Domain/**/IFileStorage`, `ISpeechEngine`, `ILanguageModel`, `ITokenIssuer`, `IPasswordHasher`, `I*Publisher`… | `Application/Abstractions/` hoặc `Application/<Module>/` |
| `Domain/Documents/DocumentFileRules.cs` (phần dùng PdfPig) | rule giữ ở Domain; phần đọc PDF chuyển vào `Infrastructure/Documents/PdfPigInspector.cs` qua port `IPdfInspector` |
| `Infrastructure/**/*Service.cs` (use case) | `Application/<Module>/` |
| `Infrastructure/**/*Repository.cs`, `Configurations/`, `Migrations/`, `Seed/`, `TrustCabinetDbContext*` | giữ ở Infrastructure, gom vào `Infrastructure/Persistence/` |
| `Infrastructure/Authentication/Redis*Store`, `JwtTokenIssuer`, `GoogleTokenVerifier`, `PasswordHasher` | giữ ở Infrastructure; `AuthenticationService` chuyển sang Application |
| `Infrastructure/Email/*Sender`, `ResendWebhookReceiver`, `EmailQueueProcessor` | sender/receiver giữ ở Infrastructure; quyết định gửi cho ai, khi nào (`MeetingInvitationDispatchProcessor`, `MeetingInvitationEvents`) tách phần nghiệp vụ sang Application |
| `Infrastructure/Summaries/MinutesPdf`, `MinutesDocx` (QuestPDF, AngleSharp) | giữ ở Infrastructure sau port `IMinutesRenderer` |
| `Infrastructure/Summaries/MinutesTemplate`, `SpokenNumbers`, `MinutesLayout`, `SummaryWriter` | không dùng package ngoài thì chuyển sang Application, có dùng thì giữ ở Infrastructure; xác minh khi làm module |
| `Infrastructure/Calls/LiveKitGateway`, `Recordings/RecordingStorage`, `Transcription/SpeechWorkerClient`, `VoiceId/VoiceprintProtector`, `Ai/AiKeyProtector` | giữ ở Infrastructure; `CallRoomService`, `RecordingService`, `RoomTakeService`, `TranscriptionService`, `VoiceIdService`, `AiSettingsService` chuyển sang Application |
| `Infrastructure/**/*ServiceCollectionExtensions.cs` | tách thành `Application/DependencyInjection.cs` (`AddApplication()`) và `Infrastructure/DependencyInjection.cs` (`AddInfrastructure()`) |
| `Web/MeetingHub.cs`, `Web/*ChangePublisher.cs` dùng `DbContext` | tính nhóm người nhận qua một port Application (`IMeetingAudienceQuery`); publisher chỉ còn gửi SignalR |

### 3.1 Lớp đang đọc `TrustCabinetDbContext` ngoài tầng lưu trữ

Đo ngày 14/11/2026. Không tính repository, `TrustCabinetDbContext`, `TrustCabinetDbContextFactory`,
`DependencyInjection` (migrate và seed lúc khởi động) và `Seed/`. Cột `db.` là số dòng gọi `db.` trong file, chỉ để
ước độ nặng. Mỗi lớp ở Infrastructure, khi tới module của nó, hoặc chuyển phần nghiệp vụ sang Application, hoặc ở
lại Infrastructure dưới dạng cài đặt một port (repository, query port, adapter) theo bảng §3. Các lớp ở Web là danh
sách ngoại lệ ban đầu của `ArchitectureTests` cho quy tắc "không đọc `DbContext` ngoài Infrastructure".

| Module | Lớp | `db.` | Giai đoạn |
|---|---|---|---|
| Audit | `AuditService`, `AuditLogQueryService` | 2, 4 | 2.1 |
| Access | `AdministratorInvariantGuard` | 3 | 2.2 |
| CalendarSharing | `CalendarSharingService`, `SharedCalendarService`, `MeetingSlotService`, `CalendarBusyReader`, `CalendarShareCloser`, `CalendarShareStopper`, `CalendarSharePeople` (static) | 26, 3, 4, 1, 1, 1, 4 | **2.4 (mới)** |
| Agenda | `AgendaMeetingEnd` (nằm trong `AgendaRepository.cs`), `AgendaPresenterNotifier` (lấy `DbContext` bằng `GetRequiredService`) | 1, 10 | 2.5 |
| Participants | `ParticipantService` | 27 | 2.5 |
| Attendance | `AttendanceService` | 8 | 2.5 |
| GuestAccess | `GuestAccessService`, `GuestSessionAccess` | 17, 1 | 2.5 |
| Reports | `ReportService` | 4 | **2.6 (mới)** |
| Documents | `MeetingDocumentService`, `DocumentCommentService`, `PersonalLibraryService`, `StoredFileWriter`, `OrphanFilePurger` | 23, 8, 30, 7, 4 | 2.7 |
| Notes | `MeetingNoteService` | 9 | 2.7 |
| Votes | `VoteService`, `VoteMeetingEnd` | 27, 4 | 2.8 |
| Speaking | `SpeakingService`, `SpeakingMeetingEnd` | 21 (cả file) | **2.9 (mới)** |
| Recordings | `CombinedRecordingService`, `SpeechClipService` | 11, 18 | 2.9 |
| VoiceId | `VoiceCollectionService` | 9 | 2.9 |
| Ai | `AiSettingsService`, `LanguageModelProvider` | 5, 1 | 2.10 |
| Summaries | `SummaryService`, `MinutesService`, `MeetingTranscriptReader` | 25, 23, 5 | 2.10 |
| Email | `EmailQueueProcessor`, `MeetingInvitationDispatchProcessor`, `ResendWebhookReceiver`, `MeetingInvitationEvents` (static) | 7, 15, 1, 2 | 2.11 |
| Web | `AgendaChangePublisher`, `MeetingDocumentChangePublisher`, `NotesChangePublisher`, `SpeechChangePublisher`, `SpeakingChangePublisher`, `AttendanceChangePublisher`, `VoteChangePublisher`; endpoint `/api/health` | 0–1 mỗi lớp | 3 |

Mười service vừa dùng repository vừa gọi `db.`: `VoteService`, `SummaryService`, `MinutesService`,
`MeetingDocumentService`, `SpeakingService`, `SpeechClipService`, `MeetingNoteService`, `VoiceCollectionService`,
`AttendanceService`, `DocumentCommentService`. Phần gọi `db.` ở đó chủ yếu là transaction và isolation level,
`ChangeTracker.Clear()` khi retry, khoá dòng bằng `FOR SHARE`/`FOR UPDATE`, và truy vấn đọc dùng một lần trên nhiều
bảng. Ba việc đầu chuyển sang `IUnitOfWork` và method repository có tên nghiệp vụ (mẫu sẵn có:
`VoteRepository.LockMeetingAsync`), việc cuối chuyển sang query port.

`MinutesService` ghi thẳng `db.Notifications` và `db.AuditLog`, `CalendarSharingService` ghi thẳng
`db.Notifications`, dù đã có `NotificationRepository` và `AuditService`. Khi chuyển module, kiểm tra việc đó có cố ý để ghi trong cùng một
`SaveChanges` hay không; nếu có thì `IUnitOfWork` phải giữ được điều đó.

## 4. Kế hoạch theo giai đoạn

Mỗi giai đoạn là một hoặc vài PR nhỏ. Sau mỗi PR, `main` phải build được, test xanh, chạy được.
Không làm big-bang: các feature SCRUM-33/37 vẫn đang vào song song.

**Giai đoạn 0: khung và rào chắn** (1 PR)

1. Viết ADR-0059 ghi nhận quyết định Clean Architecture (đã có, 09/11/2026, trạng thái *Đề xuất*).
2. Tạo `TrustCabinet.Application`, thêm vào `TrustCabinet.sln`. `Infrastructure` và `Web` reference nó.
3. Thêm `IUnitOfWork`, `TransactionMode`, các exception lưu trữ và enum `UniqueConflict`. Infrastructure cài
   `EfUnitOfWork` và `PersistenceExceptionTranslator`.
4. Thêm `ArchitectureTests` vào `tests/TrustCabinet.Web.Tests`, dùng reflection
   (`Assembly.GetReferencedAssemblies()` và quét kiểu tham số constructor), không thêm package. Test kiểm tra:
   - Domain không reference assembly nào ngoài BCL;
   - Application không reference Infrastructure, EF Core, Npgsql, ASP.NET Core hay các SDK ngoài;
   - không lớp nào ngoài Infrastructure nhận `TrustCabinetDbContext` trong constructor **hoặc tham số method**,
     kể cả method do compiler sinh cho lambda endpoint (để bắt `/api/health`) và helper static như
     `AgendaChangePublisher.ReaderGroupsAsync`;
   - không lớp nào trong Infrastructure cài đặt `I*Service`.

   Lúc đầu test có **danh sách ngoại lệ** gồm các lớp hiện tại. Danh sách chỉ được rút ngắn, không được thêm;
   đến giai đoạn 4 thì phải rỗng.

**Giai đoạn 1: chuyển contract** (1 PR, thay đổi cơ học)

- Chuyển `I*Service`, `*Contracts.cs`, interface repository và port từ Domain sang Application, đổi
  namespace `TrustCabinet.Domain.X` thành `TrustCabinet.Application.X`.
- Bỏ PdfPig khỏi Domain (`IPdfInspector`).
- Sau PR này, Domain chỉ còn entity, enum, rule.

**Giai đoạn 2: chuyển use case theo module** (mỗi module một PR, từ nhỏ đến lớn, ít phụ thuộc trước)

1. Notifications, Audit
2. Authentication, Access (Role, RolePermission, `AdministratorInvariantGuard`), Users
3. Meetings (`MeetingService`, `MeetingLifecycleService`, `MeetingOverdueService`, kèm `IMeetingListQuery`)
4. **CalendarSharing** (thêm 14/11/2026): `CalendarSharingService`, `SharedCalendarService`, `MeetingSlotService`,
   `CalendarBusyReader`, `CalendarShareCloser`, `CalendarShareStopper`, `CalendarSharePeople`. Đặt sau Meetings vì
   tìm giờ họp đọc lịch bận từ cuộc họp và tạo nháp cuộc họp từ khung giờ; dùng Notifications/Audit đã chuyển ở bước 1.
   `CalendarSharePeople.LockAndLoadAsync` và `LockAsync` khoá dòng, chuyển thành method repository giữ nguyên hành vi khoá.
5. Agenda, Participants, Attendance, GuestAccess
6. **Reports** (thêm 14/11/2026): `ReportService`, chỉ đọc `Meetings` và `MeetingGuestInvitations`, nên thành
   một query port. Đặt sau GuestAccess.
7. Documents (MeetingDocument, PersonalLibrary, DocumentComment, `StoredFileWriter`, `OrphanFilePurger`), Notes
8. Votes
9. Calls, **Speaking** (thêm 14/11/2026: `SpeakingService`, `SpeakingMeetingEnd`, dùng transaction và đọc
   `CallParticipants`, `AgendaItems`), Recordings, RoomTake, SpeechClip, Transcription, VoiceId
10. Ai, Summaries, Minutes
11. Email (tách nghiệp vụ dispatch khỏi sender)

Ba module CalendarSharing, Reports, Speaking (khoảng 1.780 dòng) vào code từ 08/11 đến 10/11/2026, cùng ngày hoặc
sau lần viết note đầu, nên chưa có trong thứ tự module ở mục 4 của ADR-0059. Thiếu chúng thì giai đoạn 4 không đạt được.

Checklist cho mỗi module:

- [ ] Service chuyển sang `Application/<Module>/`, không còn `using Microsoft.EntityFrameworkCore`, `Npgsql`, `TrustCabinet.Infrastructure`.
- [ ] Service gọi trực tiếp `db.` được thay bằng method repository hoặc query port có tên nghiệp vụ.
- [ ] `InTransactionAsync`/`SaveChangesAsync` được thay bằng `IUnitOfWork`, giữ đúng isolation và retry như cũ.
- [ ] Các nhánh bắt lỗi Postgres được thay bằng exception lưu trữ. Tên index chuyển vào bảng của `PersistenceExceptionTranslator`.
- [ ] Mã `MSGnn` và HTTP status không đổi. Test integration hiện có chạy xanh mà không phải sửa assertion.
- [ ] Ít nhất một unit test mới cho service, chạy với fake repository, không cần Postgres.
- [ ] Rút module đó khỏi danh sách ngoại lệ của `ArchitectureTests`.

**Giai đoạn 3: dọn tầng Web** (1–2 PR)

- 7 publisher ở §3.1 (gồm `SpeakingChangePublisher`) không dùng `TrustCabinetDbContext` nữa; `MeetingHub` thôi
  inject `IMeetingRepository`.
- `NotificationHub`, `NotificationWorker`, `AuthEndpoints`, `CapabilityGate` thôi inject `IUserAccountRepository`;
  endpoint chỉ gọi service Application.
- `/api/health` kiểm tra DB qua một port (ví dụ `IDatabaseProbe`) do Infrastructure cài đặt, vẫn trả 503 khi DB
  không kết nối được, vì deploy dựa vào đó để rollback (ADR-0047).
- `Program.cs` chỉ còn `AddApplication()` và `AddInfrastructure(configuration)`.

**Giai đoạn 4: đóng lại** (1 PR)

- Danh sách ngoại lệ của `ArchitectureTests` phải rỗng.
- Cập nhật tài liệu cùng commit:
  - [AGENTS.md](../../AGENTS.md), phần Layout và câu "`Program.cs` will outgrow itself";
  - [03-co-so-du-lieu.md](../02-design/03-co-so-du-lieu.md);
  - package diagram backend/Domain trong [15-package-diagram/](../15-package-diagram/README.md);
  - sơ đồ kiến trúc trong [16-architecture-diagram/](../16-architecture-diagram/README.md);
  - class diagram nếu có nhắc tới namespace.

## 5. Rủi ro và cách giữ an toàn

| Rủi ro | Cách giữ |
|---|---|
| Đổi hành vi transaction (serializable, retry, `FOR UPDATE`) | `EfUnitOfWork` giữ đúng `TransactionMode` từng service đang dùng; method repository có khoá vẫn khoá như cũ, chỉ đổi tên (ví dụ `LockForChangeAsync`) và ghi rõ hành vi khoá trong XML doc |
| Mất phân biệt lỗi (trùng, hết kết nối, xung đột) dẫn tới sai `MSGnn` | Translator ánh xạ đủ các trường hợp đang bắt hôm nay; test integration hiện có (Postgres thật) là lưới an toàn, không sửa assertion |
| Conflict với các PR feature đang mở | Làm theo module; trước khi động vào module nào thì báo nhóm, và merge PR refactor module đó trong ngày |
| 88 file test đang `using TrustCabinet.Infrastructure…` và 152 file `using TrustCabinet.Domain…` (đo 14/11/2026) | Giai đoạn 1 đổi namespace hàng loạt; test integration vẫn khởi động app qua `WebApplicationFactory` nên ít phải sửa |
| N+1 hoặc chậm hơn khi bỏ truy vấn EF viết thẳng trong service | Truy vấn nặng chuyển nguyên khối vào query port của Infrastructure, không tách thành nhiều lần gọi repository |
| CI `-warnaserror` | Chạy `dotnet build TrustCabinet.sln -warnaserror` trước khi push mỗi PR |

## 6. Không làm trong đợt này

- Không thêm MediatR, AutoMapper hay NetArchTest. Gọi service trực tiếp, map tay, kiểm tra kiến trúc bằng reflection.
- Không đổi schema, migration, endpoint, contract API, mã `MSGnn`.
- Không tách Domain thành aggregate kiểu DDD hay rich domain model. Entity giữ nguyên, rule giữ nguyên chỗ.
- Không đụng `frontend/` và `workers/speech/`.

## 7. Cần nhóm chốt

1. Có đồng ý làm theo lộ trình trên không, và ai phụ trách từng module ở giai đoạn 2.
2. Giữ `I*Service` cho mọi service (để fake trong test endpoint) hay bỏ interface với service chỉ có một cài đặt.
3. Thời điểm bắt đầu: trước hay sau khi SCRUM-33/37 (gửi duyệt, ban hành, ký biên bản) vào `main`.
4. Thêm CalendarSharing, Reports, Speaking vào thứ tự module ở ADR-0059: sửa ADR khi nó còn *Đề xuất*, hay ghi
   vào ADR thay thế viết ở giai đoạn 4.
