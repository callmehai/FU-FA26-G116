# 16 · Kiến trúc phần mềm

Sơ đồ kiến trúc tổng thể của TrustCabinet (Report 4, mục I.1 Software Architecture), vẽ theo **code
và cấu hình triển khai hiện có** (28/10/2026), không phải thiết kế mục tiêu. Nguồn của từng thành
phần:

- tầng client: `frontend/src` và `frontend/package.json`;
- tầng ứng dụng: `backend/TrustCabinet.Web/Program.cs` (xác thực JWT, endpoint, hub, worker), các
  service và adapter trong `backend/TrustCabinet.Infrastructure`;
- reverse proxy, dữ liệu và dịch vụ ngoài: `deploy/docker-compose.yml`, `deploy/Caddyfile`,
  `docker-compose.yml` ở gốc repo và `workers/speech`.

| Source | Ảnh |
|---|---|
| [kien-truc-phan-mem.drawio](kien-truc-phan-mem.drawio) | [kien-truc-phan-mem.png](kien-truc-phan-mem.png) |

## Quy ước

- Bốn khung theo tầng: Client Tier (trình duyệt), Application Tier (một tiến trình ASP.NET Core 10),
  Data Tier, External Services. Caddy nằm giữa client và ứng dụng.
- Mũi tên liền là chiều gọi hoặc chiều dữ liệu; hai đầu là trao đổi hai chiều (WebRTC, email gửi đi
  và webhook báo trạng thái gửi). Mũi tên nét đứt `maps to` là ánh xạ entity, không phải lời gọi.
- Hình vẽ server test: Caddy chỉ có ở đó. Khi dev, Vite proxy `/api` và `/hubs` thay Caddy, file
  tài liệu nằm trên SeaweedFS thay cho Cloudflare R2.

## Những gì cố ý không vẽ

- Phụ thuộc chi tiết giữa các package — xem [15-package-diagram](../15-package-diagram/README.md).
- `NotificationWorker` gửi thẳng vào `NotificationHub`; trên hình gộp vào mũi tên Workers → Service
  Layer và Change Publishers → Hubs.
- Google Fonts mà trình duyệt tải về, và GitHub Actions/Lightsail của phần deploy.

## Sửa và xuất lại

Sửa trực tiếp `.drawio` theo [quy tắc chỉnh draw.io](../../DRAWIO_EDITING_RULES.md), rồi xuất lại
PNG và thay ảnh ở mục I.1 của Report 4 (`docs/08-reports/source/`):

```bash
draw.io -x -f png -s 2 -o kien-truc-phan-mem.png kien-truc-phan-mem.drawio
```
