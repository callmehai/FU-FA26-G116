# SCRUM-14 · Thông báo trong ứng dụng

> **Một cuộc họp là một buổi họp ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).** Từ migration
> `MeetingOwnsScheduleDropSessions`, lịch nằm trên Meeting (`plannedStartTime`, `plannedEndTime`, `onlineLink`) và code
> không còn `MeetingSession`. Các đoạn lịch sử bên dưới nhắc Meeting Session là mô tả thời điểm viết.
> Số UC đã đổi theo baseline 70 UC ([bảng đổi số](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới));
> các mã `BR-54` → `BR-61` nhắc trong file là số BR cũ đã ngừng dùng ([danh mục BR](../01-requirements/06-business-rules.md)).

Backend cung cấp danh sách thông báo theo đúng tài khoản đã đăng nhập, trạng thái đã đọc và
SignalR. Đây là UC-17–UC-20 trong class diagram Notification Management. Các endpoint yêu cầu
Bearer token; tài khoản đã bị vô hiệu hóa nhận `403`. ID của người nhận luôn lấy từ token,
không lấy từ request.

## REST API

| Method | Path | Kết quả |
|---|---|---|
| `GET` | `/api/notifications?page=1&pageSize=20` | `{ items, unreadCount, totalCount, page, pageSize, totalPages }`; mới nhất trước, `pageSize` từ 1 đến 100 |
| `GET` | `/api/notifications/unread-count` | `{ unreadCount }` cho nút chuông |
| `POST` | `/api/notifications/{id}/read` | Notification đã đọc; gọi lại giữ `readAt` cũ |
| `POST` | `/api/notifications/read-all` | `{ markedCount, unreadCount: 0 }` |
| `GET` | `/api/notifications/{id}/target` | `{ targetType: "Meeting", targetId, url }` khi còn quyền xem meeting |

Mỗi item có `id`, `type`, `title`, `body`, `meetingId`, `targetType`, `targetId`,
`actionUrl`, `isRead`, `readAt`, `createdAt`. Với thông báo họp, `targetType` là `Meeting`,
`targetId` là ID cuộc họp và `actionUrl` là `/mgmt/meetings/{id}`. `actionUrl` được dựng khi trả
response từ `targetType`/`targetId`, không được lưu trong database. FE chỉ dùng
`GET /{id}/target` để mở đích: endpoint kiểm tra lại quyền và chỉ trả URL khi còn hợp lệ.
`isRead` được tính từ `readAt`; `Notification.MarkAsRead` giữ nguyên thời điểm đọc khi gọi lặp.
`NotificationService` xử lý nghiệp vụ và `NotificationRepository` đọc/ghi các dòng theo người nhận.
Thiếu token trả `401`. Đọc/sửa thông báo của người khác hoặc mở meeting không còn quyền xem
trả `404`, không tiết lộ dòng dữ liệu có tồn tại. URL meeting hiện là
`/mgmt/meetings/{id}`; `GET /api/meetings/{id}` đã được triển khai và tự kiểm tra quyền.
Thông báo chưa có đích hợp lệ cũng trả `404` ở endpoint `target`.

## Ghi chú đối chiếu UC Description và sequence diagram

Phần này ghi lại chênh lệch giữa `UC list.docx` do BA cung cấp, bốn sequence
diagram Notification Management và backend hiện tại ngày 28/09/2026. Đây là gap cần
BA/BE thống nhất; không được coi hành vi trong UC Description hoặc diagram là đã có
nếu code chưa thực hiện.

### So với sequence diagram UC-17–UC-20

