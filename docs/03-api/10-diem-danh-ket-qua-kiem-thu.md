# Kiểm thử Backend SCRUM-26

Ngày 20/10/2026; branch `feat/SCRUM-26-BACKEND-Taking-attendance-of-participants`.
[Contract](10-diem-danh.md), [bàn giao](10-diem-danh-ban-giao.md).

## Resolve merge main và review — 23/10/2026

Merge main `82a8af2` vào branch đã tích hợp SCRUM-97 (`629f483`). Xử lý trùng mã: MSG287 giữ nghĩa thông báo cập nhật quyền đã vào main; lỗi đọc attendance chuyển sang MSG289, mã tiếp theo MSG290. ADR điểm danh đổi số từ 0022 sang 0024 để giữ ADR-0022/0023 của main; cập nhật link, không đổi quyết định kỹ thuật. Các mã trong ghi nhận ngày 22/10 bên dưới là lịch sử trước lần remap này.

- Release build `-warnaserror`: pass, 0 warning/error. Frontend lint/build pass; còn cảnh báo bundle phòng họp trên 500 kB.
- Regression: 1.312 test; lượt đầu 1.287 pass, 25 lỗi kết nối Redis cổng 6379. Chạy lại toàn bộ 25 test Redis với container riêng cổng 6389: 25/25 pass; đối chiếu TRX xác nhận không còn test lỗi hoặc skipped.
- EF model check và Git whitespace check pass; không còn unmerged entries/conflict marker.
- Review phần tích hợp SCRUM-26/SCRUM-97: chưa phát hiện lỗi cần yêu cầu sửa còn lại về phân quyền, transaction/version, realtime hoặc migration. Guest production/UI điểm danh end-to-end chưa được kiểm chứng trong lượt này.
- Chưa tạo merge commit, push hoặc deploy.

## Đồng bộ SCRUM-97 — 22/10/2026

Tích hợp branch SCRUM-97 tại `48268ede97ede308bf6313a95059ffa788f857b5` vào SCRUM-26 sau merge main `fa408bc`. Giữ MSG287/MSG278 của attendance, bổ sung MSG258/MSG260 của UC-73, mã tiếp theo MSG288; giữ cả hai collection trong README Postman. Không thêm hoặc sửa migration.

- Release build `-warnaserror`: 0 warning/error; frontend lint/build pass (cảnh báo bundle phòng họp trên 500 kB còn nguyên); EF model check pass.
- Regression toàn solution: 1.305 test, lượt đầu 1.287 pass và 18 fail do Redis test đang tắt. Khởi động Redis tại cổng 6389, chạy lại toàn bộ 25 test Redis: 25/25 pass; xác nhận cả 18 test lỗi ban đầu đều pass trong lượt chạy lại. Không có test skipped hoặc lỗi còn lại.
- 60 test Attendance pass, gồm test mới chạy đồng thời POST chair-handover và POST attendance: cả hai thành công, role/attendance/recorder/version được giữ, Secretary tiếp tục lưu được với token mới.
- Không còn unmerged entries; `git diff --cached --check` pass. Kết quả được stage, chưa tạo merge commit hoặc push.

## Kiểm tra sau resolve merge — 22/10/2026

Tích hợp main `f6db57f` vào branch SCRUM-26: giữ migration SCRUM-20 trên main, bỏ grants đã được loại khỏi roster, dùng MeetingGuestSession chung; lỗi đọc attendance chuyển từ MSG277 sang MSG287 để tránh trùng validation tạo meeting. Quyền ghi vẫn Secretary/InProgress.

- Release build `-warnaserror`: pass, 0 warning/error.
- Regression: **1.239/1.239 pass**, 0 failed/skipped, 2 phút 51 giây; gồm 59 test Attendance. Bổ sung xác nhận owner/member nhận event không cần grant; giữ các ca mất quyền, Guest session và concurrency.
- PostgreSQL dùng database riêng của từng fixture; Redis test tại `127.0.0.1:6389`. Không migrate database ứng dụng/server chung.
- EF `has-pending-model-changes`: không có thay đổi chưa được migration phản ánh.
- `npm ci`, frontend lint/build: pass; build còn cảnh báo bundle phòng họp trên 500 kB.
- Git: không còn unmerged entries/conflict marker; `git diff --cached --check` pass. Chưa commit/push/deploy.

Các kết quả ngày 20/10 dưới đây là lịch sử trước merge; chính sách grants và ẩn invitationResponse khi đó đã được thay bằng quyền roster hiện tại trên main.

## Môi trường và dependency

- Windows, .NET SDK 10.0.402; ASP.NET Core/EF Core 10, PostgreSQL thật qua test harness tạo database tên riêng cho từng fixture rồi dọn sau test.
- SCRUM-20 squash local từ `adca53ba29c0b4c670cd01984b675d17018c8f4c`, giữ nguyên migration dependency; không tạo commit. HEAD vẫn `e133eb770c64424c733deb4f492713fda252e5d4`.
- Baseline đầu: 1.068 pass, 25 fail do Redis. Chẩn đoán cổng localhost:6379 trả Redis 3.0.504 dù container Compose Redis 8 đã chạy. Không sửa/stop Redis có sẵn; dùng container Redis 8 dành riêng test tại `127.0.0.1:16379` với `TRUSTCABINET_TEST_REDIS` ở process chạy test.
- Không áp dụng migration vào database ứng dụng/server chung. Không dùng EF InMemory làm bằng chứng cho khóa/transaction/migration.

