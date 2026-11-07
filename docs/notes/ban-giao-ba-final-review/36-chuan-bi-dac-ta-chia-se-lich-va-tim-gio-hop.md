# Chuẩn bị đặc tả — chia sẻ lịch làm việc và tìm giờ họp (eCab US #352 / #353)

**Ngày:** 07/11/2026 · **Nguồn nghiệp vụ:** quét code AI-eCabinet `bba2097f` (30/10/2026) — đọc code, chỉ ghi
nghiệp vụ. US #352 = chia sẻ lịch + tìm giờ; US #353 = trợ lý AI.
**Trạng thái:** ghi nhận và chuẩn bị, **chưa phải đặc tả**, phạm vi chưa chốt.

Note này trả lời ba câu: viết đặc tả **cái gì**, viết **ở đâu**, và **còn phải chốt gì trước**.
Đặc tả thật chỉ viết sau khi có quyết định ở §4 — và theo [note 33 §4](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md),
sau khi BE có code; không đặc tả trước rồi bắt code chạy theo.

## 1. Hiện trạng trong repo (07/11/2026)

| Hạng mục | Trạng thái thật |
|---|---|
| **SCRUM-137 Tìm giờ họp** | Jira `To Do`, giao Khánh. Comment `10449` chốt phạm vi **chỉ bận/rảnh**, không lộ tên cuộc họp, người dự hay địa điểm của lịch người khác. Chưa có UC, BR, code, sequence. |
| **Chia sẻ lịch / uỷ quyền xem lịch** | **Chưa có gì**: không ticket, không UC/BR, không bảng dữ liệu, không endpoint. BR-105 chỉ nói bỏ uỷ quyền *phản hồi lời mời* — khác hẳn việc này. |
| **Dữ liệu bận/rảnh** | Không endpoint nào. `GET /api/meetings` chỉ trả cuộc họp người gọi được xem và đã lọc quyền, nên không dùng lại được để xem lịch người khác. |
| **Màn hình** | Report 3 §I.5.1 Screen List dừng ở **15.1**; không có màn "Tìm giờ họp" hay "Chia sẻ lịch". Report 3 §III cũng chưa có mục cho hai màn này. |
| **Quyền** | `Capability` chỉ có `MeetingCreator`, `MeetingApprover`, `MinutesEditor`, `AdminUsers`. Không có quyền nào về xem lịch người khác. |
| **Trợ lý AI chat** | ADR-0043 ghi TrustCabinet **không có** "Trợ lý AI, Tìm giờ họp". SCRUM-137 vừa đưa Tìm giờ họp vào, nên câu đó trong ADR đã lệch — xem D4. |
| **Biên bản** | Đã đặc tả UC-58–UC-60, BR-383–BR-396, `MSG395`–`MSG406`; PR #271 đang mở thêm UC-61/64/65/82/83. eCab **không có** duyệt, ban hành hay ký biên bản, nên phần này không lấy từ eCab (§3.4). |

## 2. Số hiệu: chờ #271 trước khi đặt

| Loại | Đang thế nào | Đặc tả này lấy gì |
|---|---|---|
| **MSG** | `main` báo mã tiếp theo `MSG414`. #271 xin `MSG406`–`MSG425`, **trùng** `MSG406`–`MSG413` đã dùng trên `main` (cấu hình AI, đổi người nói một câu, định dạng tải biên bản) → #271 phải dời thành `MSG414`–`MSG433`. | Theo ghi chú 07/11: chừa **`MSG434`–`MSG439`** cho đặc tả này, mã tiếp theo **`MSG440`**. Đọc lại danh mục lúc thêm mã, đừng tin trí nhớ. |
| **UC** | Dừng ở **UC-81**; #271 xin UC-82 (ghi chú thư ký) và UC-83 (đính chính biên bản) | Từ **UC-84** trở đi. |
| **BR** | Dừng ở **BR-396**; #271 đề xuất BR-395–BR-413 nhưng 395/396 đã dùng, nên #271 cũng phải đánh lại | Số tiếp theo **sau khi** #271 chốt lại. |
| **Màn (§I.5.1, §III)** | Screen List đánh số theo FE, dừng ở 15.1 | Đặt số sau khi chốt màn thuộc FE nào (D3). |

## 3. Nghiệp vụ eCab, tóm tắt để viết đặc tả

### 3.1 Chia sẻ lịch làm việc

Người điều phối họp (bên eCab là vai trò hệ thống *Điều phối họp*, thường là thư ký) xem **giờ bận** của
một người khác — *chủ lịch* — theo mức chủ lịch cho phép. **Không bao giờ thấy tên hay chi tiết cuộc họp.**

