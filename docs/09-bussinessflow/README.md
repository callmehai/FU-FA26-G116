# Business Flow (BF)

Năm swimlane mô tả luồng nghiệp vụ chính, bản PO đã duyệt ngày 09/10/2026 (nguồn: thư mục `Diagrams/Main flow Swimlane`
của nhóm, đồng bộ vào repo ngày 10/10/2026). File `.drawio` là nguồn để sửa; file `.drawio.png` có nhúng XML nên mở bằng
draw.io vẫn sửa tiếp được. Diễn giải chi tiết từng bước, kèm UC và BR, ở
[02-design/04-luong-nghiep-vu-chinh.md](../02-design/04-luong-nghiep-vu-chinh.md).

| File | Trigger → Kết thúc | Swimlane | UC chính |
|---|---|---|---|
| [BF-01](BF-01_System_Access_Administration.drawio.png) System Access & Administration | Cấp / đổi quyền truy cập → user đăng nhập và dùng được đúng chức năng | System Admin, User, TrustCabinet, OAuth Provider | UC-01 → UC-03, UC-05 → UC-12 |
| [BF-02](BF-02_Meeting_Preparation_Approval.drawio.png) Meeting Preparation, Approval & Participant Coordination | Member cần tổ chức họp → cuộc họp `Published`, lời mời đã gửi (hoặc `Cancelled` trước khi họp) | Member (Meeting Creator), Chair / Secretary, TrustCabinet, Approver, Participant / Guest, Resend Email | UC-04, UC-13 → UC-19, UC-22, UC-23, UC-29 → UC-32, UC-36, UC-81 |
| [BF-03](BF-03_Meeting_Execution_Decision_Making.drawio.png) Meeting Execution & Decision Making | Chair bắt đầu cuộc họp `Published` → cuộc họp `Ended`, kết quả agenda, biểu quyết và bản ghi đã lưu | Participant / Guest, Chair, TrustCabinet, Secretary | UC-20, UC-21, UC-24, UC-25, UC-27, UC-33, UC-42 → UC-48, UC-50, UC-52, UC-62 → UC-67 |
| [BF-04](BF-04_Post_Meeting_Processing_Formal_Records.drawio.png) Post-Meeting Processing & Formal Records | Cuộc họp `Ended` → biên bản được Chủ toạ duyệt là ban hành, khoá, tra cứu theo quyền | AI Drafting Service, TrustCabinet, Secretary, Chair, Authorized User | UC-51 → UC-55, UC-58 → UC-61, UC-69 → UC-74 |
| [BF-05](BF-05_Meeting_Document_Sharing_Review.drawio.png) Meeting Document Sharing & Review | Chair / Secretary cần chia sẻ tài liệu cho cuộc họp → tài liệu đã lưu (gắn agenda / dự thảo biểu quyết khi cần), người tham dự mở theo quyền, bình luận và xác nhận đã đọc được ghi | Chair / Secretary, TrustCabinet, Participant / Guest | UC-35 → UC-42 (FE-07) |

Số UC theo Report 3 sau đợt đánh số lại ngày 24/11/2026 ([ADR-0079](../05-decisions/0079-cho-danh-so-lai-br-uc-co-bang-anh-xa.md));
các mục cũ bên dưới giữ số tại thời điểm viết.

BF-05 thêm ngày 28/11/2026 (Report 3 §2.5, Figure 6; các hình sau lùi một số).

## Điểm chính của bản BF 09/10/2026

- **Một cuộc họp = một buổi họp.** Không còn Meeting Session; lịch nhập khi tạo cuộc họp, agenda gắn thẳng vào cuộc họp
  ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Theo AI-eCabinet: chữ "phiên họp" trên UI của họ là agenda item.
- **BF-01:** đăng nhập sai quá số lần cho phép trong một phút thì hệ thống báo lỗi và yêu cầu thử lại sau (MSG414); không khoá tài khoản, không có thao tác mở khoá. Mọi thay đổi của Admin
  ghi audit log.
- **BF-02:** (bản 09/10/2026, đã thay bởi bản 24/11/2026 bên dưới) Organizer tạo cuộc họp, gán Thư ký đầu tiên, chọn
  người duyệt, gửi duyệt; Thư ký chuẩn bị thành phần, vai trò, agenda, tài liệu; công bố khi đã có Chair và Secretary; sau
  khi công bố chỉ được dời lịch.
- **BF-03:** Chair bắt đầu họp → hệ thống chuyển `InProgress` và báo người tham dự; Thư ký điểm danh chính thức; với
  họp Online/Hybrid người tham dự vào phòng họp. Mỗi mục agenda: Chair đặt mục hiện tại → thảo luận → nếu cần thì tạo và mở
  biểu quyết → bỏ phiếu → Chair đóng → hệ thống kiểm phiếu → Chair ghi kết luận *Passed / Not Passed / Deferred* → hoàn
  thành mục. Hết mục thì Chair kết thúc họp, hệ thống đóng phòng họp. **Biểu quyết chỉ diễn ra trong cuộc họp**
  ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)) — không vote trước / sau họp, không quorum, không Resolution.
