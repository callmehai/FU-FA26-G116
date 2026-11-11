# Sequence Diagram — Quản lý vai trò và nhật ký

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho UC-10 → UC-13.

| Use Case | Source |
|---|---|
| UC-10 View Role Permission Matrix | [UC10_ViewRolePermissionMatrix.txt](UC10_ViewRolePermissionMatrix.txt) |
| UC-11 Configure Role Permissions | [UC11_ConfigureRolePermissions.txt](UC11_ConfigureRolePermissions.txt) |
| UC-12a Create Role | [UC12a_CreateRole.txt](UC12a_CreateRole.txt) |
| UC-12b Update Role | [UC12b_UpdateRole.txt](UC12b_UpdateRole.txt) |
| UC-12c Delete Role | [UC12c_DeleteRole.txt](UC12c_DeleteRole.txt) |
| UC-13 View Audit Log | [UC13_ViewAuditLog.txt](UC13_ViewAuditLog.txt) |

Đọc [convention sequence chung](../README.md), đặc tả UC, Business Rules và ADR liên quan trước khi sửa.
Phân biệt role cấp hệ thống với Meeting Role; nguồn quyết định về role nằm ở [ADR-0008](../../05-decisions/0008-vai-tro-la-du-lieu.md).

**UC-13 (11/11/2026, SCRUM-16 BA Final Review):** sequence đối chiếu với code `main` (PR #311): `MSG469`, `MSG470`,
BR-440 → BR-443, người thực hiện là tài khoản, Khách hoặc hệ thống. Lifeline theo code thật: `:Audit Log Endpoints`
(`AuditLogEndpoints`, Minimal API) → `:Audit Log Query Service` (`AuditLogQueryService`) → `:Database`, **không có**
repository riêng; bộ đọc tham số nằm ở `AuditLogQueryParser` trong endpoints nên kiểm bộ lọc đứng trước lời gọi
service; thiếu quyền trả `403` không mã, hết phiên `401`. Đã xuất lại PNG, tab `uc13_viewauditlog` và ảnh trong
`drawio-png/`. Class diagram FE-03 đã sửa cùng đợt.

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/role-management`.
Cập nhật đúng tab UC trong file draw.io gộp theo hướng dẫn ở README chung.
