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

## Mã tiếp theo

**`MSG138`** — workflow SCRUM-19 dùng `MSG41`–`MSG47`, `MSG78`–`MSG89` và `MSG130`–`MSG137`; `MSG90`–`MSG91` ngừng dùng, không tái cấp. `MSG48`–`MSG77` và `MSG95`–`MSG128` đã dành cho Meeting Session, Agenda, participant, room và các màn đọc liên quan. Đặt lại mật khẩu UC-08 dùng `MSG92`–`MSG94` và `MSG129` (FE).

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

### Đặt lại mật khẩu qua liên kết — nửa sau của UC-08 (`/api/auth/password-reset`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG92` | 200 | `POST /api/auth/password-reset` | Đặt mật khẩu mới thành công | `Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.` | BE |
| `MSG93` | 400 | `POST /api/auth/password-reset` | Liên kết không hợp lệ, hết hạn, đã dùng, đã bị liên kết mới hơn thay thế, hoặc tài khoản đã bị vô hiệu hoá / không có mật khẩu | `Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quản trị viên.` | BE |
| `MSG94` | 400 | `POST /api/auth/password-reset` | Mật khẩu mới không hợp lệ — kèm `errors.newPassword` | `Mật khẩu mới không hợp lệ.` | BE |
| `MSG129` | — | — (FE, trang `/reset-password` không có `#token=`) | Trang không có token: liên kết thiếu token, hoặc tải lại / mở lại thẻ sau khi FE đã xoá token khỏi URL. **Khác `MSG93`**: liên kết có thể vẫn dùng được, nên không gọi API và không bảo "hết hạn" | `Không tìm thấy mã đặt lại mật khẩu. Hãy bấm lại liên kết trong email.` | FE |

Đặc tả UC-08 chỉ có nửa "quản trị viên gửi liên kết"; bốn mã này là của nửa "người dùng mở liên kết",
đặc tả và sơ đồ chưa vẽ. Ghi lại để BA bổ sung ở `BA Final Review`.

`MSG01` dùng chung cho `/login` và `/refresh` có chủ ý: cùng tình huống "không xác thực được",
chỉ khác câu chữ. Nhánh "không có quyền truy cập hệ thống" đã bỏ từ `SCRUM-63`
([ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md)) và **không có mã**.

### Quản lý người dùng — UC-04 … UC-08 (`/api/users`)

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

### Ma trận phân quyền — UC-09, UC-10 (`/api/roles/capabilities`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG16` | 200 | `PUT /api/roles/capabilities` | Cập nhật phân quyền thành công (kể cả không đổi gì) | `Cập nhật phân quyền thành công.` | BE |
| `MSG17` | 400 | `PUT /api/roles/capabilities` | Cấu hình không hợp lệ — kèm `errors` (`roleId`, `capabilities`, `adminUsers`) | `Cấu hình phân quyền không hợp lệ.` | BE |
| `MSG18` | 500 | `PUT /api/roles/capabilities` | Database từ chối ghi | `Không thể cập nhật phân quyền. Vui lòng thử lại.` | BE |

### Quản lý vai trò — UC-12, UC-13, UC-14 (`/api/roles`)

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

### Quản lý cuộc họp — UC-15 … UC-18 (`/api/meetings`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG29` | 201 | `POST /api/meetings` | Tạo cuộc họp nháp thành công | `Tạo cuộc họp thành công.` | BE |
| `MSG30` | 200 | `PUT /api/meetings/{id}` | Cập nhật cuộc họp thành công | `Cập nhật cuộc họp thành công.` | BE |
| `MSG31` | 400 | `POST /api/meetings`, `PUT /api/meetings/{id}` | Thông tin không hợp lệ — kèm `errors`; PUT với JSON sai cú pháp thì không có `errors` | `Thông tin cuộc họp không hợp lệ.` / `Nội dung JSON không hợp lệ.` | BE |
| `MSG32` | 400 | `GET /api/meetings` | Tiêu chí hiển thị danh sách cuộc họp không hợp lệ — kèm `errors` | `Tiêu chí danh sách cuộc họp không hợp lệ.` | BE |
| `MSG33` | 409 | `PUT /api/meetings/{id}` | Cuộc họp không còn ở trạng thái sửa được | `Cuộc họp này không thể chỉnh sửa được nữa.` | BE |
| `MSG34` | 415 | `PUT /api/meetings/{id}` | Lỗi kỹ thuật khi request không được gửi dưới dạng JSON — ngoài luồng nghiệp vụ UC-16 | `Content-Type phải là application/json.` | BE — phản hồi API kỹ thuật hiện hành |
| `MSG35` | 500 | `POST /api/meetings` | Không lưu được khi tạo | `Không thể tạo cuộc họp. Vui lòng thử lại.` | BE |
| `MSG36` | 500 | `PUT /api/meetings/{id}` | Không lưu được khi sửa | `Không thể cập nhật cuộc họp. Vui lòng thử lại.` | BE |
| `MSG37` | 500 | `GET /api/meetings/{id}`, `PUT /api/meetings/{id}` | Không tải được chi tiết cuộc họp. Ở `GET` gồm cả lỗi khi kiểm tra tài khoản đang hoạt động; ở `PUT` chỉ khi bước kiểm tra trạng thái không đọc được cuộc họp | `Không thể tải thông tin cuộc họp. Vui lòng thử lại.` | BE |
| `MSG38` | 500 | `GET /api/meetings` | Không tải được danh sách (kể cả lỗi khi kiểm tra tài khoản đang hoạt động) | `Không thể tải danh sách cuộc họp. Vui lòng thử lại.` | BE |

