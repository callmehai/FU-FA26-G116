# SCRUM-97 — Kết quả triển khai và kiểm thử Backend

Ngày 22/10/2026, Asia/Saigon. Kết quả trong tài liệu là các lệnh thực chạy của phiên này, không lấy từ báo cáo SCRUM-20 cũ. **Backend sẵn sàng tích hợp**; chưa kết luận Jira Done khi FE, BA Final Review và Tester chưa hoàn tất.

## 1. Baseline và phần thay đổi

Thông tin Git và kết quả ở mục 1–5 ghi nhận phiên triển khai đầu. Bản sửa sau review độc lập và kết quả xác minh mới nằm ở mục 6.

Branch: `refactor/SCRUM-97-BACKNEND-Synchronize-attendees-according-to-PO-relate-to-SCRUM-20`.
HEAD ban đầu `adca53ba29c0b4c670cd01984b675d17018c8f4c`, working tree sạch. Đã fast-forward đúng yêu cầu owner tới `875dde2b09702af539bb49fda2c44c8396d0e235`. Không tạo commit mới. Mọi thay đổi SCRUM-97 vẫn nằm trong working tree, chưa stage/commit/push.

`main` trước và sau đều là `ed94dc99f545121fe15f3981c2a783a34a2ae90f`. Không merge main, mở PR, deploy, đăng comment hoặc đổi trạng thái Jira.

Kế thừa từ SCRUM-20: atomic Chair swap trong PUT assignments, confirmation, MSG256/257/259, unique index, audit, notification/dispatch, publisher và PO20/10 Secretary thay Chair inactive. SCRUM-97 bổ sung:

- GET `/api/meetings/{meetingId}/participants/chair-handover-candidates`, lọc và phân trang server-side, counts trước/sau search, revision/ETag, empty state200/MSG258.
- POST `/api/meetings/{meetingId}/participants/chair-handover`, chỉ current active Chair Published/InProgress, body ID người nhận + confirmation, actor/Chair cũ do server xác định.
- MSG260 và thứ tự lỗi actor → body/header → recipient → stale → required roles. Dùng account state vừa khóa, không UPDATE users ngoài ý muốn.
- Reuse transaction/ChangeAsync và side effects, audit action mới; nhận diện wrapper timeout của Npgsql để trả lỗi đọc/lưu đúng contract.
- 57 tests bổ sung, Postman smoke UC-73, contract/FE guide/catalogue/plan và change note cục bộ.

Không thay entity/configuration/model/migration hoặc source Frontend. `git diff 875dde2 -- frontend backend/TrustCabinet.Infrastructure/Migrations` rỗng. FE build chỉ tạo output gitignored `wwwroot`.

## 2. File thay đổi và lý do

| File (từ repository root) | Thay đổi |
|---|---|
| `backend/TrustCabinet.Domain/Participants/ParticipantContracts.cs` | Operation/failure, candidate DTO/page và input chuyên biệt |
| `backend/TrustCabinet.Infrastructure/Participants/ParticipantService.cs` | GET filtering, UC-73 access gate, target validation trước stale, shared fresh account locks/eligibility, transaction reuse, persistence error mapping |
| `backend/TrustCabinet.Web/Endpoints/ParticipantEndpoints.cs` | Hai routes, strict body riêng, MSG258/260, envelope/ETag |
| `tests/TrustCabinet.Web.Tests/Participants/ChairHandoverEndpointTests.cs` | Success, quyền/status, dữ liệu bảo toàn, validation, filtering, rollback và lỗi transport |
| `tests/TrustCabinet.Web.Tests/Participants/ChairHandoverConcurrencyTests.cs` | POST/POST, POST/PUT, target/status/revision/account changes với DbContext độc lập và synchronization |
| `tests/TrustCabinet.Web.Tests/Participants/ParticipantFixture.cs` | Hooks trước meeting/account lock và lỗi đọc; dùng fixture PostgreSQL thật |
| `tests/TrustCabinet.Web.Tests/Participants/ParticipantDeliveryTests.cs` | Handover dispatch materialization/retry concurrent không tạo email trùng |
| `tests/TrustCabinet.Web.Tests/Participants/ParticipantHubTests.cs` | SignalR long polling đến Chair cũ/mới, chỉ sau commit |
| `docs/02-design/10-scrum97-ke-hoach-backend.md`, `docs/02-design/README.md` | Lưu plan đã duyệt, nguồn đối chiếu và hướng dẫn thực thi |
| `docs/03-api/00-danh-muc-ma-thong-bao.md` | MSG258/260 thành implemented, giữ số mã/nextMSG285, sửa tổng hợp reserved |
| `docs/03-api/09-thanh-phan-tham-du.md` | Contract UC-73, thứ tự lỗi, tương thích PUT |
| `docs/03-api/09-thanh-phan-tham-du-ban-giao.md` | Luồng dialog, request, phản hồi/realtime để FE triển khai |
| `docs/03-api/postman/scrum20-participants.postman_collection.json`, `docs/03-api/postman/README.md` | Folder UC-73: 9 requests tuần tự, token chỉ qua environment |
| `docs/03-api/10-scrum97-ket-qua-kiem-thu.md`, `docs/03-api/README.md` | Báo cáo này và liên kết chỉ mục |

