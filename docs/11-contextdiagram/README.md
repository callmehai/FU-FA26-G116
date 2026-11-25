# Context Diagram

Thư mục chứa context diagram của TrustCabinet ở dạng draw.io có thể chỉnh sửa và PNG để xem nhanh.

- [trustcabinet-context.drawio](trustcabinet-context.drawio) — source editable.
- [trustcabinet-context.drawio.png](trustcabinet-context.drawio.png) — ảnh xuất từ source.

Ngày 25/11/2026 bỏ ô *File Storage* (trước là Cloudflare R2) và hai luồng tệp: kho SeaweedFS chạy trên chính server
`app` (ADR-0084) nên nằm trong ranh giới hệ thống; nó được vẽ ở [sơ đồ kiến trúc](../16-architecture-diagram/README.md).

Cũng ngày 25/11/2026, luồng dữ liệu bám theo quyền của vai trò hệ thống (`ReferenceData.cs`): Approver và System Admin đều giữ quyền tạo cuộc họp nên có thêm *Meeting setup & approval data* (vào) và *Meeting status & approval results* (ra); System Admin thu giọng mẫu hộ nên có *Voice samples & consent data* / *Voice sample status*; bỏ *Meeting documents* khỏi Member vì chỉ Chủ trì và Thư ký tải tài liệu cuộc họp. Nhãn luồng luôn là danh từ chỉ dữ liệu, không phải động từ.

Khi chỉnh sửa, giữ ranh giới hệ thống và các hệ thống/actor bên ngoài đúng theo tài liệu kiến trúc;
cập nhật PNG sau khi sửa source. Dùng [quy tắc chỉnh draw.io](../../DRAWIO_EDITING_RULES.md) và
[drawio-skill](../../.agents/skills/drawio-skill/SKILL.md) của repository.
