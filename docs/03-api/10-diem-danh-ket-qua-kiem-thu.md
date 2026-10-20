# Kiểm thử Backend SCRUM-26

Ngày 20/10/2026; branch `feat/SCRUM-26-BACKEND-Taking-attendance-of-participants`.
[Contract](10-diem-danh.md), [bàn giao](10-diem-danh-ban-giao.md).

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
