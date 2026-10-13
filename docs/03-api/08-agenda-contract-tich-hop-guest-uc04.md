# SCRUM-25 — contract tích hợp Guest cho người triển khai UC-04

Phạm vi đã chốt ngày 13/10/2026: SCRUM-25 chuẩn bị phần đọc Agenda/realtime cho Guest theo ADR-0016; UC-04/SCRUM-20 triển khai lời mời, OTP, profile, session và lưu trữ. Không thêm endpoint xác thực, entity, migration, gửi email hoặc tạo User giả trong SCRUM-25. Các adapter mặc định **từ chối Guest**; không bật tính năng bằng cấu hình cho phép tất cả.

## Phần Agenda đã thực hiện

- Giữ `GET /api/meetings/{meetingId}/agenda` và `/hubs/meetings`, không thêm endpoint Guest riêng. DTO/event không đổi. Đường đi User nội bộ giữ `MeetingReadPolicy` và kiểm user active.
- `IAgendaService.GetForGuestAsync` kiểm identity hợp lệ, đúng meeting, quyền hiện tại qua adapter, meeting tồn tại và ở Published/InProgress/Ended như Member. Guest hợp lệ nhận metadata `{id,title}` của tài liệu gắn vào item trong meeting theo ADR-0016; không nhận file path, version chưa cấp qua meeting, email hoặc entity User.
- Guest chỉ đọc: `permissions/actions` đều false. Editor options và cả sáu mutation bị chặn `403` body rỗng. Không lấy quyền Secretary/Chair từ bất kỳ User ID trùng invitation/session ID hoặc subject nào. Presenter lựa chọn vẫn là internal participant active; tên tự nhập không tạo Guest.
- Hub kiểm quyền khi kết nối và mỗi `JoinMeeting`. Group do server tạo theo **meeting + invitation + session**, tách namespace User. `LeaveMeeting` chỉ tác động connection hiện tại và gọi lặp an toàn, kể cả sau mất quyền.
- Publisher lấy lại danh sách Guest session còn quyền trước mỗi lần phát event sau commit; group của session đã hết hạn/thu hồi không nằm trong danh sách gửi mới. Session khác của cùng invitation không kéo dài quyền cho session cũ. Sai meeting/identity rỗng trong kết quả adapter không được gửi. Nếu adapter đọc quyền lỗi, lần publish thất bại và được log, không fallback broadcast và không đổi kết quả mutation đã commit.
- FE join rồi GET; mọi event dẫn đến GET lại. Reconnect phải join lại và GET; resync khi tab hoạt động lại/định kỳ theo contract Agenda hiện có.

## Hai adapter UC-04 phải cung cấp

| Thành phần | Vị trí contract | Trách nhiệm |
|---|---|---|
| `IAgendaGuestIdentityResolver.Resolve(ClaimsPrincipal)` | `backend/TrustCabinet.Web/AgendaGuestIdentity.cs` | Map principal **đã được authentication middleware xác thực** thành `AgendaGuestSession(MeetingId, InvitationId, SessionId)`. Đây chỉ là identity, chưa cấp quyền đọc. |
| `IAgendaGuestAccess.CanReadAsync(session, ct)` | `backend/TrustCabinet.Domain/Agenda/AgendaGuestAccess.cs` | Đọc lại invitation và session hiện tại; trả true chỉ khi tất cả điều kiện dưới đây đúng. |
| `IAgendaGuestAccess.ListReadersAsync(meetingId, ct)` | Cùng interface | Trả các identity session hiện còn quyền trong đúng meeting, dựa trên cùng rule với CanReadAsync, dùng query theo tập hợp để tránh N+1. Không trả tên/email/token. |

Identity sử dụng ba GUID không rỗng, không phải schema bắt buộc của database UC-04. `SessionId` phân biệt từng phiên truy cập đã xác minh, giữ ổn định khi reconnect; không dùng connection ID làm session. Gia hạn/cấp phiên mới không hồi sinh session đã hết hạn/thu hồi. Không dùng tên tự khai làm khóa.

`CanReadAsync` và `ListReadersAsync` phải cùng kiểm:

1. Invitation tồn tại và thuộc MeetingId của identity.
2. Session tồn tại, thuộc đúng InvitationId, đã được thiết lập sau xác minh email lời mời; không suy ra verified chỉ từ claim client.
3. Invitation và session đều chưa hết hạn, chưa bị thu hồi/vô hiệu hóa; dùng UTC từ TimeProvider. Thu hồi invitation phải loại tất cả session của nó.
4. Mọi giới hạn truy cập Guest hiện hành của UC-04; không dựa vào việc đã join hub thành công trước đó. Agenda tự kiểm trạng thái Published/InProgress/Ended, không cho adapter mở quyền đọc Draft/WaitingApproval/Rejected/Approved.

Khi không đủ quyền trả false/danh sách không chứa session. Khi đọc dữ liệu lỗi, propagate lỗi persistence/cancellation theo convention hiện tại (`DbUpdateException`, `NpgsqlException`, `TimeoutException`, `OperationCanceledException`); không dùng lỗi kỹ thuật làm bằng chứng được cấp quyền. GET map lỗi persistence thành `500/MSG128`; publisher log và không phát. Adapter ngoài EF/Npgsql phải chuyển lỗi lưu trữ dự kiến về convention tương thích tại adapter, không catch mọi lỗi lập trình.

## Authentication và đăng ký DI

