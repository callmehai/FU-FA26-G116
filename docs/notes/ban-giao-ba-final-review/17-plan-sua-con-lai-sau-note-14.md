# Plan sửa phần còn lại của note 14 (BA Final Review, 29/10/2026)

**Người ghi:** KhanhPG (BA). Rà [note 14](14-ghi-chu-nhan-27-28-10-2026-phan-loai-theo-fe.md) với `main` và Jira ngày 29/10/2026.
Phần ghi âm và Voice ID nằm ở [note 16](16-ghi-am-voice-id-28-29-10-2026.md), không lặp ở đây.

## 0. Đã xong, không làm lại

- FE-06: SCRUM-20, 26, 97 ở **In Review**.
- FE-07: tài liệu đã lên `main` (PR #186). Ngày 29/10 đã chuyển **In Review**: SCRUM-23, 58, 110, 111, 112, 113 (cùng 21, 22,
  107 đã chuyển từ 28/10).
- Còn treo của FE-07, không chặn kiểm thử: câu hỏi PO "Quản trị viên có thấy tệp và `meetingId` của cuộc họp Thường mà họ
  không tham gia" (SCRUM-111, Lộc hỏi). PO trả lời khác as-built thì mở ticket riêng.

## 1. Nguyên tắc khi sửa Report 3

Report 3 là **đặc tả yêu cầu**, viết cho người đọc nghiệp vụ, không phải tài liệu kỹ thuật:

- Viết theo hành vi người dùng thấy và quy tắc nghiệp vụ. **Không** đưa tên nhà cung cấp hay công nghệ (Resend, Cloudflare R2,
  LiveKit, SMTP…), endpoint, webhook, tên worker/job, khoá cấu hình, tên bảng/cột, mã HTTP.
- **Actor chuẩn:** gửi email dùng đúng actor Report 3 đã có, **"Email / SMTP Service"** (UC có gửi thư và context
  diagram đều dùng tên này); không đổi tên, không thêm "Resend". Lưu tệp (Cloudflare R2) **không phải actor**: kho tệp là
  một phần của hệ thống, Report 3 không nhắc tới. Đăng nhập Google giữ actor "Google Identity Provider" như hiện có.
- Chi tiết kỹ thuật để ở `docs/03-api/`, ADR và ERD; Report 3 chỉ cần khớp về nghiệp vụ.
- Sửa DOCX `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`, rồi chạy
  `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`.
  Không sửa snapshot Markdown. Mỗi lượt sửa ghi một dòng Change Log.

## 2. Việc cần làm, theo thứ tự

### Lượt 1 — FE-04, SCRUM-99: huỷ cuộc họp và tự huỷ (Hải)

Nguồn đối chiếu: ADR-0032, `docs/03-api/05-quan-ly-cuoc-hop.md` mục "SCRUM-99", comment Jira SCRUM-99 `10296`.

Report 3:

- [x] **BR-301** viết lại: cuộc họp chưa bắt đầu (Draft, WaitingApproval, Rejected, Approved, Published) mà quá 24 giờ sau giờ
      **bắt đầu** dự kiến thì hệ thống tự huỷ, lý do "Không diễn ra theo lịch", người thực hiện là Hệ thống, báo Người tổ chức.
      Bỏ câu "delay is configurable".
- [x] **BR-302** (tự kết thúc sau 4 giờ): đánh dấu **ngừng áp dụng** theo PO 27/10, không xoá số BR.
- [x] **UC-75**: thêm EX "Lý do huỷ quá 1.000 ký tự" → `MSG345`; thêm ý huỷ khi đang chờ duyệt thì báo Người duyệt;
      Secondary Actor giữ "Email / SMTP Service".
- [x] **UC-16, UC-17**: lịch có nhóm lọc "Đã huỷ" (mặc định tắt, bật thì tên cuộc họp gạch ngang); danh sách Cuộc họp lọc
      được "Đã huỷ".
- [x] Bảng System Messages: thêm `MSG345`.
- [x] Sơ đồ trạng thái trong Report 3 (nếu có hình): thêm `Cancelled`, khớp `docs/02-design/01-vong-doi-cuoc-hop.md`.

Ngoài Report 3:

- [ ] Sequence UC-75 (nếu đã có) và nhánh tự huỷ: đối chiếu as-built.
- [ ] Comment Jira SCRUM-99 tóm tắt đã sửa → chuyển **In Review**.

