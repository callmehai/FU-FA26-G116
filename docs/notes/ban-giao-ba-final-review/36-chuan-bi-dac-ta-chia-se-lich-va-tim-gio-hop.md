# Chuẩn bị đặc tả — chia sẻ lịch làm việc và tìm giờ họp (eCab US #352 / #353)

**Ngày:** 07/11/2026 · **Nguồn nghiệp vụ:** quét code AI-eCabinet `bba2097f` (30/10/2026) — đọc code, chỉ ghi
nghiệp vụ. US #352 = chia sẻ lịch + tìm giờ; US #353 = trợ lý AI.
**Trạng thái:** phạm vi đã chốt ngày 07/11/2026 (§4), **chưa phải đặc tả**.

Note này trả lời ba câu: viết đặc tả **cái gì**, viết **ở đâu**, và **còn phải chốt gì trước**.
Đặc tả thật chỉ viết sau khi có code — theo [note 33 §4](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md),
không đặc tả trước rồi bắt code chạy theo.

## 1. Hiện trạng trong repo (07/11/2026)

| Hạng mục | Trạng thái thật |
|---|---|
| **SCRUM-137 Tìm giờ họp** | Jira `To Do`, giao Khánh. Comment `10449` chốt phạm vi **chỉ bận/rảnh**, không lộ tên cuộc họp, người dự hay địa điểm của lịch người khác. Chưa có UC, BR, code, sequence. |
| **Chia sẻ lịch / uỷ quyền xem lịch** | **Chưa có gì**: không ticket, không UC/BR, không bảng dữ liệu, không endpoint. BR-105 chỉ nói bỏ uỷ quyền *phản hồi lời mời* — khác hẳn việc này. |
| **Dữ liệu bận/rảnh** | Không endpoint nào. `GET /api/meetings` chỉ trả cuộc họp người gọi được xem và đã lọc quyền, nên không dùng lại được để xem lịch người khác. |
| **Màn hình** | Report 3 §I.5.1 Screen List dừng ở **15.1**; không có màn "Tìm giờ họp" hay "Chia sẻ lịch". Report 3 §III cũng chưa có mục cho các màn này. |
| **Quyền** | `Capability` chỉ có `MeetingCreator`, `MeetingApprover`, `MinutesEditor`, `AdminUsers`. Không có quyền nào về xem lịch người khác. |
| **Trợ lý AI chat** | ADR-0043 ghi TrustCabinet **không có** "Trợ lý AI, Tìm giờ họp". PO chốt 07/11: **không làm trợ lý AI** (D4); Tìm giờ họp thì làm, nên ADR-0043 chỉ còn lệch đúng nửa câu. |
| **Biên bản** | Đã đặc tả UC-58–UC-60, BR-383–BR-396, `MSG395`–`MSG406`; PR #271 đang mở thêm UC-61/64/65/82/83. eCab **không có** duyệt, ban hành hay ký biên bản, nên phần này không lấy từ eCab (§3.4). |

## 2. Số hiệu: chờ #271 trước khi đặt

| Loại | Đang thế nào | Đặc tả này lấy gì |
|---|---|---|
| **MSG** | `main` báo mã tiếp theo `MSG414`. #271 xin `MSG406`–`MSG425`, **trùng** `MSG406`–`MSG413` đã dùng trên `main` (cấu hình AI, đổi người nói một câu, định dạng tải biên bản) → #271 phải dời thành `MSG414`–`MSG433`. | Theo ghi chú 07/11: chừa **`MSG434`–`MSG439`** cho đặc tả này, mã tiếp theo **`MSG440`**. Bốn UC có thể cần hơn 6 mã — xin thêm từ `MSG440` khi thiếu. Đọc lại danh mục lúc thêm, đừng tin trí nhớ. |
| **UC** | Dừng ở **UC-81**; #271 xin UC-82 (ghi chú thư ký) và UC-83 (đính chính biên bản) | Từ **UC-84** trở đi. |
| **BR** | Dừng ở **BR-396**; #271 đề xuất BR-395–BR-413 nhưng 395/396 đã dùng, nên #271 cũng phải đánh lại | Số tiếp theo **sau khi** #271 chốt lại. |
| **Màn (§I.5.1, §III)** | Screen List đánh số theo FE, dừng ở 15.1 | FE mới, số đề xuất **FE-16** (§5), đặt số màn sau khi chốt danh sách màn. |

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

Ba điểm phải định nghĩa lại cho ta, không tê nguyên từ eCab:

1. **Vai trò.** Ta **có** vai trò hệ thống `Thành viên` (built-in) nhưng **không có** vai trò tên
   *Điều phối họp*: việc tạo/tìm cuộc họp của ta là capability `MeetingCreator`, seed đang cấp cho
   `Chủ toạ` và `Thư ký`. Nên phát biểu bằng capability, đừng bám tên vai trò — xem §3.5.
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

