# ADR-0064: Backend bốn project theo Clean Architecture

- **Trạng thái:** Đã chấp nhận — thay thế [ADR-0059](0059-giu-ba-project-backend-tach-application-sau-bao-cao.md)
- **Ngày:** 2026-11-17
- **Người quyết định:** nhóm BE, Lộc ghi
- **Liên quan:** SCRUM-143 và 22 subtask `[CA-00]` → `[CA-20]`, `[CA-18b]` (SCRUM-165 → SCRUM-185, SCRUM-209);
  quy chuẩn [11-quy-chuan-clean-architecture.md](../02-design/11-quy-chuan-clean-architecture.md);
  note [Refactor backend sang Clean Architecture](../notes/NOTE-refactor-clean-architecture.md);
  [package diagram](../15-package-diagram/README.md), [sơ đồ kiến trúc](../16-architecture-diagram/README.md)

## Bối cảnh

ADR-0059 giữ backend ba project `Web` → `Infrastructure` → `Domain` đến buổi báo cáo giảng viên 13/11/2026, rồi tách
tầng Application theo module. Use case nằm trong Infrastructure cạnh EF Core: 13 service nhận thẳng
`TrustCabinetDbContext`, service bắt `PostgresException` theo `SqlState`, hub và publisher ở Web đọc `DbContext`. Nghiệp vụ
chỉ test được qua test integration trên PostgreSQL thật.

Việc tách làm từ 15/11 đến 17/11/2026 trên nhánh tích hợp `refactor/clean-architecture`, mỗi subtask một PR:
CA-00 #349, CA-01 #365, CA-02 #369, CA-03 #372, CA-04 #374, CA-05 #378, CA-06 #379, CA-07 #380, CA-08 → CA-11 #384
(bốn nhánh xếp chồng), CA-12 #387, CA-13 #388, CA-14 #389, CA-15 #390, CA-16 #391, CA-17 #392, CA-18 #401, CA-18b #402
(đưa `main` `9c282599` vào nhánh refactor). Quy chuẩn bắt buộc của mọi subtask là
[11-quy-chuan-clean-architecture.md](../02-design/11-quy-chuan-clean-architecture.md), chép từ SCRUM-143 ở CA-00.

## Quyết định

Backend là bốn project, đo trên `refactor/clean-architecture` `e2c23dd0` ngày 17/11/2026 (số file `.cs`, không tính
`Migrations/`):

| Project | Chứa | Tham chiếu |
|---|---|---|
| `TrustCabinet.Domain` (91 file) | entity, enum, `*Rules`, `*Policy`, catalogue và phép tính thuần | không project, không package |
| `TrustCabinet.Application` (167 file) | 35 service use case và interface `I*Service`; DTO; port: 30 repository, 9 query, các adapter (`IFileStorage`, `ISpeechEngine`…); `Abstractions/` (`IUnitOfWork`, exception lưu trữ, `UniqueConflict`); `AddApplication()` | Domain; ba package `Microsoft.Extensions.*.Abstractions`/`Options` |
| `TrustCabinet.Infrastructure` (142 file) | `TrustCabinetDbContext`, `Configurations/`, `Migrations/`, `Seed/`; 38 lớp repository/query cài port ở `<Module>/`; `Persistence/` (`EfUnitOfWork`, `PersistenceExceptionTranslator`, `DbErrors`); adapter Redis, LiveKit, S3/R2, email, máy ngôn ngữ, speech worker, PDF/DOCX; `AddTrustCabinetDatabase()` | Application, Domain |
| `TrustCabinet.Web` (55 file) | 29 nhóm endpoint, `MeetingHub`, `NotificationHub`, 7 change publisher, 7 worker nền, lệnh `--migrate`, composition root | Application, Infrastructure; chỉ `Program.cs` và `DatabaseMigrationCommand.cs` dùng namespace Infrastructure |

1. **Chiều phụ thuộc** Web → Application → Domain và Infrastructure → Application → Domain. Application không biết EF
   Core, Npgsql, ASP.NET Core hay SDK ngoài; Web không nhận `DbContext` hay repository.
2. **Service đọc ghi qua port.** Mọi lời gọi repository nằm trong `IUnitOfWork.ExecuteAsync` với `TransactionMode`
   `None`, `ReadCommitted` hoặc `RepeatableRead`; `EfUnitOfWork` là điểm duy nhất dịch lỗi của EF Core/Npgsql thành
   `PersistenceException` (`UniqueConflictException` mang `UniqueConflict`, `ReferenceConflictException`,
   `ConcurrencyConflictException`, `PersistenceUnavailableException`).
3. **Hub và publisher** hỏi `Application/Meetings/MeetingAudience` ai được nghe về một cuộc họp; Web chỉ đổi danh sách
   người nhận thành tên group SignalR.