| UC | Sequence diagram | Backend hiện tại | Chênh lệch / cần chốt |
|---|---|---|---|
| UC-17 · Xem thông báo | Trả danh sách và số chưa đọc mà không đổi trạng thái đã đọc. | `GET /api/notifications` đúng hành vi này, nhưng service thực hiện ba truy vấn riêng cho danh sách, `unreadCount` và `totalCount`; API dùng `page`/`pageSize` thống nhất với danh sách cuộc họp. | Kết quả nghiệp vụ khớp; phân trang và `totalCount` là phần mở rộng không thể hiện trong diagram. |
| UC-18 · Đánh dấu đã đọc | Sau khi lưu, repository lấy `unreadCount` mới và controller trả notification cùng số chưa đọc. | `POST /api/notifications/{id}/read` trả riêng `NotificationResponse`; client phải gọi thêm `/unread-count`. Thao tác gọi lặp vẫn giữ `readAt` cũ. | Thiếu `unreadCount` trong response so với diagram. Cần chốt sửa contract hay sửa diagram. |
| UC-19 · Đánh dấu tất cả đã đọc | Cả nhánh không có dòng chưa đọc và nhánh thành công đều trả danh sách notification mới nhất cùng `unreadCount`. | `POST /api/notifications/read-all` chỉ trả `{ markedCount, unreadCount: 0 }`; không đọc lại hay trả danh sách. | Response gọn hơn diagram và có thêm `markedCount`. Cần chốt FE tự cập nhật danh sách hay backend trả lại toàn bộ page. |
| UC-20 · Mở đích | Nếu notification chưa đọc thì đánh dấu đã đọc trước, sau đó kiểm tra quyền hiện tại và trả đích meeting hoặc task. | `GET /api/notifications/{id}/target` chỉ kiểm tra và trả đích; không thay đổi `ReadAt`. Backend chỉ hỗ trợ `Meeting`, vì task/action item nằm ngoài phạm vi. | Khác diagram ở bước tự đánh dấu đã đọc. Nếu giữ endpoint `GET` không side effect thì phải sửa diagram; nếu bám diagram thì cần một contract có mutation rõ ràng. |

### So với class diagram Notification Management

