# ADR-0015: Server test chung chạy Docker Compose trên một máy Lightsail, ở chế độ Development, tự deploy từ `main`

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-12
- **Người quyết định:** Hải Trần Việt, chờ nhóm SEP490-G116 duyệt qua pull request

## Bối cảnh

Tester và BA cần một bản chạy chung để test theo quy trình BE → FE → BA Final Review → Tester, thay
vì mỗi người tự cài .NET, Node, Docker và cấu hình secret trên máy mình. Nhóm có khoảng 130 USD credit
AWS và cần server sống đến khi bảo vệ xong. Đây là đồ án: vài người dùng cùng lúc, dữ liệu giả.

Ba ràng buộc từ code hiện tại:

- Ngoài `Development`, app không tự migrate (cố ý — xem `Program.cs`) và **không có tài khoản nào**:
  `DevelopmentDataSeeder` là nơi duy nhất tạo người dùng, và chưa có cơ chế tạo quản trị viên đầu
  tiên cho môi trường thật.
- Cookie refresh token đặt `Secure` và Google Sign-In không nhận origin `http`, nên bắt buộc HTTPS.
- Repo là private, nên server phải có quyền đọc repo.

## Quyết định

Chúng tôi chạy server test trên **một máy AWS Lightsail gói 1 GB** (Singapore), bằng
`deploy/docker-compose.yml` gồm bốn container: app (build từ `Dockerfile` ở gốc repo), PostgreSQL,
Redis và Caddy làm reverse proxy tự lấy chứng chỉ Let's Encrypt. Tên miền là bản ghi A
`trustcabinet.workspace-hub.space` trỏ về IP tĩnh của máy.

App chạy với **`ASPNETCORE_ENVIRONMENT=Development`**: tự migrate và seed tài khoản demo khi khởi
động. Server chỉ chạy nhánh `main`, chỉ chứa dữ liệu giả.

**Tự deploy:** `.github/workflows/deploy.yml` chạy sau mỗi lần CI trên `main` xanh, build image trên
runner GitHub, chép sang server qua SSH (`docker save | docker load`) rồi khởi động lại. Server dùng
một deploy key chỉ-đọc để lấy `docker-compose.yml` và `Caddyfile` đúng commit.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| EC2 `t4g.small` (2 GB) | Không bị giới hạn cỡ máy với tài khoản mới; đã chạy thử được | Khoảng 20 USD/tháng vì tính riêng ổ đĩa và IP; giao diện quản lý phức tạp | Đắt gần gấp ba, nhóm khó tự quản lý. Đã dựng thử rồi gỡ |
| Lightsail gói 2 GB ($12) | Build trên server thoải mái, dư cho gọi video | Tài khoản AWS mới bị Lightsail từ chối gói này, kể cả sau khi lên Paid plan | Không tạo được; nâng bằng snapshot khi AWS nới giới hạn |
| EC2/Lightsail + RDS + ElastiCache | Đúng kiểu hạ tầng production | RDS, ElastiCache mỗi thứ tốn ngang hoặc hơn cả máy | Không đáng cho một môi trường test |
| Build image trên server khi deploy | Không cần secret SSH trên GitHub | Máy 1 GB build mất 15–20 phút, đốt hết CPU burst, web chậm hẳn trong lúc build | Chậm và làm hỏng trải nghiệm test |
| Đẩy image lên GHCR rồi server kéo về | Cách làm phổ biến, có lịch sử image | Server cần thêm token đọc package; package private chiếm dung lượng miễn phí có hạn | Thêm một secret và một chỗ tốn dung lượng mà không được gì thêm ở quy mô này |
| Chạy ở `Staging`/`Production` | Không lộ trang lỗi chi tiết, không có mật khẩu công khai | Phải viết thêm cơ chế migrate có chủ đích và tạo quản trị viên đầu tiên | Chưa có trong phạm vi; để dành cho khi triển khai thật |

## Hệ quả

- Chi phí khoảng 7 USD/tháng, trừ vào credit. Budget theo tháng 20 USD, chỉ đếm *Usage*, cảnh báo qua
  email ở 80% thực tế và 100% dự đoán.
- Ai có URL cũng đăng nhập được bằng tài khoản quản trị demo, và lỗi trả về stack trace. Chấp nhận
  được vì dữ liệu là đồ giả; không bao giờ nhập dữ liệu thật vào đây.
- Merge vào `main` là lên server sau khoảng 10 phút. Migration áp dụng tự động, nên chỉ deploy
  `main`: một migration chưa merge lên server sẽ làm lệch schema.
- GitHub giữ một SSH key đăng nhập được server (secret `DEPLOY_SSH_KEY`). Lộ key này là lộ server;
  host key ghim trong secret `DEPLOY_HOST_KEY` để workflow không tin nhầm máy khác.
- Không có backup: mất máy thì dựng lại và seed lại.
- 1 GB RAM và CPU burstable đủ cho các màn hiện có; gọi video (LiveKit, ADR-0010) cần đo lại khi về
  `main`, có thể phải nâng gói bằng snapshot.
- Sau khi bảo vệ xong phải xoá máy và IP tĩnh rồi đóng tài khoản AWS — tài khoản đang ở Paid plan,
  hết credit là trừ tiền thẻ.
- Một bản triển khai thật về sau vẫn cần ADR riêng: migrate có chủ đích, tạo quản trị viên đầu tiên,
  backup.
