# 16 · Kiến trúc phần mềm

Sơ đồ kiến trúc tổng thể của TrustCabinet (Report 4, mục I.1 Software Architecture), vẽ theo **code
và cấu hình triển khai hiện có** (17/11/2026, `refactor/clean-architecture` `e2c23dd0`), không phải thiết kế mục
tiêu. Application Tier là bốn project ([ADR-0064](../05-decisions/0064-backend-bon-project-clean-architecture.md)):
`Web` (xác thực, endpoint, hub, publisher, worker) gọi Service Layer ở `Application`; service đọc ghi qua **Ports**
khai ở `Application`; `Infrastructure` cài các port đó bằng repository/query trên EF Core và bằng các adapter tích hợp;
`Domain` là entity, enum và rule. Bản ba project trước đó (09/11/2026,
[ADR-0059](../05-decisions/0059-giu-ba-project-backend-tach-application-sau-bao-cao.md)) nằm trong lịch sử git.
Nguồn của từng thành phần:

- tầng client: `frontend/src` và `frontend/package.json`;
- tầng ứng dụng: `backend/TrustCabinet.Web/Program.cs` (xác thực JWT, endpoint, hub, worker), service và port trong
  `backend/TrustCabinet.Application`, repository, query và adapter trong `backend/TrustCabinet.Infrastructure`;
- reverse proxy, dữ liệu và dịch vụ ngoài: `deploy/docker-compose.yml`, `deploy/Caddyfile`,
  `docker-compose.yml` ở gốc repo và `workers/speech`.

| Sơ đồ | Source | Ảnh |
|---|---|---|
| Kiến trúc phần mềm (Report 4, Figure 1.1) | [kien-truc-phan-mem.drawio](kien-truc-phan-mem.drawio) | [kien-truc-phan-mem.png](kien-truc-phan-mem.png) |
| Triển khai vật lý (Report 4, Figure 1.2) | [trien-khai-vat-ly.drawio](trien-khai-vat-ly.drawio) | [trien-khai-vat-ly.png](trien-khai-vat-ly.png) |

Sơ đồ triển khai vật lý là UML deployment diagram của server `app`, thêm theo review của GVHD trên Report 4
(25/11/2026). Nguồn: `deploy/docker-compose.yml` (container, image, cổng, volume), `deploy/Caddyfile` (`/rtc` sang
LiveKit, còn lại sang app), `deploy/livekit.yaml` (webhook về `app:8080`), [deploy/README.md](../../deploy/README.md)
(máy Lightsail, firewall, backup `pg_dump`, máy AI qua Tailscale §11, Resend §8). Đường nối là đường truyền
(communication path), mũi tên chỉ bên mở kết nối; hai đầu là trao đổi hai chiều.

## Quy ước

- Bốn khung theo tầng: Client Tier (trình duyệt), Application Tier (một tiến trình ASP.NET Core 10),
  Data Tier, External Services. Caddy nằm giữa client và ứng dụng.
- Mũi tên liền là chiều gọi hoặc chiều dữ liệu; hai đầu là trao đổi hai chiều (WebRTC, email gửi đi
  và webhook báo trạng thái gửi). Mũi tên nét đứt `maps to` là ánh xạ entity, không phải lời gọi.
- Mũi tên nét đứt đầu tam giác rỗng `implements` là cài đặt interface: Repository Layer và Integration adapters (ở
  `TrustCabinet.Infrastructure`) cài Ports của Application. Service Layer chỉ gọi Ports, không gọi thẳng adapter
  hay EF Core.
- Change Publishers ↔ Service Layer hai đầu: service phát sự kiện qua port publisher, publisher hỏi
  `MeetingAudience` (Application) ai được nhận. SignalR Hubs cũng hỏi `MeetingAudience` (`reads audience`) thay cho
  đọc database.
- Background Workers: bảy worker ở Web chạy việc qua Service Layer; `Email delivery` (`EmailDeliveryWorker`) và
  `OrphanFilePurge` (`OrphanFilePurgeWorker`) là hai worker ở Infrastructure, chạy qua port của Application
  (`IEmailQueueProcessor`, `IStoredFileRepository`, `IFileStorage`, `IUnitOfWork`). Hình gộp chín worker vào một hộp.
- Phòng họp đi hai đường: signalling `/rtc` qua Caddy (`deploy/Caddyfile`), media WebRTC đi thẳng tới LiveKit
  qua cổng ICE 7881/TCP và 7882/UDP (`deploy/docker-compose.yml`).
- Hình vẽ server test: Caddy chỉ có ở đó. Khi dev, Vite proxy `/api` và `/hubs` thay Caddy. File tài liệu nằm trên
  SeaweedFS ở cả máy dev lẫn server `app` (ADR-0084); hộp kho tệp trên hình ghi *SeaweedFS* (trước 25/11/2026 là
  *Cloudflare R2*).

## Những gì cố ý không vẽ

- Phụ thuộc giữa từng package và class — xem [15-package-diagram](../15-package-diagram/README.md).
- Google Fonts mà trình duyệt tải về. GitHub Actions và máy Lightsail chỉ có trên sơ đồ triển khai.

## Sửa và xuất lại

Sửa trực tiếp `.drawio` theo [quy tắc chỉnh draw.io](../../DRAWIO_EDITING_RULES.md), rồi xuất lại
PNG và thay ảnh ở mục I.1 của Report 4 (`docs/08-reports/source/`):

```bash
draw.io -x -f png -s 2 -o kien-truc-phan-mem.png kien-truc-phan-mem.drawio
draw.io -x -f png -s 2 -o trien-khai-vat-ly.png trien-khai-vat-ly.drawio
```

Sau đó đẩy lên Drive bằng `python scripts/push_diagrams.py architecture --confirm` (thư mục Diagrams ›
Architecture diagram, xem [DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md)). Dòng "View the full-size image on
Google Drive" dưới Figure 1.1 của Report 4 trỏ vào file PNG đó trên Drive; cập nhật cùng file thì link giữ nguyên.
