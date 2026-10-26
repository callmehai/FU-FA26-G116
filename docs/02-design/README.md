# 02 · Thiết kế

Đã có:

- [`01-vong-doi-cuoc-hop.md`](01-vong-doi-cuoc-hop.md) — 7 trạng thái của cuộc họp và 4 mức mật
- [`02-erd.md`](02-erd.md) — mô hình dữ liệu, kèm phần lấy gì / bỏ gì so với AI-eCabinet
- [`03-co-so-du-lieu.md`](03-co-so-du-lieu.md) — cài PostgreSQL, chuỗi kết nối, vòng đời migration
- [`04-luong-nghiep-vu-chinh.md`](04-luong-nghiep-vu-chinh.md) — 4 luồng nghiệp vụ chính BF-01 → BF-04 (bản PO duyệt 09/10/2026)
- [`08-ke-hoach-phong-hop-truc-tuyen.md`](08-ke-hoach-phong-hop-truc-tuyen.md) — thiết kế giải pháp phòng họp trực tuyến (LiveKit / WebRTC)
- [`10-scrum97-ke-hoach-backend.md`](10-scrum97-ke-hoach-backend.md) — kế hoạch thực thi UC-73, nguồn yêu cầu, thứ tự validation và ma trận kiểm thử Backend.

Dự kiến có thêm:

- `05-kien-truc.md` — sơ đồ tổng thể: SPA ↔ ASP.NET Core ↔ PostgreSQL, vị trí SignalR & LiveKit
- `06-giao-dien.md` — sơ đồ màn hình, wireframe, quy ước thiết kế
- `07-bao-mat.md` — xác thực, phân quyền RBAC, kiểm soát truy cập tài liệu

Sơ đồ đặt trong các thư mục [06-sequencediagram](../06-sequencediagram/),
[07-classdiagram](../07-classdiagram/), [09-bussinessflow](../09-bussinessflow/),
[10-statemachine](../10-statemachine/), [11-contextdiagram](../11-contextdiagram/),
[12-screenflow](../12-screenflow/), [13-erd](../13-erd/) và [14-uc-diagram](../14-uc-diagram/);
commit cả file nguồn lẫn ảnh xuất ra.

SCRUM-98: ERD Markdown và hướng dẫn database mô tả migration Agenda, legacy presenter và khóa kết luận bằng MeetingMinutes. Nguồn diagram không sửa; phần lệch được ghi ở [bàn giao](../03-api/08-scrum98-ban-giao.md).
