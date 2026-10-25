# 00 · Danh mục mã thông báo (`MSGnn`)

Nguồn sự thật duy nhất cho mọi mã `code` mà API trả về hoặc FE tự hiển thị. Đặc tả use case,
sequence diagram, code BE, code FE và test đều phải khớp với bảng này.

Business Rule là hệ quy tắc riêng, không dùng mã `BR` thay mã `MSG`. Nội dung BR tập trung tại
[danh mục Business Rules](../01-requirements/06-business-rules.md); UC dẫn chiếu BR và MSG tại
các bước tương ứng, còn sequence thể hiện cùng BR ID và message của nhánh đó.

## Quy tắc

1. **Một dải số chung cho cả dự án.** Không đánh lại từ `MSG01` cho mỗi use case hay mỗi sơ đồ —
   đó chính là lỗi đã khiến `MSG01`, `MSG03`, `MSG04` mang nhiều nghĩa khác nhau.
2. **Một mã = một tình huống.** Cùng một tình huống xuất hiện ở nhiều endpoint thì dùng chung mã
   (ví dụ `MSG02` "tài khoản không khả dụng" ở cả `/login` lẫn `/google`). Hai tình huống khác nhau
   thì không bao giờ chung mã, kể cả khi câu chữ gần giống nhau.
3. **Định dạng `MSG` + hai chữ số** (`MSG07`, `MSG41`); qua `MSG99` thì dùng ba chữ số (`MSG100`).
   Không dùng mã dạng chữ (`MEETING_CREATED`, `NOTIFICATION_LOAD_FAILED`, …).
