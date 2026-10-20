# SCRUM-20 — kết quả kiểm thử Backend

## Sửa review 19/10/2026 — bản tích hợp local chưa commit

Ghi chú do AI hỗ trợ soạn. Kiểm thử working tree trên HEAD `169fd3c`, MERGE_HEAD `3790928`; không tạo commit/push hoặc kết thúc merge. 64 file staged có sẵn được giữ nguyên; các sửa mới nằm ở working tree.

- Sửa test `Publish_does_not_notify_the_publisher_in_app_even_when_they_are_a_participant`: assert đúng tập Chair, Secretary, Member, không thiếu/trùng recipient; assert publisher bị loại. Không sửa production notification policy.
- Giữ mapping Participants MSG275/MSG276 đã giải quyết merge; API/handoff nêu rõ SCRUM-97 sẽ thay hành vi Secretary đổi Chair sau Published và bổ sung UC-73. Chưa triển khai yêu cầu đó trong SCRUM-20.
- Release build `-warnaserror`: **0 warning/error**. Full suite trên PostgreSQL 17 và Redis 8 riêng: **1093 passed, 0 failed, 0 skipped**, khoảng 2m25s; bao gồm test vừa sửa và nhóm Participants.
- Frontend lint/build, `git diff --check HEAD`, parse Postman JSON đều pass. EF không có model changes chưa migration.
- Evidence local: `%TEMP%/scrum20-review19/review19.trx`, `%TEMP%/scrum20-review19-build.log`, `%TEMP%/scrum20-review19-tests.log`, `%TEMP%/scrum20-review19-fe-lint.log`, `%TEMP%/scrum20-review19-fe-build.log`. Các container test tạm được dừng sau test.
- Đây **không phải CI của commit cuối trên PR**: cần commit/push bản tích hợp và chạy CI sau đó. Backup/restore/preflight và image cũ của server test chưa xác minh; không merge/deploy. SMTP thật và Guest/room E2E vẫn chưa kiểm chứng.

Các phần bên dưới giữ nguyên để truy vết các lượt kiểm thử trước đó.

Chốt ngày **15/10/2026 (Asia/Saigon)**. Branch `feat/scrum20-Backend-Invite-participants-and-acept`, base HEAD `6a4a7fe`, thay đổi chưa commit. Backend-only; không sửa source/lockfile Frontend, đã gửi change note Jira theo yêu cầu sửa review, không merge/deploy.

**Cập nhật khi giải quyết merge ngày 15/10/2026:** main `e754010` đã dành MSG238/MSG239 cho ADR-0020 và dùng ADR-0019 cho biểu quyết. Participants chuyển sang MSG252/MSG253, mã tiếp theo MSG254; ADR của Participants chuyển sang ADR-0021. Các kết quả kiểm thử bên dưới là evidence trước merge, với phân bổ mã tại thời điểm đó.

**Cập nhật khi merge main `3790928` ngày 19/10/2026:** main đã dành MSG252–MSG274 cho thay đổi Report 3; Participants hiện dùng MSG275 (stale revision) và MSG276 (query/If-Match không hợp lệ), mã tiếp theo MSG277. Dòng và kết quả kiểm thử phía dưới giữ nguyên như evidence lịch sử.

**Kiểm tra bản giải quyết merge:** Release build `-warnaserror` có 0 warning/error; frontend lint/build pass. Full suite có 1.063 passed, 25 failed do Redis localhost6379; chạy lại riêng 25 test đó trên Redis8 tạm tại127.0.0.1:6389 có 25/25 passed. Tổng 1.088 test được xác minh qua hai lượt, 0 skipped; gồm hai regression bảo toàn MSG238/MSG239 cho tài liệu. Container test tạm đã dừng. Postman JSON, số ADR duy nhất và `git diff --check` pass.

## Sửa review ngày 15/10/2026