## 3. Môi trường và bằng chứng thực chạy

- Windows, .NET SDK **10.0.402** (global.json 10.0.401 cho phép latestMinor), Node **v25.8.0**; CI dùng Node22, chưa chạy CI vì chưa push.
- PostgreSQL **17**, Redis **8** qua containers dành cho kiểm thử `scrum20-po-pg` (localhost5439), `scrum20-po-redis` (localhost6389). Fixture tạo database riêng và tự dọn, không dùng database server test hoặc dữ liệu làm việc.
- Connection string/Redis cấu hình qua environment; credential không xuất vào source hoặc báo cáo.
- Kestrel smoke tại `http://127.0.0.1:5297`, Development, database mới tên prefix `scrum97_smoke_`, migration/seed bằng ứng dụng, SMTP/Google disabled. JWT key ngẫu nhiên trong process; login bằng tài khoản demo của seeder. Token Newman giữ trong bộ nhớ, không export environment/report chứa token.
- Log/TRX cục bộ: `C:\Users\PC\AppData\Local\Temp\scrum97-results\`.

| Check | Kết quả | Artifact trong thư mục log |
|---|---|---|
| Baseline Release build `-warnaserror` | Pass, 0 warning/error | `baseline-build.log` |
| Baseline full tests trước sửa | **1170 pass, 0 fail, 0 skip**, 1m58s | `baseline-test.log`, `baseline.trx` |
| `dotnet restore TrustCabinet.sln` | Pass | `restore.log` |
| Release build cuối `--no-restore -warnaserror` | Pass, **0 warning/error** | `final-build.log` |
| Tests UC-73 và delivery/hub mới | **57 pass, 0 fail, 0 skip** | `targeted.log`, `scrum97-targeted.trx` |
| Participants + MessageCodeCatalogueTests | **213 pass, 0 fail, 0 skip** | `regression.log`, `scrum97-regression.trx` |
| Full backend suite | **1227 pass, 0 fail, 0 skip**, 1m55s | `full.log`, `scrum97-full.trx` |
| Concurrency sau bổ sung synchronization ở request đang chờ meeting lock | **9 pass, 0 fail, 0 skip** | `concurrency-final.log`, `scrum97-concurrency-final.trx` |
| `npm --prefix frontend run lint` | Pass, exit0 | `frontend-lint.log` |
| `npm --prefix frontend run build` | Pass; có cảnh báo bundle >500kB, không sửa FE ngoài scope | `frontend-build.log` |
| EF `migrations has-pending-model-changes` | Không có thay đổi model | `model.log` |
| Newman, folder `UC-73 — smoke tuần tự` | **9 requests, 12 assertions, 0 failure**, 1.738s | `postman-smoke.log` |
| `git diff --check` | Pass | Kiểm trực tiếp working tree |

Full suite chạy trên code runtime cuối; sau đó chỉ tăng synchronization của ba concurrency scenarios trong tests để chờ request thứ hai đến meeting lock trước khi thả request thứ nhất. Build lại và chạy đủ 9 concurrency tests. Không thay runtime sau full suite/smoke.

Các lệnh backend thực chạy dùng `--configuration Release`; tests dùng `--no-build`, `--results-directory "$env:TEMP/scrum97-results"`, logger TRX. Filter regression: `FullyQualifiedName~Participants|FullyQualifiedName~MessageCodeCatalogueTests`. EF dùng `--project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --configuration Release --no-build`.

Newman được gọi bằng Node API với collection đã lưu và environment trong bộ nhớ; chọn đúng folder UC-73. Các bước kiểm: candidates/ETag → POST MSG257/revision+1 → context Chair cũ Member → context Chair mới → retry cũ MSG260 → candidates mới → valid actor/recipient với ETag cũ MSG275 → search miss không MSG258 → context vẫn Chair mới. Có thể chạy lại trên database mới; fixture đã swap không còn trạng thái khởi đầu của collection.

## 4. Phạm vi kiểm chứng

Tests mới chứng minh:

- Handover ở Published/InProgress giữ đúng assignment IDs, response/respondedAt, attendance/recorder/time, scalar user/system role, ballot/receipt/option/session và một history row có dữ liệu sẵn; chỉ role và UpdatedAt của hai assignment thay đổi.
- Chỉ active current Member đủ điều kiện; pagination ổn định, eligibleTotal độc lập search, không gọi Guest profile adapter. MSG258 chỉ khi không còn candidate toàn meeting.
- Actor/status gate trước validation; recipient invalid trước stale; account deactivate giữa snapshot và lock không được nhận Chair; Secretary thay Chair inactive qua PUT vẫn thành công và không UPDATE users.
- Khóa meeting tuần tự hóa hai POST, POST/PUT cả hai thứ tự; thay đổi target/meeting/roster qua transaction độc lập đang giữ lock được nhìn thấy sau khi request đợi khóa.
- Inject lỗi save1/save2 rollback toàn bộ role/revision/audit/notification/dispatch và không có event. Realtime lỗi sau commit vẫn MSG257.
- Success tạo đúng 2 notifications, 2 dispatch, 1 audit, 1 invalidation; dispatch retry concurrent không tạo EmailMessage trùng. SignalR long polling thật qua TestServer tới hai reader; rollback không phát event, Chair cũ vẫn join được.
- Toàn suite cũ pass, bao gồm legacy PUT, account-lock regression, catalogue, meeting/access/auth/delivery/call regressions.

Lỗi gặp trong lần viết tests đầu: fixture Removed thiếu removedAt/removedBy theo constraint, và so sánh DateTimeOffset trước roundtrip PostgreSQL bị khác độ chính xác. Đã sửa fixture/snapshot để so sánh dữ liệu thực đã lưu; không nới constraint hoặc bỏ test. Test timeout phát hiện wrapper Npgsql cần mapping bổ sung trong service. Các run cuối đều xanh.

Chưa kiểm SMTP thật, Guest OTP production, LiveKit disconnect thật hoặc UI end-to-end; đây không phải điều kiện nghiệm thu phần API mới. FE lint/build không thay thế tích hợp dialog UC-73. Không có schema change, không có migration mới.

## 5. Change note Jira — bản nháp, chưa đăng

Nội dung để owner đăng vào SCRUM-97/SCRUM-20 khi bàn giao:

> Backend SCRUM-97 bổ sung trên baseline SCRUM-20 `875dde2` ngày 22/10/2026. Thêm GET chair-handover-candidates và POST chair-handover dành cho current active Chair ở Published/InProgress; POST chỉ nhận assignment ID người nhận + confirmed true và If-Match. MSG258 đã triển khai là empty state HTTP200; MSG260 là HTTP409 khi actor/status không còn cho chuyển giao. Recipient invalid MSG259 được ưu tiên trước stale MSG275. Giữ atomic swap, audit/notification/durable dispatch/realtime; không đổi model/migration. PUT assignments vẫn tương thích, kể cả ngoại lệ PO20/10 Secretary thay Chair inactive qua confirmed batch; Secretary không dùng POST mới.
>
> Kiểm thử local: baseline1170 pass; sau bổ sung1227 pass, 0 fail/skip; 213 Participants/catalogue pass; Release -warnaserror 0 warning/error; FE lint/build pass; EF không pending changes; Newman9 requests/12 assertions pass. Source FE không đổi. Tài liệu API/FE/Postman đã cập nhật; chưa commit/push/PR/merge/deploy trong phiên này, kết quả local không thay thế CI của commit tương lai.
>
> BA Final Review cần đối chiếu UC-73, UC-28b, BR-126/169/294/295/296, sequence UC-73/UC-28b và class FE-06: API chuyên biệt và error precedence; empty search khác không có candidate; eligibility không yêu cầu Accepted/Present/room connection; Chair cũ giữ Member; ngoại lệ Chair inactive và bỏ grants/recovery theo change note10240. Contract `docs/03-api/09-thanh-phan-tham-du.md` §8 là nguồn bàn giao kỹ thuật. FE dùng candidates mới + một POST, reload context/detail/roster theo response/event; chưa đánh dấu toàn Jira Done.

Các khác biệt baseline cũ đã ghi trong comment10240/10242 của SCRUM-20 và10241 của SCRUM-97. Phần bổ sung hai API/MSG258/260 ở đây **chưa đăng Jira** theo phạm vi owner yêu cầu. Không sửa hàng loạt tài liệu BA hoặc ADR đã chấp nhận.

## 6. Sửa review P2 — ưu tiên lỗi body/media/size trước query

Ngày 22/10/2026. Đầu phiên sửa: đúng branch SCRUM-97, HEAD `9c7ad556f603eaf26ff06d87544e0e8178639e3c`, working tree sạch. Giữ nguyên commit hiện có; phần sửa này chưa stage/commit/push. Main vẫn `ed94dc99f545121fe15f3981c2a783a34a2ae90f`.

Finding hợp lệ: nhánh query trong `ParticipantEndpoints.Write` ghi đè `ParseFailure` dù ParseAsync đã phát hiện lỗi. Với POST chair-handover có `?unexpected=1`, JSON sai/confirmed false/media sai/body quá lớn đều trả MSG276, trái thứ tự API §8.3.

Sửa tối thiểu: riêng operation HandOverChair, chỉ gán InvalidRequest cho query khi ParseFailure còn None. Các mutations khác, gồm PUT assignments, giữ nguyên thứ tự lỗi hiện hành. Không thay contract, model hoặc FE.

Thêm theory gồm bốn trường hợp, đều có current active Chair, meeting Published, If-Match hợp lệ và query lạ:

| Lỗi kèm query lạ | POST handover sau sửa |
|---|---|
| JSON sai cú pháp | 400/MSG101 |
| confirmed false | 400/MSG101 |
| Content-Type text/plain | 415/MSG34 |
| Body 65.537 byte | 413/MSG87 |

Mỗi trường hợp cũng gửi PUT assignments để kiểm chứng vẫn trả400/MSG276 và xác nhận không đổi Chair/revision/audit/notification/dispatch/event. Tests query với body hợp lệ hiện có tiếp tục kiểm400/MSG276.

Kết quả thực chạy trên PostgreSQL17/Redis8 riêng:

- Trước sửa: cả bốn tests mới fail đúng finding (`p2-red.log`).
- Sau sửa: Release build `--no-restore -warnaserror` pass, 0 warning/error (`p2-build.log`).
- Participants + MessageCodeCatalogueTests: **217 pass, 0 fail, 0 skip** (`p2-regression.log`, `scrum97-p2-regression.trx`).
- `git diff --check`: pass.

Artifacts nằm tại `C:\Users\PC\AppData\Local\Temp\scrum97-results\`. Phiên sửa này không chạy lại full suite, Newman hoặc FE build; số1227/full-suite và9 requests/Newman ở mục3 là kết quả trước bản sửa P2, không phải kết quả mới. Không đăng Jira hoặc triển khai môi trường chung.
