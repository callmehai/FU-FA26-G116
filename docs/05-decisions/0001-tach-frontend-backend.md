# ADR-0001: Tách frontend và backend thành hai thư mục

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-18
- **Người quyết định:** Nhóm SEP490-G116

## Bối cảnh

Commit khởi tạo `6f14d06` dựng đúng một project ASP.NET Core (`src/TrustCabinet.Web`) vừa làm API
vừa phục vụ giao diện từ `wwwroot/` bằng HTML/CSS/JS thuần, không có bước build và không có npm.

Cách đó ổn cho một trang giới thiệu tĩnh, nhưng phạm vi đồ án gồm phòng họp realtime, biểu quyết
trực tiếp và gọi thoại/video WebRTC — toàn những màn hình có nhiều trạng thái phía client. Viết
bằng JS thuần với thao tác DOM tay sẽ phình rất nhanh. Ngoài ra nhóm cần chia việc FE/BE song song,
mà khi hai phần nằm chung một thư mục thì ranh giới trách nhiệm không rõ.

## Quyết định

Chúng tôi sẽ tách thành hai thư mục ngang hàng ở gốc repo:

- `backend/` — ASP.NET Core 10, chỉ còn nhiệm vụ phục vụ API và bản build tĩnh của FE
- `frontend/` — SPA React 19 + Vite + TypeScript

Khi dev chạy hai tiến trình: Vite ở `:5173`, backend ở `:5225`. Vite proxy `/api` và `/hubs` sang
backend nên trình duyệt chỉ thấy một origin — **không cần cấu hình CORS**, và cũng không cần biến
môi trường base URL vì FE luôn gọi đường dẫn tương đối.

Khi build, `npm run build` xuất thẳng vào `backend/TrustCabinet.Web/wwwroot/`; backend có
`MapFallbackToFile("index.html")` để client-side routing hoạt động. Thư mục `wwwroot/` là sản phẩm
build nên được gitignore.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ HTML/CSS/JS thuần trong `wwwroot` | Không thêm toolchain, chạy một lệnh | Không component hoá, không kiểu tĩnh; màn hình họp realtime + WebRTC sẽ rất khó bảo trì | Phạm vi đồ án quá nặng phần client |
| Blazor Server / WebAssembly | Một ngôn ngữ C# cho cả hai phía | Hệ sinh thái WebRTC nghèo, phải interop JS cho đúng phần khó nhất; nhóm chưa quen | Rủi ro cao ở đúng chức năng khác biệt của đồ án |
| Next.js | Có routing/SSR sẵn | Nặng hơn, thêm một tầng server nữa bên cạnh .NET; ranh giới server component dễ gây nhầm | Không cần SSR cho ứng dụng nội bộ sau đăng nhập |
| React + Vite + TypeScript | Nhẹ, hot-reload nhanh, tài liệu nhiều, hợp WebRTC/SignalR | Thêm Node vào yêu cầu môi trường, phải chạy hai tiến trình khi dev | **Đã chọn** |

## Hệ quả

- Mọi thành viên cần cài thêm Node.js bên cạnh .NET SDK 10.
- Khi dev phải mở hai terminal — đổi lại có hot-reload cho FE.
- `backend/TrustCabinet.Web/wwwroot/` không còn nằm trong git; ai clone mới mà chưa build FE thì
  mở `http://localhost:5225/` sẽ nhận 404, còn `/api/health` vẫn chạy bình thường.
- Giao diện cũ trong `wwwroot` đã được chuyển thành component React; stylesheet `site.css` giữ
  nguyên thành `frontend/src/styles/global.css` nên bộ token màu và giao diện không đổi.
- Hợp đồng API giữa hai bên giờ là ranh giới thật, cần chốt trong [../03-api/](../03-api/).