- **BF-04:** (bản 09/10/2026, đã thay bởi bản 24/11/2026 bên dưới) Thư ký upload audio → chép lời → sửa transcript →
  AI sinh tóm tắt → AI sinh biên bản → Thư ký gửi duyệt → Chủ toạ duyệt là ban hành hoặc trả lại. Không ký số (PO
  14/11/2026, SCRUM-37 đóng).

## Cập nhật 24/11/2026

Rà BF-02 → BF-04 theo code trên `app` và Report 3 ngày 24/11/2026; BF-01 vẫn khớp nên giữ nguyên. Tên actor theo Report 3:
Meeting Organizer → **Member**, Meeting Approver → **Approver**, Email Service → **Resend Email**, AI Provider →
**AI Drafting Service**.

- **BF-02:**
  - **Tìm giờ họp chung (UC-81):** đầu luồng có quyết định *Find Common Time Slot?*. Có thì Member tìm khung giờ
    (người mời, thời lượng, khoảng ngày); hệ thống hiện khung giờ hợp với tất cả, không có thì hiện các khung nhiều người
    rảnh nhất (MSG445); Member chọn khung giờ và xác nhận, hệ thống lưu `Draft`. Không thì tạo cuộc họp như cũ (UC-13).
  - **Vai trò chọn ngay khi tạo:** form tạo cuộc họp có thành phần với đúng một Chair (Thư ký tuỳ chọn), nên bỏ bước
    "Assign First Secretary". Thành phần do Chair hoặc Secretary quản lý, nên lane đổi thành **Chair / Secretary**; thiếu
    Secretary khi công bố thì người trong lane này bổ sung.
  - **Duyệt / từ chối chỉ báo người tạo cuộc họp** (`Meeting.CreatedById`), không báo các thành viên: ô ghi *Notify
    Creator*, lane ghi **Member (Meeting Creator)** để không lẫn với vai trò Member trong cuộc họp.
  - **Huỷ cuộc họp (UC-22):** sau bước dời lịch có quyết định *Cancel Meeting?*; Member huỷ kèm lý do → `Cancelled`, báo
    người được mời, Resend gửi email huỷ. Sơ đồ vẽ ca đã `Published`; huỷ ở `Draft` → `Approved` và hệ thống tự huỷ sau 24
    giờ nằm trong đặc tả UC-22.
  - Email đổi lịch không còn quay về bước trả lời lời mời: mỗi người chỉ trả lời một lần, dời lịch không mở lại câu trả lời.
- **BF-03:** sau điểm danh, Thư ký bật ghi âm phòng (đã báo người dự) và hệ thống ghi, chép lời (UC-52); người dự có thể
  giơ tay xin phát biểu khi thảo luận (UC-50); kết thúc họp thì hệ thống đóng phòng và lưu bản ghi.
- **BF-04 (vẽ lại):** tải tệp audio chỉ còn là nhánh tuỳ chọn *Audio File to Upload?* (họp trực tiếp); bỏ bước "nhắc
  Thư ký upload" vì hệ thống không gửi thông báo này. Hệ thống chép lời (tách người nói, gợi ý Voice ID) → Thư ký rà và
  gán người nói → sinh tóm tắt, AI Drafting Service viết dự thảo ba mục → rà, sửa và **duyệt tóm tắt** (UC-61) → sinh
  biên bản: hệ thống **điền mẫu cố định**, không còn AI viết biên bản (UC-70) → Thư ký sửa, gửi duyệt → Chủ toạ duyệt là
  ban hành hoặc trả lại → hệ thống khoá, ghi nhật ký kiểm toán, báo người được đọc.
- **Không vẽ:** đính chính biên bản (UC-75) và ghi chú cuộc họp (UC-28) là thao tác tuỳ lúc, không nằm trên luồng chính.
- Đã render và xem lại cả ba sơ đồ; validator của drawio-skill 0 lỗi; PNG có nhúng model. Ảnh đã thay vào Report 3 §I.2.

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

Ngày 25/11/2026 (note 61 §4, góp ý của thầy về chữ trong hình): xuất lại bốn PNG ở tỉ lệ 2x, vẫn nhúng model
(`drawio -x -e -f png -s 2 -o <file>.drawio.png <file>.drawio`), và thay hình BF-01 → BF-04 trong Report 3 §I.2 bằng bản này;
mỗi hình có caption `Figure n:` và link ảnh gốc trên GitHub ngay dưới. Nội dung sơ đồ không đổi.

**Quy tắc:** không thêm note, shape ghi chú hay chữ chú thích tự do vào swimlane — xem
[DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md#no-notes-inside-diagrams).

## Lịch sử

Bản trước ngày 09/10/2026 có cấp Meeting Session, "Shared Voting Sub-flow", biểu quyết trước họp (BF-02) và sau họp
(BF-04). Danh sách việc cần sửa docs theo bản BF mới đã thực hiện xong trong đợt đồng bộ 10/10/2026 (gom UC, đánh số lại
UC-01 → UC-70); bảng đổi số ở [Master Use Case Inventory §5](../01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới).
