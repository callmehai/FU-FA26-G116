# Ghi chú nhận ngày 27–28/10/2026 và phân loại theo Feature (chuẩn bị BA Final Review)

**Người ghi:** KhanhPG (BA). **Nguồn:** tin nhắn từ bot (leader/PO Hải) và gumarr (Lộc) ngày 27/10 và 28/10/2026, trạng thái Jira
đọc ngày 28/10/2026. Note này gom lại mọi việc BA được giao để xử lý theo thứ tự, không thay thế Jira hay đặc tả.

Số Feature theo [danh mục UC](../../01-requirements/00-master-use-case-inventory.md) (baseline 15 Feature, UC-01 → UC-77).

## 1. Trạng thái Jira và bước tiếp theo

**Cập nhật 28/10/2026 (sau khi PR #178 merge):** workflow Jira là `BA Final Review` → `In Review` (kiểm thử) → `Done`.
Đã chuyển **`In Review`**: SCRUM-20, 97, 21, 22, 107. Đã chuyển **`Done`**: SCRUM-91, 92 (sequence và class diagram FE-07).
SCRUM-110, 111, 112 đã gán Lộc. SCRUM-26 chờ PR sửa `10-diem-danh.md` (mục 3.4) merge rồi mới chuyển.
Bảng bên dưới giữ trạng thái đọc lúc 27/10, cột cuối ghi việc đã làm.

**Cập nhật 29/10/2026:** SCRUM-26 đã ở `In Review`. PR #186 (FE-07) đã merge; chuyển **`In Review`** SCRUM-23, 58, 110, 111, 112, 113.
Việc còn lại lên kế hoạch ở [note 17](17-plan-sua-con-lai-sau-note-14.md).

Cả 18 ticket dưới đây đang ở **BA Final Review** (đọc ngày 28/10/2026). Việc của BA ở trạng thái này: đối chiếu code đã
merge với UC, BR, MSG, sequence, class diagram, ERD; sửa tài liệu; sau đó chuyển ticket sang kiểm thử khi không còn lệch.

| FE | Ticket | Nội dung | Người làm | Việc BA còn lại | Có thể chuyển kiểm thử khi |
|---|---|---|---|---|---|
| FE-04 | SCRUM-99 | Huỷ cuộc họp, tự huỷ quá hạn (UC-75, UC-16, UC-17) | Hải | Mục 3.1 | Xong mục 3.1 |
| FE-04 | SCRUM-69 | Bắt đầu và kết thúc cuộc họp (UC-21, UC-22) | Huy | Đối chiếu như mục 3.4 | Đối chiếu xong, không lệch |
| FE-05 | SCRUM-25 | Soạn và điều hành nghị sự (UC-23–26) | Huy | Mục 3.3 | Xong mục 3.3 |
| FE-05 | SCRUM-98 | Đồng bộ nghị sự theo PO 16/10 | Huy | Mục 3.3 | Xong mục 3.3 |
| FE-06 | SCRUM-20 | Mời và xác nhận tham dự | Huy | Đã cập nhật 24/10 (#136) | **Đã chuyển In Review 28/10** |
| FE-06 | SCRUM-97 | Chair giao chủ toạ (UC-28b, UC-73) | Huy | Đã cập nhật 24/10 (#136) | **Đã chuyển In Review 28/10** |
| FE-06 | SCRUM-26 | Điểm danh (UC-31) | Huy | Đã sửa `docs/03-api/10-diem-danh.md` §6 (mục 3.4), chờ PR merge | Sau khi PR sửa tài liệu merge |
| FE-07 | SCRUM-21 | Xem tài liệu cuộc họp (UC-32a, 32c) | Lộc | Đã đối chiếu (PR #166, #178) | **Đã chuyển In Review 28/10** |
| FE-07 | SCRUM-22 | Quyền đọc tài liệu theo quyền xem cuộc họp | Lộc | Làm cùng SCRUM-21 | **Đã chuyển In Review 28/10** |
| FE-07 | SCRUM-107 | Tải từ máy lên cuộc họp, R2 (UC-33a AF-1) | Lộc | Đã đối chiếu | **Đã chuyển In Review 28/10** |
| FE-07 | SCRUM-23 | Góp ý tài liệu (UC-35) | Lộc | Mục 3.2 | Xong mục 3.2 |
| FE-07 | SCRUM-58 | Ticket cha của kho tài liệu | Lộc | Mục 3.2 | Khi 110, 111, 112, 113 xong |
| FE-07 | SCRUM-110 | Thư mục kho (UC-72a–d) | Lộc (đã gán 28/10) | Mục 3.2 | Xong mục 3.2 |
| FE-07 | SCRUM-111 | Tài liệu trong kho (UC-34, 36, 71a–c) | Lộc (đã gán 28/10) | Mục 3.2 | Xong mục 3.2, có trả lời PO |
| FE-07 | SCRUM-112 | Gỡ tài liệu, gắn/gỡ mục nghị sự (UC-33c, 33d) | Lộc (đã gán 28/10) | Mục 3.2 | Xong mục 3.2 |
| FE-07 | SCRUM-113 | FE Quản trị viên xem kho tài khoản khoá (UC-34b) | Hải | Mục 3.2 | Xong mục 3.2 |
| FE-08 | SCRUM-41 | Phòng họp trực tuyến (UC-37–40) | Hải | Mục 3.5 | Xong mục 3.5 |
| FE-13 | SCRUM-15 | Gửi email qua Resend (ADR-0031) | Huy Nguyễn Trường | Mục 3.6 | Xong mục 3.6 |

Ngoài các ticket trên:

- **SCRUM-91, SCRUM-92** (sequence và class diagram FE-07, của BA) đã chuyển **Done** ngày 28/10. Phần UC-74 vẫn PROPOSED vì
  thuộc SCRUM-96; sequence và class diagram FE-07 còn đổi sang as-built ở BA Final Review SCRUM-110/111/112 (mục 3.2).
- **SCRUM-96** (biểu quyết và UC-74, FE-11, Huy) còn **To Do**.

## 2. Việc đã làm hoặc đang chờ ở PR

- PR #165 đã merge (note bàn giao, script đối chiếu).
- PR #166 đã merge nhưng **vào nhánh cha** `docs/scrum-58-ban-giao-kho-tai-lieu` (base PR chưa đổi về `main`). **PR #178**
  (nhánh cha vào `main`) đã merge ngày 28/10/2026 (merge commit `3fcb7b5`), nên tài liệu FE-07 đã có trên `main`.
- PR #147 (phòng họp trực tuyến, UC-37–42, BR-322–331) và PR #146 (biểu quyết) vẫn mở, base `docs/design-diagrams`.

## 3. Việc cần làm theo từng Feature

### 3.1 FE-04 — Quản lý cuộc họp (SCRUM-99, SCRUM-69)

Nguồn: tin bot 4:25 PM và 4:33 PM ngày 27/10, comment SCRUM-99 `10296`, ADR-0032, PR #170, #171, #175.

- **BR-301:** tự huỷ tính 24 giờ từ **giờ bắt đầu dự kiến** (không phải giờ kết thúc), áp cho mọi trạng thái chưa bắt đầu
  (Draft → Published), không chỉ Published.
- **BR-302** (tự `Ended` sau 4 giờ): **bỏ** theo PO.
- **UC-75:** thêm `MSG345` "Lý do huỷ không được vượt quá 1.000 ký tự."; huỷ lúc đang chờ duyệt thì báo người duyệt.
- **UC-16, UC-17:** lịch có nhóm lọc "Đã huỷ" (tắt mặc định, bật lên thì tên cuộc họp gạch ngang); danh sách Cuộc họp lọc được
  "Đã huỷ".
- Sơ đồ trạng thái thêm `Cancelled`: dev đã sửa `docs/02-design/01-vong-doi-cuoc-hop.md`.
- Dữ liệu: bảng `meetings` thêm 4 cột `cancelled_*`; `meeting_status_history.actor_id` cho phép null (Hệ thống). **BA cập nhật 3 ERD
  drawio** trong `docs/13-erd/` (xem mục 4).
- Liên quan FE-07: cuộc họp đã huỷ trả `MSG349` ở mọi thao tác tài liệu (xem mục 3.2).
- SCRUM-69: PR #175 sửa thời điểm ghi thông báo Start; đối chiếu UC-21, UC-22 với code.

### 3.2 FE-07 — Kho tài liệu và tài liệu cuộc họp (SCRUM-21, 22, 23, 58, 107, 110, 111, 112, 113)

Nguồn: tin bot 6:58 PM, 10:55 PM, 11:26 PM, 11:38 PM, 11:56 PM ngày 27/10; tin gumarr 4:29 PM; comment Jira SCRUM-58 `10290`,
SCRUM-110 `10291`, SCRUM-111 `10292`, SCRUM-112 `10293`, SCRUM-23 `10294`; PR #169 (BE), #172 (FE và BE), #174 (SCRUM-113).

**Đã làm (PR #166, chờ #178 lên `main`):**

- Quyết định PO 27/10: hàng đợi tải lên theo từng tệp (ADR-0033) → BR-286 và UC-33a POST-4 "theo từng tệp", BR-194 giữ nguyên;
  hạn mức **10 GB cho cả hệ thống** (ADR-0034) thay "1 GB mỗi người" → UC-36, UC-33a/33b AF-1, đầy thì `MSG346`.
- UC-34b (Quản trị viên đọc kho tài khoản khoá): quyền `AdminUsers`, chỉ xem và tải về, ẩn tệp và thư mục của cuộc họp
  Confidential/Secret, `MSG158` khi mở tệp lỗi, `MSG176` khi tải danh sách lỗi, `MSG175` khi tài khoản còn hoạt động; BR-333–335;
  ADR-0035.

**Đã làm trên nhánh `docs/ba-final-review-fe-07` (28/10/2026):**

1. Report 3 DOCX: thêm EX `MSG347` (UC-71b), `MSG348` (UC-72a/b, UC-35), `MSG349` (UC-33a–d, UC-35); cập nhật UC-34b theo màn PR #174 (nút "Xem kho tài liệu" tắt + tooltip khi tài khoản còn hoạt động; không tìm kiếm/lọc; tài khoản kích hoạt lại → `MSG175` về màn Người dùng; thư mục không còn hoặc Confidential/Secret → về gốc kho); Permission Matrix thêm quyền đọc kho tài khoản khoá (`Restricted ²⁰`); Change Log và bảng System Messages. Đã chạy `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3` → snapshot MD + `05-use-case-specifications.md`.
2. Sequence source `.txt` as-built (Controller/Service, không Endpoints/HTTP/SignalR; EX `MSG347`/`MSG348`/`MSG349`) cho UC-33a×2, UC-33b–d, UC-35, UC-71b, UC-72a/b; xuất lại PNG SequenceDiagram.org cạnh `.txt`. Quy chuẩn Naming bổ sung trong `docs/06-sequencediagram/README.md`.
3. Tab draw.io + `drawio-png/` dựng lại bằng `scripts/txt_to_sequence_drawio_png.py` (style agenda UC-23a: actor/boundary/control/database, title UC, message 13px; xuất file 1 trang, không `--embed-diagram`).
4. Class diagram FE-07 as-built (§6e): tách 3 operation gắn mục nghị sự; thêm `getDocumentContent` kho cá nhân; PNG xuất lại.

**Còn lại (không chặn PR tài liệu này):**

1. **Câu hỏi PO còn mở (SCRUM-111, Lộc):** Quản trị viên có thấy tệp và `meetingId` của cuộc họp mức **Thường** mà Quản trị viên không tham gia hay không. As-built hiện ẩn Confidential/Secret, vẫn hiện mức Thường — chờ PO.
2. Chuẩn hoá câu chữ "submission" trong EX UC-33a/33b/36 (cosmetic; nghĩa đã chốt ADR-0033).
3. Rà tab draw.io gộp bằng giao diện (khuyến nghị trước khi merge nếu leader muốn).

### 3.3 FE-05 — Chương trình nghị sự (SCRUM-25, SCRUM-98)

Nguồn: tin bot 11:56 PM ngày 27/10. Code đã lên `main`. Đối chiếu UC-23 → UC-26, UC-77 với code; lưu ý UC-33b (gắn tài liệu vào mục
nghị sự) giờ là chức năng riêng, API sửa mục nghị sự vẫn nhận `documentIds` và chỉ Thư ký dùng được. Các điểm đã cập nhật ngày 24/10
nằm ở [note 08](08-chuong-trinh-nghi-su.md).

### 3.4 FE-06 — Thành phần tham dự (SCRUM-20, SCRUM-26, SCRUM-97)

Nguồn: tin bot 11:56 PM ngày 27/10. Tài liệu đã cập nhật ngày 24/10 (PR #136). "Không còn gì thì chuyển sang kiểm thử."
SCRUM-26 còn `docs/03-api/10-diem-danh.md` mục 5–6 lệch Report 3. **Đã sửa 28/10 (PR #180 đã merge):** mục 6 bỏ chip "Có mặt x/y" (Report 3, UC-31 và BR-172 ghi không hiện tổng;
FE không render `totals`) và ghi biểu tượng "Điểm danh" riêng ở góc dưới bên phải của phòng họp; mục 5 (migration `AddAttendanceVersions`,
phiên bản riêng từng người, ADR-0025) khớp Report 3 nên giữ nguyên. Chuyển SCRUM-26 sang `In Review` khi không còn lệch. Xem [note 04](04-scrum-26-diem-danh.md) và
[note 02](02-scrum-97-giao-chu-toa.md).

### 3.5 FE-08 — Phòng họp trực tuyến (SCRUM-41)

Nguồn: tin bot 10:55 PM ngày 27/10. Phần mã thông báo xong ở PR #162; lệch đặc tả ghi ở comment SCRUM-41 ngày 26/10. Tài liệu
UC-37–42, BR-322–331 nằm ở PR #147 (còn mở). Xem [note 11](11-tai-lieu-fe-phong-hop-ghi-am.md).

### 3.6 FE-13 — Email và thông báo (SCRUM-15)

Nguồn: tin bot 3:10 PM ngày 27/10, PR #167, ADR-0031 (`docs/05-decisions/0031-gui-email-qua-resend.md`).

- Email gửi qua **Resend** thay Gmail SMTP.
- Bảng `email_messages` thêm 4 cột: `provider_message_id`, `delivery_state`, `delivery_state_at`, `delivery_detail`. `02-erd.md` dev đã
  sửa; **BA cập nhật 3 ERD drawio** (mục 4).
- Thêm endpoint hệ thống `POST /api/email/resend/webhook`, chỉ Resend gọi, không có màn hình: ghi vào
  `docs/03-api/07-gui-email-thu-moi.md` (dev đã ghi), đối chiếu Report 3 xem có UC hay BR nào cần nhắc tới.

## 4. Việc dùng chung

- **ERD drawio** trong `docs/13-erd/` (`chen`, `hien-tai`, `muc-tieu`): cập nhật cho SCRUM-15 (4 cột `email_messages`) và SCRUM-99
  (4 cột `cancelled_*`, `actor_id` null). Theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md): sửa có chọn lọc, giữ ID.
- **Catalogue MSG:** `MSG345` thuộc SCRUM-99; `MSG346`–`MSG349` thuộc FE-07 (trong đó `MSG349` dùng chung với SCRUM-99); mã tiếp theo
  theo catalogue. Không đặt mã mới trong PR tài liệu khi chưa cấp trong catalogue.
- **Thứ tự xử lý đề xuất:** (1) ~~merge PR #178~~ xong; (2) ~~chuyển kiểm thử SCRUM-20, 97, 21, 22, 107~~ xong; (3) ~~merge PR #180 sửa
  `10-diem-danh.md`~~ xong, còn chuyển SCRUM-26 sang `In Review`; (4) FE-07 mục 3.2 (Report 3/MSG + sequence + class as-built trên nhánh `docs/ba-final-review-fe-07`; còn câu hỏi PO); (5) FE-04 mục 3.1
  và 2 ERD; (6) FE-13 mục 3.6; (7) FE-05 và FE-08.

## 5. Danh sách việc cần BA review còn lại (gom từ mục 3)

| # | Việc | Feature | Ticket | Ghi chú |
|---|---|---|---|---|
| 1 | ~~Thêm EX cho `MSG347`, `MSG348`, `MSG349` vào Report 3~~ | FE-07 | 110, 111, 23, 112 | DOCX + sync MD xong trên nhánh `docs/ba-final-review-fe-07` |
| 2 | ~~UC-34b bước 1: nút "Xem kho tài liệu" tắt kèm lý do; không tìm kiếm/lọc; về gốc kho khi thư mục không còn~~ | FE-07 | 113 | Report 3 đã cập nhật |
| 3 | Hỏi PO: Quản trị viên có thấy tệp và `meetingId` của cuộc họp Thường mà họ không tham gia | FE-07 | 111 | Câu hỏi của Lộc; as-built giữ hiện Thường |
| 4 | ~~Sequence PNG org + class diagram FE-07 as-built~~ | FE-07 | 110, 111, 112 | Xong trên nhánh `docs/ba-final-review-fe-07` |
| 5 | ~~Permission Matrix thêm quyền đọc kho tài khoản khoá~~ | FE-07 | 113 | Report 3 đã cập nhật |
| 6 | Mở file draw.io gộp bằng giao diện kiểm tra tab đã dựng lại | FE-07 | — | Khuyến nghị trước merge |
| 7 | ~~BR-301, BR-302, UC-75 `MSG345`, UC-16/17 lọc "Đã huỷ", sơ đồ trạng thái~~ | FE-04 | 99 | Report 3 đã sửa 29/10 (BR-298/300/301/302, UC-75 AF-2 + EX-4, UC-17 AF-4, MSG345, từ điển dữ liệu); Report 3 không có sơ đồ trạng thái chỉnh sửa được nên bỏ qua |
| 8 | ~~Đối chiếu UC-21, UC-22 với code~~ | FE-04 | 69 | Đã đối chiếu 29/10, không lệch (PR #175 chỉ đổi thời điểm ghi thông báo, Report 3 đã nói lỗi thông báo không huỷ việc bắt đầu) |
| 9 | ~~3 ERD drawio: 4 cột `email_messages` và 4 cột `cancelled_*`, `actor_id` null~~ | FE-13, FE-04 | 15, 99 | Xong `muc-tieu` (sửa + xuất PNG). `chen` không có thuộc tính nên bỏ qua. `hien-tai` không sửa tay (snapshot sinh từ DB, README cấm; đang cũ nhiều chỗ) — cần sinh lại sau migration |
| 10 | ~~Đối chiếu phần gửi email với Report 3 (chỉ nghiệp vụ, không đưa Resend/webhook vào Report 3)~~ | FE-13 | 15 | Xong 29/10: BR-300 thêm quy tắc thư huỷ chỉ gửi người đã nhận thư mời; bỏ chữ Gmail ở mô tả giao diện SMTP; không có màn hình trạng thái giao thư nên không thêm |
| 11 | ~~Đối chiếu UC-23–26, UC-77 với code; UC-33b liên quan API sửa mục nghị sự~~ | FE-05 | 25, 98 | Xong 29/10: UC-23a–d, 24, 25, 77 khớp code; UC-26 thêm Guest (đọc), từ điển dữ liệu AgendaItem bỏ thời lượng. Form sửa mục vẫn có ô chọn tài liệu nên UC-23a/b giữ nguyên |
| 12 | Đối chiếu mã thông báo phòng họp và lệch ghi ở comment 26/10 | FE-08 | 41 | Mục 3.5, PR #147 còn mở |
| 13 | ~~Chuyển SCRUM-26 sang `In Review` sau khi PR #180 merge~~ | FE-06 | 26 | Đã ở In Review (29/10) |
