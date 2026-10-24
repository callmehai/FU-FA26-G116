# SCRUM-25 — kết quả triển khai và kiểm thử Backend

Ngày kiểm tra: 13/10/2026 (UTC+7). Branch `codex/scrum25-backend-agenda`, tách từ `origin/main` tại `ee27fb1`. Tài liệu participant ở `0de8254` chỉ được tham chiếu, không đưa commit participant vào branch.

Ghi chú 21/10/2026: tên identity/resolver trong báo cáo lịch sử bên dưới đã được hợp nhất thành `MeetingGuestSession` / `IMeetingGuestIdentityResolver` dùng chung Agenda và Participants; xem [contract hiện hành](08-agenda-contract-tich-hop-guest-uc04.md).

## Thay đổi thực tế

### Bổ sung điểm tích hợp Guest trong phạm vi SCRUM-25 (13/10/2026)

Theo phạm vi người dùng chốt, chỉ triển khai consumer phía Agenda và [contract bàn giao UC-04](08-agenda-contract-tich-hop-guest-uc04.md), không triển khai OTP, lời mời, session storage hoặc tạo User Guest. Không có migration mới.

- Thêm identity `AgendaGuestSession`, hai adapter `IAgendaGuestIdentityResolver` / `IAgendaGuestAccess` và implementation mặc định từ chối. UC-04 phải thay cả hai bằng resolver và policy thật.
- GET Agenda dùng đường đọc Guest riêng, đúng meeting và trạng thái như Member, trả metadata tài liệu sau kiểm quyền hiện tại; tất cả cờ mutation false. Options/mutation chặn Guest kể cả subject trùng User ID; quyền User nội bộ vẫn kiểm active và MeetingReadPolicy.
- Hub kiểm Guest lúc connect/join, group theo meeting/invitation/session; publisher query lại session còn quyền trước mỗi event. Session mất hiệu lực không nhận event nhờ session khác của cùng invitation. Adapter lỗi không fallback broadcast, dữ liệu đã commit vẫn thành công.
- Thêm **16 case AgendaGuestTests**, dùng JWT test + fake adapter UC-04 + PostgreSQL thật và SignalR LongPolling của TestServer. Bao phủ tài liệu/quyền chỉ đọc, chặn mutation, cách ly meeting/session, revoked/expired/unverified/missing, trạng thái meeting, default deny, lỗi adapter, reconnect và dữ liệu commit. Không tuyên bố đã test OTP/session production.
- Lượt full đầu: 1.043 pass, 1 fail ở test JWT expiry cũ do token 2 giây hết hạn trước lệnh join dưới tải song song. Sửa test dành 5 giây cho handshake, kiểm connection tự đóng khi hết hạn kể cả chưa join; các test join vẫn được giữ ở nhóm transport riêng.
- Lượt cuối restore/build Release `-warnaserror` và full suite trên Linux SDK 10.0.401, PostgreSQL 17/Redis 8 riêng: **1.044 pass, 0 fail, 0 skip**, build 0 warning/error. Kết quả tại `tests/TrustCabinet.Web.Tests/TestResults/guest-contract-final/guest-contract-final.trx`; log `%TEMP%/agenda-guest-contract-final.log`. Windows build Release cũng pass. Container/network tạm đã dọn; không chạy lại Frontend hoặc đo lại coverage trong lượt này.
- Contract có cách thay DI, ràng buộc identity/token/session, quy tắc error/cancellation và sáu bước test thủ công sau khi UC-04 tích hợp. Giữ nguyên index/staged merge có sẵn; thay đổi lần này chưa commit/push.

### Đối chiếu Jira và tài liệu mới merge (13/10/2026)

