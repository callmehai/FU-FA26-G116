# Trạng thái thực thi — cập nhật 15/10/2026

Plan bên dưới được giữ làm baseline đã duyệt. Implementation Backend, migration và tests đã được tạo trên `feat/scrum20-Backend-Invite-participants-and-acept`, chưa commit/merge/deploy. Contract thực tế: [API](../03-api/09-thanh-phan-tham-du.md), [FE/manual/Postman](../03-api/09-thanh-phan-tham-du-ban-giao.md), [UC-04](../03-api/09-thanh-phan-tham-du-guest-uc04.md), [báo cáo kiểm thử](../03-api/09-thanh-phan-tham-du-ket-qua-kiem-thu.md). Các đoạn “chưa implement” và checklist trong bản plan mô tả thời điểm lập kế hoạch; kết quả thực tế nằm trong báo cáo, không dùng chúng để suy Guest E2E đã hoàn tất.

# SCRUM-20 — Kế hoạch triển khai Backend thành phần và phản hồi lời mời

Ngày lập: **14/10/2026**. Ticket: [SCRUM-20](https://sep490g116.atlassian.net/browse/SCRUM-20).

**Đây là implementation plan, chưa phải báo cáo đã triển khai hoặc đã chạy test.** Các tên file mới, endpoint, DTO, migration và lựa chọn kỹ thuật bên dưới là thiết kế đề xuất cho lần implement này. Những mục ghi “hiện có” đã được đối chiếu với code của branch.

Branch thực hiện: `feat/scrum20-Backend-Invite-participants-and-acept`. Làm trực tiếp trên branch này; không tạo branch thay thế, không tự commit, push, merge, deploy hoặc sửa `main`. Backend trước; thành viên khác làm Frontend. Không sửa `frontend/` trong phạm vi này; bàn giao contract và ví dụ request/response cho người đó.

## 1. Baseline và nguồn yêu cầu

### 1.1. Quyết định đã chốt với người phụ trách

1. Dùng quyết định PO ngày 13/10/2026, UC/BR hiện hành và ADR-0016/0017 làm baseline. Jira SCRUM-20 đọc qua connector đã cập nhật đúng baseline này, đang ở trạng thái **Backend**, không có comment hoặc attachment tại thời điểm kiểm tra. Không tiếp tục coi description Jira là bản yêu cầu delegation cũ.
2. **Tách UC-04**: SCRUM-20 làm roster, lời mời, phản hồi và contract tích hợp Guest; OTP, profile tự khai, cấp token/session production thuộc công việc UC-04 riêng.
3. Lần này chỉ mở hai quyền bổ sung **View Meeting Participants** và **View Invitation Responses** cho Member/Guest. Kiến trúc phải thêm được quyền khác sau này; đây là phạm vi iteration, không sửa BR-174/ADR-0016 thành giới hạn vĩnh viễn hai quyền.

### 1.2. Bộ tài liệu phải dùng khi implement

| Nguồn | Nội dung ràng buộc với SCRUM-20 |
|---|---|
| [AGENTS.md](../../AGENTS.md), [workflow](../DEVELOPMENT_WORKFLOW.md) | Convention, ngôn ngữ, server authorization, BE → FE → BA Final Review → Tester, cách ghi nhận thay đổi requirement |
| [Master UC](../01-requirements/00-master-use-case-inventory.md), [UC specifications](../01-requirements/05-use-case-specifications.md) | UC-27, UC-28a/b/c, UC-29, UC-30 là phạm vi trực tiếp; UC-04 là dependency; UC-20/21 là điểm tích hợp readiness |
| [Business Rules](../01-requirements/06-business-rules.md) | BR-78–88, 90–97, 100–106, 115–127, 129, 148–157, 168–169, 174–178, 180–182 |
| [Rà soát Guest và điểm danh](../01-requirements/07-ra-soat-guest-va-diem-danh.md) | Phân biệt response, attendance, Guest identity và permission |
| [Sequence FE-06](../06-sequencediagram/meeting-participant-management/README.md) | Đã có đủ nguồn UC27, UC28a, UC28b, UC28c, UC29, UC30; phải đối chiếu từng normal/alternative/exception flow |
| [UC04 sequence](../06-sequencediagram/auth/UC04_AccessMeetingViaGuestInvitation.txt), [UC20 sequence](../06-sequencediagram/meeting-management/UC20_PublishMeeting.txt) | Xác minh email Guest trước khi có access; chỉ gửi lời mời khi công bố hoặc thêm sau công bố |
| [Class diagrams](../07-classdiagram/README.md), `feature-06-meeting-participant-management-class-diagram.drawio` | Mô hình phân tích có lịch sử assignment và Guest riêng. Không bắt code phải có đúng mọi tên controller/repository trong hình |
| [State machines](../10-statemachine/README.md) | SM-03 assignment, SM-04 response, SM-05 Guest; tài liệu draft, đọc cùng UC/BR đã chốt |
| [ERD](02-erd.md), [database](03-co-so-du-lieu.md) | Phân biệt schema hiện tại và schema đích; migration code-first, Guid v7, enum string, không User giả cho Guest |
| [Vòng đời](01-vong-doi-cuoc-hop.md), [business flow](04-luong-nghiep-vu-chinh.md) | Bảy trạng thái, WaitingApproval đóng băng roster; phân biệt preparation và execution |
| [ADR index](../05-decisions/README.md) | Đọc chuỗi thay thế: 0004; 0005/0006/0010/0014 bị 0016 thay một phần; 0013 bỏ MeetingSession; 0017 bỏ gate attendance/vote khi End; 0018 thuộc tài liệu, không migration tài liệu trong task này |
| [Message catalogue](../03-api/00-danh-muc-ma-thong-bao.md) | Nguồn duy nhất của MSG và HTTP; không lấy số message cục bộ từ report/diagram |
| [Server authorization](../03-api/04-phan-quyen-phia-server.md), [Meeting API](../03-api/05-quan-ly-cuoc-hop.md) | Fresh authorization; 404 để giấu tài nguyên; không mở quyền từ system role |
| [SCRUM-19 handoff](../03-api/06-scrum19-ban-giao-tich-hop.md), [notification API](../03-api/06-thong-bao-trong-ung-dung.md) | Publish, lịch sử, recipients, notification target |
| [Email queue](../03-api/07-gui-email-thu-moi.md), [email FE note](../03-api/07-gui-email-thu-moi-note-cho-fe.md) | Queue bền vững hiện có; HTTP thành công không đồng nghĩa SMTP giao thư thành công |
| [Agenda API](../03-api/08-chuong-trinh-nghi-su.md), [Agenda handoff](../03-api/08-chuong-trinh-nghi-su-ban-giao.md), [Guest adapters](../03-api/08-agenda-contract-tich-hop-guest-uc04.md) | Thu hồi quyền phải áp dụng cho Agenda/Hub; consumer contract không thay thế UC-04 production |
| [Report 3](../08-reports/Report-3_Software_Requirement_Specification.md), [report instructions](../08-reports/README.md) | Đối chiếu phần FE-06, permission matrix, yêu cầu phi chức năng; không sửa riêng Markdown snapshot hoặc đồng bộ lại cả báo cáo ngoài phạm vi |
| [CI](../../.github/workflows/ci.yml), [Postman](../03-api/postman/README.md), [deploy](../../deploy/README.md) | Build warnings-as-errors, PostgreSQL/Redis thật, kiểm thử local; server chung chỉ theo main |

**Tình trạng tài liệu:** không thiếu UC description hoặc sequence nguồn của UC-27/28a/28b/28c/29/30 và UC-04. Catalogue quyền đầy đủ của BR-174 còn mở trong tài liệu; quyết định ở §1.1 giải quyết phạm vi hiện tại. Không bịa schema OTP/profile hoặc thời hạn Guest để lấp phần UC-04 đã tách.

**Phạm vi đọc và xác minh:** đã rà inventory `docs/`, đối chiếu các nguồn trực tiếp và các điểm tích hợp trong bảng trên. Các report tổng hợp, lịch sử sửa docs, bản DOCX/PPTX và toàn bộ hình của những feature khác không được coi là đã kiểm chứng từng trang chỉ vì đã liệt kê file. Trước khi coding, kiểm tra lại các nguồn bị thay đổi kể từ ngày lập plan; nếu phát hiện yêu cầu liên quan chỉ có trong nguồn chưa đối chiếu hoặc thiếu artifact, báo người phụ trách trước khi tự quyết nghiệp vụ. Không dùng câu “đã đọc toàn bộ tài liệu dự án” làm bằng chứng nghiệm thu.

### 1.3. Những chỗ cần phân biệt tài liệu và code

- README/AGENTS còn đoạn mô tả skeleton; code hiện đã có authentication, meeting workflow, notifications, email và Agenda. Tiếp nối các service này.
- Jira đã bỏ delegation. Code/schema vẫn còn `MeetingRole.Guest`, `InvitationResponse.Delegated`, `DelegatedToUserId`: đây là legacy, không phải lý do mở API delegation.
- ERD là schema đích ở nhiều chỗ. `MeetingGuestInvitation` và trạng thái removed chưa tồn tại trong schema hiện tại.
- Ghi chú Agenda đang gộp “UC-04/SCRUM-20” cho OTP/session. Khi implement cập nhật handoff thành dependency UC-04 riêng theo quyết định người phụ trách.
- UC20 sequence còn dùng MSG46 cho nhiều lỗi readiness; code/catalogue hiện có MSG137 cho thiếu Chair/Secretary. Giữ catalogue và contract hiện hành, ghi điểm lệch để BA final review; không đổi MSG chỉ để giống diagram.

## 2. Kết quả phải có và giới hạn nghiệm thu

### 2.1. Trong SCRUM-20 Backend

- Đọc/tìm kiếm roster hiện tại; đọc phản hồi/tổng hợp với quyền riêng biệt.
- Organizer khởi tạo/phục hồi đúng một Secretary khi đủ điều kiện.
- Secretary thêm batch internal; tạo Guest invitation; đổi role/grants; thay Chair nguyên tử; thay Secretary cuối nguyên tử; gỡ/revoke mềm.
- Người được mời phản hồi lời mời của chính mình Accepted/Declined; lưu giá trị mới nhất và thời gian.
- Persistence, authorization, kiểm tra concurrency và notification/email tích hợp với hệ thống hiện có.
- Sửa các query cũ để assignment đã gỡ không còn được tính quyền, readiness hoặc recipients.
- Contract Guest fail-closed và test consumer; tài liệu API, collection Postman, test report, handoff FE/UC-04/SCRUM-26/phòng họp.

### 2.2. Ngoài phạm vi

Không làm UI hoặc TS wrapper; không OTP/profile/session production; không API điểm danh UC-31/SCRUM-26; không delegation; không LiveKit/room UC-37–41; không thêm Start/End Meeting endpoint; không triển khai voting/documents hoặc migration ADR-0018; không thêm org unit, tenant, task tracking hay hệ thống capability mới cho roster.

### 2.3. Hai mốc nghiệm thu tách biệt

| Mốc | Được tuyên bố | Không được tuyên bố |
|---|---|---|
| BE độc lập SCRUM-20 | Internal hoàn chỉnh; CRUD Guest invitation; Guest response/read qua contract test; delivery intent Guest được lưu để tích hợp | OTP chạy thật, Guest tự mở link và phản hồi end-to-end, media disconnect thật |
| Sau tích hợp UC-04 và room | Guest dùng email/link/OTP/session thật; revoke/expiry chặn request và event; room xử lý thu hồi theo contract | Không suy từ JWT giả lập hoặc fake adapter ra production đã an toàn |

SCRUM-20 không được chuyển **Done toàn bộ trải nghiệm Guest** khi UC-04 còn thiếu. Việc tách task không có nghĩa bỏ test tích hợp; ghi dependency này trong bàn giao và bản nháp Jira ở §15.

## 3. Bản đồ code và cách tổ chức

Giữ ba project hiện có. Domain chứa entity, DTO, policy và interface; Infrastructure chứa EF/service/queue; Web chứa HTTP/authentication/DI. Không tạo Application project chỉ cho task này.

### 3.1. File mới dự kiến

| File/thư mục dưới repository root | Trách nhiệm cụ thể |
|---|---|
| `backend/TrustCabinet.Domain/Participants/ParticipantContracts.cs` | DTO/request/result/outcome; discriminated identity Internal/Guest; không đưa EF hoặc ClaimsPrincipal vào Domain |
| `...Domain/Participants/ParticipantRules.cs` | State matrix, recovery, final-roster invariants, response eligibility; pure functions |
| `...Domain/Participants/MeetingPermissionCatalog.cs` | Hai permission key và grant policy của iteration; dễ bổ sung mục mới |
| `...Domain/Participants/IParticipantService.cs` | Các use case ở §6, CancellationToken cho mọi I/O |
| `...Domain/Participants/IParticipantRepository.cs` | Query/projection, meeting lock, transaction, truy vấn current và history tường minh |
| `...Domain/Participants/ParticipantGuestAccess.cs` | Identity/session access contract và profile projection tối thiểu từ UC-04 |
| `...Domain/Entities/MeetingGuestInvitation.cs` | Lời mời Guest riêng biệt, response, revoke, evidence |
| `...Domain/Entities/MeetingParticipantPermission.cs` | Grant theo assignment/invitation; không theo User toàn hệ thống |
| `...Domain/Entities/MeetingInvitationDispatch.cs` | Ý định gửi email bền vững sau một thay đổi; không phải SMTP sender thứ hai |
| `...Infrastructure/Configurations/MeetingGuestInvitationConfiguration.cs` | Mapping, constraints/index Guest |
| `...Infrastructure/Configurations/MeetingParticipantPermissionConfiguration.cs` | XOR owner, FK, unique permission |
| `...Infrastructure/Configurations/MeetingInvitationDispatchConfiguration.cs` | Unique event-recipient key, worker indexes |
| `...Infrastructure/Participants/ParticipantRepository.cs` | EF queries, snapshot reads, transaction locking |
| `...Infrastructure/Participants/ParticipantService.cs` | Điều phối auth → validate → thay đổi → audit/dispatch → commit |
| `...Infrastructure/Participants/ParticipantServiceCollectionExtensions.cs` | DI scoped và default Guest deny adapters |
| `...Infrastructure/Email/MeetingInvitationDispatchProcessor.cs` | Materialize intent sang `EmailMessage`, retry/dependency handling |
| `...Web/Endpoints/ParticipantEndpoints.cs` | Map routes, strict JSON, message constants, outcome mapping |
| `...Web/ParticipantGuestIdentity.cs` | Chuyển principal đã xác thực thành Guest identity; mặc định unavailable |
| `...Infrastructure/Seed/ParticipantDevelopmentDataSeeder.cs` | Seed fixture dev có prefix riêng, insert-only, không sửa meeting người khác |
| `tests/TrustCabinet.Web.Tests/Participants/` | Unit, endpoint, PostgreSQL, migration, concurrency, Guest contract, integration/performance tests |

Ký hiệu `...Domain`, `...Infrastructure`, `...Web` ở bảng là viết tắt của các project cùng prefix `backend/TrustCabinet.`; không tạo thư mục có tên dấu ba chấm.

### 3.2. File hiện có phải sửa có chủ đích

- `Domain/Entities/MeetingParticipant.cs`, `Meeting.cs`; `Infrastructure/TrustCabinetDbContext.cs`; configurations tương ứng; migration mới và snapshot.
- `Domain/Meetings/MeetingReadPolicy.cs`, `MeetingInvitationRecipientPolicy.cs`, `Agenda/AgendaRules.cs`: thêm current-assignment predicate dùng được trong LINQ-to-EF.
- `Infrastructure/Meetings/MeetingRepository.cs`, `MeetingService.cs`: include/query current phù hợp, publish readiness, snapshot notification/dispatch trong transaction.
- `Infrastructure/Notifications/NotificationRepository.cs`: reminder recipients chỉ current; `NotificationService.cs` kiểm target quyền hiện tại.
- `Infrastructure/Email/MeetingInvitationQueue.cs`, `EmailDeliveryWorker.cs`, DI email: sửa reconciliation theo recipient, tích hợp dispatch processor; tái sử dụng `EmailQueueProcessor` và SMTP sender.
- `Web/Program.cs`: đăng ký service và `MapParticipantEndpoints()`; không nhét toàn bộ logic vào Program.
- `Web/MeetingHub.cs`/Agenda publisher: sửa nếu shared read policy chưa đủ; giữ group authorization theo user/session, không broadcast toàn meeting.
- Các test hiện có cho Meeting/Agenda/Email/Notifications/model/catalogue phải được cập nhật kỳ vọng khi contract thay đổi hợp lệ.

Tên file là kế hoạch; trước khi thêm, kiểm tra branch có file tương đương mới chưa. Tái sử dụng phần tương đương thay vì tạo service trùng trách nhiệm.

## 4. Ma trận nghiệp vụ để chuyển trực tiếp thành test

### 4.1. Trạng thái

| Hành động | Draft | WaitingApproval | Rejected | Approved | Published | InProgress | Ended |
|---|---|---|---|---|---|---|---|
| Current active Secretary quản lý roster/grants | Có | Không | Có | Có | Có | Có | Không |
| Organizer phục hồi nếu không còn current Secretary | Có | Không | Có | Có | Không | Không | Không |
| Current active Secretary đọc roster | Có | Có | Có | Có | Có | Có | Có |
| Current active Chair đọc roster/responses mặc định | Không | Không | Không | Không | Có | Có | Có |
| Current Member/verified Guest đọc roster có grant tương ứng | Không | Không | Không | Không | Có | Có | Có |
| Current Member/verified Guest đọc responses có grant tương ứng | Không | Không | Không | Không | Có | Có | Có |
| Secretary đọc responses | Không | Không | Không | Không | Có | Có | Có |
| Owner trả lời lời mời | Không | Không | Không | Không | Có | Có | Không |
| Gửi lời mời khi thêm mới | Không | Không | Không | Không | Có | Có | Không thêm được |

Guest còn phải qua kiểm tra session/email verification/expiry/revoke của UC-04 ở mỗi request. “Có grant” không vượt qua giới hạn trạng thái hoặc identity.

### 4.2. Quyền và role

1. `CurrentInternal` = assignment chưa removed và không phải legacy Guest/delegation; **không gộp** `User.IsActive` vào định nghĩa roster current. Roster vẫn có thể hiển thị người đã vô hiệu hóa để Secretary xử lý; actor/access và readiness phải thêm điều kiện active.
2. `CurrentGuest` = invitation chưa revoked. Access hết hạn không tự xóa lịch sử hoặc âm thầm loại khỏi roster; permission access luôn còn phải kiểm expiry. Không tự cho mời trùng chỉ vì access cũ hết hạn: revoke trước, tạo invitation mới sau.
3. Mỗi current internal có một role `Chair`, `Secretary` hoặc `Member`. Không nhận số enum, `Guest`, system-role ID hoặc `Delegated` từ API.
4. Tối đa một current Chair, kể cả current Chair có account inactive. Publish/Start đòi Chair active và ít nhất một Secretary active; Declined không làm mất role và không chặn readiness.
5. Published/InProgress không được bỏ Chair; thay Chair phải outgoing → Member, incoming → Chair trong cùng transaction. Không tự chọn người thay.
6. Published/InProgress phải giữ ít nhất một current Secretary trong final roster; demote/remove Secretary cuối bị từ chối nếu không có replacement trong cùng thao tác role. Draft/Rejected/Approved được tạm thời không có Secretary.
7. Organizer chỉ recovery khi **không có current Secretary**, không phải chỉ “không có Secretary active”. Nếu còn Secretary inactive, không tự diễn giải thành quyền recovery; xử lý account/roster theo quyền hiện hành.
8. Recovery chọn một internal active chưa current hoặc current Member. Nếu đã là Member: dùng lại assignment ID, giữ response/time/attendance/grants tương thích. Không chọn current Chair, không thêm grants, không add batch.
9. System Admin, Organizer, Approver hoặc system role mang tên Secretary không tự có quyền quản lý/đọc roster. Một người có nhiều quan hệ được cộng quyền hợp lệ: Organizer đồng thời là current Secretary có quyền Secretary.
10. `ViewMeetingParticipants` và `ViewInvitationResponses` là **hai permission key đề xuất cho wire contract**, không phải system capability. Member/Guest được cấp; Chair/Secretary có quyền mặc định nên không được tạo grant mới dư thừa trong iteration này. Grant Member cũ còn tương thích khi được promote thì giữ, khi demote vẫn có tác dụng. Giữ nguyên tập grant cũ nếu request không sửa grants.
11. View responses cho phép tên/email trong response rows, không cho gọi roster, search hoặc editor options. View roster không tự cho response/time/totals. Tránh dùng chung DTO làm lộ dữ liệu.
12. Gỡ assignment không xóa quyền riêng của Organizer/Approver. Những quan hệ riêng đó cũng không tự mở UC-27/30.

## 5. Data model và migration

### 5.1. Thay đổi entity

**MeetingParticipant hiện có:** thêm `AssignmentStatus` (`Current|Removed`), `RemovedAt`, `RemovedById` nullable; nav remover dùng FK restrict. Giữ `Response`, `RespondedAt`, `Attendance`, `AttendanceRecordedById/At` và ID cũ. Thêm `InitialInvitationDeliveryKey` nullable để giữ dedup tương thích khi nâng cấp. Keys/timestamps theo convention Entity và `TimeProvider`.

**Meeting:** thêm `ParticipantsRevision` kiểu `long`, mặc định 0, check không âm. Chỉ tăng một lần mỗi transaction có thay đổi roster/role/grants/revoke; không tăng khi response thay đổi hoặc request no-op. Đây là revision cho optimistic concurrency, không thay thế meeting row lock.

**MeetingGuestInvitation:**

| Field | Kiểu / quy tắc |
|---|---|
| Id, CreatedAt, UpdatedAt | Theo Entity hiện có |
| MeetingId | FK Meeting, bắt buộc |
| InvitedName | string 1–200 sau trim; giữ nguyên evidence khi UC-04 có display name khác |
| Email | string tối đa 256 sau trim; `citext` như UserConfiguration hiện có; kiểm format theo cùng quy tắc User, không nhận display-address/CRLF |
| InvitedById, SensitivityAtInvitation | FK User + enum snapshot, bắt buộc; audit cảnh báo Mật/Tối mật |
| Response, RespondedAt | Pending/null lúc mới; sau đó Accepted/Declined + UTC |
| RevokedAt, RevokedById | nullable; revoke giữ row và evidence |
| Attendance, AttendanceRecordedById, AttendanceRecordedAt | Cùng biểu diễn Unknown/Present/Absent hiện có của internal; Unknown mặc định, SCRUM-20 không có setter nghiệp vụ điểm danh |
| InitialInvitationDeliveryKey | Key riêng theo invitation ID |

TokenHash/ExpiresAt/VerifiedAt và session/profile trong ERD đích do UC-04 bổ sung/hoàn thiện migration của UC-04. Không tự sinh fake token, lưu profile đã xác minh giả, hoặc suy ra verified từ việc có email. SCRUM-20 giữ contract trả identity/profile và contract cấp link ở §9 để tránh hai task sở hữu cùng dữ liệu xác thực.

**MeetingParticipantPermission:** Id; `MeetingParticipantId?`; `GuestInvitationId?`; `PermissionKey` string tối đa 100; `GrantedById`; `GrantedAt`. Check chính xác một owner khác null. FK restrict. Unique partial `(MeetingParticipantId,PermissionKey)` và `(GuestInvitationId,PermissionKey)`. Không dùng một GUID owner không có FK, không có hai boolean cứng trong entity. Grant bị thu hồi khỏi tập hiện hành được xóa cùng transaction nhưng trước/sau được audit; khi assignment removed, giữ rows để làm evidence và policy bỏ qua toàn bộ chúng.

**MeetingInvitationDispatch:** bảng intent theo §8, có owner internal XOR Guest, event ID/type, snapshot recipient/meeting, dedup key, trạng thái materialize/retry và EmailMessageId. Thêm vì `EmailMessage` hiện đòi nội dung thư đã render, trong khi Guest link chưa có nếu UC-04 chưa tích hợp. Không nhét JSON nghiệp vụ vào Body rồi để SMTP vô tình gửi JSON đó.

### 5.2. Constraints/index bắt buộc

- Thay unique toàn bộ `(MeetingId,UserId)` bằng unique partial **chỉ current**. Một người bị gỡ rồi mời lại có assignment ID mới, response Pending; row cũ vẫn tồn tại.
- Unique partial `MeetingId` với current role Chair. Khi swap, demote outgoing và flush trước, promote incoming và flush sau **trong cùng transaction** để không vi phạm immediate unique index.
- Check `AssignmentStatus=Current` thì `RemovedAt/RemovedById` null; Removed thì có đủ thời điểm/người gỡ. Quy tắc chuyển dữ liệu legacy phải được kiểm trước khi thêm check.
- Guest unique partial `(MeetingId,Email)` trên cột `citext` khi `RevokedAt IS NULL`; revoke timestamp/by đi cùng nhau. Không thêm cột NormalizedEmail thứ hai dễ lệch với Email; so sánh chéo internal/Guest dùng cùng semantics `citext` của PostgreSQL.
- Index current lookup `(MeetingId,AssignmentStatus)`, response rows theo meeting; FK/index owner của grants; index dispatch pending theo next attempt. Bổ sung index search chỉ khi đo/explain cho thấy cần, không cài extension mới chỉ để search vài trăm người.
- Trùng email internal ↔ Guest là invariant chéo bảng, xử lý dưới **cùng meeting row lock**. Mọi add/revoke/re-add đều đi qua lock này. Không tuyên bố hai unique index riêng tự giải quyết được cross-table race.
- Không global EF query filter che lịch sử: dùng query current rõ ràng; audit/test lịch sử vẫn đọc được row removed.

### 5.3. Migration an toàn với dữ liệu đang có

1. Sinh migration mới `MeetingParticipantLifecycleAndGuestInvitations` bằng EF; không sửa migration đã vào main.
2. Preflight bằng SQL **trong migration transaction**: phát hiện nhiều current Chair, legacy `Role=Guest`, `Response=Delegated` hoặc `DelegatedToUserId` khác null. Nếu có: abort với thông báo rõ và báo người phụ trách để chốt mapping riêng. Không tự đổi Delegated → Pending, Guest → Member, xóa người hoặc reset attendance để migration chạy xanh.
3. Với dữ liệu hợp lệ: backfill existing assignment thành Current; removed fields null; revision 0; giữ ID/FK/response/time/attendance và mọi dữ liệu khác.
4. Existing assignment dùng delivery key legacy `meeting:{meetingId}:published:{userId}`; assignment tạo mới dùng `participant:{assignmentId}:invited`. Guest dùng `guest:{invitationId}:invited`. Nhờ đó không gửi lại toàn bộ email cũ và mời lại sau removal vẫn tạo event mới.
5. Thêm tables/config/index/check; giữ legacy enum/columns để đọc history và rollback ứng dụng khi cần, nhưng write path mới không nhận chúng. Việc xóa hẳn legacy columns là migration riêng sau khi có evidence sạch.
6. Down migration có thể mất data mới và unique cũ không nhận nhiều historical assignments. Thêm guard từ chối Down khi đã có dữ liệu không thể bảo toàn; không xóa row để ép rollback. Deployment rollback thực tế ưu tiên forward fix/backup restore có chủ đích.
7. Chạy upgrade từ migration hiện tại `AgendaExecutionInvariants`, upgrade từ DB trắng, và kiểm data preservation trên PostgreSQL thật. Chỉ dùng database tạm của test, không `database update` vào server test chung.
8. Cập nhật ERD markdown phần “hiện có/đích”, không tự regenerate toàn bộ `.drawio`. Nếu phải sửa drawio trong implementation: đọc skill/rules, giữ IDs/relationships, render và validate theo AGENTS.

## 6. Contract HTTP đề xuất để FE làm độc lập

Base: `/api/meetings/{meetingId}`. GUID route. String enum đúng PascalCase; tên JSON camelCase. Các response datetime là ISO-8601 UTC. Các URL dưới đây chưa tồn tại cho tới khi implement.

### 6.1. Quy ước chung

- Bearer authentication như hệ thống hiện có. Internal token → fresh account active check; Guest token → resolver và session access adapter. Không lấy actorId/userId từ body để cấp quyền.
- GET nhạy cảm trả `Cache-Control: no-store`. Không trả entity trực tiếp, password/hash, token/session secret, audit payload hoặc navigation.
- Query page mặc định 1, pageSize 20, tối đa 100. q trim tối đa 200; escape wildcard `%`, `_` khi muốn tìm literal bằng ILIKE; parameterized query. Sort cố định theo tên chuẩn hóa rồi kind rồi ID để phân trang ổn định.
- Roster/options dùng q. Responses chỉ page/pageSize, **không q hoặc search theo tên/email**; từ chối query search thay vì vô tình expose search qua quyền responses.
- GET context và roster trả `ETag: "participants-<revision>"`; mọi roster mutation gửi `If-Match` giá trị đã đọc. Thiếu/sai định dạng → 400/MSG276 (request metadata không hợp lệ); stale → 409/MSG275 (mã mới dự kiến). Chỉ nhận một strong ETag đúng định dạng; reject `*`, weak ETag hoặc danh sách nhiều tag. Đọc lại authority/state trước khi trả stale; không lộ revision cho người mất quyền.
- Không dùng revision cho UC-29: response dùng serialization theo meeting lock và giá trị mới nhất. Role/grant mutation không được đè response vừa lưu.
- Strict JSON theo `AgendaEndpoints`: tối đa 64 KiB kể cả chunked, đúng content-type, reject unknown/duplicate property, wrong casing/type, enum numeric, null bắt buộc. Không dùng model binding trả lỗi business trước authorization. Parse thành result rồi service quyết định thứ tự thông báo.
- Technical limits: batch add tối đa 100 user IDs; changes tối đa 100 current assignments; name/email giới hạn ở §5. Đây là giới hạn request đề xuất, không phải luật cap tổng số người của meeting; FE có thể chia batch.

### 6.2. Routes và DTO tối thiểu

| Method/path sau base | Người được gọi / nội dung |
|---|---|
| GET `/participant-context` | Người có quyền đọc meeting hoặc Guest hợp lệ. Trả status, revision, cờ hành động của **chính caller**; không tên/email/count roster |
| GET `/participants?q=&page=&pageSize=` | UC-27 có quyền; chỉ current Internal/Guest |
| GET `/participants/editor-options?q=&page=&pageSize=` | Current Secretary trong state editable hoặc Organizer recovery hợp lệ; options active internal, role/perms có thể chọn |
| POST `/participants/secretary-recovery` | Organizer recovery, `{ "userId": "guid" }`; server tự gán Secretary, không role/grants từ client |
| POST `/participants` | Secretary, `{ "userIds": ["guid"], "role": "Member", "additionalPermissions": [] }` |
| PUT `/participants/assignments` | Secretary, đổi một/nhiều current assignment trong một transaction; schema §6.3 |
| DELETE `/participants/{participantId}` | Secretary; If-Match, không body, soft remove |
| POST `/guest-invitations` | Secretary, `{ "invitedName": "Khách A", "email": "guest@example.test", "additionalPermissions": [] }` |
| PUT `/guest-invitations/{invitationId}/permissions` | Secretary, `{ "additionalPermissions": ["ViewMeetingParticipants"] }`; không nhận role |
| DELETE `/guest-invitations/{invitationId}` | Secretary; If-Match, revoke |
| GET `/invitation-response` | Own current internal invitation hoặc Guest session hợp lệ; không nhận target ID |
| PUT `/invitation-response` | Own identity, `{ "response": "Accepted" }` hoặc Declined |
| GET `/invitation-responses?page=&pageSize=` | UC-30 matching permission, names/emails + response/time + current totals |

`participant-context` trả ví dụ:

```json
{
  "meetingId": "<guid>",
  "status": "Published",
  "revision": "12",
  "permissions": {
    "canViewParticipants": true,
    "canViewResponses": true,
    "canManageParticipants": true,
    "canRecoverSecretary": false,
    "canRespond": true
  }
}
```

Revision là chuỗi decimal để FE không phụ thuộc giới hạn integer JavaScript. Organizer chưa có roster access vẫn nhận context và recovery options nếu đúng điều kiện, nhưng gọi roster/responses nhận 404. Context không trả current Chair ID hay người nào cho caller thiếu quyền.

Roster response `{items,total,page,pageSize,revision}`. Mỗi item có `id`, `kind: Internal|Guest`, `displayName`, `email`, `role: Chair|Secretary|Member|null`. Internal có `userId`, `isActive`; Guest có `invitedName`, `selfDeclaredDisplayName`, `selfDeclaredJobTitle` nullable. UC-04 chưa có profile thì dùng invitedName cho displayName, các field tự khai null; không gắn nhãn tên đã xác thực. Chỉ Secretary được trả `additionalPermissions` và cờ edit của từng item; những caller khác không cần thấy grants của người khác. **Roster DTO không có response/RespondedAt/attendance**.

Options response `{items,total,page,pageSize,mode,roles,permissionCatalog,revision}`. Mode `Manage` hoặc `RecoverSecretary`; recovery chỉ chọn một người, role list chỉ Secretary, permissionCatalog rỗng. Chỉ trả id/name/email cần chọn. Không reuse `/api/users` vì endpoint đó đòi quyền quản trị. Options không phải authority: service phải kiểm lại từng ID lúc submit.

Own-response DTO `{id,kind,response,respondedAt,canRespond}`. GET được đọc sau công bố kể cả Ended nếu relationship còn hợp lệ; canRespond false ở Ended. Người không có own invitation không nhận thông tin lời mời của người khác.

Responses response `{items,total,page,pageSize,totals}`; item chỉ `{id,kind,displayName,email,response,respondedAt}`. Totals `{pending,accepted,declined,total}` tính trên toàn bộ current invitees của meeting, không chỉ trang hiện tại. Không trả grants/role editor/attendance. Dùng một database snapshot thống nhất cho rows/count/totals để không trả tổng lệch khi có response đồng thời.

### 6.3. Đổi role/grants nguyên tử

Request thường:

```json
{
  "changes": [
    {
      "participantId": "<current-member-id>",
      "role": "Member",
      "additionalPermissions": ["ViewMeetingParticipants", "ViewInvitationResponses"]
    }
  ],
  "chairReplacement": null
}
```

`role` bắt buộc cho mỗi internal change. `additionalPermissions` là optional: **omit = giữ nguyên**, `[]` = thu hồi toàn bộ optional grants, array = tập cuối. Không dùng null để ám chỉ giữ nguyên. Reject duplicate keys/permission/participant IDs. Chỉ đổi grants cho Member hiện tại; promote/demote có thể giữ tập compatible sẵn có. Nếu request vừa đổi role vừa đặt grants, validate tập cuối theo role cuối và catalogue iteration, không âm thầm bỏ grant không hợp lệ.

Thay Chair cần hai change rõ ràng và xác nhận đúng cặp đã xem:

```json
{
  "changes": [
    { "participantId": "<old-chair-id>", "role": "Member" },
    { "participantId": "<new-chair-id>", "role": "Chair" }
  ],
  "chairReplacement": {
    "outgoingParticipantId": "<old-chair-id>",
    "incomingParticipantId": "<new-chair-id>",
    "confirmed": true
  }
}
```

Nếu incoming đang là Secretary cuối của Published/InProgress, request phải kèm change thứ ba promote một current Member khác thành Secretary. Đổi Secretary cuối sang Member cũng phải kèm replacement trong cùng `changes`. Tính final roster trước khi save; không xử lý change array thành các request độc lập. Endpoint này không thêm người mới hoặc xóa assignment; thêm replacement mới trước bằng POST, sau đó thay role. Muốn remove Chair phải swap trước rồi remove former Chair.

Return `200 {code,message,revision}` và ETag mới: MSG100 nếu có role change; MSG153 nếu chỉ grants; MSG154 nếu không thay đổi. Không trả full roster vì caller có thể vừa tự demote và mất quyền đọc. Frontend tải lại context trước, chỉ GET các view còn được phép.

### 6.4. Thành công và lỗi

| Trường hợp | HTTP/code đề xuất |
|---|---|
| Add internal/Guest, recovery thêm assignment mới | 201/MSG96; `{code,message,createdIds,revision}` |
| Recovery promote Member, role change | 200/MSG100 |
| Remove/revoke | 200/MSG103 |
| Own response saved | 200/MSG105 kèm own-response DTO |
| Permissions only | 200/MSG153 |
| No-op role/grants; repeat own response cùng giá trị | 200/MSG154; không đổi timestamp, không phát notification |
| Đọc dữ liệu lỗi | 500/MSG95; không trả empty-success |
| Selection/batch/Guest input sai | 400/MSG97; field errors không chứa dữ liệu người ngoài meeting |
| State không cho sửa roster | 409/MSG98 |
| Persistence add thất bại | 503/MSG99 |
| Role/schema change request sai | 400/MSG101 |
| Persistence role/grants thất bại | 503/MSG102 |
| Persistence remove/revoke thất bại | 503/MSG104 |
| Response value/schema sai | 400/MSG106 |
| Own invitation còn đọc được nhưng state không còn answerable | 409/MSG107 |
| Persistence response thất bại | 503/MSG108 |
| Final roster vi phạm required roles/Chair invariant | 409/MSG109 |
| Grant không hợp lệ | 400/MSG152 |
| Stale roster revision | 409/MSG275, dự kiến: “Danh sách thành phần đã thay đổi. Vui lòng tải lại và thử lại.” |
| Query/header request sai, pagination sai, thiếu/sai If-Match | 400/MSG276, dự kiến: “Yêu cầu thành phần cuộc họp không hợp lệ.”; errors chỉ rõ field/header |
| Payload quá lớn / content type không hỗ trợ | 413/MSG87; 415/MSG34, dùng đúng catalogue hiện có |

Khi giải quyết merge với main `3790928` ngày 19/10/2026, MSG252–MSG274 đã được dành cho thay đổi Report 3; MSG238/MSG239 giữ cho liên kết tài liệu theo ADR-0020. SCRUM-20 dùng MSG275 (stale revision), MSG276 (query/If-Match sai), mã tiếp theo MSG277. Catalogue giữ nguyên phân bổ FE-07/FE-11.

401 token thiếu/sai/hết hạn; active-user gate theo convention hiện có. 404 body rỗng khi meeting/assignment/invitation không tồn tại, không thuộc meeting, không có quyền đọc dữ liệu cụ thể, Guest expired/revoked/unverified. Chỉ dùng 403 body rỗng cho caller đã được phép đọc meeting nhưng không có quyền thực hiện hành động quản lý đó; không để 403 tiết lộ meeting mà caller chưa biết. Không trả MSG107 kèm tên/email cho Guest session đã mất hiệu lực.

Khi code, thêm section “SCRUM-20 implemented” trong catalogue với **từng cặp HTTP/code** ở trên, hằng số đầu endpoint file và ít nhất một test assertion cho mỗi mã. Không coi bảng plan là thay thế catalogue.

## 7. Thuật toán service và concurrency

### 7.1. Khung chung của mọi mutation

Thực hiện theo thứ tự dưới đây; không đảo validation dữ liệu lên trước authorization:

```text
Authenticate request; parse bounded input into a result, not an early business response.
Begin ReadCommitted transaction using the existing meeting transaction abstraction.
Lock Meeting by ID with SELECT ... FOR UPDATE.
Read actor and current assignments from this transaction; revalidate active identity.
If meeting is missing or caller cannot know it: return NotFound.
Check operation authority, then allowed MeetingStatus.
For roster mutations: validate If-Match and compare ParticipantsRevision.
Read current target assignments, guests, grants and selected users.
Validate input, ownership, uniqueness, grant policy and proposed final roster.
If unchanged: return NoChanges without timestamp/revision/notification changes.
Stage domain changes, audit evidence and notification/dispatch intents.
Save all changes; flush demotions first when a Chair swap needs it.
Increment ParticipantsRevision once for roster mutations.
Commit once. Only then return success / trigger best-effort worker wake-up.
```

- Reuse `MeetingRepository.InTransactionAsync`/`FindWorkflowAsync(..., forUpdate:true)` hoặc extension tương đương có cùng connection/DbContext. Mọi writer trên một meeting phải dùng cùng lock, gồm Publish, roster, response và reconciliation. Không dùng một lock memory vì nhiều process không chia sẻ nó.
- Quy tắc lock: meeting trước; selected User rows theo ID tăng dần nếu cần khóa để chống deactivate/email edit trong validation; assignment/grant/dispatch sau. Kiểm writer User hiện có không tạo vòng lock ngược. Không giữ row lock lúc SMTP, SignalR hoặc I/O ngoài database.
- Account inactive sau thời điểm transaction đã commit là một thay đổi độc lập hợp lệ; mọi request tiếp theo kiểm active lại. Không cấp quyền vĩnh viễn từ snapshot token.
- `ReadCommitted` + meeting lock serialize writes; read rows/count/totals nhiều query dùng một read-only `RepeatableRead` transaction hoặc một SQL projection có snapshot thống nhất. Không trộn count trước commit khác với page sau commit.
- Catch các lỗi persistence dự kiến như Agenda: `DbUpdateException`, `NpgsqlException`, `TimeoutException`; rollback rồi map failure theo operation. Unique violations của đúng index biết tên → duplicate/invariant conflict. Không biến mọi exception thành “email trùng”; không swallow cancellation/programming errors.
- `DbContext` sau failure rollback cần dispose/clear tracking trước retry; không dùng object vẫn bị demote trong bộ nhớ để trả success. Mỗi request/worker iteration dùng scoped context.
- Audit row phải nằm trong **cùng transaction**. `AuditService.RecordAsync` hiện tự SaveChanges; có thể gọi khi bảo đảm dùng đúng context/transaction, hoặc thêm API stage audit không tự commit. Không ghi audit thành công ngoài transaction nghiệp vụ thất bại.
- Không thêm `MeetingStatusHistory` khi roster thay đổi vì meeting không chuyển trạng thái. History trạng thái hiện có là append-only.

### 7.2. Recovery Secretary

1. Caller phải là `Meeting.CreatedById` còn active; không dựa system role có tên Organizer. Check state Draft/Rejected/Approved và **current Secretary count = 0** trong lock.
2. Input chỉ một UserId, active. Reject current Chair, Guest ID, current Secretary, user không tồn tại, input role/grants/array lạ.
3. Nếu current Member: đổi chính row sang Secretary; không tạo row; giữ tất cả fields response/attendance và compatible grants. Success MSG100.
4. Nếu chưa current: kiểm trùng email Guest current, thêm current Secretary Pending/null và grants rỗng. Success MSG96.
5. Bump revision, audit recovery cùng transaction. Chưa Published nên không dispatch invitation/role email. Hai Organizer request đồng thời: chỉ một thành công, request còn lại thấy không còn recovery authority/stale; không có hai Secretary được tạo do cùng bootstrap.

### 7.3. Add internal batch / Guest

Internal: kiểm list không rỗng, tối đa 100, không duplicate/empty GUID; query tất cả active users một lần, không N+1. Shared role/grants hợp lệ. Kiểm current user duplicate và email Guest collision cho **mọi** user. Batch role Chair chỉ có một target và không có current Chair; không tự demote Chair khác từ endpoint add. Dựng tất cả rows với ID mới, Pending/null, Unknown attendance. Bất kỳ target sai → không lưu ai.

Guest: chỉ current active Secretary; validate name/email và grants, check Guest/email internal current trùng không phân biệt hoa thường. Lưu invitation, grants, inviter/time/sensitivity snapshot cùng transaction. Không tạo User, system role, MeetingParticipant hoặc internal capability. Không gọi UC-04 để gửi OTP lúc Secretary mời; OTP do Guest chủ động mở luồng xác minh.

Nếu state Published/InProgress, stage invitation events cho người mới; nếu preparation, không tạo event gửi. Không gửi thư chỉ vì nhấn lưu Approved; Publish sẽ tạo event cho toàn bộ current roster.

### 7.4. Role/grants và Chair replacement

1. Load actor/targets và grants current trong lock. Target ID phải thuộc chính meeting và current; lịch sử không phải target hợp lệ.
2. Clone trạng thái role/grants cần tính toán sang một final-state model; apply toàn bộ changes vào model trước khi sửa tracked entities.
3. Validate đủ cặp Chair replacement và explicit confirmation; ID outgoing khớp Chair hiện tại; incoming active và current. Thiếu confirmation không tự suy từ changes.
4. At-most-one Chair mọi trạng thái; Published/InProgress phải có Chair và ít nhất một Secretary. Với assignment mới được promote, User phải active. Không dùng Accepted làm điều kiện role.
5. Chỉ áp dụng grant đã chọn hợp lệ. Nếu giữ nguyên tập existing compatible grants khi promote Chair/Secretary thì cho phép; nếu thêm grant mới cho role đó trong iteration này thì MSG152. Tập quyền mặc định của role không được client thu hồi bằng gửi `[]`.
6. Stage demotion outgoing Chair, SaveChanges trong outer transaction để giải phóng unique index. Sau đó apply incoming và các thay đổi khác, SaveChanges, commit. Nếu bước sau lỗi, rollback phải khôi phục outgoing Chair và mọi side effect.
7. Không đổi response, RespondedAt, attendance, system-role. Emit đúng một event/recipient bị ảnh hưởng sau cùng; Chair swap có cả hai người. No-op không emit gì.
8. Cho phép Secretary tự demote ở preparation hoặc khi final roster sau publication vẫn có Secretary. Quyền request được xét trước mutation; response không tiết lộ roster sau khi caller mất quyền.

### 7.5. Remove/revoke và mời lại

1. Check current authority/state/If-Match và target meeting/current. Với internal, tính final required-role counts. Xóa Chair hiện tại hoặc Secretary cuối Published/InProgress → MSG109.
2. Internal: đặt status Removed + RemovedAt/By, giữ row/response/time/attendance/recordedBy/grants. Guest: đặt RevokedAt/By, giữ invitation và evidence. Không cascade-delete response/comment/ballot/call history.
3. Stage audit + removal notification/email event ở Published/InProgress. Event này không chứa protected access link, online meeting URL hoặc attachment ICS chứa URL.
4. Shared access predicates loại relationship ngay sau commit. Guest access adapter bắt buộc kiểm invitation revoke trực tiếp mỗi lần, nên không chờ worker mới khóa quyền.
5. Callback room sau commit là dependency riêng: fail callback không làm access có hiệu lực lại; room entry/token refresh luôn phải tự kiểm relationship. Xem §10.
6. Re-add cùng User hoặc email sau removal/revoke: tạo ID mới, Pending/null, grants mới theo request. Row/grants/session cũ không hồi sinh. Repeated DELETE target đã removed/revoked → 404; FE reload context sau lỗi mạng để xác định state, không giả định delete thất bại.

### 7.6. Response của chính người được mời

1. Không có target UserId/participantId/invitationId trong body. Internal resolve assignment từ authenticated UserId + MeetingId + Current; Guest từ verified access identity + MeetingId + InvitationId.
2. Sau meeting lock, revalidate internal active/current hoặc Guest access còn hợp lệ; kiểm Published/InProgress.
3. Nhận đúng Accepted/Declined. Reject Pending, Delegated, số enum, extra role/attendance/grants hoặc foreign identity. Email trong body không phải chứng minh ownership.
4. Nếu giá trị khác hiện tại, cập nhật Response/RespondedAt bằng cùng `now` UTC (độ chính xác microsecond phù hợp PostgreSQL), audit trước/sau. Không sửa role/attendance/grants/revision, không tự gửi lời mời mới.
5. Hai response đồng thời được serialize theo lock; trạng thái cuối theo transaction commit sau, timestamp tương ứng. Không hứa thứ tự client click khi network gửi đảo thứ tự.
6. Cùng giá trị gửi lại là no-op; giữ RespondedAt, trả MSG154 theo contract no-change được bổ sung trong catalogue. Nếu nhóm muốn mỗi click đều đổi timestamp phải thay contract/test có ghi nhận; không implement hai hành vi tùy endpoint.

## 8. Notification và email: lưu chắc, gửi sau commit

### 8.1. Tại sao phải sửa điểm tích hợp hiện tại

`MeetingInvitationQueue` đang queue theo `(meeting,user)`, chỉ nhận Published. Reconcile chỉ chọn meeting chưa có bất kỳ email nào. Cách đó bỏ sót người thêm sau công bố, re-invite sau removal, Guest, role/grant change và trạng thái InProgress. Không chỉ thêm một lệnh gọi queue sau POST rồi coi đã xong.

Chọn một bảng intent nhỏ `MeetingInvitationDispatch`, tái sử dụng queue SMTP hiện có. Đây là lựa chọn kỹ thuật của plan, phải ghi ADR mới khi implement (số tiếp theo sau 0018 tại thời điểm này; kiểm lại index trước khi đặt số). ADR mô tả lý do tách intent khỏi email đã render và tương thích delivery cũ; không sửa ADR đã accepted.

### 8.2. Dữ liệu dispatch

Mỗi row ứng với một business event cho một recipient:

- `Id`, `EventId`, `MeetingId`, `ParticipantId?`, `GuestInvitationId?` (XOR), `EventKind` (`Invitation|RoleChanged|PermissionsChanged|Removed`), `DeduplicationKey` unique.
- `PayloadVersion=1`, `PayloadJson` là snapshot tối thiểu: tên/email nhận, tên meeting, lịch/địa điểm cần cho loại thư, role/grants trước/sau nếu liên quan, operation time. Không lưu token OTP/session, nội dung tài liệu hoặc entity graph. Payload DTO có version, deserialize lỗi phải Failed/log metadata.
- `State` (`Pending|BlockedDependency|Materialized|Suppressed|Failed`), `AttemptCount`, `NextAttemptAt`, `LastError` đã lọc secret, `EmailMessageId?`, timestamps.
- Initial invite key lấy từ assignment/invitation ở §5; role/perms/remove key `roster:{eventId}:{kind}:{ownerId}`. EventId tạo server một lần cho transaction. API replay với revision cũ bị reject, no-op không có event mới.
- FK owner giữ lịch sử, không cascade-delete dispatch khi revoke. Unique EmailMessage.DeduplicationKey là tuyến bảo vệ cuối khi materialize retry.

### 8.3. Producer trong transaction

- Publish: sau khi readiness pass, lưu status/history + notification nội bộ + initial dispatch cho mọi current recipient hợp lệ trong transaction publish. Nếu không ghi được intent/notification vào DB thì coi là **persistence transaction thất bại**; không trả success giả. Lỗi **delivery sau commit** không rollback Publish (BR-40/94).
- Add/role/grants/remove Published/InProgress: lưu change/audit + notification nội bộ + dispatch trong transaction roster. Một batch có toàn bộ recipients hoặc không có gì.
- In-app chỉ dành User nội bộ. Publish giữ nguyên type/dedup key/OriginHistoryId hiện có để không nhân đôi notification cũ. Thêm type role/perms/remove nếu cần và ghi rõ cho FE; không đổi nghĩa type cũ.
- Removal in-app đặt TargetType/TargetId null; không dựa FE để “giấu link”. Kiểm `NotificationService.GetTarget` từ chối protected target khi quyền đã mất.
- SMTP disabled: vẫn ghi intent để không mất event, nhưng không tạo `EmailMessage` mới và không gửi. Đây là thay đổi nội bộ có chủ đích so với queue chỉ tạo khi enabled; giữ ý nghĩa FE rằng HTTP success không bảo đảm thư đã gửi. Document lại cấu hình/re-enable behavior.
- Không phát SignalR/thư trước commit. Notification worker hiện có phát từ notification đã commit; lỗi delivery retry theo cơ chế hiện có.

### 8.4. Processor và compatibility

1. Worker email mở scope riêng cho materialize dispatch, rồi scope riêng cho gửi EmailMessage. Không dùng DbContext singleton. Mỗi vòng lấy một batch giới hạn (ví dụ 50), cancellation-aware; không bận lặp nếu SMTP disabled.
2. Peek candidate chưa lock để lấy MeetingId, mở transaction, khóa meeting rồi khóa dispatch với `FOR UPDATE SKIP LOCKED`, đọc lại state. Không khóa dispatch trước meeting để tránh deadlock với producer.
3. Recheck điều kiện an toàn trước khi materialize: initial/role/perms của assignment không còn current hoặc User inactive → Suppressed; Guest đã revoked → Suppressed. Removed event vẫn được gửi tới snapshot email để thông báo thu hồi, không có link. Không gửi initial invite khi meeting đã Ended trước khi materialize.
4. Internal link dùng đường dẫn hiện có `/mgmt/meetings/{id}` và ApplicationBaseUrl đã validate. Invitation có ICS bằng builder hiện có; role/perms chỉ text; removal không ICS/link.
5. Guest invitation/role/perms có link: gọi **port UC-04** `IGuestInvitationLinkProvider.PrepareAsync(invitationId, ct)` trong cùng DB unit of work, không SMTP/network. Provider trả link tới luồng xác minh email, **không** trả authenticated access token. Missing provider → BlockedDependency, hẹn kiểm lại sau 60 giây, không render link giả. Guest removal không cần provider.
6. Provider UC-04 phải idempotent: nếu đã có link/key còn phù hợp thì dùng lại, không vô tình rotate mỗi lần retry làm email trước vô hiệu. Nếu cần persistence xác thực thì cùng transaction với materialize hoặc có recovery contract riêng được test. UC-04 sở hữu hash/random secret/expiry policy; SCRUM-20 không tự chọn thời hạn Guest.
7. Render và thêm EmailMessage với dedup key, đánh Materialized + EmailMessageId trong **cùng transaction**, commit. Nếu email key đã tồn tại (legacy/retry), liên kết row đó và hoàn tất, không chép hoặc chỉnh nội dung thư đã xếp hàng.
8. Persistence/materialization failure: giữ intent để retry; attempts/backoff có cấu hình (đề xuất 30s, 60s, 120s… tối đa 15 phút, tối đa 8 lần lỗi kỹ thuật rồi Failed). BlockedDependency không tiêu thụ 8 lần lỗi kỹ thuật; log chuyển trạng thái có cấu trúc, tránh spam mỗi tick. Chỉ log IDs/type, không dump Body/Payload/email secrets.
9. `EmailQueueProcessor` hiện có tiếp tục lease/SMTP retry và Sent/Failed. Không thay bằng fire-and-forget Task.Run hoặc HTTP request gửi SMTP trực tiếp. At-least-once có thể có thư trùng nếu process chết sau SMTP accept trước khi mark Sent; không hứa exactly-once giao thư.
10. Reconcile legacy theo **mỗi recipient key** ở Published/InProgress; khóa meeting, query existing keys theo tập hợp. Key legacy đã có EmailMessage thì không gửi lại. Meeting có một email không có nghĩa tất cả invitees đã được queue. Không reconcile role/perms/remove từ trạng thái hiện tại vì sẽ mất snapshot; chúng phải có intent từ transaction gốc.
11. Intent pending nhưng invitation đã revoke thì không phát link mới. Email đã giao trước revoke không thể thu hồi; endpoint/link UC-04 phải từ chối ngay. Không giữ meeting row lock qua SMTP để cố xóa hoàn toàn race delivery; quyền được bảo đảm tại server lúc truy cập.

Thêm automated tests cho crash trước/sau materialize, hai worker cùng claim, re-enable SMTP, missing UC-04 provider, và queue cũ. Update tài liệu SCRUM-15/19 về điểm commit/intent; không để hai producer cũ và mới cùng gửi cho một event.

## 9. Contract UC-04: không mở đường vòng xác thực

### 9.1. Identity/access tối thiểu

Định nghĩa `ParticipantGuestSession(MeetingId, InvitationId, SessionId)` trong Domain, kiểm tất cả GUID khác empty. Web resolver nhận principal đã được middleware xác thực. Nếu có dấu hiệu Guest nhưng claim lỗi/thiếu thì trả invalid identity để từ chối, không fallback sang User ID. Principal vừa Guest vừa internal ID cũng không được dùng quyền internal.

`IParticipantGuestAccess` đề xuất:

```csharp
Task<bool> CanAccessAsync(ParticipantGuestSession session, CancellationToken ct);
Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(
    Guid meetingId, IReadOnlyCollection<Guid> invitationIds, CancellationToken ct);
```

`GuestDisplayProfile` chỉ chứa InvitationId, self-declared display name/job title; không có OTP/hash/raw token. Profile là presentation, không là evidence authorization. `GetProfilesAsync` trả empty nếu UC-04 chưa có, query theo batch khi tích hợp; không N+1 per Guest.

SCRUM-20 service vẫn tự kiểm invitation đúng meeting, chưa revoked, state meeting và matching grant. Adapter bổ sung: session tồn tại/bound đúng invitation, email đã verify, session/invitation chưa expired/revoked, UTC từ TimeProvider. Khi read adapter lỗi → lỗi đọc/lưu phù hợp, tuyệt đối không cho phép theo default.

Default implementations: resolver unavailable; access false; profiles empty; link provider unavailable. Không thêm config `AllowAllGuests`, test claims production hoặc public endpoint nhận invitationId rồi trả dữ liệu meeting.

### 9.2. Tích hợp với Agenda có sẵn

UC-04 phải cung cấp cùng nguồn quyền cho `IAgendaGuestIdentityResolver` + `IAgendaGuestAccess` hiện có và adapter Participants mới. Có thể dùng một service kiểm access chung rồi adapter chuyển identity, không duplicate ba bộ expiry/revoke logic.

- Guest JWT có subject namespace không parse thành UserId, không internal capability/role. JWT lifetime không vượt session/invitation lifetime; không lấy header/query tự khai làm identity.
- GET roster/responses/own response dùng current access + grant như §4. Guest gọi management options/mutations không có quyền, không bị nhầm thành User có Guid trùng ID.
- Revoke invitation phải làm mọi session của invitation mất access; revoke một session không ảnh hưởng session khác nếu còn hợp lệ. Session cũ không được sống lại khi session mới được cấp.
- Agenda hub group theo meeting+invitation+session. `ListReadersAsync` và `CanReadAsync` dùng cùng policy; publisher recheck trước mỗi event. Hết hạn token đóng kết nối theo hub configuration hiện có.
- Guest UI không cần internal login nhưng cần luồng UC-04 thật. Contract tests được inject fake adapters và signed test JWT vào TestServer; ghi rõ đây là consumer verification.

### 9.3. Những thông tin UC-04 phải bàn giao trước test Guest E2E

Claim names/scheme; route mở invitation và return URL sau OTP; cách khởi tạo/hủy/gia hạn invitation credentials; cấu hình validity; session repository; profile fields/validation đã chốt; DI registration; test account/email sandbox; test cho OTP single-use/rate-limit/replay. Đây là dependency cần chủ task UC-04 xác định, không phải chỗ AI implement SCRUM-20 được tự invent.

## 10. Sửa ảnh hưởng xuyên feature và handoff room/attendance

| Điểm tích hợp | Sửa cần thiết | Bằng chứng phải có |
|---|---|---|
| MeetingReadPolicy expression và in-memory predicate | Require current assignment; giữ independent CreatedBy/Approver access; internal role Guest/delegation legacy không cấp access mới | Member removed 404; Organizer+removed Member vẫn đọc bằng Organizer; SQL và pure policy cho cùng kết quả |
| Meeting list/detail/history/workflow DTO/actions | Mọi count/role flag dùng current; không serialize historical roster | History row giữ nguyên nhưng không mở quyền qua row cũ |
| Publish readiness | Exactly one active current Chair + ≥1 active current Secretary; không phụ thuộc response/attendance | Removed Chair/Secretary không làm meeting publish được; Declined active vẫn đáp ứng |
| AgendaRules, presenter options, mutation authority | Thêm current filter; người bị gỡ không còn làm presenter lựa chọn mới hoặc Secretary edit | Presenter snapshot cũ không bị xóa; removed user không đọc/edit qua assignment cũ |
| Agenda documents metadata | Giữ access theo shared meeting policy, không mở rộng document API | Removed participant không đọc metadata Agenda bằng quyền cũ |
| MeetingHub/AgendaChangePublisher | Fresh policy khi join và mỗi publish; user/session removed không nhận event mới | Test connection đã join trước revoke; independent reader vẫn nhận nếu policy cho phép |
| Email/notification/reminders | Current recipients; queue theo assignment; target verify hiện tại | Không reminder cho removed; không gửi initial mới tới revoked |
| User deactivate/email edit | Actor/add/promote kiểm active/current email trong transaction; không sửa User API nghiệp vụ ngoài phạm vi | Race test không add tài khoản đã inactive trước commit; không tự thay Guest identity khi User đổi email |
| Room SCRUM-41/UC-37 | Bàn giao `MeetingAccessChanged(meetingId, subjectKind, subjectId, eventId)` sau commit; provider room recheck mọi independent relationship | Consumer contract; actual disconnect chỉ test khi room implementation có mặt |
| Attendance SCRUM-26 | Cung cấp internal assignment ID/Guest invitation ID và current queries, giữ Unknown/response/history | Không tự mark Present khi Accepted/join, không auto mark Absent khi chưa response |

Room chưa có implementation trong branch: không thêm LiveKit SDK hoặc giả vờ đã disconnect. Có thể dùng port mặc định no-op **chỉ cho side effect disconnect chưa tồn tại**, không cho authorization. Note rõ cần room handler khi tích hợp; endpoint access của SCRUM-20 vẫn phải thu hồi ngay.

Sửa docstring cũ của `MeetingParticipant.Attendance`: bỏ mô tả quorum và “person themselves” khi chạm file, thay bằng quy tắc hiện hành official attendance do Secretary. Không làm migration chuyển Unknown sang Absent và không thêm gate kết thúc họp.

## 11. Các bước implement theo thứ tự, mỗi bước có điều kiện hoàn tất

Không chạy tất cả thay đổi rồi mới test ở cuối. Hoàn thành từng bước và test liên quan; sau đó mới full regression.

| Bước | Việc làm cụ thể | Điều kiện hoàn tất |
|---|---|---|
| P0 — Baseline | Kiểm branch/status; đọc lại Jira + docs thay đổi; ghi các quyết định §1; kiểm SDK/DB/Redis; chạy baseline checks nếu môi trường sẵn | Biết lỗi có trước; không có thay đổi user bị ghi đè; related UC/sequence không thiếu |
| P1 — Contract | Tạo API doc + handoff draft; chốt DTO/outcomes/constants ở Domain; bổ sung catalogue mappings và mã stale hợp lệ | Mỗi route có quyền, request, response, error; không code UI |
| P2 — Model | Entities/configs/indexes; revision; grants; dispatch; migration + snapshot | Build; model tests; fresh/upgrade migration tests và preflight legacy pass |
| P3 — Policy | Pure ParticipantRules/catalogue; current predicate; state/role/grants matrix | Parameterized rule tests toàn bộ §4; EF translation test current filter |
| P4 — Repository | Queries roster/options/responses/current/history; lock và transaction; projection/paging | PostgreSQL repository tests, consistent totals, không N+1; options không leak |
| P5 — Read HTTP | GET context, roster, options, own response, summary; strict query parse | Endpoint tests 401/403/404/success, field isolation, Guest deny default |
| P6 — Recovery/add | Recovery riêng, batch add, Guest create, all-or-none; bind If-Match | Positive/negative API tests; same-user/email concurrency; response/attendance defaults |
| P7 — Role/grant/remove | Final-state validation; Chair flush ordering; Secretary replacement; soft removal/re-add | Rollback failure injection; invariant race; history and independent access tests |
| P8 — Response | Own identity resolution, Accepted/Declined, timestamp, no-op | Cross-user/meeting denial; response/removal/state race; attendance untouched |
| P9 — Delivery | Dispatch producer/processor; notification rows; Publish integration; reconcile migration compatibility | SMTP fake/sandbox, dependency/retry/crash tests; no duplicate business events |
| P10 — Cross-feature | Update Meeting/Agenda/Hub/reminders policies; UC-04 adapter consumer; room handoff | Existing tests + new revoked-session/realtime tests pass; no regression auth |
| P11 — Dev/manual assets | Insert-only seed, Postman collection, environment template không secret; FE/UC04/SCRUM26 notes | Người khác chạy theo tài liệu mà không sửa database bằng tay |
| P12 — Final verification | Full CI-equivalent, performance measurement, manual API scenarios; review diff/security | Test report có command/result/limitation; không claim test chưa chạy; git diff --check pass |

Nếu có failure ở một bước, sửa nguyên nhân rồi chạy lại phần liên quan; không tắt analyzer/test/authorization để vượt gate. Khi hoàn tất không tự commit/merge/deploy.

## 12. Automated test plan

### 12.1. Bộ test mới

| File đề xuất trong `tests/TrustCabinet.Web.Tests/Participants/` | Trách nhiệm |
|---|---|
| `ParticipantRulesTests.cs` | Ma trận state × role × grants; recovery; final role constraints |
| `ParticipantEndpointTests.cs` | HTTP/DTO/JSON/message codes/content type/body limits/auth |
| `ParticipantRepositoryTests.cs` | PostgreSQL query, search, paging, projections, totals/history |
| `ParticipantMutationTests.cs` | Use case happy/alternative/exception, preservation và rollback |
| `ParticipantConcurrencyTests.cs` | Hai contexts/connections thật và barriers; không dùng sleep làm đồng bộ chính |
| `ParticipantMigrationTests.cs` | Fresh upgrade, data retention, partial unique, legacy abort, safe Down guard |
| `ParticipantDeliveryTests.cs` | Intent/notifications/queue compatibility, retry, dedup, worker crash/dependency |
| `ParticipantGuestContractTests.cs` | Fake UC-04 consumer contract, identity isolation, revoked/expired session |
| `ParticipantAccessRegressionTests.cs` | Meeting/Agenda/hubs/notifications sau removal; independent relationship |
| `ParticipantPerformanceTests.cs` | Query counts, large roster, 100 callers measurement |
| `ParticipantFixture.cs` | Unique PostgreSQL database, TimeProvider cố định, clients/fixtures, failure injection |

Tái sử dụng `TrustCabinetApiFactory`, `PostgresTestDatabase`, `PostgreSqlTestDatabase` và kiểu fixture Agenda. EF InMemory chỉ dùng pure/service test thích hợp; không dùng nó chứng minh FK/unique/transaction/ILIKE/citext/concurrency đúng. Không thêm package test nếu dependencies hiện có đã đủ.

### 12.2. Cases bắt buộc (ID dùng trong test report)

| ID | Setup/thao tác | Kỳ vọng chính |
|---|---|---|
| AUTH-01 | Tất cả routes không token, token sai signature/expired | 401, không query/ghi dữ liệu nghiệp vụ trái phép |
| AUTH-02 | User token còn hạn nhưng account vừa inactive | Gate hiện có từ chối; không dựa claim cũ |
| AUTH-03 | Outsider/Admin-only truy cập meeting/assignment ID đúng, sai và khác meeting | 404 nhất quán, không leak tên/email/count/validation riêng |
| AUTH-04 | Organizer/Approver đọc meeting hợp lệ nhưng không được quản lý roster | Context hạn chế; mutation 403; roster/responses 404 |
| AUTH-05 | System role tên Secretary nhưng không assigned Secretary | Không được manage; assigned Secretary không có system role đó vẫn manage được |
| READ-01 | UC-27 tất cả 7 states × Secretary/Chair/Member/Guest × grant có/không | Đúng §4; Guest còn cần verified current access |
| READ-02 | Member chỉ ViewInvitationResponses | Summary có names/email; roster/options/search bị chặn; DTO không grants/attendance |
| READ-03 | Member chỉ ViewMeetingParticipants | Roster có identity; summary bị chặn; roster không response/time |
| READ-04 | Current + removed/revoked + inactive internal | Removed/revoked ngoài rows/totals; inactive current vẫn hiển thị isActive=false, không có access |
| READ-05 | Search name/email case khác, khoảng trắng, `%`, `_`, Unicode; page out of range | Literal search, stable sort, total đúng; không injection hoặc N+1 |
| READ-06 | Response rows đồng thời thay đổi trong lúc đọc nhiều query | Rows/count/totals cùng snapshot; all-Pending accepted=0/declined=0 |
| REC-01 | Organizer không current Secretary ở Draft/Rejected/Approved, chưa từng bootstrap hoặc đã gỡ Secretary | Recovery thêm đúng một active Secretary, Pending/null, không extras/email |
| REC-02 | Recovery current Member đã Accepted, có attendance/grants | Cùng assignment ID, chỉ role đổi; evidence giữ nguyên |
| REC-03 | Recovery WaitingApproval/Published/InProgress/Ended, còn Secretary, current Chair, inactive target, batch/extras | Từ chối; không mutation; inactive Secretary vẫn là current và không mở recovery |
| ADD-01 | Batch Member/Secretary hợp lệ; role Chair một người khi chưa Chair | Toàn bộ rows/grants saved; defaults đúng; system roles giữ nguyên |
| ADD-02 | Một ID inactive/missing/duplicate/current/empty, Chair batch >1 | Cả batch thất bại, không row/audit success/notification/dispatch thừa |
| ADD-03 | Guest name/email invalid, CRLF, display address, duplicate case-insensitive, email current internal | MSG97, không Guest/User giả; cross-table checks đúng |
| ADD-04 | Add internal có email Guest current | Reject nhất quán; revoke Guest rồi retry có thể thêm |
| ADD-05 | Guest Confidential/Secret | Save inviter/time/sensitivity; không yêu cầu extra confirmation flag |
| ROLE-01 | Single role change và grants-only | MSG100/153; response/attendance/system role nguyên vẹn |
| ROLE-02 | Thay Chair có confirmation và final Secretary hợp lệ | Cả outgoing/incoming cùng đổi; thông báo cả hai |
| ROLE-03 | Thay Chair thiếu/mismatch confirmation, Guest role, target historical, tạo Chair thứ hai | Không lưu một phần |
| ROLE-04 | Demote/remove Secretary cuối ở preparation vs Published/InProgress | Preparation được; sau công bố phải giữ/replacement nguyên tử |
| ROLE-05 | Promote Secretary cuối thành Chair cùng replacement Secretary thứ ba | Final model hợp lệ; rollback không để mất Secretary |
| ROLE-06 | Grant unknown/duplicate; mới cấp cho Chair/Secretary; Guest internal privileges | MSG152; không âm thầm drop/normalize thành quyền khác |
| ROLE-07 | Grant Member cũ promote rồi demote, omit vs [] | Compatible grants bảo toàn nếu omit; [] chỉ bỏ optional grants; defaults role vẫn còn |
| ROLE-08 | No-op và self-demotion | No-op không bump revision/dispatch; self-demotion success không trả full roster trái quyền |
| DEL-01 | Remove Member/Guest đã có response/attendance | Row/evidence/grants history còn; current queries và access không còn |
| DEL-02 | Remove + re-add cùng identity | ID mới, Pending/null, grants mới; session/history ID cũ không hồi sinh |
| DEL-03 | Removed participant đồng thời là Organizer/Approver | Chỉ quyền từ assignment mất; độc lập còn đúng policy, không tự có roster |
| RESP-01 | Pending→Accepted→Declined ở Published/InProgress | Latest response/time; role/attendance/grants/revision không đổi |
| RESP-02 | Body có UserId khác, Pending/Delegated/numeric/unknown props | Reject; không response thay người khác |
| RESP-03 | Draft/WaitingApproval/Rejected/Approved/Ended; removed/inactive/revoked | Không ghi; 404 hoặc MSG107 theo khả năng đọc hiện tại |
| RESP-04 | Lặp cùng response | No-op; timestamp không đổi, không notification thừa |
| RACE-01 | Hai add cùng User/email, hoặc internal add và Guest add cùng email | Một success, một stale/duplicate; DB chỉ một identity current tương ứng |
| RACE-02 | Hai promote Chair từ cùng revision | Không bao giờ hai current Chair; request sau conflict |
| RACE-03 | Hai Secretary cùng remove/demote lẫn nhau | Published/InProgress vẫn còn ít nhất một Secretary; không “cả hai đều thấy count=2” rồi cùng xóa |
| RACE-04 | Recovery đồng thời; add/removal đồng thời Publish | Tất cả writers cùng meeting lock; publish không bỏ sót/nhân đôi invite và readiness final đúng |
| RACE-05 | Response đồng thời remove/revoke hoặc state chuyển Ended | Serializable kết quả theo lock; không ghi response sau khi đã mất quyền ở thời điểm transaction kiểm |
| RACE-06 | Role update đồng thời response | Role save không ghi đè Response/RespondedAt vừa commit |
| RACE-07 | Deactivate hoặc đổi email User trong lúc add/promote | Validation/lock strategy không commit target đã invalid trước save; không cấp access từ token cũ |
| TX-01 | Inject DbUpdateException sau demote Chair/trước promote hoặc giữa batch | Rollback toàn bộ rows/grants/revision/audit/notifications/dispatch |
| TX-02 | DB read lỗi, write lỗi, cancellation | Read không thành empty200; write map đúng code; cancellation không bị báo success |
| MAIL-01 | Add preparation, Publish, add Published/InProgress | Trước publish không dispatch; publish all current; thêm sau publish chỉ người mới |
| MAIL-02 | Role/grants/no-op/Chair swap/remove | Đúng affected recipients; removal không access URL/ICS; Guest email-only |
| MAIL-03 | SMTP timeout/fail, worker restart, duplicate processor, crash sau render trước commit | Roster giữ nguyên đã commit; intent retry; EmailMessage key không duplicate |
| MAIL-04 | SMTP disabled rồi enabled; legacy published có một số email | Không gửi khi disabled; recipient missing được bù; email legacy không gửi lại |
| MAIL-05 | Guest provider missing rồi có; revoke trong lúc pending | BlockedDependency rồi retry được; revoked không có thư mời mới; không link giả |
| MAIL-06 | Initial dispatch đã render/giao rồi revoke | Link luôn bị server từ chối; không khẳng định thu hồi được email đã giao |
| GUEST-01 | Signed test JWT + adapter verified current + matching grants | Read/own response đúng meeting; không cần User row |
| GUEST-02 | Missing/malformed claims, mixed internal+Guest principal, trùng Guid với User | Không nâng quyền/fallback; mutation/options bị chặn |
| GUEST-03 | Unverified/expired/revoked/missing session/invitation/sai meeting | Fail closed; không lộ roster/response; no event qua session cũ |
| GUEST-04 | Hai sessions một invitation, revoke một rồi revoke cả invitation | Chính xác session còn quyền; revoke invitation chặn tất cả |
| GUEST-05 | Default adapters chưa thay, storage adapter lỗi | Không Guest access; technical failure không được hiểu là allowed |
| REG-01 | Removed Member thử meeting list/detail/history, Agenda GET/edit/options và hub join | Không access qua history; independent access vẫn theo policy |
| REG-02 | Connection đã join trước revoke/removal, sau đó Secretary sửa Agenda | Không event cho subject mất quyền; authorized users còn nhận; snapshot không bị rollback vì publish lỗi |
| REG-03 | Reminder và notification target sau remove | Không reminder mới; target protected bị chặn; removal notification không link |
| MIG-01 | DB trắng và upgrade DB trước migration có response/attendance/email data | Schema đúng và toàn bộ evidence/legacy dedup preserved |
| MIG-02 | Legacy unsupported data hoặc nhiều Chair | Migration abort có nguyên nhân, data/schema không nửa chừng |
| MIG-03 | Constraints với insert trực tiếp trong test DB | Duplicate current bị chặn; historical duplicates hợp lệ; XOR grants được enforce |
| HTTP-01 | Missing/wrong If-Match, stale revision, invalid GUID/query/content type/JSON, body >64KiB kể cả chunked | Code/status đúng; authorization không bị bypass; payload bound |
| CAT-01 | MessageCodeCatalogueTests toàn solution | Hằng số/code/http/test đúng; next code đúng; không inline MSG literal |

### 12.3. Performance và security kiểm tra có mục đích

- Repository query count không tăng tuyến tính theo số participants/Guest/grants: đo roster 1 so với 100/500 rows; profiles phải batch, count đúng pagination. Kiểm indexes bằng query plan trên DB test nếu chậm.
- Theo pattern `AgendaPerformanceTests`, warm-up rồi đo 100 caller concurrent với roster 500 entries, ba lượt GET roster/response trên fixture có quyền; ghi p50/p95, query count, runtime/CPU, error count. Đối chiếu mục tiêu API p95 ≤1s trong môi trường đo; không gọi kết quả TestServer là chứng nhận server Lightsail.
- Mutation contention: nhiều meeting song song không bị serialize bởi global lock; cùng meeting phải giữ invariants. Đo/report, không hardcode timing assertion dễ flaky trong unit tests.
- Security assertions: IDOR, stale token/role, mass assignment, enum numeric, SQL wildcard/injection, HTML/ICS newline escaping, Guest email/session binding, token/log redaction, query/body limits, fields isolation và CSRF theo scheme UC-04 nếu sau này dùng cookie.
- Không pentest/load test server chung hoặc gửi thư ra người thật. Email integration dùng fake sender/SMTP sandbox mà nhóm cho phép. Không mở kết nối SMTP thật chỉ để chứng minh test xanh.

### 12.4. Lệnh kiểm tra sau implement

Chạy từ root trong PowerShell; không ghi connection string thật vào tài liệu/log. `TRUSTCABINET_TEST_CONNECTION_STRING` hoặc cấu hình test hiện có phải trỏ PostgreSQL test; Redis local cũng cần cho full suite.

```powershell
dotnet restore TrustCabinet.sln
dotnet build TrustCabinet.sln --no-restore --configuration Release -warnaserror
dotnet test TrustCabinet.sln --no-build --configuration Release --filter FullyQualifiedName~Participants --logger "trx;LogFileName=scrum20-participants.trx"
dotnet test TrustCabinet.sln --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=scrum20-full.trx"
```

Chạy ở thư mục `frontend` để kiểm regression build dù không sửa FE:

```powershell
npm ci
npm run lint
npm run build
```

Nếu đo coverage dùng coverlet collector đã có: `dotnet test ... --collect:"XPlat Code Coverage"` cho suite phù hợp; báo coverage của code mới kèm nhánh chưa cover, không chạy lặp full suite chỉ để tạo số đẹp. CI hiện có bốn cổng build BE, test BE, lint FE, build FE; không bỏ cổng nào.

Trong bước migration dùng local tool theo repo:

```powershell
dotnet tool restore
dotnet dotnet-ef migrations add MeetingParticipantLifecycleAndGuestInvitations --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --output-dir Migrations
dotnet dotnet-ef migrations has-pending-model-changes --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web
git diff --check
git status --short
```

Factory design-time dùng `TRUSTCABINET_CONNECTION`, khác config runtime/test; đọc [database guide](03-co-so-du-lieu.md) trước khi chạy. Không dump User Secrets/.env để debug. Không chạy destructive database cleanup ngoài database tạm do test fixture sở hữu.

## 13. Test thủ công sau khi code

### 13.1. Chuẩn bị và dữ liệu

Backend implementer tạo collection `docs/03-api/postman/SCRUM-20-Participants.postman_collection.json` và file hướng dẫn tương ứng. Collection có scripts lấy ID/ETag từ response, assertion HTTP/code/response shape và biến token theo actor. Không ghi access token/password thật vào collection hoặc environment export.

Dev seeder chỉ chạy Development, prefix `[SCRUM20-DEV]`, insert-only/idempotent như seeder Agenda. Khi chạy lại không sửa response/attendance của data đang dùng test. Tạo fixtures ở các trạng thái cần thiết để test InProgress/Ended khi branch chưa có Start/End endpoint; không thêm “test state endpoint” công khai hoặc sửa tay DB dùng chung.

| Ký hiệu | Quan hệ cần chuẩn bị |
|---|---|
| O | Organizer có capability tạo/công bố; không phải participant ban đầu |
| S1, S2 | Hai User active có thể làm Secretary; system role không cần mang tên Secretary |
| C1, C2 | User active dùng cho Chair swap |
| M1, M2 | Active Members; M2 ban đầu không optional grants |
| X | Active outsider; thêm một Admin-only để chứng minh không bypass |
| I | Inactive User, dùng negative selection test |
| G1, G2 | Email sandbox khác nhau; không là User; chỉ dùng mailbox do tester kiểm soát |
| D, W, R, A, P, IP, E | Bảy meeting với prefix riêng ở Draft, WaitingApproval, Rejected, Approved, Published, InProgress, Ended |

Lấy token bằng login thật của tài khoản dev do repo cung cấp/cấu hình local. Chỉ automated TestServer dùng ký token test. Không fake token Guest để “test manual E2E”.

Chạy backend ở một terminal:

```powershell
dotnet run --project backend/TrustCabinet.Web
```

Manual API dùng `http://localhost:5225`; chỉ khi FE team tích hợp mới mở UI `http://localhost:5173`. Đây không phải thay đổi same-origin contract của FE. Mail dùng fake/sandbox; giữ SMTP thật disabled nếu chưa có mailbox test được phép.

### 13.2. Ví dụ request PowerShell

Các biến token/ID bên dưới phải lấy từ login/collection/seed local; không copy placeholder rồi coi request fail là bug code. Trong Windows PowerShell dùng `curl.exe` nếu muốn curl, vì `curl` có thể là alias khác.

```powershell
$apiBase = 'http://localhost:5225'
$meetingId = '<meeting-guid>'
$secretaryToken = '<token-from-real-local-login>'
$memberUserId = '<active-member-user-guid>'
$secretaryHeaders = @{ Authorization = "Bearer $secretaryToken" }

$context = Invoke-WebRequest -Method Get -Uri "$apiBase/api/meetings/$meetingId/participant-context" -Headers $secretaryHeaders
$writeHeaders = @{
    Authorization = "Bearer $secretaryToken"
    'If-Match' = [string]$context.Headers['ETag']
}
$body = @{
    userIds = @($memberUserId)
    role = 'Member'
    additionalPermissions = @('ViewInvitationResponses')
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post -Uri "$apiBase/api/meetings/$meetingId/participants" -Headers $writeHeaders -ContentType 'application/json' -Body $body
```

Sau mỗi roster mutation lấy lại context/ETag trước thao tác tiếp theo. Response dùng token của chính invitee và không cần If-Match:

```powershell
$memberToken = '<member-token-from-real-local-login>'
$memberHeaders = @{ Authorization = "Bearer $memberToken" }
Invoke-RestMethod -Method Put -Uri "$apiBase/api/meetings/$meetingId/invitation-response" -Headers $memberHeaders -ContentType 'application/json' -Body '{"response":"Accepted"}'
Invoke-RestMethod -Method Get -Uri "$apiBase/api/meetings/$meetingId/invitation-response" -Headers $memberHeaders
```

### 13.3. Kịch bản nghiệm thu internal

| Bước | Thao tác | Kết quả quan sát được |
|---|---|---|
| M01 | O GET context của D chưa có Secretary; gọi roster | canRecoverSecretary=true; roster 404, không lộ người khác |
| M02 | O POST recovery S1 với ETag | 201/MSG96; S1 Pending; O không có thêm quyền quản lý roster |
| M03 | O thử recovery lần hai/add Member/change grants | Bị chặn; S1 được đọc/manage D bằng quan hệ assigned |
| M04 | S1 add batch C1 role Chair một người, M1/M2 role Member; thử batch có I | Batch hợp lệ success; batch chứa I không thêm ai |
| M05 | S1 mời G1 khi D và thử duplicate email khác case/current internal email | G1 saved, chưa gửi email; duplicate bị từ chối |
| M06 | S1 gỡ chính mình ở D; O phục hồi từ M1 đã current | Cho gỡ preparation; recovery đổi đúng row M1 → Secretary, không reset response/attendance/grants |
| M07 | Trên W, S1 đọc roster rồi thử add/change/remove | Đọc được; mọi mutation MSG98; Organizer không recovery |
| M08 | O Publish A đủ active Chair/Secretary, bằng endpoint workflow hiện có | Published/MSG45; queue intents cho current recipients, không removed; HTTP không đợi SMTP |
| M09 | M2 không grants gọi roster/summary, rồi S1 cấp chỉ ViewInvitationResponses | Trước grant cả hai 404; sau grant summary 200 có tên/email, roster vẫn 404 |
| M10 | S1 đổi M2 chỉ ViewMeetingParticipants | Roster200 không có response/time; summary404; UC-40 connected list không liên quan |
| M11 | M1 Accepted rồi Declined; kiểm own response và summary bằng S1 | Giá trị/time cập nhật; role/attendance không đổi; repeat cùng value no-op |
| M12 | S1 thay C1 → Member và C2 → Chair với đúng confirmation | Cả hai đổi; notification hai người; không thời điểm committed có hai Chair |
| M13 | S1 thử remove Chair hoặc demote Secretary cuối ở P/IP | MSG109; final role còn nguyên; thay Secretary cuối kèm replacement thì được |
| M14 | Add M2/G2 vào P hoặc IP | Chỉ người mới có invitation event; không gửi lại mọi người cũ |
| M15 | M2 mở Agenda/hub, S1 remove M2; thử lại API/target/join; S1 sửa Agenda | M2 mất access từ assignment và không event mới; removal notification không link |
| M16 | Remove một participant đồng thời là O | O vẫn đọc meeting bằng Organizer; không vì vậy được đọc roster/responses |
| M17 | Re-add M2 | Assignment ID mới Pending/null; history ID cũ giữ evidence và không effective grants |
| M18 | Dùng ETag cũ để update sau một mutation khác | 409/stale code; không ghi đè thay đổi của người trước; reload và xác nhận lại |
| M19 | Chạy GET/response trên E | Authorized views đọc được; response/mutation bị khóa, không tự sửa attendance |
| M20 | SMTP sandbox fail hoặc worker dừng tạm trong môi trường riêng | Domain save giữ thành công; pending/failed delivery nhìn được qua log/DB test; bật lại retry đúng recipient |

Các case rollback, duplicate race và fault injection chạy automated trên DB riêng; không cố làm hỏng database dùng chung để test thủ công. Kết quả mỗi Mxx ghi HTTP/code, IDs giả lập, timestamp và ảnh/output đã che token nếu cần; không lưu secret trong báo cáo.

### 13.4. Kịch bản Guest sau khi UC-04 tích hợp

1. S1 mời G1 vào P, đọc mailbox sandbox, mở link thật; hoàn tất OTP đúng email và profile theo UC-04. Số User rows không tăng.
2. Guest GET own response, Accepted rồi Declined. Không dùng User token hoặc token dựng bằng tay. Đổi MeetingId → 404; thử respond thay invitation khác bị từ chối.
3. Không grants: roster/summary404; cấp từng grant và cả hai, kiểm đúng tách quyền. Guest management/role/system admin APIs luôn bị từ chối.
4. Tên tự khai khác invitedName: roster phân biệt invited evidence/self-declared profile, không đổi invited email hoặc ownership.
5. Dùng hai Guest sessions; revoke một session, phiên đó mất GET/join/events, phiên còn lại vẫn theo policy. Revoke invitation từ S1 thì cả hai mất quyền ngay; link cũ không khôi phục invitation.
6. Test expiry bằng TimeProvider/config hợp lệ trong môi trường test, không chỉnh clock hệ điều hành hoặc bypass JWT validation. Kiểm JWT expiry và invitation/session expiry độc lập.
7. Re-invite cùng email sau revoke: invitation ID mới, Pending; session cũ không dùng được với ID mới. Kiểm Agenda guest integration thật; room disconnect chỉ đánh pass khi room provider đã triển khai.

Nếu chưa có UC-04, ghi các bước này **Blocked by UC-04**, không ghi Passed/Skipped rồi coi đã nghiệm thu Guest.

## 14. Bàn giao riêng cho Frontend

Tạo `docs/03-api/09-thanh-phan-tham-du.md` (contract), `09-thanh-phan-tham-du-ban-giao.md` (FE/manual) và `09-thanh-phan-tham-du-guest-uc04.md` (dependency). Các file là đầu ra cần viết khi implementation; plan hiện tại chưa phải API đã phát hành.

Frontend implementer cần:

1. Tạo types/API wrappers trong `src/types/` và `src/api/`, dùng client hiện có và relative URL. Backend không sửa những file đó trong task này. Không dùng `fetch` trực tiếp từ component hoặc VITE_API_BASE_URL.
2. Participants panel dùng chung theo ADR-0011; shell/routes/design theo project skills, không tạo layout khác. Read context trước, render controls theo flags; quyền cuối cùng vẫn ở Backend.
3. Tách “Thành phần” và “Phản hồi” bằng hai permission; không preload roster vì user chỉ có quyền responses. Không suy capability từ system role/label tên người dùng.
4. Recovery UI dành Organizer xuất hiện qua canRecoverSecretary dù không được đọc roster; một lựa chọn active User, không role/grants tùy chọn. Sau recovery reload context.
5. Internal add chọn nhiều User và shared role; Guest form riêng tên/email/grants, không chọn Chair/Secretary. Không có “mời cả đơn vị”. Cảnh báo inline Confidential/Secret, không thêm dialog xác nhận mật lần hai.
6. Chair replacement cần hiển thị outgoing/incoming và xác nhận; submit một atomic request. Nếu incoming là Secretary cuối, UI yêu cầu chọn replacement Secretary trong cùng request hoặc chuẩn bị thêm Secretary trước. Không gọi hai PUT tuần tự.
7. Có If-Match/ETag trong API wrapper; stale code → tải lại context/view và để người dùng xác nhận lại, không tự replay mutation lên roster mới.
8. 201/200 chỉ xác nhận đã lưu, không nói “email đã giao”. Không poll queue hoặc đưa cấu hình worker/SMTP vào giao diện.
9. Sau save reload context rồi tải các view còn được phép, vì self-demotion/removal có thể làm mất quyền ngay. Xóa cached roster/response khi 401/403/404 mất quyền; không để PII cũ còn hiện dưới tab bị cấm.
10. Accepted/Declined chỉ là phản hồi, không hiển thị như Có mặt/Vắng mặt. Không có UI delegation, điểm danh hoặc hoàn tất họp trong phần việc này.
11. Guest name/job title tự khai khác email verified; invitedName là evidence. Trước UC-04, hiển thị phần chưa tích hợp rõ ràng ở môi trường phát triển, không tạo mock token để demo nghiệm thu.
12. Empty list, all-Pending, loading, denied, read error và stale data là các trạng thái riêng. 500/MSG95 không biến thành “không có ai”.
13. Quyền response-only vẫn thấy tên/email trong bảng responses nhưng không có search roster; không thêm ô search gọi roster endpoint để lách quyền.
14. Participant realtime tự động chưa được yêu cầu trong UC-27/30; refresh sau mutation và khi tab hoạt động lại là đủ cho iteration này. Không tự thêm SignalR contract mới. Các event Agenda hiện có vẫn phải được kiểm quyền như §10.
15. Dùng MSG/code và field errors, không match message string. Chuỗi UI tiếng Việt, enum wire PascalCase. Route UI thiếu quyền dùng màn 404 như project convention.

## 15. Tài liệu, Jira note và báo cáo sau implementation

### 15.1. Tài liệu phải cập nhật cùng thay đổi code

- API contract/handoff/UC-04 integration ở §14; index `docs/03-api/README.md`.
- Catalogue MSG: kích hoạt codes reserved dùng thật, cặp HTTP/code, messages tiếng Việt, next value. Không sửa/renumber codes đã dùng.
- ERD hiện tại/đích cho lifecycle/grants/Guest/dispatch; database migration notes và ADR intent email.
- Email/SCRUM-19 handoff: intent được ghi trong transaction, materialize sau commit, re-enable/reconcile theo recipient, Guest dependency.
- Guest Agenda handoff: bỏ giả định UC-04 production sẽ nằm trong SCRUM-20, giữ adapter contract và integration checklist.
- Test report `docs/03-api/09-thanh-phan-tham-du-ket-qua-kiem-thu.md`: ngày, branch/commit base, lệnh, test counts/failure/skip, môi trường, performance, manual outcomes và dependency còn chờ.
- Không tự sửa scope toàn sản phẩm, báo cáo DOCX/PPTX hay ADR accepted. Không mở lại yêu cầu BA hoàn tất tài liệu trước khi code vì workflow BA Final Review nằm sau FE.

### 15.2. Bản nháp note Jira để người phụ trách gửi

Theo yêu cầu sửa review ngày 15/10/2026, đã đăng [change note 10221](https://sep490g116.atlassian.net/browse/SCRUM-20?focusedCommentId=10221), liên kết SCRUM-59 (assignee hiện tại Nguyễn Quang Huy). Nội dung nháp lịch sử của plan bên dưới đã được cụ thể hóa trong comment:

> SCRUM-20 Backend theo baseline PO 13/10/2026, UC-27/28a/28b/28c/29/30. Không delegation; điểm danh ở SCRUM-26. Organizer recovery Secretary chỉ khi không còn current Secretary trong Draft/Rejected/Approved; giữ history khi remove/revoke. Iteration này mở View Meeting Participants và View Invitation Responses cho Member/Guest; catalogue có thể mở rộng. Theo trao đổi với người phụ trách, OTP/profile/session production tách sang UC-04; SCRUM-20 bàn giao adapters/dispatch và consumer tests, Guest E2E cần tích hợp UC-04 trước nghiệm thu. Endpoint contract/validation, migration/dispatch và test results: [điền đường dẫn]. FE làm riêng theo handoff. Chưa commit/merge/deploy tự động.

Các lựa chọn kỹ thuật cần ghi nhận trong note/PR khi implementation: ETag/revision và stale code; strict payload/batch limit; cùng-value response là no-op; Guest credential ownership ở UC-04; intent persisted khi SMTP disabled và materialize khi enabled. Không gọi chúng là yêu cầu nguyên văn của PO nếu chỉ là thiết kế của plan.

### 15.3. Mẫu báo cáo kết thúc cho người phụ trách

```text
Đã thay đổi:
- [Endpoint/data model/access/delivery đã implement thực sự]
- Migration: [tên], preservation/preflight: [kết quả]

Kiểm thử:
- Backend Release -warnaserror: [pass/fail + lệnh]
- Tests: [passed/failed/skipped + suites + artifact]
- Frontend lint/build regression: [kết quả]
- Manual: [M01–M20 đã chạy, còn thiếu gì]
- Guest: [consumer contract / E2E thật / blocked UC-04, tách rõ]
- Performance: [môi trường + số đo, không suy thành production SLA]

Bàn giao:
- API/FE/UC04 notes và Postman: [links]
- Hướng dẫn test thủ công: [link]
- Jira note: [comment 10221](https://sep490g116.atlassian.net/browse/SCRUM-20?focusedCommentId=10221)

Git:
- Branch feat/scrum20-Backend-Invite-participants-and-acept
- [Danh sách thay đổi chưa commit]; không tự commit/merge/deploy.
```

## 16. Checklist hoàn tất dành cho AI implementer

- [ ] Đúng branch; giữ mọi thay đổi có trước của người dùng.
- [ ] Kiểm lại baseline Jira/UC/BR và nguồn thay đổi; báo ngay nếu thiếu tài liệu liên quan hoặc gặp mâu thuẫn chưa chốt.
- [ ] Mỗi normal/alternative/exception flow UC-27–30 có endpoint/service/test tương ứng; không bỏ recovery, Guest hoặc Chair swap vì khó.
- [ ] Migration giữ evidence, legacy preflight rõ, constraints chạy thật trên PostgreSQL.
- [ ] Không API nào tin quyền do UI/body/system-role-name cung cấp.
- [ ] Không leak roster/response/search qua DTO/context/options/notification/Guest ID.
- [ ] Mọi roster writer cùng meeting lock; revision và required-role checks trong transaction; rollback đã được test.
- [ ] Own response không ảnh hưởng role/attendance/grants; không delegation.
- [ ] Remove/revoke thu hồi access qua mọi path hiện có và giữ independent relationships/history.
- [ ] Publish/add/role/grants/remove có đúng recipients và durable delivery; SMTP failure không rollback domain đã commit.
- [ ] UC-04 chưa tích hợp thì fail-closed và ghi rõ dependency; không báo Guest E2E hoàn tất.
- [ ] Không thêm UI, attendance endpoints, room SDK, document/vote migration hoặc công việc ngoài phạm vi.
- [ ] Build/tests/lint theo CI pass hoặc báo chính xác blocker; không giả pass do skip hạ tầng.
- [ ] Test report và manual instructions có kết quả thật, không chỉ danh sách dự định.
- [ ] API/MSG/ERD/ADR/handoff cập nhật đúng phần; report/diagram edits tuân thủ hướng dẫn riêng nếu có.
- [ ] `git diff --check` sạch; review diff không secret/generated wwwroot; không commit/merge/deploy tự động.

## 17. Trạng thái ở thời điểm bàn giao plan

Plan đã đối chiếu Jira và các nguồn trực tiếp ở §1, cùng hiện trạng code của điểm tích hợp. **Chưa có implementation SCRUM-20, migration hoặc test mới được tạo/chạy bởi việc viết plan này.** Khi thực thi, bắt đầu P0 rồi đi theo thứ tự; cập nhật checklist bằng evidence thực tế. Phần tài liệu ngoài phạm vi đã đối chiếu và dependency Guest được nêu rõ, không che dưới một kết luận “mọi thứ đã hoàn tất”.