**Không làm.** PO chốt 07/11/2026 (D4). Ghi ở đây để lúc họp phạm vi không phải quét lại; ADR-0043 giữ
nguyên câu "không có trợ lý AI".

### 3.4 Biên bản — chỉ để đối chiếu

eCab **không có** luồng duyệt, ban hành hay ký: `MinutesIssued` và quyền `minutes.sign` chỉ khai báo sẵn,
chưa có code. Phần eCab chạy thật: kiểm tra trước khi viết (bản ghi chưa xong, giọng chưa đặt tên), dự thảo AI,
chọn mẫu rồi ghép dự thảo vào mẫu, Chủ trì hoặc Thư ký sửa, xuất PDF/Word, cuộc họp Mật không xuất.
Ta đã có tương đương (`MSG405` chặn xuất biên bản Mật), và luồng duyệt–ban hành là của ta (PR #271).
**Không lấy gì thêm từ eCab cho biên bản.**

### 3.5 Ai xin được lịch của ai

| Điều kiện của eCab | Ta làm thế nào |
|---|---|
| Người **xin** phải giữ vai trò hệ thống *Điều phối họp* | Người có capability **`MeetingCreator`** (seed: Chủ toạ, Thư ký; hoặc vai trò tự tạo được cấp quyền này). Thành viên thường và Quản trị viên không xin được nếu không có quyền đó. |
| **Chủ lịch** phải giữ vai trò *Thành viên* | Đề xuất **mọi tài khoản nội bộ đang hoạt động**, không chỉ vai trò `Thành viên`: một tổ chức phẳng (ADR-0004), và Chủ toạ/Thư ký cũng cần người khác xem được giờ của mình. Chờ xác nhận — nếu giữ đúng eCab thì chỉ vai trò `Thành viên` làm chủ lịch. |
| Không tự xin cho chính mình | Giữ. |
| Khách không có lịch | Giữ: Guest không có tài khoản, không nằm trong danh sách chọn người. |
| Mỗi cặp người chỉ một chia sẻ đang chờ **hoặc** đang hiệu lực | Giữ, kể cả bản ghi cũ đã kết thúc thì được tạo lại. |
| Người xin mất vai trò, hoặc một trong hai tài khoản bị khoá → **tạm ngưng** | Giữ, nhưng phát biểu là "người xin mất `MeetingCreator` hoặc tài khoản không còn hoạt động", không phải "mất vai trò". |
| Chủ lịch tự chia sẻ → chỉ chọn người giữ vai trò Điều phối họp | Chọn người có `MeetingCreator`; luôn mức thấp nhất; người nhận bấm nhận hoặc từ chối. |

## 4. Quyết định — PO chốt 07/11/2026

| # | Quyết định | Hệ quả cho đặc tả |
|---|---|---|
| **D1** | **Có làm chia sẻ lịch** (uỷ quyền xem bận), không dùng phương án "mọi người xem chung". | Có bảng dữ liệu mới (ERD + migration khi BE làm), 2 màn về chia sẻ, ~5 mã MSG, audit log + thông báo hai chiều. |
| **D2** | **SCRUM-137 gồm cả hai tab**: *Tìm khung giờ* và *Lịch được chia sẻ*. | Tab thứ hai có màn riêng, dùng lại dữ liệu chia sẻ của D1. |
| **D3** | **Màn riêng**, không nhét vào FE-04 (lịch/cuộc họp). | Đề xuất FE mới **FE-16** cho "Lịch làm việc & tìm giờ họp"; số màn mới trong §I.5.1 và §III. |
| **D4** | **Không làm trợ lý AI.** | Không UC/BR/MSG cho chat; chỉ sửa ADR-0043 ở nửa câu "Tìm giờ họp". |
| **D5** | **Chỉ xin lịch của người trong hệ thống** — khách không có lịch. | Chi tiết ở §3.5; còn một dòng chờ xác nhận (chủ lịch có bị giới hạn ở vai trò `Thành viên` không). |
| **D6** | **Có thêm capability mới** cho chia sẻ lịch. | Sửa `Capability`, `ReferenceData` seed, ma trận quyền, `PermissionCatalog` và test — cùng commit với code. |

Còn chờ: số UC/BR/MSG thật (sau #271), tên FE mới, các con số giới hạn (§7 Q1, Q2).

## 5. Khung đặc tả đề xuất

- **UC (đề xuất, số thật sau #271):**
  - UC-84 *Yêu cầu xem lịch* — Thư ký/người có `MeetingCreator` gửi yêu cầu, chủ lịch chấp nhận (có thể mức thấp hơn) hoặc từ chối.
  - UC-85 *Chủ lịch tự chia sẻ* — chọn người có `MeetingCreator`, luôn mức thấp nhất; bên nhận đồng ý.
  - UC-86 *Quản lý chia sẻ đã có* — nâng mức/gia hạn, hạ mức/đổi khoảng ngày, rút, ngừng chia sẻ, thôi xem lịch.
  - UC-87 *Tìm khung giờ* — nhập tham số, server trả khung, tạo nháp.
  - UC-88 *Xem lịch được chia sẻ* — lịch tuần nhiều màu.
  Gộp UC-84/85/86 thành một nếu khi viết thấy quá vụn.
- **BR (đề xuất):** khoảng 12–15 quy tắc — mức xem theo `Sensitivity`, ngày ngoài khoảng là chưa biết, một chia sẻ
  mỗi cặp, tạm ngưng khi mất quyền/khoá tài khoản, hết hạn tự đóng, không lộ tên cuộc họp, chỉ báo bận/rảnh,
  giới hạn 10 người / 31 ngày / đệm 0–4 giờ / bước nhảy 5 phút–2 giờ, tạo nháp điền sẵn và kiểm lại trước khi lưu.
- **MSG:** `MSG434`–`MSG439` (6 mã) — đề xuất: gửi yêu cầu, chấp nhận/từ chối, rút/ngừng, tìm xong, không có
  khung nào, lỗi. Năm UC có thể cần hơn 6 mã; xin thêm từ `MSG440`.
- **Màn (đề xuất, FE-16):** 3 màn — *Chia sẻ lịch* (danh sách người tôi chia sẻ / người chia sẻ cho tôi, kèm
  dialog xin chia sẻ và dialog nâng mức), *Tìm giờ họp* (tab Tìm khung giờ), *Lịch được chia sẻ* (tab thứ hai).
  Ảnh chụp sau khi FE có màn, cách làm theo [note 06](06-plan-dac-ta-man-hinh.md).
- **Sequence:** 2–4 file `.txt` (yêu cầu chia sẻ, tìm khung giờ, xem lịch), vẽ draw.io theo
  [drawio-skill](../../../.agents/skills/drawio-skill/SKILL.md) như các đợt trước.
- **ERD:** bảng chia sẻ lịch (tên và cột do BE đặt khi làm, ghi vào
  [02-erd](../../02-design/02-erd.md) cùng migration — không chốt trong note này).

## 6. Thứ tự làm

1. ~~Chốt D1–D6 với PO~~ — xong 07/11/2026 (§4).
2. Tạo ticket Jira cho phần **chia sẻ lịch** (hiện chỉ có SCRUM-137) và ticket **màn FE-16**; ghi rõ SCRUM-137
   phụ thuộc ticket chia sẻ.
3. Chờ **PR #271 merge**, rồi chốt lại dải `MSG`, số UC, số BR thật (§2).
4. BE viết UC Specification cơ bản + sequence cho phần đã chốt ([DEVELOPMENT_WORKFLOW §2.1](../../DEVELOPMENT_WORKFLOW.md#21-changing-a-requirement-during-implementation)),
   ghi mọi chỗ lệch ticket vào Jira.
5. BA Final Review mới đưa vào Report 3: §II (UC), §V.1 (BR), §V.2 (MSG), §III + §I.5.1 (màn), rồi
   `python scripts/sync_reports.py`.
6. Sửa ADR-0043 (bỏ "Tìm giờ họp" khỏi câu không-có), viết ADR mới cho chia sẻ lịch nếu BE đụng dữ liệu cá
   nhân người khác; cập nhật `frontend/src/pages/scopeData.ts`.
7. Chụp ảnh và viết đặc tả màn theo note 06, chỉ khi FE đã có màn chạy thật.

## 7. Câu hỏi cho PO

1. Mức xem: giữ ba mức như eCab, hay hai mức (bỏ mức *Tất cả* vì ta có mức `Secret`)?
2. Giữ nguyên các con số của eCab (10 người, 31 ngày, 8 giờ, 0–4 giờ đệm, 5 phút–2 giờ bước nhảy) hay tự đặt?
3. Chủ lịch có bị giới hạn ở vai trò `Thành viên` như eCab, hay mọi tài khoản nội bộ đều được chia sẻ lịch?
4. Người dự "hạn chế" của eCab — ta có loại tương đương không, hay chỉ `Chair`/`Secretary`/`Member`/`Guest`?

## 8. Liên quan

- [note 33 §4](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md) — SCRUM-137 được tạo thế nào và phạm vi "chỉ bận/rảnh".
- [note 06](06-plan-dac-ta-man-hinh.md) — quy trình chụp và đặc tả màn hình Report 3.
- **PR #271** (`docs/dac-ta-so-bo-scrum-30-33`, đang mở) — đặc tả sơ bộ SCRUM-30/33, file `docs/notes/NOTE-dac-ta-so-bo-scrum-30-33.md`
  chỉ có trên nhánh đó; là mốc phải chờ để đánh số UC/BR/MSG.
- [Danh mục mã thông báo](../../03-api/00-danh-muc-ma-thong-bao.md) — đọc lại "Mã tiếp theo" trước khi thêm.
- [ADR-0043](../../05-decisions/0043-thanh-phan-theo-ai-ecabinet-giu-sidebar.md) — câu "TrustCabinet không có Tìm giờ họp" đã lệch.
