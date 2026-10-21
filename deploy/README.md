# Server test chung

Một bản TrustCabinet chạy 24/7 để cả nhóm (Tester, BA, giảng viên) mở bằng trình duyệt, không cần
cài gì. Chạy trên **một máy AWS Lightsail** bằng Docker Compose: app, PostgreSQL, Redis, LiveKit (phòng
họp trực tuyến) và Caddy (tự lấy chứng chỉ HTTPS). Mỗi lần merge vào `main` và CI xanh, server **tự cập nhật**. Lý do chọn cách
này: [ADR-0015](../docs/05-decisions/0015-server-test-tren-lightsail.md).

> **Đây là môi trường test, không phải production.** App chạy ở chế độ `Development`: tự migrate,
> tự seed tài khoản demo với mật khẩu công khai trong repo, và trả stack trace khi lỗi. Ai có URL
> cũng đăng nhập được bằng quyền quản trị. **Chỉ nhập dữ liệu giả** — không tải lên tài liệu, ghi âm
> hay thông tin cá nhân thật (Nghị định 13/2023/NĐ-CP).

## Dành cho người test

Mở <https://trustcabinet.workspace-hub.space>. Tài khoản, mật khẩu chung `TrustCabinet@2026`:

| Email | Vai trò |
|---|---|
| `quantri@trustcabinet.local` | Quản trị |
| `chutoa@trustcabinet.local` | Chủ toạ (phê duyệt) |
| `thuky@trustcabinet.local` | Thư ký (tạo cuộc họp) |
| `thanhvien@trustcabinet.local` | Thành viên |

Cần thử nhiều vai trò cùng lúc thì mở thêm cửa sổ ẩn danh hoặc profile Chrome khác — mỗi cửa sổ
giữ một phiên đăng nhập.

### Phòng họp trực tuyến

Cuộc họp demo **"Rà soát kế hoạch chuyển đổi số"** đang diễn ra và họp trực tuyến, với ba thành
phần: `chutoa@` (chủ toạ), `thuky@` (thư ký), `thanhvien@` (thành viên). Mở cuộc họp → **Vào phòng
họp**, cho trình duyệt dùng micro và camera. Muốn thấy nhau thì mỗi người một cửa sổ, đăng nhập bằng
một tài khoản khác nhau.

- Server nhỏ (1 GB RAM, CPU chia sẻ): thử với **khoảng 5–6 người bật camera** là vừa, đừng mời cả
  lớp vào một lúc.
- Media đi thẳng tới server qua cổng **7882/UDP**, mạng chặn UDP thì qua **7881/TCP**. Wi-Fi công ty
  hay trường học chặn cả hai thì không vào được phòng (vẫn dùng được các màn khác); khi đó dùng 4G
  hoặc mạng nhà. Server test không có TURN qua cổng 443.

Server chạy đúng code của `main`. Merge PR xong khoảng 10 phút (CI rồi deploy) là bản mới lên server;
tính năng chưa merge thì chưa có.

## Tự động deploy

[.github/workflows/deploy.yml](../.github/workflows/deploy.yml) là job cuối của CI trên `main`, chạy
khi build & test đã xanh: build image trên runner của GitHub, chép sang server qua SSH, đưa code trên
server về đúng commit đó rồi khởi động lại container. App tự áp dụng migration mới khi khởi động.
Không build trên server: máy 1 GB tự build mất 15–20 phút và đốt hết CPU burst.

- Xem đã deploy xong chưa: bấm dấu tích cạnh commit mới nhất trên trang chủ repo — check *CI / Deploy
  server test / Build & deploy* xanh là server đã chạy bản đó. Mục **Deployments** ở cột phải trang
  chủ repo ghi lần deploy gần nhất, kèm link mở server.
- Deploy lại mà không cần push: tab **Actions** → *Deploy server test* → **Run workflow**.
- CI đỏ thì không deploy; server giữ bản cũ. Trên pull request, check deploy hiện là *Skipped*.

**Chỉ `main` được deploy.** Database ở đây áp dụng migration tự động: deploy một nhánh có migration
chưa merge rồi quay về `main` sẽ để lại schema mà `main` không biết, và chỉ sửa được bằng cách xoá
database.

### Thiết lập một lần

Workflow cần ba secret của repo. Làm trong terminal của server (Lightsail → máy `trustcabinet` →
**Connect using SSH**) và trang **GitHub → repo → Settings → Secrets and variables → Actions → New
repository secret**:

1. Tạo SSH key riêng cho GitHub Actions và cho phép nó đăng nhập server:
   ```bash
   ssh-keygen -t ed25519 -N "" -C github-actions-deploy -f ~/.ssh/ci_deploy
   cat ~/.ssh/ci_deploy.pub >> ~/.ssh/authorized_keys
   cat ~/.ssh/ci_deploy
   ```
   Secret **`DEPLOY_SSH_KEY`** = toàn bộ khối vừa in, từ `-----BEGIN` đến hết `-----END ...-----`.
   Lưu secret xong thì xoá bản trên server: `rm ~/.ssh/ci_deploy`.
