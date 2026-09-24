# SCRUM-14 · Thông báo trong ứng dụng

Backend cung cấp danh sách thông báo theo đúng tài khoản đã đăng nhập, trạng thái đã đọc và
SignalR. Đây là UC-18–UC-21 trong class diagram Notification Management. Các endpoint yêu cầu
Bearer token; tài khoản đã bị vô hiệu hóa nhận `403`. ID của người nhận luôn lấy từ token,
không lấy từ request.

## REST API

| Method | Path | Kết quả |
|---|---|---|
| `GET` | `/api/notifications?limit=50&offset=0` | `{ items, unreadCount, totalCount, limit, offset }`; mới nhất trước, `limit` từ 1 đến 100 |
| `GET` | `/api/notifications/unread-count` | `{ unreadCount }` cho nút chuông |
| `POST` | `/api/notifications/{id}/read` | Notification đã đọc; gọi lại giữ `readAt` cũ |
| `POST` | `/api/notifications/read-all` | `{ markedCount, unreadCount: 0 }` |
| `GET` | `/api/notifications/{id}/target` | `{ targetType: "Meeting", targetId, url }` khi còn quyền xem meeting |

Mỗi item có `id`, `type`, `title`, `body`, `meetingId`, `targetType`, `targetId`,
`actionUrl`, `isRead`, `readAt`, `createdAt`. Với thông báo họp, `targetType` là `Meeting`,
`targetId` là ID cuộc họp và `actionUrl` là `/mgmt/meetings/{id}`. FE chỉ dùng
`GET /{id}/target` để mở đích: endpoint kiểm tra lại quyền và chỉ trả URL khi còn hợp lệ.
`isRead` được tính từ `readAt`; `Notification.MarkAsRead` giữ nguyên thời điểm đọc khi gọi lặp.
`NotificationService` xử lý nghiệp vụ và `NotificationRepository` đọc/ghi các dòng theo người nhận.
Thiếu token trả `401`. Đọc/sửa thông báo của người khác hoặc mở meeting không còn quyền xem
trả `404`, không tiết lộ dòng dữ liệu có tồn tại. URL meeting hiện là
`/mgmt/meetings/{id}`; API chi tiết meeting khi được triển khai vẫn phải tự kiểm tra quyền.
Thông báo chưa có đích hợp lệ cũng trả `404` ở endpoint `target`.

## SignalR

Hub: `/hubs/notifications`, yêu cầu JWT và chỉ chấp nhận `access_token` trong query string ở
đúng path hub khi WebSocket không gửi được header. Hub lấy user ID từ claim `sub` đã xác thực,
kiểm tra tài khoản còn hoạt động và không cung cấp method để client gửi thông báo tùy ý.

Server gửi sự kiện `notificationReceived` **chỉ đến người nhận**:

```json
{
  "notification": {
    "id": "<uuid>",
    "type": "MeetingStartingSoon",
    "title": "Cuộc họp sắp bắt đầu",
    "body": "...",
    "meetingId": "<uuid>",
    "targetType": "Meeting",
    "targetId": "<uuid>",
    "actionUrl": "/mgmt/meetings/<uuid>",
    "isRead": false,
    "readAt": null,
    "createdAt": "<ISO 8601>"
  },
  "unreadCount": 3
}
```

Worker đọc notification đã commit rồi mới push. Realtime là gửi ít nhất một lần: nếu worker
ngừng sau khi push nhưng trước khi ghi `pushed_at`, client có thể nhận lại cùng `id`; FE cần
khử trùng theo `id`. Khi user không online, lần `GET /api/notifications` sau vẫn đọc được dữ
liệu đã lưu. Nút chuông và panel trong `AppShell.tsx` là phần FE tích hợp sau khi API sẵn sàng.

## Nguồn tạo thông báo

