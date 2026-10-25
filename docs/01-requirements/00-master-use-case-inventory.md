# TrustCabinet — Danh mục Use Case tổng thể & Baseline vẽ Use Case Diagram

> **Baseline:** 10/10/2026 — danh sách UC đã **gom và đánh số lại** theo Report 1, Report 2, Report 3 (bản DOCX
> 10/10/2026), sau quyết định PO 09/10/2026 (bỏ Meeting Session, biểu quyết chỉ trong cuộc họp).
> **Mục đích:** nguồn chuẩn về số UC, phạm vi Feature, Actor và quan hệ `<<extend>>` / `<<include>>` để vẽ
> Use Case Diagram và đặt tên ticket.
> **Liên kết:** [Đặc tả Use Case](05-use-case-specifications.md) · [Business Rules](06-business-rules.md) ·
> [Luồng nghiệp vụ chính](../02-design/04-luong-nghiep-vu-chinh.md) · [Business Flow BF-01 → BF-04](../09-bussinessflow/README.md) ·
> [ERD](../02-design/02-erd.md) · [Use Case Diagram](../assets/use-case-diagrams/README.md).
> **Nguồn chuẩn:** [Report 1](../08-reports/Report-1_Project_Introduction.md) (phạm vi FE-01 → FE-15),
> [Report 2](../08-reports/Report-2_Project_Management_Plan.md) (kế hoạch),
> [Report 3](../08-reports/Report-3_Software_Requirement_Specification.md) (UC, BR, quyền).

**Bản trích hiện tại: 15 Feature, 73 Use Case (UC-01 → UC-72 và UC-77).** UC-77 được bổ sung từ Report 3 ngày 24/10/2026 trong SCRUM-98; các UC bổ sung khác ngoài phạm vi task này chờ đồng bộ riêng. Cập nhật FE-07/FE-11 ngày 15/10/2026:
UC-32b vẫn bỏ; UC-33b được tái cấp cho gắn tài liệu vào mục nghị sự, bổ sung UC-33d để bỏ gắn một mục
theo [ADR-0020](../05-decisions/0020-tai-lieu-theo-quyen-xem-cuoc-hop.md). FE-07 có 16 UC chi tiết;
FE-11 có 7 UC chi tiết. Số UC gom giữ nguyên.

## Các quyết định đã áp dụng

| # | Quyết định | Hệ quả trên danh mục UC |
|---|---|---|
| D1 | **Bỏ Meeting Session.** Một cuộc họp = một buổi họp; lịch (`PlannedStartTime`, `PlannedEndTime`), agenda, biểu quyết, ghi âm, biên bản gắn thẳng vào Meeting ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)) | Feature *Meeting Session Management* bị xoá; 6 UC phiên họp không còn. Lịch nhập khi tạo cuộc họp (UC-14); sau `Published` chỉ dời được giờ, địa điểm, link (UC-15 — *Reschedule*) |
| D2 | **Chỉ mở và gửi phiếu trong cuộc họp.** Chair chuẩn bị nháp ở Approved/Published/InProgress; sau Ended vẫn đóng/kiểm phiếu cũ và ghi kết luận. Không time-boxed, quorum hoặc Resolution ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md), [ADR-0017](../05-decisions/0017-ket-thuc-hop-khong-phu-thuoc-diem-danh-bieu-quyet.md), [ADR-0019](../05-decisions/0019-chuan-bi-bieu-quyet-truoc-khi-hop.md)) | FE-11 *Voting Management*; Vote thuộc Meeting, không gắn mục nghị sự. UC-57: Chair ghi `Passed` / `NotPassed` / `Deferred`, ghi rồi không sửa |
| D3 | **Secretary tự upload audio.** Không có vai trò Audio Uploader | UC-44 chỉ có actor Secretary |
| D4 | **Bỏ uỷ quyền tham dự (delegation).** Phản hồi lời mời chỉ còn `Accepted` / `Declined` (BR-105) | UC-29 không còn nhánh Delegated |
| D5 | **Gom UC nhỏ.** Các thao tác CRUD cùng một màn hình được gom thành một UC, tách thành UC con có hậu tố chữ (`UC-06a`, `UC-06b`…) | Danh mục từ 94 UC (88 đang dùng) xuống **70 UC**; toàn bộ UC được đánh số lại liền mạch theo Feature |
| D6 | **Tài liệu không có phiên bản** (ADR-0018), cập nhật quyền đọc và liên kết theo [ADR-0020](../05-decisions/0020-tai-lieu-theo-quyen-xem-cuoc-hop.md) | UC-32b vẫn bỏ; UC-33b tái cấp cho gắn mục, UC-33d bỏ gắn một mục. Ai xem được Meeting thì đọc được tài liệu. Chair/Secretary thêm, gỡ và sửa liên kết trước khi kết thúc; một tài liệu gắn nhiều mục. Sau Ended tài liệu và bình luận chỉ đọc. Thư mục tự tạo dùng ngày bắt đầu dự kiến, giờ Việt Nam |

