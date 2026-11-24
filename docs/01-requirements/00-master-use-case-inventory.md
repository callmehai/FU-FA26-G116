# TrustCabinet — Danh mục Use Case tổng thể & Baseline vẽ Use Case Diagram

> **Baseline:** 10/10/2026 — danh sách UC đã **gom và đánh số lại** theo Report 1, Report 2, Report 3 (bản DOCX
> 10/10/2026), sau quyết định PO 09/10/2026 (bỏ Meeting Session, biểu quyết chỉ trong cuộc họp).
> **Mục đích:** nguồn chuẩn về số UC, phạm vi Feature, Actor và quan hệ `<<extend>>` / `<<include>>` để vẽ
> Use Case Diagram và đặt tên ticket.
> **Liên kết:** [Đặc tả Use Case](05-use-case-specifications.md) · [Business Rules](06-business-rules.md) ·
> [Luồng nghiệp vụ chính](../02-design/04-luong-nghiep-vu-chinh.md) · [Business Flow BF-01 → BF-04](../09-bussinessflow/README.md) ·
> [ERD](../02-design/02-erd.md) · [Use Case Diagram](../14-uc-diagram/README.md).
> **Nguồn chuẩn:** [Report 1](../08-reports/Report-1_Project_Introduction.md) (phạm vi FE-01 → FE-15),
> [Report 2](../08-reports/Report-2_Project_Management_Plan.md) (kế hoạch),
> [Report 3](../08-reports/Report-3_Software_Requirement_Specification.md) (UC, BR, quyền).

**Baseline hiện tại: 16 Feature, UC-01 → UC-81** — mã đánh số lại ngày 24/11/2026 theo ADR-0079, tra mã cũ ở [bảng 10](10-bang-anh-xa-uc-dot-1.md) (FE-16 thêm ngày 07/11/2026; UC-28 thuộc SCRUM-30; UC-75 là Tạo bản đính chính biên bản, đã đặc tả ở SCRUM-33; UC-57 là Thư ký thu Voice ID hộ, SCRUM-152). Cập nhật FE-07/FE-11 ngày 15/10/2026:
UC-32b cũ vẫn bỏ; UC-36b được tái cấp cho gắn tài liệu vào mục nghị sự, bổ sung UC-36d để bỏ gắn một mục
theo [ADR-0020](../05-decisions/0020-tai-lieu-theo-quyen-xem-cuoc-hop.md). FE-07 có 16 UC chi tiết;
FE-11 có 9 UC chi tiết.

Bổ sung ngày 24/10/2026 (đợt BA Final Review, đã có trong Report 3 §I.4 và §II): **UC-34** Hand Over Chair
to Another Participant, **UC-42** Confirm Document Read, **UC-22** Cancel Meeting, **UC-68** Manage Vote
Documents (UC-68a, UC-68b), **UC-27** Record Agenda Item Conclusion. Các UC này nối tiếp dải số lúc đó, không
đánh lại các số đã có (trước đợt đánh số lại 24/11/2026).

Bổ sung ngày 25/10/2026 (rà soát FE-08 với code): **UC-45d** Choose Audio and Video Devices (UC con của
UC-45) và **UC-49** Keep Meeting in a Floating Window (`<<extend>>` UC-43). UC-43 → UC-48 nay đã đặc tả
đầy đủ; UC-47 chỉ còn nghĩa "Chủ toạ tắt mic người khác", không gồm giơ tay (xem UC-50), tắt mic tất cả hay mời ra khỏi
phòng.

Bổ sung ngày 29/10/2026 (BA Final Review FE-08, SCRUM-27): **UC-50** Register to Speak — giơ tay xin phát biểu,
Chủ toạ mời từng người theo lượt (`<<extend>>` UC-43); là yêu cầu, chưa cài (mới có bảng `speaking_requests`).
Gọi 1–1 (SCRUM-40) đã bỏ khỏi phạm vi nên không có UC nào cho nó.

Bổ sung ngày 30/10/2026 (BA Final Review FE-09/FE-10): tổ chức lại theo code as-built (ghi âm theo hình thức họp ADR-0040, mic phòng từng lượt ADR-0041, mic từng người ADR-0038/0039, Voice ID ADR-0042). Thêm **UC-53** Manage My Recorded Speech, **UC-56** Manage My Voice ID (UC-56a–UC-56f), **UC-55d** Delete a Sentence. UC-51, UC-52, UC-54, UC-55 đổi nội dung và tách các UC con (UC-51a–c, UC-52a–e, UC-54a–c, UC-55a–d). UC-58–UC-61 tạm giữ dưới FE-10.

Bổ sung ngày 13/11/2026 (BA Final Review SCRUM-152, ADR-0060): **UC-57** Collect Voice Samples for Attendees (UC-57a–UC-57d) — Thư ký thu giọng mẫu hộ bằng micro phòng họp cho người dự đã đồng ý Voice ID theo thông báo 1.3; mẫu thu hộ thuộc về người được thu, mỗi bộ (tự thu / thu hộ) tối đa 5 mẫu dưới một lần đồng ý.

