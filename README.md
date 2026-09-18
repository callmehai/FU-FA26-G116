# TrustCabinet

Hệ thống **phòng họp không giấy ứng dụng AI**, có tích hợp **gọi thoại/video trong ứng dụng**.

Đồ án tốt nghiệp — **SEP490-G116**. Nghiệp vụ tham chiếu từ hệ thống AI-eCabinet (SmartMeet);
TrustCabinet là bản hiện thực độc lập trên nền .NET, có điều chỉnh phạm vi cho phù hợp đồ án.

> **Trạng thái: v0.1.0 — mới khởi tạo.** Hiện chỉ có trang giới thiệu tĩnh và một endpoint
> `/api/health`. Chưa có cơ sở dữ liệu, chưa có nghiệp vụ.

## Chạy thử

Cần .NET SDK 10 (bản pin trong [global.json](global.json)). Nếu chưa có, tải tại
<https://dotnet.microsoft.com/download/dotnet/10.0>.

```bash
dotnet restore
dotnet run --project src/TrustCabinet.Web
```

Mở <http://localhost:5225>. Kiểm tra API: <http://localhost:5225/api/health>.

## Cấu trúc

```
TrustCabinet.sln
global.json                      # pin phiên bản .NET SDK cho cả nhóm
src/
  TrustCabinet.Web/              # ASP.NET Core — API + phục vụ trang tĩnh
    Program.cs                   # cấu hình pipeline, khai báo endpoint
    appsettings.json
    wwwroot/                     # giao diện
      index.html
      css/site.css
      js/site.js
```

Khi nghiệp vụ lớn dần, dự kiến tách thêm `src/TrustCabinet.Domain`,
`src/TrustCabinet.Application`, `src/TrustCabinet.Infrastructure` và `tests/`.

## Công nghệ

| Thành phần | Lựa chọn | Ghi chú |
|---|---|---|
| Backend | ASP.NET Core 10 (Minimal API) | đã có |
| Realtime | SignalR | tín hiệu gọi, biểu quyết, thông báo — chưa làm |
| Thoại/video | WebRTC | chưa làm |
| Cơ sở dữ liệu | SQL Server + EF Core | chưa làm |
| Giao diện | HTML/CSS/JS thuần | có thể đổi sang SPA sau |

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
- (M) Theo dõi nhiệm vụ được giao
- (S) Tách người nói, gán danh tính giọng
- (S) Tóm tắt & trích nhiệm vụ bằng AI
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
