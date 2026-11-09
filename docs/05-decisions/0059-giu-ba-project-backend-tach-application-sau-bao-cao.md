# ADR-0059: Backend giữ ba project Web → Infrastructure → Domain; tách tầng Application sau buổi báo cáo giảng viên

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-11-09
- **Người quyết định:** nhóm BE, Khánh ghi
- **Liên quan:** SCRUM-143 (refactor Clean Architecture), SCRUM-144 (bỏ PdfPig khỏi Domain, `0eae63bd`),
  note [Refactor backend sang Clean Architecture](../notes/NOTE-refactor-clean-architecture.md) (phân tích và lộ trình),
  [03-co-so-du-lieu.md](../02-design/03-co-so-du-lieu.md) (đã dự tính tách `TrustCabinet.Application`),
  [package diagram](../15-package-diagram/README.md), [sơ đồ kiến trúc](../16-architecture-diagram/README.md)

## Bối cảnh

Backend hiện có ba project. Số liệu đo trên `main` ngày 09/11/2026 (`0eae63bd`):

| Project | Chứa | Tham chiếu |
|---|---|---|
| `TrustCabinet.Web` | `Program.cs` (309 dòng), 24 nhóm endpoint trong `Endpoints/`, `MeetingHub`, `NotificationHub`, 6 change publisher, 6 worker nền, lệnh `--migrate` | `ProjectReference` → Infrastructure. Dùng namespace Domain qua tham chiếu bắc cầu |
| `TrustCabinet.Infrastructure` | 20 package nghiệp vụ, trong đó có 27 lớp `*Service` (khoảng 9.800 dòng use case); `TrustCabinetDbContext`, `Configurations/`, `Migrations/`, `Seed/`; worker gửi email và dọn tệp mồ côi; adapter Redis, LiveKit, S3/R2, email, máy ngôn ngữ, speech worker, PDF/DOCX | `ProjectReference` → Domain; EF Core, Npgsql, Redis, AWSSDK.S3, MailKit, LiveKit, QuestPDF, PdfPig… |
| `TrustCabinet.Domain` | entity, enum, rule và policy; interface service, interface repository và record DTO (`*Contracts.cs`) | Không tham chiếu project hay package nào (SCRUM-144; `DomainDependencyTests` chặn package quay lại) |

Không có tầng Application. Use case nằm trong Infrastructure, cạnh EF Core:

- 13 service inject thẳng `TrustCabinetDbContext`. Nhiều service bắt `DbUpdateException`/`PostgresException` theo
  `SqlState` và so tên unique index để chọn mã `MSGnn`.
- Ở Web, `MeetingHub` và các publisher `Attendance`, `Notes`, `Speech`, `Vote` đọc `TrustCabinetDbContext` để tính
  người nhận.

Hệ quả là nghiệp vụ chỉ test được qua test integration (Postgres thật, 85 file test `using TrustCabinet.Infrastructure`),
và đổi cách lưu trữ là phải sửa logic nghiệp vụ. Note refactor đã phân tích chi tiết và đề xuất tách
`TrustCabinet.Application` theo 5 giai đoạn.

