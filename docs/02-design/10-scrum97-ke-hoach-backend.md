# SCRUM-97 — Kế hoạch Backend chuyển giao Chủ tọa UC-73

Ngày thực hiện: 22/10/2026 (Asia/Saigon). Kế hoạch đã được owner duyệt trong phiên triển khai. Đây là hướng dẫn triển khai và đối chiếu, không thay thế đặc tả BA. Contract hoàn chỉnh nằm tại [API Participants §8](../03-api/09-thanh-phan-tham-du.md#8-scrum-97--api-chuyên-biệt-uc-73); bằng chứng thực chạy nằm trong [báo cáo](../03-api/10-scrum97-ket-qua-kiem-thu.md).

## 1. Phạm vi và baseline

- Branch duy nhất: `refactor/SCRUM-97-BACKNEND-Synchronize-attendees-according-to-PO-relate-to-SCRUM-20`.
- HEAD ban đầu sạch: `adca53ba29c0b4c670cd01984b675d17018c8f4c`. Fast-forward được owner cho phép tới `875dde2b09702af539bb49fda2c44c8396d0e235`; không tạo merge commit. Mọi diff mới tính từ commit này.
- Ghi nhận main trước thực hiện: `ed94dc99f545121fe15f3981c2a783a34a2ae90f`. Kiểm tra lại khi kết thúc.
- Chỉ Backend, tests và tài liệu; Frontend tự làm types/API client/UI theo bàn giao. Không sửa source FE, không commit/push/PR/merge main/deploy hoặc đăng Jira.
- Kế thừa transaction swap, unique Chair, confirmation, MSG256/257/259, audit, notifications, dispatch, realtime từ SCRUM-20. Bổ sung hai API UC-73, MSG258/260 và tests riêng.
- Không thêm entity, migration, trạng thái, voting workflow, attendance workflow, Guest OTP hoặc thao tác rời phòng.

## 2. Nguồn đã đối chiếu và quy tắc áp dụng

Các tài liệu liên quan trực tiếp đã được đọc trong quá trình khảo sát và triển khai; danh sách này không khẳng định đã đọc mọi tài liệu ngoài phạm vi UC-73.

| Nhóm | Nguồn | Nội dung áp dụng |
|---|---|---|
| Quy trình | `AGENTS.md`, `docs/README.md`, `docs/DEVELOPMENT_WORKFLOW.md`, README các nhánh tài liệu thiết kế/API/Postman/requirements/reports/sequence/class/state/decisions | BE → FE → BA Final Review → Tester; catalogue MSG; không tự kết luận Jira Done |
| Jira | [SCRUM-97](https://sep490g116.atlassian.net/browse/SCRUM-97), comment 10241; [SCRUM-20](https://sep490g116.atlassian.net/browse/SCRUM-20), change notes 10240 và 10242 ngày 21/10 | Quyết định PO 20/10 thay baseline cũ: Secretary được thay Chair inactive qua confirmed PUT |
| UC/BR | [Report 3](../08-reports/Report-3_Software_Requirement_Specification.md), FE-06: UC-27, UC-28a/b/c, UC-29, UC-30, UC-73; BR-92/126/169/294/295/296 và rules vai trò/đọc meeting liên quan | Chuyển giao một thao tác, bảo toàn vai trò bắt buộc, không chọn Guest/Secretary; Chair cũ ở lại |
| Sequence | Nguồn `.txt` UC-27, UC-28a/b/c, UC-29, UC-30, UC-73 trong `docs/06-sequencediagram/meeting-participant-management/` | Success, empty, recipient invalid, permission lost, atomic failure; ghi nhận khác biệt baseline |
| Thiết kế | Class diagram FE-06 trong `docs/07-classdiagram/`; state machine meeting/participant trong `docs/10-statemachine/`; `docs/02-design/01-vong-doi-cuoc-hop.md`, `02-erd.md`, `03-co-so-du-lieu.md`; [ADR-0021](../05-decisions/0021-vong-doi-thanh-phan-va-y-dinh-gui-thu.md) | Current assignment, remove evidence, durable dispatch, unique Chair, không đổi schema |
| API/quyền | `docs/03-api/00-danh-muc-ma-thong-bao.md`, `04-phan-quyen-phia-server.md`, `05-quan-ly-cuoc-hop.md`, `09-thanh-phan-tham-du.md`, `09-thanh-phan-tham-du-ban-giao.md`, `09-thanh-phan-tham-du-guest-uc04.md` | 401/403/404, context ba flags, ETag, Guest adapter, message mapping |
| Delivery | `docs/03-api/06-thong-bao-trong-ung-dung.md`, `07-gui-email-thu-moi.md`, Participants delivery/realtime contract | Hai notifications/dispatch trong transaction; realtime sau commit; không SMTP trong request |

Không thiếu UC-73 hoặc sequence UC-73/UC-28b. Có tài liệu BA còn theo baseline cũ: grants, recovery, quyền Organizer và Secretary không được thay Chair trong mọi trường hợp sau Published. Giữ baseline PO 20/10 đã ghi ở Jira; không đưa lại các hành vi cũ. BA đồng bộ khi Final Review. Hai API chuyên biệt, MSG258/260 và thứ tự lỗi là phần bổ sung của task này, được ghi thành change note cục bộ trong báo cáo để owner đăng.

## 3. Trình tự triển khai theo file

### Bước 1 — Chuẩn bị

1. Kiểm tra branch/HEAD/working tree và main; chỉ fast-forward khi sạch và đúng ancestor. Không reset dữ liệu nếu lệch.
2. Chạy Release build `-warnaserror` và toàn bộ tests trước sửa.
3. Dùng PostgreSQL 17/Redis 8 riêng; fixture tạo database riêng từng bộ tests. Cấu hình bằng `TRUSTCABINET_TEST_CONNECTION_STRING`, `TRUSTCABINET_TEST_REDIS`, không đưa credential vào source/log.

### Bước 2 — Domain contract

File `backend/TrustCabinet.Domain/Participants/ParticipantContracts.cs`:

1. Thêm operations `ChairHandoverCandidates`, `HandOverChair`, failure `CannotHandOverChair`.
2. Thêm `ChairHandoverCandidate(ParticipantId,UserId,FullName,Email)` và page `(Items,Total,EligibleTotal,Page,PageSize,Revision)`.
3. Thêm optional input `NewChairParticipantId`, `Confirmed`. Dùng lại `InvalidChairRecipient`, `InvalidRole`, `ChairHandedOver` và lỗi persistence/revision hiện có.
4. Empty state biểu diễn bằng `EligibleTotal == 0`; endpoint ánh xạ MSG258. Domain không chứa literal MSG hoặc HTTP status.

### Bước 3 — HTTP mapping

File `backend/TrustCabinet.Web/Endpoints/ParticipantEndpoints.cs`:

1. Đăng ký GET `/participants/chair-handover-candidates` và POST `/participants/chair-handover` vào group hiện có; không thêm pipeline/service riêng trong Program.
2. GET chấp nhận đúng `q/page/pageSize`. POST không nhận query.
3. Tạo body riêng `{newChairParticipantId,confirmed}`, strict JSON case-sensitive; unknown/duplicate/null sai, ID thiếu/rỗng/sai hoặc confirmed khác true → MSG101.
4. Giữ giới hạn 64 KiB/JSON media và ETag hiện có. Khai báo MSG258/260 bằng named constants.
5. Map page sang response đủ `items,total,eligibleTotal,page,pageSize,revision,code,message`. Hai field cuối null nếu còn candidate toàn meeting. POST dùng mutation success chung MSG257.

### Bước 4 — GET candidates

Trong `ParticipantService.ReadAsync`, dùng transaction RepeatableRead và repository snapshot hiện có:

1. Kiểm access trước. Guest đọc hợp lệ → MSG85; internal non-Chair hoặc status ngoài Published/InProgress → MSG260.
2. Validate query sau quyền: page>=1, size1..100, search trim<=200; sai MSG276.
3. Lọc đúng meeting, `ParticipantRules.Current`, role Member, User active, khác caller. Không yêu cầu Accept, có mặt hoặc kết nối.
4. Tính eligibleTotal trước search; search name/email OrdinalIgnoreCase; sort fullName OrdinalIgnoreCase rồi participantId; total trước paging. Dùng offset long tránh overflow.
5. Trả page + cùng revision/ETag snapshot. Nhánh này kết thúc trước mọi Guest profile lookup.

### Bước 5 — POST transaction và thứ tự lỗi

Trong `ParticipantService.MutateAsync`:

1. Transaction ReadCommitted; repository khóa meeting `FOR UPDATE` trước snapshot. Khóa account actor; access theo database.
2. Dùng guard riêng UC-73: Guest readable403/MSG85; không còn Chair/status sai409/MSG260. Giữ nguyên guard cũ cho PUT.
3. Trả lỗi parse body/content size/media, rồi kiểm header If-Match có mặt/đúng cú pháp.
4. Đọc lại account current bằng `FOR SHARE ORDER BY id`, untracked. Không gán snapshot User vào entity tracked; không phát UPDATE users.
5. Kiểm target ID là current active Member khác caller; không hợp lệ400/MSG259, trước revision stale409/MSG275.
6. Tạo đúng hai changes trong server: outgoing→Member, incoming→Chair; tạo confirmation từ hai assignment IDs. Không gọi HTTP PUT hoặc MutateAsync lồng.
7. Dùng lại ChangeAsync với account snapshot đã khóa; helper eligibility dùng chung với live Chair replacement. Kiểm đủ Chair/Secretary409/MSG109 trước thay đổi.
8. Demote Chair cũ; SaveChanges trong cùng transaction để giải phóng unique index. Promote người nhận và stage hai role notifications/dispatch, tăng revision một lần, audit `meeting.participants.HandOverChair` chứa đúng hai changes.
9. SaveChanges lần cuối, commit một lần. Lỗi ở bất kỳ save rollback role/revision/audit/notifications/dispatch, trả503/MSG102. Nhận diện cả wrapper timeout của Npgsql để mapping failure nhất quán.
10. Sau commit phát một participantsChanged đến readers trước/sau; publisher lỗi chỉ log. Không gọi remove hook, không rời phòng, không SMTP trực tiếp.

### Bước 6 — Tương thích và bàn giao

Giữ PUT assignments/MSG256/MSG100/MSG257; Secretary thay Chair inactive vẫn qua PUT, POST luôn chỉ current active Chair. Context vẫn đúng ba flags. Viết API/FE guide/Postman/catalogue trong cùng diff; không sửa UC/sequence/accepted ADR hàng loạt. FE dùng GET làm nguồn candidates, một POST duy nhất, reload context/detail/roster và dedupe realtime eventId.

## 4. Ma trận kiểm thử bắt buộc

| Nhóm | Bằng chứng cần có |
|---|---|
| Success | Published/InProgress; hai role đổi, IDs giữ nguyên, một Chair/Secretary, revision+1, ETag/cache headers |
| Bảo toàn | Scalar snapshot response/respondedAt, attendance recorder/time, user/system role, ballot/receipt/option/session và history có dữ liệu thật |
| Candidates | Active/current Member, Guest/Secretary/Chair/inactive/removed/delegated/cross-meeting bị loại; count trước search, paging tie deterministic, empty MSG258, search miss null |
| Access/state | 401, outsider404, readable Guest403/MSG85, Member/Secretary/Organizer409/MSG260; Draft/WaitingApproval/Rejected/Approved/Ended chặn trước validation |
| Validation | JSON/schema/media/size/header/query; target self/Secretary/inactive/removed/missing/Guest/cross-meeting; thiếu Secretary |
| Atomicity | Inject lỗi save1 và save2; không có dữ liệu lưu một phần hoặc event; retry sau rollback thành công |
| Concurrency | Hai POST cùng ETag; POST cạnh tranh PUT cả hai thứ tự; target bị gỡ/thành Secretary, roster khác đổi, meeting ended trong transaction cạnh tranh trước lock; deactivation sau snapshot trước account lock |
| Delivery | Hai notifications, hai dispatch, một audit/event; transport fail sau commit vẫn MSG257; worker materialization/retry dedup email; SignalR long polling cho cả Chair cũ/mới và không có event rollback |
| Regression | Legacy PUT, inactive Chair account-lock regression, context/detail/roster quyền mới, toàn suite có PostgreSQL/Redis thật |

Các lớp mới: `ChairHandoverEndpointTests`, `ChairHandoverConcurrencyTests`; mở rộng `ParticipantDeliveryTests`, `ParticipantHubTests` và hooks fixture. Không dùng EF InMemory hoặc chỉ gọi request tuần tự để chứng minh khóa.

## 5. Các lệnh nghiệm thu

```powershell
dotnet restore TrustCabinet.sln
dotnet build TrustCabinet.sln --no-restore --configuration Release -warnaserror
dotnet test TrustCabinet.sln --no-build --configuration Release --filter "FullyQualifiedName~Participants|FullyQualifiedName~MessageCodeCatalogueTests"
dotnet test TrustCabinet.sln --no-build --configuration Release --logger "trx;LogFileName=scrum97-full.trx"
npm --prefix frontend run lint
npm --prefix frontend run build
dotnet dotnet-ef migrations has-pending-model-changes --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web
git diff --check
```

Thực chạy Postman/Newman smoke trên ứng dụng/database riêng: candidates→POST→đọc role/context, request cấm và stale. Lưu log/TRX thật, số pass/fail/skip và môi trường trong báo cáo; không tái dùng số từ report cũ. Không nâng package để sửa dependency; nếu cần dùng npm ci theo lockfile.

Hoàn tất Backend khi mọi check phù hợp pass, contract khớp response và model/FE source không đổi. Chưa đồng nghĩa Jira Done: còn FE, BA Final Review và Tester. SMTP thật/Guest OTP production/LiveKit disconnect nằm ngoài nghiệm thu API này.
