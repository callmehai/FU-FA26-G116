# Chuẩn bị đặc tả — chia sẻ lịch làm việc và tìm giờ họp (eCab US #352 / #353)

**Ngày:** 07/11/2026 · **Nguồn nghiệp vụ:** quét code AI-eCabinet `bba2097f` (30/10/2026) — đọc code, chỉ ghi
nghiệp vụ. US #352 = chia sẻ lịch + tìm giờ; US #353 = trợ lý AI.
**Trạng thái:** phạm vi chốt 07/11/2026 (§4 D1–D6), PO (Hải) trả lời Q1–Q4 và FE-16 cùng ngày (§4 D7–D11);
**chưa phải đặc tả**. Ticket: SCRUM-137 (tìm giờ họp), **SCRUM-142** (chia sẻ lịch).

Note này trả lời ba câu: viết đặc tả **cái gì**, viết **ở đâu**, và **còn phải chốt gì trước**.
Đặc tả thật chỉ viết sau khi có code — theo [note 33 §4](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md),
không đặc tả trước rồi bắt code chạy theo.

## 1. Hiện trạng trong repo (07/11/2026)

| Hạng mục | Trạng thái thật |
|---|---|
| **SCRUM-137 Tìm giờ họp** | Jira `To Do`, giao Khánh. Comment `10449` chốt phạm vi **chỉ bận/rảnh**, không lộ tên cuộc họp, người dự hay địa điểm của lịch người khác. Chưa có UC, BR, code, sequence. |
| **Chia sẻ lịch / uỷ quyền xem lịch** | Có ticket **SCRUM-142** (`To Do`, giao Huy, nhãn `moscow-should`). Chưa có UC/BR, bảng dữ liệu, endpoint. BR-105 chỉ nói bỏ uỷ quyền *phản hồi lời mời* — khác hẳn việc này. |
| **Dữ liệu bận/rảnh** | Không endpoint nào. `GET /api/meetings` chỉ trả cuộc họp người gọi được xem và đã lọc quyền, nên không dùng lại được để xem lịch người khác. |
| **Màn hình** | Report 3 §I.5.1 Screen List dừng ở **15.1**; không có màn "Tìm giờ họp" hay "Chia sẻ lịch". Report 3 §III cũng chưa có mục cho các màn này. |
| **Mức mật** | Từ 07/11/2026 chỉ còn **3 mức** `Normal` / `Internal` / `Secret` (Thường / Nội bộ / Mật) — bỏ Hạn chế, họp đang Hạn chế chuyển thành Mật qua migration `SensitivityThreeLevels` ([ADR-0057](../../05-decisions/0057-ba-muc-mat-nhu-ai-ecabinet.md)). Phần BR/Report 3 về mức mật do người làm PR đó gửi note riêng. |
| **Quyền** | `Capability` chỉ có `MeetingCreator`, `MeetingApprover`, `MinutesEditor`, `AdminUsers`. Không có quyền nào về xem lịch người khác. |
| **Trợ lý AI chat** | ADR-0043 ghi TrustCabinet **không có** "Trợ lý AI, Tìm giờ họp". PO chốt 07/11: **không làm trợ lý AI** (D4); Tìm giờ họp thì làm, nên ADR-0043 chỉ còn lệch đúng nửa câu. |
| **Biên bản** | Đã đặc tả UC-58–UC-60, BR-383–BR-396, `MSG395`–`MSG406`; PR #271 (đã merge 07/11/2026) thêm UC-61/64/65/82/83. eCab **không có** duyệt, ban hành hay ký biên bản, nên phần này không lấy từ eCab (§3.4). |

## 2. Số hiệu (đã chốt 07/11/2026)

Số đã chốt nằm trong [đặc tả sơ bộ](../NOTE-dac-ta-so-bo-scrum-137-142.md); bảng dưới ghi lại các dải số đã có chủ
trước đó, để thấy vì sao đặc tả này bắt đầu từ các số đó.

