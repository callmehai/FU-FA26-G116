# Server test chung

Một bản TrustCabinet chạy 24/7 để cả nhóm (Tester, BA, giảng viên) mở bằng trình duyệt, không cần
cài gì. Chạy trên **một máy AWS Lightsail** bằng Docker Compose: app, PostgreSQL, Redis, LiveKit (phòng
họp trực tuyến), Caddy (tự lấy chứng chỉ HTTPS), và máy chuyển chữ nếu máy đủ 4 GB RAM (§10). Mỗi lần merge vào `main` và CI xanh, server **tự cập nhật**. Lý do chọn cách
này: [ADR-0015](../docs/05-decisions/0015-server-test-tren-lightsail.md).

> **Đây là môi trường test, không phải production.** App chạy ở chế độ `Development`: tự migrate,
> tự seed tài khoản demo với mật khẩu công khai trong repo, và trả stack trace khi lỗi. Ai có URL
> cũng đăng nhập được bằng quyền quản trị. **Chỉ nhập dữ liệu giả** — không tải lên tài liệu, ghi âm
> hay thông tin cá nhân thật (Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15).

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

Mặc định tắt. Server gửi qua Resend, từ `no-reply@<SITE_ADDRESS>`
([ADR-0031](../docs/05-decisions/0031-gui-email-qua-resend.md)). Làm một lần:

