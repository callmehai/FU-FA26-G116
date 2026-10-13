# SCRUM-25 — bàn giao Frontend và kiểm thử

Đọc [API contract](08-chuong-trinh-nghi-su.md) và [catalogue](00-danh-muc-ma-thong-bao.md). Backend không tạo type, API wrapper hoặc UI; thành viên FE phụ trách các phần này.

[Báo cáo triển khai và kết quả kiểm thử](08-chuong-trinh-nghi-su-ket-qua-kiem-thu.md) ghi rõ coverage, performance, môi trường và các giới hạn kiểm chứng.

## Tích hợp Frontend

1. Tạo `AgendaPanel(meetingId, variant: 'page' | 'room')` dùng chung ADR-0011; API wrapper dùng `api` hiện có, đường dẫn tương đối. Panel tự GET agenda, không đọc từ response meeting detail.
2. Dùng permissions/actions Backend; không suy quyền theo system role. Phân biệt loading, empty, reload, validation, conflict và load failure. Reload lỗi phải báo snapshot cũ, không giả là dữ liệu mới nhất.
3. 403: mất quyền; 404: NotFound chung; 409: hiển thị message rồi reload. MSG141 phải cập nhật lại field nào còn sửa được. PUT gửi toàn bộ planning fields; field khóa không đổi vẫn hợp lệ.
4. Join `/hubs/meetings` bằng connection helper hiện có, gọi `JoinMeeting(meetingId)` rồi GET. Event `agendaChanged` chỉ là tín hiệu: gộp các refetch dồn, bỏ response cũ đến muộn, không cập nhật item trực tiếp từ payload.
5. Reconnect phải join lại rồi GET; resync khi tab active và mỗi 30 giây khi panel hiển thị. Leave khi đổi meeting/unmount; không ngắt connection dùng chung của tính năng khác.
6. Reorder gửi đúng Pending IDs và orderVersion GET gần nhất. Không renumber. Lịch dự kiến tính cộng dồn duration từ plannedStartTime, gap position không tạo nghỉ. Tổng duration vượt meeting chỉ cảnh báo.
7. Presenter options chỉ internal participant active; tên tự nhập không phải Guest invitation xác minh. Document IDs là Document của model hiện tại; migration MeetingDocument sau này phải xử lý tương thích rõ ràng.
8. Link tài liệu trong room mở tab mới, không rời cuộc gọi. Kiểm giao diện thật ở trang detail và panel 360px theo hai skill frontend trong `.claude/skills/`.

Ví dụ tạo:

```json
{"title":"Báo cáo tiến độ","description":"Nội dung\nThảo luận","plannedDurationMinutes":20,"presenterId":null,"presenterName":"Báo cáo viên","documentIds":[]}
```

Ví dụ thành công:

```json
{"code":"MSG61","message":"Cập nhật mục nghị sự thành công.","meetingId":"<meeting-id>","agendaItemId":"<item-id>"}
```

## Fixture Development

Seeder insert-only chạy sau DevelopmentDataSeeder khi `Database:MigrateAndSeedOnStartup=true`. Không reset meeting/item đã có, kể cả người dùng đã sửa/xóa item. Fixture metadata tài liệu không có file thật; không dùng nó để chứng minh download/upload.

| Biến Postman | ID / nguồn |
|---|---|
| planningMeetingId | `25000000-0000-7000-8000-000000000001` |
| runningMeetingId | `25000000-0000-7000-8000-000000000002` |
| endedMeetingId | `25000000-0000-7000-8000-000000000003` |
| meetingId | Ban đầu đặt bằng planningMeetingId |
| itemId, documentId, presenterId | Lấy từ GET agenda và editor-options |
| foreignDocumentId | Document của meeting khác trong ba fixture |

Tài khoản `thuky@trustcabinet.local`, `chutoa@trustcabinet.local`, `thanhvien@trustcabinet.local`; `quantri@trustcabinet.local` là outsider chưa phân công. Mật khẩu demo theo DevelopmentDataSeeder/tài liệu auth hiện hành. Chỉ dùng tài khoản demo trong Development. Token và mật khẩu để biến local, không export/commit secrets.

## Kiểm thử thủ công