2. Secret **`DEPLOY_HOST`** = IP tĩnh của server.
3. Secret **`DEPLOY_HOST_KEY`** = kết quả của lệnh dưới (dạng `ssh-ed25519 AAAA...`), để workflow
   nhận đúng server chứ không tin bất kỳ máy nào trả lời ở IP đó:
   ```bash
   awk '{print $1" "$2}' /etc/ssh/ssh_host_ed25519_key.pub
   ```

Thử bằng **Run workflow**; bước *Kiểm tra* xanh là xong.

### Bật phòng họp trên server dựng trước khi có LiveKit

Chỉ một việc: thêm hai rule firewall **7881/TCP** và **7882/UDP** như ở bước 2 của *Dựng server lần
đầu*. Secret của LiveKit thì lần deploy kế tiếp tự sinh trên server, và bước *Kiểm tra* của workflow
xác nhận LiveKit đã chạy sau Caddy.

## Việc làm tay trên server

Mở terminal: Lightsail → máy `trustcabinet` → **Connect using SSH**, rồi `cd ~/TrustCabinet`.

### Xem log, kiểm tra

```bash
docker compose -f deploy/docker-compose.yml ps
docker compose -f deploy/docker-compose.yml logs -f app        # Ctrl+C để thoát
docker compose -f deploy/docker-compose.yml logs caddy | grep -i certificate
docker compose -f deploy/docker-compose.yml logs livekit | grep -i nodeIP   # IP báo cho trình duyệt
```

Ở chế độ Development, app in mọi câu SQL ra log — rối nhưng không phải lỗi; tìm dòng `fail:` hay
`Exception`. Dòng `Failed executing DbCommand ... __ef_migrations_history` ở lần chạy đầu là bình
thường: EF Core hỏi bảng lịch sử migration trước khi tạo nó.

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
DBeaver đi qua SSH. Tải key SSH của Lightsail: **Account → SSH keys → Default keys → Singapore →
Download**. Rồi tạo kết nối PostgreSQL mới:

| Tab | Trường | Giá trị |
|---|---|---|
| Main | Host / Port | `localhost` / `5432` (là localhost **của server**, nhìn từ đầu SSH) |
| Main | Database / Username | `trustcabinet` / `trustcabinet` |
| Main | Password | `POSTGRES_PASSWORD` trong `deploy/.env` trên server |
| SSH | Use SSH Tunnel | bật |
| SSH | Host / Port / User | IP tĩnh / `22` / `ubuntu` |
| SSH | Authentication | Public Key, chọn file `.pem` vừa tải |

Nhớ đây là database cả nhóm đang test — sửa tay có thể làm hỏng kịch bản của người khác.

## Dựng server lần đầu

Chỉ làm một lần, toàn bộ trên giao diện web của AWS và GitHub. Chi phí khoảng 7 USD/tháng.

### 1. Tạo máy

Lightsail → **Create instance**:

- Location: **Singapore**.
- Platform **Linux operating system**, blueprint **Ubuntu 24.04 LTS**.
- Plan **General purpose**, network **Dual-stack**, size **$7** (1 GB RAM, 2 vCPU). Tài khoản AWS mới
  bị Lightsail chặn các gói lớn hơn; muốn lớn hơn thì gửi case *Account and billing* cho AWS Support.
- Đặt tên `trustcabinet`, bỏ qua snapshot, tag, advanced settings → **Create instance**.

### 2. Mạng

Máy `trustcabinet` → tab **Networking**:

- **Attach static IP** — không có IP tĩnh thì IP đổi khi máy khởi động lại. IP tĩnh không tốn tiền
  khi đang gắn vào máy, nhưng **tính tiền khi nằm không**: xoá máy thì xoá luôn IP tĩnh.
- **Firewall rules → Add rule → HTTPS**, nguồn *Any IPv4 or IPv6 address*. SSH (22) và HTTP (80) có
  sẵn; HTTP cần để Caddy xin chứng chỉ và chuyển hướng sang HTTPS.
- Thêm hai rule nữa cho media của phòng họp, cùng nguồn *Any IPv4 or IPv6 address*: **Custom, TCP,
  7881** và **Custom, UDP, 7882**. Thiếu hai rule này thì trang vẫn mở được nhưng vào phòng họp sẽ
  báo mất kết nối.

### 3. Tên miền

Trỏ một bản ghi **A** về IP tĩnh — hiện là `trustcabinet` → IP tĩnh trong DNS của
`workspace-hub.space` (Namecheap → Advanced DNS). Chưa có tên miền thì dùng `sslip.io`, dịch vụ DNS
miễn phí trả về IP nằm ngay trong tên: IP `13.250.1.2` → `13-250-1-2.sslip.io`.

### 4. Cài Docker

Terminal của server (**Connect using SSH**):

```bash
# 4 GB swap: máy chỉ có 1 GB RAM
sudo fallocate -l 4G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
```