| Mục | eCab làm |
|---|---|
| Mức xem | *Chỉ họp Thường* → *Thường và Nội bộ* → *Tất cả cuộc họp* (kể cả Mật, vẫn không hiện tên). |
| Thời gian chia sẻ | Một khoảng ngày, hoặc không giới hạn. Ngày ngoài khoảng là *chưa biết*, **không** tính là rảnh. |
| Hai cách bắt đầu | (1) Thư ký xin: chọn người, mức, khoảng ngày → chủ lịch chấp nhận (được cho mức thấp hơn / ít ngày hơn) hoặc từ chối. (2) Chủ lịch tự chia sẻ cho một người đang giữ vai trò Điều phối họp, luôn mức thấp nhất; người đó nhận hoặc từ chối. |
| Trạng thái | Chờ → Đang hiệu lực, hoặc Bị từ chối / Đã rút lại / Đã kết thúc. |
| Sau khi hiệu lực | Thư ký xin nâng mức hoặc gia hạn (mỗi lúc một yêu cầu nâng mức); chủ lịch tự hạ mức hoặc đổi khoảng ngày; ai gửi thì tự rút; một trong hai bên kết thúc ("Ngừng chia sẻ" / "Thôi xem lịch"). |
| Ràng buộc | Không tự chia sẻ cho chính mình; mỗi cặp người chỉ một chia sẻ đang chờ **hoặc** đang hiệu lực; chỉ người giữ vai trò Điều phối họp mới được nhận; chủ lịch phải có vai trò Thành viên; mất vai trò hoặc tài khoản bị khoá thì chia sẻ **tạm ngưng** (còn trong danh sách, không xem được lịch); hết hạn thì tự đóng và phải chia sẻ lại; mỗi bước ghi audit log và báo bên kia. |

Áp vào TrustCabinet — ba điểm phải định nghĩa lại, không tê nguyên:

1. **Vai trò.** Ta không có vai trò hệ thống "Điều phối họp" hay "Thành viên"; ta có `Capability`
   (`MeetingCreator`, …) và `MeetingRole` chỉ nằm trong một cuộc họp (`Chair`, `Secretary`, `Member`, `Guest`).
   Vậy "ai được xin xem lịch ai" phải phát biểu bằng capability, không bằng tên vai trò.
2. **Mức xem.** `Sensitivity` của ta là `Normal`, `Internal`, `Confidential`, `Secret` — bốn mức, không ba.
   Phải nói rõ mức *Tất cả* gồm những mức nào.
3. **"Chưa biết".** eCab coi ngày ngoài khoảng chia sẻ là chưa biết. Ta phải quyết nó khác "rảnh" và
   khác "bận" thế nào trên màn hình.

### 3.2 Tìm giờ họp

Màn có 2 tab: **Tìm khung giờ** (không dùng AI) và **Lịch được chia sẻ**.

| Mục | eCab làm |
|---|---|
| Tham số | Người cần dự (tối đa 10, có thể tính cả mình); thời lượng 15 phút–8 giờ; khoảng ngày tối đa 31 ngày; các thứ trong tuần; giờ làm việc; giờ nghỉ trưa; khoảng đệm 0–4 giờ; bước nhảy giờ bắt đầu 5 phút–2 giờ. |
| Mặc định | 2 giờ; từ hôm nay (hoặc ngày làm việc kế tiếp nếu hôm nay không đủ giờ) đến thứ Sáu; thứ 2–6, 08:00–17:00, nghỉ 12:00–13:00; đệm 30 phút; bước nhảy 30 phút. |
| Server tìm | Giờ bắt đầu sao cho cuộc họp nằm trọn trong giờ làm việc, không chạm giờ nghỉ, đủ khoảng đệm với lịch bận của mọi người. Không đề xuất giờ đã qua. Người tính bận là người **có mặt** trong cuộc họp (Chủ trì, Thư ký, Lãnh đạo, Thành viên, Tham dự hạn chế); chỉ là người tạo hay người duyệt thì không tính. Cuộc họp đã kết thúc thì tính bận đến giờ kết thúc thật. |
| Kết quả | Có giờ chung → liệt kê theo ngày. Không có → khung **nhiều người dự được nhất**, ghi rõ ai bận. Người **chưa chia sẻ lịch** hiện "Chưa được chia sẻ lịch", không tính là rảnh. |
| Tạo nháp | Bấm "Tạo nháp" trên một khung → mở hộp thoại tạo cuộc họp, điền sẵn: người đầu tiên làm Chủ trì, còn lại là thành viên, người đang tìm làm Thư ký. Server kiểm lại khung trước khi lưu; cuộc họp tạo ở Nháp rồi đi luồng duyệt bình thường. |
| Quyền | Cần quyền tạo cuộc họp. |
| Tab Lịch được chia sẻ | Lịch tuần chỉ gồm giờ bận của người đã chia sẻ với mình; mỗi người một màu; trùng giờ thì nằm cạnh nhau; không hiện tên cuộc họp. |