> **Quy ước số:** từ baseline này **UC-01 → UC-70 đóng băng số**. Thêm UC mới thì lấy `UC-71` trở đi; thêm thao tác vào
> một UC gom thì lấy hậu tố chữ kế tiếp. Số UC cũ (trước 10/10/2026) chỉ còn dùng để tra lịch sử — xem [§5](#5-bảng-đổi-số-uc-cũ--mới).
> Commit, ticket Jira, tên file sequence diagram từ nay dùng số mới.

---

# 1. Quy ước quan hệ UML

- **`<<extend>>`** — hành động tuỳ chọn mở ra từ một UC nền đã tự hoàn chỉnh. UC dạng *View/List* thường là UC nền;
  CRUD hoặc thao tác nghiệp vụ mở từ màn hình đó extend UC nền. Mũi tên đi **từ UC mở rộng → UC nền**.
- **`<<include>>`** — chỉ dùng khi UC **bắt buộc** chạy một hành vi dùng chung mỗi lần. Không dùng `include` chỉ vì cần
  đăng nhập, vì UC trước tạo dữ liệu cho UC sau, hay vì hai UC là hai bước trong vòng đời.
- **Vòng đời không phải `include/extend`.** Chuỗi *Tạo → Gửi duyệt → Duyệt → Publish → Start → End* thể hiện bằng State /
  Activity / Sequence Diagram, không bằng quan hệ UC (xem [§6](#6-vòng-đời--phụ-thuộc)).
- **Guest:** UC-29, UC-32 và UC-37 có cả actor nội bộ lẫn Guest, nên diagram tổng **không** vẽ `include UC-04` vô điều kiện.
  Trên diagram riêng của Guest có thể ghi `UC-29 / UC-32 / UC-37 [Guest path] --<<include>>--> UC-04`.

# 2. Actor

| Actor | Vai trò |
|---|---|
| **System Admin** | Quản trị tài khoản, vai trò, quyền và nhật ký kiểm toán |
| **Meeting Organizer** | Tạo, cập nhật, gửi duyệt, công bố và dời lịch cuộc họp mình tổ chức; gán Thư ký khi chưa có hoặc không còn trong giai đoạn chuẩn bị |
| **Meeting Approver** | Duyệt / từ chối yêu cầu duyệt được phân công cho mình |
| **Chair** | Chủ toạ: bắt đầu / kết thúc họp, điều khiển agenda, tạo – mở – đóng biểu quyết, ghi kết luận, điều phối phòng họp |
| **Secretary** | Thư ký: agenda, thành phần, điểm danh, tài liệu, upload audio, transcript, tóm tắt, biên bản |
| **Participant** | Thành viên được mời: phản hồi lời mời, vào phòng họp, xem tài liệu, bình luận, bỏ phiếu, ký biên bản khi được chỉ định |
| **Guest** | Khách ngoài, không có tài khoản; truy cập đúng một cuộc họp qua lời mời và mã xác minh email (UC-04) |
| *Google Identity Provider* | Actor hệ thống ngoài của UC-02 |
| *AI / Speech Processing Provider* | Actor hệ thống ngoài: audio → transcript có speaker → bản nháp tóm tắt (UC-44 → UC-47) |
| *Email / SMTP Service* | Actor hệ thống ngoài: email đặt lại mật khẩu, thư mời, email liên quan ký biên bản |

Chair, Secretary, Participant là **vai trò trong cuộc họp** (`MeetingParticipant.MeetingRole`), độc lập với vai trò hệ
thống. Quyền chi tiết theo actor nằm ở [Report 3 §I.4.4 Permission Matrix](../08-reports/Report-3_Software_Requirement_Specification.md#44-permission-matrix).

# 3. Ưu tiên

FE-01 → FE-13 là **Must-have**; FE-14 → FE-15 là **Should-have** (theo Report 1).

---

# 4. Danh mục Use Case theo Feature

Cột **Đặc tả**: *Đã đặc tả* = Report 3 có bảng đặc tả; *Placeholder* = đã trong phạm vi, chưa có đặc tả chi tiết (BE viết
đặc tả cơ bản khi nhận ticket).

## FE-01 — Xác thực & Bảo mật tài khoản *(Authentication & Account Security)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-01 | Đăng nhập bằng Email và Mật khẩu *(Sign in with Email and Password)* | Internal user | Đã đặc tả | Độc lập |
| UC-02 | Đăng nhập bằng Google *(Sign in with Google)* | Internal user; Google Identity Provider | Đã đặc tả | Độc lập |
| UC-03 | Đăng xuất *(Sign Out)* | Internal user | Đã đặc tả | Độc lập |
| UC-04 | Truy cập cuộc họp qua lời mời Guest *(Access Meeting via Guest Invitation)* | Guest; Email / SMTP | Đã đặc tả | Độc lập — xác minh email + profile tự khai, guest access cho UC-29/32/35/37/55 |

## FE-02 — Quản lý Người dùng & Hồ sơ *(User & Profile Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-05 | Xem danh sách tài khoản *(View User Accounts)* | System Admin | Đã đặc tả | Nền |
| UC-06 | Quản lý tài khoản *(Manage User Accounts)* — gồm UC-06a Tạo, UC-06b Cập nhật, UC-06c Vô hiệu hoá / Kích hoạt lại | System Admin | Đã đặc tả | `<<extend>>` UC-05 |
| UC-07 | Đặt lại mật khẩu *(Reset User Password)* | System Admin; Email / SMTP | Đã đặc tả | `<<extend>>` UC-05 |
| UC-08 | Gán vai trò hệ thống *(Assign System Role to User)* | System Admin | Đã đặc tả | `<<extend>>` UC-05 |
| UC-09 | Mở khoá tài khoản *(Unlock User Account)* | System Admin | Đã đặc tả | `<<extend>>` UC-05 |

## FE-03 — Kiểm soát truy cập theo vai trò *(Role-Based Access Control)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-10 | Xem ma trận vai trò & quyền *(View Role & Permission Matrix)* | System Admin | Đã đặc tả | Nền |
| UC-11 | Cấu hình quyền của vai trò *(Configure Role Permissions)* | System Admin | Đã đặc tả | `<<extend>>` UC-10 |
| UC-12 | Quản lý vai trò *(Manage Roles)* — gồm UC-12a Tạo, UC-12b Cập nhật, UC-12c Xoá | System Admin | Đã đặc tả | `<<extend>>` UC-10 |
| UC-13 | Xem nhật ký kiểm toán *(View Audit Log)* | System Admin | Đã đặc tả | Độc lập |

## FE-04 — Quản lý cuộc họp *(Meeting Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-14 | Tạo cuộc họp *(Create Meeting)* — kèm lịch, hình thức, địa điểm, link tuỳ chọn | Meeting Organizer | Đã đặc tả | `<<extend>>` UC-17 |
| UC-15 | Cập nhật / dời lịch cuộc họp *(Update Meeting)* — sửa khi `Draft`/`Rejected`; *Reschedule* khi `Published` | Meeting Organizer | Đã đặc tả | `<<extend>>` UC-16 |
| UC-16 | Xem chi tiết cuộc họp *(View Meeting Details)* | Actor có quan hệ với cuộc họp | Đã đặc tả | `<<extend>>` UC-17 |
| UC-17 | Xem danh sách cuộc họp *(View Meeting List)* | Actor có quan hệ với cuộc họp | Đã đặc tả | Nền |
| UC-18 | Gửi duyệt cuộc họp *(Submit Meeting for Approval)* | Meeting Organizer | Đã đặc tả | `<<extend>>` UC-16 |
| UC-19 | Duyệt / Từ chối cuộc họp *(Review Meeting Approval)* | Meeting Approver | Đã đặc tả | Độc lập |
| UC-20 | Công bố cuộc họp *(Publish Meeting)* | Meeting Organizer; Email / SMTP | Đã đặc tả | `<<extend>>` UC-16 |
| UC-21 | Bắt đầu cuộc họp *(Start Meeting)* | Chair | Đã đặc tả | `<<extend>>` UC-16 |
| UC-22 | Kết thúc cuộc họp *(End Meeting)* — không phụ thuộc Vote Open hay điểm danh đủ/thiếu | Chair | Đã đặc tả | `<<extend>>` UC-16 |

## FE-05 — Quản lý chương trình nghị sự *(Agenda Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-23 | Quản lý mục agenda *(Manage Agenda Items)* — gồm UC-23a Tạo, UC-23b Cập nhật, UC-23c Xoá, UC-23d Sắp xếp | Secretary | Đã đặc tả | `<<extend>>` UC-26 |
| UC-24 | Đặt mục agenda hiện tại *(Set Current Agenda Item)* | Chair | Đã đặc tả | `<<extend>>` UC-26 |
| UC-25 | Hoàn thành mục agenda *(Complete Agenda Item)* | Chair | Đã đặc tả | `<<extend>>` UC-26 |
| UC-26 | Xem chương trình nghị sự *(View Meeting Agenda)* | Actor có quan hệ với cuộc họp | Đã đặc tả | `<<extend>>` UC-16 |

| UC-77 | Ghi kết luận mục nghị sự *(Record Agenda Item Conclusion)* | Chair, Secretary | Đã đặc tả trong Report 3 FE-05; đồng bộ SCRUM-98 | `<<extend>>` UC-26 |

## FE-06 — Quản lý thành phần tham dự *(Meeting Participant Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-27 | Xem thành phần tham dự *(View Meeting Participants)* | Secretary, Chair, Participant, Guest | Đã đặc tả | `<<extend>>` UC-16 |
| UC-28 | Quản lý thành phần tham dự *(Manage Meeting Participants)* — gồm UC-28a thêm nhiều người nội bộ cùng vai trò/quyền hoặc mời từng Guest, UC-28b Quản lý vai trò & quyền bổ sung, UC-28c Xoá / thu hồi | Meeting Organizer, Secretary | Đã đặc tả | Secretary: `<<extend>>` UC-27; Organizer gán Thư ký khi chưa có hoặc không còn trong giai đoạn chuẩn bị: `<<extend>>` UC-16 |
| UC-29 | Phản hồi lời mời họp *(Respond to Meeting Invitation)* | Chair, Secretary, Participant, Guest | Đã đặc tả | Độc lập; Guest đi qua UC-04 |
| UC-30 | Xem trạng thái phản hồi *(View Participant Response Status)* | Secretary, Chair, Participant, Guest | Đã đặc tả | Mở từ cuộc họp có thể truy cập; Member/Guest cần quyền xem phản hồi. Thấy danh tính trong dòng phản hồi không cấp quyền xem hoặc tìm trong roster |
| UC-31 | Ghi nhận điểm danh *(Record Attendance)* — Secretary lưu Present/Absent cho một/nhiều người được chọn khi `InProgress` | Secretary | Đã đặc tả | `<<extend>>` UC-27 |

## FE-07 — Kho tài liệu cá nhân & Tài liệu cuộc họp *(Personal Document Library & Meeting Document Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-32 | Xem tài liệu cuộc họp *(View Meeting Documents)* — UC-32a Xem danh sách, UC-32c Xem trước/tải về; quyền đọc theo quyền xem Meeting. `Secret` không tải về; UC-32b vẫn bỏ | Người có quyền xem Meeting, gồm Guest có lời mời đã xác minh còn hiệu lực | Đã đặc tả các UC con | Internal: `<<extend>>` UC-16; Guest qua UC-04 |
| UC-33 | Quản lý tài liệu cuộc họp *(Manage Meeting Documents)* — UC-33a Thêm một hoặc nhiều tài liệu cùng lần, UC-33b Gắn vào mục (tái cấp theo ADR-0020), UC-33c Gỡ khỏi cuộc họp, UC-33d Bỏ gắn một mục; khoá khi Meeting kết thúc. Nhóm tài liệu thêm cùng lần thành công toàn bộ hoặc không thêm tài liệu nào | Chair, Secretary hiện hành có quyền xem Meeting | Đã đặc tả bốn UC con | `<<extend>>` UC-32 |
| UC-34 | Xem kho tài liệu cá nhân *(View Personal Document Library)* | Internal user | Đã đặc tả | Độc lập |
| UC-35 | Bình luận tài liệu cuộc họp *(Comment on Meeting Document)* — người có quyền đọc được bình luận trước khi Meeting kết thúc; sau đó chỉ đọc | System Admin, Organizer, Approver, Chair, Secretary, Participant, Guest có quyền xem Meeting | Đã đặc tả | `<<extend>>` UC-32 |
| UC-36 | Tải tài liệu lên kho cá nhân *(Upload Document to Personal Library)* — một hoặc nhiều PDF/DOCX cùng lần, tối đa 50 MB mỗi file, vào thư mục đang chọn; cả nhóm thành công hoặc không thêm tài liệu nào | Internal user | Đã đặc tả | `<<extend>>` UC-34 |
| UC-71 | Quản lý tài liệu trong kho *(Manage Personal Documents)* — gồm UC-71a Xoá khỏi kho (tài liệu đã gắn vào cuộc họp vẫn giữ nguyên), UC-71b Đổi tên, UC-71c Chuyển thư mục | Internal user | Đã đặc tả các UC con | `<<extend>>` UC-34 |
| UC-72 | Quản lý thư mục kho *(Manage Library Folders)* — gồm UC-72a Tạo, UC-72b Đổi tên, UC-72c Chuyển, UC-72d Xoá thư mục rỗng; lồng tối đa 5 cấp | Internal user | Đã đặc tả các UC con | `<<extend>>` UC-34 |

## FE-08 — Phòng họp trực tuyến *(Online Meeting Room Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-37 | Vào phòng họp *(Join Meeting Room)* | Organizer, Approver, Chair, Secretary, Participant có phân công; Guest | Placeholder | Internal: `<<extend>>` UC-16; Guest qua UC-04 |
| UC-38 | Rời phòng họp *(Leave Meeting Room)* | User đang trong phòng | Placeholder | Độc lập khi đã kết nối |
| UC-39 | Điều khiển trong phòng *(Manage Meeting Room Controls)* — gồm UC-39a Micro, UC-39b Camera, UC-39c Chia sẻ màn hình | User đang trong phòng | Placeholder | Độc lập khi đã kết nối |
| UC-40 | Xem người đang trong phòng *(View Meeting Room Participants)* | User đang trong phòng | Placeholder | Độc lập khi đã kết nối |
| UC-41 | Điều phối phòng họp *(Moderate Meeting Room)* | Chair | Placeholder | Độc lập khi đã kết nối |
| UC-42 | Chat trong phòng *(Use In-room Chat)* | User đang trong phòng | Placeholder | Độc lập khi đã kết nối |

## FE-09 — Upload audio & Thư viện media *(Meeting Audio Upload & Media Library)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-43 | Xem & phát audio cuộc họp *(View & Play Meeting Audio)* — gồm UC-43a Xem thư viện media, UC-43b Phát audio | User có quyền | Placeholder | `<<extend>>` UC-16 |
| UC-44 | Upload audio cuộc họp *(Upload Meeting Audio)* — chỉ sau khi `Ended`, có xác nhận đồng ý ghi âm | **Secretary**; AI / Speech Provider | Placeholder | `<<extend>>` UC-43 |

## FE-10 — Transcript có speaker & Tóm tắt *(Speaker-Attributed Transcription & Meeting Summary)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-45 | Xem transcript có speaker *(View Speaker-Attributed Transcript)* | Secretary | Placeholder | `<<extend>>` UC-16 |
| UC-46 | Chỉnh transcript & speaker *(Edit Transcript & Speaker Attribution)* | Secretary | Placeholder | `<<extend>>` UC-45 |
| UC-47 | Sinh / sinh lại tóm tắt *(Generate / Regenerate Meeting Summary)* | Secretary; AI / Speech Provider | Placeholder | `<<extend>>` UC-45 |
| UC-48 | Xem tóm tắt *(View Meeting Summary)* | Secretary | Placeholder | `<<extend>>` UC-16 |
| UC-49 | Chỉnh tóm tắt *(Edit Meeting Summary)* | Secretary | Placeholder | `<<extend>>` UC-48 |
| UC-50 | Duyệt tóm tắt *(Approve Meeting Summary)* | Secretary | Placeholder | `<<extend>>` UC-48 |
| UC-51 | Công bố tóm tắt *(Publish Meeting Summary)* — bước riêng sau khi duyệt | Secretary | Placeholder | `<<extend>>` UC-48 |

## FE-11 — Biểu quyết *(Voting Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-52 | Xem biểu quyết *(View Meeting Votes)* — gồm UC-52a Xem danh sách, UC-52b Xem trạng thái / kết quả (xem được cả sau khi `Ended`); nháp chỉ Chair hiện hành thấy | User có quyền | Đã đặc tả các UC con | `<<extend>>` UC-16 |
| UC-53 | Tạo / cấu hình biểu quyết *(Create / Configure Vote)* — tạo, sửa và xoá nháp khi `Approved`, `Published`, `InProgress`; Chair chọn vai trò được bỏ phiếu | Chair | Đã đặc tả | `<<extend>>` UC-52 |
| UC-54 | Mở biểu quyết *(Open Vote)* — chỉ `InProgress`, tối đa một Vote Open mỗi Meeting, ít nhất một người đủ điều kiện | Chair | Đã đặc tả | `<<extend>>` UC-52 |
| UC-55 | Bỏ phiếu *(Cast Vote)* — người trong danh sách chốt lúc mở và còn quyền truy cập; Guest xác minh qua lời mời | Chair, Secretary, Participant, Guest | Đã đặc tả | `<<extend>>` UC-52 |
| UC-56 | Đóng biểu quyết *(Close Vote)* — khi `InProgress` hoặc sau `Ended` | Chair | Đã đặc tả | `<<extend>>` UC-52 |
| UC-57 | Ghi kết luận biểu quyết *(Record Vote Conclusion)* — Chair chọn `Passed` / `NotPassed` / `Deferred`; ghi rồi không sửa | Chair | Đã đặc tả | `<<extend>>` UC-52 |

## FE-12 — Biên bản & Ký điện tử *(Meeting Minutes & Digital Signing)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-58 | Xem biên bản *(View Meeting Minutes)* | User có quyền | Placeholder | `<<extend>>` UC-16 |
| UC-59 | Sinh bản nháp biên bản *(Generate Meeting Minutes)* — từ tóm tắt đã duyệt, điểm danh, agenda, kết quả và kết luận biểu quyết | Secretary | Placeholder | `<<extend>>` UC-58 |
| UC-60 | Chỉnh biên bản *(Edit Meeting Minutes)* | Secretary | Placeholder | `<<extend>>` UC-58 |
| UC-61 | Gửi ký biên bản *(Submit Meeting Minutes for Signing)* | Secretary; Email / SMTP | Placeholder | `<<extend>>` UC-58 |
| UC-62 | Ký biên bản *(Sign Meeting Minutes)* | Người ký được chỉ định | Placeholder | `<<extend>>` UC-58 |
| UC-63 | Xem trạng thái ký & lịch sử *(View Signature Status & Audit Trail)* | User có quyền | Placeholder | `<<extend>>` UC-58 |
| UC-64 | Hoàn tất / niêm phong biên bản *(Finalize Meeting Minutes)* | **Secretary** | Placeholder | `<<extend>>` UC-58 |
| UC-65 | Yêu cầu sửa biên bản *(Request Revision of Meeting Minutes)* | Người ký được chỉ định | Placeholder | `<<extend>>` UC-58 |

## FE-13 — Email & Thông báo trong ứng dụng *(Email & In-App Notification)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-66 | Xem thông báo *(View Notifications)* | Internal user | Đã đặc tả | Nền |
| UC-67 | Đánh dấu đã đọc *(Mark Notifications as Read)* — gồm UC-67a Một thông báo, UC-67b Tất cả | Internal user | Đã đặc tả | `<<extend>>` UC-66 |
| UC-68 | Mở đối tượng từ thông báo *(Open Notification Target)* | Internal user | Đã đặc tả | `<<extend>>` UC-66 |

## FE-14 — Dashboard cá nhân & Không gian họp *(Personal Dashboard & Meeting Workspace)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-69 | Xem dashboard cá nhân & không gian họp *(View Personal Dashboard & Meeting Workspace)* | Internal user | Placeholder | Độc lập |

## FE-15 — Báo cáo & Thống kê *(Reporting & Statistics)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-70 | Xem & xuất báo cáo *(View & Export Reports)* — gồm UC-70a Xem, UC-70b Xuất | User có quyền báo cáo | Placeholder | Độc lập (UC-70b `<<extend>>` UC-70a) |

---

# 5. Bảng đổi số UC cũ → mới

Số cũ là số trong danh mục 94 UC (baseline 05/10/2026), dùng trong commit, ticket và sơ đồ trước 10/10/2026.

| Cũ | Mới | | Cũ | Mới | | Cũ | Mới |
|---|---|---|---|---|---|---|---|
| UC-01 | UC-01 | | UC-33 | UC-23c | | UC-65 | UC-46 |
| UC-02 | UC-02 | | UC-34 | UC-23d | | UC-66 | UC-48 |
| UC-03 | UC-03 | | UC-35 | UC-24 | | UC-67 | UC-49 |
| UC-04 | UC-05 | | UC-36 | UC-25 | | UC-68 | UC-50 |
| UC-05 | UC-06a | | UC-37 | UC-27 | | UC-69 | UC-51 |
| UC-06 | UC-06b | | UC-38 | UC-28a | | UC-70 | UC-52a |
| UC-07 | UC-06c | | UC-39 | UC-28b | | UC-71 | UC-53 |
| UC-08 | UC-07 | | UC-40 | UC-28c | | UC-72 | UC-54 |
| UC-09 | UC-08 | | UC-41 | UC-29 | | UC-73 | UC-55 |
| UC-10 | UC-10 | | UC-42 | UC-30 | | UC-74 | UC-52b |
| UC-11 | UC-11 | | UC-43 | UC-21 | | UC-75 | UC-56 |
| UC-12 | UC-12a | | UC-44 | UC-22 | | UC-76 | UC-57 |
| UC-13 | UC-12b | | UC-45 | UC-37 | | UC-77 | UC-58 |
| UC-14 | UC-12c | | UC-46 | UC-38 | | UC-78 | UC-59 |
| UC-15 | UC-14 | | UC-47 | UC-39a | | UC-79 | UC-60 |
| UC-16 | UC-15 | | UC-48 | UC-39b | | UC-80 | UC-61 |
| UC-17 | UC-16 | | UC-49 | UC-39c | | UC-81 | UC-62 |
| UC-18 | UC-17 | | UC-50 | UC-40 | | UC-82 | UC-63 |
| UC-19 | UC-18 | | UC-51 | UC-41 | | UC-83 | UC-64 |
| UC-20 | UC-19 | | UC-52 | UC-42 | | UC-84 | UC-69 |
| UC-21 | UC-20 | | UC-53 | *bỏ* (View Meeting Sessions) | | UC-85 | UC-70a |
| UC-22 | UC-66 | | UC-54 | UC-26 | | UC-86 | UC-70b |
| UC-23 | UC-67a | | UC-55 | UC-09 | | UC-87 | UC-04 |
| UC-24 | UC-67b | | UC-56 | UC-32a | | UC-88 | UC-31 |
| UC-25 | UC-68 | | UC-57 | UC-33a | | UC-89 | UC-34 |
| UC-26 | *bỏ* (Create Meeting Session → nhập lịch trong UC-14) | | UC-58 | UC-33b *(bỏ 13/10/2026)* | | UC-90 | UC-47 |
| UC-27 | *bỏ* (Update Meeting Session → *Reschedule* trong UC-15) | | UC-59 | UC-32b *(bỏ 13/10/2026)* | | UC-91 | UC-35 |
| UC-28 | *bỏ* (Cancel Meeting Session) | | UC-60 | UC-32c | | UC-92 | UC-13 |
| UC-29 | *bỏ* (Start Meeting Session → UC-21) | | UC-61 | UC-43a | | UC-93 | UC-65 |
| UC-30 | *bỏ* (End Meeting Session → UC-22) | | UC-62 | UC-44 | | UC-94 | UC-36 |
| UC-31 | UC-23a | | UC-63 | UC-43b | | | |
| UC-32 | UC-23b | | UC-64 | UC-45 | | | |

Feature cũ → mới: FE-01 → FE-04 giữ số; **FE-05 Meeting Session Management bị bỏ**; FE-06 → FE-05, FE-07 → FE-06, FE-08 → FE-07,
FE-09 → FE-08, FE-10 → FE-09, FE-11 → FE-10, FE-12 → FE-11, FE-13 → FE-12, FE-14 → FE-13, FE-15 → FE-14, FE-16 → FE-15.

Cột *mới* là số sau ngày 10/10/2026. `UC-71`, `UC-72` mới (thêm 13/10/2026, kho tài liệu) không liên quan `UC-71`, `UC-72` cũ ở cột trái.

> Bảng `00-danh-muc-ma-thong-bao.md` vẫn ghi số UC cũ ở cột UC; tra bảng này khi đọc.

---

# 6. Vòng đời / phụ thuộc

Chi tiết theo luồng nghiệp vụ ở [04-luong-nghiep-vu-chinh.md](../02-design/04-luong-nghiep-vu-chinh.md) và
[01-vong-doi-cuoc-hop.md](../02-design/01-vong-doi-cuoc-hop.md).

```text
Meeting
UC-14 Create → Draft ──UC-18 Submit──> WaitingApproval ──UC-19──> Approved | Rejected
   ↑ UC-15 Update (Draft, Rejected)                                   │          └─ UC-15 sửa → UC-18 gửi lại
                                                                       └─UC-20 Publish──> Published
                                    UC-15 Reschedule (giờ, địa điểm, link; báo lại người được mời) ─┘
Published ──UC-21 Start──> InProgress ──UC-22 End (không phụ thuộc phiếu đang mở)──> Ended

Agenda Item (thuộc trực tiếp Meeting)
UC-23a Create → Pending ── UC-24 Set Current → InProgress ── UC-25 Complete → Done
UC-23b/c/d sửa, xoá, sắp xếp khi Meeting chưa Ended; UC-22 End tự hoàn tất mục đang chạy

Guest
UC-28a mời → email → UC-04 xác minh mã → tự khai tên/chức vụ → quyền Member trong cuộc họp → UC-29 / UC-32 / UC-35 / UC-37–42 / UC-55

Document
UC-36 Upload vào kho cá nhân → UC-34 → UC-33a chọn từ kho (hoặc upload thẳng từ máy) → UC-33c gỡ khi cần đổi file
UC-72 thư mục, UC-71 đổi tên / chuyển / xoá trong kho. Xoá khỏi kho: cuộc họp vẫn giữ tham chiếu riêng; file không còn ai tham chiếu thì job nền dọn.
Tải thẳng từ máy vào cuộc họp: dòng kho vào thư mục `<tên cuộc họp> - dd/mm/yyyy` của người tải

Audio → Tóm tắt (sau khi Ended)
UC-44 Secretary upload audio → AI transcript có speaker → UC-45 Xem → UC-46 Sửa → UC-47 Sinh tóm tắt
→ UC-48 Xem → UC-49 Sửa → UC-50 Duyệt → UC-51 Công bố

Biểu quyết (ADR-0019: chuẩn bị nháp trước khi họp)
UC-53 Tạo/cấu hình nháp khi Approved / Published / InProgress
→ UC-54 Mở khi InProgress → UC-55 Bỏ phiếu khi InProgress → UC-56 Đóng → hệ thống kiểm phiếu → UC-57 Chair ghi kết luận
Sau Ended: không nhận phiếu mới; Chair vẫn UC-56 đóng phiếu còn mở và UC-57 ghi kết luận (ADR-0017). Phiếu Draft xoá được trong UC-53

Biên bản
UC-59 Sinh nháp → UC-60 Sửa → UC-61 Gửi ký → UC-62 Ký | UC-65 Yêu cầu sửa (chữ ký cũ mất hiệu lực, vòng ký mới)
→ đủ chữ ký → UC-64 Secretary niêm phong
```

# 7. Thống kê

| FE | Feature | Số UC | UC con |
|---|---|---:|---:|
| FE-01 | Authentication & Account Security | 4 | — |
| FE-02 | User & Profile Management | 5 | 3 |
| FE-03 | Role-Based Access Control | 4 | 3 |
| FE-04 | Meeting Management | 9 | — |
| FE-05 | Agenda Management | 5 | 4 |
| FE-06 | Meeting Participant Management | 5 | 3 |
| FE-07 | Personal Document Library & Meeting Document Management | 7 | 11 |
| FE-08 | Online Meeting Room Management | 6 | 3 |
| FE-09 | Meeting Audio Upload & Media Library | 2 | 2 |
| FE-10 | Speaker-Attributed Transcription & Meeting Summary | 7 | — |
| FE-11 | Voting Management | 6 | 2 |
| FE-12 | Meeting Minutes & Digital Signing | 8 | — |
| FE-13 | Email & In-App Notification | 3 | 2 |
| FE-14 | Personal Dashboard & Meeting Workspace | 1 | — |
| FE-15 | Reporting & Statistics | 1 | 2 |
| **Tổng** | | **73** | **35** |

# 8. Ngoài phạm vi UC

- Chỉ có trong code, chưa thành UC: *ReturnToDraft*, *TransferOrganizer* (endpoint workflow SCRUM-19). Cần PO/BA quyết định
  promote hay dọn code.
- Không làm (Report 1): theo dõi task / action item, nhận dạng giọng nói (voiceprint), ký HSM thật, triển khai air-gapped,
  tích hợp eOffice, nhiều tổ chức / đơn vị ([ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md)).
- Lịch sử trạng thái, lịch tuần / tháng: là màn hình / luồng phụ của UC-16, UC-17, không tách UC.

Guest 13/10/2026: [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md); UC-27/30 cần quyền xem tương ứng như Member, UC-40 xem người đang kết nối mặc định. BR-122 đã chốt; xem [quyết định mới](../05-decisions/0017-ket-thuc-hop-khong-phu-thuoc-diem-danh-bieu-quyet.md).

Phân phạm vi task: UC-27–UC-30 thuộc roster/lời mời/phản hồi SCRUM-20; UC-31 điểm danh giữ riêng ở SCRUM-26. Xoá/revoke ở SCRUM-20 bảo toàn lịch sử response/attendance. Sequence UC-04 có tại [auth/README](../06-sequencediagram/auth/README.md).
