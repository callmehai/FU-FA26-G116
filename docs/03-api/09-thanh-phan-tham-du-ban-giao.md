# SCRUM-20 / SCRUM-97 — bàn giao Frontend và test thủ công

Baseline PO 20/10/2026; tài liệu do AI hỗ trợ cập nhật ngày 21/10/2026. Backend bao gồm UC-73 bàn giao Chủ tọa trong PR #92. FE do Hải triển khai; UC/BR/sequence được BA chốt tại Final Review.

## Contract FE cần đổi

1. Form tạo họp lấy user từ `GET /api/meetings/participant-options?q=&page=1&pageSize=20` bằng quyền `MeetingCreator` (không gọi `/api/users`). Response items chỉ gồm id/fullName/email, có total/page/pageSize/roles, không có revision/ETag. Form tạo họp gửi `participants:[{userId,role}]`, 1..100 current internal active, đúng một Chair. Người tạo không bắt buộc tham dự; không bắt buộc Secretary khi tạo. Thiếu/không hợp lệ: 400/MSG277, `errors` là dictionary string theo key participants hoặc participants[i].userId/.role. PUT meeting không nhận participants.
2. Chi tiết/create trả `chair:{userId,fullName}|null`, `secretaries:[]`, `myMeetingRole`. Dùng để hiện vai trò và trạng thái sẵn sàng; không thay authorization server.
3. Context có `myRole` và đúng ba flags `canManageParticipants,canReplaceChair,canRespond`. Bỏ recovery UI và mọi checkbox quyền bổ sung.
4. Roster gồm response/respondedAt cho nhãn cạnh tên; **không còn `totals`** (21/10/2026, theo AI-eCabinet); bỏ gọi invitation-responses. Trả lời lời mời theo [§4.1 contract](09-thanh-phan-tham-du.md#41-trả-lời-lời-mời--theo-ai-ecabinet-po-hải-21102026): một lần, Chủ tọa không trả lời, FE dùng `myResponse`/`canRespond` của dữ liệu cuộc họp. Options bỏ mode/permissionCatalog, roles luôn Chair/Secretary/Member.
5. UC-73 mới dùng GET candidates và một POST chair-handover theo hướng dẫn bên dưới. PUT assignments có batch demote/promote và confirmation vẫn tương thích: Published/InProgress chỉ Chair bàn giao hoặc Secretary thay Chair inactive; recipient không phải Secretary và phải active/current. Thành công Chair bàn giao: MSG257; Secretary thay Chair inactive: MSG100.
6. Giữ ETag/If-Match và revision dạng string. 409/MSG275 phải tải lại và để người dùng xác nhận lại. MSG276 query/header sai. Không dùng MSG252/253 (SCRUM-96).
7. Sau save hoặc `participantsChanged`, reload context/roster/detail. Event có thể cùng revision khi chỉ thay response; không bỏ event chỉ vì revision không tăng. Dedupe eventId. Người bị gỡ nhận invalidation và reload404 thì rời màn chi tiết. Không báo “đã gửi email” chỉ từ save thành công.
8. Các mã MSG256–MSG260 đã triển khai; MSG258 là empty state HTTP200, MSG260 là lỗi409 của UC-73 mới. MSG152/153 ngừng dùng. SCRUM-26 phải remap MSG277/278 trước tích hợp.
9. Guest OTP/profile/session thật còn chờ SCRUM-59; phòng nội bộ đã tích hợp truy vấn current/active với SCRUM-41: gỡ participant chặn cấp quyền mới ngay; job đối soát phòng loại người đã gỡ ở chu kỳ tiếp theo (30 giây). Không nghiệm thu Guest production bằng test JWT/adapters.

## FE đã tích hợp (21/10/2026)

- Form tạo họp có mục **Thành phần tham dự** bắt buộc: tìm qua participant-options, chọn vai trò từng người, đúng một Chủ tọa (chọn Chủ tọa khác thì người cũ thành Thành viên). MSG277 hiện dưới đúng dòng người bị lỗi.
- Tab **Thành phần tham dự** ở màn chi tiết: tìm kiếm, phân trang; Chủ tọa/Thư ký thêm người, mời khách, đổi vai trò, giao Chủ tọa (một request kèm confirmation), gỡ/thu hồi. MSG275/MSG98/403/404 đóng hộp thoại, báo lý do và tải lại.
- Thanh "Bạn có tham dự cuộc họp này?" ở đầu màn chi tiết và trên thẻ lịch dạng danh sách; lịch gửi `excludeDeclined=true`.
- `/hubs/meetings` dùng chung một kết nối cho mỗi cuộc họp đang mở: `participantsChanged` (dedupe theo eventId) và mỗi lần nối lại đều tải lại roster, context và chi tiết cuộc họp.

## Test thủ công

Dùng database Development riêng, đăng nhập qua API; giữ token trong Postman environment, không export secrets. Import [collection](postman/scrum20-participants.postman_collection.json), điền token đúng actor, meetingId, userId, outgoingParticipantId, incomingParticipantId. GET context tự lưu ETag. API trực tiếp mặc định localhost:5225; FE vẫn relative URL qua Vite.

Seed `[SCRUM20-DEV]` có 7 trạng thái, ID prefix `20000000-0000-7000-8000-`, suffix 1..7 tương ứng Draft, Rejected, Approved, WaitingApproval, Published, InProgress, Ended. Mỗi roster có Chair/Secretary/Member. Seed insert-only không reset scenario đã chỉnh.

| Bước | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Tạo họp có Chair + Member; rồi thử bỏ Chair, trùng user, inactive user | 201/MSG29 hoặc 400/MSG277; lỗi không để lại meeting/history/roster |
| 2 | Chair đọc Draft và WaitingApproval; Member đọc cùng ID | Chair200; Member404 |
| 3 | Chair/Secretary thêm, đổi, gỡ Member/Secretary ở Draft | Thành công; có đúng một Chair; chưa có dispatch |
| 4 | Gỡ Chair hoặc Chair tự hạ mà không thay | 409/MSG109 |
| 5 | Secretary thay Chair active ở Published/InProgress | 409/MSG256 |
| 6 | Chair bàn giao Member, đúng confirmation | 200/MSG257; Chair cũ Member, người mới Chair; 2 role-change notifications/dispatch |
| 7 | Bàn giao cho Secretary/inactive/removed/nonexistent | 400/MSG259 |
| 8 | Deactivate Chair qua quản lý users, Secretary thay bằng Member active | 200/MSG100; Secretary cuối vẫn được giữ |
| 9 | Chair gỡ/hạ Secretary cuối ở Published/InProgress | 409/MSG109; có Secretary khác thì được |
| 10 | Member/Organizer không role/current approver đọc roster và thử sửa | GET200; mutation403/MSG85 |
| 11 | Accept/Decline bằng người được mời; gửi câu khác lần hai; Chủ tọa trả lời; search không khớp và paging roster | MSG105; lần hai MSG285; Chủ tọa MSG286; attendance không đổi; lịch người từ chối không còn cuộc họp |
| 12 | Hai tab cùng ETag sửa roster | Một thành công; request cũ409/MSG275 |
| 13 | Hai kết nối hub, gỡ một participant | Cả người quản lý và người bị gỡ nhận participantsChanged; người bị gỡ reload404 |
| 14 | Mời/revoke Guest, mời lại | ID mới, lịch sử cũ giữ; Guest không được cấp internal role |
| 15 | Dời lịch Published có Guest | Durable dispatch Rescheduled theo event/invitation, kể cả dependency chưa Ready |

SMTP thật không thuộc kết quả automated tests. Khi kiểm mail dùng SMTP sandbox, kiểm lịch/role/link và không gửi tới người thật. Guest default giữ BlockedDependency với backoff 1m/5m/30m/6h. Integration thật theo [contract UC-04](09-thanh-phan-tham-du-guest-uc04.md). Migration không tự chạy lại trên database đã ghi nhận cùng ID cũ: môi trường thử đã apply bản PR cũ cần dựng database thử mới hoặc owner quản lý nâng cấp riêng; không sửa schema bằng tay.

Kết quả kiểm thử hiện tại ở [test report](09-thanh-phan-tham-du-ket-qua-kiem-thu.md).

## Gate tích hợp và regression review 21/10/2026

**Chờ FE do Hải thực hiện gửi participants khi tạo meeting rồi merge phối hợp cùng lúc.** Không đưa riêng contract bắt buộc mới lên server test khi màn tạo họp còn gửi payload cũ.

Test thêm: mời Guest khi UC-04 Unavailable rồi gỡ, hoặc thêm/gỡ Member trước khi worker materialize: dispatch Removed phải Suppressed, không có thư thu hồi. Với Invitation đã Materialized, gỡ vẫn tạo thư thu hồi không chứa link bảo vệ. Đây là điều kiện đã xếp hàng email, không cam kết SMTP đã gửi.

Trên môi trường kiểm thử riêng, dùng provider ném InvalidOperationException ở IsAvailableAsync/PrepareAsync: attempt tăng, có backoff, đủ MaxAttempts thành Failed; dispatch sau và email reset mật khẩu đã xếp hàng vẫn được xử lý. Không cố tình gây lỗi provider/database dùng chung.

## Regression tích hợp phòng và lookup (21/10/2026)

1. Token chỉ có MeetingCreator: gọi lookup tìm theo tên/email, pageSize=1 rồi page=2; user inactive không xuất hiện, `/api/users` vẫn 403. Query sai trả MSG276. Dùng ID trả về tạo họp có một Chair, kỳ vọng 201/MSG29.
2. Token không có MeetingCreator gọi lookup: 403; chưa đăng nhập: 401. Thu hồi quyền trong database rồi gọi lại với token cũ: 403.
3. Với cuộc họp Online/InProgress: gỡ Member, dùng token Member thử vào phòng phải bị từ chối; chờ job đối soát loại kết nối hiện có. Mời lại cùng user, vào phòng được theo assignment mới và không lỗi trùng dòng. Lịch sử assignment cũ vẫn giữ.

Vẫn chờ FE gửi participants trước khi merge/deploy phối hợp. Lookup mới và sửa truy vấn không cần migration.

## UC-73 riêng — bàn giao SCRUM-97 ngày 22/10/2026

Backend task này chỉ bổ sung API/tests/tài liệu. Thành viên FE tự tạo types và functions qua `src/api/client.ts`, dùng relative URL và contract [API §8](09-thanh-phan-tham-du.md#8-scrum-97--api-chuyên-biệt-uc-73). Không đổi ba flags context.

1. Hiện hành động khi `myRole === "Chair"`, status Published/InProgress và `permissions.canReplaceChair`. Server luôn kiểm quyền lại.
2. Mở dialog gọi GET `/api/meetings/{id}/participants/chair-handover-candidates?q=&page=1&pageSize=20`. Không dùng editor-options, không tự lọc một trang roster.
3. Mỗi item có participantId/userId/fullName/email; giữ participantId làm lựa chọn. Giữ revision string và ETag **cùng response GET**.
4. Nếu code MSG258 trong HTTP200, hiển thị trạng thái chưa có Member phù hợp và khóa nút xác nhận; không gọi isApiError cho tình huống này. Search rỗng với eligibleTotal>0 chỉ là không khớp tìm kiếm.
5. Chọn người rồi xác nhận; gửi **một POST** `/participants/chair-handover` với `{newChairParticipantId: selected.participantId,confirmed:true}` và `If-Match: "participants-${revision}"`. Không tự hạ Chair bằng request riêng, không gửi actor/role.
6. Khi `participantsChanged`, tải lại dữ liệu cần thiết và dedupe theo eventId; revision có thể không đổi khi event do response lời mời.
7. Secretary thay Chair inactive tiếp tục dùng PUT assignments với confirmation hiện hành. API mới không dành cho Secretary, kể cả canReplaceChair đang true.

| Response | Cách FE xử lý |
|---|---|
| 200/MSG257 | Đóng dialog, reload context/detail/roster; Chair cũ vẫn Member, không tự rời phòng |
| 200/MSG258 | Empty state, khóa nút xác nhận; không coi là exception |
| 400/MSG259 | Bỏ lựa chọn cũ, tải lại candidates |
| 409/MSG260 | Đóng dialog, tải lại context/detail/roster vì quyền/status đổi |
| 409/MSG275 | Reload và yêu cầu người dùng chọn/xác nhận lại; không tự retry mutation |
| 409/MSG109 | Báo thiếu vai trò bắt buộc; tải lại roster để người có quyền phân công Secretary |
| 400/MSG101 hoặc MSG276 | Kiểm schema/header/query của client; không tự gửi lại batch |
| 401/403/404 | Theo xử lý session/access hiện có; không suy luận meeting tồn tại từ404 |
| 500/MSG95 hoặc 503/MSG102 | Báo lỗi tương ứng; tải lại dữ liệu trước khi người dùng thử lại |

Collection Postman có folder `UC-73 — smoke tuần tự` (DB riêng), dùng token Chair và token Member được cấp bên ngoài collection. Kết quả/giới hạn xác minh và change note cho BA: [báo cáo SCRUM-97](10-scrum97-ket-qua-kiem-thu.md).

## FE đã tích hợp UC-73 (23/10/2026)

- **Một chỗ duy nhất để đổi Chủ tọa**: nút bút chì "Đổi vai trò" trên dòng của người sẽ làm Chủ tọa, chọn "Chủ tọa", hộp thoại nói rõ "X thành Chủ tọa, Y thành Thành viên", bấm **Giao Chủ tọa**. Không có nút riêng trên thanh công cụ, để mọi trạng thái cuộc họp và mọi người bấm đều đi cùng một đường.
- Lựa chọn "Chủ tọa" chỉ hiện khi `permissions.canReplaceChair` và người trên dòng nhận được: tài khoản còn hoạt động, và từ Published chỉ Thành viên (không phải Thư ký). Lựa chọn và request được chốt lúc mở hộp thoại; panel tải lại phía sau không đổi chúng, server kiểm lại.
- FE tự chọn API: Chủ tọa của cuộc họp Published/InProgress gọi POST chair-handover (§8); trước Published, và Thư ký thay Chủ tọa đã khoá tài khoản, gọi PUT assignments kèm `chairReplacement`. Người dùng không thấy khác biệt.
- MSG259: báo trong hộp thoại. MSG260, MSG275, 403, 404: đóng hộp thoại, báo lý do, tải lại tab. MSG102: báo trong hộp thoại, bấm lại được.
- **Không dùng** GET chair-handover-candidates, nên MSG258 không hiện trên UI: không ai nhận được thì không dòng nào có lựa chọn "Chủ tọa". Khác Normal Flow bước 1–2 của UC-73 ("chọn Hand over Chair, hệ thống hiện danh sách"); quy tắc nghiệp vụ không đổi. Đã ghi vào Jira SCRUM-97 cho BA Final Review.
- Bảng thành phần đổi bố cục theo độ rộng của chính nó (container query, dưới 520px): vai trò thành chip dưới tên, tên xuống dòng thay vì cắt, nút sửa/gỡ luôn trong khung. Trước đây trên điện thoại và ở cửa sổ 1024px (bảng nằm cạnh Lịch sử trạng thái) cột Vai trò và nút thao tác bị đẩy ra ngoài, phải cuộn ngang.
