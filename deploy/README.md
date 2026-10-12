# Server test chung

Một bản TrustCabinet chạy 24/7 để cả nhóm (Tester, BA, giảng viên) mở bằng trình duyệt, không cần
cài gì. Chạy trên **một máy AWS EC2** bằng Docker Compose: app, PostgreSQL, Redis và Caddy (tự lấy
chứng chỉ HTTPS). Lý do chọn cách này: [ADR-0015](../docs/05-decisions/0015-server-test-tren-ec2.md).

> **Đây là môi trường test, không phải production.** App chạy ở chế độ `Development`: tự migrate,
> tự seed tài khoản demo với mật khẩu công khai trong repo, và trả stack trace khi lỗi. Ai có URL
> cũng đăng nhập được bằng quyền quản trị. **Chỉ nhập dữ liệu giả** — không tải lên tài liệu, ghi âm
> hay thông tin cá nhân thật (Nghị định 13/2023/NĐ-CP).

## Dành cho người test

Mở `https://<SITE_ADDRESS>` (link ghim trong nhóm). Tài khoản, mật khẩu chung `TrustCabinet@2026`:

| Email | Vai trò |
|---|---|
| `quantri@trustcabinet.local` | Quản trị |
| `chutoa@trustcabinet.local` | Chủ toạ (phê duyệt) |
| `thuky@trustcabinet.local` | Thư ký (tạo cuộc họp) |
| `thanhvien@trustcabinet.local` | Thành viên |

Cần thử nhiều vai trò cùng lúc thì mở thêm cửa sổ ẩn danh hoặc profile Chrome khác — mỗi cửa sổ
giữ một phiên đăng nhập.

Server luôn chạy code của nhánh `main` lần deploy gần nhất. Tính năng chưa merge vào `main` thì chưa
có trên server.

## Dành cho người deploy

### Cập nhật lên `main` mới nhất

Việc thường làm nhất — sau mỗi lần merge PR mà nhóm muốn test:

```bash
ssh -i ~/.ssh/trustcabinet-test.pem ubuntu@<SITE_ADDRESS>
cd TrustCabinet
git pull
docker compose -f deploy/docker-compose.yml up -d --build
docker image prune -f        # dọn image cũ, ổ đĩa chỉ có 20 GB
```

Build mất vài phút; trong lúc đó bản cũ vẫn chạy, chỉ gián đoạn vài giây lúc đổi container. Khởi
động lại, app tự áp dụng migration mới.

**Chỉ deploy `main`.** Database ở đây áp dụng migration tự động: deploy một nhánh có migration chưa
merge rồi quay về `main` sẽ để lại schema mà `main` không biết, và chỉ sửa được bằng cách xoá database.

### Xem log, kiểm tra

```bash
docker compose -f deploy/docker-compose.yml ps
docker compose -f deploy/docker-compose.yml logs -f app        # Ctrl+C để thoát
curl https://<SITE_ADDRESS>/api/health
```

Dòng `Failed executing DbCommand ... __ef_migrations_history` ở lần chạy đầu tiên là bình thường:
EF Core hỏi bảng lịch sử migration trước khi tạo nó.

### Xoá sạch dữ liệu, seed lại từ đầu

```bash
docker compose -f deploy/docker-compose.yml down -v     # -v xoá luôn database
docker compose -f deploy/docker-compose.yml up -d
```

Mọi cuộc họp, người dùng và vai trò tạo thêm đều mất; tài khoản demo được seed lại.

### Sửa trực tiếp database

Trên server:

```bash
docker compose -f deploy/docker-compose.yml exec postgres psql -U trustcabinet -d trustcabinet
```

Từ DBeaver ở máy mình: PostgreSQL không mở ra Internet, chỉ nghe ở `127.0.0.1:5432` của server, nên
DBeaver đi qua SSH. Tạo kết nối PostgreSQL mới:

| Tab | Trường | Giá trị |
|---|---|---|
| Main | Host / Port | `localhost` / `5432` (là localhost **của server**, nhìn từ đầu SSH) |
| Main | Database / Username | `trustcabinet` / `trustcabinet` |
| Main | Password | `POSTGRES_PASSWORD` trong `deploy/.env` trên server |
| SSH | Use SSH Tunnel | bật |
| SSH | Host / Port / User | Elastic IP / `22` / `ubuntu` |
| SSH | Authentication | Public Key, chọn file `trustcabinet-test.pem` |

Nhớ đây là database cả nhóm đang test — sửa tay có thể làm hỏng kịch bản của người khác.

## Dựng server lần đầu

Chỉ làm một lần. Ước tính chi phí khoảng 20 USD/tháng (EC2 `t4g.small` + 20 GB ổ đĩa + IP tĩnh), nên
120 USD credit đủ chạy liên tục đến hết tháng 12 với dư. **Không** dùng RDS hay ElastiCache — đắt
gấp nhiều lần mà một môi trường test không cần.

### 1. Tạo máy EC2 (AWS Console)