- **Được mời họp:** theo quyết định BA cho ticket này, gọi
  `NotificationService.QueueParticipantInvitedAsync` khi thêm participant vào meeting đã
  `Published`. Thêm notification trong cùng `SaveChangesAsync` với participant. Backend chưa có
  endpoint thêm participant, nên điểm gọi này thuộc luồng cuộc họp sau.
- **Sắp bắt đầu:** worker kiểm tra meeting `Published` mỗi 5 giây và xếp một thông báo cho mỗi
  participant còn hoạt động khi còn tối đa **15 phút** trước giờ bắt đầu. Unique key ngăn xếp
  lặp qua nhiều vòng quét.
- **Được giao nhiệm vụ:** phạm vi đã chốt của TrustCabinet không có task/action item (xem
  [ERD](../02-design/02-erd.md)); vì vậy chưa có nguồn hay URL đích cho loại thông báo này.
  Cần thay đổi phạm vi ở Jira/BA trước khi triển khai.

## Kiểm thử Postman

Trong Postman, đặt `baseUrl=http://localhost:5225` và đăng nhập bằng tài khoản Development;
dùng access token nhận được làm Bearer token cho các request bên dưới. Với
database Development mới, seeder tạo hai notification của tài khoản thành viên để thử đọc.
Nếu database Development đã có user từ trước, seeder không thêm hai dòng này. Có thể thêm một
notification thử vào database **Development** bằng SQL sau trong pgAdmin hoặc `psql`, rồi xóa theo
`deduplication_key` sau khi thử. Đây chỉ là dữ liệu thử, không phải thay đổi schema:

```sql
INSERT INTO notifications
    (id, user_id, type, title, body, meeting_id, target_type, target_id,
     action_url, created_at, updated_at, deduplication_key)
SELECT gen_random_uuid(), u.id, 'MeetingInvited', 'Thử Postman',
       'Thông báo thử cho SCRUM-14', m.id, 'Meeting', m.id,
       '/mgmt/meetings/' || m.id::text, now(), now(), 'manual:scrum-14'
FROM users u
JOIN meeting_participants p ON p.user_id = u.id
JOIN meetings m ON m.id = p.meeting_id
WHERE u.email = 'thanhvien@trustcabinet.local' AND m.status = 'Published'
LIMIT 1
ON CONFLICT (deduplication_key) DO NOTHING;

-- Sau khi kiểm thử:
DELETE FROM notifications WHERE deduplication_key = 'manual:scrum-14';
```

Chạy câu `DELETE` riêng **sau** các request Postman, không chạy cùng câu `INSERT`.

1. Đăng nhập qua `/api/auth/login`, đặt access token làm Bearer token trong Postman.
2. Gọi `GET /api/notifications` và `/unread-count`; xác nhận số chưa đọc bằng số item có
   `readAt=null` trong DB. Item thông báo họp phải có `targetType=Meeting`, `targetId=meetingId`
   và `actionUrl=/mgmt/meetings/{meetingId}`. `limit=0` phải trả `400`.
3. Gọi `POST /{id}/read` hai lần; `readAt` không thay đổi ở lần hai. Gọi `/read-all` và kiểm tra
   `unreadCount=0`.
4. Đăng nhập bằng user khác; `POST /{id}/read` và `GET /{id}/target` với ID của user đầu phải
   trả `404`. Không có token phải trả `401`.
5. Với thông báo meeting hợp lệ, gọi `GET /{id}/target`; kiểm tra `targetId` và URL. Với meeting
   mà user không được xem, phải trả `404`.
6. Dùng SignalR client kết nối hub, tạo notification qua luồng nghiệp vụ hoặc chờ lịch nhắc;
   client nhận `notificationReceived` mà không reload. Postman HTTP thuần chỉ kiểm được bước
   negotiate và các REST endpoint; muốn nhận sự kiện hub cần SignalR client hỗ trợ protocol.

Trong lúc luồng thêm participant chưa có API, test tự động tạo notification trực tiếp qua
`NotificationService`; không mở endpoint gửi thông báo tùy ý để phục vụ test.