## Kết quả

| Kiểm tra | Kết quả |
|---|---|
| Release build `-warnaserror` | Pass, 0 warning, 0 error |
| Regression trước bổ sung các ca biên cuối | 1.145/1.145 pass, 0 skipped, khoảng 2 phút 42 giây |
| Regression cuối | **1.152/1.152 pass**, 0 failed, 0 skipped, 2 phút 34 giây; gồm 59 ca mới trong namespace Attendance |
| EF `has-pending-model-changes` | Không có model change chưa được migration phản ánh |
| SQL migration SCRUM-26 | Đúng hai ALTER TABLE ADD attendance_version bigint NOT NULL DEFAULT 0; ngoài ra chỉ ghi EF migration history |
| Frontend lint | Pass |
| Frontend build | Pass; không chỉnh source FE |
| `git diff --check`, `git diff --cached --check` | Pass |
| Postman collection | JSON parse hợp lệ; chưa chạy collection trên app thật |

Những log lỗi được test cố ý tạo (read/save/commit/transport failure) không phải test thất bại. TRX và output build/test là artifact local được gitignore; báo cáo này giữ kết quả có thể review mà không commit log hoặc credentials.

## Phạm vi test

- API: internal/Guest/batch hỗn hợp, partial update, current membership, inactive target, Unknown và legacy, recorder/time, audit, no-op, ABA cùng fake timestamp, token binding, format/duplicate/unknown JSON, body limit kể cả chunked, query, UserId thay assignment ID, target khác meeting, quyền server và ẩn invitation response.
- Transaction PostgreSQL: hai connection, chờ lock thật qua pg_stat_activity; cùng entry một winner, khác entry đều thành công, batch stale atomic, revalidate Secretary/active/Ended/removed/revoked, unrelated addition/response/role không tạo stale, đọc target sau khi chờ row lock, hai batch đảo thứ tự không deadlock, overflow rollback.
- Persistence: lỗi SaveChanges và deferred constraint trigger trong database test cố ý làm COMMIT thất bại sau UPDATE/INSERT; xác nhận attendance/version/audit rollback đồng thời. Trigger chỉ tồn tại trong fixture DB riêng và được dọn, không thuộc migration ứng dụng.
- Migration: nâng cấp dữ liệu trước migration, legacy/recorder/time/response không đổi, default 0, operation đúng hai AddColumn, không global concurrency token, Down/Up chỉ trên DB test dùng một lần, snapshot khớp.
- Realtime: SignalR client thật qua TestServer LongPolling; chỉ joined roster readers nhận event, no-op/rollback không phát, GET sau event thấy dữ liệu commit, reconnect, mất grant/active/current membership ngừng nhận, Guest session revoke/expire/unverified và invitation revoke, phân tách hai session. Adapter production mặc định deny.
- Regression toàn solution: Participants, Agenda, Meetings, Auth/Access, Notifications/Email, catalogue và database model.

## Hiệu năng

Đo GET `/attendance?pageSize=100`, roster 103 người, 100 tài khoản khác nhau có quyền xem cùng gọi đồng thời, connection pool PostgreSQL = 20. Một GET có 6 reader commands, không tăng tuyến tính theo số người. Transport TestServer trong cùng máy, không có mạng WAN/TLS/proxy.

| Vòng | p50 | p95 | Max | Lỗi |
|---|---|---|---|---|
| 1 | 467 ms | 578 ms | 601 ms | 0 |
| 2 | 236 ms | 353 ms | 355 ms | 0 |
| 3 | 177 ms | 296 ms | 313 ms | 0 |

Ba vòng của lượt regression cuối đạt mục tiêu p95 GET dưới 1 giây trong điều kiện đo này. Đây không phải chứng nhận tải production hoặc độ trễ room/browser. POST được kiểm chức năng/atomic/concurrency; chưa benchmark 100 request ghi đồng thời. Test không dùng assertion thời gian phụ thuộc phần cứng để làm CI chập chờn.

## Lệnh tái lập

Khởi động PostgreSQL test theo database guide và Redis tương thích. Nếu máy có Redis cũ ở 6379, dùng Redis test cổng riêng; không ghi connection string/password vào repo.

```powershell
docker run --rm -d --name trustcabinet-scrum26-redis -p 127.0.0.1:16379:6379 redis:8-alpine
$env:TRUSTCABINET_TEST_REDIS = '127.0.0.1:16379'
dotnet build TrustCabinet.sln --configuration Release -warnaserror
dotnet test TrustCabinet.sln --configuration Release --no-build --verbosity minimal --logger "trx;LogFileName=scrum26-final.trx"
dotnet dotnet-ef migrations has-pending-model-changes --project backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web
npm --prefix frontend run lint
npm --prefix frontend run build
git diff --check
git diff --cached --check
```

Container Redis dành riêng lượt kiểm thử đã được dừng/dọn sau khi hoàn tất. Khi chạy lại, chỉ dừng container test do mình tạo; không dừng Redis dùng chung.

## Giới hạn bàn giao

Guest tests dùng adapter giả, không chứng minh OTP/session/profile production SCRUM-59. Không làm FE/AttendancePanel 360px hoặc room SCRUM-41, không chạy browser E2E. Realtime best effort không có outbox; FE phải GET lại khi reconnect/focus và polling dự phòng. Không chạy Postman collection thủ công vì cần tài khoản/meeting InProgress của môi trường app riêng. Không commit, push, merge main hoặc deploy.