1. Chọn region **Asia Pacific (Singapore) `ap-southeast-1`** — gần Việt Nam nhất.
2. **EC2 → Launch instance**:
   - Name: `trustcabinet-test`
   - AMI: **Ubuntu Server 24.04 LTS**, Architecture **64-bit (Arm)**
   - Instance type: **`t4g.small`** (2 vCPU, 2 GB RAM)
   - Key pair: **Create new key pair**, ED25519, `.pem`, đặt tên `trustcabinet-test` — file tải về
     chỉ có một lần, cất vào `~/.ssh/` và `chmod 400`
   - Network settings: tick **Allow SSH from My IP**, **Allow HTTPS**, **Allow HTTP** (HTTP để Caddy
     xin chứng chỉ và chuyển hướng sang HTTPS)
   - Storage: **20 GiB gp3**
3. **EC2 → Elastic IPs → Allocate**, rồi **Associate** vào máy vừa tạo. Không có IP tĩnh thì mỗi lần
   stop/start máy đổi IP, và địa chỉ `sslip.io` bên dưới đổi theo.
4. **Billing → Budgets → Create budget**: ngưỡng 80 USD, gửi email cảnh báo — để biết trước khi hết
   credit.
5. **Billing → Free Tier**: xem ngày hết hạn của Free plan. Tài khoản Free plan bị đóng khi hết hạn
   (6 tháng) hoặc hết credit — nếu ngày hết hạn rơi trước 31/12 thì phải nâng lên Paid plan; credit
   còn lại vẫn được dùng tiếp.

### 2. Cài Docker trên server

```bash
ssh -i ~/.ssh/trustcabinet-test.pem ubuntu@<ELASTIC_IP>

# 2 GB swap: build .NET + frontend trên máy 2 GB RAM dễ hết bộ nhớ nếu không có
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
exit        # đăng nhập lại để nhóm docker có hiệu lực
```

### 3. Lấy code, cấu hình, chạy

```bash
ssh -i ~/.ssh/trustcabinet-test.pem ubuntu@<ELASTIC_IP>
git clone https://github.com/callmehai/TrustCabinet.git
cd TrustCabinet
```

Tạo `deploy/.env` — lệnh dưới tự lấy IP public, dựng địa chỉ `sslip.io` và sinh hai secret ngẫu nhiên:

```bash
IP=$(curl -s https://checkip.amazonaws.com)
cat > deploy/.env <<EOF
SITE_ADDRESS=${IP//./-}.sslip.io
POSTGRES_PASSWORD=$(openssl rand -hex 24)
SIGNING_KEY=$(openssl rand -hex 32)
EOF
cat deploy/.env
```

- `SITE_ADDRESS` — `sslip.io` là dịch vụ DNS miễn phí trả về IP nằm ngay trong tên: IP `13.250.1.2`
  → `13-250-1-2.sslip.io`. Có tên miền riêng thì trỏ bản ghi A về Elastic IP rồi sửa dòng này.
- `POSTGRES_PASSWORD`, `SIGNING_KEY` — sinh một lần, đừng đổi: đổi `POSTGRES_PASSWORD` sau khi
  database đã tạo thì app không kết nối được nữa; đổi `SIGNING_KEY` thì mọi người bị đăng xuất.

```bash
docker compose -f deploy/docker-compose.yml up -d --build
docker compose -f deploy/docker-compose.yml logs -f caddy app
```

Xong khi Caddy báo `certificate obtained successfully` và `curl https://<SITE_ADDRESS>/api/health`
trả `"status":"ok"`.

### 4. Đăng nhập bằng Google (tuỳ chọn)

Nút "Đăng nhập bằng Google" chỉ chạy khi origin của server có trong OAuth client. Người quản lý
OAuth client `1024662324975-…` trên Google Cloud Console thêm `https://<SITE_ADDRESS>` vào
**Authorized JavaScript origins**. Chưa thêm thì nút Google báo lỗi; đăng nhập email + mật khẩu vẫn
bình thường.

### 5. Gửi email thật (tuỳ chọn)

Mặc định tắt. Điền các biến `SMTP_*` trong `deploy/.env` (xem
[note SMTP](../docs/03-api/07-gui-email-thu-moi-note-cai-dat-may-moi.md#4-cấu-hình-smtp)), đặt
`SMTP_ENABLED=true`, rồi `docker compose -f deploy/docker-compose.yml up -d`. Email chỉ đến được
những người dùng có địa chỉ thật — các tài khoản `@trustcabinet.local` thì không.

## Lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| Trình duyệt báo chứng chỉ không hợp lệ | `logs caddy`: cổng 80/443 phải mở trong Security Group, `SITE_ADDRESS` phải trỏ đúng IP |
| `502 Bad Gateway` | App đang khởi động hoặc chết — `logs app` |
| Build bị kill giữa chừng | Thiếu RAM: kiểm tra swap bằng `free -h` |
| Đăng nhập xong bị văng ra ngay | Phải vào bằng `https://`; cookie refresh token chỉ gửi qua HTTPS |
| Không SSH được nữa | IP mạng nhà đổi — sửa rule SSH "My IP" trong Security Group |
| Hết ổ đĩa | `docker system prune -f` (không có `-v`, để giữ database) |