Thứ Sáu 13/11/2026 nhóm báo cáo tiến độ với giảng viên hướng dẫn. Bản báo cáo, package diagram và sơ đồ kiến
trúc phải tả đúng code đang chạy. Cùng lúc đó vẫn còn feature đang mở song song: SCRUM-27 (PR #292), SCRUM-33 và SCRUM-37.

## Quyết định

1. **Đến buổi báo cáo, backend giữ nguyên ba project** `Web` → `Infrastructure` → `Domain`. Không tạo
   `TrustCabinet.Application`, không chuyển service.
2. **Tài liệu tả đúng ba project này.** Package diagram và sơ đồ kiến trúc vẽ đúng code trên `main`: use case nằm ở
   Infrastructure, publisher và hub ở Web đọc `TrustCabinetDbContext`. Không vẽ tầng Application chưa có, và không vẽ
   hai phụ thuộc là một.
3. **Trong lúc chờ, không làm lệch thêm:**
   - Domain không thêm `PackageReference` (`DomainDependencyTests` đã chặn);
   - code mới không thêm lớp nào ở Web đọc `TrustCabinetDbContext`;
   - service mới đặt cùng chỗ với service cùng module trong Infrastructure, không mở chỗ thứ ba.
4. **Bắt đầu tách Application ngay sau buổi báo cáo 13/11/2026**, theo lộ trình của note refactor. Mỗi giai đoạn gồm một hoặc
   vài PR nhỏ; sau mỗi PR `main` vẫn build, test xanh và chạy được:
   - **Giai đoạn 0:** tạo `TrustCabinet.Application` và thêm vào `.sln`; thêm `IUnitOfWork`, exception lưu trữ,
     `PersistenceExceptionTranslator`; thêm `ArchitectureTests` (reflection, không thêm package) với danh sách ngoại lệ
     chỉ được rút ngắn.
   - **Giai đoạn 1:** chuyển `I*Service`, `*Contracts.cs`, interface repository và port từ Domain sang Application.
     Việc bỏ PdfPig khỏi Domain đã xong ở SCRUM-144.
   - **Giai đoạn 2:** chuyển use case theo module, mỗi module một PR, từ nhỏ đến lớn: Notifications/Audit →
     Authentication/Access/Users → Meetings → Agenda/Participants/Attendance/GuestAccess → Documents/Notes → Votes →
     Calls/Recordings/Transcription/VoiceId → Ai/Summaries/Minutes → Email.
   - **Giai đoạn 3:** hub và publisher ở Web thôi đọc `TrustCabinetDbContext`; endpoint chỉ gọi service Application.
   - **Giai đoạn 4:** danh sách ngoại lệ của `ArchitectureTests` rỗng. Cập nhật AGENTS.md, `03-co-so-du-lieu.md`,
     package diagram và sơ đồ kiến trúc sang bốn project, viết ADR mới thay ADR này.
5. **Không đổi gì ra ngoài.** Không đổi endpoint, response, mã `MSGnn`, schema hay migration. Test integration hiện có
   phải xanh mà không sửa assertion.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Kết luận |
|---|---|---|---|
| A — Giữ ba project đến buổi báo cáo, tách theo module ngay sau đó | Báo cáo và sơ đồ khớp code; `main` ổn định lúc demo; feature đang mở không bị xung đột | Đến hết đợt báo cáo, use case vẫn dính EF/Npgsql | **Chọn** |
| B — Tách Application ngay trước buổi báo cáo | Báo cáo được "Clean Architecture" | Đụng 27 service và 85 file test trong vài ngày; xung đột với PR #292 và SCRUM-33/37; nếu dở dang thì code, sơ đồ và báo cáo lệch nhau đúng lúc trình bày | Bỏ |
| C — Tạo project Application rỗng trước, chuyển dần sau | Sơ đồ có bốn khung | Vẽ một tầng không có gì là tả sai code; không giảm được phụ thuộc nào | Bỏ |
| D — Giữ ba project luôn, không tách | Không tốn công | Không unit test được nghiệp vụ nếu thiếu Postgres; `03-co-so-du-lieu.md` đã dự tính tách | Bỏ |

## Hệ quả

- Ở buổi báo cáo, kiến trúc được trình bày đúng là ba tầng: Web (HTTP, SignalR, worker), Infrastructure (use case,
  EF Core và adapter), Domain (mô hình và luật). Kèm theo kế hoạch tách Application và lý do chưa tách.
- [Package diagram](../15-package-diagram/README.md) và [sơ đồ kiến trúc](../16-architecture-diagram/README.md) được
  vẽ lại theo `main` ngày 09/11/2026. Sau giai đoạn 4 phải vẽ lại một lần nữa.
- SCRUM-143 tách thành ticket theo từng giai đoạn sau buổi báo cáo. Ai phụ trách module nào ở giai đoạn 2 thì nhóm BE
  chốt lúc đó.
- Từ giờ đến lúc tách, review PR backend kiểm thêm điểm 3 của Quyết định.