"Không tìm thấy cuộc họp" và "không có quyền xem cuộc họp" **không có mã**: API trả `404` không
body để không lộ sự tồn tại của cuộc họp (AGENTS.md, mục *403 hay 404*).

### Thông báo trong ứng dụng — UC-22 … UC-25 (`/api/notifications`)

| Mã | HTTP | Endpoint | Tình huống | `message` | Trạng thái |
|---|---|---|---|---|---|
| `MSG39` | 400 | `GET /api/notifications` | Không thể hiển thị phần danh sách thông báo được yêu cầu — kèm `errors` | `Trang thông báo được yêu cầu không khả dụng.` | BE |
| `MSG40` | 500 | mọi endpoint `/api/notifications/*` | Lỗi database khi kiểm tra tài khoản đang hoạt động | `Không thể tải thông báo. Vui lòng thử lại.` | BE |

### Luồng cuộc họp — UC-19, UC-20, UC-21 (`/api/meetings`)

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
| `MSG85` | 403 | GET readiness, POST transitions | Caller không có quyền thực hiện action | `Bạn không có quyền thực hiện thao tác này.` | BE |
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

### Dành sẵn, chưa có code

Có trong sequence diagram cuộc họp nhưng chưa có endpoint. Người làm các use case này dùng đúng
các mã dưới đây; `message` tiếng Việt đặt khi code.

