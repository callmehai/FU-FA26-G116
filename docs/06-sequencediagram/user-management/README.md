# Sequence Diagram — Quản lý người dùng

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho UC-05 → UC-09.

| Use Case | Source |
|---|---|
| UC-05 View User Account | [UC05_ViewUserAccount.txt](UC05_ViewUserAccount.txt) |
| UC-06a Create User Account | [UC06a_CreateUserAccount.txt](UC06a_CreateUserAccount.txt) |
| UC-06b Update User Account | [UC06b_UpdateUserAccount.txt](UC06b_UpdateUserAccount.txt) |
| UC-06c Deactivate/Reactivate User Account | [UC06c_DeactivateReactivateUserAccount.txt](UC06c_DeactivateReactivateUserAccount.txt) |
| UC-07 Reset User Password | [UC07_ResetUserPassword.txt](UC07_ResetUserPassword.txt) |
| UC-08 Assign System Role to User | [UC08_AssignSystemRoleToUser.txt](UC08_AssignSystemRoleToUser.txt) |
| UC-09 Unlock User Account | [UC09_UnlockUserAccount.txt](UC09_UnlockUserAccount.txt) |

Đọc [convention sequence chung](../README.md), đặc tả UC và Business Rules trước khi sửa. Các thao tác
quản trị tài khoản và gán role cấp hệ thống không đồng nghĩa với quyền trong một cuộc họp. Nội dung
message phải khớp [danh mục System Messages](../../03-api/00-danh-muc-ma-thong-bao.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/user-management`.
Cập nhật đúng tab UC trong file draw.io gộp theo hướng dẫn ở README chung.