Bổ sung ngày 22/11/2026 (note 58, PR #466–#471, #473, #474, #476; ADR-0072–ADR-0076, chờ PO duyệt ADR-0074/0076): **UC-57** chuyển thành người có quyền "Thu giọng mẫu hộ" (mặc định Quản trị viên) thu ở màn Quản trị → Người dùng, cần đồng ý thường trực 1.4+ và xác nhận của người được thu cho từng lần thu (thiếu xác nhận → `400 MSG483`); bỏ đường Thư ký thu trong cuộc họp, bỏ giới hạn 5 mẫu mỗi người thu (giữ 50 mẫu mỗi người, `MSG476`; `MSG477` ngừng dùng). Thông báo Voice ID hiện tại 1.5. Ngưỡng tách người nói/gợi ý: 0,72/0,8 và 0,6/0,05/0,7/0,7/0,8, tự gán 0,6. Chỉ còn 3 vai trò hệ thống (Thành viên, Phê duyệt viên, Quản trị viên); Chủ toạ/Thư ký chỉ là vai trò trong cuộc họp; bỏ quyền "Soạn biên bản".

## Các quyết định đã áp dụng

| # | Quyết định | Hệ quả trên danh mục UC |
|---|---|---|
| D1 | **Bỏ Meeting Session.** Một cuộc họp = một buổi họp; lịch (`PlannedStartTime`, `PlannedEndTime`), agenda, biểu quyết, ghi âm, biên bản gắn thẳng vào Meeting ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)) | Feature *Meeting Session Management* bị xoá; 6 UC phiên họp không còn. Lịch nhập khi tạo cuộc họp (UC-13); sau `Published` chỉ dời được giờ, địa điểm, link (UC-14 — *Reschedule*) |
| D2 | **Chỉ mở và gửi phiếu trong cuộc họp.** Chair chuẩn bị nháp ở Approved/Published/InProgress; sau Ended vẫn đóng/kiểm phiếu cũ và ghi kết luận. Không time-boxed, quorum hoặc Resolution ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md), [ADR-0017](../05-decisions/0017-ket-thuc-hop-khong-phu-thuoc-diem-danh-bieu-quyet.md), [ADR-0019](../05-decisions/0019-chuan-bi-bieu-quyet-truoc-khi-hop.md)) | FE-11 *Voting Management*; Vote thuộc Meeting, không gắn mục nghị sự. UC-67: Chair ghi `Passed` / `NotPassed` / `Deferred`, ghi rồi không sửa |
| D3 | **Cách ghi suy ra từ hình thức họp** (ADR-0040); mic phòng Thư ký ghi từng lượt nối thành một bản ghi (ADR-0041); mic từng người tự ghi khi bật mic (ADR-0038/0039). Thư ký tải tệp audio khi cần | FE-09 *Meeting Recording*; Thư ký điều khiển mic phòng (UC-52a/b), xóa lượt (UC-52d), xóa bản ghi (UC-52e) hoặc upload tệp (UC-52c); người dự quản lý câu của mình (UC-53). Không có vai trò Audio Uploader riêng |
| D4 | **Bỏ uỷ quyền tham dự (delegation).** Phản hồi lời mời chỉ còn `Accepted` / `Declined` (BR-046) | UC-31 không còn nhánh Delegated |
| D5 | **Gom UC nhỏ.** Các thao tác CRUD cùng một màn hình được gom thành một UC, tách thành UC con có hậu tố chữ (`UC-06a`, `UC-06b`…) | Danh mục từ 94 UC (88 đang dùng) xuống **70 UC**; toàn bộ UC được đánh số lại liền mạch theo Feature |
| D6 | **Tài liệu không có phiên bản** (ADR-0018), cập nhật quyền đọc và liên kết theo [ADR-0020](../05-decisions/0020-tai-lieu-theo-quyen-xem-cuoc-hop.md) | UC-32b cũ vẫn bỏ; UC-36b tái cấp cho gắn mục, UC-36d bỏ gắn một mục. Ai xem được Meeting thì đọc được tài liệu. Chair/Secretary thêm, gỡ và sửa liên kết trước khi kết thúc; một tài liệu gắn nhiều mục. Sau Ended tài liệu và bình luận chỉ đọc. Thư mục tự tạo dùng ngày bắt đầu dự kiến, giờ Việt Nam |

> **Quy ước số:** từ 24/11/2026 mã UC theo đợt đánh số lại của ADR-0079 (**UC-01 → UC-81**, [bảng 10](10-bang-anh-xa-uc-dot-1.md)). Thêm UC mới thì lấy `UC-82` trở đi; thêm thao tác vào
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
- **Guest:** UC-31, UC-35, UC-43 có cả actor nội bộ lẫn Guest, nên diagram tổng **không** vẽ `include UC-04` vô điều kiện.
  Trên diagram riêng của Guest có thể ghi `UC-31 / UC-35 / UC-43 [Guest path] --<<include>>--> UC-04`.

# 2. Actor