| Mã | Use case | Tình huống (theo sơ đồ) |
|---|---|---|
| `MSG48` | UC-26 Create Meeting Session | *Meeting session created successfully.* |
| `MSG49` | UC-27 Update Meeting Session | *Meeting session updated successfully.* |
| `MSG50` | UC-28 Cancel Meeting Session | *Meeting session cancelled successfully.* |
| `MSG51` | UC-29 Start Meeting Session | *Meeting session started successfully.* |
| `MSG52` | UC-30 End Meeting Session | *Meeting session ended successfully.* |
| `MSG53` | UC-26 / UC-27 | *Invalid meeting session information.* |
| `MSG54` | UC-26 | *Meeting sessions can no longer be added to this meeting.* |
| `MSG55` | UC-27 | *This meeting session can no longer be edited.* |
| `MSG56` | UC-28 | *This meeting session can no longer be cancelled.* |
| `MSG57` | UC-29 | *Another meeting session is already in progress. End it before starting another session.* |
| `MSG58` | UC-29 | *This meeting session cannot be started.* |
| `MSG59` | UC-30 | *This meeting session cannot be ended.* |
| `MSG60` | UC-31 Create Agenda Item | *Agenda item created successfully.* |
| `MSG61` | UC-32 Update Agenda Item | *Agenda item updated successfully.* |
| `MSG62` | UC-33 Delete Agenda Item | *Agenda item deleted successfully.* |
| `MSG63` | UC-34 Reorder Agenda Items | *Agenda order updated successfully.* |
| `MSG64` | UC-35 Set Current Agenda Item | *Current agenda item updated successfully.* |
| `MSG65` | UC-36 Complete Agenda Item | *Agenda item completed successfully.* |
| `MSG66` | UC-31 / UC-32 | *Invalid agenda item information.* (Các lỗi trường: *Agenda item title is required.*, *Planned duration must be greater than 0.*, *One or more selected documents are not available to this meeting.*) |
| `MSG67` | UC-31 / UC-32 / UC-33 / UC-34 | *The agenda for this meeting session can no longer be modified.* |
| `MSG68` | UC-31 | *Unable to create the agenda item. Please try again.* |
| `MSG69` | UC-32 | *Unable to update the agenda item. Please try again.* |
| `MSG70` | UC-33 | *Unable to delete the agenda item. Please try again.* |
| `MSG71` | UC-34 | *Unable to update the agenda order. Please try again.* |
| `MSG72` | UC-35 | *Another agenda item is already in progress. Complete it before starting another agenda item.* |
| `MSG73` | UC-35 | *This agenda item cannot be set as current.* |
| `MSG74` | UC-36 | *This agenda item cannot be completed.* |
| `MSG75` | UC-35 | *Unable to set the current agenda item. Please try again.* |
| `MSG76` | UC-36 | *Unable to complete the agenda item. Please try again.* |
| `MSG77` | UC-28 | *The last planned meeting session of a published meeting cannot be cancelled.* |
| `MSG90` | UC-21 Phát hành cuộc họp | *At least one valid planned Meeting Session is required before publication.* — **Ngừng dùng**; Report 3 source-of-truth xác định Meeting Sessions là tùy chọn. Không tái sử dụng mã. |
| `MSG91` | SCRUM-19 | Mã từng được giữ cho workflow nhưng không còn tình huống backend — **Ngừng dùng**, không tái sử dụng. |
| `MSG95`–`MSG109` | UC-37–UC-42 F08 | Mã tải roster, thêm participant, role, removal, response và readiness; xem đặc tả UC. |
| `MSG110`–`MSG116` | UC-43–UC-44 Start/End Meeting | Mã thành công, điều kiện không hợp lệ và lỗi hệ thống; xem đặc tả UC. |
| `MSG117`–`MSG126` | UC-45–UC-52 GOF09 Online Meeting Room | Mã trạng thái room, thiết bị, chia sẻ, presence, moderation, chat và xác minh Guest; xem đặc tả UC. |
| `MSG127` | UC-53 View Meeting Sessions | *Unable to load meeting sessions. Please try again.* |
| `MSG128` | UC-54 View Meeting Agenda | *Unable to load the meeting agenda. Please try again.* |

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
bảng này; **các file PNG trong `docs/06-sequencediagram/` vẫn mang số cũ**.

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
| UC-15 CreateMeeting | *(không mã)* | Meeting created successfully | `MSG29` |
| UC-15 CreateMeeting | `MSG01` | Unable to create the meeting | `MSG35` |
| UC-15, UC-16 | *(không mã)* | Validation errors | `MSG31` |
| UC-16 UpdateMeeting | `MSG02` | Meeting updated successfully | `MSG30` |
| UC-16 UpdateMeeting | `MSG03` | This meeting can no longer be edited | `MSG33` |
| UC-16 UpdateMeeting | `MSG04` | Unable to update the meeting | `MSG36` |
| UC-17 ViewMeetingDetails | `MSG05` | Meeting not found | không mã — `404` không body |
| UC-17 ViewMeetingDetails | *(không mã)* | You do not have permission to view this meeting | không mã — `404` không body |
| UC-17 ViewMeetingDetails | `MSG06` | Unable to load meeting details | `MSG37` |
| UC-18 ViewMeetingList | `MSG08` | Unable to load meetings | `MSG38` |
| UC-19 SubmitMeetingForApproval | `MSG11` | This meeting can no longer be submitted | `MSG41` |
| UC-19 SubmitMeetingForApproval | `MSG12` | Unable to submit the meeting | `MSG42` |
| UC-20 ReviewMeetingApproval | `MSG13` | Meeting approved successfully | `MSG43` |
| UC-20 ReviewMeetingApproval | `MSG14` | Meeting rejected successfully | `MSG44` |
| UC-21 PublishMeeting | `MSG18` | Meeting published successfully | `MSG45` |
| UC-21 PublishMeeting | `MSG19` | This meeting can no longer be published | `MSG46` |
| UC-21 PublishMeeting | `MSG20` | Unable to publish the meeting | `MSG47` |
| UC-22 ViewNotifications | *(không mã)* | Notifications cannot be loaded | `MSG40` — hiện BE chỉ trả mã này khi lỗi ở bước kiểm tra tài khoản; lỗi đọc danh sách vẫn là `500` không body |

Sơ đồ thông báo UC-19/20/21 không ghi mã, và các nhánh lỗi lưu "đã đọc" của chúng chưa có mã
riêng trong code: mark-read trả `404` không body khi không thấy thông báo.

**ADR đã chấp nhận không được sửa**, nên
[ADR-0008](../05-decisions/0008-vai-tro-la-du-lieu.md) và
[ADR-0009](../05-decisions/0009-tu-lien-ket-google-lan-dau-theo-email.md) vẫn ghi số cũ
(`MSG03`/`MSG04`/`MSG05` của UC-01, UC-02) — tra bảng trên khi đọc.

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