1. Cấu hình PostgreSQL, Redis và user-secrets theo [database](../02-design/03-co-so-du-lieu.md), [auth](01-xac-thuc.md). Chạy `dotnet run --project backend/TrustCabinet.Web`, kiểm `http://localhost:5225/api/health`.
2. Import [Postman collection](postman/SCRUM-25-Agenda.postman_collection.json), đặt baseUrl `http://localhost:5225`. Login Secretary, Chair, Member, Admin; lưu accessToken vào các biến token tương ứng.
3. Secretary GET agenda/options; tạo không presenter, chọn presenterId, hoặc tên tự nhập; gắn document chưa được dùng. Mong 201/MSG60.
4. Thử title trắng, duration 0/thập phân, document foreign/trùng/đã gắn item khác: 400/MSG66 và errors. GET kiểm không có item dở dang.
5. Tạo ba item, xóa giữa, tạo tiếp. Vị trí giữ gap; mới luôn max+1. Xóa giữ Document và DocumentVersion.
6. GET lấy orderVersion; đảo Pending IDs bằng PUT order. Gửi lại token cũ sau thay đổi: 409/MSG143; không renumber fixed positions.
7. Đổi meetingId sang runningMeetingId. Chair start Pending đầu tiên: MSG64; start item khác khi có current: MSG72; start item không đầu khi chưa có current: MSG144.
8. Secretary sửa title/description current bằng đầy đủ giá trị planning không đổi: MSG61. Đổi duration/document/presenter: MSG141, không lưu một phần. Xóa current: MSG142.
9. Chair complete ngay: MSG65, Done, duration từ actual timestamps, meeting vẫn InProgress và item kế vẫn Pending. Complete lần nữa: MSG74.
10. Member thử planning: 403; Admin outsider GET: 404; foreign item: 404 body rỗng. Meeting Ended: planning 409/MSG67.
11. Realtime: dùng [client Node](postman/agenda-realtime.mjs) ở hai terminal; join rồi GET. Thao tác một bên, bên kia thấy agendaChanged và snapshot đã commit. Thử leave, reconnect. Thu hồi quyền cần writer SCRUM-20 hoặc automated fixture; không sửa database dùng chung để mô phỏng sự cố.
12. Kiểm tra database chỉ bằng truy vấn đọc nếu cần. Lỗi persistence, khóa và race do automated test chứng minh; không dừng database dùng chung.

Ví dụ PowerShell:

```powershell
$env:AGENDA_BASE_URL = 'http://localhost:5225'
$env:AGENDA_MEETING_ID = '25000000-0000-7000-8000-000000000002'
# Set AGENDA_TOKEN locally to a current access token; never commit it.
node docs/03-api/postman/agenda-realtime.mjs
```

## Dependency và ghi chú Jira/BA cần người phụ trách đăng

- Chỉ hoàn thành Backend; không đánh dấu SCRUM-25 Done trước FE → BA Final Review → Tester → Review.
- Không needsVote, không cấp Agenda qua system Admin; presenter tên tự nhập không tạo user/guest. Dùng UC/BR/sequence mới thay các dòng Screen Inventory cũ còn nói Draft/Rejected, needsVote hoặc Meeting Session.
- Document association tạm theo Document.MeetingId/AgendaItemId qua adapter riêng; không migration MeetingDocument trong task này.
- Reorder thêm token SHA-256 chống ghi đè snapshot cũ; vị trí các mục đã bắt đầu giữ nguyên.
- SCRUM-20 phải cập nhật MeetingReadPolicy và AgendaRules.CurrentParticipant khi có Current/Removed. Writer participant/document phải dùng cùng meeting lock.
- SCRUM-69 dùng `IAgendaMeetingEnd.PrepareAsync` trong transaction caller đã khóa meeting; helper không SaveChanges/Commit/publish. Sau commit caller phát change trả về qua IAgendaChangePublisher và đóng room. Không có current trả null; nếu vẫn cần báo MeetingEnded, caller tạo event không item. Test helper không phải test endpoint End Meeting.
- Bản đối chiếu class diagram: Controller = AgendaEndpoints; service/repository theo ba project hiện hữu, reuse IMeetingRepository; không nhân bản repository chỉ để giống tên lifeline.
- Các ghi chú trên **chưa được đăng Jira**, theo phạm vi người dùng giao.

## Rollout

Migration `AgendaExecutionInvariants` thêm check ba trạng thái và unique partial index một InProgress/meeting; không thêm duration, session hoặc bảng document. Dừng nếu legacy có status khác hoặc nhiều current. Người phụ trách phải xác minh lịch sử rồi sửa bằng migration riêng có review; không tự map Postponed. Áp migration trước triển khai writer mới, tránh writer cũ không theo meeting lock. Accepted ADR không bị sửa.
