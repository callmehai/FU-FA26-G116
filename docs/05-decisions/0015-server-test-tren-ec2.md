# ADR-0015: Server test chung chạy Docker Compose trên một máy EC2, ở chế độ Development

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-12
- **Người quyết định:** Hải Trần Việt, chờ nhóm SEP490-G116 duyệt qua pull request

## Bối cảnh

Tester và BA cần một bản chạy chung để test theo quy trình BE → FE → BA Final Review → Tester, thay
vì mỗi người tự cài .NET, Node, Docker và cấu hình secret trên máy mình. Nhóm có 120 USD credit AWS
và cần server sống đến hết tháng 12/2026.

Hai ràng buộc từ code hiện tại:

- Ngoài `Development`, app không tự migrate (cố ý — xem `Program.cs`) và **không có tài khoản nào**:
  `DevelopmentDataSeeder` là nơi duy nhất tạo người dùng, và chưa có cơ chế tạo quản trị viên đầu
  tiên cho môi trường thật.
- Cookie refresh token đặt `Secure` và Google Sign-In không nhận origin `http`, nên bắt buộc HTTPS.

## Quyết định

Chúng tôi chạy server test trên **một máy EC2 `t4g.small`** (ARM, 2 GB RAM, Singapore), bằng
`deploy/docker-compose.yml` gồm bốn container: app (build từ `Dockerfile` ở gốc repo), PostgreSQL,
Redis và Caddy làm reverse proxy tự lấy chứng chỉ Let's Encrypt.

App chạy với **`ASPNETCORE_ENVIRONMENT=Development`**: tự migrate và seed tài khoản demo khi khởi
động. Server chỉ deploy nhánh `main`, chỉ chứa dữ liệu giả.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| EC2 + RDS PostgreSQL + ElastiCache Redis | Đúng kiểu hạ tầng production | RDS và ElastiCache mỗi thứ tốn ngang hoặc hơn cả EC2 | Không đủ credit đến hết tháng 12 |
| Render / Railway gói miễn phí | Không phải quản lý máy | App ngủ khi không dùng, PostgreSQL miễn phí của Render hết hạn sau 30 ngày | Không sống được đến tháng 12 |
| Mac của một thành viên + tunnel | Miễn phí, dựng trong vài phút | Chỉ truy cập được khi máy đó bật | Không phải server chung |
| Chạy ở `Staging`/`Production` | Không lộ trang lỗi chi tiết, không có mật khẩu công khai | Phải viết thêm cơ chế migrate có chủ đích và tạo quản trị viên đầu tiên | Chưa có trong phạm vi; để dành cho khi triển khai thật |

## Hệ quả

- Chi phí khoảng 20 USD/tháng; 120 USD đủ đến hết tháng 12. Đặt AWS Budget cảnh báo ở 80 USD.
- Ai có URL cũng đăng nhập được bằng tài khoản quản trị demo, và lỗi trả về stack trace. Chấp nhận
  được vì dữ liệu là đồ giả; không bao giờ nhập dữ liệu thật vào đây.
- Deploy một nhánh có migration chưa merge sẽ làm hỏng schema của `main`, nên chỉ deploy `main`.
- Không có backup: mất máy thì dựng lại và seed lại.
- Khi làm phòng họp trực tuyến (LiveKit, ADR-0010), server cần mở thêm cổng UDP và có thể phải nâng
  cấu hình máy; xem lại chi phí lúc đó.
- Một bản triển khai thật về sau vẫn cần ADR riêng: migrate có chủ đích, tạo quản trị viên đầu tiên,
  backup.
