# Sequence Diagram — Quản lý thông báo

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho UC-66, UC-67a, UC-67b và UC-68.

| Use Case | Source |
|---|---|
| UC-66 View Notifications | [UC66_ViewNotifications.txt](UC66_ViewNotifications.txt) |
| UC-67a Mark Notification as Read | [UC67a_MarkNotificationAsRead.txt](UC67a_MarkNotificationAsRead.txt) |
| UC-67b Mark All Notifications as Read | [UC67b_MarkAllNotificationsAsRead.txt](UC67b_MarkAllNotificationsAsRead.txt) |
| UC-68 Open Notification Target | [UC68_OpenNotificationTarget.txt](UC68_OpenNotificationTarget.txt) |

Đọc [convention sequence chung](../README.md), đặc tả UC và BR mà từng source tham chiếu. Với message
`MSGnn`, dùng câu chữ và mã từ [danh mục System Messages](../../03-api/00-danh-muc-ma-thong-bao.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/notification-management`.
Cập nhật đúng tab UC trong file draw.io gộp theo hướng dẫn ở README chung.
