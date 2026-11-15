# Quy chuẩn refactor backend sang Clean Architecture

- **Ticket:** [SCRUM-143](https://sep490g116.atlassian.net/browse/SCRUM-143), 21 subtask `[CA-00]` → `[CA-20]`
  (SCRUM-165 → SCRUM-185)
- **Nguồn:** mục 2, 3, 4 của SCRUM-143 (viết 14/11/2026), chép vào repo ở CA-00 (SCRUM-165) kèm chữ ký thật của
  các lớp nền tảng vừa tạo
- **Liên quan:** [ADR-0059](../05-decisions/0059-giu-ba-project-backend-tach-application-sau-bao-cao.md),
  note [Refactor backend sang Clean Architecture](../notes/NOTE-refactor-clean-architecture.md)

**File này là nguồn chuẩn.** Nếu ticket và file lệch nhau thì theo file. Muốn đổi quy chuẩn thì mở PR sửa file
này, không tự diễn giải trong code. Các subtask có thể do nhiều người hoặc nhiều phiên AI agent khác nhau làm, nên
mọi subtask làm đúng theo mục 3, không tự chọn cách khác.

Đích đến là bốn project:

```
TrustCabinet.Domain          entity, enum, rule, policy thuần. Không tham chiếu project hay package nào.
TrustCabinet.Application     use case service, DTO, port (interface). Chỉ tham chiếu Domain.
TrustCabinet.Infrastructure  EF Core, migration, seed, repository, unit of work, adapter ngoài. Tham chiếu Application + Domain.
TrustCabinet.Web             endpoint, hub, publisher, worker, composition root. Gọi Application; chỉ Program.cs đụng Infrastructure để đăng ký DI.
```

## 1. Bất biến (không subtask nào được phá)

- Không đổi endpoint, request, response, HTTP status, mã `MSGnn`, schema, migration, seed.
- Không đổi hành vi: cùng điều kiện thì cùng kết quả, cùng thứ tự ghi, cùng khoá dòng, cùng mức isolation, cùng thời
  điểm phát sự kiện SignalR và email. Ngoại lệ duy nhất đã chốt ở SCRUM-146 và làm ở CA-01: lỗi lưu trữ bị bọc trong
  `InvalidOperationException` trước đây lọt thành 500, nay trả mã `MSGnn` lỗi lưu trữ sẵn có của endpoint đó.
- Không đụng `frontend/`, `workers/speech/`, `deploy/`.
- Test integration hiện có (Postgres thật) phải xanh **mà không sửa assertion**. Chỉ được sửa `using`, namespace và
  phần dựng đối tượng trong test. Nếu thấy phải sửa assertion thì dừng lại, ghi comment vào subtask.
- Sau mỗi subtask, nhánh refactor build được với `-warnaserror`, toàn bộ test xanh, app chạy được.

## 2. Quy trình nhánh và merge

- **Nhánh tích hợp:** `refactor/clean-architecture`, tách từ `main` (`47d022d9`, 15/11/2026). Mọi việc của SCRUM-143
  nằm trên nhánh đó cho tới bước cuối.
- **Nhánh subtask:** `refactor/ca-<nn>-<slug>` (ví dụ `refactor/ca-05-meetings`), tách từ `refactor/clean-architecture`
  **sau khi subtask đứng trước đã merge**. PR của subtask có base là `refactor/clean-architecture`, không phải `main`.
- Một subtask = một PR. Làm tuần tự theo số `CA-nn`; không làm song song hai subtask (chúng sửa chung
  `DependencyInjection.cs`, `ArchitectureTests.cs`, `UniqueConflict`).
- Không rebase nhánh đã mở PR. Việc đưa `main` vào `refactor/clean-architecture` (nếu `main` có hotfix) do người phụ
  trách SCRUM-143 làm bằng merge, giữa hai subtask; agent không tự làm.
- `refactor/clean-architecture` chỉ merge vào `main` một lần, ở CA-20, sau khi: mọi subtask đã merge vào nhánh
  refactor, build `-warnaserror` xanh, toàn bộ `dotnet test` xanh, frontend build + lint xanh, và E2E toàn hệ thống pass.
- Commit message tiếng Việt; tiêu đề và mô tả PR tiếng Anh; comment review và comment Jira tiếng Việt
  ([AGENTS.md](../../AGENTS.md)).

## 3. Quy chuẩn bắt buộc

### R1. Chiều phụ thuộc và package

- `Domain`: không `ProjectReference`, không `PackageReference`.
- `Application`: chỉ tham chiếu `Domain`. Package được phép: `Microsoft.Extensions.Logging.Abstractions`,
  `Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection.Abstractions` (cùng phiên bản
  `Microsoft.Extensions.*` mà Infrastructure dùng, hiện là `10.0.12`). **Cấm** `Microsoft.EntityFrameworkCore*`,
  `Npgsql*`, `StackExchange.Redis`, `AWSSDK.*`, `MailKit`, `Livekit.*`, `QuestPDF`, `AngleSharp`, `PdfPig`,
  `Microsoft.AspNetCore.*`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Configuration*`,
  `Microsoft.Extensions.Hosting*`.
- `Infrastructure`: tham chiếu `Application` và `Domain`.
- `Web`: tham chiếu `Application` và `Infrastructure`. Chỉ `Program.cs` và `DatabaseMigrationCommand.cs` được
  `using TrustCabinet.Infrastructure…`.
- Không thêm package mới ở bất kỳ project nào (không MediatR, AutoMapper, NetArchTest, Moq, NSubstitute).

### R2. Vị trí và tên

- Thư mục module giữ nguyên tên hiện có: `Access, Agenda, Ai, Attendance, Audit, Authentication, CalendarSharing,
  Calls, Documents, Email, GuestAccess, Meetings, Notes, Notifications, Participants, Recordings, Reports, Speaking,
  Summaries, Transcription, Users, VoiceId, Votes`.
- File chuyển project bằng `git mv`, **giữ nguyên tên file, tên type, tên method, chữ ký**. Chỉ đổi namespace gốc:
  `TrustCabinet.Infrastructure.<Module>` hoặc `TrustCabinet.Domain.<Module>` thành `TrustCabinet.Application.<Module>`.
- Thứ dùng chung của Application nằm ở `Application/Abstractions/` (namespace `TrustCabinet.Application.Abstractions`).
- Lớp cài đặt ở Infrastructure **ở nguyên** `Infrastructure/<Module>/`, giữ tên (`VoteRepository`, không đổi thành
  `EfVoteRepository`, không gom vào `Persistence/`). Đây là chỗ khác note §2.3 và §3, để giảm diff và conflict. Riêng
  lớp nền tảng mới dùng chung (`EfUnitOfWork`, `PersistenceExceptionTranslator`, `DbErrors`) đặt ở
  `Infrastructure/Persistence/`.
- Không di chuyển, không đổi tên `Migrations/`, `Configurations/`, `Seed/`, `TrustCabinetDbContext*`.
- Một file đang chứa cả repository lẫn service (ví dụ `MeetingDocumentService.cs`) thì tách: repository ra file riêng
  cùng tên lớp ở Infrastructure, service sang Application.

### R3. Domain chứa gì

- Ở lại Domain: entity, enum, `*Rules.cs`, `*Policy.cs`, catalogue và phép tính thuần không I/O (`PermissionCatalog`,
  `AuditEventCatalog`, `MeetingSlotFinder`, `SpeechClipFold`…).
- Sang Application: mọi interface service, repository, port; DTO request/response; kiểu outcome/result của use case.
- Kiểu nào bị một entity, enum, rule hay policy ở Domain tham chiếu thì **ở lại Domain** (tách khỏi file
  `*Contracts.cs` nếu cần), vì Domain không được tham chiếu Application. Danh sách đã biết ở CA-02.
- Không viết lại entity thành aggregate DDD, không thêm value object mới.

### R4. Service ở Application

- Giữ tên lớp, interface, chữ ký method public, kiểu trả về (outcome/failure enum) và ánh xạ sang `MSGnn` ở endpoint.
  Không thêm, không bớt interface service.
- Constructor chỉ nhận: port của Application, `TimeProvider`, `ILogger<T>`, `IOptions<T>`, `IServiceScopeFactory`.
  Không nhận `TrustCabinetDbContext`, `IHttpClientFactory`, `IConfiguration`, hay lớp cụ thể của Infrastructure.
- File không còn `using Microsoft.EntityFrameworkCore`, `using Npgsql`, `using TrustCabinet.Infrastructure`.
- Lớp options là POCO mà service dùng thì chuyển theo service; việc bind cấu hình (`services.Configure<…>`) ở lại
  Infrastructure.
- Giữ nguyên mức truy cập (`internal` vẫn là `internal`); `TrustCabinet.Application.csproj` khai `InternalsVisibleTo`
  cho `TrustCabinet.Web.Tests` và `TrustCabinet.Application.Tests`. Từ CA-03 khai thêm cho `TrustCabinet.Infrastructure`,
  chỉ vì `TrustCabinetDbContext` dùng chung `AuditService.DetailsOptions`. Đây không phải tiền lệ: Infrastructure không
  dựng hay gọi service `internal` của Application.
- Logic quyết định (ai được làm gì, trạng thái nào được chuyển, tính toán) ở Application hoặc Domain. Một lớp ở
  Infrastructure còn `if` nghiệp vụ là sai tầng.

### R5. Port: repository và query

- **Repository** `I<Tên>Repository`: nạp và lưu entity cho use case ghi, kể cả method khoá dòng. Repository đang là
  lớp cụ thể chưa có interface (`VoteRepository`, `AttendanceRepository`) thì thêm interface cùng tên có tiền tố `I`.
- **Query port** `I<Chủ đề>Query` (ví dụ `IMeetingAudienceQuery`, `IMeetingReportQuery`): truy vấn chỉ đọc trả DTO,
  không tracking. Cài đặt là lớp `<Chủ đề>Query` ở `Infrastructure/<Module>/`.
- Interface repository **sẵn có** giữ nguyên method và tên; không tách lại chỉ để phân loại. Quy tắc tên dưới đây áp
  dụng cho method **mới**.
- Chữ ký port không được có: `IQueryable`, `DbSet`, `Expression<…>`, `EntityEntry`, kiểu của EF Core hay Npgsql, tên
  bảng, tên index, SQL.
- Tên method nói bằng nghiệp vụ, có hậu tố `Async`. Method khoá dòng bắt đầu bằng `Lock…Async` và XML doc ghi rõ khoá
  độc quyền hay khoá chia sẻ, khoá những dòng nào, theo thứ tự nào. Cấm các từ `ForUpdate`, `ForShare`, `Sql`,
  `Tracked`, `Queryable` trong tên mới.
- Thêm/xoá entity: `void Add(X entity)`, `void Remove(X entity)` (chỉ xếp hàng, ghi khi `SaveChangesAsync`). Lệnh ghi
  thẳng (`ExecuteUpdateAsync`, `ExecuteDeleteAsync`, SQL thô) thành method repository tên nghiệp vụ, trả số dòng bị
  ảnh hưởng, XML doc ghi "runs immediately, not staged".
- **Chuyển truy vấn nguyên khối:** câu LINQ hoặc SQL trong service được cắt sang method repository/query **không viết
  lại**: giữ `Include`, `AsNoTracking`, `OrderBy`, điều kiện lọc, lọc theo quyền, và số lần truy vấn. Không tách một
  truy vấn thành nhiều lời gọi repository.
- Cấm generic repository (`IRepository<T>`), specification pattern.

### R6. Mô hình tracking

Giữ mô hình hiện tại: repository trả entity đang được theo dõi, service sửa entity rồi gọi
`IUnitOfWork.SaveChangesAsync`. Không viết lại thành detached/`Update` tường minh. Mọi lớp trong một request dùng
chung một `TrustCabinetDbContext` scoped, nên repository của các module thấy cùng một transaction.

### R7. `IUnitOfWork`

[`Application/Abstractions/IUnitOfWork.cs`](../../backend/TrustCabinet.Application/Abstractions/IUnitOfWork.cs):

```csharp
namespace TrustCabinet.Application.Abstractions;

public enum TransactionMode { None, ReadCommitted, RepeatableRead }

public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default);
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, Func<T, bool> commitWhen,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default);
    Task ExecuteAsync(Func<CancellationToken, Task> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    void DiscardChanges();
}
```

- `None`: không mở transaction, chỉ chạy `work` và dịch lỗi lưu trữ (R8).
- `ReadCommitted`, `RepeatableRead`: chưa có transaction thì mở ở mức đó, `work` trả về thì commit, ném exception thì
  rollback. Đang có transaction (do lời gọi ngoài hoặc code cũ mở) thì **chạy trong transaction đó**, không commit;
  nếu mức yêu cầu mạnh hơn mức đang mở thì ném `InvalidOperationException` (lỗi lập trình).
- `commitWhen`: commit khi hàm trả `true`, ngược lại rollback và vẫn trả kết quả. Chỉ dùng ở lời gọi ngoài cùng, cho
  khối cũ có lối ra chủ ý không commit sau khi đã ghi.
- `ExecuteAsync` **không** tự gọi `SaveChangesAsync`; service gọi tường minh đúng chỗ code cũ đang gọi.
- `DiscardChanges()` thay cho `db.ChangeTracker.Clear()`.
- Ánh xạ từ code cũ: `BeginTransactionAsync(ct)` và `InTransactionAsync` → `ReadCommitted`;
  `BeginTransactionAsync(IsolationLevel.RepeatableRead)` → `RepeatableRead`; đoạn
  `if (db.Database.CurrentTransaction is null) …` → một lời gọi `ExecuteAsync` (tự nhập vào transaction đang mở).
  Không có chỗ nào dùng `Serializable`; không thêm.
- Việc sau commit (phát SignalR, gửi email, xoá file) vẫn đặt **sau** khi `ExecuteAsync` trả về, đúng thứ tự cũ.

Cài đặt là [`Infrastructure/Persistence/EfUnitOfWork.cs`](../../backend/TrustCabinet.Infrastructure/Persistence/EfUnitOfWork.cs),
đăng ký scoped trong `AddTrustCabinetDatabase`, bọc đúng `TrustCabinetDbContext` scoped của request. Hành vi cụ thể
(có test ở `tests/TrustCabinet.Web.Tests/Persistence/EfUnitOfWorkTests.cs`):

- Nhận ra transaction đang mở bằng `db.Database.CurrentTransaction`, nên nhập được vào transaction do code cũ mở bằng
  `BeginTransactionAsync`. Mức đang mở đọc từ `GetDbTransaction().IsolationLevel`; thứ tự mạnh dần là
  `ReadCommitted` < `RepeatableRead` (và `Snapshot`) < `Serializable`.
- Khi nhập vào transaction đang mở, và với `TransactionMode.None`, `commitWhen` **không được xét**: chủ của
  transaction đó quyết định commit hay rollback. Vì vậy `commitWhen` chỉ có nghĩa ở lời gọi ngoài cùng. Đây là đúng
  hành vi của khối cũ kiểu `owned = CurrentTransaction is null ? Begin… : null` (ví dụ `CallRepository`): khi được gọi
  bên trong transaction của nơi khác, nó thoát ra mà không commit và để nơi gọi quyết định.
- Lồng nhau chỉ theo một chiều. Code cũ mở transaction rồi gọi unit of work thì nhập được. Chiều ngược lại, unit of
  work bọc ngoài một đoạn code cũ gọi `BeginTransactionAsync` mà không kiểm `CurrentTransaction`, thì đoạn cũ ném
  "The connection is already in a transaction". CA-01 đã sửa `MeetingRepository.InTransactionAsync` và
  `VoiceIdRepository.InTransactionAsync`: đang có transaction thì chạy `operation` trong transaction đó và không
  commit, chưa có thì mở và commit như cũ (test ở `EfUnitOfWorkTests`). Chỗ khác được xử lý ở subtask chuyển module
  đó, hoặc bằng port mỏng theo R9.
- Sau rollback, change tracker vẫn giữ những gì `work` đã xếp hàng; lần `SaveChangesAsync` sau sẽ ghi lại chúng. Muốn
  thử lại từ dữ liệu đã lưu thì gọi `DiscardChanges()`, như code cũ gọi `ChangeTracker.Clear()`.
- Lệnh ghi thẳng (SQL thô, `ExecuteUpdateAsync`) thất bại trong transaction làm PostgreSQL huỷ cả transaction đó
  (mọi lệnh sau trả `25P02`). `SaveChangesAsync` thất bại thì không, vì EF Core tự đặt savepoint.
- Transaction mới mở qua `db.Database.CreateExecutionStrategy()`. Dự án không bật retry, nên strategy không chạy lại
  `work`; lỗi tạm thời (mất kết nối, serialization failure `40001` ở `RepeatableRead`) nó bọc trong
  `InvalidOperationException` được dịch thành `PersistenceUnavailableException`.
- Provider không phải relational (EF InMemory trong `NotificationServiceTests`, `NotificationEndpointsTests`,
  `NotificationWorkerTests`, `EmailDeliveryTests`, `MeetingRepositoryTests`) không có transaction và từ chối mở: với
  provider đó mọi `TransactionMode` chỉ chạy `work`, vẫn dịch lỗi. Test trên provider đó không kiểm được rollback.

### R8. Lỗi lưu trữ

[`Application/Abstractions/PersistenceExceptions.cs`](../../backend/TrustCabinet.Application/Abstractions/PersistenceExceptions.cs)
và [`UniqueConflict.cs`](../../backend/TrustCabinet.Application/Abstractions/UniqueConflict.cs):

```csharp
public abstract class PersistenceException(string message, Exception? innerException) : Exception  // gốc
public sealed class UniqueConflictException(UniqueConflict conflict, Exception? innerException = null)
                                                    // 23505; có UniqueConflict Conflict
