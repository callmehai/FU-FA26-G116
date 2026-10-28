# TrustCabinet

Hệ thống **phòng họp không giấy ứng dụng AI**, có **phòng họp trực tuyến và gọi thoại/video ngay
trong ứng dụng**.

Đồ án tốt nghiệp **SEP490-G116**.

## Hệ thống làm được gì

Một cuộc họp đi trọn vòng đời trong một ứng dụng: Thư ký lập lịch và mời thành phần, Chủ toạ phê
duyệt, mọi người đọc tài liệu và góp ý trước, vào họp trực tiếp hoặc trực tuyến, rồi bản ghi được
chuyển thành văn bản để làm biên bản.

| Mảng | Trạng thái |
|---|---|
| Đăng nhập (email + mật khẩu, Google), quản lý người dùng, phân quyền theo vai trò | Đã có |
| Lập lịch, phê duyệt, huỷ cuộc họp; lịch họp; mời thành phần, xác nhận tham dự | Đã có |
| Chương trình nghị sự, điểm danh | Đã có |
| Tài liệu cuộc họp (phiên bản, phân quyền đọc, góp ý), kho tài liệu cá nhân | Đã có |
| Phòng họp trực tuyến (thoại, video, chia sẻ màn hình) qua LiveKit tự host | Đã có |
| Ghi âm, chuyển giọng nói tiếng Việt thành chữ, tách người nói | Đã có |
| Voice ID — gợi ý tên người nói, chỉ khi người đó đồng ý | Đã có |
| Thông báo trong ứng dụng (realtime) và email | Đã có |
| Biểu quyết, biên bản và phê duyệt biên bản, tóm tắt bằng AI | Chưa làm |

Nhận dạng giọng nói chạy trên máy chủ của nhóm, âm thanh không gửi ra dịch vụ AI bên ngoài
([ADR-0026](docs/05-decisions/0026-tu-chay-nhan-dang-giong-noi.md)). Giọng nói dùng để nhận dạng
một người là dữ liệu sinh trắc học, nên Voice ID chỉ chạy khi người đó đồng ý và rút lại được
bất cứ lúc nào ([ADR-0027](docs/05-decisions/0027-voice-id-co-dong-y.md)).

## Dùng thử

Bản chạy sẵn của nhánh `main`: <https://trustcabinet.workspace-hub.space>. Mật khẩu chung
`TrustCabinet@2026`:

| Email | Vai trò |
|---|---|
| `quantri@trustcabinet.local` | Quản trị |
| `chutoa@trustcabinet.local` | Chủ toạ |
| `thuky@trustcabinet.local` | Thư ký |
| `thanhvien@trustcabinet.local` | Thành viên |

Đây là môi trường test, ai có URL cũng vào được — **chỉ nhập dữ liệu giả**. Cách deploy và dựng lại
server: [deploy/README.md](deploy/README.md).

## Chạy ở máy mình

Cần **.NET SDK 10** (bản pin trong [global.json](global.json)), **Node.js** và **Docker**.

Lần đầu:

```bash
cp .env.example .env            # rồi đặt mật khẩu của riêng bạn
docker compose up -d            # PostgreSQL, Redis, SeaweedFS (kho tệp), LiveKit
dotnet restore && dotnet tool restore
cd frontend && npm install
```

Chuỗi kết nối và các secret khác đặt bằng `dotnet user-secrets`, không để trong repo — các bước ở
[docs/02-design/03-co-so-du-lieu.md](docs/02-design/03-co-so-du-lieu.md). Thiếu chuỗi kết nối thì
backend cố ý không khởi động.

Chạy **hai tiến trình** ở hai terminal:

```bash
# Terminal 1 — backend, cổng 5225
dotnet run --project backend/TrustCabinet.Web

# Terminal 2 — frontend, cổng 5173, có hot reload
cd frontend && npm run dev
```

Mở <http://localhost:5173>. Vite proxy `/api` và `/hubs` sang backend nên trình duyệt chỉ thấy một
origin — không cần CORS. Ở môi trường Development, app tự chạy migration và seed bốn tài khoản như
bảng trên.

Chuyển giọng nói thành chữ cần thêm worker nhận dạng (tải model ~230 MB lần đầu):

```bash
docker compose --profile speech up -d --build
```

Chi tiết: [workers/speech/README.md](workers/speech/README.md).

## Kiểm tra trước khi mở PR