### 3.3 Trợ lý AI hỏi–đáp

eCab có trợ lý chat dùng 4 công cụ theo thứ tự: tìm người (tối đa 10, trùng tên thì hỏi lại), xem lịch bận
(gợi ý xin chia sẻ hoặc xin gia hạn), tìm khung giờ (tối đa 10 khung, mặc định không đệm), tạo cuộc họp nháp
có **thẻ xác nhận** trước khi lưu và server kiểm lại quyền.

**Không đặc tả đợt này.** ADR-0043 ghi TrustCabinet không có trợ lý AI, chưa có ticket, và nó cần quyết định
riêng về phạm vi, chi phí và dữ liệu gửi ra ngoài. Ghi ở đây để lúc họp phạm vi không phải quét lại.

### 3.4 Biên bản — chỉ để đối chiếu

eCab **không có** luồng duyệt, ban hành hay ký: `MinutesIssued` và quyền `minutes.sign` chỉ khai báo sẵn,
chưa có code. Phần eCab chạy thật: kiểm tra trước khi viết (bản ghi chưa xong, giọng chưa đặt tên), dự thảo AI,
chọn mẫu rồi ghép dự thảo vào mẫu, Chủ trì hoặc Thư ký sửa, xuất PDF/Word, cuộc họp Mật không xuất.
Ta đã có tương đương (`MSG405` chặn xuất biên bản Mật), và luồng duyệt–ban hành là của ta (PR #271).
**Không lấy gì thêm từ eCab cho biên bản.**

## 4. Phải chốt trước khi viết đặc tả

| # | Câu hỏi | Vì sao chặn |
|---|---|---|
| **D1** | Có làm **chia sẻ lịch** (uỷ quyền xem bận) không, hay cho mọi người trong tổ chức xem bận/rảnh của nhau luôn? | Quyết định cả bộ: có chia sẻ thì mới có màn danh sách/yêu cầu/nhận, bảng dữ liệu mới và 4–5 mã MSG. Không chia sẻ thì "bận" lấy trực tiếp từ cuộc họp, chỉ còn một màn Tìm giờ họp, và **không cần** phần lớn nghiệp vụ ở §3.1. ADR-0004 (một tổ chức, không đơn vị) làm phương án "xem chung" khả thi về mặt lý thuyết, nhưng là quyết định về quyền riêng tư, phải PO chốt. |
| **D2** | SCRUM-137 gồm tab nào: chỉ **Tìm khung giờ**, hay cả **Lịch được chia sẻ**? | Ticket hiện chỉ ghi "chỉ bận/rảnh". Tab thứ hai là màn riêng, cần quyết định trước khi đặt số màn. |
| **D3** | Màn thuộc **FE-04** (lịch/cuộc họp) hay **FE mới**? | Đặt số §I.5.1/§III và tên feature. |
| **D4** | **Trợ lý AI** có vào phạm vi không? | Nếu có: ADR mới, cập nhật `frontend/src/pages/scopeData.ts`, chi phí model, và rà CSP nếu gọi dịch vụ ngoài. Nếu không: ghi rõ "không làm" để lần sau khỏi bàn lại. |
| **D5** | Ai được **xin** xem lịch ai? | Phát biểu bằng capability (`MeetingCreator`?) chứ không bằng tên vai trò như eCab (§3.1). |
| **D6** | Có thêm capability mới không (`CalendarShareRequest`, `CalendarShareGrant`, …)? | Thêm `Capability` là sửa enum, seed, ma trận quyền, `PermissionCatalog` và test — phải làm cùng lúc với đặc tả. |

## 5. Khung đặc tả đề xuất (chỉ để biết sẽ viết bao nhiêu, chưa phải nội dung)

- **UC (đề xuất, số thật sau #271):** UC-84 *Yêu cầu xem lịch* (thư ký xin), UC-85 *Chủ lịch tự chia sẻ*,
  UC-86 *Tìm khung giờ*, UC-87 *Xem lịch được chia sẻ*. Gộp hay tách phụ thuộc D1/D2.
- **BR:** khoảng 8–12 quy tắc — mức xem theo `Sensitivity`, ngày ngoài khoảng là chưa biết, một chia sẻ cho
  mỗi cặp, tạm ngưng khi mất quyền/khoá tài khoản, hết hạn tự đóng, không lộ tên cuộc họp, giới hạn 10 người /
  31 ngày / đệm 0–4 giờ / bước nhảy 5 phút–2 giờ, tạo nháp điền sẵn và kiểm lại trước khi lưu.
- **MSG:** `MSG434`–`MSG439` (6 mã) — đề xuất chia: gửi yêu cầu, chấp nhận/từ chối, rút/kết thúc, tìm xong,
  không có khung nào, lỗi. Chốt lại khi viết.
- **Màn:** 2–4 màn mới (danh sách chia sẻ, dialog xin chia sẻ, Tìm giờ họp, Lịch được chia sẻ) + 2 dòng
  §I.5.1 + ảnh chụp khi FE có màn (cách làm theo [note 06](06-plan-dac-ta-man-hinh.md)).
- **Sequence:** 1–2 file `.txt` cho UC tìm giờ và UC chia sẻ; vẽ draw.io theo
  [drawio-skill](../../../.agents/skills/drawio-skill/SKILL.md) như các đợt trước.
- **ERD:** nếu D1 = có, cần bảng chia sẻ lịch (tên và cột do BE đặt khi làm, ghi vào
  [02-erd](../../02-design/02-erd.md) cùng migration — không chốt trong note này).

## 6. Thứ tự làm

1. Chốt D1–D6 với PO. Không viết trước.
2. Nếu D1 = có: tạo ticket Jira cho phần chia sẻ lịch (hiện chỉ có SCRUM-137), ghi rõ phụ thuộc.
3. Chờ **PR #271 merge**, rồi chốt lại dải `MSG`, số UC, số BR thật (§2).
4. BE viết UC Specification cơ bản + sequence cho phần đã chốt ([DEVELOPMENT_WORKFLOW §2.1](../../DEVELOPMENT_WORKFLOW.md#21-changing-a-requirement-during-implementation)),
   ghi mọi chỗ lệch ticket vào Jira.
5. BA Final Review mới đưa vào Report 3: §II (UC), §V.1 (BR), §V.2 (MSG), §III + §I.5.1 (màn), rồi
   `python scripts/sync_reports.py`.
6. Sửa ADR-0043 (hoặc ADR mới) + `scopeData.ts` cho câu "TrustCabinet không có Tìm giờ họp".
7. Chụp ảnh và viết đặc tả màn theo note 06, chỉ khi FE đã có màn chạy thật.

## 7. Câu hỏi cho PO

1. Bận/rảnh lấy từ đâu: chia sẻ lịch (D1 = có) hay mặc định ai cũng xem được bận/rảnh của người khác?
2. Nếu có chia sẻ: có giữ đủ ba mức xem như eCab không, hay hai (bỏ mức *Tất cả* vì có mức `Secret`)?
3. Những người dự hạn chế (eCab có loại này) — ta có tương đương không, hay chỉ `Chair`/`Secretary`/`Member`/`Guest`?
4. Giữ nguyên các con số của eCab (10 người, 31 ngày, 8 giờ, 0–4 giờ đệm, 5 phút–2 giờ bước nhảy) hay tự đặt?
5. Có làm tab "Lịch được chia sẻ" đợt này không (D2)?
6. Trợ lý AI: làm trong đồ án hay chỉ ghi nhận (D4)?

## 8. Liên quan

- [note 33 §4](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md) — SCRUM-137 được tạo thế nào và phạm vi "chỉ bận/rảnh".
- [note 06](06-plan-dac-ta-man-hinh.md) — quy trình chụp và đặc tả màn hình Report 3.
- **PR #271** (`docs/dac-ta-so-bo-scrum-30-33`, đang mở) — đặc tả sơ bộ SCRUM-30/33, file `docs/notes/NOTE-dac-ta-so-bo-scrum-30-33.md`
  chỉ có trên nhánh đó; là mốc phải chờ để đánh số UC/BR/MSG.
- [Danh mục mã thông báo](../../03-api/00-danh-muc-ma-thong-bao.md) — đọc lại "Mã tiếp theo" trước khi thêm.
- [ADR-0043](../../05-decisions/0043-thanh-phan-theo-ai-ecabinet-giu-sidebar.md) — câu "TrustCabinet không có Tìm giờ họp" đã lệch.
