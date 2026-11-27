# Screen Flow

Thư mục chứa sơ đồ luồng chuyển màn hình (Screen Flow) của TrustCabinet, phục vụ đặc tả kiến trúc điều hướng giao diện trong Report 3 (mục I.5.1.a) và quá trình thiết kế màn hình.

- [TrustCabinet-screen-flow.drawio](TrustCabinet-screen-flow.drawio) — file nguồn editable (vẽ trên draw.io).
- [TrustCabinet-screen-flow.png](TrustCabinet-screen-flow.png) — hình xuất để nhúng vào báo cáo và xem nhanh.

## Quy tắc chỉnh sửa

- Khi chỉnh sửa luồng màn hình, đồng bộ với danh sách màn hình (Screen List) trong Report 3 (§I.5.1.b) và wireframe phiên bản hiện hành tại [docs/wireframe/](../wireframe/).
- Cập nhật file ảnh `.png` sau khi chỉnh sửa nguồn `.drawio`.
- Tuân thủ [DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md) và hướng dẫn của [drawio-skill](../../.agents/skills/drawio-skill/SKILL.md).
- Ảnh xuất ở tỉ lệ 2x (`drawio -x -f png -s 2 -o TrustCabinet-screen-flow.png TrustCabinet-screen-flow.drawio`), từ 25/11/2026
  (note 61 §4). Report 3 §I.5.1.a dùng ảnh này, có caption `Figure 14:` và link ảnh gốc. Sơ đồ đã cập nhật 27/11/2026 theo code hiện hành: bỏ các màn đã loại (`14.1 Personal Dashboard`, `14.2 Meeting Workspace`, `Sign Minutes`, `Signing Status`), chuyển `7.5 Document Preview Modal` sang dạng modal (ellipse), bổ sung các màn mới (`7.6`, `8.6`, `8.8`, `9.3`, `9.4`, `10.3`, `4.10–4.12`, `15.1–15.7`) và đồng bộ 1-1 với Screen List (§I.5.1.b).
