# TrustCabinet

Hệ thống **phòng họp không giấy ứng dụng AI**, có tích hợp **gọi thoại/video trong ứng dụng**.

Đồ án tốt nghiệp — **SEP490-G116**. Nghiệp vụ tham chiếu từ hệ thống AI-eCabinet (SmartMeet);
TrustCabinet là bản hiện thực độc lập trên nền .NET, có điều chỉnh phạm vi cho phù hợp đồ án.

> **Trạng thái: v0.1.0 — mới dựng khung.** Đã có khung giao diện (đăng nhập, danh sách và chi
> tiết cuộc họp, quản trị) chạy trên dữ liệu mẫu, cùng một endpoint `/api/health`. Chưa có cơ sở
> dữ liệu, xác thực đang giả lập phía trình duyệt, chưa có nghiệp vụ thật.

## Chạy thử

Cần **.NET SDK 10** (bản pin trong [global.json](global.json), tải tại
<https://dotnet.microsoft.com/download/dotnet/10.0>) và **Node.js** (tải tại <https://nodejs.org>).

Lần đầu:

```bash
dotnet restore
cd frontend && npm install
```

Khi dev, chạy **hai tiến trình** ở hai terminal:

```bash
# Terminal 1 — backend, cổng 5225
dotnet run --project backend/TrustCabinet.Web

# Terminal 2 — frontend, cổng 5173, có hot reload
cd frontend && npm run dev
```

Mở <http://localhost:5173>. Vite proxy `/api` và `/hubs` sang backend nên trình duyệt chỉ thấy
một origin — không cần cấu hình CORS.

Ở màn đăng nhập, **bấm thẳng nút "Đăng nhập" là vào**, không cần điền gì: chưa có cơ sở dữ liệu
nên xác thực đang được giả lập. Ô "Vai trò" dùng để thử phân quyền — đổi vai trò sẽ thấy menu và
nút bấm thay đổi theo.

Muốn chạy bằng một tiến trình duy nhất (giống lúc triển khai): build frontend rồi chạy backend,
lúc đó vào <http://localhost:5225>.

```bash
cd frontend && npm run build   # xuất vào backend/TrustCabinet.Web/wwwroot/
dotnet run --project backend/TrustCabinet.Web
```

## Kiểm tra trước khi mở PR

```bash
dotnet build TrustCabinet.sln     # build backend
dotnet test TrustCabinet.sln      # test backend
cd frontend && npm run lint       # oxlint
cd frontend && npm run build      # tsc -b + vite build
```

CI chạy đúng bốn lệnh này trên mọi pull request
([.github/workflows/ci.yml](.github/workflows/ci.yml)).

> **Không commit chuỗi kết nối, API key hay mật khẩu.** `appsettings.json` được commit và repo
> đang để public, nên secret không nằm ở đó. Khi làm phần SQL Server, dùng
> `dotnet user-secrets set "ConnectionStrings:Default" "..."` ở máy mình, và biến môi trường khi
> triển khai.

## Quy trình làm việc

Nhóm chạy theo Sprint 1 tuần, xử lý từng Use Case theo đường ống BA → BE → FE → Tester, không
làm xong hết tài liệu rồi mới code. Chi tiết — ai bàn giao gì cho ai, Definition of Done, quy ước
branch/PR — nằm trong [docs/DEVELOPMENT_WORKFLOW.md](docs/DEVELOPMENT_WORKFLOW.md). Đọc trước khi
bắt tay vào việc đầu tiên.

Jira (project `SCRUM`) là nguồn sự thật cho yêu cầu, user story và kế hoạch Sprint.

## Cấu trúc

```
TrustCabinet.sln
global.json                      # pin phiên bản .NET SDK cho cả nhóm
.editorconfig                    # quy ước format chung, mọi IDE đều đọc
.github/workflows/ci.yml         # CI: build + test backend, lint + build frontend
CLAUDE.md                        # hướng dẫn cho Claude Code
backend/
  TrustCabinet.Web/              # ASP.NET Core 10 — API + phục vụ bản build của frontend
    Program.cs                   # cấu hình pipeline, khai báo endpoint
    appsettings.json
    wwwroot/                     # sinh tự động từ `npm run build` — không commit
frontend/                        # React 19 + Vite + TypeScript, chia theo feature
  vite.config.ts                 # proxy sang backend khi dev, outDir về wwwroot khi build
  src/
    main.tsx                     # điểm vào
    app/App.tsx                  # bảng route
    lib/routes.ts                # bản đồ đường dẫn tập trung
    features/                    # auth · meetings · access · calendar
    components/                  # khung ứng dụng & UI dùng chung
    api/                         # lớp gọi HTTP dùng chung
    types/                       # kiểu dữ liệu khớp response của backend
    styles/global.css            # design token + toàn bộ style
tests/
  TrustCabinet.Web.Tests/        # xunit — hiện mới có smoke test cho /api/health
docs/                            # tài liệu đồ án — xem docs/README.md
  DEVELOPMENT_WORKFLOW.md        # quy trình làm việc của nhóm — đọc trước khi bắt tay
```

Giao diện dựng theo khung của hệ thống tham chiếu AI-eCabinet: hai không gian làm việc
(`/mgmt`, `/calendar`) dùng chung một khung. Lý do và ranh giới tham chiếu: xem
[ADR-0002](docs/05-decisions/0002-khung-giao-dien-theo-ai-ecabinet.md).

Vì sao tách `backend/` và `frontend/`: xem
[ADR-0001](docs/05-decisions/0001-tach-frontend-backend.md).

Khi nghiệp vụ lớn dần, dự kiến tách thêm `backend/TrustCabinet.Domain`,
`backend/TrustCabinet.Application` và `backend/TrustCabinet.Infrastructure`.

## Công nghệ

| Thành phần | Lựa chọn | Ghi chú |
|---|---|---|
| Backend | ASP.NET Core 10 (Minimal API) | đã có |
| Frontend | React 19 + Vite + TypeScript | đã có |
| Định tuyến FE | React Router | đã có |
| Realtime | SignalR | tín hiệu gọi, biểu quyết, thông báo — chưa làm |
| Thoại/video | WebRTC | chưa làm |
| Cơ sở dữ liệu | SQL Server + EF Core | chưa làm |

## Phạm vi chức năng dự kiến

Mức ưu tiên: **(M)** bắt buộc · **(S)** nên có · **(C)** có thể có.

### Trước họp
- (M) Tạo lịch họp, gửi thư mời, xác nhận tham dự
- (M) Tải tài liệu, quản lý phiên bản, phân quyền đọc
- (S) Góp ý trên tài liệu trước họp
- (S) Biểu quyết bất đồng bộ (lấy ý kiến trước)

### Trong họp
- (M) Điều hành chương trình nghị sự
- (M) Điểm danh, đăng ký phát biểu
- (M) Biểu quyết trực tiếp, hiển thị kết quả
- (M) Ghi âm phiên họp hoặc nhập file có sẵn
- (S) Ghi chú thời gian thực cho thư ký

### Sau họp
- (M) Chuyển giọng nói thành văn bản (ASR tiếng Việt)
- (M) Sinh biên bản, phê duyệt, lưu bản chốt
- (S) Tách người nói, gán danh tính giọng
- (S) Tóm tắt nội dung cuộc họp bằng AI
- (C) Ký số biên bản (PKI)
- (C) Báo cáo, thống kê

### Gọi trong ứng dụng — điểm khác biệt so với hệ thống gốc
- (M) Gọi thoại 1–1 giữa các thành viên
- (M) Gọi nhóm theo phòng họp
- (S) Gọi video, bật/tắt mic & camera
- (S) Chia sẻ màn hình khi trình bày
- (C) Ghi lại cuộc gọi để đưa vào luồng ASR

### Nền tảng dùng chung
- (M) Đăng nhập, phân quyền theo vai trò (RBAC)
- (M) Quản lý người dùng & đơn vị
- (M) Thông báo trong ứng dụng & email
- (S) Nhật ký kiểm toán các hành động quan trọng

### Không làm

Đã chốt là ngoài phạm vi, không đưa lại vào backlog:

- Quản lý nhiệm vụ / action item — giao việc, hạn chót, trạng thái hoàn thành, nhắc việc.
  TrustCabinet dừng ở biên bản đã chốt; việc theo dõi thực thi thuộc về hệ thống điều hành
  công việc khác. Biên bản vẫn ghi phần "phân công" như văn bản, nhưng hệ thống không theo dõi
  trạng thái của nó.

## Nhóm chức năng cân nhắc thu hẹp

Tốn nhiều công và rủi ro cao cho một đồ án. Nếu tiến độ căng, cắt hoặc đơn giản hoá
nhóm này trước tiên:

- Nhận diện người nói bằng voiceprint — là dữ liệu sinh trắc học, kéo theo yêu cầu
  đồng ý và xoá dữ liệu theo Nghị định 13/2023/NĐ-CP
- Ký số bằng USB token / HSM thật — thay bằng ký mô phỏng để trình diễn
- Trợ lý AI hỏi đáp xuyên nguồn, có trích dẫn
- Triển khai mạng cô lập (air-gapped), tự vận hành mô hình AI
- Tích hợp hệ thống eOffice bên ngoài

Danh sách này chưa chốt — thống nhất lại với giảng viên hướng dẫn trước khi khoá phạm vi.

## Tài liệu

Toàn bộ tài liệu đồ án nằm trong [docs/](docs/) — phạm vi, thiết kế, hợp đồng API, biên bản họp
nhóm và các quyết định kỹ thuật (ADR).