### Lượt 2 — ERD drawio (SCRUM-99, SCRUM-15)

`docs/13-erd/chen`, `hien-tai`, `muc-tieu`. Theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md) và drawio-skill:
sửa có chọn lọc, giữ ID, xuất lại PNG, kiểm bằng mắt.

- [x] `meetings`: thêm 4 cột huỷ (thời điểm huỷ, người huỷ — null là Hệ thống, lý do, trạng thái trước khi huỷ). Lấy tên đúng
      ở `docs/02-design/02-erd.md`.
- [x] `meeting_status_history.actor_id` cho phép null.
- [x] `email_messages`: thêm `provider_message_id`, `delivery_state`, `delivery_state_at`, `delivery_detail`.
- [x] Biểu đồ Chen chỉ thêm thuộc tính nếu mức chi tiết hiện tại có cột tương đương; không thì bỏ qua.

> Đã làm 29/10 (BA, KhanhPG): chỉ `muc-tieu` được sửa và xuất lại PNG (đồng thời `02-erd.md`: `ActorId` nullable, `DeliveryStateAt`, `DeliveryDetail`). `chen` không có thuộc tính nên bỏ qua. `hien-tai` **không sửa tay**: README cấm, và snapshot đang cũ nhiều chỗ (còn `starts_at`/`ends_at`); cần sinh lại từ DB sau khi chạy migration.
> `muc-tieu` còn `AgendaItem` cũ (`PlannedDurationMinutes`, `PresenterName`) — ngoài phạm vi lượt này.

### Lượt 3 — FE-04, SCRUM-69: bắt đầu và kết thúc cuộc họp (Huy)

- [x] Đối chiếu UC-21, UC-22 với code trên `main` (PR #175 sửa thời điểm ghi thông báo khi bắt đầu họp): ai được bấm,
      trạng thái nào, ai được báo, mã thông báo.
- [ ] Lệch thì sửa Report 3; không lệch thì comment Jira "đã đối chiếu, không lệch".
- [ ] Chuyển SCRUM-69 **In Review**.

### Lượt 4 — FE-13, SCRUM-15: gửi email (Huy Nguyễn Trường)

Theo mục 1: Report 3 **không** nhắc Resend hay webhook.

- [x] Chỉ kiểm phần nghiệp vụ: email thư mời/thông báo gửi khi nào, cho ai; gửi lỗi thì hệ thống làm gì (người dùng thấy gì).
      Actor giữ "Email / SMTP Service"; nếu có chữ "Gmail" hay "Resend" thì bỏ.
- [x] Trạng thái giao thư (đã giao, bị trả về…) chỉ ghi vào Report 3 nếu có màn hình hiện cho người dùng; không có thì bỏ qua.
- [ ] ERD làm ở lượt 2. Chuyển SCRUM-15 **In Review**.

### Lượt 5 — FE-05, SCRUM-25 và SCRUM-98: chương trình nghị sự (Huy)

Nguồn: [note 08](08-chuong-trinh-nghi-su.md) (đã cập nhật 24/10).

- [x] Đối chiếu UC-23a–…, UC-24, 25, 26, 77 với code trên `main`.
- [x] UC-23 (sửa mục nghị sự): ghi chú gắn tài liệu vào mục nghị sự giờ là chức năng riêng UC-33b, chỉ Thư ký dùng; bỏ bước
      chọn tài liệu trong UC-23a/23b nếu không còn trên màn hình.
- [ ] Chuyển SCRUM-25, SCRUM-98 **In Review**.

> Đã làm 29/10: Report 3 (DOCX + sync) cho lượt 1, 3, 4, 5; chưa comment Jira và chưa chuyển trạng thái ticket (để người review làm).

### Lượt 6 — FE-08, SCRUM-41: phòng họp trực tuyến (Hải) — chờ

- [ ] Chờ PR #147 (UC-37–42, BR-322–331) merge, rồi đối chiếu mã thông báo phòng họp và các lệch ở comment SCRUM-41 26/10.
      Nhớ ADR-0040: phòng gọi không còn ô "Lời nói" và không ghi trộn cuộc gọi (xem note 16).

## 3. Sau mỗi lượt

- Không commit sau từng lượt: làm xong lượt 1–5 rồi commit một lần (message tiếng Việt) và push.
- Gạch mục tương ứng ở bảng §5 note 14 và ở file này.