Đã đọc toàn bộ ba comment hiện có của SCRUM-25, đối chiếu code `c91f318` với cập nhật `main` tại `729c1e0` (PR #83). Phần mới merge chỉ thay đổi tài liệu; UC-26 đổi danh sách actor, không đổi quy tắc đọc. Helper End Meeting không có gate Vote/attendance nên phù hợp phần Agenda của ADR-0017. Chưa cần sửa runtime cho người dùng nội bộ; luồng Guest theo ADR-0016 còn phụ thuộc invitation identity/session/policy của UC-04/SCRUM-20 như ghi rõ trong contract. Không xác nhận Guest hoặc endpoint End Meeting đã hoạt động.

Chỉ cập nhật contract, dependency bàn giao và đoạn mô tả catalogue bị cũ sau merge; không đổi endpoint, DTO, schema hoặc Frontend. Chạy lại restore/build Release `-warnaserror` và full suite từ source hiện tại trên Linux SDK 10.0.401 + PostgreSQL 17/Redis 8 riêng: **1.028 pass, 0 fail, 0 skip**, build 0 warning/error. Kết quả tại `tests/TrustCabinet.Web.Tests/TestResults/merge-review/merge-review.trx`, log `%TEMP%/agenda-merge-review.log`. Không đo lại coverage hoặc chạy lại kiểm tra Frontend trong lượt này. Container/network tạm đã dọn.

Các điểm tích hợp chưa có trong ticket đã được bổ sung vào [comment 10155](https://sep490g116.atlassian.net/browse/SCRUM-25?focusedCommentId=10155). Giữ nguyên index/staged của merge đang dở; các chỉnh sửa tài liệu lần này chưa commit/push.

### Bổ sung sau review độc lập (13/10/2026)

Trên branch hiện tại `feat/scrum25-backend-agenda`, sửa hai finding P2:

- Publisher được await trực tiếp sau commit, vẫn nhận cancellation token 2 giây. Request chờ task kết thúc trước khi dispose scoped dependencies; lỗi muộn được catch/log, mutation đã commit vẫn thành công. Mốc 2 giây không phải hard timeout của response.
- Lỗi giải mã surrogate trong tên thuộc tính JSON được chuyển thành lỗi parse ngay tại bước đọc tên, không catch rộng quanh nghiệp vụ. Create/update trả `400/MSG66`, reorder trả `400/MSG143`, không lưu hoặc phát event.
- Thêm bốn case regression: publisher hoàn tất thành công/lỗi sau cancellation; high/low surrogate lỗi trên cả create, update, reorder. Test publisher giữ task bằng gate và kiểm tra mutation chưa trả về, dữ liệu đã commit, scoped DbContext vẫn dùng được khi task tiếp tục.
- Chạy lại restore, build Release `-warnaserror` và full suite từ source trên Linux SDK 10.0.401, PostgreSQL 17/Redis 8 riêng: **1.028 pass, 0 fail, 0 skip**, gồm 76 case Agenda. Build không warning/error; `git diff --check` pass. Container/network kiểm thử tạm đã dọn.
- Kết quả: `tests/TrustCabinet.Web.Tests/TestResults/review-fixes/review-fixes.trx`; log local `%TEMP%/agenda-review-fixes.log`. Không đo lại coverage; tỷ lệ và kiểm tra Frontend bên dưới thuộc lần nghiệm thu ban đầu. Lần sửa này không thay đổi Frontend hoặc schema.

Các kết quả và mô tả sau đây ghi lại lần triển khai ban đầu:

- Tám endpoint cho bảy UC: đọc snapshot/options, tạo, sửa, xóa, reorder, start và complete. Xem [contract và ví dụ JSON](08-chuong-trinh-nghi-su.md), [bảng MSG](00-danh-muc-ma-thong-bao.md#agenda--contract-backend-scrum-25).
- Domain chứa hợp đồng, rule và execution; Infrastructure chứa service/repository dùng chung scoped DbContext; Web chịu trách nhiệm parse JSON, mapping HTTP và hub. Không thêm framework hoặc Application project.
- Mutation dùng transaction ReadCommitted + PostgreSQL meeting-row lock, kiểm lại assignment/status sau lock. Reorder token chống snapshot cũ; giữ gap và vị trí item đã bắt đầu. Document association không xóa metadata/version hoặc chuyển tài liệu từ mục khác.
- Hub `/hubs/meetings` phát `agendaChanged` sau commit, lọc lại active reader theo policy chung ở mỗi lần phát. No-op/rollback không phát. Push lỗi không đổi response lưu thành thất bại.
- Helper `IAgendaMeetingEnd` chuẩn bị complete current trong transaction caller; không tự SaveChanges/Commit/publish. Có test commit, rollback và caller save lỗi; chưa có endpoint End Meeting.
- Migration mới: `20261012153015_AgendaExecutionInvariants` và ModelSnapshot. Bỏ Postponed khỏi enum, thêm check ba trạng thái và partial unique index current/meeting. Không sửa migration cũ; không thêm duration/session/MeetingDocument.
- Fixture Development insert-only cho ba meeting planning/InProgress/Ended; [Postman](postman/SCRUM-25-Agenda.postman_collection.json) và [client SignalR](postman/agenda-realtime.mjs). Không thay đổi source Frontend.

## Kiểm tra đã thực hiện

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore TrustCabinet.sln` | Pass |
| `dotnet build TrustCabinet.sln --no-restore --configuration Release -warnaserror` | Pass, 0 warning / 0 error |
| Agenda + các regression có tên Agenda trên Windows | 75 pass, 0 fail, 0 skip; sau đó thêm 1 test SQL trigger lỗi liên kết nhiều tài liệu và test này cũng pass |
| Toàn bộ regression cuối, restore/build từ source trên Linux SDK 10.0.401 + PostgreSQL 17/Redis 8 riêng | **1.024 pass, 0 fail, 0 skip**; gồm **72 test Agenda mới** và 952 test có sẵn |
| Coverage code Agenda mới | 418/423 dòng = **98,82%**, vượt mục tiêu 70% |
| Query count snapshot | **6 query** ở cả 1 item và 50 item/200 document liên kết |
| `npm ci`, `npm run lint`, `npm run build` | Pass; không đổi package-lock hoặc source FE |
| JSON Postman, cú pháp client Node, `git diff --check` | Pass |

Nhóm test mới bao gồm unit rule/time/hash, HTTP/service/PostgreSQL, security, concurrency, migration, SignalR transport LongPolling thật và performance. Tất cả PostgreSQL test dùng database riêng có GUID; không skip khi thiếu database và không clear pool của test khác.

| Class test mới | Số case pass |
|---|---:|
| AgendaRulesTests | 7 |
| AgendaEndpointTests | 32 |
| AgendaConcurrencyTests | 12 |
| AgendaMigrationTests | 4 |
| AgendaHubTests | 6 |
| AgendaAdditionalTests | 9 |
| AgendaPerformanceTests | 2 |

Artifact local: `tests/TrustCabinet.Web.Tests/TestResults/linux/agenda-isolated-release.trx` (full suite cuối), `agenda-verified.trx` (coverage run Windows), `agenda-document-atomicity.trx` (SQL trigger từ chối association sau insert), và `68d02f4f-90ae-44cb-9c87-b2151a71fc09/coverage.cobertura.xml`. TestResults là output gitignored; các số liệu cần review được ghi ở báo cáo này. Container PostgreSQL/Redis tạm đã được dọn sau khi test hoàn tất.

Các race được kiểm tra: create/create, start cùng/khác item, complete/complete, reorder/reorder, reorder đang chờ start/delete/create, update đang chờ start/Ended/thu hồi role/removed. Test chờ bằng quan sát PostgreSQL lock trên connection riêng với timeout, không suy đoán bằng delay dài. Helper End Meeting có test rollback của cả meeting, agenda và history khi caller lưu lỗi.

HTTP kiểm status/MSG, field errors, JSON sai kiểu/cấu trúc/trường hệ thống, UUID, body quá lớn kể cả không Content-Length, JWT thiếu/hết hạn, inactive, role mới với JWT cũ, document không thuộc meeting, no-op và metadata bị hạn chế. Catalogue scanner không bị sửa hoặc bỏ qua mã Agenda.

SignalR kiểm join/leave/reconnect, outsider/meeting khác/chưa join, trạng thái đã commit khi nhận event, no-op/rollback không event, subscriber inactive/removed/delegated không nhận event tiếp, leave không ngắt connection khác của cùng user, token hết hạn đóng connection và query token không dùng được trên REST.

## Performance local

Máy: Windows 11 build 26200, AMD Ryzen 7 7840HS, 16 logical processors, tổng RAM hệ thống báo khoảng 27,7 GiB; .NET SDK 10.0.401 / runtime 10.0.12; PostgreSQL **18.6 native** trên localhost:5432 (đã kiểm tra listener và `SHOW server_version`, không phải port forwarding Docker). Pool riêng fixture tối đa 20 connection để không chiếm toàn bộ connection server.

Dataset: 50 agenda item, 100 tài liệu liên kết, 100 user/participant riêng. Sau warm-up, 100 HTTP client chạy song song trong 3 vòng (300 GET); 100 SignalR client join meeting rồi nhận một mutation và mỗi client GET snapshot mới (100 số đo event + GET). Collection performance chạy riêng để không cạnh tranh với các class test khác.

| Chỉ số | Kết quả đo Windows, 00:10:38 ngày 13/10 | Mục tiêu |
|---|---:|---:|
| GET snapshot p95 | **383,53 ms** | ≤ 1.000 ms |
| Từ bắt đầu mutation → viewer nhận event và GET thấy item, p95 | **514,96 ms** | ≤ 2.000 ms |

Lượt full suite cuối trên Linux/WSL2 kernel 6.18.33.2, PostgreSQL 17/Redis 8 trong network Docker riêng (03:13:11 ngày 13/10, cùng dataset và 16 logical processors) đo GET p95 **327,21 ms**, event + GET p95 **441,42 ms**. Cả hai lượt đều đạt mục tiêu trong điều kiện local đã nêu.

Đây là phép đo TestServer + LongPolling + PostgreSQL localhost, không gồm mạng WAN, TLS/reverse proxy, WebSocket production hoặc UI render. 100 latency realtime đến từ cùng một lần broadcast, không phải 100 mutation độc lập. Không tuyên bố chứng nhận NFR trên môi trường triển khai hoặc p95 của mọi endpoint từ phép đo này.

## Hạ tầng và giới hạn kiểm tra

- Lượt full suite Windows đầu tiên gặp Redis native chiếm localhost:6379 và test tải chạm giới hạn connection PostgreSQL. Fixture tải đã được giới hạn pool; PostgreSQL/Redis native được giữ nguyên. Lượt full regression đầu tiên pass trong namespace Redis Docker bằng SDK Linux, dùng PostgreSQL native qua host.docker.internal và database riêng. Lượt build source kế tiếp có 2 lỗi kết nối IPv6, rồi khi dùng IP container PostgreSQL cũ thì gặp credential của volume cũ không khớp biến môi trường container. Vì vậy lượt cuối dùng PostgreSQL 17 và Redis 8 tạm riêng, không volume/cổng host, và dọn sau test.
- Khi tái sử dụng binary Windows trên Linux, MvcTestingAppManifest phải trỏ content root Linux. **Lượt cuối đã restore/build trực tiếp từ source trong container**, chạy đúng `dotnet test TrustCabinet.sln --no-build --configuration Release --verbosity normal`, nên không phụ thuộc manifest Windows. Không sửa scanner, tắt test hoặc dùng skip để đạt kết quả.
- `npm audit` báo một dependency gián tiếp baseline mức high: source-map-js <1.2.2, GHSA-68fv-2mgg-jv7q. Không sửa dependency FE ngoài phạm vi task.
- Chưa thực hiện UI integration hoặc dùng Postman thao tác vào database Development của người dùng. Postman và client Node là artifact bàn giao để tester chạy theo [kịch bản thủ công](08-chuong-trinh-nghi-su-ban-giao.md#kiểm-thử-thủ-công); endpoint behavior đã được automated test trên database riêng.

## Việc còn thuộc các task khác

Frontend viết type/wrapper/AgendaPanel và tích hợp detail/room. SCRUM-20 cập nhật policy khi có Current/Removed; writer participant/document/lifecycle tuân thủ cùng meeting lock. Task tài liệu chuyển adapter sang MeetingDocument có chủ đích. SCRUM-69 tích hợp helper trong transaction End Meeting, phát event sau commit và đóng room. Chưa kiểm thử xuyên luồng các endpoint chưa tồn tại này.

Migration sẽ dừng nếu có status legacy ngoài ba trạng thái hoặc nhiều current; cần giải quyết dữ liệu bằng quyết định/migration có review. Realtime không có outbox bền vững: FE phải resync khi reconnect, active tab và định kỳ 30 giây. Xem [ghi chú Jira/BA và bàn giao đầy đủ](08-chuong-trinh-nghi-su-ban-giao.md); chưa đăng comment Jira và chưa đánh dấu task Done.