4. **`ArchitectureTests`** (reflection, không thêm package) giữ bảy luật A1–A7. Mọi mảng ngoại lệ đã rỗng, trừ
   `AllowedDbContextAdapters` có một mục vĩnh viễn kèm lý do: `GuestSessionAccess`.
5. **Unit test nghiệp vụ** ở `tests/TrustCabinet.Application.Tests` (50 file, 349 test), chỉ tham chiếu Application và
   Domain, dùng fake viết tay, chạy không cần PostgreSQL. Test integration giữ nguyên assertion.
6. **Không đổi gì ra ngoài:** endpoint, response, mã `MSGnn`, schema, migration, seed giữ nguyên. Ngoại lệ duy nhất đã
   chốt ở SCRUM-146 (CA-01): lỗi lưu trữ trước đây lọt thành 500 nay trả mã lỗi lưu trữ sẵn có của endpoint đó.
7. Quy chuẩn ở `11-quy-chuan-clean-architecture.md` thành **quy chuẩn thường trực** cho code backend mới.

### Chỗ khác ADR-0059 và note refactor

| Note / ADR-0059 | Đã làm | Lý do |
|---|---|---|
| Lớp cài port gom vào `Infrastructure/Persistence/` | Ở nguyên `Infrastructure/<Module>/`; chỉ lớp nền tảng dùng chung nằm ở `Persistence/` | Giảm diff và conflict giữa các subtask (R2) |
| Lớp cài có tiền tố `Ef` (`EfVoteRepository`) | Giữ tên cũ (`VoteRepository`); chỉ `EfUnitOfWork` có tiền tố | Như trên |
| Tách read model danh sách thành port riêng, ví dụ `IMeetingListQuery` | Không tách; truy vấn sẵn có chuyển nguyên khối vào repository hoặc query của module | Không viết lại truy vấn, giữ số lần truy vấn và lọc theo quyền (R5) |
| `TransactionMode { Default, Serializable }`, `CommitChangesAsync` | `TransactionMode { None, ReadCommitted, RepeatableRead }`, `SaveChangesAsync`, `DiscardChanges` | Code cũ không có chỗ nào dùng `Serializable`; ánh xạ một-một với `BeginTransactionAsync` cũ (R7) |
| `ReferenceMissingException` | `ReferenceConflictException` (23503 và 23001) | Gom cả vi phạm khoá ngoại lẫn `RESTRICT` |
| Lộ trình giai đoạn 2 theo ADR-0059 | Thêm ba module CalendarSharing, Reports, Speaking; chia nhỏ thành CA-03 → CA-17; thêm CA-01 (`DbErrors`) và CA-18b (merge `main`) | Ba module có sau khi viết ADR-0059; `main` nhận email HTML, nhắc họp và máy AI dự phòng trong lúc refactor |
| Query port trả DTO, không tracking | Thêm một ngoại lệ: `ICallerAccountQuery` trả entity `User` có tracking, do `UserAccountRepository` cài | Giữ hành vi của `CapabilityGate` (R5, chốt ở CA-18) |

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| A — Bốn project, tách theo module, mỗi subtask một PR trên nhánh tích hợp, merge vào `main` một lần ở CA-20 | `main` không có lúc nào nửa cũ nửa mới; mỗi PR nhỏ, review được; test integration làm lưới an toàn | Phải merge `main` vào nhánh refactor giữa chừng (CA-18b) | **Chọn** |
| B — Tách từng module thẳng vào `main` | Không có nhánh sống lâu | `main` lẫn hai kiểu code nhiều ngày, feature đang mở phải theo cả hai | Bỏ |
| C — Giữ ba project như ADR-0059 điểm 1 | Không tốn công | Nghiệp vụ vẫn dính EF/Npgsql, không unit test được | Bỏ |

## Hệ quả

- ADR-0059 chuyển *Thay thế bởi ADR-0064*.
- AGENTS.md, `03-co-so-du-lieu.md`, package diagram, sơ đồ kiến trúc, class diagram FE-09, 29 sequence diagram và
  Report 4 vẽ lại theo bốn project trong cùng PR với ADR này (CA-19, SCRUM-184).
- Endpoint mới: service ở `Application/<Module>/`, port ở Application, repository/query/adapter cài ở
  `Infrastructure/<Module>/`, endpoint ở `Web/Endpoints/` chỉ làm HTTP. Đăng ký trong `AddApplication()` và
  `AddTrustCabinetDatabase()`.
- Review PR backend kiểm theo `11-quy-chuan-clean-architecture.md`; `ArchitectureTests` đỏ khi phụ thuộc đi ngược.
- Còn lại CA-20 (SCRUM-185): build `-warnaserror`, toàn bộ test, E2E toàn hệ thống, rồi merge
  `refactor/clean-architecture` vào `main`.