| Loại | Đang thế nào | Đặc tả này lấy gì |
|---|---|---|
| **MSG** | `MSG406`–`MSG414` đã dùng trên `main`; `MSG415`–`MSG423` của ghi chú thư ký (SCRUM-30, UC-82) đã vào `main`; `MSG424`–`MSG434` thuộc PR #279 (SCRUM-33, UC-83). | **`MSG435`–`MSG455`**, mã tiếp theo **`MSG456`**. Đọc lại danh mục lúc thêm, đừng tin trí nhớ. |
| **UC** | Dừng ở **UC-81**; UC-82 (ghi chú thư ký, SCRUM-30) và UC-83 (PR #279, SCRUM-33) đã có chủ | **UC-84**, **UC-85** (UC gom); UC-86 → UC-90 không dùng. |
| **BR** | BR-397 → BR-413 thuộc SCRUM-30 và SCRUM-33 (PR #279 giữ BR-404 → BR-413) | **BR-414 → BR-431**. |
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
2. **Mức xem.** Ta cũng chỉ còn 3 mức họp (`Normal`, `Internal`, `Secret` — ADR-0057), nhưng PO chốt **không có
   mốc *Tất cả***: chỉ **2 mốc chia sẻ**, họp Mật không bao giờ chia sẻ, kể cả chỉ hiện bận (D7).
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
| Người **xin** phải giữ vai trò hệ thống *Điều phối họp* | Người có capability **`MeetingCreator`** (seed: Chủ toạ, Thư ký; hoặc vai trò tự tạo được cấp quyền này). Thành viên thường và Quản trị viên không xin được nếu không có quyền đó. **Không dùng tên vai trò để gác**: `MeetingOrganizer` trong Report 3 là *người tạo cuộc họp* theo từng cuộc họp, không phải một dòng trong bảng `roles` (bảng chỉ có `Chủ toạ`, `Thư ký`, `Thành viên`, `Quản trị viên`); gác bằng vai trò sẽ phải thêm vai trò thứ năm và kiểm trùng với capability. |
| **Chủ lịch** phải giữ vai trò *Thành viên* | **Mọi tài khoản nội bộ đang hoạt động**, trừ chính người xin — chốt 07/11/2026, không giới hạn ở vai trò `Thành viên` như eCab (một tổ chức phẳng, Chủ toạ/Thư ký cũng cần người khác xem được giờ của mình). |
| Không tự xin cho chính mình | Giữ: người xin không nằm trong danh sách chọn chủ lịch. |
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
| **D5** | **Chỉ xin lịch của người trong hệ thống**: người xin phải có `MeetingCreator`; chủ lịch là **mọi tài khoản nội bộ đang hoạt động, trừ chính người xin**; khách không có lịch. | Chi tiết ở §3.5. Không tạo thêm vai trò `MeetingOrganizer` chỉ để gác — nó là actor theo cuộc họp, không phải dòng trong `roles`. |
| **D6** | **Không thêm capability mới** — PO xác nhận ở Q4: dùng chung `MeetingCreator` cho cả yêu cầu chia sẻ lẫn tab tìm giờ. | Không sửa `Capability`/seed/ma trận quyền/`PermissionCatalog`. |
| **D7** | **Mức xem: 2 mốc chia sẻ** (Q1, PO sửa lại 07/11 sau khi bỏ mức Hạn chế). **Mốc 1** = họp Thường; **Mốc 2** = Thường + Nội bộ. Họp **Mật không bao giờ chia sẻ**, kể cả chỉ hiện bận. | Enum mức chia sẻ có 2 giá trị; "mức thấp nhất" khi chủ lịch tự chia sẻ = Mốc 1; "nâng mức" chỉ còn Mốc 1 → Mốc 2. Hệ quả cần ghi trong BR: giờ của một cuộc họp Mật hiện là **rảnh** với người xem lịch, nên Tìm khung giờ có thể đề xuất trùng — xem §7. |
| **D8** | **Giữ nguyên số của eCab** (Q2): tối đa 10 người; thời lượng 15 phút–8 giờ, mặc định 2 giờ; khoảng ngày tối đa 31 ngày; T2–T6 08:00–17:00, nghỉ 12:00–13:00; đệm 0–4 giờ, mặc định 30 phút; bước nhảy 5 phút–2 giờ, mặc định 30 phút. | Thành BR giới hạn + validation `400` ở BE; mặc định ở FE. |
| **D9** | **Chỉ Chủ toạ / Thư ký / Thành viên tính bận** (Q3); không map Lãnh đạo / Tham dự hạn chế của eCab. | Người tạo, người duyệt, Khách không tính bận. |
| **D10** | **Lập FE-16 "Lịch làm việc & tìm giờ họp"**, gom màn lịch hiện có (`CalendarPage`) về FE này. | Report 3 §I.5.1 + §III: chuyển màn lịch hiện có sang FE-16, thêm màn mới; sửa chỗ trỏ màn lịch cũ. |
| **D11** | **FE-16 ưu tiên Should-have.** | Khớp nhãn `moscow-should` của SCRUM-142; ghi MoSCoW trong Report 3 và `scopeData.ts`. |
| **D12** | **Khung trùng họp Mật vẫn cho tạo nháp** (PO, 07/11/2026 — đóng Q5 ở §7). Bước kiểm lại khi Tạo nháp dùng đúng dữ liệu đã chia sẻ như lúc tìm (không tính Secret); không mã lỗi, cảnh báo, thông báo hay audit nào khác đi vì có họp Mật. Màn tìm giờ luôn hiện một câu cố định (cho mọi khung) rằng chỉ tính lịch được chia sẻ, người được mời vẫn có thể bận. Người được mời từ chối qua UC-29 như lời mời thường. | BR-428/BR-429; UC-85b + EX-1; câu cố định trên màn tìm giờ (UC-85a). Lý do: từ chối tạo (dù báo chung) cũng cho phép dò lịch Mật bằng cách thử từng khung. Tạo nháp: người đầu tiên làm Chair, còn lại Member, người tìm làm Secretary; cuộc họp ở Draft, đi luồng duyệt thường; tạo cuộc họp hiện KHÔNG kiểm trùng lịch người dự — giữ nguyên. |

Còn chờ: ticket màn FE-16; số UC/BR/MSG đã chốt trong [đặc tả sơ bộ](../NOTE-dac-ta-so-bo-scrum-137-142.md)
(UC-84 gom UC-84a → UC-84e, UC-85 gom UC-85a → UC-85c; số UC-86..UC-90 trả lại, không dùng), BR-414 → BR-431, `MSG435`–`MSG455`, mã tiếp theo `MSG456`; dựa trên PR #279 merge trước) — BA giữ hoặc đổi số khi vào Report 3.

## 5. Khung đặc tả đề xuất

Số hiệu đã chốt trong [đặc tả sơ bộ](../NOTE-dac-ta-so-bo-scrum-137-142.md) (07/11/2026, tách UC Respond + tách mã
07/11/2026, đổi số tránh PR #279 + tách UC tạo nháp + gộp UC theo mẫu Report 3 07/11/2026) — các gợi ý tạm dưới đây
chỉ còn giá trị lịch sử, dùng số trong note đặc tả (UC-84/UC-85 gom, BR-414 → BR-431, `MSG435`–`MSG455`,
mã tiếp theo `MSG456`, dựa trên PR #279 merge trước):

- **UC (đã chốt):**
  - UC-84 *Quản lý chia sẻ lịch* (UC gom) — UC-84a *Yêu cầu xem lịch*, UC-84b *Chủ lịch tự chia sẻ*,
    UC-84c *Trả lời chia sẻ*, UC-84d *Thay đổi chia sẻ*, UC-84e *Rút/ngừng chia sẻ*.
  - UC-85 *Tìm giờ họp* (UC gom) — UC-85a *Tìm khung giờ*, UC-85b *Tạo nháp từ khung giờ*, UC-85c *Xem lịch được chia sẻ*.
- **BR (đề xuất):** khoảng 12–15 quy tắc — mức xem theo `Sensitivity`, ngày ngoài khoảng là chưa biết, một chia sẻ
  mỗi cặp, tạm ngưng khi mất quyền/khoá tài khoản, hết hạn tự đóng, không lộ tên cuộc họp, chỉ báo bận/rảnh,
  giới hạn 10 người / 31 ngày / đệm 0–4 giờ / bước nhảy 5 phút–2 giờ, tạo nháp điền sẵn và kiểm lại trước khi lưu.
- **MSG:** gợi ý ban đầu 6 mã (gửi yêu cầu, chấp nhận/từ chối, rút/ngừng, tìm xong, không có khung nào, lỗi); đặc
  tả chốt 21 mã `MSG435`–`MSG455`, một mã một tình huống.
- **Màn (đề xuất, FE-16):** 3 màn — *Chia sẻ lịch* (danh sách người tôi chia sẻ / người chia sẻ cho tôi, kèm
  dialog xin chia sẻ và dialog nâng mức), *Tìm giờ họp* (tab Tìm khung giờ), *Lịch được chia sẻ* (tab thứ hai).
  Ảnh chụp sau khi FE có màn, cách làm theo [note 06](06-plan-dac-ta-man-hinh.md).
- **Sequence:** 2–4 file `.txt` (yêu cầu chia sẻ, tìm khung giờ, xem lịch), vẽ draw.io theo
  [drawio-skill](../../../.agents/skills/drawio-skill/SKILL.md) như các đợt trước.
- **ERD:** bảng chia sẻ lịch (tên và cột do BE đặt khi làm, ghi vào
  [02-erd](../../02-design/02-erd.md) cùng migration — không chốt trong note này).

## 6. Thứ tự làm

1. ~~Chốt D1–D6 với PO~~ — xong 07/11/2026 (§4).
2. ~~Tạo ticket chia sẻ lịch~~ — **SCRUM-142** (07/11/2026), đã ghi SCRUM-137 phụ thuộc nó. Còn: ticket **màn FE-16**;
   cập nhật mục "PO tick" của SCRUM-142 theo D6–D11 (Q1 là đáp án **C — 2 mốc**, không phải A).
3. ~~Chờ **PR #271 merge**, rồi chốt lại dải `MSG`, số UC, số BR thật (§2)~~ — xong 07/11/2026, tách UC Respond +
   tách UC tạo nháp + tách mã + đổi số tránh PR #279 + gộp UC theo mẫu Report 3, 07/11/2026:
   UC-84 gom (UC-84a → UC-84e), UC-85 gom (UC-85a → UC-85c), BR-414 → BR-431, `MSG435`–`MSG455` trong [đặc tả sơ bộ](../NOTE-dac-ta-so-bo-scrum-137-142.md)
   (BA giữ hoặc đổi số khi vào Report 3; số dựa trên PR #279 merge trước — nếu nhánh này merge trước thì #279 dời).
4. ~~BE viết UC Specification cơ bản + sequence cho phần đã chốt~~ — xong phần sơ bộ 07/11/2026, cập nhật 07/11/2026
   ([note đặc tả](../NOTE-dac-ta-so-bo-scrum-137-142.md) + 8 sequence UC-84a → UC-84e, UC-85a → UC-85c);
   ghi mọi chỗ lệch ticket vào Jira (không sửa ticket trong task này).
5. BA Final Review mới đưa vào Report 3: §II (UC), §V.1 (BR), §V.2 (MSG), §III + §I.5.1 (màn), rồi
   `python scripts/sync_reports.py`.
6. Sửa ADR-0043 (bỏ "Tìm giờ họp" khỏi câu không-có), viết ADR mới cho chia sẻ lịch nếu BE đụng dữ liệu cá
   nhân người khác; cập nhật `frontend/src/pages/scopeData.ts`.
7. Chụp ảnh và viết đặc tả màn theo note 06, chỉ khi FE đã có màn chạy thật.

## 7. Câu hỏi cho PO

Q1–Q4 và FE-16 đã trả lời 07/11/2026 → D6–D11 ở §4. Q1 chốt hai lần: lần đầu 3 mốc (Thường / +Nội bộ / +Hạn chế),
sau khi bỏ mức Hạn chế (ADR-0057) còn **2 mốc** — dùng bản sau.

Q5 đã đóng bằng D12 ở §4 (07/11/2026): khung trùng họp Mật vẫn cho tạo nháp, kiểm lại theo đúng dữ liệu đã chia
sẻ, không tín hiệu nào khác đi vì có họp Mật; chi tiết ở [đặc tả sơ bộ](../NOTE-dac-ta-so-bo-scrum-137-142.md)
(BR-428/BR-429, UC-85b).

## 8. Liên quan

- [note 33 §4](33-pr260-scrum-36-32-du-thao-bien-ban-va-scrum-137-tim-gio-hop.md) — SCRUM-137 được tạo thế nào và phạm vi "chỉ bận/rảnh".
- [note 06](06-plan-dac-ta-man-hinh.md) — quy trình chụp và đặc tả màn hình Report 3.
- **PR #271** (`docs/dac-ta-so-bo-scrum-30-33`, đã merge 07/11/2026) — đặc tả sơ bộ SCRUM-30/33, file `docs/notes/NOTE-dac-ta-so-bo-scrum-30-33.md`
  chỉ có trên nhánh đó; là mốc phải chờ để đánh số UC/BR/MSG.
- [Danh mục mã thông báo](../../03-api/00-danh-muc-ma-thong-bao.md) — đọc lại "Mã tiếp theo" trước khi thêm.
- [ADR-0043](../../05-decisions/0043-thanh-phan-theo-ai-ecabinet-giu-sidebar.md) — câu "TrustCabinet không có Tìm giờ họp" đã lệch.