| Actor | Vai trò |
|---|---|
| **System Admin** | Quản trị tài khoản, vai trò, quyền và nhật ký kiểm toán |
| **Meeting Organizer** | Tạo, cập nhật, gửi duyệt, công bố và dời lịch cuộc họp mình tổ chức; gán Thư ký khi chưa có hoặc không còn trong giai đoạn chuẩn bị |
| **Meeting Approver** | Duyệt / từ chối yêu cầu duyệt được phân công cho mình |
| **Chair** | Chủ toạ: bắt đầu / kết thúc họp, điều khiển agenda, tạo – mở – đóng biểu quyết, ghi kết luận, điều phối phòng họp |
| **Secretary** | Thư ký: agenda, thành phần, điểm danh, tài liệu, upload audio, transcript, tóm tắt, biên bản |
| **Participant** | Thành viên được mời: phản hồi lời mời, vào phòng họp, xem tài liệu, bình luận, bỏ phiếu, đọc và tải biên bản đã ban hành |
| **Guest** | Khách ngoài, không có tài khoản; truy cập đúng một cuộc họp qua lời mời và mã xác minh email (UC-04) |
| *OAuth Provider* | Actor hệ thống ngoài: xác thực tài khoản Google và trả danh tính đã xác minh (UC-02) |
| *AI Drafting Service* | Actor hệ thống: máy AI của nhóm chạy trong hệ thống, viết dự thảo 3 mục của cuộc họp từ bản gỡ băng, chương trình và kết quả biểu quyết (UC-58, PR #260). Chỉ nhận chữ, không nhận âm thanh; lời thoại không ra khỏi hệ thống (ADR-0054, BR-129) |
| *Resend Email* | Actor hệ thống ngoài: gửi email đặt lại mật khẩu, thư mời, thông báo |
| *Cloudflare R2* | Actor hệ thống ngoài: kho đối tượng giữ byte tệp tài liệu cá nhân và tài liệu cuộc họp (ADR-0024) |

**Khoá actor (05/11/2026): 7 actor người và 3 actor hệ thống ngoài, không thêm bớt khi chưa có quyết định của PO.**
Actor người: System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant, Guest. Actor ngoài:
OAuth Provider, Resend Email, Cloudflare R2. Quy tắc để một bên là actor ngoài: nó là hệ thống
bên ngoài ranh giới TrustCabinet mà backend gọi ra (hoặc nó gọi vào) trong luồng của một UC. Vì vậy **không phải
actor**: gỡ băng, Voice ID và máy AI viết dự thảo (chạy trên máy của nhóm, trong mạng nội bộ, không gọi dịch vụ AI
bên ngoài, ADR-0026/0036/0054), LiveKit (hạ tầng
phòng họp), PostgreSQL, Redis và SMTP dự phòng của Resend Email. "Internal user" và "System" không có trong bảng
actor của Report 3 §4.1; các UC còn ghi hai tên này sẽ được thay bằng danh sách vai trò khi BA rà tiếp.

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
| UC-01 | Đăng nhập bằng Email và Mật khẩu *(Sign in with Email and Password)* | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả | Độc lập |
| UC-02 | Đăng nhập bằng Google *(Sign in with Google)* | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant; OAuth Provider | Đã đặc tả | Độc lập |
| UC-03 | Đăng xuất *(Sign Out)* | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả | Độc lập |
| UC-04 | Truy cập cuộc họp qua lời mời Guest *(Access Meeting via Guest Invitation)* | Guest; Email / SMTP | Đã đặc tả | Độc lập — xác minh email + profile tự khai, guest access cho UC-31, UC-35, UC-38, UC-43, UC-65 |

## FE-02 — Quản lý Người dùng & Hồ sơ *(User & Profile Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-05 | Xem danh sách tài khoản *(View User Accounts)* | System Admin | Đã đặc tả | Nền |
| UC-06 | Quản lý tài khoản *(Manage User Accounts)* — gồm UC-06a Tạo, UC-06b Cập nhật, UC-06c Vô hiệu hoá / Kích hoạt lại | System Admin | Đã đặc tả | `<<extend>>` UC-05 |
| UC-07 | Đặt lại mật khẩu *(Reset User Password)* | System Admin; Email / SMTP | Đã đặc tả | `<<extend>>` UC-05 |
| UC-08 | Gán vai trò hệ thống *(Assign System Role to User)* | System Admin | Đã đặc tả | `<<extend>>` UC-05 |

## FE-03 — Kiểm soát truy cập theo vai trò *(Role-Based Access Control)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-09 | Xem ma trận vai trò & quyền *(View Role & Permission Matrix)* | System Admin | Đã đặc tả | Nền |
| UC-10 | Cấu hình quyền của vai trò *(Configure Role Permissions)* | System Admin | Đã đặc tả | `<<extend>>` UC-09 |
| UC-11 | Quản lý vai trò *(Manage Roles)* — gồm UC-11a Tạo, UC-11b Cập nhật, UC-11c Xoá | System Admin | Đã đặc tả | `<<extend>>` UC-09 |
| UC-12 | Xem nhật ký kiểm toán *(View Audit Log)* | System Admin | Đã đặc tả | Độc lập |

## FE-04 — Quản lý cuộc họp *(Meeting Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-13 | Tạo cuộc họp *(Create Meeting)* — kèm lịch, hình thức, địa điểm, link tuỳ chọn | Meeting Organizer | Đã đặc tả | `<<extend>>` UC-16 |
| UC-14 | Cập nhật / dời lịch cuộc họp *(Update Meeting)* — sửa khi `Draft`/`Rejected`; *Reschedule* khi `Published` | Meeting Organizer | Đã đặc tả | `<<extend>>` UC-15 |
| UC-15 | Xem chi tiết cuộc họp *(View Meeting Details)* | Actor có quan hệ với cuộc họp | Đã đặc tả | `<<extend>>` UC-16 |
| UC-16 | Xem danh sách cuộc họp *(View Meeting List)* | Actor có quan hệ với cuộc họp | Đã đặc tả | Nền |
| UC-17 | Gửi duyệt cuộc họp *(Submit Meeting for Approval)* | Meeting Organizer | Đã đặc tả | `<<extend>>` UC-15 |
| UC-18 | Duyệt / Từ chối cuộc họp *(Review Meeting Approval)* | Meeting Approver | Đã đặc tả | Độc lập |
| UC-19 | Công bố cuộc họp *(Publish Meeting)* | Meeting Organizer; Email / SMTP | Đã đặc tả | `<<extend>>` UC-15 |
| UC-20 | Bắt đầu cuộc họp *(Start Meeting)* | Chair | Đã đặc tả | `<<extend>>` UC-15 |
| UC-21 | Kết thúc cuộc họp *(End Meeting)* — không phụ thuộc Vote Open hay điểm danh đủ/thiếu | Chair | Đã đặc tả | `<<extend>>` UC-15 |
| UC-22 | Huỷ cuộc họp *(Cancel Meeting)* — Organizer huỷ trước khi họp bắt đầu, bắt buộc nêu lý do; Meeting `Cancelled` chỉ đọc và thông báo cho người được mời nếu đã `Published` | Meeting Organizer; Email / SMTP | Đã đặc tả | `<<extend>>` UC-15 |

## FE-05 — Quản lý chương trình nghị sự *(Agenda Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-23 | Quản lý mục agenda *(Manage Agenda Items)* — gồm UC-23a Tạo, UC-23b Cập nhật, UC-23c Xoá, UC-23d Sắp xếp | Secretary | Đã đặc tả | `<<extend>>` UC-26 |
| UC-24 | Đặt mục agenda hiện tại *(Set Current Agenda Item)* | Chair | Đã đặc tả | `<<extend>>` UC-26 |
| UC-25 | Hoàn thành mục agenda *(Complete Agenda Item)* | Chair | Đã đặc tả | `<<extend>>` UC-26 |
| UC-26 | Xem chương trình nghị sự *(View Meeting Agenda)* | Actor có quan hệ với cuộc họp | Đã đặc tả | `<<extend>>` UC-15 |
| UC-27 | Ghi kết luận mục agenda *(Record Agenda Item Conclusion)* — Chair/Secretary ghi hoặc sửa kết luận của mục `InProgress`/`Done` cho tới khi sinh biên bản nháp | Chair, Secretary | Đã đặc tả | `<<extend>>` UC-26 |
| UC-28 | Ghi chú cuộc họp *(Take Meeting Notes)* — Thư ký ghi chú thời gian thực theo mục nghị sự hoặc cả cuộc họp; Chủ toạ đọc; BR-134, BR-133; MSG415–MSG423 | Secretary (ghi), Chair (đọc) | Đã đặc tả | `<<extend>>` UC-26 |

## FE-06 — Quản lý thành phần tham dự *(Meeting Participant Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-29 | Xem thành phần tham dự *(View Meeting Participants)* | Secretary, Chair, Participant, Guest | Đã đặc tả | `<<extend>>` UC-15 |
| UC-30 | Quản lý thành phần tham dự *(Manage Meeting Participants)* — gồm UC-30a thêm nhiều người nội bộ cùng vai trò/quyền hoặc mời từng Guest, UC-30b Quản lý vai trò cuộc họp, UC-30c Xoá / thu hồi | Meeting Organizer, Secretary | Đã đặc tả | Secretary: `<<extend>>` UC-29; Organizer gán Thư ký khi chưa có hoặc không còn trong giai đoạn chuẩn bị: `<<extend>>` UC-15 |
| UC-31 | Phản hồi lời mời họp *(Respond to Meeting Invitation)* | Secretary, Participant, Guest | Đã đặc tả | Độc lập; Guest đi qua UC-04 |
| UC-32 | Xem trạng thái phản hồi *(View Participant Response Status)* | Secretary, Chair, Participant, Guest | Đã đặc tả | Mở từ cuộc họp có thể truy cập; Member/Guest cần quyền xem phản hồi. Thấy danh tính trong dòng phản hồi không cấp quyền xem hoặc tìm trong roster |
| UC-33 | Ghi nhận điểm danh *(Record Attendance)* — Secretary lưu Present/Absent cho một/nhiều người được chọn khi `InProgress` | Secretary | Đã đặc tả | `<<extend>>` UC-29 |
| UC-34 | Chuyển vai trò Chair cho người khác *(Hand Over Chair to Another Participant)* — Chair hiện hành chuyển trong một bước: người nhận thành Chair, người chuyển thành Member | Chair | Đã đặc tả | `<<extend>>` UC-29 |

## FE-07 — Kho tài liệu cá nhân & Tài liệu cuộc họp *(Personal Document Library & Meeting Document Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-35 | Xem tài liệu cuộc họp *(View Meeting Documents)* — UC-35a Xem danh sách, UC-35b Xem trước/tải về; quyền đọc theo quyền xem Meeting. `Secret` không tải về; UC-32b cũ vẫn bỏ | Người có quyền xem Meeting, gồm Guest có lời mời đã xác minh còn hiệu lực | Đã đặc tả các UC con | Internal: `<<extend>>` UC-15; Guest qua UC-04 |
| UC-36 | Quản lý tài liệu cuộc họp *(Manage Meeting Documents)* — UC-36a Thêm một hoặc nhiều tài liệu cùng lần, UC-36b Gắn vào mục (tái cấp theo ADR-0020), UC-36c Gỡ khỏi cuộc họp, UC-36d Bỏ gắn một mục; khoá khi Meeting kết thúc. Nhóm tài liệu thêm cùng lần thành công toàn bộ hoặc không thêm tài liệu nào | Chair, Secretary hiện hành có quyền xem Meeting | Đã đặc tả bốn UC con | `<<extend>>` UC-35 |
| UC-37 | Xem kho tài liệu cá nhân *(View Personal Document Library)* | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả | Độc lập |
| UC-37a | Xem kho cá nhân của tài khoản đã khoá *(View a Locked Account’s Personal Library)* — Quản trị viên chỉ đọc, mỗi lần đọc ghi nhật ký kiểm toán (BR-085, BR-009–BR-011) | System Admin | Đã đặc tả | Độc lập |
| UC-38 | Bình luận tài liệu cuộc họp *(Comment on Meeting Document)* — người có quyền đọc được bình luận trước khi Meeting kết thúc; sau đó chỉ đọc | System Admin, Organizer, Approver, Chair, Secretary, Participant, Guest có quyền xem Meeting | Đã đặc tả | `<<extend>>` UC-35 |
| UC-39 | Tải tài liệu lên kho cá nhân *(Upload Document to Personal Library)* — một hoặc nhiều PDF/DOCX cùng lần (tối đa 10 tệp / 100 MB mỗi lần, 10 MB mỗi tệp — họp 19/11/2026; tổng kho cả hệ thống 10 GB), vào thư mục đang chọn; cả nhóm thành công hoặc không thêm tài liệu nào | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả | `<<extend>>` UC-37 |
| UC-40 | Quản lý tài liệu trong kho *(Manage Personal Documents)* — gồm UC-40a Xoá khỏi kho (tài liệu đã gắn vào cuộc họp vẫn giữ nguyên), UC-40b Đổi tên, UC-40c Chuyển thư mục | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả các UC con | `<<extend>>` UC-37 |
| UC-41 | Quản lý thư mục kho *(Manage Library Folders)* — gồm UC-41a Tạo, UC-41b Đổi tên, UC-41c Chuyển, UC-41d Xoá thư mục rỗng; lồng tối đa 5 cấp | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả các UC con | `<<extend>>` UC-37 |
| UC-42 | Xác nhận đã đọc tài liệu biểu quyết *(Confirm Document Read)* — cuộn tới trang cuối rồi xác nhận; bắt buộc trước khi bỏ phiếu | Chair, Secretary, Participant, Guest có quyền bỏ phiếu | Đã đặc tả | Được gọi trước UC-65 |

## FE-08 — Phòng họp trực tuyến *(Online Meeting Room Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-43 | Vào phòng họp *(Join Meeting Room)* | Chair, Secretary, Participant có phân công hiện hành; Guest có lời mời hợp lệ | Đã đặc tả | Internal: `<<extend>>` UC-15; Guest qua UC-04; UC-49 `<<extend>>` UC-43 |
| UC-44 | Rời phòng họp *(Leave Meeting Room)* | User đang trong phòng | Đã đặc tả | Độc lập khi đã kết nối |
| UC-45 | Điều khiển trong phòng *(Manage Meeting Room Controls)* — gồm UC-45a Micro, UC-45b Camera, UC-45c Chia sẻ màn hình, UC-45d Chọn thiết bị âm thanh và hình ảnh | User đang trong phòng | Đã đặc tả bốn UC con | Độc lập khi đã kết nối |
| UC-46 | Xem người đang trong phòng *(View Meeting Room Participants)* | User đang trong phòng | Đã đặc tả | Độc lập khi đã kết nối |
| UC-47 | Điều phối phòng họp *(Moderate Meeting Room)* — chỉ tắt mic người khác, một chiều | Chair hiện hành | Đã đặc tả | Độc lập khi đã kết nối |
| UC-48 | Chat trong phòng *(Use In-room Chat)* | User đang trong phòng | Đã đặc tả | Độc lập khi đã kết nối |
| UC-49 | Giữ cuộc họp trong cửa sổ nổi *(Keep Meeting in a Floating Window)* | User đang trong phòng | Đã đặc tả | `<<extend>>` UC-43 |
| UC-50 | Đăng ký phát biểu *(Register to Speak)* — giơ tay, Chủ toạ mời từng người, phát biểu theo lượt | Chair, Secretary, Participant, Guest trong phòng; Chair mời | Đã đặc tả (yêu cầu, chưa cài) | `<<extend>>` UC-43 |

## FE-09 — Ghi âm cuộc họp *(Meeting Recording)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-51 | Xem & nghe bản ghi cuộc họp *(View & Play Meeting Recordings)* — gồm UC-51a Xem danh sách bản ghi (kèm bản ghi tổng hợp của họp kết hợp đã kết thúc), UC-51b Phát audio (bản ghi phòng, bản ghi tổng hợp hoặc câu nói), UC-51c Tải bản ghi audio (Chủ toạ, Thư ký; cấm khi cuộc họp Mật) | User có quyền | Đã đặc tả các UC con | `<<extend>>` UC-15 |
| UC-52 | Ghi âm phòng họp & Tải tệp audio *(Record Room Audio)* — gồm UC-52a Bắt đầu ghi mic phòng (hỏi xác nhận trước lượt đầu), UC-52b Dừng và lưu lượt ghi (nối vào bản ghi phòng duy nhất, tự xếp hàng gỡ băng), UC-52c Tải tệp âm thanh cuộc họp (chỉ họp Trực tiếp khi In Progress hoặc Ended; họp kết hợp báo MSG478), UC-52d Xoá một lượt ghi, UC-52e Xoá bản ghi cuộc họp (xoá tệp và transcript, giữ vết kiểm toán; bản ghi tổng hợp bị xoá thì không dựng lại) | **Secretary**; System (tự lưu, tự dựng bản tổng hợp, hết hạn) | Đã đặc tả các UC con | `<<extend>>` UC-51a |
| UC-53 | Quản lý câu nói đã ghi của tôi *(Manage My Recorded Speech)* — xem câu chờ gửi và đã lưu, nghe lại, rút/xoá câu của mình khỏi dòng thời gian | Chair, Secretary, Participant hiện hành | Đã đặc tả | `<<extend>>` UC-43, UC-51a |

## FE-10 — Gỡ băng, nhận diện người nói và tóm tắt *(Transcription, Speaker Identification & Meeting Summary)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-54 | Xem bản gỡ băng cuộc họp *(View Meeting Transcript)* — gồm UC-54a Xem bản gỡ băng (đoạn có giờ, tên người nói, vạch phân lượt, chữ do người sửa phân biệt với chữ máy; gợi ý tên chỉ Thư ký thấy), UC-54b Xuất bản gỡ băng.txt (Chủ toạ, Thư ký; cấm khi Mật), UC-54c Gỡ băng bản ghi cuộc họp (Thư ký chạy lại bản đã xong / khi lỗi / hệ thống tự xếp hàng; bản ghi tổng hợp báo MSG479, không đưa vào hàng chờ; chạy lại giữ phần Thư ký sửa tay theo BR-125, BR-154) | User có quyền; Secretary | Đã đặc tả các UC con | `<<extend>>` UC-51a (UC-54c `<<include>>` từ UC-52b, UC-52c) |
| UC-55 | Chỉnh sửa bản gỡ băng & gán người nói *(Edit Transcript & Speaker Attribution)* — gồm UC-55a Sửa chữ đoạn gỡ băng hoặc câu nói, UC-55b Gán người nói cho giọng (có gợi ý Voice ID, gán cả nhóm; đổi người nói một câu ghi dấu người/lúc chuyển để lần chạy lại mang theo theo BR-125, BR-154), UC-55c Loại hoặc đưa lại câu khỏi bản ghi (lý do Lẫn tiếng/Khác; chỉ Chủ toạ/Thư ký), UC-55d Xoá câu nói khỏi dòng thời gian (Thư ký xoá câu) | Secretary, Chair | Đã đặc tả các UC con | `<<extend>>` UC-51a, UC-54a |
| UC-58 | Sinh / sinh lại tóm tắt *(Generate / Regenerate Meeting Summary)* — dự thảo 3 mục I–III, chỉ Chủ tọa/Thư ký; AI chạy tại chỗ, xếp hàng, hỏi lại mỗi 5 giây; kết quả biểu quyết chép từ dữ liệu | Chair, Secretary; AI Drafting Service | Đã đặc tả | `<<extend>>` UC-54a |
| UC-59 | Xem tóm tắt *(View Meeting Summary)* — người khác chỉ thấy sau khi duyệt | Chair, Secretary (bản chưa duyệt); User có quyền (sau khi duyệt) | Đã đặc tả | `<<extend>>` UC-15 |
| UC-60 | Chỉnh tóm tắt *(Edit Meeting Summary)* — lưu là mất dấu đã duyệt, phải duyệt lại | Chair, Secretary | Đã đặc tả | `<<extend>>` UC-59 |
| UC-61 | Duyệt tóm tắt *(Approve Meeting Summary)* — từ đây người dự họp khác đọc được | Chair, Secretary | Đã đặc tả | `<<extend>>` UC-59 |
| UC-51 cũ | Công bố tóm tắt *(Publish Meeting Summary)* — bước riêng sau khi duyệt | Secretary | Bỏ/retired (tạm giữ số, sau đánh số lại) | `<<extend>>` UC-59 |
| UC-56 | Quản lý nhận diện giọng nói cá nhân *(Manage My Voice ID)* — gồm UC-56a Xem thông tin giọng nói (có tab "Người khác thu hộ"), UC-56b Xác nhận đồng ý Voice ID (thông báo 1.5; "cho phép người khác thu" = đồng ý ≥ 1.4), UC-56c Thu âm mẫu giọng (tối đa 5 mẫu tự thu, 6–60s; mẫu thu hộ tối đa 50, tính riêng), UC-56d Nghe lại mẫu giọng (cả hai bộ, chỉ chính chủ), UC-56e Xoá mẫu giọng, UC-56f Rút đồng ý Voice ID (xoá ngay cả hai bộ mẫu và mọi gợi ý) | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả các UC con | Độc lập (UC-56b–f `<<extend>>` UC-56a) |
| UC-57 | Thu giọng mẫu hộ *(Collect Voice Samples for Attendees)* — gồm UC-57a Xem danh sách thu hộ, UC-57b Thu mẫu giọng cho người khác (cần quyền Thu giọng mẫu hộ, ở màn Quản trị → Người dùng, cần đồng ý thường trực ≥ 1.4 và xác nhận từng lần thu, thiếu xác nhận → `400 MSG483`), UC-57c Nghe lại mẫu đã thu (ghi nhật ký), UC-57d Xoá mẫu đã thu | Voice sample collection permission holder (mặc định Administrator) | Đã đặc tả các UC con (SCRUM-152, sửa note 58) | Độc lập |

## FE-11 — Biểu quyết *(Voting Management)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-62 | Xem biểu quyết *(View Meeting Votes)* — gồm UC-62a Xem danh sách, UC-62b Xem trạng thái / kết quả (xem được cả sau khi `Ended`); nháp chỉ Chair hiện hành thấy | User có quyền | Đã đặc tả các UC con | `<<extend>>` UC-15 |
| UC-63 | Tạo / cấu hình biểu quyết *(Create / Configure Vote)* — tạo, sửa và xoá nháp khi `Approved`, `Published`, `InProgress`; Chair chọn vai trò được bỏ phiếu | Chair | Đã đặc tả | `<<extend>>` UC-62 |
| UC-64 | Mở biểu quyết *(Open Vote)* — chỉ `InProgress`, tối đa một Vote Open mỗi Meeting, ít nhất một người đủ điều kiện | Chair | Đã đặc tả | `<<extend>>` UC-62 |
| UC-65 | Bỏ phiếu *(Cast Vote)* — người trong danh sách chốt lúc mở và còn quyền truy cập; Guest xác minh qua lời mời | Chair, Secretary, Participant, Guest | Đã đặc tả | `<<extend>>` UC-62 |
| UC-66 | Đóng biểu quyết *(Close Vote)* — khi `InProgress` hoặc sau `Ended` | Chair | Đã đặc tả | `<<extend>>` UC-62 |
| UC-67 | Ghi kết luận biểu quyết *(Record Vote Conclusion)* — Chair chọn `Passed` / `NotPassed` / `Deferred`; ghi rồi không sửa | Chair | Đã đặc tả | `<<extend>>` UC-62 |
| UC-68 | Quản lý tài liệu biểu quyết *(Manage Vote Documents)* — gồm UC-68a Gắn tài liệu vào Vote nháp, UC-68b Bỏ gắn; danh sách chốt khi Vote mở | Chair, Secretary | Đã đặc tả các UC con | `<<extend>>` UC-63 |

## FE-12 — Biên bản & Duyệt, ban hành *(Meeting Minutes & Approval)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-69 | Xem biên bản *(View Meeting Minutes)* — bản nháp, chờ duyệt, bị trả lại chỉ Chủ tọa/Thư ký; bản đã ban hành (và bản cũ) cho mọi người đọc được cuộc họp, trừ Khách; tải Word/PDF trừ họp Mật (BR-139) | Chair, Secretary, Participant | Đã đặc tả | `<<extend>>` UC-15 |
| UC-70 | Sinh bản nháp biên bản *(Generate Meeting Minutes)* — một mẫu cố định (Mẫu 1.4 NĐ30), điền từ hồ sơ cuộc họp + dự thảo đã duyệt | Chair, Secretary | Đã đặc tả | `<<extend>>` UC-69 |
| UC-71 | Chỉnh biên bản *(Edit Meeting Minutes)* — trình soạn HTML (UC-71, NFR) có xem trước, chống ghi đè, xuất Word (không tải được khi Mật) | Chair, Secretary | Đã đặc tả | `<<extend>>` UC-69 |
| UC-72 | Gửi duyệt biên bản *(Submit Meeting Minutes for Approval)* — Thư ký gửi bản nháp hoặc bị trả lại cho Chủ tọa | Secretary; Chair nhận thông báo | Đã đặc tả | `<<extend>>` UC-69 |
| UC-73 | Duyệt và ban hành biên bản *(Approve and Issue Meeting Minutes)* — duyệt là ban hành luôn, ghi nhật ký (BR-009–BR-011) | **Chair** | Đã đặc tả | `<<extend>>` UC-69 |
| UC-74 | Trả lại biên bản *(Return Meeting Minutes for Revision)* — kèm lý do, chỉ giữ lý do của lần trả cuối | **Chair** | Đã đặc tả | `<<extend>>` UC-69 |
| UC-75 | Tạo bản đính chính biên bản *(Correct Issued Meeting Minutes)* — bản mới từ bản đã ban hành, bản cũ giữ nguyên | Chair, Secretary | Đã đặc tả | `<<extend>>` UC-69 |

## FE-13 — Email & Thông báo trong ứng dụng *(Email & In-App Notification)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-76 | Xem thông báo *(View Notifications)* | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả | Nền |
| UC-77 | Đánh dấu đã đọc *(Mark Notifications as Read)* — gồm UC-77a Một thông báo, UC-77b Tất cả | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả | `<<extend>>` UC-76 |
| UC-78 | Mở đối tượng từ thông báo *(Open Notification Target)* | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả | `<<extend>>` UC-76 |

## FE-15 — Báo cáo & Thống kê *(Reporting & Statistics)*

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-79 | Xem & xuất báo cáo *(View & Export Reports)* — gồm UC-79a Xem (dải số quản lý + truy vấn theo bảng/nhóm/bộ lọc/top N, EX `400 MSG484` khi tham số không có; BR-152 nới cho dòng nhóm theo người/cuộc họp; phạm vi thấy theo BR-153, BR-151), UC-79b Xuất (đúng bảng đang xem, tệp `bao-cao-<bảng>_<từ>_<đến>.xlsx`, nhãn audit "Xuất báo cáo") | System Admin, Meeting Organizer, Meeting Approver, Chair, Secretary, Participant | Đã đặc tả các UC con (SCRUM-38, 09/11/2026; truy vấn 22/11/2026, PR #488) | Độc lập (UC-79b `<<extend>>` UC-79a) |

## FE-16 — Lịch làm việc & tìm giờ họp *(Working Calendar & Meeting Time Search)*

Quyết định phạm vi: [ADR-0058](../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md) — chỉ chia sẻ bận/rảnh, họp Mật không bao giờ chia sẻ, không có trợ lý AI.

| UC | Tên | Actor | Đặc tả | Quan hệ |
|---|---|---|---|---|
| UC-80 | Quản lý chia sẻ lịch *(Manage Calendar Sharing)* — gồm UC-80a Yêu cầu xem lịch, UC-80b Tự chia sẻ lịch (luôn Mốc 1), UC-80c Trả lời chia sẻ lịch, UC-80d Thay đổi chia sẻ lịch, UC-80e Rút hoặc ngừng chia sẻ lịch, UC-80f Xem danh sách chia sẻ; chỉ chia sẻ bận/rảnh, họp Mật không bao giờ chia sẻ (BR-140, BR-141) | Meeting Organizer (bên xem); mọi tài khoản nội bộ đang hoạt động (chủ lịch) | Đã đặc tả các UC con | UC-80a `<<extend>>` UC-81a |
| UC-81 | Tìm giờ họp *(Find Meeting Time)* — gồm UC-81a Tìm khung giờ (tối đa 10 người ngoài người tìm), UC-81b Tạo nháp từ khung giờ, UC-81c Xem lịch được chia sẻ | Meeting Organizer | Đã đặc tả các UC con | UC-81b `<<extend>>` UC-81a, dùng lại luồng UC-13 |

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

Cột *mới* là số sau ngày 10/10/2026. `UC-71`, `UC-72` mới (thêm 13/10/2026, kho tài liệu) không liên quan `UC-71`, `UC-72` cũ ở cột trái;
`UC-78` mới (thêm 25/10/2026, cửa sổ nổi) không liên quan `UC-78` cũ ở cột trái (đã đổi thành UC-59); `UC-79` mới (thêm 29/10/2026, đăng ký phát biểu) cũng không liên quan `UC-79` cũ (đã đổi thành UC-60).

> Bảng `00-danh-muc-ma-thong-bao.md` vẫn ghi số UC cũ ở cột UC; tra bảng này khi đọc.

---

# 6. Vòng đời / phụ thuộc

Chi tiết theo luồng nghiệp vụ ở [04-luong-nghiep-vu-chinh.md](../02-design/04-luong-nghiep-vu-chinh.md) và
[01-vong-doi-cuoc-hop.md](../02-design/01-vong-doi-cuoc-hop.md).

```text
Meeting
UC-13 Create → Draft ──UC-17 Submit──> WaitingApproval ──UC-18──> Approved | Rejected
   ↑ UC-14 Update (Draft, Rejected)                                   │          └─ UC-14 sửa → UC-17 gửi lại
                                                                       └─UC-19 Publish──> Published
                                    UC-14 Reschedule (giờ, địa điểm, link; báo lại người được mời) ─┘
Published ──UC-20 Start──> InProgress ──UC-21 End (không phụ thuộc phiếu đang mở)──> Ended

Agenda Item (thuộc trực tiếp Meeting)
UC-23a Create → Pending ── UC-24 Set Current → InProgress ── UC-25 Complete → Done
UC-23b/c/d sửa, xoá, sắp xếp khi Meeting chưa Ended; UC-21 End tự hoàn tất mục đang chạy

Guest
UC-30a mời → email → UC-04 xác minh mã → tự khai tên/chức vụ → quyền Member trong cuộc họp → UC-31 / UC-35 / UC-38 / UC-43–UC-48 / UC-65

Document
UC-39 Upload vào kho cá nhân → UC-37, UC-36a chọn từ kho (hoặc upload thẳng từ máy) → UC-36c gỡ khi cần đổi file
UC-41 thư mục, UC-40 đổi tên / chuyển / xoá trong kho. Xoá khỏi kho: cuộc họp vẫn giữ tham chiếu riêng; file không còn ai tham chiếu thì job nền dọn.
Tải thẳng từ máy vào cuộc họp: dòng kho vào thư mục `<tên cuộc họp> - dd/mm/yyyy` của người tải

Audio → Gỡ băng & Tóm tắt
- Họp Trực tiếp: UC-52a Bật mic phòng (từng lượt) / UC-52c Upload audio → UC-52b Lưu lượt (ghép vào một bản ghi) → Hệ thống tự xếp hàng gỡ băng (UC-54c) → UC-54a Xem gỡ băng → UC-55a Sửa chữ, UC-55b Gán người nói (có gợi ý Voice ID từ UC-56c) → UC-58 Sinh tóm tắt → UC-59 Xem → UC-60 Sửa → UC-61 Duyệt → UC-51 cũ Công bố.
- Họp Trực tuyến: UC-45a Bật mic gửi từng câu → câu lưu trong tab Bản ghi (UC-51a) → UC-55a Thư ký sửa chữ, UC-55c Loại/đưa lại câu lọt tiếng, UC-53 Người nói xem/nghe/rút câu của mình.
- Họp Kết hợp (SCRUM-153, ADR-0061): mic phòng từng lượt (UC-52a/b) + mic từng người (UC-45a); sau Ended hệ thống tự dựng một bản ghi tổng hợp theo giờ thật (không UC riêng) → nghe/tải như bản ghi thường (UC-51a AF-3, UC-51b/c), không chuyển chữ (UC-54c báo MSG479); nguồn đổi thì tự dựng lại, Thư ký xoá thì không dựng lại (UC-52e).
- Tải về: UC-51c Tải bản ghi audio, UC-54b Xuất bản gỡ băng.txt (Chủ toạ, Thư ký; cấm khi cuộc họp Mật).
- Thu hộ Voice ID (UC-57, note 58, ADR-0073/0075): người có quyền "Thu giọng mẫu hộ" (mặc định Quản trị viên) thu ở màn Quản trị → Người dùng, cần đồng ý thường trực ≥ 1.4 và xác nhận từng lần thu; mẫu thuộc về người được thu, mỗi lần thu/nghe/xoá đều ghi nhật ký.

Biểu quyết (ADR-0019: chuẩn bị nháp trước khi họp)
UC-63 Tạo/cấu hình nháp khi Approved / Published / InProgress
→ UC-64 Mở khi InProgress → UC-65 Bỏ phiếu khi InProgress → UC-66 Đóng → hệ thống kiểm phiếu → UC-67 Chair ghi kết luận
Sau Ended: không nhận phiếu mới; Chair vẫn UC-66 đóng phiếu còn mở và UC-67 ghi kết luận (ADR-0017). Phiếu Draft xoá được trong UC-63

Biên bản
UC-70 Sinh nháp → UC-71 Sửa → UC-72 Thư ký gửi duyệt → UC-73 Chủ tọa duyệt và ban hành (khoá vĩnh viễn)
UC-74 Chủ tọa trả lại kèm lý do → Thư ký sửa (UC-71) rồi gửi lại (UC-72)
Sửa sau ban hành: UC-75 tạo bản đính chính, đi lại UC-72, UC-62 cũ, UC-63 cũ, UC-73; bản cũ thành Superseded
```

# 7. Thống kê

| FE | Feature | Số UC | UC con |
|---|---|---:|---:|
| FE-01 | Authentication & Account Security | 4 | — |
| FE-02 | User & Profile Management | 4 | 3 |
| FE-03 | Role-Based Access Control | 4 | 3 |
| FE-04 | Meeting Management | 10 | — |
| FE-05 | Agenda Management | 5 | 4 |
| FE-06 | Meeting Participant Management | 6 | 3 |
| FE-07 | Personal Document Library & Meeting Document Management | 8 | 13 |
| FE-08 | Online Meeting Room Management | 8 | 4 |
| FE-09 | Meeting Recording | 3 | 8 |
| FE-10 | Transcription, Speaker Identification & Meeting Summary | 9 | 17 |
| FE-11 | Voting Management | 7 | 4 |
| FE-12 | Meeting Minutes & Approval | 7 | — |
| FE-13 | Email & In-App Notification | 3 | 2 |
| FE-15 | Reporting & Statistics | 1 | 2 |
| **Tổng** | | **79** | **63** |

# 8. Ngoài phạm vi UC

- Chỉ có trong code, chưa thành UC: *ReturnToDraft*, *TransferOrganizer* (endpoint workflow SCRUM-19). Cần PO/BA quyết định
  promote hay dọn code.
- Không làm (Report 1): theo dõi task / action item, ký HSM thật, triển khai air-gapped,
  tích hợp eOffice, nhiều tổ chức / đơn vị ([ADR-0004](../05-decisions/0004-mot-to-chuc-khong-co-don-vi.md)).
- Thư ký thu hồi riêng một phiên của Khách (PO chốt 01/11/2026, SCRUM-116): không làm. Gỡ lời mời là mọi phiên của lời mời
  đó hết hiệu lực (UC-30c POST-2); Khách đăng xuất chỉ kết thúc phiên của chính họ (UC-04). Chỉ mở lại khi hội đồng hoặc PO yêu cầu.
- PO chốt 14/11/2026, không làm:
  - Ký số biên bản, kể cả bản mô phỏng (SCRUM-37 đóng; UC-62 cũ, UC-63 cũ bỏ). Biên bản dừng ở Chủ tọa duyệt là ban hành (UC-73).
- PO chốt 15/11/2026, không làm (SCRUM-207, QA 15/11):
  - Mở khoá tài khoản (UC-09 cũ bỏ hẳn; không khoá khi nhập sai mật khẩu; giới hạn đăng nhập theo phút MSG414 giữ nguyên).
  - Dashboard cá nhân & Không gian họp (UC-69 cũ/FE-14 bỏ hẳn; chỉ giữ bốn thẻ đếm trên danh sách cuộc họp).
  - Thư ký gom góp ý trên tài liệu thành danh sách để đưa vào nghị sự (SCRUM-106 đóng). Bình luận trên tài liệu cuộc họp
    vẫn có (SCRUM-23).
  - Thu giọng mẫu (Voice ID) của Khách (SCRUM-136 đóng). Khách trực tuyến đã có tên theo micro của chính họ; câu của Khách
    ngồi trong phòng (mic phòng) hiện "Chưa rõ người nói", Thư ký đổi người nói từng câu. Lý do: giọng dùng để nhận diện là dữ
    liệu sinh trắc học nhạy cảm (Luật 91/2025/QH15), không thu của người ngoài hệ thống theo nguyên tắc tối thiểu hoá dữ liệu.
- Lịch sử trạng thái, lịch tuần / tháng: là màn hình / luồng phụ của UC-15, UC-16, không tách UC.

Guest 13/10/2026: [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md); UC-29, UC-32 cần quyền xem tương ứng như Member, UC-46 xem người đang kết nối mặc định. BR-040 đã chốt; xem [quyết định mới](../05-decisions/0017-ket-thuc-hop-khong-phu-thuoc-diem-danh-bieu-quyet.md).

Phân phạm vi task: UC-29–UC-32 thuộc roster/lời mời/phản hồi SCRUM-20; UC-33 điểm danh giữ riêng ở SCRUM-26. Xoá/revoke ở SCRUM-20 bảo toàn lịch sử response/attendance. Sequence UC-04 có tại [auth/README](../06-sequencediagram/auth/README.md).