| Class diagram | Code hiện tại | Thay đổi / cải tiến |
|---|---|---|
| `NotificationController` gọi `NotificationService`. | ASP.NET Minimal API ánh xạ thành `NotificationEndpoints`; mọi route dùng `NotificationService` và lấy `userId` từ claim đã xác thực. | Không có controller instance như UML, nhưng ranh giới Web → Service vẫn được giữ. Caller không thể truyền `recipientUserId` để đọc dữ liệu người khác. |
| `NotificationService` trả danh sách, số chưa đọc, đánh dấu đã đọc và mở target. | `INotificationService` trả `NotificationPage`, nhận `page`/`pageSize`, trả `markedCount` khi mark-all và tách `GetTargetAsync` khỏi mutation read status. | Bổ sung phân trang và dữ liệu phục vụ FE. Việc tách target thành `GET` không side effect an toàn hơn về HTTP, nhưng chưa khớp UC-20/BR-53. |
| `NotificationRepository` có các thao tác theo recipient và notification ID. | Repository luôn ghép `notificationId` với `userId`, có thêm total count, truy vấn reminder, deduplication và hàng đợi realtime. | Bảo vệ ownership ngay trong query, hỗ trợ phân trang, retry an toàn và không tạo trùng notification. `MarkAllUnreadAsReadAsync` trả số dòng đã đổi thay vì `void`. |
| `Notification` có `notificationId`, `recipientUserId`, `kind`, target, `actionUrl`, `readAt`, `createdAt`. | `Id`, `UserId`, `Type` dùng enum, `TargetType`, `TargetId`, `ReadAt`, `CreatedAt`; có thêm `Body`, `MeetingId`, `UpdatedAt`, `DeduplicationKey`, `PushedAt`. `ActionUrl` chỉ có trong response và `IsRead` là giá trị suy ra. | Không ghim route UI vào database. `ReadAt` là nguồn sự thật duy nhất; gọi lặp `MarkAsRead` không làm mất thời điểm đọc đầu tiên. |
| `User` có `username`, `email`, `status` và quan hện 1–n với notification. | Domain dùng `FullName`, `Email`, `IsActive`; FK `Notification.UserId` là `Restrict`. | UML đang dùng tên field không khớp domain model đã chốt; quan hệ người nhận vẫn khớp. |
| `NotificationPanel` hiển thị danh sách, unread badge, lỗi và thao tác mở/đánh dấu. | `features/notifications/`: `NotificationBell` (nút chuông + badge trên app bar), `NotificationPanel` (danh sách) và `NotificationsProvider` (trạng thái + SignalR client). | Tách thêm provider để badge, danh sách và kết nối hub sống qua việc đổi workspace. Chi tiết ở [Hợp đồng tích hợp FE](#hợp-đồng-tích-hợp-fe). |

Các cải tiến kỹ thuật không thể hiện trong class diagram gồm SignalR chỉ gửi đúng
recipient, kiểm tra lại tài khoản active khi kết nối hub, lưu database trước khi push,
khử trùng bằng `DeduplicationKey`, timestamp `PushedAt`, index cho truy vấn unread/pending và
`TimeProvider` để test thời gian xác định. Khi mở target, backend kiểm tra quyền hiện tại qua
`IMeetingRepository.FindAccessibleAsync`; URL chỉ được dựng từ target hợp lệ ở lớp Web.

### So với UC Description và Business Rules

| Nguồn yêu cầu | Backend hiện tại | Kết quả đối chiếu |
|---|---|---|
| UC-17 yêu cầu hiển thị notification của chính user, chỉ báo chưa đọc và không đổi read status khi mở danh sách (BR-47–BR-49). | Query luôn lọc theo `UserId`; `ListAsync` chỉ đọc dữ liệu và trả `unreadCount`. | Khớp yêu cầu chính. EX-1 chưa có mã/message API riêng khi không tải được danh sách. |
| UC-18 yêu cầu chỉ sửa notification thuộc user, hiển thị trạng thái đã đọc và `unreadCount` mới. EX-1 yêu cầu thông báo “This notification is no longer available”; EX-2 yêu cầu trả trạng thái thực lưu và số chưa đọc khi lưu lỗi. | Ownership và idempotency đã có. Endpoint thành công không trả `unreadCount`; notification không tồn tại/của user khác trả `404` rỗng; lỗi lưu không có response nghiệp vụ. | Mới khớp phần thay đổi read status và bảo vệ ownership; response thành công và hai nhánh lỗi chưa đủ theo UC Description. |
| UC-19/BR-51 yêu cầu đánh dấu mọi notification chưa đọc của user, kể cả dòng không nằm trên page hiện tại; sau đó hiển thị trạng thái và số chưa đọc thực lưu. | Repository cập nhật tất cả dòng `UserId` có `ReadAt == null`, không phụ thuộc page. Response luôn ghi `unreadCount: 0` và không đọc lại danh sách/trạng thái. | Phạm vi update khớp BR-51. AF-1 và EX-1 chưa trả current list/current saved state như UC Description. |
| UC-20/BR-53 yêu cầu khi chọn notification thì thử đánh dấu đã đọc; nếu lưu read status thất bại vẫn tiếp tục mở target nếu user có quyền. | `GetTargetAsync` không thử lưu `ReadAt`. FE gọi `POST /{id}/read` (không chờ, bỏ qua lỗi) rồi mới gọi `GET /{id}/target`. | Đáp ứng main flow, POST-2 và EX-3 ở mức hệ thống bằng hai lời gọi; sequence diagram vẫn vẽ một lời gọi có side effect nên cần sửa theo. |
| BR-45 quy định ba nguồn: được mời họp, meeting sắp bắt đầu, và được giao task. | Nhắc sắp bắt đầu đã chạy qua worker. Nguồn lời mời chờ luồng publish meeting; notification giao task không có. | Mới có một nguồn hoàn chỉnh. Yêu cầu task xung đột với phạm vi repo đã loại task/action item; BA phải chốt nguồn nào ưu tiên. |
| UC-16 và BR-37–BR-40 quy định sau khi meeting chuyển thành công từ `Approved` sang `Published`, hệ thống xếp invitation delivery bất đồng bộ cho **tất cả** participant; lỗi delivery phải retry và không rollback `Published`. | Publish (SCRUM-19) tạo notification lời mời cho participant active, chưa Delegated, trừ người Publish, cùng transaction; email thư mời xếp hàng sau commit, retry và không rollback `Published`. | Đã nối. Lệch spec: người Publish không nhận notification in-app (quyết định team 16/10/2026), BA chốt ở BA Final Review. |
| BR-42 yêu cầu invitation notification có link trực tiếp tới meeting đã publish và tài liệu đính kèm. | Notification có link chi tiết meeting; không có target/link riêng cho tài liệu. | Đã có link meeting; cần BA chốt tài liệu được mở bên trong trang meeting hay cần contract target khác. |
| BR-43 yêu cầu theo dõi việc xếp hàng và trạng thái delivery của tất cả invitation message cho audit. | Notification chỉ có `PushedAt` và log lỗi chung của worker; không có attempt count, trạng thái pending/failed hay audit record theo lần delivery. | Chưa đủ dữ liệu để đáp ứng BR-43 và retry có kiểm soát của BR-40. |
| BR-46 yêu cầu lưu notification để xem sau và cập nhật realtime notification cùng unread count khi recipient đang kết nối. | Notification được lưu trước khi worker push `notificationReceived` kèm `unreadCount` qua SignalR. | Khớp yêu cầu; FE nhận sự kiện hub, khử trùng theo `id` và cập nhật badge cùng danh sách. |

`UC list.docx` còn có hai hệ mã song song: danh mục tổng hợp gọi Notification
Management là `UC-031` (gộp `UC-NOTI-01`, `UC-NOTI-02`, `UC-NOTI-03`, `UC-NOTI-08`), trong
khi phần đặc tả chi tiết dùng `UC-17`–`UC-20`. Repo hiện dùng hệ mã chi tiết
`UC-17`–`UC-20` và story `SCRUM-14`; BA cần chốt mapping traceability chính thức.

Query phân trang sai trả `400` với `code=MSG39` và lỗi database khi kiểm tra
tài khoản active trả `500` với `code=MSG40`, thống nhất status với API danh sách
cuộc họp. Các nhánh lỗi lưu trạng thái đọc
vẫn chưa trả trạng thái đã lưu thực tế hay `unreadCount` như diagram.

Bước “kiểm tra quyền hiện tại” của UC-20 dùng chung
`IMeetingRepository.FindAccessibleAsync` với API chi tiết meeting theo BR-29: creator đọc mọi trạng thái,
assigned approver đọc từ `WaitingApproval`, và người được gán vai trò `Chair`, `Secretary`, `Member` hoặc
`Guest` đọc từ `Published`. Trạng thái lời mời không thu hồi quyền xem meeting đã công bố; notification
không tự cấp quyền. Quyền đọc tài liệu và recording tiếp tục theo các rule riêng.

Những phần backend có thêm ngoài bốn diagram gồm endpoint đếm riêng, phân trang,
`totalCount`, SignalR, worker nhắc meeting sắp bắt đầu, `deduplicationKey` và trạng thái
`pushedAt`. Đây là phần mở rộng, không mâu thuẫn với luồng UC-17–UC-20.

## SignalR

Hub: `/hubs/notifications`, yêu cầu JWT và chỉ chấp nhận `access_token` trong query string ở
đúng path hub khi WebSocket không gửi được header. Hub lấy user ID từ claim `sub` đã xác thực,
kiểm tra tài khoản còn hoạt động và không cung cấp method để client gửi thông báo tùy ý. Server
đóng kết nối hub khi access token hết hạn; client phải lấy token mới rồi kết nối lại.

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
liệu đã lưu. FE khử trùng theo `id` và đọc lại số chưa đọc mỗi khi kết nối lại — xem
[Hợp đồng tích hợp FE](#hợp-đồng-tích-hợp-fe).

## Hợp đồng tích hợp FE

Chuông thông báo nằm trên app bar, dùng chung cho cả hai workspace. Code ở
`frontend/src/features/notifications/`, gọi API qua `src/api/notifications.ts` và hub qua
`src/api/hubs.ts`.

- **Một kết nối cho mỗi phiên đăng nhập.** `NotificationsProvider` nằm trong `RequireAuth`, phía
  trên shell của cả hai workspace: chuyển giữa Không gian làm việc và Lịch làm việc giữ nguyên kết
  nối, badge và danh sách đã tải. Provider gắn `key` theo user, nên tài khoản khác đăng nhập không
  bao giờ thấy danh sách của tài khoản trước. Mọi route cần đăng nhập phải nằm dưới `RequireAuth`;
  một route nằm ngoài (kể cả route chỉ để chuyển hướng) làm provider mount lại và mở kết nối mới.
- **Badge.** Lấy từ `/unread-count` khi vào app, sau đó theo `unreadCount` của mỗi sự kiện hub và
  mỗi response danh sách. Ẩn khi bằng `0`, hiển thị `99+` khi lớn hơn 99. Nhãn của nút chuông đọc
  kèm số chưa đọc cho trình đọc màn hình.
- **Danh sách.** Mỗi lần mở panel đọc lại trang 1 (`pageSize=20`) và giữ danh sách cũ trên màn hình
  trong lúc đọc. "Xem thông báo cũ hơn" tải trang kế tiếp và bỏ dòng trùng `id`, vì thông báo mới
  đến làm các dòng cũ dịch sang trang sau. Mỗi loại thông báo có icon và màu riêng; loại FE chưa
  biết dùng icon chuông, không lỗi.
- **Đánh dấu đã đọc.** Một dòng: cập nhật ngay (optimistic), trả lại trạng thái cũ và báo lỗi nếu
  backend từ chối. Tất cả: mọi dòng đã tải chuyển sang đã đọc, số chưa đọc lấy theo response
  (`unreadCount: 0`) — đúng BR-51, vì backend đánh dấu cả những dòng FE chưa tải.
- **Mở thông báo** (UC-20): gọi `POST /{id}/read` nhưng không chờ, rồi `GET /{id}/target` và đi tới
  `url`. `404` hiện toast "Thông báo này không còn khả dụng." (UC-18 EX-1). Thông báo không có
  `targetType` chỉ được đánh dấu đã đọc, không gọi `target`. FE chỉ điều hướng tới đường dẫn trong
  app (bắt đầu bằng `/` nhưng không phải `//`), kể cả khi dữ liệu trong database sai. Đang ở đúng
  trang chi tiết của cuộc họp đó thì trang không mount lại, nên FE đọc lại cuộc họp và lịch sử trạng
  thái, không làm trống màn hình.
- **Realtime.** Sự kiện `notificationReceived` được khử trùng theo `id`. Panel đang đóng: chuông
  rung và hiện toast tiêu đề kèm nội dung. Panel đang mở: dòng mới hiện ở đầu danh sách. Sự kiện
  phát trong lúc mất kết nối không được gửi lại, nên mỗi lần kết nối lại, khi tab hiện lại và khi
  có mạng trở lại, FE đọc lại số chưa đọc (và trang 1 nếu danh sách đã tải).
- **Kết nối lại.** Thử lại sau 0, 2, 5, 10 giây rồi cứ 30 giây một lần, không bỏ cuộc. Token được
  lấy mới ở mỗi lần kết nối, nên khi server đóng kết nối vì token hết hạn, lần kết nối sau mang
  token đã refresh.
- **Lỗi.** Không tải được lần đầu: hiện `message` của backend (`MSG40`) cùng nút "Thử lại". Không
  làm mới được khi đã có danh sách: giữ danh sách, hiện một dòng báo lỗi kèm "Thử lại". Không tải
  được trang sau: báo ở cuối panel. FE luôn gửi `page`/`pageSize` hợp lệ nên không chờ `MSG39`.
- **Bàn phím và màn hình nhỏ.** `Escape` hoặc bấm ra ngoài đóng panel; `Escape` trả focus về nút
  chuông. Dưới 480px panel chiếm hết bề ngang, nút "Đánh dấu tất cả đã đọc" chỉ còn icon, và nút
  đánh dấu từng dòng luôn hiện trên thiết bị cảm ứng.

## Nguồn tạo thông báo

- **Luồng duyệt (SCRUM-19):** gửi duyệt tạo `MeetingWaitingApproval` cho người duyệt; duyệt tạo
  `MeetingApproved`, từ chối tạo `MeetingRejected` (body kèm lý do) cho người tổ chức.
- **Được mời họp (`MeetingInvited`):** luồng Publish của SCRUM-19 tạo một thông báo cho mỗi
  participant active, chưa Delegated, **trừ chính người Publish**, cùng transaction với thay đổi
  trạng thái. Email thư mời (SCRUM-15) vẫn gửi cho mọi participant active, kể cả người Publish.
- **Sắp bắt đầu:** worker kiểm tra meeting `Published` mỗi 5 giây và xếp một thông báo cho mỗi
  participant còn hoạt động khi còn tối đa **15 phút** trước `plannedStartTime`. Unique key
  `meeting-starting-soon:{meetingId}:{plannedStart unix seconds}:{userId}` ngăn xếp lặp; cuộc họp bị
  dời lịch có giờ mới nên được nhắc lại.
- **Dời lịch (`MeetingRescheduled`):** PUT dời lịch meeting `Published` (UC-15 AF-1) tạo một thông
  báo cho mỗi participant active, chưa Delegated, cùng transaction với thay đổi lịch (BR-76).
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
     created_at, updated_at, deduplication_key)
SELECT gen_random_uuid(), u.id, 'MeetingInvited', 'Thử Postman',
       'Thông báo thử cho SCRUM-14', m.id, 'Meeting', m.id,
       now(), now(), 'manual:scrum-14'
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
   và `actionUrl=/mgmt/meetings/{meetingId}`. `pageSize=0` phải trả `400` cùng
   `code=MSG39`.
3. Gọi `POST /{id}/read` hai lần; `readAt` không thay đổi ở lần hai. Gọi `/read-all` và kiểm tra
   `unreadCount=0`.
4. Đăng nhập bằng user khác; `POST /{id}/read` và `GET /{id}/target` với ID của user đầu phải
   trả `404`. Không có token phải trả `401`.
5. Với thông báo meeting hợp lệ, gọi `GET /{id}/target`; kiểm tra `targetId` và URL. Với meeting
   mà user không được xem, phải trả `404`.
6. Dùng SignalR client kết nối hub, tạo notification qua luồng nghiệp vụ hoặc chờ lịch nhắc;
   client nhận `notificationReceived` mà không reload. Postman HTTP thuần chỉ kiểm được bước
   negotiate và các REST endpoint; muốn nhận sự kiện hub cần SignalR client hỗ trợ protocol.

Test tự động tạo fixture notification trực tiếp trong database in-memory; không mở endpoint gửi
thông báo tùy ý để phục vụ test.