```bash
dotnet build TrustCabinet.sln -warnaserror   # CI coi cảnh báo là lỗi
dotnet test TrustCabinet.sln
cd frontend && npm run lint                  # oxlint
cd frontend && npm run build                 # tsc -b + vite build
```

CI chạy bốn bước này trên mọi pull request, nhưng chỉ phần bị đổi: PR chỉ sửa frontend thì backend
chỉ chạy các test đọc mã nguồn FE và docs, PR chỉ sửa README hay `deploy/` thì bỏ qua cả hai
([.github/workflows/ci.yml](.github/workflows/ci.yml)). Merge vào `main` xong thì server test tự cập
nhật, và chỉ build lại image nào có code đổi.

> **Không commit chuỗi kết nối, API key hay mật khẩu.** Repo để public. Dùng `dotnet user-secrets`
> ở máy mình và biến môi trường khi triển khai.

## Quy trình làm việc

Sprint 1 tuần, mỗi Use Case đi qua BE → FE → BA chốt tài liệu → Tester. Ai bàn giao gì cho ai,
Definition of Done, quy ước branch/PR: [docs/DEVELOPMENT_WORKFLOW.md](docs/DEVELOPMENT_WORKFLOW.md).
Jira (project `SCRUM`) là nguồn sự thật cho yêu cầu và kế hoạch Sprint.

## Cấu trúc

```
backend/
  TrustCabinet.Domain/           # entity và enum — không phụ thuộc EF Core
  TrustCabinet.Infrastructure/   # DbContext, cấu hình bảng, migration, seed
  TrustCabinet.Web/              # ASP.NET Core 10 — API, SignalR hub, worker nền
    Endpoints/                   # endpoint theo từng mảng nghiệp vụ
    wwwroot/                     # sinh từ `npm run build` — không commit
frontend/                        # React 19 + Vite + TypeScript
  src/
    app/App.tsx                  # bảng route
    features/                    # mỗi mảng nghiệp vụ một thư mục
    components/                  # khung ứng dụng và UI dùng chung
    api/  types/                 # lớp gọi HTTP và kiểu dữ liệu khớp response
    styles/global.css            # design token và toàn bộ style
workers/speech/                  # worker Python nhận dạng giọng nói (sherpa-onnx, chạy CPU)
tests/TrustCabinet.Web.Tests/    # xunit
deploy/                          # server test: Docker Compose + Caddy trên AWS Lightsail
docs/                            # tài liệu đồ án — xem docs/README.md
docker-compose.yml               # dịch vụ phụ trợ cho môi trường dev
AGENTS.md                        # hướng dẫn cho AI coding agent (CLAUDE.md trỏ về đây)
```

## Công nghệ

| Thành phần | Lựa chọn |
|---|---|
| Backend | ASP.NET Core 10 (Minimal API) |
| Frontend | React 19 + Vite + TypeScript, React Router |
| Cơ sở dữ liệu | PostgreSQL 17 + EF Core 10, code-first — [ERD](docs/02-design/02-erd.md) |
| Realtime | SignalR |
| Thoại/video | WebRTC qua LiveKit SFU tự host — [ADR-0010](docs/05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md) |
| Lưu tệp | SeaweedFS khi dev, Cloudflare R2 trên server test — [ADR-0024](docs/05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md) |
| Nhận dạng giọng nói | sherpa-onnx, tự chạy — [ADR-0026](docs/05-decisions/0026-tu-chay-nhan-dang-giong-noi.md) |
| Email | Resend — [ADR-0031](docs/05-decisions/0031-gui-email-qua-resend.md) |

Mỗi lựa chọn kỹ thuật lớn có một ADR trong [docs/05-decisions/](docs/05-decisions/), ghi lý do và
phương án đã loại.

## Ngoài phạm vi

- **Theo dõi nhiệm vụ** — giao việc, hạn chót, trạng thái hoàn thành. TrustCabinet dừng ở biên bản
  đã chốt; biên bản vẫn ghi phần phân công như văn bản.
- **Nhiều tổ chức, phòng ban, cây đơn vị** — hệ thống phục vụ đúng một tổ chức, mời họp là mời từng
  người ([ADR-0004](docs/05-decisions/0004-mot-to-chuc-khong-co-don-vi.md)).
- Ký số bằng USB token / HSM thật, triển khai mạng cô lập, tích hợp hệ thống văn bản điều hành bên
  ngoài.

## Tài liệu

[docs/](docs/) chứa phạm vi, đặc tả Use Case, thiết kế, hợp đồng API, sơ đồ và các ADR.
