# SCRUM-69 — báo cáo kiểm thử Backend

Phiên triển khai và kiểm chứng 22–23/10/2026 (Asia/Saigon). Branch
`feat/SCRUM-69-BACKEND-Starting-and-ending-a-meeting`, base HEAD `f6db57f`.
Thay đổi chưa commit/push/merge/deploy. Không sửa source Frontend, không thêm migration/schema.

## Kết quả

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build TrustCabinet.sln --configuration Release -warnaserror` | PASS — 0 warning, 0 error |
| `dotnet test TrustCabinet.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --logger "trx;LogFileName=scrum69-review-full-isolated.trx"` | PASS — **1.252 passed, 0 failed, 0 skipped**, khoảng 2m34s (sau sửa review P2) |
| Nhóm SCRUM-69 (`FullyQualifiedName~MeetingLifecycle`) | PASS — 72 test service/HTTP/PostgreSQL/concurrency/SignalR/performance (gồm 5 regression case P2) |
| Workflow/HTTP/catalogue cùng lifecycle ở mốc kiểm tra trước realtime | PASS — 306 test |
| `npm --prefix frontend run lint` | PASS |
| `npm --prefix frontend run build` | PASS — cảnh báo chunk MeetingRoomPage lớn hơn 500 kB; không sửa FE trong task này |
| `git diff --check` | PASS |
| Postman JSON | Parse hợp lệ, 11 request đều có assertion; chưa chạy riêng bằng Newman |
| Smoke LiveKit v1.13.7, Chrome headless, hai client không media | PASS — End ngắt client, join ticket mới bị từ chối, token cũ được kiểm tra và reconciliation ngắt lại |

Test PostgreSQL dùng `PostgreSqlTestDatabase`, mỗi fixture một database ngẫu nhiên và pool riêng,
áp migrations hiện có rồi xóa database test; không ghi database Development/chung của nhóm.
SDK local 10.0.402, runtime 10.0.13; PostgreSQL 17, Redis 8, Windows/Docker Desktop.

Lượt full suite đầu có 25 lỗi Redis do cổng 6379 trên host trả về Redis Windows 3.0.504,
khác Redis 8 trong container. Đã dùng container test riêng ở cổng 16389 và đặt
`TRUSTCABINET_TEST_REDIS=127.0.0.1:16389`; cả 25 test Redis và lượt full suite sau đều PASS.
Không đổi assertion, không skip/tắt test để xử lý lỗi môi trường này.

Để tái chạy với Redis test riêng (từ root repository, PostgreSQL test đã sẵn sàng):

```powershell
docker run -d --name scrum69-redis-test -p 16389:6379 redis:8-alpine
$env:TRUSTCABINET_TEST_REDIS = '127.0.0.1:16389'
dotnet build TrustCabinet.sln --configuration Release -warnaserror
dotnet test TrustCabinet.sln --configuration Release --no-build --collect:"XPlat Code Coverage"
docker stop scrum69-redis-test
docker rm scrum69-redis-test
```

## Coverage và bằng chứng

Coverage Cobertura của lượt full suite cuối:

- `MeetingLifecycleService.cs`: **85/86 dòng, 98,84%**, vượt mức 70%; dòng chưa đi qua là nhánh rethrow khi request hủy trước commit.
- `InvalidAgendaExecutionException`: 100%.
- Các file tích hợp (tính toàn file, gồm code cũ): NotificationService 98,73%; CallRoomService 94,56%; MeetingRepository 97,16%; MeetingEndpoints 93,77%.

Artifact local (gitignored, không commit kết quả máy):

- `tests/TrustCabinet.Web.Tests/TestResults/scrum69-review-full-isolated.trx`.
- `tests/TrustCabinet.Web.Tests/TestResults/scrum69-lifecycle.trx`.
- `tests/TrustCabinet.Web.Tests/TestResults/29ab33f7-f80f-468c-95e4-e64d8f7573a6/coverage.cobertura.xml`.

Source test: `MeetingLifecycleTests.cs`, `MeetingLifecycleConcurrencyTests.cs`,
`MeetingLifecycleRealtimeTests.cs`, `MeetingLifecyclePerformanceTests.cs`, fixture cùng thư mục
`tests/TrustCabinet.Web.Tests/Meetings/`. Smoke thủ công có runner riêng, không phải một test CI
âm thầm skip khi thiếu LiveKit.

## Phạm vi đã chứng minh

- Start/End theo cả ba mode; timestamp/history/recipient/target/deduplication key đúng; gọi lặp không thêm history/notification/side effect.
- Chỉ active current Chair; không cần MeetingCreator. Secretary/Member/Organizer bị từ chối; missing/inaccessible cùng 404 body rỗng. Auth, inactive-user gate, lỗi gate, parser strict, property giả, Content-Type và giới hạn body giữ contract.
- Start yêu cầu active current Secretary nhưng không yêu cầu Accepted/Present; End vẫn được khi Secretary inactive/removed/delegated. Guest/Removed/Delegated/inactive không nhận Start notification; Declined vẫn nhận.
- End có/không có current item; current thành Done, Pending/Done khác giữ nguyên. Vote Open, receipt/choice, response/attendance không thay đổi; interceptor xác nhận End không query bảng voting.
- Lỗi save và lỗi sau SaveChanges/trước commit rollback Meeting/Agenda/History; không event/room/notification sau rollback. Timestamp agenda nằm sau mốc End trả MSG116 và rollback.
- Notification save lỗi sau commit không rollback Start; request Start lặp không tự replay notification. Lỗi publisher không ngăn đóng room; lỗi provider/bookkeeping vẫn MSG111 và lượt reconciliation tiếp theo khôi phục. Có cả room không có active CallSession.
- Hai request và burst 20 request Start/End cùng Meeting: một winner; không duplicate mutation/effect. Dùng transaction/khóa và TaskCompletionSource quan sát request tới khóa, không sleep ngẫu nhiên để quyết định thứ tự.
- Request chờ khóa đọc lại Chair sau handover, đọc lại account sau deactivation, giữ lịch mới sau reschedule; End cạnh tranh với agenda Start/Complete không để sót InProgress item.
- Hủy client sau commit không hủy notification/agenda/room effect. SignalR transport thật nhận MeetingEnded và GET snapshot Ended; inactive/removed/khác Meeting không nhận. Reconnect khôi phục bằng GET.
- Full regression gồm Meetings, Agenda, Participants, Calls, Notifications, auth, message catalogue và các nhóm còn lại trong solution.

Test account deactivation đã phát hiện snapshot User cũ từ active-user gate trong ChangeTracker.
Repository hiện refresh các User chưa sửa từ kết quả đã khóa FOR SHARE trước khi query roster;
test tái hiện lỗi đã PASS cùng regression workflow cũ.

## Hiệu năng và LiveKit thật

100 caller đồng thời trên 100 Meeting độc lập, pool PostgreSQL của fixture giới hạn 20,
API chạy trong ASP.NET TestServer, LiveKit thật ở Docker local; không có tải media:

| Action | P50 | P95 | Max |
|---|---:|---:|---:|
| Start | 551,4 ms | **584,4 ms** | 588,3 ms |
| End | 451,0 ms | **476,7 ms** | 497,4 ms |

Lượt local này đáp ứng mục tiêu API P95 ≤ 1 giây và nằm dưới 3 giây; đây là thời gian request,
không phải đo render form FE hay chứng nhận môi trường triển khai. Đo với fake provider khỏe trong
nhóm test riêng: Start P95 564,0 ms, End P95 462,8 ms. Fake provider báo lỗi ngay: End 49,5 ms;
không coi đây là số đo network timeout. Mỗi effect sau commit có budget 2 giây; hai effect End
chậm liên tiếp có thể vượt mục tiêu 3 giây khi hạ tầng lỗi.

Smoke thật với hai client không mở microphone/camera:

1. Start, cấp ticket và cả hai client join được.
2. End trả MSG111; cả hai client ngắt sau **465,7 ms** tính từ lúc gửi End.
3. Cấp join ticket mới sau End bị từ chối.
4. Token cấp trước End **vẫn được LiveKit chấp nhận** để tái vào/tạo lại room.
5. Một lượt reconciliation trực tiếp ngắt client dùng token cũ sau **122,7 ms**.

Production janitor có chu kỳ 30 giây, cộng thời gian xử lý/provider/retry; smoke đo một lượt
reconciliation trực tiếp, **không đo và không cam kết SLA thu hồi tối đa 30 giây**. Khi provider
không sẵn sàng, Meeting vẫn Ended nhưng client có thể còn kết nối lâu hơn. JWT cũ không bị vô hiệu
hóa tức thời. Không bật recording/transcript hoặc lưu media trong thử nghiệm.

## Thay đổi và bàn giao

- Domain: enum Start/End/MeetingStarted, port IMeetingLifecycleService, notification/room methods,
  exception riêng cho timestamp agenda không hợp lệ.
- Infrastructure: lifecycle transaction, notification sau Start, đóng room sau End và refresh User
  snapshot sau khóa; service workflow cũ từ chối trực tiếp Start/End.
- Web: dispatch hai action trên endpoint transitions hiện có, MSG110/111/112/114/115/116 và HTTP mapping.
- Tài liệu: contract Meetings/Agenda/Notifications/Calls, catalogue MSG (next vẫn MSG287),
  [bàn giao FE/Tester](10-scrum69-ban-giao.md), collection Postman và hướng dẫn smoke.

Backend sẵn sàng bàn giao với các giới hạn đã chốt: notification creation best effort không có
outbox/replay mới; room cleanup eventual qua reconciliation. Chưa đánh dấu toàn bộ SCRUM-69 Done:
còn Frontend tích hợp → BA Final Review → Tester theo workflow. Không kiểm thử UI mới, tải media
50 người hoặc pentest toàn hệ thống trong phạm vi task Backend này.

## Bổ sung sau review P2 — bất biến thời gian CallSession

Đã xác nhận finding: join bằng ticket cũ sau khi Meeting End commit có thể tạo session tại t0+1s,
trong khi code cũ đóng session bằng t0. Đã bỏ tham số endedAt khỏi CloseMeetingRoomAsync;
CallRoomService dùng TimeProvider để lấy mốc ngay sau DeleteRoom thành công, kể cả nhánh janitor
đóng room. CallRepository chặn effectiveEnd trước StartedAt hoặc các JoinedAt/LeftAt đã lưu,
đóng các stint mở bằng cùng mốc hiệu lực và giữ nguyên stint đã đóng. Meeting/history vẫn giữ t0.

Thêm 5 regression case trong `MeetingLifecycleCallClosureTests.cs`:

- HTTP End + PostgreSQL thật, gateway chèn webhook participant_joined giữa commit và DeleteRoom hoàn tất,
  cho cả trường hợp chưa có session và đã có session. t0=08:00:00, join=08:00:01, provider xác nhận=08:00:02.
  Assert Meeting/history=t0; session.EndedAt và stint.LeftAt=08:00:02; đọc lại bằng DbContext mới,
  reconciliation sau đó không thay timestamp.
- Ba case repository: timestamp bắt đầu, join hoặc leave đã lưu nằm sau thời điểm đóng đề nghị;
  EndedAt không nhỏ hơn bằng chứng đó, stint không có duration âm/LeftAt vượt session.EndedAt;
  gọi EndSession lặp không ghi đè mốc kết thúc đã lưu.

Nhóm lifecycle/calls: **95 passed, 0 failed, 0 skipped**. Release build `-warnaserror`: PASS,
0 warning/error. Không thay schema/Frontend.

Smoke LiveKit thật chạy lại sau fix: PASS; hai client không media ngắt sau **604,8 ms**,
ticket mới bị từ chối; token cũ vẫn tái vào được và một lượt reconciliation ngắt lại **144,7 ms**.
100 caller/100 Meeting độc lập, LiveKit local: P95 Start **603,5 ms**, End **650,1 ms**.
Các giới hạn eventual cleanup/JWT cũ và việc chưa chạy Newman vẫn như phần trên.

Regression cuối sau sửa P2: **1.252 passed, 0 failed, 0 skipped**, khoảng 2m34s,
Redis 8 riêng `127.0.0.1:16389`. Artifact `scrum69-review-full-isolated.trx` và coverage mới được
liệt kê ở trên. Lượt trước chạy song song với smoke/load LiveKit có 1/1.252 ca fail:
`Concurrent_lifecycle_requests_have_exactly_one_winner(Start,20)` thấy 0 notification thay vì 3,
trong khi transition/history đúng. TRX không có log đủ để kết luận nguồn lỗi side effect.
Ca này PASS trong suite chạy độc lập; không sửa assertion, không bỏ test hoặc đổi budget notification.
Lưu bằng chứng lượt đó ở `TestResults/scrum69-review-full.trx` để theo dõi nếu tái xuất hiện.

## Tích hợp main mới sau review P1 — 23/10/2026

Tích hợp `origin/main` tại `e75bb65` (bao gồm `da5caf6`, Documents/R2) vào nhánh
SCRUM-69 từ `d9138df`, dùng merge chưa commit. Bỏ seed Document/DocumentVersions
và `Scenario.DocumentId` không được lifecycle test sử dụng; không khôi phục model cũ.
Resolve catalogue bằng cách giữ cả contract SCRUM-69 và SCRUM-107, mã tiếp theo `MSG290`.
Viết lại comment post-commit để mô tả đúng cách xử lý lỗi side effect.

Kết quả trên bản tích hợp này:

- `dotnet build TrustCabinet.sln --configuration Release -warnaserror`: PASS, 0 warning/error.
- `dotnet test TrustCabinet.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --logger "trx;LogFileName=scrum69-main-integration.trx"`:
  **1.432 passed, 0 failed, 0 skipped**, 3m18s; PostgreSQL thật và Redis 8 riêng,
  `TRUSTCABINET_TEST_REDIS=127.0.0.1:16389`. Không chạy smoke/load song song.
- `npm --prefix frontend run lint` và `npm --prefix frontend run build`: PASS.
  Vite vẫn cảnh báo chunk lớn hơn 500 kB.
- `git diff --check` và `git diff --cached --check`: PASS; không còn unmerged path.

Artifact nằm trong `tests/TrustCabinet.Web.Tests/TestResults/`: `scrum69-main-integration.trx`
và `76ae486e-e1f1-4d52-a123-e950307608e3/coverage.cobertura.xml`.
Không thêm migration hoặc sửa Frontend ngoài nội dung nhập từ main. Không chạy lại LiveKit
smoke/Newman trong lượt sửa fixture này; kết quả LiveKit trước đó không phải lượt kiểm chứng mới.
Chưa commit/push và chưa merge vào main; toàn UC vẫn chờ FE → BA Final Review → Tester.