1. **Tên miền gửi.** Trên [resend.com](https://resend.com) → *Domains* → *Add Domain*, nhập đúng `SITE_ADDRESS`
   (ví dụ `trustcabinet.workspace-hub.space`), region Tokyo (gần server Singapore). Resend đưa ra 3 bản ghi: TXT
   `resend._domainkey` (DKIM), CNAME `rsend` và CNAME `send` (SPF, địa chỉ nhận thư dội). Thêm chúng ở Namecheap →
   *Advanced DNS*. Ô *Host* chỉ ghi phần trước tên miền gốc, ví dụ `resend._domainkey.trustcabinet`,
   `rsend.trustcabinet`, `send.trustcabinet`; giá trị chép đúng từ trang của Resend. Chờ Resend báo *Verified*.
2. **API key.** *API Keys* → *Create*, quyền **Sending access**, chỉ trên tên miền vừa xác minh. Key chỉ hiện một
   lần.
3. **Webhook.** *Webhooks* → *Add Endpoint*: URL `https://<SITE_ADDRESS>/api/email/resend/webhook`, chọn các sự kiện
   `email.delivered`, `email.delivery_delayed`, `email.bounced`, `email.complained`, `email.suppressed`,
   `email.failed`. Mở webhook vừa tạo, chép *Signing Secret* (`whsec_…`).
4. **`deploy/.env`.** Thêm từng dòng một bằng `nano` (key và secret là một dòng, không dán nhiều dòng một lúc):
   `RESEND_API_KEY=re_…`, `RESEND_FROM=no-reply@<SITE_ADDRESS>`, `RESEND_WEBHOOK_SECRET=whsec_…`, rồi
   `RESEND_ENABLED=true`, và chạy `docker compose -f deploy/docker-compose.yml up -d`.

Email chỉ đến được những người dùng có địa chỉ thật. Thư tới các tài khoản `@trustcabinet.local` được đánh dấu
`Failed` ngay, không gửi đi (`Email:UndeliverableDomains` trong `appsettings.Development.json`): gửi tới đó chỉ
bị dội lại, mà tỉ lệ dội cao thì Resend khoá tài khoản.

Bật lần đầu thì mọi lời mời tích lại từ lúc tắt được tạo thành thư và gửi một lượt. Gói miễn phí cho 100 thư/ngày:
quá thì Resend trả `429`, thư ở lại hàng đợi và được thử lại sau. Xem hàng đợi và kết quả giao thư bằng
`docker compose -f deploy/docker-compose.yml exec postgres psql -U trustcabinet -d trustcabinet -c "select status,
delivery_state, count(*) from email_messages group by 1, 2"`, hoặc trên trang *Emails* của Resend.

Quay lại SMTP (ví dụ máy chủ thư nội bộ): `EMAIL_PROVIDER=Smtp`, các biến `SMTP_*` và `SMTP_ENABLED=true` (xem
[note SMTP](../docs/03-api/07-gui-email-thu-moi-note-cai-dat-may-moi.md#4-cấu-hình-smtp)).

### 9. Kho tệp tài liệu trên Cloudflare R2

Tải tài liệu lên cuộc họp lưu byte trên R2 ([ADR-0024](../docs/05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md)).
Chưa cấu hình thì app vẫn chạy, nút tải lên báo `MSG166`.

1. Cloudflare Dashboard → **R2 Object Storage** → bật R2 (cần thêm phương thức thanh toán, gói miễn phí vẫn đủ
   cho server test) → **Create bucket** `trustcabinet-docs`, Location: **Provide a location hint → Asia-Pacific
   (APAC)** (server test ở Singapore). Vị trí không đổi được sau khi tạo, và R2 **chỉ nhận hint ở lần đầu một tên
   bucket được tạo**: xoá rồi tạo lại cùng tên thì vẫn ra vị trí cũ, nên muốn đổi vị trí phải dùng tên mới
   (`trustcabinet-test` đã bị khoá ở ENAM). Xem **Settings → Location** để chắc chắn là APAC. **Không** bật Public
   access hay custom domain.
2. **R2 → Manage API tokens → Create API token**: quyền *Object Read & Write*, áp dụng cho **chỉ** bucket
   `trustcabinet-docs`. Lưu lại Access Key ID, Secret Access Key và endpoint
   `https://<account-id>.r2.cloudflarestorage.com` (secret chỉ hiện một lần).
3. Trên server, thêm vào `deploy/.env` rồi `docker compose -f deploy/docker-compose.yml up -d`:

   ```bash
   R2_ENDPOINT=https://<account-id>.r2.cloudflarestorage.com
   R2_BUCKET=trustcabinet-docs
   R2_ACCESS_KEY_ID=...
   R2_SECRET_ACCESS_KEY=...
   ```

Trình duyệt không gọi thẳng R2 nên CSP trong `Caddyfile` giữ nguyên. Chỉ tải lên dữ liệu giả: R2 lưu ngoài Việt
Nam, tức là chuyển dữ liệu cá nhân xuyên biên giới (Luật 91/2025/QH15 Điều 20, xem ADR-0024).

### 10. Máy chuyển chữ (tuỳ chọn, cần máy 4 GB)

Chuyển bản ghi thành chữ, Voice ID và ghi theo micro từng người đều cần worker nhận dạng giọng nói
([`workers/speech`](../workers/speech/README.md), [ADR-0026](../docs/05-decisions/0026-tu-chay-nhan-dang-giong-noi.md)).
Chưa bật thì app vẫn chạy, các chức năng đó báo `MSG312`. Worker chạy ngay trên server, không mở cổng ra
ngoài, nên âm thanh không rời server.

Worker cần khoảng 2,5 GB RAM, máy $7 (1 GB) **không chạy nổi**. Bật trên máy 1 GB thì server hết RAM.

1. **Lên máy 4 GB ($24/tháng)**: Lightsail → máy `trustcabinet` → **Snapshots → Create snapshot**. Từ snapshot
   đó → **Create new instance**, chọn size **$24** (4 GB). Lightsail không đổi size tại chỗ. Tài khoản bị chặn
   gói này thì gửi case *Account and billing* cho AWS Support (xem §1).
2. **Chuyển IP tĩnh** sang máy mới: **Networking → IP tĩnh → Attach** vào máy mới. Tên miền giữ nguyên.
   Mở lại firewall như §2 (máy mới tạo từ snapshot không mang theo firewall).
3. Trên máy mới, thêm vào `deploy/.env`:

   ```bash
   COMPOSE_PROFILES=speech
   SPEECH_URL=http://speech:8090
   ```

4. GitHub → **Actions → Deploy server test → Run workflow**. Lần đầu workflow build image worker (~770 MB) rồi
   chép sang, mất thêm vài phút. Các lần sau chỉ chép lại khi `workers/speech` đổi. Bước *Kiểm tra máy chuyển
   chữ* xanh là xong.
5. Xoá máy 1 GB cũ khi máy mới đã chạy ổn, để không bị tính tiền hai máy.

CPU của Lightsail là loại *burst*: chạy hết công suất được trong thời gian ngắn, chạy liên tục thì bị giới hạn.
Một câu nói chuyển chữ trong vài giây. Một bản ghi dài chạy chậm dần, và lúc đó phòng họp cũng có thể giật.

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
