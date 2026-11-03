# ADR-0047: Server test chạy ở `Staging`; deploy migrate có chủ đích, backup database và tự quay lại khi health check đỏ

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-11-02
- **Người quyết định:** Hải Trần Việt

Thay thế phần "chạy ở `Development`, tự migrate khi khởi động, không backup" của
[ADR-0015](0015-server-test-tren-lightsail.md). Phần còn lại của ADR-0015 (Lightsail, Docker Compose, build
trên runner GitHub rồi chép sang qua SSH) giữ nguyên.

## Bối cảnh

ADR-0015 cho server test chạy `ASPNETCORE_ENVIRONMENT=Development` vì lúc đó chỉ Development mới tự migrate
và tạo tài khoản. Từ đó tới nay server đã có dữ liệu thật của nhóm (cuộc họp, bản ghi, giọng mẫu thử) và được
cả nhóm, giảng viên và hội đồng mở. Development kéo theo bốn vấn đề:

- **Lộ stack trace.** Development bật trang lỗi chi tiết: một exception chưa bắt trả về đường dẫn file, câu SQL
  và cấu hình cho bất kỳ ai có URL.
- **Migrate ngầm khi khởi động.** Container app mới tự áp dụng migration lúc boot. Migration lỗi thì app không lên,
  mà image cũ đã bị xoá ngay sau `up -d`, nên không quay lại được.
- **Không có backup.** Volume `trustcabinet-test_pgdata` là bản duy nhất của database.
- **Deploy có thể lùi.** CI của một commit cũ xong sau commit mới thì deploy của nó đưa server về code cũ:
  `concurrency` chỉ xếp hàng, không biết thứ tự. Nút *Run workflow* còn deploy được bất kỳ nhánh nào.

Database trên server đã migrate đủ và đã có các tài khoản demo, nên server không còn cần seed lúc khởi động.

## Quyết định

1. **Server test chạy `ASPNETCORE_ENVIRONMENT=Staging`.** Lỗi không trả stack trace. Thư tới
   `@trustcabinet.local` (tài khoản demo) vẫn bị chặn, nay qua `appsettings.Staging.json` thay cho
   `appsettings.Development.json`. Máy dev vẫn chạy Development như cũ.
2. **Migrate là một bước riêng của deploy.** `dotnet TrustCabinet.Web.dll --migrate` áp dụng mọi migration còn
   thiếu và dữ liệu tham chiếu (ma trận phân quyền, ADR-0007), rồi thoát với mã 0 (thành công) hoặc 1 (lỗi), không
   mở web server. Workflow chạy nó bằng `docker compose run --rm app --migrate`, **bằng image mới, trước khi** đổi
   container app. Migrate lỗi thì deploy dừng, container app vẫn chạy bản cũ.
   `--migrate --seed-demo` thêm cả dữ liệu demo; chỉ dùng khi dựng lại server test từ database rỗng.
3. **Backup trước mỗi lần migrate.** Khi image app đổi (chỉ khi đó mới có thể có migration mới), workflow
   `pg_dump -Fc` database vào `~/backups` trên server và giữ 14 bản mới nhất.
4. **Giữ image cũ tới khi health check xanh.** Bước *Kiểm tra* đỏ (hoặc migrate lỗi) thì bước *Quay lại* đưa tag
   `latest` về image container app chạy trước đó, `git` về commit trước, rồi `up -d`. Job vẫn đỏ. Image cũ chỉ bị
   xoá sau khi bản mới đã qua *Kiểm tra*.
5. **`/api/health` kiểm database.** Không tới được database thì trả `503`, để một bản lên được nhưng không phục vụ
   được gì cũng bị quay lại.
6. **Chỉ deploy `main`, không lùi.** Chọn nhánh khác ở *Run workflow* thì bước đầu từ chối. Commit cần deploy là tổ
   tiên của commit server đang chạy thì bỏ qua (xanh, kèm thông báo). Deploy lại đúng commit đang chạy vẫn được.

**Quay lại code không hoàn tác migration.** Migration đã áp dụng vẫn còn trong database khi image cũ chạy lại.
Migration của nhóm thường chỉ thêm (cột, bảng), nên code cũ vẫn chạy được trên schema mới. Không chạy được thì phải
khôi phục database từ bản backup ngay trước lần deploy đó, bằng tay, theo
[deploy/README.md](../../deploy/README.md#khôi-phục-database). Dữ liệu ghi vào giữa lúc backup và lúc khôi phục sẽ
mất. Workflow không tự khôi phục: ghi đè database là việc phải có người quyết.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ Development, chỉ tắt trang lỗi | Ít đổi nhất | Vẫn migrate ngầm lúc boot, vẫn chạy seeder demo mỗi lần khởi động | Chỉ chữa một trong bốn vấn đề |
| `Production` thay cho `Staging` | Đúng tên môi trường khi triển khai thật | Server vẫn có tài khoản demo mật khẩu công khai, dữ liệu thử | Tên `Staging` nói đúng đây là bản thử; bản triển khai thật sau này tự chọn `Production` |
| `dotnet ef database update` hoặc migration bundle (`efbundle`) | Cách Microsoft hướng dẫn | Image runtime không có SDK; bundle là thêm một artifact phải build, chép và giữ khớp phiên bản | Lệnh `--migrate` nằm sẵn trong image app, chạy đúng code đã build, không thêm gì phải chép |
| Bật `Database:SeedReferenceDataOnStartup` và migrate lúc boot ở Staging | Không đổi workflow | Lại là migrate ngầm, app hỏng thì không quay lại được | Đúng điều ADR này muốn bỏ |
| Backup bằng snapshot Lightsail | Chụp cả máy | Phải gọi API AWS từ GitHub (thêm khoá), chậm, tốn tiền theo dung lượng | `pg_dump` đủ cho một database vài chục MB, khôi phục được từng database |
| Tự khôi phục database khi health check đỏ | Không cần người | Ghi đè database dựa trên một lần health check có thể đỏ vì lý do khác (Caddy, LiveKit) | Mất dữ liệu nhóm đang test là cái giá quá đắt cho một quyết định tự động |

## Hệ quả

- Server không còn tự seed: dữ liệu demo hiện có vẫn ở đó. Xoá sạch database thì phải chạy
  `--migrate --seed-demo` (deploy/README.md), không thì không có tài khoản nào để đăng nhập.
- Seeder demo không còn sửa gì trên server mỗi lần khởi động (đổi hash mật khẩu cũ, thêm cuộc họp demo mới thêm vào
  seeder). Thêm dữ liệu demo mới cho server thì chạy `--migrate --seed-demo` bằng tay.
- `~/backups` trên server chứa dữ liệu cá nhân (dữ liệu thử của nhóm): thư mục `700`, chỉ user `ubuntu` đọc được, và
  bị xoá cùng máy sau khi bảo vệ xong.
- Một deploy có image app đổi chậm thêm vài giây cho `pg_dump` và container migrate.
- Lần deploy đầu sau khi merge: database đã migrate đủ nên `--migrate` không làm gì, tài khoản demo giữ nguyên, app
  khởi động lại ở Staging. Bản cũ (Development) nếu phải quay lại vẫn tự migrate như trước.
- Health check giờ phụ thuộc database: PostgreSQL chết thì `/api/health` trả `503` thay vì `200`.
