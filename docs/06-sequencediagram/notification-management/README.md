# Sequence Diagram — Quản lý thông báo

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho UC-76, UC-77a, UC-77b, UC-78.

| Use Case | Source |
|---|---|
| UC-76 View Notifications | [UC76_ViewNotifications.txt](UC76_ViewNotifications.txt) |
| UC-77a Mark Notification as Read | [UC77a_MarkNotificationAsRead.txt](UC77a_MarkNotificationAsRead.txt) |
| UC-77b Mark All Notifications as Read | [UC77b_MarkAllNotificationsAsRead.txt](UC77b_MarkAllNotificationsAsRead.txt) |
| UC-78 Open Notification Target | [UC78_OpenNotificationTarget.txt](UC78_OpenNotificationTarget.txt) |

Đọc [convention sequence chung](../README.md), đặc tả UC và BR mà từng source tham chiếu. Với message
`MSGnn`, dùng câu chữ và mã từ [danh mục System Messages](../../03-api/00-danh-muc-ma-thong-bao.md).

Sau khi sửa source, xuất lại ảnh bằng `node scripts/export-diagrams.js docs/06-sequencediagram/notification-management`.
Cập nhật đúng tab UC trong file draw.io gộp theo hướng dẫn ở README chung.