public sealed class ReferenceConflictException(Exception? innerException = null)      // 23503 và 23001
public sealed class ConcurrencyConflictException(Exception? innerException = null)    // DbUpdateConcurrencyException
public sealed class PersistenceUnavailableException(Exception? innerException = null) // DbUpdateException khác, NpgsqlException,
                                                    // TimeoutException, InvalidOperationException bọc hai loại đó
public enum UniqueConflict { Unknown = 0 /* module thêm dần */ }
```

- Chỉ `Infrastructure/Persistence/PersistenceExceptionTranslator` biết `SqlState` và tên index. Nó có một bảng
  `tên index → UniqueConflict`. Exception gốc giữ ở `InnerException`. `OperationCanceledException` không bị dịch.
- Điểm dịch duy nhất là `EfUnitOfWork` (`ExecuteAsync`, `SaveChangesAsync`). Vì vậy **trong service Application, mọi
  lời gọi repository và query port phải nằm trong delegate của** `ExecuteAsync` (đọc thuần thì `TransactionMode.None`,
  trừ khi code cũ đọc trong transaction). `catch` đặt **ngoài** `ExecuteAsync`; trong delegate không bắt lỗi lưu trữ.
  Ngoại lệ duy nhất (chốt 15/11/2026 ở CA-04): code cũ bắt lỗi quanh **đúng một lời gọi lưu** nằm giữa một transaction
  rồi đi tiếp trong transaction đó (đếm lại, nạp lại entity, trả outcome để transaction vẫn commit), ví dụ
  `RoleService`, `RolePermissionService`, `UserService`. Khi đó `catch` bọc sát `IUnitOfWork.SaveChangesAsync` (cũng là
  điểm dịch, nên chỉ exception đã dịch tới được đây), giữ nguyên chỗ và thứ tự cũ. Đưa ra ngoài `ExecuteAsync` sẽ làm
  `catch` bắt cả lần ghi audit sau đó (đang là 500 thành mã `MSGnn`) và đẩy phần đếm lại ra khỏi khoá: đổi hành vi.
- Ánh xạ từ code cũ: `catch (Exception ex) when (PersistenceError(ex))` → `catch (PersistenceException)`; bắt
  `UniqueViolation` không xét tên constraint → `catch (UniqueConflictException)`; có xét tên constraint →
  `catch (UniqueConflictException e) when (e.Conflict == UniqueConflict.X)`; `ForeignKeyViolation`/`RestrictViolation`
  → `catch (ReferenceConflictException)`; `DbUpdateConcurrencyException` → `catch (ConcurrencyConflictException)`.
  Giữ nguyên thứ tự các khối `catch` và mã trả về.
- Chỉ thêm thành viên `UniqueConflict` khi code cũ phân biệt theo tên constraint. Thêm thành viên và dòng trong bảng
  của translator trong cùng PR.
- Code còn ở Infrastructure (repository, adapter, service chưa chuyển) dùng `DbErrors` (CA-01), lớp này nhận ra cả
  exception gốc lẫn exception đã dịch.

[`Infrastructure/Persistence/DbErrors.cs`](../../backend/TrustCabinet.Infrastructure/Persistence/DbErrors.cs)
(`internal static`, test ở `tests/TrustCabinet.Web.Tests/Persistence/DbErrorsTests.cs`):

```csharp
bool IsPersistenceFailure(Exception exception)                        // mọi lỗi lưu trữ, dạng rộng nhất
bool IsUniqueViolation(Exception exception, string? constraint = null) // 23505; có tên thì đúng index đó
bool IsReferenceViolation(Exception exception)                        // 23503 hoặc 23001
bool IsConcurrencyConflict(Exception exception)                       // DbUpdateConcurrencyException
```

- `DbErrors` và translator phân loại bằng cùng một hàm, `PersistenceExceptionTranslator.Classify`, nên một lỗi được
  trả lời giống nhau dù tới `catch` ở dạng gốc hay dạng đã dịch. Với `PersistenceException`, `DbErrors` phân loại
  exception gốc nằm trong `InnerException` (nhờ vậy vẫn so được tên index); exception đã dịch không mang exception
  gốc, như fake trong unit test ném ra, thì xét theo kiểu.
- `IsPersistenceFailure` đúng với cả ba loại hẹp hơn. Vì vậy `catch` cho unique, reference, concurrency phải đứng
  **trước** `catch` dùng `IsPersistenceFailure`, như thứ tự code cũ.
- Vi phạm unique và reference được tìm trong cả chuỗi `InnerException`: vi phạm ném trần từ SQL thô hoặc
  `ExecuteUpdateAsync`, và vi phạm bọc trong `InvalidOperationException` của execution strategy, cũng được nhận ra.
  Bộ lọc cũ kiểu `DbUpdateException { InnerException: PostgresException }` chỉ thấy dạng EF bọc một lớp.
- Hai loại còn lại không dò chuỗi. Concurrency chỉ xét chính exception đó: `InvalidOperationException` bọc
  `DbUpdateConcurrencyException` là lỗi lưu trữ thường, không phải concurrency. Lỗi lưu trữ khác xét chính exception
  đó, hoặc đúng một lớp `InvalidOperationException` bọc ngoài; bọc hai lớp thì không còn là lỗi lưu trữ.
- Tên index vẫn là hằng trong file đang dùng nó; subtask của module chuyển nó vào bảng của translator cùng lúc đổi
  `catch` sang `UniqueConflictException`.
- Sau CA-01, `using Npgsql` ở Infrastructure chỉ còn trong `Persistence/PersistenceExceptionTranslator.cs`,
  `Audit/AuditLogQuery.cs` (dùng `NpgsqlParameter`, `NpgsqlDbType`; trước CA-03 nằm trong `AuditLogQueryService.cs`) và
  `Migrations/` (file sinh ra). File mới không tự so `SqlState` hay kiểu exception của EF Core/Npgsql.

`PersistenceExceptionTranslator.Translate(Exception)` trả `PersistenceException?` và phân loại theo đúng thứ tự này
(test ở `tests/TrustCabinet.Web.Tests/Persistence/PersistenceExceptionTranslatorTests.cs`):

1. `PersistenceException` (đã dịch ở lời gọi trong) hoặc `OperationCanceledException` → `null`, ném lại nguyên vẹn.
2. `DbUpdateConcurrencyException` → `ConcurrencyConflictException`.
3. Có `PostgresException` là chính exception đó hoặc nằm trong chuỗi `InnerException`, với `SqlState` 23505 →
   `UniqueConflictException` (tra bảng theo `ConstraintName`, không có thì `Unknown`); 23503 hoặc 23001 →
   `ReferenceConflictException`.
4. `DbUpdateException`, `NpgsqlException` (gồm mọi `PostgresException` còn lại), `TimeoutException`, hoặc
   `InvalidOperationException` có `InnerException` là `DbUpdateException`/`NpgsqlException`/`TimeoutException` →
   `PersistenceUnavailableException`. `DbUpdateException` ở vế cuối là phần thêm so với ticket: lỗi tạm thời xảy ra
   trong `SaveChanges` tới đây dưới dạng `InvalidOperationException` → `DbUpdateException` → `PostgresException`, thiếu
   nó thì service đã chuyển trả 500.
5. Còn lại → `null`: không phải lỗi lưu trữ.

Bảng index hiện rỗng, nên mọi vi phạm unique là `Unknown` cho tới khi module đầu tiên thêm dòng.

Translator xét kiểu exception, không xét nó đến từ đâu: một `TimeoutException` do chính `work` ném (kể cả timeout của
Redis) cũng thành `PersistenceUnavailableException`, giống các bộ lọc `PersistenceError` cũ. `HttpRequestException` và
`IOException` thì đi thẳng.

### R9. Giai đoạn chuyển tiếp

- Service chưa chuyển vẫn chạy như cũ. Service đã chuyển và chưa chuyển dùng chung `DbContext`, nên transaction lồng
  nhau hoạt động nhờ R7.
- Service đang chuyển mà cần một helper static nhận `TrustCabinetDbContext` của **module chưa chuyển**
  (`MeetingInvitationEvents.Stage`, `SpeakingRequestCleanup.CloseOpenAsync`, lớp cụ thể `CalendarShareStopper`…): tạo
  một port tên nghiệp vụ ở `Application/<module sở hữu helper>/`, cài đặt ở Infrastructure bằng một lớp mỏng gọi lại
  helper cũ, **không sửa helper**. Subtask của module sở hữu sẽ thay lớp mỏng đó.
- `SaveChangesAsync` và `InTransactionAsync` trên interface repository: service đã chuyển không gọi nữa. Method bị xoá
  khỏi interface ở subtask chuyển consumer cuối cùng (kiểm bằng `git grep`).

### R10. DI

- `Application/DependencyInjection.cs` có `AddApplication(this IServiceCollection)`, bên trong mỗi module một method
  `private static void Add<Module>(IServiceCollection)`. Mỗi subtask chuyển dòng đăng ký service của module mình sang
  đó, **giữ nguyên lifetime**. `Program.cs` gọi `AddApplication()` ngay trước `AddTrustCabinetDatabase`.
- `Infrastructure/<Module>/*ServiceCollectionExtensions.cs` chỉ còn đăng ký repository, query, adapter và bind options.

### R11. Web

- Endpoint chỉ làm HTTP: đọc request, lấy caller, gọi một service Application, ánh xạ kết quả ra status và `MSGnn`.
  Hằng `MSGnn` ở nguyên trong `Web/Endpoints/` (`MessageCodeCatalogueTests` đọc thư mục đó).
- Hub, publisher, worker ở Web gọi service hoặc query port của Application; không nhận `TrustCabinetDbContext`, không
  nhận repository.

### R12. Test

- Unit test mới ở `tests/TrustCabinet.Application.Tests/<Module>/<Service>Tests.cs`. Project này chỉ tham chiếu
  Application và Domain, nên chạy không cần Postgres.
- Dùng fake viết tay trong `tests/TrustCabinet.Application.Tests/Fakes/` (`FakeUnitOfWork`, repository trong bộ nhớ).
  Không dùng thư viện mock.
- Mỗi service chuyển sang có tối thiểu: một test luồng chính, một test nhánh từ chối nghiệp vụ, một test "lỗi lưu trữ →
  đúng outcome" (fake ném `PersistenceUnavailableException`), và một test cho mỗi `UniqueConflict` service đó bắt.
- Test integration hiện có: theo mục 1.

`FakeUnitOfWork` chạy `work` ngay và đếm: `Commits`, `Rollbacks`, `Saves`, `Discards`, cùng `Modes` (mức của từng lời
gọi `ExecuteAsync`, theo thứ tự). Như bản thật, chỉ lời gọi có transaction ngoài cùng mới commit hoặc rollback; lời
gọi lồng bên trong và `TransactionMode.None` chỉ được ghi vào `Modes`. Gán `FailNextSave` để lần `SaveChangesAsync`
kế tiếp ném exception đó một lần.

### R13. `ArchitectureTests`

Nằm ở [`tests/TrustCabinet.Web.Tests/ArchitectureTests.cs`](../../tests/TrustCabinet.Web.Tests/ArchitectureTests.cs),
dùng reflection. Mỗi luật có một mảng ngoại lệ (tên đầy đủ của type, sắp theo ABC), điền ở CA-00 bằng mọi vi phạm
hiện có. Có test bắt lỗi khi một mục trong mảng không còn vi phạm, nên danh sách chỉ rút ngắn được. **Mỗi subtask xoá
các mục của module mình; không subtask nào được thêm mục.**

- **A1** Domain chỉ tham chiếu BCL; từ CA-02 thêm: Domain không có interface nào ngoài `IPdfInspector`, không có type
  tên kết thúc bằng `Service` hay `Repository`.
- **A2** Application chỉ tham chiếu Domain và package ở R1 (xét cả assembly lẫn `TrustCabinet.Application.csproj`).
- **A3** Không type nào ngoài Infrastructure có field, tham số constructor hay tham số method kiểu
  `TrustCabinetDbContext` (kể cả method compiler sinh cho lambda).
- **A4** Không lớp nào ở Infrastructure cài đặt interface `I*Service` (xét interface ở cả Domain lẫn Application, để
  danh sách đủ ngay từ CA-00).
- **A5** Không interface nào ngoài `IUnitOfWork` có method `SaveChangesAsync` hay `InTransactionAsync` (xét cả Domain
  lẫn Application).
- **A6** Type ở Web không có field hay tham số kiểu `I*Repository`, và không dùng type của
  `TrustCabinet.Infrastructure` trừ `Program` và `DatabaseMigrationCommand`.
- **A7** Lớp ở Infrastructure nhận `TrustCabinetDbContext` phải có tên kết thúc bằng `Repository` hoặc `Query`, hoặc
  nằm trong `Persistence/`, `Seed/`, hoặc là `TrustCabinetDbContext*`, `DependencyInjection`. Lớp khác phải nằm ở mảng
  `PendingDbContextReaders` (phải rỗng ở CA-20) hoặc `AllowedDbContextAdapters` (vĩnh viễn, mỗi mục một dòng lý do;
  chỉ cho lớp cài đặt một port của Application và không có nhánh nghiệp vụ).
- Reflection không đọc thân method, nơi `CapabilityGate`, `NotificationWorker`, `AgendaPresenterNotifier` gọi
  `GetRequiredService<T>()`: các lớp đó có mục trong mảng của luật mình kèm comment, và CA-18, CA-20 kiểm bằng
  `git grep` (chi tiết ở phần cách đo bên dưới).

Cách `ArchitectureTests` đo, để subtask sau đọc kết quả cho đúng:

- "Nhận" một type nghĩa là type đó là kiểu của field, tham số constructor hoặc tham số method, tính cả đối số generic
  (`Func<TrustCabinetDbContext, …>`) và phần tử mảng. Biến mà lambda bắt, và biến cục bộ của method `async`, là field
  của type lồng do compiler sinh, nên handler của endpoint cũng được thấy.
- Type lồng và type compiler sinh được tính cho type cấp ngoài cùng mà nó được viết trong đó; mảng ngoại lệ ghi tên
  type cấp ngoài cùng. Type do source generator thêm vào assembly (ngoài namespace `TrustCabinet`) không được xét.
- A6 có hai mảng. `WebTypesTakingRepositories` đo bằng reflection như trên. `WebFilesNamingInfrastructure` đọc **mã
  nguồn**: file `.cs` dưới `backend/TrustCabinet.Web` có `using TrustCabinet.Infrastructure…` hoặc tên đầy đủ
  `TrustCabinet.Infrastructure.X`, trừ `Program.cs` và `DatabaseMigrationCommand.cs`; mục là đường dẫn tính từ thư mục
  đó. Lý do: `new PdfPigInspector()`, hằng số hay lời gọi static không để lại dấu trong chữ ký nên reflection bỏ sót
  (`PdfPageCounter.cs`, `MeetingGuestIdentity.cs`, `UserEndpoints.cs`).
- `CapabilityGate`, `NotificationWorker` và `AgendaPresenterNotifier` lấy repository hoặc `DbContext` bằng
  `GetRequiredService<T>()` trong thân method. Reflection vẫn thấy chúng ở cả Debug lẫn Release, vì biến cục bộ sống qua
  một `await` nên thành field; chúng nằm trong mảng của đúng luật mình (A6, A7), không có mảng ghi tay riêng. Nếu một
  lần sửa làm reflection hết thấy mà lời gọi vẫn còn thì mục đó bị buộc xoá trong khi vi phạm chưa hết, nên CA-18 và
  CA-20 vẫn kiểm bằng `git grep "GetRequiredService<\(TrustCabinetDbContext\|I[A-Za-z]*Repository\)>" backend`.
- Giới hạn đã biết của luật xét theo tên, để CA-18 và CA-20 biết `git grep` phải phủ gì:
  - A4 không thấy service có interface `I*Service` khai trong Infrastructure.
  - A5 chỉ xét hai tên `SaveChangesAsync` và `InTransactionAsync`; `ITranscriptRepository.DiscardChanges` và
    `IUserAccountRepository.DiscardChangesAsync` đang có và không bị bắt.
  - A7 cho qua mọi lớp tên `*Repository`/`*Query`, có nhánh nghiệp vụ hay không; phần đó reviewer kiểm theo R4.
  - Mảng tính theo type cấp ngoài cùng (hoặc theo file): `Program` đã có mục cho `/api/health`, nên một lambda mới
    trong `Program.cs` nhận `DbContext` không làm test đỏ.

Số mục lúc điền ở CA-00 (đo trên `47d022d9`, Debug và Release cho cùng danh sách):

| Mảng | Luật | Số mục |
|---|---|---|
| `DbContextOutsideInfrastructure` | A3 | 8: `Program` (lambda `/api/health`) và 7 publisher |
| `ServicesInInfrastructure` | A4 | 35 |
| `InterfacesExposingTheUnitOfWork` | A5 | 8 |
| `WebTypesTakingRepositories` | A6 | 12 |
| `WebFilesNamingInfrastructure` | A6 | 12 file |
| `PendingDbContextReaders` | A7 | 40 |
| `AllowedDbContextAdapters` | A7 | 0 |

`PendingDbContextReaders` có 40 lớp, hơn bảng §3.1 của note một lớp: `SpeakingRequestCleanup` (helper static nhận
`TrustCabinetDbContext`, note chưa liệt kê).

### R14. Mỗi PR phải có

- Danh sách file đã chuyển (từ đâu sang đâu).
- Bảng ánh xạ: mỗi lời gọi `db.` cũ → method port mới; mỗi khối transaction cũ → `TransactionMode`; mỗi `catch` lỗi
  Postgres cũ → exception mới.
- Với mỗi lời gọi `ExecuteAsync` có `commitWhen`: service đó có bị gọi bên trong transaction của nơi khác không, và
  nếu có thì code cũ ở đúng chỗ đó làm gì. Bên trong transaction đang mở `commitWhen` không được xét (R7, chốt
  15/11/2026), nên một khối trước đây tự mở transaction vô điều kiện mà nay bị gọi lồng sẽ để nơi gọi commit phần nó
  đã ghi.
- Các mục đã xoá khỏi danh sách ngoại lệ của `ArchitectureTests`.
- Kết quả `dotnet build TrustCabinet.sln -warnaserror` và `dotnet test TrustCabinet.sln`.
- Chỗ lệch khỏi quy chuẩn (nếu có) và lý do.

### R15. Khi vướng

Nếu quy chuẩn không phủ một trường hợp, hoặc làm đúng quy chuẩn thì phải đổi hành vi hay sửa assertion: **dừng lại**,
ghi comment vào subtask (file, dòng, hai phương án), không tự chế cách thứ ba. Không refactor "tiện tay" ngoài phạm vi
subtask (đổi tên, dọn code, tối ưu truy vấn).

## 4. Subtask và thứ tự

| Mã | Ticket | Việc |
|---|---|---|
| CA-00 | SCRUM-165 | Khung: project Application, `IUnitOfWork`, exception lưu trữ, `ArchitectureTests`, project unit test, file quy chuẩn |
| CA-01 | SCRUM-166 | Gom nhận diện lỗi Postgres về `DbErrors` (làm phần việc của SCRUM-146 trên nhánh refactor) |
| CA-02 | SCRUM-167 | Chuyển interface và DTO từ Domain sang Application |
| CA-03 | SCRUM-168 | Notifications, Audit |
| CA-04 | SCRUM-169 | Authentication, Access, Users |
| CA-05 | SCRUM-170 | Meetings |
| CA-06 | SCRUM-171 | CalendarSharing |
| CA-07 | SCRUM-172 | Agenda |
| CA-08 | SCRUM-173 | Participants, Attendance |
| CA-09 | SCRUM-174 | GuestAccess |
| CA-10 | SCRUM-175 | Reports |
| CA-11 | SCRUM-176 | Documents, Notes |
| CA-12 | SCRUM-177 | Votes |
| CA-13 | SCRUM-178 | Calls, Speaking |
| CA-14 | SCRUM-179 | Recordings (ghi âm, room take, bản ghép, speech clip) |
| CA-15 | SCRUM-180 | Transcription, VoiceId |
| CA-16 | SCRUM-181 | Ai, Summaries, Minutes |
| CA-17 | SCRUM-182 | Email |
| CA-18 | SCRUM-183 | Dọn tầng Web: hub, publisher, worker, `/api/health`, `Program.cs` |
| CA-19 | SCRUM-184 | Tài liệu: package, architecture, class, sequence diagram, Report 4, AGENTS.md, ADR mới |
| CA-20 | SCRUM-185 | Đóng: danh sách ngoại lệ rỗng, kiểm thử toàn bộ, E2E, merge vào `main` |

Thứ tự module ở đây chia nhỏ hơn và thêm ba module (CalendarSharing, Reports, Speaking) so với mục 4 của ADR-0059;
trình tự chung giữ nguyên.

## 5. Done khi

- Mọi subtask Done; mọi mảng ngoại lệ của `ArchitectureTests` rỗng, trừ `AllowedDbContextAdapters` (mỗi mục có lý do).
- `git grep -n "TrustCabinetDbContext" backend/TrustCabinet.Application backend/TrustCabinet.Web` chỉ còn `Program.cs`
  và `DatabaseMigrationCommand.cs`.
- `dotnet build TrustCabinet.sln -warnaserror`, `dotnet test TrustCabinet.sln`, `npm run build`, `npm run lint` xanh
  trên `refactor/clean-architecture`.
- E2E toàn hệ thống pass (CA-20), rồi mới merge `refactor/clean-architecture` vào `main`.