4. **Mã mới lấy số kế tiếp** ở mục [Mã tiếp theo](#mã-tiếp-theo), rồi thêm một dòng vào bảng dưới
   **trong cùng commit** với code.
5. **Không tái sử dụng số.** Mã bị bỏ thì giữ nguyên dòng, đánh dấu *Ngừng dùng*, không gán số đó
   cho nghĩa khác.
6. **FE khớp theo `code`, không khớp theo `message`.** Câu chữ đổi được bất cứ lúc nào; `code` là
   hợp đồng.
7. Trong code BE, khai báo mã thành hằng số có tên theo mẫu `Msg29Created = "MSG29"` ở đầu file
   endpoint, không rải chuỗi `"MSGnn"` trong thân hàm.

## Attendance — contract Backend SCRUM-26

UC-31 đã có Backend theo [contract điểm danh](10-diem-danh.md). Các dòng nguồn UC phía dưới giữ để truy vết; MSG147–151 không còn chỉ dành sẵn.

| Mã | HTTP | Endpoint / tình huống | Thông báo |
|---|---|---|---|
| `MSG147` | 200 | POST attendance, gồm no-op hợp lệ | Lưu điểm danh thành công. |
| `MSG148` | 400 | Body, entry hoặc định dạng token không hợp lệ | Thông tin điểm danh không hợp lệ. Chọn Có mặt hoặc Vắng cho từng người được gửi. |
| `MSG149` | 409 | Meeting không còn InProgress | Chỉ được điểm danh khi cuộc họp đang diễn ra. |
| `MSG150` | 503 | Không lưu được hoặc kết quả commit chưa xác định | Không xác nhận được kết quả lưu điểm danh. Vui lòng tải lại dữ liệu trước khi thử lại. |
| `MSG151` | 409 | Entry không còn current hoặc version điểm danh stale | Thành phần hoặc dữ liệu điểm danh đã thay đổi. Vui lòng tải lại và kiểm tra trước khi lưu. |
| `MSG289` | 500 | GET attendance lỗi đọc | Không tải được dữ liệu điểm danh. |
| `MSG278` | 400 | Query GET attendance không hợp lệ | Tham số phân trang điểm danh không hợp lệ. |

Dùng lại 415/MSG34 và 413/MSG87 cho content-type và body 64 KiB; 401 authentication, 403/404 body rỗng. MSG150 không hứa rollback khi client/server mất kết nối đúng lúc commit. MSG151 bao gồm xung đột điểm danh theo entry, không dùng revision roster.

## Participants — contract Backend SCRUM-20

Các mã UC-27–30 dưới đây đã có Backend. OTP/session production thuộc UC-04 riêng. Mã trong bảng nguồn UC phía dưới giữ để truy vết, không còn là “chưa triển khai” với những mã được liệt kê ở đây.

| Mã | HTTP | Endpoint / tình huống | Thông báo |
|---|---|---|---|
| `MSG95` | 500 | GET participant context/roster/options/own-response hoặc /api/meetings/participant-options lỗi đọc | Không tải được danh sách thành phần hoặc phản hồi lời mời. |
| `MSG96` | 201 | Thêm internal/Guest hoặc recovery thêm Secretary | Thêm thành phần tham dự thành công. |
| `MSG97` | 400 | Selection/batch/Guest input không hợp lệ | Thông tin gửi lên không hợp lệ. |
| `MSG98` | 409 | State không cho sửa roster | Không thể sửa thành phần ở trạng thái hiện tại của cuộc họp. |
| `MSG99` | 503 | Add/recovery không lưu được | Không lưu được thay đổi. Vui lòng thử lại. |
| `MSG100` | 200 | Đổi role hoặc recovery current Member | Cập nhật vai trò cuộc họp thành công. |
| `MSG101` | 400 | Role/atomic replacement input sai | Vai trò cuộc họp hoặc yêu cầu thay vai trò không hợp lệ. |
| `MSG102` | 503 | Role/grants không lưu được | Không lưu được thay đổi. Vui lòng thử lại. |
| `MSG103` | 200 | Remove/revoke | Gỡ thành phần tham dự thành công. |
| `MSG104` | 503 | Remove/revoke không lưu được | Không lưu được thay đổi. Vui lòng thử lại. |
| `MSG105` | 200 | Own invitation response được ghi | Đã ghi nhận phản hồi lời mời. |
| `MSG106` | 400 | Response/schema sai | Thông tin gửi lên không hợp lệ. |
| `MSG107` | 409 | Invitation đọc được nhưng không còn answerable | Lời mời này không còn cho phép phản hồi. |
| `MSG108` | 503 | Response không lưu được | Không lưu được thay đổi. Vui lòng thử lại. |
| `MSG109` | 409 | Vi phạm required Chair/Secretary | Cuộc họp phải giữ Chủ tọa và Thư ký theo quy định. Hãy phân công người thay thế. |
| `MSG285` | 409 | PUT invitation-response: đã trả lời Tham dự/Không tham dự rồi, gửi câu trả lời khác (trả lời một lần, như AI-eCabinet) | Bạn đã trả lời lời mời này. Câu trả lời không đổi được. |
| `MSG286` | 409 | PUT invitation-response: Chủ tọa trả lời lời mời | Chủ tọa điều hành cuộc họp nên không cần xác nhận tham dự. |
| `MSG152` | — | Ngừng dùng — quyền bổ sung đã bỏ theo PO 20/10/2026 | Không tái cấp. |
| `MSG153` | — | Ngừng dùng — quyền bổ sung đã bỏ theo PO 20/10/2026 | Không tái cấp. |
| `MSG154` | 200 | Role/grants không có thay đổi, hoặc gửi lại đúng câu trả lời đã ghi | Không có thay đổi cần lưu. |
| `MSG275` | 409 | Revision roster stale | Danh sách thành phần đã thay đổi. Vui lòng tải lại và thử lại. |
| `MSG276` | 400 | Query (kể cả lookup trước tạo họp) hoặc If-Match thiếu/sai | Query hoặc If-Match không hợp lệ. Vui lòng tải lại dữ liệu. |

Endpoint Participants dùng lại `415/MSG34` cho content-type và `413/MSG87` cho giới hạn 64 KiB. 401 authentication; 403/404 body rỗng theo quyền và khả năng tiết lộ tài nguyên.

## Agenda — contract Backend SCRUM-25

Các dòng nguồn UC trong bảng phía dưới được giữ để đối chiếu phân bổ, nhưng MSG60–76, MSG128, MSG141–144 **đã triển khai**, không còn dành sẵn. Endpoint và câu phản hồi hiện hành:

| Mã | HTTP | Endpoint / tình huống | Thông báo |
|---|---|---|---|
| `MSG60` | 201 | POST agenda | Tạo mục nghị sự thành công. |
| `MSG61` | 200 | PUT item | Cập nhật mục nghị sự thành công. |
| `MSG62` | 200 | DELETE item | Xóa mục nghị sự thành công. |
| `MSG63` | 200 | PUT order | Sắp xếp mục nghị sự thành công. |
| `MSG64` | 200 | POST current | Bắt đầu mục nghị sự thành công. |
| `MSG65` | 200 | POST complete | Hoàn thành mục nghị sự thành công. |
| `MSG66` | 400 | POST/PUT item; errors theo trường hoặc body | Thông tin mục nghị sự không hợp lệ. |
| `MSG67` | 403 | Planning/options: thiếu Secretary | Bạn không có quyền thực hiện thao tác này. |
| `MSG67` | 409 | Planning/options: meeting đã khóa | Trạng thái cuộc họp không cho phép thao tác này. |
| `MSG68` | 503 | Lưu tạo thất bại | Không lưu được thao tác nghị sự. Vui lòng thử lại. |
| `MSG69` | 503 | Lưu sửa thất bại | Không lưu được thao tác nghị sự. Vui lòng thử lại. |
| `MSG70` | 503 | Lưu xóa thất bại | Không lưu được thao tác nghị sự. Vui lòng thử lại. |
| `MSG71` | 503 | Lưu reorder thất bại | Không lưu được thao tác nghị sự. Vui lòng thử lại. |
| `MSG72` | 409 | Start: đã có current | Đã có mục nghị sự đang diễn ra. |
| `MSG73` | 403 | Start: thiếu Chair | Bạn không có quyền thực hiện thao tác này. |
| `MSG73` | 409 | Start: trạng thái meeting/item không hợp lệ | Trạng thái cuộc họp không cho phép thao tác này. / Mục nghị sự không còn ở trạng thái chờ. |
| `MSG74` | 403 | Complete: thiếu Chair | Bạn không có quyền thực hiện thao tác này. |
| `MSG74` | 409 | Complete: trạng thái meeting/item không hợp lệ | Trạng thái cuộc họp không cho phép thao tác này. / Chỉ được hoàn thành mục nghị sự đang diễn ra. |
| `MSG75` | 503 | Lưu start thất bại | Không lưu được thao tác nghị sự. Vui lòng thử lại. |
| `MSG76` | 503 | Lưu complete thất bại hoặc actual time không hợp lệ | Không lưu được thao tác nghị sự. Vui lòng thử lại. |
| `MSG128` | 500 | GET agenda/options thất bại | Không tải được chương trình nghị sự. |
| `MSG141` | 409 | Sửa field bị khóa | Chỉ được sửa tiêu đề và mô tả sau khi mục đã bắt đầu. |
| `MSG142` | 409 | Xóa item đã bắt đầu | Chỉ được xóa mục nghị sự chưa bắt đầu. |
| `MSG143` | 400 | Reorder: body hoặc tập Pending ID không hợp lệ | Danh sách sắp xếp phải chứa đúng mỗi mục chưa bắt đầu một lần. |
| `MSG143` | 409 | Reorder: orderVersion cũ | Chương trình nghị sự đã thay đổi. Vui lòng tải lại. |
| `MSG144` | 409 | Start: không phải Pending đầu tiên | Phải bắt đầu mục nghị sự chờ đầu tiên. |
| `MSG34` | 415 | Agenda POST/PUT có body không phải JSON | Yêu cầu phải dùng Content-Type application/json. |
| `MSG87` | 413 | Agenda POST/PUT có body vượt 64 KiB | Nội dung yêu cầu vượt quá 64 KiB. |

### Contract bổ sung PO 20/10/2026 — SCRUM-20 / SCRUM-97

| Mã | HTTP | Endpoint / tình huống | Thông báo |
|---|---|---|---|
| `MSG256` | 409 | PUT assignments: Secretary tác động Chair còn active ở Published/InProgress | Chỉ Chủ tọa được giao vai trò Chủ tọa khi tài khoản hiện tại còn hoạt động. |
| `MSG257` | 200 | PUT assignments / POST chair-handover: Chair bàn giao ở Published/InProgress | Giao vai trò Chủ tọa thành công. |
| `MSG258` | 200 | GET chair-handover-candidates: toàn meeting không có current active Member đủ điều kiện | Không có thành viên đủ điều kiện nhận vai trò Chủ tọa. Hãy thêm thành viên nội bộ trước. |
| `MSG259` | 400 | PUT assignments / POST chair-handover: người nhận Chair không phải current active Member khác Chair | Người nhận vai trò Chủ tọa phải là thành viên nội bộ hiện tại, đang hoạt động và không phải Thư ký. |
| `MSG260` | 409 | GET chair-handover-candidates / POST chair-handover: người đọc nội bộ không còn là current Chair hoặc status không cho bàn giao | Bạn không còn quyền giao vai trò Chủ tọa cho cuộc họp này. |
| `MSG277` | 400 | POST meetings: roster khởi tạo không hợp lệ | Danh sách thành phần tham dự không hợp lệ. |

`MSG258` và `MSG260` đã triển khai trong API chuyên biệt UC-73 của SCRUM-97. `MSG258` là empty state trong response thành công, không dùng cho search không khớp. SCRUM-26 dùng `MSG289` cho lỗi đọc và `MSG278` cho query không hợp lệ; `MSG277` đã thuộc validation roster khi tạo họp trong PR #92.

### Bắt đầu/kết thúc cuộc họp — SCRUM-69 (UC-21/22)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG110` | 200 | `POST /api/meetings/{id}/transitions` | Start thành công | `Bắt đầu cuộc họp thành công.` | BE |
| `MSG111` | 200 | `POST /api/meetings/{id}/transitions` | End thành công | `Kết thúc cuộc họp thành công.` | BE |
| `MSG112` | 403 | `POST /api/meetings/{id}/transitions` | Start: caller đọc được nhưng không phải active current Chair | `Không thể bắt đầu cuộc họp này.` | BE |
| `MSG112` | 409 | `POST /api/meetings/{id}/transitions` | Start: sai trạng thái hoặc thiếu roster | `Không thể bắt đầu cuộc họp này.` | BE |
| `MSG114` | 403 | `POST /api/meetings/{id}/transitions` | End: caller đọc được nhưng không phải active current Chair | `Không thể kết thúc cuộc họp này.` | BE |
| `MSG114` | 409 | `POST /api/meetings/{id}/transitions` | End: sai trạng thái | `Không thể kết thúc cuộc họp này.` | BE |
| `MSG115` | 503 | `POST /api/meetings/{id}/transitions` | Start: lỗi persistence trước commit | `Không thể bắt đầu cuộc họp. Vui lòng thử lại.` | BE |
| `MSG116` | 503 | `POST /api/meetings/{id}/transitions` | End: lỗi persistence hoặc timestamp agenda không hợp lệ trước commit | `Không thể kết thúc cuộc họp. Vui lòng thử lại.` | BE |

## Tài liệu cuộc họp — contract Backend SCRUM-107

`POST /api/meetings/{id}/documents/upload` (UC-33a AF-1), chi tiết ở [11-tai-lieu-cuoc-hop.md](11-tai-lieu-cuoc-hop.md). Các mã dưới đây **đã triển khai**; dòng nguồn UC phía dưới giữ để truy vết. Không cấp mã mới.

| Mã | HTTP | Endpoint / tình huống | Thông báo |
|---|---|---|---|
| `MSG161` | 201 | Tải lên thành công, kèm `addedCount` và `documents` | Đã thêm tài liệu vào cuộc họp. |
| `MSG162` | 403 | Xem được cuộc họp nhưng không còn là Chủ toạ/Thư ký hiện tại | Bạn không còn quyền thêm tài liệu vào cuộc họp này. |
| `MSG164` | 400 | Không có tệp, không phải multipart, multipart hỏng, hoặc tệp có đuôi khác `.pdf`/`.docx` / nội dung không khớp đuôi / rỗng / quá 50 MB; `errors["files[i]"]` | Hãy chọn tệp PDF hoặc DOCX hợp lệ, mỗi tệp không quá 50 MB. |
| `MSG166` | 503 | R2/DB lỗi hoặc chưa cấu hình kho tệp; đã xoá object của lần tải | Không thêm được tài liệu. Chưa có tài liệu nào được thêm. Vui lòng thử lại. |
| `MSG172` | 409 | Cuộc họp `Ended`, kể cả khi vừa kết thúc trong lúc tải | Không thể thêm tài liệu sau khi cuộc họp đã kết thúc. |
| `MSG174` | 404 | Không xem được cuộc họp hoặc cuộc họp không tồn tại (cùng body) | Không truy cập được cuộc họp này. |
| `MSG288` | 400 | Quá 10 tệp hoặc tổng quá 200 MB (BR-319) | Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 200 MB. |

## Xem tài liệu cuộc họp — contract Backend SCRUM-21

`GET /api/meetings/{id}/documents` (UC-32a) và `GET /api/meetings/{id}/documents/{meetingDocumentId}/content` (UC-32c),
chi tiết ở [11-tai-lieu-cuoc-hop.md](11-tai-lieu-cuoc-hop.md#scrum-21--xem-và-tải-tài-liệu). Các mã dưới đây **đã triển khai**;
dòng nguồn UC phía dưới giữ để truy vết. Không cấp mã mới.

| Mã | HTTP | Endpoint / tình huống | Thông báo |
|---|---|---|---|
| `MSG156` | 404 | Không xem được cuộc họp, cuộc họp không tồn tại, hoặc tài liệu đã gỡ / không thuộc cuộc họp này (cùng body) | Không truy cập được tài liệu của cuộc họp. |
| `MSG157` | 500 | GET danh sách lỗi đọc DB | Không tải được danh sách tài liệu. Vui lòng thử lại. |
| `MSG158` | 503 | GET content (xem) không đọc được tệp: object thiếu, kho chưa cấu hình/lỗi, DB lỗi, hoặc không ghi được nhật ký kiểm toán | Không hiển thị được tài liệu. Vui lòng thử lại. |
| `MSG159` | 403 | `?download=true` với cuộc họp `Secret` (BR-188) | Tài liệu của cuộc họp Mật chỉ được xem trực tuyến, không được tải về. |
| `MSG160` | 503 | Như `MSG158` nhưng khi `?download=true` | Không tải về được tài liệu. Vui lòng thử lại. |

## Mã tiếp theo

**`MSG290`** — `MSG288` (UC-33a EX-8, giới hạn 10 tệp / 200 MB mỗi lần tải, PO 22/10/2026) đã triển khai cùng `MSG161`, `MSG162`, `MSG164`, `MSG166`, `MSG172`, `MSG174` trong SCRUM-107. SCRUM-105 dùng `MSG287` (FE). Phòng họp trực tuyến (SCRUM-39/41) dùng `MSG117`, `MSG118`, `MSG123`, `MSG124` đã dành sẵn và `MSG280`–`MSG284`. SCRUM-20 đã triển khai `MSG95`–`MSG109`, `MSG154`, `MSG256`/`MSG257`/`MSG259`, `MSG275`–`MSG277`, `MSG285`/`MSG286`; `MSG278` và `MSG289` thuộc SCRUM-26; `MSG279` chưa cấp. `MSG258`/`MSG260` đã triển khai trong SCRUM-97; trong dải `MSG252`–`MSG274`, chỉ `MSG252`–`MSG255` và `MSG261`–`MSG274` còn dành sẵn cho thay đổi Report 3 ngày 18/10/2026 (UC-28b, UC-55, UC-73, UC-74, UC-75, UC-76, UC-77). `MSG238`–`MSG251` dành sẵn, chưa triển khai, cho ADR-0020; đặc tả UC-23b dùng `MSG250` cho giới hạn planning fields. `MSG163`, `MSG168`, `MSG173`, `MSG237` ngừng dùng, không tái cấp. `MSG205`–`MSG236` dành sẵn cho đặc tả FE-11 (UC-52a, UC-52b, UC-53–UC-57), chưa triển khai. `MSG156`–`MSG204` dành sẵn cho toàn bộ UC FE-07 trong Report 3; trong dải này SCRUM-107 đã triển khai `MSG161`, `MSG162`, `MSG164`, `MSG166`, `MSG172`, `MSG174`, SCRUM-21 đã triển khai `MSG156`–`MSG160`, phần còn lại chưa triển khai. `MSG140` dành cho UC-18 EX-5; `MSG145`/`MSG146` dành cho thay đổi FE-06 trong Report 3; `MSG147`–`MSG151` đã triển khai trong SCRUM-26; `MSG152`/`MSG153` ngừng dùng, không tái cấp. Agenda đã triển khai `MSG60`–`MSG76`, `MSG128`, `MSG141`–`MSG144` theo bảng contract phía trên. Workflow SCRUM-19 dùng `MSG41`–`MSG47`, `MSG78`–`MSG89` và `MSG130`–`MSG137`; dời lịch cuộc họp dùng `MSG138`. `MSG90`–`MSG91`, `MSG139`, `MSG155` ngừng dùng, không tái cấp. `MSG95`–`MSG126` dành cho participant, room và các màn đọc liên quan theo từng dòng bên dưới; các mã Meeting Session (`MSG48`–`MSG59`, `MSG77`, `MSG113`, `MSG127`) ngừng dùng theo [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md). Đặt lại mật khẩu UC-07 dùng `MSG92`–`MSG94` và `MSG129` (FE).

## Danh mục

Trạng thái: **BE** = backend trả trong body JSON · **FE** = FE tự hiển thị, không qua API ·
**Dành sẵn** = đã có trong đặc tả/sơ đồ, chưa có code.

### Xác thực — UC-01, UC-02, UC-03 (`/api/auth`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG01` | 401 | `POST /login` | Email không tồn tại, sai mật khẩu, hoặc bỏ trống | `Email hoặc mật khẩu không đúng.` | BE |
| `MSG01` | 401 | `POST /refresh` | Refresh token hết hạn, bị thu hồi, hoặc không có cookie | `Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.` | BE |
| `MSG02` | 401 | `POST /login`, `POST /google`, `POST /refresh` | Tài khoản bị vô hiệu hoá | `Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.` | BE |
| `MSG03` | 401 | `POST /google` | ID token Google không verify được (thiếu, sai định dạng, hết hạn, sai audience, sai chữ ký, email chưa xác minh) | `Đăng nhập bằng Google không thành công.` | BE |
| `MSG03` | — | — (FE, trước khi gọi `/google`) | Google báo lỗi hoặc người dùng huỷ đăng nhập Google — không có ID token để gửi | `Đăng nhập bằng Google không thành công.` | FE |
| `MSG04` | — | `POST /google` | Không xác minh được thông tin Google trả về | *Unable to verify your Google account. Please try again.* | Dành sẵn — verifier chưa tách được khỏi `MSG03` |
| `MSG05` | 401 | `POST /google` | Google xác thực OK nhưng không có tài khoản nội bộ nào liên kết/tự liên kết được | `Tài khoản Google này chưa được cấp quyền truy cập hệ thống.` | BE |
| `MSG06` | — | — (FE, sau `POST /logout`) | `/logout` không tới được server, phiên được giữ nguyên | `Không thể đăng xuất. Vui lòng thử lại.` | FE |

### Đặt lại mật khẩu qua liên kết — nửa sau của UC-07 (`/api/auth/password-reset`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG92` | 200 | `POST /api/auth/password-reset` | Đặt mật khẩu mới thành công | `Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.` | BE |
| `MSG93` | 400 | `POST /api/auth/password-reset` | Liên kết không hợp lệ, hết hạn, đã dùng, đã bị liên kết mới hơn thay thế, hoặc tài khoản đã bị vô hiệu hoá / không có mật khẩu | `Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quản trị viên.` | BE |
| `MSG94` | 400 | `POST /api/auth/password-reset` | Mật khẩu mới không hợp lệ — kèm `errors.newPassword` | `Mật khẩu mới không hợp lệ.` | BE |
| `MSG129` | — | — (FE, trang `/reset-password` không có `#token=`) | Trang không có token: liên kết thiếu token, hoặc tải lại / mở lại thẻ sau khi FE đã xoá token khỏi URL. **Khác `MSG93`**: liên kết có thể vẫn dùng được, nên không gọi API và không bảo "hết hạn" | `Không tìm thấy mã đặt lại mật khẩu. Hãy bấm lại liên kết trong email.` | FE |

Bốn mã này là của nửa "người dùng mở liên kết". Đặc tả UC-07 đã có đủ (EX-4 → EX-6); sequence diagram
vẫn chỉ vẽ nửa "quản trị viên gửi liên kết".

`MSG01` dùng chung cho `/login` và `/refresh` có chủ ý: cùng tình huống "không xác thực được",
chỉ khác câu chữ. Nhánh "không có quyền truy cập hệ thống" đã bỏ từ `SCRUM-63`
([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)) và **không có mã**.

### Quản lý người dùng — UC-05 … UC-07 (`/api/users`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG07` | 201 | `POST /api/users` | Tạo tài khoản thành công | `Tạo tài khoản thành công.` | BE |
| `MSG08` | 409 | `POST /api/users`, `PUT /api/users/{id}` | Email đã được dùng | `Email đã được sử dụng.` | BE |
| `MSG09` | 400 | `POST`, `PUT /api/users/{id}`, `PUT /api/users/{id}/status` | Thông tin không hợp lệ — kèm `errors` theo field | `Thông tin tài khoản không hợp lệ.` | BE |
| `MSG10` | 200 | `PUT /api/users/{id}` | Cập nhật tài khoản thành công | `Cập nhật tài khoản thành công.` | BE |
| `MSG11` | 200 | `PUT /api/users/{id}/status` | Vô hiệu hoá / kích hoạt lại thành công | `Đổi trạng thái tài khoản thành công.` | BE |
| `MSG12` | 409 | `PUT /api/users/{id}`, `PUT /api/users/{id}/status` | Không được đổi trạng thái / hạ vai trò (tự khoá, quản trị viên cuối cùng) | `Không thể đổi trạng thái tài khoản này.` | BE |
| `MSG13` | 200 | `POST /api/users/{id}/password-reset` | Đã gửi email đặt lại mật khẩu | `Đã gửi email đặt lại mật khẩu.` | BE |
| `MSG14` | 409 | `POST /api/users/{id}/password-reset` | Tài khoản không dùng được chức năng đặt lại mật khẩu (đã bị vô hiệu hoá, hoặc chưa có mật khẩu — chỉ đăng nhập bằng Google) | `Tài khoản này không dùng được chức năng đặt lại mật khẩu.` | BE |
| `MSG15` | 503 | `POST /api/users/{id}/password-reset` | Không gửi được email đặt lại mật khẩu (SMTP tắt, không kết nối được, hoặc từ chối) | `Không gửi được email đặt lại mật khẩu. Vui lòng thử lại.` | BE |

### Ma trận phân quyền — UC-10, UC-11 (`/api/roles/capabilities`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG16` | 200 | `PUT /api/roles/capabilities` | Cập nhật phân quyền thành công (kể cả không đổi gì) | `Cập nhật phân quyền thành công.` | BE |
| `MSG17` | 400 | `PUT /api/roles/capabilities` | Cấu hình không hợp lệ — kèm `errors` (`roleId`, `capabilities`, `adminUsers`) | `Cấu hình phân quyền không hợp lệ.` | BE |
| `MSG18` | 500 | `PUT /api/roles/capabilities` | Database từ chối ghi | `Không thể cập nhật phân quyền. Vui lòng thử lại.` | BE |
| `MSG287` | — | — (FE, sự kiện hub `capabilitiesChanged`) | Quản trị viên vừa đổi vai trò của tài khoản, hoặc đổi quyền của vai trò tài khoản đang giữ; phiên giữ nguyên, menu và nút đổi theo ngay (SCRUM-105) | `Quyền truy cập của bạn vừa được cập nhật.` | FE |

### Quản lý vai trò — UC-12a, UC-12b, UC-12c (`/api/roles`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG19` | 201 | `POST /api/roles` | Tạo vai trò thành công | `Tạo vai trò thành công.` | BE |
| `MSG20` | 400 | `POST /api/roles`, `PUT /api/roles/{id}` | Thông tin vai trò không hợp lệ — kèm `errors` | `Thông tin vai trò không hợp lệ.` | BE |
| `MSG21` | 409 | `POST /api/roles`, `PUT /api/roles/{id}` | Trùng tên vai trò | `Đã có vai trò dùng tên này. Hãy đặt một tên khác.` | BE |
| `MSG22` | 200 | `PUT /api/roles/{id}` | Cập nhật vai trò thành công (kể cả không đổi gì) | `Cập nhật vai trò thành công.` | BE |
| `MSG23` | 200 | `DELETE /api/roles/{id}` | Xoá vai trò thành công | `Xoá vai trò thành công.` | BE |
| `MSG24` | 409 | `PUT /api/roles/{id}`, `DELETE /api/roles/{id}` | Đổi tên hoặc xoá vai trò mặc định | `Đây là vai trò mặc định của hệ thống, không đổi tên hoặc xoá được. Bạn vẫn sửa được quyền của nó.` | BE |
| `MSG25` | 409 | `DELETE /api/roles/{id}` | Vai trò còn tài khoản mang — kèm `userCount` | `Vai trò này vẫn còn {n} tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.` | BE |
| `MSG26` | 500 | `POST /api/roles` | Database từ chối khi tạo | `Không thể tạo vai trò. Vui lòng thử lại.` | BE |
| `MSG27` | 500 | `PUT /api/roles/{id}` | Database từ chối khi sửa | `Không thể cập nhật vai trò. Vui lòng thử lại.` | BE |
| `MSG28` | 500 | `DELETE /api/roles/{id}` | Database từ chối khi xoá | `Không thể xoá vai trò. Vui lòng thử lại.` | BE |

### Quản lý cuộc họp — UC-14 … UC-17 (`/api/meetings`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG29` | 201 | `POST /api/meetings` | Tạo cuộc họp nháp thành công | `Tạo cuộc họp thành công.` | BE |
| `MSG30` | 200 | `PUT /api/meetings/{id}` | Cập nhật cuộc họp thành công | `Cập nhật cuộc họp thành công.` | BE |
| `MSG31` | 400 | `POST /api/meetings`, `PUT /api/meetings/{id}` | Thông tin không hợp lệ — kèm `errors`; PUT với JSON sai cú pháp thì không có `errors` | `Thông tin cuộc họp không hợp lệ.` / `Nội dung JSON không hợp lệ.` | BE |
| `MSG32` | 400 | `GET /api/meetings` | Tiêu chí hiển thị danh sách cuộc họp không hợp lệ — kèm `errors` | `Tiêu chí danh sách cuộc họp không hợp lệ.` | BE |
| `MSG33` | 409 | `PUT /api/meetings/{id}` | Cuộc họp không còn ở trạng thái sửa được | `Cuộc họp này không thể chỉnh sửa được nữa.` | BE |
| `MSG34` | 415 | `PUT /api/meetings/{id}` | Lỗi kỹ thuật khi request không được gửi dưới dạng JSON — ngoài luồng nghiệp vụ UC-15 | `Content-Type phải là application/json.` | BE — phản hồi API kỹ thuật hiện hành |
| `MSG35` | 500 | `POST /api/meetings` | Không lưu được khi tạo | `Không thể tạo cuộc họp. Vui lòng thử lại.` | BE |
| `MSG36` | 500 | `PUT /api/meetings/{id}` | Không lưu được khi sửa | `Không thể cập nhật cuộc họp. Vui lòng thử lại.` | BE |
| `MSG37` | 500 | `GET /api/meetings/{id}`, `PUT /api/meetings/{id}` | Không tải được chi tiết cuộc họp. Ở `GET` gồm cả lỗi khi kiểm tra tài khoản đang hoạt động; ở `PUT` chỉ khi bước kiểm tra trạng thái không đọc được cuộc họp | `Không thể tải thông tin cuộc họp. Vui lòng thử lại.` | BE |
| `MSG38` | 500 | `GET /api/meetings` | Không tải được danh sách (kể cả lỗi khi kiểm tra tài khoản đang hoạt động) | `Không thể tải danh sách cuộc họp. Vui lòng thử lại.` | BE |

"Không tìm thấy cuộc họp" và "không có quyền xem cuộc họp" **không có mã**: API trả `404` không
body để không lộ sự tồn tại của cuộc họp (AGENTS.md, mục *403 hay 404*).

### Thông báo trong ứng dụng — UC-66 … UC-68 (`/api/notifications`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG39` | 400 | `GET /api/notifications` | Không thể hiển thị phần danh sách thông báo được yêu cầu — kèm `errors` | `Trang thông báo được yêu cầu không khả dụng.` | BE |
| `MSG40` | 500 | mọi endpoint `/api/notifications/*` | Lỗi database khi kiểm tra tài khoản đang hoạt động | `Không thể tải thông báo. Vui lòng thử lại.` | BE |

### Luồng cuộc họp — UC-18, UC-19, UC-20 (`/api/meetings`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG41` | 409 | GET readiness, POST SubmitForApproval | Cuộc họp không còn ở Draft hoặc Rejected nên không thể gửi duyệt | `Cuộc họp không còn ở trạng thái Draft hoặc Rejected.` | BE |
| `MSG42` | 503 | POST SubmitForApproval | Không lưu được yêu cầu gửi duyệt | `Không thể chuyển trạng thái cuộc họp. Vui lòng thử lại.` | BE |
| `MSG43` | 200 | POST Approve | Duyệt cuộc họp thành công | `Đã duyệt cuộc họp.` | BE |
| `MSG44` | 200 | POST Reject | Từ chối cuộc họp thành công | `Đã từ chối cuộc họp.` | BE |
| `MSG45` | 200 | POST Publish | Công bố cuộc họp thành công | `Đã công bố cuộc họp.` | BE |
| `MSG46` | 403 | POST Publish | Organizer còn đọc được meeting nhưng mất `MeetingCreator` | `Cuộc họp này không còn có thể công bố.` | BE |
| `MSG46` | 409 | POST Publish | Meeting không còn Approved | `Cuộc họp này không còn có thể công bố.` | BE |
| `MSG47` | 503 | POST Publish | Không lưu được yêu cầu công bố | `Không thể chuyển trạng thái cuộc họp. Vui lòng thử lại.` | BE |
| `MSG78` | 200 | POST SubmitForApproval | Gửi duyệt cuộc họp thành công | `Đã gửi cuộc họp để duyệt.` | BE |
| `MSG79` | 200 | POST ReturnToDraft | Đưa cuộc họp về Draft thành công | `Đã đưa cuộc họp về bản nháp.` | BE |
| `MSG80` | 400 | POST transitions | JSON, action, GUID hoặc field không hợp lệ | `Yêu cầu chuyển trạng thái không hợp lệ.` | BE |
| `MSG81` | 400 | POST SubmitForApproval | Thông tin cấp cuộc họp chưa sẵn sàng — kèm `errors` | `Cuộc họp chưa đủ thông tin để gửi duyệt.` | BE |
| `MSG82` | 400 | POST SubmitForApproval | Người duyệt inactive, thiếu `MeetingApprover` hoặc trùng Organizer | `Người duyệt không hợp lệ hoặc không còn quyền duyệt cuộc họp.` | BE |
| `MSG83` | 400 | POST Reject | Lý do từ chối bị thiếu hoặc chỉ có khoảng trắng | `Cần nhập lý do từ chối.` | BE |
| `MSG84` | 400 | POST Reject | Lý do từ chối dài hơn 1.000 ký tự sau khi trim | `Lý do từ chối không được vượt quá 1.000 ký tự.` | BE |
| `MSG85` | 403 | GET readiness, POST transitions, participant mutations/editor-options | Caller không có quyền thực hiện action | `Bạn không có quyền thực hiện thao tác này.` | BE |
| `MSG86` | 400 | GET history | Query phân trang history không hợp lệ | `Tham số truy vấn không hợp lệ.` | BE |
| `MSG87` | 413 | POST transitions, PUT meeting | Body JSON vượt quá 64 KiB | `Nội dung JSON vượt quá 64 KiB.` | BE |
| `MSG88` | 503 | POST Approve, Reject, ReturnToDraft, TransferOrganizer | Không lưu được transition | `Không thể chuyển trạng thái cuộc họp. Vui lòng thử lại.` | BE |
| `MSG89` | 200 | POST TransferOrganizer | Chuyển meeting Approved từ Organizer inactive sang Organizer mới thành công | `Đã chuyển cuộc họp cho người tổ chức mới.` | BE |
| `MSG130` | 503 | GET approvers | Không tải được danh sách người duyệt hoặc không kiểm tra được quyền truy cập | `Không thể tải danh sách người duyệt. Vui lòng thử lại.` | BE |
| `MSG131` | 503 | GET readiness, POST transitions, GET history | Không kiểm tra được trạng thái active của tài khoản | `Không thể kiểm tra tài khoản để thực hiện thao tác cuộc họp. Vui lòng thử lại.` | BE |
| `MSG132` | 409 | POST Approve, Reject | Approval cycle không còn chờ quyết định | `Trạng thái cuộc họp đã thay đổi hoặc không cho phép thao tác này.` | BE |
| `MSG133` | 409 | POST ReturnToDraft | Trạng thái không cho về Draft hoặc approver đang chờ vẫn hợp lệ | `Trạng thái cuộc họp đã thay đổi hoặc không cho phép thao tác này.` | BE |
| `MSG134` | 400 | POST TransferOrganizer | Organizer mới không hợp lệ, inactive, thiếu MeetingCreator hoặc là Assigned Approver | `Người tổ chức mới không hợp lệ hoặc không còn quyền tạo cuộc họp.` | BE |
| `MSG135` | 409 | POST TransferOrganizer | Meeting không còn Approved hoặc Organizer hiện tại vẫn active | `Trạng thái cuộc họp đã thay đổi hoặc không cho phép thao tác này.` | BE |
| `MSG136` | 503 | GET readiness, GET history | Không tải được readiness/history | `Không thể tải dữ liệu quy trình cuộc họp. Vui lòng thử lại.` | BE |
| `MSG137` | 409 | POST Publish | Không có đúng một Chair active hoặc không có Secretary active | `Cuộc họp cần có đúng một Chủ tọa đang hoạt động và ít nhất một Thư ký đang hoạt động trước khi công bố.` | BE |
| `MSG138` | 200 | `PUT /api/meetings/{id}` | Organizer dời lịch cuộc họp `Published` (UC-15 AF-1): thời gian hoặc địa điểm đã đổi và người được mời đã có thông báo. PUT không đổi gì vẫn trả `MSG30` | `Đã dời lịch cuộc họp. Người được mời sẽ nhận thông báo.` | BE |

### Phòng họp trực tuyến — SCRUM-39, SCRUM-41 (`/api/meetings/{id}/call`)

Dùng các mã UC-37/UC-41 đã dành sẵn ở bảng dưới khi tình huống khớp; tình huống chưa có mã thì lấy
mã mới.

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG117` | 409 | `POST …/call/join`, `POST …/call/participants/{userId}/mute` | Cuộc họp không ở trạng thái `InProgress` | `Phòng họp chỉ mở khi cuộc họp đang diễn ra.` | BE |
| `MSG118` | 500 | `POST …/call/join` | Lỗi database khi kiểm quyền vào phòng | `Không thể vào phòng họp. Vui lòng thử lại.` | BE |
| `MSG123` | 403 | `POST …/call/participants/{userId}/mute` | Người gọi được mời nhưng không phải chủ toạ | `Chỉ chủ toạ mới tắt được micro của người khác.` | BE |
| `MSG124` | 502 | `POST …/call/participants/{userId}/mute` | LiveKit không phản hồi hoặc từ chối | `Máy chủ phòng họp không phản hồi. Vui lòng thử lại.` | BE |
| `MSG280` | 409 | `POST …/call/join` | Cuộc họp hình thức `InPerson` — không có phòng trực tuyến | `Cuộc họp này họp trực tiếp nên không có phòng họp trực tuyến.` | BE |
| `MSG281` | 503 | `POST …/call/join`, `POST …/mute` | Máy chủ chưa cấu hình LiveKit | `Phòng họp trực tuyến chưa sẵn sàng. Vui lòng liên hệ quản trị viên.` | BE |
| `MSG282` | 409 | `POST …/call/participants/{userId}/mute` | Người bị tắt mic không còn trong phòng hoặc không có micro | `Người này không còn trong phòng hoặc chưa bật micro.` | BE |
| `MSG283` | 500 | `GET …/call` | Lỗi database khi tải trạng thái phòng | `Không thể tải trạng thái phòng họp. Vui lòng thử lại.` | BE |
| `MSG284` | 500 | `POST …/call/participants/{userId}/mute` | Lỗi database khi kiểm quyền tắt mic | `Không thể tắt micro. Vui lòng thử lại.` | BE |

Người không được mời vào phòng **không có mã**: API trả `404` không body, như cuộc họp không tồn
tại.


### Dành sẵn, chưa có code

Bảng nguồn phân bổ trong sequence diagram: các mã Agenda đã có endpoint và contract tiếng Việt ở mục Agenda phía trên; các mã còn lại chưa triển khai hoặc đã ngừng dùng như ghi chú từng dòng. Cột *Use case* theo danh mục 70 UC (10/10/2026); mã Meeting Session ghi số UC cũ. Người làm các use case còn lại dùng đúng các mã dưới đây.

| Mã | Use case | Tình huống (theo sơ đồ) |
|---|---|---|
| `MSG48` | ADR-0013 · Create Meeting Session (UC-26 cũ, đã bỏ) | *Meeting session created successfully.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG49` | ADR-0013 · Update Meeting Session (UC-27 cũ, đã bỏ) | *Meeting session updated successfully.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG50` | ADR-0013 · Cancel Meeting Session (UC-28 cũ, đã bỏ) | *Meeting session cancelled successfully.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG51` | ADR-0013 · Start Meeting Session (UC-29 cũ, đã bỏ) | *Meeting session started successfully.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG52` | ADR-0013 · End Meeting Session (UC-30 cũ, đã bỏ) | *Meeting session ended successfully.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG53` | ADR-0013 · Create / Update Meeting Session (UC-26 / UC-27 cũ, đã bỏ) | *Invalid meeting session information.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG54` | ADR-0013 · Create Meeting Session (UC-26 cũ, đã bỏ) | *Meeting sessions can no longer be added to this meeting.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG55` | ADR-0013 · Update Meeting Session (UC-27 cũ, đã bỏ) | *This meeting session can no longer be edited.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG56` | ADR-0013 · Cancel Meeting Session (UC-28 cũ, đã bỏ) | *This meeting session can no longer be cancelled.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG57` | ADR-0013 · Start Meeting Session (UC-29 cũ, đã bỏ) | *Another meeting session is already in progress. End it before starting another session.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG58` | ADR-0013 · Start Meeting Session (UC-29 cũ, đã bỏ) | *This meeting session cannot be started.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG59` | ADR-0013 · End Meeting Session (UC-30 cũ, đã bỏ) | *This meeting session cannot be ended.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG60` | UC-23a Create Agenda Item | *Agenda item created successfully.* |
| `MSG61` | UC-23b Update Agenda Item | *Agenda item updated successfully.* |
| `MSG62` | UC-23c Delete Agenda Item | *Agenda item deleted successfully.* |
| `MSG63` | UC-23d Reorder Agenda Items | *Agenda order updated successfully.* |
| `MSG64` | UC-24 Set Current Agenda Item | *Current agenda item updated successfully.* |
| `MSG65` | UC-25 Complete Agenda Item | *Agenda item completed successfully.* |
| `MSG66` | UC-23a / UC-23b | *Invalid agenda item information.* (Các lỗi trường: *Agenda item title is required.*, *Planned duration must be greater than 0.*, *One or more selected documents are not available to this meeting.*) |
| `MSG67` | UC-23a / UC-23b / UC-23c / UC-23d | *The agenda for this meeting can no longer be modified.* |
| `MSG68` | UC-23a | *Unable to create the agenda item. Please try again.* |
| `MSG69` | UC-23b | *Unable to update the agenda item. Please try again.* |
| `MSG70` | UC-23c | *Unable to delete the agenda item. Please try again.* |
| `MSG71` | UC-23d | *Unable to update the agenda order. Please try again.* |
| `MSG72` | UC-24 | *Another agenda item is already in progress. Complete it before starting another agenda item.* |
| `MSG73` | UC-24 | *This agenda item cannot be set as current.* |
| `MSG74` | UC-25 | *This agenda item cannot be completed.* |
| `MSG75` | UC-24 | *Unable to set the current agenda item. Please try again.* |
| `MSG76` | UC-25 | *Unable to complete the agenda item. Please try again.* |
| `MSG77` | ADR-0013 · Cancel Meeting Session (UC-28 cũ, đã bỏ) | *The last planned meeting session of a published meeting cannot be cancelled.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG90` | UC-20 Công bố cuộc họp | *At least one valid planned Meeting Session is required before publication.* — **Ngừng dùng**; Report 3 source-of-truth xác định Meeting Sessions là tùy chọn. Không tái sử dụng mã. |
| `MSG91` | SCRUM-19 | Mã từng được giữ cho workflow nhưng không còn tình huống backend — **Ngừng dùng**, không tái sử dụng. |
| `MSG95` | UC-27 / UC-30 | *Unable to load meeting participants. Please try again.* |
| `MSG96` | UC-28a Add Meeting Participant | *Meeting participant added successfully.* |
| `MSG97` | UC-28a | *Invalid meeting participant information.* (Lỗi: *The selected user is not available.*, *This user is already a meeting participant.*, *A valid meeting role is required.*, *The Meeting Organizer may only assign a missing Secretary during preparation.*, *Guest name and email are required.*, *Guest email is invalid.*, *This guest email is already invited to the meeting.*, *This email is already represented by an internal meeting participant.*) |
| `MSG98` | UC-28a / UC-28b / UC-28c | *The meeting participant list cannot be modified in its current status.* |
| `MSG99` | UC-28a | *Unable to add the meeting participant. Please try again.* |
| `MSG100` | UC-28b Manage Meeting Role & Additional Permissions | *Meeting role updated successfully.* |
| `MSG101` | UC-28b | *A valid meeting role is required.* |
| `MSG102` | UC-28b, UC-73 / EX-3 | *Unable to update the meeting role. Please try again.* |
| `MSG103` | UC-28c Remove Meeting Participant | *Meeting participant removed successfully.* |
| `MSG104` | UC-28c | *Unable to remove the meeting participant. Please try again.* |
| `MSG105` | UC-29 Respond to Meeting Invitation | *Meeting invitation response recorded successfully.* |
| `MSG106` | UC-29 | *Invalid meeting invitation response.* |
| `MSG107` | UC-29 | *This meeting invitation can no longer be answered.* |
| `MSG108` | UC-29 | *Unable to save the meeting invitation response. Please try again.* |
| `MSG109` | UC-28b / UC-28c | *The meeting must retain its required Chair and Secretary. Assign a replacement first.* |
| `MSG145` | UC-28a / Validation chi tiết · Dành sẵn | *One or more selected participants are invalid. No participants were added.* |
| `MSG146` | UC-28b / Mã dành sẵn, UC tinh gọn dùng MSG102 | *Unable to replace the Chair. Meeting roles remain unchanged.* |
| `MSG147` | UC-31 / Main Flow · Đã triển khai SCRUM-26 | *Meeting attendance saved successfully.* |
| `MSG148` | UC-31 / EX-1 · Đã triển khai SCRUM-26 | *Select Present or Absent for each submitted participant or Guest.* |
| `MSG149` | UC-31 / EX-2 · Đã triển khai SCRUM-26 | *Attendance can only be recorded while the meeting is in progress.* |
| `MSG150` | UC-31 / EX-3 · Đã triển khai SCRUM-26 | *Unable to save attendance. No changes were saved.* |
| `MSG151` | UC-31 / EX-4, BR-172 · Đã triển khai SCRUM-26 | *The meeting participant list has changed. Refresh it and try again.* |
| `MSG152` | UC-28a / EX-1; UC-28b / EX-1 · Ngừng dùng — PO 20/10/2026 | *One or more selected meeting permissions are not allowed.* |
| `MSG153` | UC-28b / Main Flow · Ngừng dùng — PO 20/10/2026 | *Meeting permissions updated successfully.* |
| `MSG154` | UC-28b / Trạng thái UI không có thay đổi, không có EX riêng | *No changes to save.* |
| `MSG155` | UC-22 / EX-3 cũ · Ngừng dùng 13/10/2026 | Bỏ điều kiện điểm danh đủ trước khi kết thúc theo quyết định PO; không tái sử dụng mã. |
| `MSG110` | UC-21 Start Meeting | *Meeting started successfully.* — **Đã triển khai SCRUM-69**, xem HTTP contract phía trên. |
| `MSG111` | UC-22 End Meeting | *Meeting ended successfully.* — **Đã triển khai SCRUM-69**, xem HTTP contract phía trên. |
| `MSG112` | UC-21 | *This meeting cannot be started.* — **Đã triển khai SCRUM-69**, xem HTTP contract phía trên. |
| `MSG113` | ADR-0013 · End Meeting khi còn phiên (UC-44 cũ, đã bỏ) | *Complete or cancel all unfinished meeting sessions before ending the meeting.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG114` | UC-22 | *This meeting cannot be ended.* — **Đã triển khai SCRUM-69**, xem HTTP contract phía trên. |
| `MSG115` | UC-21 | *Unable to start the meeting. Please try again.* — **Đã triển khai SCRUM-69**, xem HTTP contract phía trên. |
| `MSG116` | UC-22 | *Unable to end the meeting. Please try again.* — **Đã triển khai SCRUM-69**, xem HTTP contract phía trên. |
| `MSG117` | UC-37 Join Meeting Room · Đã triển khai SCRUM-39 | *The online meeting room is not available.* |
| `MSG118` | UC-37 · Đã triển khai SCRUM-39 | *Unable to join the meeting room. Please try again.* |
| `MSG119` | UC-39a Manage Microphone | *Microphone access is unavailable. Check your browser or device permissions.* |
| `MSG120` | UC-39b Manage Camera | *Camera access is unavailable. Check your browser or device permissions.* |
| `MSG121` | UC-39c Manage Screen Sharing | *Unable to share your screen.* |
| `MSG122` | UC-40 View Meeting Room Participants | *Unable to load meeting room participants. Please try again.* |
| `MSG123` | UC-41 Moderate Meeting Room · Đã triển khai SCRUM-39 | *You are not allowed to moderate this meeting room.* |
| `MSG124` | UC-41 · Đã triển khai SCRUM-39 | *Unable to apply the room moderation action.* |
| `MSG125` | UC-42 Use In-room Chat | *Unable to send the room message. Please try again.* |
| `MSG126` | UC-04 / EX-2 | *Unable to verify the guest invitation. Check the code and try again.* |
| `MSG127` | ADR-0013 · View Meeting Sessions (UC-53 cũ, đã bỏ) | *Unable to load meeting sessions. Please try again.* — **Ngừng dùng** theo ADR-0013 (bỏ Meeting Session). Không tái sử dụng mã. |
| `MSG128` | UC-26 View Meeting Agenda | *Unable to load the meeting agenda. Please try again.* |
| `MSG139` | UC-22 cũ · Ngừng dùng 13/10/2026 | Không còn gate đóng Vote trước End Meeting; không tái cấp mã. |
| `MSG140` | UC-18 Submit Meeting for Approval · EX-5 — Dành sẵn | *No eligible meeting approver is available. Please contact the administrator.* — `Chưa có người duyệt đủ điều kiện. Vui lòng liên hệ quản trị viên.` |
| `MSG141` | UC-23b / EX-4 · Đã triển khai SCRUM-25 | *Only Title and Description can be changed after an agenda item has started.* — Chỉ được sửa tiêu đề và mô tả sau khi mục nghị sự đã bắt đầu. **Đặc tả đã bỏ 18/10/2026** (PO 16/10/2026: sửa được mọi trường ở mọi trạng thái); BE còn trả cho tới khi SCRUM-25 sửa theo, sau đó chuyển *Ngừng dùng*. |
| `MSG142` | UC-23c / EX-2 · Đã triển khai SCRUM-25 | *Only Pending agenda items can be deleted.* — Chỉ được xóa mục nghị sự chưa bắt đầu. |
| `MSG143` | UC-23d / EX-2 · Đã triển khai SCRUM-25 | *The agenda order is invalid or out of date. Refresh the agenda and try again.* — Thứ tự chương trình nghị sự không hợp lệ hoặc đã cũ. Vui lòng tải lại và thử lại. Câu chữ đổi 18/10/2026 (PO 16/10/2026: sắp xếp mục ở mọi trạng thái); cùng tình huống, giữ mã. |
| `MSG144` | UC-24 / EX-3 · Đã triển khai SCRUM-25 | *Start the first Pending agenda item in the current order.* — Vui lòng bắt đầu mục nghị sự chưa thực hiện đầu tiên theo thứ tự hiện tại. **Đặc tả đã bỏ 18/10/2026** (PO 16/10/2026: bắt đầu được mục Pending bất kỳ); BE còn trả cho tới khi SCRUM-25 sửa theo, sau đó chuyển *Ngừng dùng*. |

`MSG83` và `MSG84` đã được workflow SCRUM-19 dùng. `MSG90` và `MSG91` chỉ được giữ để không tái cấp.


## Bảng đổi mã cũ → mới

### Trong code, test và tài liệu API

| Mã cũ | Nơi dùng | Mã mới |
|---|---|---|
| `MSG01` | `/login`, `/refresh` | `MSG01` (giữ nguyên) |
| `MSG03` | `/login`, `/refresh` — tài khoản bị vô hiệu hoá | `MSG02` |
| `MSG01` | `/google` — token không verify được | `MSG03` |
| `MSG03` | `/google` — chưa liên kết | `MSG05` |
| `MSG04` | `/google` — tài khoản bị vô hiệu hoá | `MSG02` |
| `MSG01` | FE — không đăng xuất được (UC-03) | `MSG06` |
| `MSG18` | `POST /api/roles` — lỗi lưu | `MSG26` |
| `MSG18` | `PUT /api/roles/{id}` — lỗi lưu | `MSG27` |
| `MSG18` | `DELETE /api/roles/{id}` — lỗi lưu | `MSG28` |
| `MEETING_CREATED` | | `MSG29` |
| `MEETING_UPDATED` | | `MSG30` |
| `MEETING_INVALID_INFORMATION` | | `MSG31` |
| `MEETING_INVALID_QUERY` | | `MSG32` |
| `MEETING_NOT_EDITABLE` | | `MSG33` |
| `MEETING_UNSUPPORTED_CONTENT_TYPE` | | `MSG34` |
| `MEETING_CREATE_FAILED` | | `MSG35` |
| `MEETING_UPDATE_FAILED` | | `MSG36` |
| `MEETING_LOAD_FAILED` | | `MSG37` |
| `MEETING_LIST_FAILED` | | `MSG38` |
| `NOTIFICATION_INVALID_QUERY` | | `MSG39` |
| `NOTIFICATION_LOAD_FAILED` | | `MSG40` |
| `MEETING_TRANSITION_COMPLETED` | | `MSG43`, `MSG44`, `MSG45`, `MSG78`, `MSG79` theo action |
| `MEETING_STATUS_CONFLICT` | | `MSG41`, `MSG46`, `MSG132`, `MSG133` theo action |
| `MEETING_INVALID_TRANSITION_REQUEST` | | `MSG80` |
| `MEETING_SUBMISSION_NOT_READY` | | `MSG81` |
| `MEETING_APPROVER_INVALID` | | `MSG82` |
| `MEETING_REJECTION_REASON_REQUIRED` | | `MSG83` |
| `MEETING_REJECTION_REASON_TOO_LONG` | | `MSG84` |
| `MEETING_ACTION_FORBIDDEN` | | `MSG85` |
| `MEETING_INVALID_QUERY` | GET history | `MSG86` |
| `MEETING_BODY_TOO_LARGE` | | `MSG87` |
| `MEETING_WORKFLOW_FAILED` | | `MSG42`, `MSG47`, `MSG88` theo action |
| `MEETING_LOAD_FAILED` | workflow | `MSG130`, `MSG131`, `MSG136` theo endpoint |

`MSG07`–`MSG17`, `MSG19`–`MSG25` và `MSG18` của ma trận phân quyền không đổi.

### Trong đặc tả use case và sequence diagram

Dùng để vẽ lại sơ đồ. Đặc tả UC-01/02/03 trong
[05-use-case-specifications.md](../01-requirements/05-use-case-specifications.md) đã được sửa theo
bảng này; **các file PNG trong `docs/06-sequencediagram/` vẫn mang số mã và số UC cũ**. Cột đầu ghi số UC mới.

| Sơ đồ / đặc tả | Mã cũ | Tình huống | Mã mới |
|---|---|---|---|
| UC-01 SignIn | `MSG01` | Sai email hoặc mật khẩu | `MSG01` |
| UC-01 SignIn | `MSG03` | Tài khoản không khả dụng | `MSG02` |
| UC-01 SignIn | `MSG04` | Không có quyền truy cập hệ thống | bỏ, không có mã |
| UC-02 SignInWithGoogle | `MSG01` | Đăng nhập Google không thành công | `MSG03` |
| UC-02 SignInWithGoogle | `MSG02` | Không xác minh được tài khoản Google | `MSG04` |
| UC-02 SignInWithGoogle | `MSG03` | Tài khoản Google chưa được cấp quyền | `MSG05` |
| UC-02 SignInWithGoogle | `MSG04` | Tài khoản không khả dụng | `MSG02` |
| UC-02 SignInWithGoogle | `MSG05` | Không có quyền truy cập hệ thống | bỏ, không có mã |
| UC-03 SignOut | `MSG01` | Không thể đăng xuất | `MSG06` |
| CreateRole / UpdateRole / DeleteRole | *(không mã)* | Unable to create / update / delete role | `MSG26` / `MSG27` / `MSG28` |
| UC-14 CreateMeeting | *(không mã)* | Meeting created successfully | `MSG29` |
| UC-14 CreateMeeting | `MSG01` | Unable to create the meeting | `MSG35` |
| UC-14, UC-15 | *(không mã)* | Validation errors | `MSG31` |
| UC-15 UpdateMeeting | `MSG02` | Meeting updated successfully | `MSG30` |
| UC-15 UpdateMeeting | `MSG03` | This meeting can no longer be edited | `MSG33` |
| UC-15 UpdateMeeting | `MSG04` | Unable to update the meeting | `MSG36` |
| UC-16 ViewMeetingDetails | `MSG05` | Meeting not found | không mã — `404` không body |
| UC-16 ViewMeetingDetails | *(không mã)* | You do not have permission to view this meeting | không mã — `404` không body |
| UC-16 ViewMeetingDetails | `MSG06` | Unable to load meeting details | `MSG37` |
| UC-17 ViewMeetingList | `MSG08` | Unable to load meetings | `MSG38` |
| UC-18 SubmitMeetingForApproval | `MSG11` | This meeting can no longer be submitted | `MSG41` |
| UC-18 SubmitMeetingForApproval | `MSG12` | Unable to submit the meeting | `MSG42` |
| UC-19 ReviewMeetingApproval | `MSG13` | Meeting approved successfully | `MSG43` |
| UC-19 ReviewMeetingApproval | `MSG14` | Meeting rejected successfully | `MSG44` |
| UC-20 PublishMeeting | `MSG18` | Meeting published successfully | `MSG45` |
| UC-20 PublishMeeting | `MSG19` | This meeting can no longer be published | `MSG46` |
| UC-20 PublishMeeting | `MSG20` | Unable to publish the meeting | `MSG47` |
| UC-66 ViewNotifications | *(không mã)* | Notifications cannot be loaded | `MSG40` — hiện BE chỉ trả mã này khi lỗi ở bước kiểm tra tài khoản; lỗi đọc danh sách vẫn là `500` không body |

Sơ đồ thông báo UC-67a/67b/68 không ghi mã, và các nhánh lỗi lưu "đã đọc" của chúng chưa có mã
riêng trong code: mark-read trả `404` không body khi không thấy thông báo.

**ADR đã chấp nhận không được sửa**, nên
[ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md) và
[ADR-0009](../05-decisions/0009-tu-lien-ket-google-lan-dau-theo-email.md) vẫn ghi số cũ
(`MSG03`/`MSG04`/`MSG05` của UC-01, UC-02) — tra bảng trên khi đọc.

### Tài liệu — FE-07 (UC-32, UC-33, UC-34, UC-35, UC-36, UC-71, UC-72)

Các mã dưới đây **dành sẵn** cho đặc tả ngày 14/10/2026, chưa phải contract đã triển khai.

| Mã | Tình huống / trạng thái | Thông điệp |
|---|---|---|
| `MSG156` | UC-32a / EX-1; UC-32c / EX-1; UC-35 / EX-2 · Dành sẵn | Meeting documents are not accessible. |
| `MSG157` | UC-32a / EX-2 · Dành sẵn | Unable to load the document list. Please try again. |
| `MSG158` | UC-32c / EX-2 · Dành sẵn | Unable to display the document. Please try again. |
| `MSG159` | UC-32c / EX-3 · Dành sẵn | Secret Meeting documents can only be viewed online and cannot be downloaded. |
| `MSG160` | UC-32c / EX-4 · Dành sẵn | Unable to download the document. Please try again. |
| `MSG161` | UC-33a / Normal Flow · Dành sẵn | Documents added to the Meeting successfully. |
| `MSG162` | UC-33a / EX-1 · Dành sẵn | You no longer have permission to add documents to this Meeting. |
| `MSG163` | ADR-0020 · Ngừng dùng | Retired: document addition and Agenda Item linking are separate operations under ADR-0020. Do not reuse. |
| `MSG164` | UC-33a / EX-3; UC-36 / EX-1 · Dành sẵn | Select valid PDF or DOCX files, each no larger than 50 MB. |
| `MSG165` | UC-33a / EX-4 · Dành sẵn | One or more selected documents are already attached to the Meeting. |
| `MSG166` | UC-33a / EX-5 · Dành sẵn | Unable to add the documents. No documents were added. Please try again. |
| `MSG167` | UC-33c / Normal Flow · Dành sẵn | Document removed from the Meeting successfully. |
| `MSG168` | ADR-0020 · Ngừng dùng | Retired: Meeting Minutes no longer lock document removal under ADR-0020. Do not reuse. |
| `MSG169` | UC-33c / EX-2 · Dành sẵn | You no longer have permission to remove documents from this Meeting. |
| `MSG170` | UC-33b, UC-33d / EX-2; UC-33c / EX-3 · Dành sẵn | This document is no longer attached to the Meeting. Refresh the document list. |
| `MSG171` | UC-33c / EX-4 · Dành sẵn | Unable to remove the document. It remains attached to the Meeting. Please try again. |
| `MSG172` | UC-33a / EX-6 · Dành sẵn | Documents cannot be added after the Meeting has ended. |
| `MSG173` | ADR-0020 · Ngừng dùng | Retired: Agenda Item status no longer locks document links under ADR-0020. Do not reuse. |
| `MSG174` | UC-33a / EX-7; UC-33b / EX-7; UC-33c / EX-5; UC-33d / EX-7 · Dành sẵn | This Meeting is not accessible. |
| `MSG175` | UC-34 / EX-2; UC-71a / EX-1; UC-71b / EX-1; UC-71c / EX-1; UC-72b / EX-1; UC-72c / EX-1; UC-72d / EX-1 · Dành sẵn | This personal-library item is not available. |
| `MSG176` | UC-34 / EX-1 · Dành sẵn | Unable to load your personal document library. Please try again. |
| `MSG177` | UC-36 / Normal Flow · Dành sẵn | Documents uploaded to your personal library successfully. |
| `MSG178` | UC-36 / EX-3 · Dành sẵn | Unable to upload the files. No documents were added. Please try again. |
| `MSG179` | UC-36 / EX-2; UC-71c / EX-2; UC-72a / EX-2; UC-72c / EX-2 · Dành sẵn | The selected destination folder is not available in your personal library. |
| `MSG180` | UC-71a / Normal Flow · Dành sẵn | Document deleted from your personal library successfully. |
| `MSG181` | UC-71a / EX-2 · Dành sẵn | Unable to delete the document. Your personal library remains unchanged. |
| `MSG182` | Retired — document-name constraint removed with BR-243 · Ngừng dùng | Retired: no document-name validation is specified under this code. Do not reuse. |
| `MSG183` | UC-71b / Normal Flow · Dành sẵn | Document renamed successfully. |
| `MSG184` | UC-71b / EX-2 · Dành sẵn | Unable to rename the document. Its name remains unchanged. |
| `MSG185` | UC-71c / Normal Flow · Dành sẵn | Document moved successfully. |
| `MSG186` | UC-71c / EX-3 · Dành sẵn | Unable to move the document. Its location remains unchanged. |
| `MSG187` | UC-72a / Normal Flow · Dành sẵn | Folder created successfully. |
| `MSG188` | UC-72a / EX-1; UC-72b / EX-2 · Dành sẵn | Enter a folder name. |
| `MSG189` | UC-72a / EX-3; UC-72c / EX-4 · Dành sẵn | Folders cannot be nested more than five levels deep. |
| `MSG190` | UC-72a / EX-4 · Dành sẵn | Unable to create the folder. No folder was created. |
| `MSG191` | UC-72b / Normal Flow · Dành sẵn | Folder renamed successfully. |
| `MSG192` | UC-72b / EX-3 · Dành sẵn | Unable to rename the folder. Its name remains unchanged. |
| `MSG193` | UC-72c / EX-3 · Dành sẵn | A folder cannot be moved into itself or one of its descendants. |
| `MSG194` | UC-72c / Normal Flow · Dành sẵn | Folder moved successfully. |
| `MSG195` | UC-72c / EX-5 · Dành sẵn | Unable to move the folder. Its location remains unchanged. |
| `MSG196` | UC-72d / EX-2 · Dành sẵn | Only an empty folder can be deleted. |
| `MSG197` | UC-72d / Normal Flow · Dành sẵn | Folder deleted successfully. |
| `MSG198` | UC-72d / EX-3 · Dành sẵn | Unable to delete the folder. The folder remains unchanged. |
| `MSG199` | UC-35 / Normal Flow · Dành sẵn | Comment added successfully. |
| `MSG200` | UC-35 / EX-1 · Dành sẵn | Enter a comment. |
| `MSG201` | Retired — no separate commenting permission · Ngừng dùng | Retired: document-viewing access also permits commenting. Do not reuse. |
| `MSG202` | UC-35 / EX-3 · Dành sẵn | Unable to add the comment. No comment was saved. Please try again. |
| `MSG203` | Retired — mandatory PDF page reference removed · Ngừng dùng | Retired: a PDF comment does not require a page reference. Do not reuse. |
| `MSG204` | UC-35 / EX-4 · Dành sẵn | Unable to load the document comments. Please try again. |


### Biểu quyết — FE-11 (UC-52a, UC-52b, UC-53–UC-57)

Các mã dưới đây được dành sẵn cho đặc tả nghiệp vụ trong Report 3 ngày 14/10/2026; chưa xác nhận endpoint runtime đã triển khai.

| Mã | Nguồn / tình huống | Thông báo chuẩn |
|---|---|---|
| `MSG205` | UC-52a, UC-52b / EX-1; UC-53, UC-54 / EX-6; UC-55 / EX-7; UC-56 / EX-3; UC-57 / EX-5 · Dành sẵn | Meeting votes are not accessible. |
| `MSG206` | UC-52a / EX-2 · Dành sẵn | Unable to load the meeting votes. Please try again. |
| `MSG207` | UC-52a / AF-1 · Dành sẵn | No votes to display. |
| `MSG208` | UC-52b / EX-2 · Dành sẵn | Unable to load the vote status or result. Please try again. |
| `MSG209` | UC-53, UC-54 / Invalid configuration · Dành sẵn | Invalid vote information. Check the highlighted fields. |
| `MSG210` | UC-53 / Normal Flow · Dành sẵn | Vote draft created successfully. |
| `MSG211` | UC-53 / AF-1 · Dành sẵn | Vote draft updated successfully. |
| `MSG212` | UC-53 / AF-2 · Dành sẵn | Vote draft deleted successfully. |
| `MSG213` | UC-53 / EX-2; UC-76a, UC-76b / EX-1 · Dành sẵn | Only Draft votes may be managed by the current Chair or Secretary while the meeting is Approved, Published or InProgress. |
| `MSG214` | UC-53 / EX-3 · Dành sẵn | Unable to create the vote draft. No draft was created. Please try again. |
| `MSG215` | UC-53 / EX-4 · Dành sẵn | Unable to update the vote draft. The draft remains unchanged. Please try again. |
| `MSG216` | UC-53 / EX-5 · Dành sẵn | Unable to delete the vote draft. The draft remains unchanged. Please try again. |
| `MSG217` | UC-54 / Normal Flow · Dành sẵn | Voting opened successfully. |
| `MSG218` | UC-54 / EX-2 · Dành sẵn | Only the current Chair can open a Draft vote while the meeting is InProgress. |
| `MSG219` | UC-54 / EX-3 · Dành sẵn | Another vote is already open in this meeting. Close it before opening this vote. |
| `MSG220` | UC-54 / EX-4 · Dành sẵn | No eligible voters are available for the selected roles. Voting was not opened. |
| `MSG221` | UC-54 / EX-5 · Dành sẵn | Unable to open voting. Voting was not opened. Please try again. |
| `MSG222` | UC-55 / EX-1 · Dành sẵn | Select one option before submitting your ballot. |
| `MSG223` | UC-55 / EX-2 · Dành sẵn | The selected option is not available for this vote. |
| `MSG224` | UC-55 / EX-3 · Dành sẵn | You are not eligible to cast a ballot in this vote. |
| `MSG225` | UC-55 / EX-4 · Dành sẵn | This vote is no longer accepting ballots. |
| `MSG226` | UC-55 / EX-5 · Dành sẵn | Your ballot has already been recorded. It cannot be changed or withdrawn. |
| `MSG227` | UC-55 / Normal Flow · Dành sẵn | Your ballot has been recorded. |
| `MSG228` | UC-55 / EX-6 · Dành sẵn | Unable to record your ballot. No ballot was recorded. Please try again. |
| `MSG229` | UC-56 / Normal Flow · Dành sẵn | Voting closed. The result is available. |
| `MSG230` | UC-56 / EX-1 · Dành sẵn | Only the current Chair can close an Open vote while the meeting is InProgress or Ended. |
| `MSG231` | UC-56 / EX-2 · Dành sẵn | Unable to close voting. The vote and saved ballots remain unchanged. Please try again. |
| `MSG232` | UC-57 / EX-1 · Dành sẵn | Select Passed, NotPassed or Deferred. |
| `MSG233` | UC-57 / EX-2 · Dành sẵn | The vote conclusion has already been recorded and cannot be changed. |
| `MSG234` | UC-57 / EX-3 · Dành sẵn | Only the current Chair can record a conclusion for a Closed vote while the meeting is InProgress or Ended. |
| `MSG235` | UC-57 / Normal Flow · Dành sẵn | Vote conclusion recorded successfully. |
| `MSG236` | UC-57 / EX-4 · Dành sẵn | Unable to record the vote conclusion. No conclusion was recorded. Please try again. |
| `MSG237` | FE-11 · Ngừng dùng | Retired: closing or cancelling the UI does not require a system message. Do not reuse. |

### Tài liệu và liên kết mục nghị sự — ADR-0020 (15/10/2026)

Bảng nguồn đặc tả dưới đây giữ thông điệp tiếng Anh để truy vết. Trạng thái triển khai theo từng dòng; riêng MSG256–MSG260 đã có Backend, contract HTTP và thông điệp tiếng Việt ở bảng phía trên.

| Mã | Nguồn / tình huống | Thông báo chuẩn |
|---|---|---|
| `MSG238` | UC-33b / Normal Flow · Dành sẵn, chưa triển khai | Documents attached to the Agenda Item successfully. |
| `MSG239` | UC-33d / Normal Flow · Dành sẵn, chưa triển khai | Document detached from the Agenda Item successfully. |
| `MSG240` | UC-33b / EX-1 · Dành sẵn, chưa triển khai | You no longer have permission to link documents in this Meeting. |
| `MSG241` | UC-33d / EX-1 · Dành sẵn, chưa triển khai | You no longer have permission to unlink documents in this Meeting. |
| `MSG242` | UC-33b, UC-33d / EX-3 · Dành sẵn, chưa triển khai | Select an available Agenda Item in this Meeting. |
| `MSG243` | UC-33b / EX-4 · Dành sẵn, chưa triển khai | One or more selected documents are already attached to the Agenda Item. |
| `MSG244` | UC-33b / EX-5 · Dành sẵn, chưa triển khai | Unable to link the document. No links were changed. Please try again. |
| `MSG245` | UC-33d / EX-5 · Dành sẵn, chưa triển khai | Unable to unlink the document. No links were changed. Please try again. |
| `MSG246` | UC-33d / EX-4 · Dành sẵn, chưa triển khai | This document is no longer linked to the selected Agenda Item. Refresh the document list. |
| `MSG247` | UC-33b, UC-33d / EX-6 · Dành sẵn, chưa triển khai | Document links cannot be changed after the Meeting has ended. |
| `MSG248` | UC-33c / EX-1 · Dành sẵn, chưa triển khai | Documents cannot be removed after the Meeting has ended. |
| `MSG249` | UC-33a / EX-2 · Dành sẵn, chưa triển khai | Select one or more available documents from your personal library. |
| `MSG250` | UC-23b / EX-4 (cũ) · **Ngừng dùng** 18/10/2026 | *Planned Duration and Presenter cannot be changed after an agenda item has started.* — Ngừng dùng theo PO 16/10/2026: mục nghị sự không còn Planned Duration. Không tái sử dụng mã. |
| `MSG251` | UC-35 / EX-5 · Dành sẵn, chưa triển khai | Comments are read-only after the Meeting has ended. |
| `MSG252` | UC-55 / EX-8 · Dành sẵn, chưa triển khai | Confirm that you have read all vote documents before casting your ballot. |
| `MSG253` | UC-74 / Main Flow · Dành sẵn, chưa triển khai | Document read confirmed. |
| `MSG254` | UC-74 / EX-1 · Dành sẵn, chưa triển khai | Scroll to the last page of the document before confirming that you have read it. |
| `MSG255` | UC-74 / EX-3 · Dành sẵn, chưa triển khai | Unable to confirm document reading. Please try again. |
| `MSG256` | UC-28b / EX-5 · Đã triển khai trong SCRUM-20 / SCRUM-97 | From publication onward, only the current Chair can hand over the Chair role. |
| `MSG257` | UC-73 / Main Flow · Đã triển khai trong SCRUM-20 / SCRUM-97 | Chair role handed over successfully. |
| `MSG258` | UC-73 / AF-1 · Đã triển khai trong SCRUM-97 | No participant can receive the Chair role. Add an internal participant first. |
| `MSG259` | UC-73 / EX-1 · Đã triển khai trong SCRUM-20 / SCRUM-97 | The selected participant can no longer receive the Chair role. Select another participant. |
| `MSG260` | UC-73 / EX-2 · Đã triển khai trong SCRUM-97 | You can no longer hand over the Chair role for this meeting. |
| `MSG261` | UC-75 / Main Flow · Dành sẵn, chưa triển khai | Meeting cancelled successfully. |
| `MSG262` | UC-75 / EX-1 · Dành sẵn, chưa triển khai | Enter a reason for cancelling the meeting. |
| `MSG263` | UC-75 / EX-2 · Dành sẵn, chưa triển khai | This meeting can no longer be cancelled. |
| `MSG264` | UC-75 / EX-3 · Dành sẵn, chưa triển khai | Unable to cancel the meeting. Please try again. |
| `MSG265` | UC-76a / Main Flow · Dành sẵn, chưa triển khai | Documents attached to the vote successfully. |
| `MSG266` | UC-76a / EX-3 · Dành sẵn, chưa triển khai | One or more selected documents are already attached to the vote. |
| `MSG267` | UC-76a / EX-4 · Dành sẵn, chưa triển khai | Unable to attach the documents. No documents were attached. Please try again. |
| `MSG268` | UC-76b / Main Flow · Dành sẵn, chưa triển khai | Document detached from the vote successfully. |
| `MSG269` | UC-76b / EX-2 · Dành sẵn, chưa triển khai | This document is no longer attached to the vote. Refresh the list. |
| `MSG270` | UC-76b / EX-3 · Dành sẵn, chưa triển khai | Unable to detach the document. No changes were made. Please try again. |
| `MSG271` | UC-77 / Main Flow · Dành sẵn, chưa triển khai | Agenda item conclusion saved. |
| `MSG272` | UC-77 / EX-1 · Dành sẵn, chưa triển khai | Enter a conclusion of up to 2,000 characters. |
| `MSG273` | UC-77 / EX-2 · Dành sẵn, chưa triển khai | The conclusion of this agenda item can no longer be changed. |
| `MSG274` | UC-77 / EX-3 · Dành sẵn, chưa triển khai | Unable to save the agenda item conclusion. Please try again. |
| `MSG288` | UC-33a / EX-8, BR-319 · Đã triển khai SCRUM-107 (PO 22/10/2026) | Upload up to 10 files at a time, no larger than 200 MB in total. |

## Ghi chú: mã dùng chung có chủ ý

Các mã sau mang nhiều câu chữ hoặc nhiều nguyên nhân nhưng vẫn **một tình huống** — đúng quy tắc 2:

- `MSG01` — sai thông tin đăng nhập (`/login`) và phiên hết hạn (`/refresh`): cùng là "không xác
  thực được", khác câu chữ.
- `MSG09`, `MSG17`, `MSG20`, `MSG31`, `MSG32`, `MSG39`, `MSG94` — lỗi validation; nguyên nhân cụ thể nằm
  trong `errors` theo key.
- `MSG93` — sáu nguyên nhân (không tồn tại, hết hạn, đã dùng, bị thay thế, tài khoản bị vô hiệu
  hoá, tài khoản không có mật khẩu) gộp một mã có chủ ý: endpoint ẩn danh, phân biệt chúng chỉ giúp người dò liên kết.
- `MSG12` — ba nguyên nhân, xem [02-quan-ly-nguoi-dung-note-cho-fe.md](02-quan-ly-nguoi-dung-note-cho-fe.md).
- `MSG31` — thêm biến thể "Nội dung JSON không hợp lệ." khi PUT gửi JSON sai cú pháp (không có `errors`).