- **P2 — Chair inactive:** chỉ cho phép Chair cũ inactive xuống Member trong batch thay Chair có confirmation đúng; incoming phải active, vẫn giữ required Secretary và rollback atomic. Regression chạy cả Published/InProgress, thiếu confirmation, incoming inactive và lỗi SaveChanges sau demote.
- **P2 — Guest dời lịch:** tạo dispatch Rescheduled cho current Guest cùng transaction schedule/history/notification. EventId = history ID; unique key theo event + invitation. Regression kiểm Guest revoked bị loại, lỗi lưu rollback toàn bộ, save cùng lịch no-op, worker đồng thời chỉ tạo một email và lần dời lịch tiếp theo tạo event mới.
- **P2 — mã trùng:** fetch/đọc origin/main `2bdaae7c6547bb586baf917437492d387e7304e3`, giữ nguyên toàn bộ các dòng đã dành; dùng MSG238/MSG239 cho Participants, next MSG240. Đồng bộ constants, tests, API, plan, handoff và Postman. Không merge main vào branch.
- **P3 — workflow:** đã gửi [change note 10221](https://sep490g116.atlassian.net/browse/SCRUM-20?focusedCommentId=10221), liên kết SCRUM-59 và ghi nhận assignee hiện tại Nguyễn Quang Huy, scope hai grant, UC-04 adapters và điều kiện Guest production. Không đổi status/assignee.

Ba regression case mới **fail trên code trước sửa**, sau sửa nhóm regression + message catalogue **64/64 pass**. Full suite **1086/1086 pass**, 0 skipped. Release build `-warnaserror`: 0 warning/error. EF xác nhận không có pending model changes; enum event lưu string nên không cần migration mới. Postman JSON, relative doc links và `git diff --check` pass. Frontend không thay đổi và không chạy lại lint/build trong lượt sửa review (kết quả trước đó bên dưới).

Artifacts lượt sửa review: `%TEMP%/scrum20-review-fixes/review-regressions-before.trx`, `review-regressions-after.trx`, `review-full.trx`; `%TEMP%/scrum20-review-build.log`, `%TEMP%/scrum20-review-full.log`. Full suite dùng Redis8 riêng `trustcabinet-scrum20-review-redis`, localhost6389; container được dừng sau kiểm thử. Các số đo performance chi tiết bên dưới thuộc lượt trước review, không phải phép đo mới của lượt này.

## Thay đổi đã hiện thực

- API roster/context/options, recovery Secretary, thêm internal/Guest, grants độc lập, Chair swap atomic, gỡ mềm/revoke, phản hồi của chính actor và summary. ETag/revision chống stale write; strict JSON và giới hạn batch/payload.
- Migration lifecycle/Guest/grants/dispatch, giữ evidence, preflight dữ liệu legacy và guard downgrade. Shared access Meeting/Agenda/reminder và realtime nội bộ loại assignment Removed.
- Dispatch cùng transaction domain/audit/notification; worker materialize idempotent trước SMTP queue hiện có. Guest default deny/unavailable, production OTP/session chờ UC-04.
- Fixture Development insert-only cho bảy trạng thái; API contract, FE/manual/Postman, UC-04 handoff và ADR-0021.

## Môi trường và kết quả

Windows, Ryzen 7 7840HS / 16 logical processors; .NET SDK10.0.402 được `global.json` cho phép roll-forward từ10.0.401, runtime10.0.13, Node25.8.0. PostgreSQL17 Docker; mỗi integration fixture/migration test dùng database riêng. Không áp dụng migration lên database dùng chung.

Redis localhost6379 trên máy trả về Redis3.0.504, khác container dự án Redis8. Vì vậy baseline ban đầu có25 test Redis fail. Đã dùng container test Redis8 riêng, bind127.0.0.1:6389, và thêm biến `TRUSTCABINET_TEST_REDIS` cho hai Redis integration suites (default vẫn localhost6379). Không tắt/skip test và không đụng dữ liệu Redis hiện có. Container test tạm không còn tồn tại khi kiểm tra cuối.

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build TrustCabinet.sln -c Release -warnaserror` | PASS, 0 warning, 0 error |
| `dotnet test TrustCabinet.sln -c Release --no-build` | **1086 passed, 0 failed, 0 skipped**, khoảng2m25s |
| Namespace `Participants` trong full suite | 41 passed; thêm1 case soft-remove qua API trong AgendaHubTests |
| Migration fresh/upgrade/preflight/downgrade + DB constraints | PASS trên PostgreSQL thật |
| `dotnet dotnet-ef migrations has-pending-model-changes ... --no-build` | Không có model changes chưa được migration bao phủ |
| `npm ci`, `npm run lint`, `npm run build` | PASS; audit có finding riêng bên dưới |
| `git diff --check` | PASS |

Artifact chi tiết ở máy chạy: `%TEMP%/scrum20-release/release.trx`, `%TEMP%/scrum20-release-tests.log`, `%TEMP%/scrum20-release.log`. Lượt đo riêng: `%TEMP%/scrum20-performance/performance.trx`. Không đưa log chứa môi trường riêng hoặc credentials vào git.

## Phạm vi test đã chạy

| Nhóm | Evidence chính |
|---|---|
| Quyền và trạng thái | Bảy trạng thái; roster/summary độc lập; own response; anonymous/outsider/inactive; recovery chỉ preparation và không bypass inactive current Secretary |
| Validation/security | Strict JSON/casing/duplicate/spoof fields; unknown query; If-Match thiếu/cũ; media type; payload64KiB kể cả unknown Content-Length; email citext; batch atomic |
| Recovery và lifecycle | Promote current Member giữ response/attendance; gỡ rồi mời lại có ID/Pending mới; removed Organizer giữ independent meeting access nhưng không giữ grant từ assignment |
| Role/grants | Chair confirmation; final role invariant; thay Secretary cuối atomic; rollback sau demote Chair; permission catalogue; grant Guest độc lập |
| Concurrency | Hai add cùng revision; hai Chair swap; response đua với removal; hai worker materialize cùng dispatch; publish đồng thời qua suite Meeting |
| Guest consumer | Verified/unverified/expired/revoked/mixed identity/wrong meeting; hai session revoke riêng; profile tự khai không đổi invitation evidence; default production fail-closed |
| Transaction/delivery | Publication snapshot gồm grants/Guest và rollback cùng transaction; disabled SMTP giữ intent; BlockedDependency không đốt retries; revoked suppression; removal không link/ICS; invalid provider origin; retry exhaustion; failure lưu EmailMessage rollback rồi retry không duplicate |
| Access regression | Meeting/Agenda không nhận quyền từ Removed; hub đã join rồi remove qua API không nhận event mới; toàn bộ notification/auth/role/meeting/agenda/email suites cũ chạy lại |
| Migration | Giữ Response/RespondedAt/Attendance/recorder/timestamp; backfill legacy delivery key; reject Guest/Delegated/multiple Chair trước upgrade; unique current Chair; XOR owner; history re-add; chặn Down có evidence |
| Development seed | Bảy trạng thái; preparation recovery; chạy lại không reset edit hoặc nhân đôi meeting |

Các fixture công bố cũ dùng Chair/Secretary Delegated chỉ để tránh nhận thông báo đã được sửa thành assignment current hợp lệ. Assertions publication giờ kiểm dispatch persisted và materialize sau commit; không giữ kỳ vọng email phải có ngay trong HTTP request. Các negative tests delegation vẫn được giữ.

## Performance

TestServer + PostgreSQL thật, pool20, không SMTP/network Internet trong request. Seed503 internal current; đo GET roster sau warm-up; sau đó mời thêm100 user thành603 rows. Không đặt assertion thời gian dễ flaky; vẫn assert200/no errors, pagination/count và giới hạn số DB queries. Reader sử dụng no-tracking; batch add tạm ngừng auto DetectChanges trong loop, bật lại trước SaveChanges để tránh quét cả graph cho từng recipient.

Lượt riêng, không full suite cạnh tranh tài nguyên:

| Phép đo | Kết quả |
|---|---|
| GET roster503 rows, page100, 5 lượt | 49/48/38/28/33ms, median38ms; số query không tăng theo từng recipient |
| Batch add100 người | 534ms, 10 DB commands, 100 dispatch |
| 100 caller đồng thời, mỗi caller GET roster rồi summary, round1 | 200 request, p50=722ms, p95=972ms, max1036ms, 0 errors |
| Round2 | p50=652ms, p95=797ms, max1067ms, 0 errors |
| Round3 | p50=673ms, p95=820ms, max939ms, 0 errors |

Trong full suite cuối, nhiều database/migration tests chạy song song: roster median40ms, batch551ms; load p95 lần lượt **1412/1767/1596ms**, 0 errors. Vì vậy mục tiêu p95≤1s chỉ đạt trong lượt đo riêng trên máy này, **không phải SLA production hoặc bảo đảm khi có cạnh tranh tài nguyên**. Chưa load test Lightsail. Phân trang payload ở server, nhưng snapshot hiện vẫn nạp current roster để ghép profile Guest; cần đo lại và tối ưu query/project DTO nếu quy mô lớn hơn.

## Dependency audit

Restore phát hiện advisory mới trong dependency có sẵn. Đã cập nhật `Microsoft.AspNetCore.SignalR.Client`10.0.13 và pin transitive `System.Security.Cryptography.Pkcs`10.0.13; không tắt NuGet audit/warnaserror. Nguồn bản vá: [Microsoft ASP.NET advisory](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-334f-vvp4-7g6p), [Microsoft runtime advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx).

Frontend `npm audit` còn **1 high** ở `source-map-js`1.2.1, [GHSA-68fv-2mgg-jv7q](https://github.com/advisories/GHSA-68fv-2mgg-jv7q), bản vá1.2.2. Đã ghi vào FE handoff; không chạy audit fix hoặc thay lockfile FE trong phạm vi Backend-only. Lint/build pass không đồng nghĩa dependency audit sạch.

## Manual, tích hợp và giới hạn nghiệm thu

- **Đã chạy:** automated HTTP integration qua TestServer, PostgreSQL/Redis thật, SignalR transport nội bộ, migration và load test nêu trên. Không gửi SMTP ra người thật.
- **Chưa chạy bằng tay:** collection Postman, browser/UI participant, mailbox SMTP sandbox end-to-end. Đã cung cấp22 kịch bản thao tác/kết quả mong đợi tại [FE/manual handoff](09-thanh-phan-tham-du-ban-giao.md) và [collection](postman/scrum20-participants.postman_collection.json). Không ghi manual Passed bằng kết quả automated thay thế.
- **Blocked by UC-04:** OTP, verified profile/session/credential và Guest E2E production. Consumer tests dùng adapter test-only; default production từ chối Guest. Room disconnect cần room owner nối callback; fresh server authorization không phụ thuộc callback.
- Roster DTO dùng context flags thay vì cờ edit riêng cho từng row đề xuất trong plan; FE dùng role + context để hiển thị và luôn xử lý server validation. Không phải mọi validation trả field errors: lỗi parser/invariant có code/message, một số lỗi input có dictionary errors. Contract thực tế đã ghi rõ.
- Không tuyên bố mọi case ma trận plan đã có một test riêng hoặc đo coverage100%. Các nhóm rủi ro trên có evidence, full regression xanh; FE integration → BA Final Review → Tester sign-off còn theo workflow.
- Sơ đồ/report mục tiêu giữ nguyên; ERD text phân biệt schema đã hiện thực với mô hình đích. Change note scope đã gửi: [SCRUM-20 comment 10221](https://sep490g116.atlassian.net/browse/SCRUM-20?focusedCommentId=10221); liên kết SCRUM-59/UC-04, assignee hiện tại Nguyễn Quang Huy.

## Chạy lại

```powershell
# PostgreSQL test lấy credentials từ cấu hình test/.env hiện có, không in ra console.
# Nếu localhost6379 là Redis8 đúng của dự án thì không cần override này.
$env:TRUSTCABINET_TEST_REDIS = '127.0.0.1:6389'
dotnet build TrustCabinet.sln -c Release -warnaserror
dotnet test TrustCabinet.sln -c Release --no-build --verbosity normal
dotnet test TrustCabinet.sln -c Release --no-build --filter FullyQualifiedName~ParticipantConcurrencyTests
dotnet dotnet-ef migrations has-pending-model-changes --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --configuration Release --no-build
```

Biến6389 chỉ hợp lệ khi container test riêng đang chạy; container không còn tồn tại khi kiểm tra cuối, nên khởi động một Redis test phù hợp hoặc bỏ override để dùng6379. Từ frontend: `npm ci`, `npm run lint`, `npm run build`. Không cần `dotnet tool restore` vì checkout này không có tool manifest; EF CLI đã có sẵn trên máy thực thi.