Đóng terminal, mở lại để nhóm `docker` có hiệu lực.

### 5. Lấy code

Repo là private, nên server cần một **deploy key**: SSH key riêng của server, chỉ được **đọc** đúng
repo này.

```bash
ssh-keygen -t ed25519 -N "" -C lightsail-trustcabinet -f ~/.ssh/github_deploy
printf "Host github.com\n  IdentityFile ~/.ssh/github_deploy\n  IdentitiesOnly yes\n" > ~/.ssh/config
ssh-keyscan -t ed25519 github.com >> ~/.ssh/known_hosts
cat ~/.ssh/github_deploy.pub
```

Người có quyền admin repo dán dòng vừa in vào **GitHub → repo → Settings → Deploy keys → Add deploy
key**, để trống ô *Allow write access*. Rồi:

```bash
git clone git@github.com:callmehai/TrustCabinet.git
cd TrustCabinet
```

### 6. Cấu hình và chạy

```bash
cat > deploy/.env <<EOF
SITE_ADDRESS=trustcabinet.workspace-hub.space
POSTGRES_PASSWORD=$(openssl rand -hex 24)
SIGNING_KEY=$(openssl rand -hex 32)
LIVEKIT_API_SECRET=$(openssl rand -hex 32)
EOF
chmod 600 deploy/.env
docker compose -f deploy/docker-compose.yml up -d --build
```

- `SITE_ADDRESS` — tên miền ở bước 3. Đổi về sau thì sửa dòng này rồi `up -d`.
- `POSTGRES_PASSWORD`, `SIGNING_KEY` — sinh một lần, đừng đổi: đổi `POSTGRES_PASSWORD` sau khi
  database đã tạo thì app không kết nối được nữa; đổi `SIGNING_KEY` thì mọi người bị đăng xuất.
- `LIVEKIT_API_SECRET` — khoá để backend ký vé vào phòng họp. Quên dòng này cũng không sao: workflow
  deploy tự sinh và thêm vào `deploy/.env` nếu chưa có.

Lần đầu build trên server mất 15–20 phút; các lần sau GitHub Actions build hộ. Xong khi `logs caddy`
có `certificate obtained successfully`. Cuối cùng làm phần **Tự động deploy → Thiết lập một lần**.

### 7. Đăng nhập bằng Google (tuỳ chọn)

Nút "Đăng nhập bằng Google" chỉ chạy khi origin của server có trong OAuth client. Người quản lý
OAuth client `1024662324975-…` trên Google Cloud Console thêm `https://<SITE_ADDRESS>` vào
**Authorized JavaScript origins**. Chưa thêm thì nút Google báo lỗi; đăng nhập email + mật khẩu vẫn
bình thường.

### 8. Gửi email thật (tuỳ chọn)

Mặc định tắt. Điền các biến `SMTP_*` trong `deploy/.env` (xem
[note SMTP](../docs/03-api/07-gui-email-thu-moi-note-cai-dat-may-moi.md#4-cấu-hình-smtp)), đặt
`SMTP_ENABLED=true`, rồi `docker compose -f deploy/docker-compose.yml up -d`. Email chỉ đến được
những người dùng có địa chỉ thật — các tài khoản `@trustcabinet.local` thì không.

## Sau khi bảo vệ xong

Xoá máy `trustcabinet` **và** IP tĩnh trên Lightsail, rồi đóng tài khoản AWS. Tài khoản đang ở Paid
plan: hết credit là AWS trừ tiền thẻ.

## Lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| Trang không mở được, trình duyệt báo hết thời gian | Firewall Lightsail phải có HTTPS (443) |
| Trình duyệt báo chứng chỉ không hợp lệ | `logs caddy`; bản ghi DNS phải trỏ đúng IP tĩnh, `SITE_ADDRESS` phải khớp tên miền |
| `502 Bad Gateway` | App đang khởi động hoặc chết — `logs app` |
| Workflow deploy đỏ ở bước SSH | `DEPLOY_HOST` / `DEPLOY_HOST_KEY` sai, hoặc đã đổi IP tĩnh |
| Đăng nhập xong bị văng ra ngay | Phải vào bằng `https://`; cookie refresh token chỉ gửi qua HTTPS |
| Hết ổ đĩa | `docker system prune -f` (không có `-v`, để giữ database) |
| Vào phòng họp báo mất kết nối, hoặc quay mãi không thấy ai | Firewall Lightsail phải có 7881/TCP và 7882/UDP; `logs livekit \| grep -i nodeIP` phải ra IP tĩnh, không phải 172.x; thử bằng 4G để loại trừ Wi-Fi chặn cổng |
| Workflow deploy đỏ ở bước *Kiểm tra*, báo `/rtc/validate` | `logs livekit` và `logs caddy`; container `livekit` phải đang chạy (`ps`). `/rtc/validate` trả 200 là Caddy còn chạy Caddyfile cũ: `docker compose -f deploy/docker-compose.yml restart caddy` (Caddyfile mount từng file, sửa file xong container không tự đọc lại) |