Middleware hiện nhận JWT Bearer có issuer/audience/signature/lifetime được kiểm. UC-04 chịu trách nhiệm cấp token sau xác minh, không để client tự tạo identity. Token Guest phải có `sub` riêng **không parse được thành Guid UserId** (ví dụ prefix `guest:`), không có capability/role nội bộ; lifetime JWT không vượt thời hạn session/invitation. `CloseOnAuthenticationExpiration` đã bật cho hub. Nếu dùng scheme/cookie riêng, UC-04 phải đăng ký policy cho GET Agenda/hub và kiểm thử tương thích; SCRUM-25 chưa triển khai scheme mới.

Tên claim và wire format do UC-04 quyết định, không phụ thuộc các claim `test_*` trong test SCRUM-25. Resolver chỉ đọc claim từ principal đã xác thực; không đọc header/body/query như bằng chứng danh tính, không query database trong resolver đồng bộ. Chỉ trả null cho principal không phải Guest. Principal Guest lỗi claim trả identity có GUID rỗng để Agenda từ chối; không fallback thành User. Kể cả resolver nhầm trả Guest kèm subject User, Agenda vẫn chặn toàn bộ mutation/options.

Đăng ký implementation sau các service Agenda, thay cả hai mặc định bằng `services.Replace(...)` từ `Microsoft.Extensions.DependencyInjection.Extensions`. Dùng lifetime scoped nếu adapter cần scoped DbContext; không singleton hóa DbContext. Mặc định hiện là `UnavailableAgendaGuestIdentityResolver` và `UnavailableAgendaGuestAccess`: nếu chưa thay resolver, principal Guest không có UserId nhận 403; có resolver nhưng chưa thay access adapter thì GET nhận 404 và không nhận event.

`IAgendaGuestAccess` nằm trong Domain, implementation UC-04 có thể ở Infrastructure; resolver ClaimsPrincipal ở Web. Không đưa ASP.NET/EF vào Domain. UC-04 không cần sửa thuật toán Agenda hoặc snapshot DTO để bật phần đọc Guest.

## HTTP và tiêu chí kiểm thử bàn giao

| Trường hợp | GET Agenda |
|---|---|
| Token thiếu/hết hạn/không hợp lệ | 401 theo authentication middleware |
| Guest identity sai, invitation/session mất hiệu lực, sai meeting, meeting không tồn tại hoặc chưa Published | 404 body rỗng |
| Được phép đọc | 200, snapshot; tài liệu như Member và tất cả cờ mutation false |
| Lỗi đọc policy/agenda/tài liệu | 500/MSG128 |

Query `access_token` chỉ dùng tại hub path đã cấu hình, không dùng ở REST. Hub join sai meeting/không có quyền trả cùng thông báo không tìm thấy. Endpoint tài liệu tải file/download vẫn phải kiểm quyền riêng; `canReadDocuments` không phải access token.

`AgendaGuestTests` dùng JWT ký bằng khóa test, fake adapter UC-04 và PostgreSQL thật cho Agenda, cùng transport SignalR LongPolling của TestServer. Test chứng minh consumer contract: snapshot/tài liệu chỉ đọc; chặn mọi mutation/options; ID/meeting isolation; kiểm lại revoked/expired/unverified/missing; trạng thái meeting; mặc định từ chối; lỗi policy/publisher; per-session delivery; reconnect và GET dữ liệu commit. Không phải bằng chứng OTP, Guest session production, endpoint tài liệu hay WebSocket/proxy đã hoạt động.

Người triển khai UC-04 phải chạy lại các scenario đó với repository/session thật, thêm xác minh OTP dùng một lần, chống replay, rate limit, thu hồi toàn invitation, JWT expiry đóng connection, cookie/CSRF nếu dùng cookie, logout và truy cập chéo mọi API nội bộ. FE chỉ thử Guest end-to-end khi adapter thật đã đăng ký và token được cấp bằng luồng UC-04. Trước đó không tạo token Guest thủ công trên server test để coi là nghiệm thu.

Sau khi cấu hình PostgreSQL test theo hướng dẫn dự án, có thể chạy riêng contract consumer:

```powershell
dotnet test TrustCabinet.sln --configuration Release --filter FullyQualifiedName~AgendaGuestTests
```

Test thủ công sau khi UC-04 đã tích hợp:

1. Secretary mời Guest vào meeting Published/InProgress có item gắn tài liệu; Guest xác minh OTP qua UC-04, lấy token bằng luồng thật.
2. Dùng token Guest gọi GET Agenda: 200, có metadata tài liệu của meeting, cờ chỉnh sửa đều false. GET meeting khác: 404 body rỗng. Options và mọi mutation: 403.
3. Dùng client SignalR bàn giao với token Guest: join rồi GET. Secretary sửa item, Guest nhận event và tải lại snapshot mới.
4. Thu hồi session thứ nhất khi còn session thứ hai của cùng invitation: phiên thứ nhất GET 404 và không nhận event tiếp theo; phiên thứ hai vẫn hoạt động nếu còn hiệu lực.
5. Thu hồi invitation: mọi session của invitation mất quyền GET/join và không nhận event mới. Hết hạn cũng phải có cùng kết quả; JWT expiry đóng connection.
6. Reconnect một phiên còn hiệu lực rồi join/GET để lấy snapshot mới nhất. Kiểm tra tài khoản User/roster không tăng do đăng nhập Guest.

Các lỗi storage và race dùng automated test trên database riêng, không gây lỗi trên server test dùng chung.
