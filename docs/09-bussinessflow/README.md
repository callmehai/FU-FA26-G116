# Business Flow (BF)

Bốn swimlane mô tả luồng nghiệp vụ chính, bản PO đã duyệt ngày 09/10/2026 (nguồn: thư mục `Diagrams/Main flow Swimlane`
của nhóm, đồng bộ vào repo ngày 10/10/2026). File `.drawio` là nguồn để sửa; file `.drawio.png` có nhúng XML nên mở bằng
draw.io vẫn sửa tiếp được. Diễn giải chi tiết từng bước, kèm UC và BR, ở
[02-design/04-luong-nghiep-vu-chinh.md](../02-design/04-luong-nghiep-vu-chinh.md).

| File | Trigger → Kết thúc | Swimlane | UC chính |
|---|---|---|---|
| [BF-01](BF-01_System_Access_Administration.drawio.png) System Access & Administration | Cấp / đổi quyền truy cập → user đăng nhập và dùng được đúng chức năng | System Admin, Internal User, TrustCabinet, Google Identity Provider | UC-01 → UC-03, UC-05 → UC-13 |
| [BF-02](BF-02_Meeting_Preparation_Approval.drawio.png) Meeting Preparation, Approval & Participant Coordination | Organizer cần tổ chức họp → cuộc họp `Published`, lời mời đã gửi | Meeting Organizer, Secretary, TrustCabinet, Meeting Approver, Participant / Guest, Email Service | UC-04, UC-14 → UC-20, UC-23, UC-27 → UC-30, UC-33 |
| [BF-03](BF-03_Meeting_Execution_Decision_Making.drawio.png) Meeting Execution & Decision Making | Chair bắt đầu cuộc họp `Published` → cuộc họp `Ended`, kết quả agenda và biểu quyết đã ghi | Participant / Guest, Chair, TrustCabinet, Secretary | UC-21, UC-22, UC-24, UC-25, UC-31, UC-37 → UC-42, UC-53 → UC-57, UC-74, UC-77 |
| [BF-04](BF-04_Post_Meeting_Processing_Formal_Records.drawio.png) Post-Meeting Processing & Formal Records | Cuộc họp `Ended` → biên bản đã ký, niêm phong, tra cứu theo quyền | AI Provider, TrustCabinet, Secretary, Chair / Designated Signers, Authorized User | UC-43 → UC-49, UC-58 → UC-65 |

## Điểm chính của bản BF 09/10/2026

- **Một cuộc họp = một buổi họp.** Không còn Meeting Session; lịch nhập khi tạo cuộc họp, agenda gắn thẳng vào cuộc họp
  ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Theo AI-eCabinet: chữ "phiên họp" trên UI của họ là agenda item.
- **BF-01:** đăng nhập sai quá số lần cho phép thì khoá tài khoản; System Admin mở khoá (UC-09). Mọi thay đổi của Admin
  ghi audit log.
- **BF-02:** Organizer tạo cuộc họp (tiêu đề, lịch, hình thức, địa điểm, độ mật), gán Thư ký đầu tiên, chọn người duyệt,
  gửi duyệt. Thư ký chuẩn bị thành phần (kể cả Guest), vai trò, agenda, tài liệu. Bị từ chối thì Organizer sửa và gửi lại.
  Công bố chỉ khi đã có Chair và Secretary; hệ thống tạo lời mời và gửi email. Sau khi công bố, Organizer chỉ được dời
  giờ, địa điểm, link (*Reschedule*, BR-158) và hệ thống báo lại người được mời.
- **BF-03:** Chair bắt đầu họp → hệ thống chuyển `InProgress` và báo người tham dự; Thư ký điểm danh chính thức; với
  họp Online/Hybrid người tham dự vào phòng họp. Mỗi mục agenda: Chair đặt mục hiện tại → thảo luận → nếu cần thì tạo và mở
  biểu quyết → bỏ phiếu → Chair đóng → hệ thống kiểm phiếu → Chair ghi kết luận *Passed / Not Passed / Deferred* → hoàn
  thành mục. Hết mục thì Chair kết thúc họp, hệ thống đóng phòng họp. **Biểu quyết chỉ diễn ra trong cuộc họp**
  ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)) — không vote trước / sau họp, không quorum, không Resolution.
- **BF-04:** Thư ký upload audio (không còn vai trò Audio Uploader) → hệ thống chép lời → Thư ký sửa transcript → AI
  sinh tóm tắt, Thư ký sửa (không có bước duyệt hay công bố tóm tắt) → AI sinh biên bản, Thư ký sửa rồi gửi ký.
  Biên bản **chỉ có một người ký**: chấp nhận thì ký, không thì trả Thư ký sửa và gửi ký lại — chưa có chữ ký nào nên
  không có bước huỷ chữ ký. Ký xong thì Thư ký niêm phong; biên bản bị khoá và tra cứu theo quyền.

## Cập nhật 19/10/2026

Theo Report 3 ngày 18/10/2026 (trao đổi PO 16/10/2026, góp ý hội đồng lần 1), sửa cục bộ, giữ bố cục:

- **BF-03:** "Set Current Agenda Item" thành "Start Any Pending Agenda Item" (BR-70); "Create & Open Vote" thành mở
  từ nháp do Chair hoặc Thư ký chuẩn bị (UC-53, BR-252); người tham dự xác nhận đã đọc tài liệu biểu quyết trước khi bỏ
  phiếu (UC-74); thêm bước "Record Agenda Item Conclusion" trước khi hoàn thành mục (UC-77).
- **Không vẽ trong BF:** huỷ cuộc họp (UC-75) và giao chủ toạ (UC-73) là hành động tuỳ lúc, không nằm trên luồng chính,
  nên không thêm vào swimlane; xem [SM-01](../10-statemachine/SM-01_Meeting.drawio) và sequence của hai UC đó. BF-02
  giữ nguyên.
- Đã render và xem lại BF-03; PNG xuất bằng draw.io 31.7.0 có nhúng model (chunk `zTXt` `mxGraphModel`), đã giải mã và
  đối chiếu với source nhưng chưa mở lại bằng draw.io.

**Quy tắc:** không thêm note, shape ghi chú hay chữ chú thích tự do vào swimlane — xem
[DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md#no-notes-inside-diagrams).

## Lịch sử

Bản trước ngày 09/10/2026 có cấp Meeting Session, "Shared Voting Sub-flow", biểu quyết trước họp (BF-02) và sau họp
(BF-04). Danh sách việc cần sửa docs theo bản BF mới đã thực hiện xong trong đợt đồng bộ 10/10/2026 (gom UC, đánh số lại
UC-01 → UC-70); bảng đổi số ở [Master Use Case Inventory §5](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới).
