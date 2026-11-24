# Sequence Diagram — Quản lý vai trò và nhật ký

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho UC-09 → UC-12.

| Use Case | Source |
|---|---|
| UC-09 View Role Permission Matrix | [UC09_ViewRolePermissionMatrix.txt](UC09_ViewRolePermissionMatrix.txt) |
| UC-10 Configure Role Permissions | [UC10_ConfigureRolePermissions.txt](UC10_ConfigureRolePermissions.txt) |
| UC-11a Create Role | [UC11a_CreateRole.txt](UC11a_CreateRole.txt) |
| UC-11b Update Role | [UC11b_UpdateRole.txt](UC11b_UpdateRole.txt) |
| UC-11c Delete Role | [UC11c_DeleteRole.txt](UC11c_DeleteRole.txt) |
| UC-12 View Audit Log | [UC12_ViewAuditLog.txt](UC12_ViewAuditLog.txt) |

Đọc [convention sequence chung](../README.md), đặc tả UC, Business Rules và ADR liên quan trước khi sửa.
Phân biệt role cấp hệ thống với Meeting Role; nguồn quyết định về role nằm ở [ADR-0008](../../05-decisions/0008-vai-tro-la-du-lieu.md).

**UC-12 (11/11/2026, SCRUM-16 BA Final Review):** sequence đối chiếu với code `main` (PR #311): `MSG469`, `MSG470`,
BR-012, UC-12, người thực hiện là tài khoản, Khách hoặc hệ thống. Lifeline theo code thật: `:Audit Log Endpoints`
(`AuditLogEndpoints`, Minimal API) → `:Audit Log Query Service` (`AuditLogQueryService`) → `:Database`, **không có**
repository riêng; bộ đọc tham số nằm ở `AuditLogQueryParser` trong endpoints nên kiểm bộ lọc đứng trước lời gọi
service; thiếu quyền trả `403` không mã, hết phiên `401`. Đã xuất lại PNG, tab `uc12_viewauditlog` và ảnh trong
`drawio-png/`. Class diagram FE-03 đã sửa cùng đợt.

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/role-management`.
Cập nhật đúng tab UC trong file draw.io gộp theo hướng dẫn ở README chung.

## Cập nhật theo kiến trúc

- **17/11/2026 — CA-19 (SCRUM-184):** service không còn gọi thẳng `:Database` (backend bốn project, [ADR-0064](../../05-decisions/0064-backend-bon-project-clean-architecture.md)); chèn lifeline `:Audit Log Repository` (UC-10, UC-11a/b/c), `:Notification Repository` (UC-10) và `:Audit Log Query` (`IAuditLogQuery`, UC-12: `count`, `findPage`, `findNames`). Message tới `:Database` giữ nguyên nội dung; luồng, nhánh `MSG`/`BR` và actor không đổi.
