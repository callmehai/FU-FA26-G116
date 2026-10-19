# Trao đổi với PO — 16/10/2026

> **Người ghi:** KhanhPG · **Ngày trao đổi:** 16/10/2026 (mốc tin nhắn GOAT BA 16/10/2026 21:25) · **Ghi lại:** 18/10/2026
> **Trạng thái 18/10/2026: ĐÃ ÁP DỤNG MỘT PHẦN vào Report 3 local** (UC list, đặc tả UC, BR, System Messages; chưa
> commit, chưa lên Drive). Đã tạo ticket Jira ngày 18/10/2026 (xem cuối bảng); chưa có ADR, sơ đồ hay code. Số UC đề xuất ở các mục bên dưới là bản nháp; số
> chốt nằm ở bảng "Kết quả áp dụng".

## Kết quả áp dụng (18/10/2026)

| Ý | Kết quả trong Report 3 | Còn lại |
|---|---|---|
| 1. Secretary tạo vote | Đã sửa UC-52a, UC-52b, UC-53; BR-189, BR-252, BR-253, BR-274; MSG213 | Code FE-11 |
| 2. Chair giao chủ toạ | Thêm **UC-73 Hand Over Chair to Another Participant** (một thao tác); sửa UC-28b; BR-126, BR-169, BR-294 → BR-296; MSG256 → MSG260 | Code FE-06 |
| 3. Agenda đơn giản | Sửa UC-23a, UC-23b, UC-23c (chỉ dẫn chiếu BR), UC-23d, UC-24, UC-25, UC-26; viết lại BR-66/67/68/70/72/238; BR-236, BR-240, BR-241 ngừng dùng; MSG141, MSG144, MSG250 ngừng dùng; MSG143 đổi câu chữ | Màn hình §III 5.2/5.3; migration + code SCRUM-25 |
| 4. Edit & Submit Minutes | Chưa sửa: FE-12 còn placeholder | [Note UC chưa đặc tả](../notes/NOTE-uc-chua-dac-ta-cho-sua.md) |
| 5. Ghi nhận lời nói | Không có UC (hệ thống tự làm); chưa sửa vì FE-08/FE-10 còn placeholder | Note UC chưa đặc tả; ADR |
| 6. Giơ tay | Chưa sửa: FE-08 còn placeholder | Note UC chưa đặc tả |
| 7. Vote gắn tài liệu | Thêm **UC-74 Confirm Document Read** và **UC-76 Manage Vote Documents** (76a gắn, 76b gỡ; 3 nguồn tài liệu); sửa UC-32c, UC-52b, UC-53, UC-55, UC-33b, UC-33d; BR-287 → BR-293, BR-304, BR-305; MSG252 → MSG255, MSG265 → MSG270 | Entity, code FE-07/FE-11; ADR |

**Ticket (Sprint 2):** ý 1 + 7 → SCRUM-96 · ý 2 → SCRUM-97 · ý 3 → SCRUM-98 · ADR, Report 3 phần còn sót → SCRUM-100 ·
đặc tả FE-08 / FE-09 / FE-10 / FE-12 (ý 4, 5, 6) → SCRUM-101 / SCRUM-102 / SCRUM-103 / SCRUM-104 (subtask của SCRUM-53).

Thay đổi đảo ngược quyết định đã chốt cần ADR mới trong
[05-decisions/](../05-decisions/README.md).

Liên quan: [Góp ý hội đồng lần 1](../notes/NOTE-gop-y-hoi-dong-lan-1.md) — mục 1 và 3 của note đó (xác định người nói)
trùng với ý 5 bên dưới.

## Ý PO đã nêu

1. Cả **Chair và Secretary** được tạo biểu quyết.
2. Chair **giao vai trò chủ toạ cho người khác** rồi rời cuộc họp; **không cho Thư ký thay Chair**.
3. Agenda: **bỏ thời gian**, **không chặn thứ tự**, **bớt BR**.
4. *Review & Submit Minutes for Signing* đổi thành **Edit & Submit Meeting Minutes**.
5. Rà UC cho việc **người nói bật mic thì hệ thống ghi nhận lời nói**.
6. **Giơ tay phát biểu.**
7. **Biểu quyết gắn với tài liệu**; người bỏ phiếu phải **đọc xong tài liệu** mới được bỏ phiếu.

---

## Đối chiếu với tài liệu hiện tại và đề xuất sửa

### 1. Chair và Secretary cùng tạo biểu quyết — FE-11

| Chỗ bị ảnh hưởng | Hiện tại | Đề xuất |
|---|---|---|
| UC-53 *Create / Configure Vote* | Actor: Chair | Actor: **Chair, Secretary** |
| BR-252 | Chỉ Chair hiện hành tạo/cấu hình nháp | Chair **hoặc Secretary** hiện hành |
| BR-253 | Chỉ Chair thấy nháp | Chair và Secretary thấy nháp |
| BR-189 | Chỉ Chair xoá nháp | Chair và Secretary |
| BR-274 | Chair được vào phần chuẩn bị nháp khi `Approved` | Mở rộng cho Secretary (Secretary vốn đã có quyền theo cuộc họp — kiểm lại) |
| UC-52 | "nháp chỉ Chair hiện hành thấy" | sửa mô tả theo BR-253 mới |

**Cần hỏi lại PO:** Secretary chỉ **tạo/sửa nháp**, hay được cả **mở – đóng – ghi kết luận** (UC-54, UC-56, UC-57;
BR-160, BR-270, BR-161)? Mặc định đề xuất: chỉ tạo/sửa/xoá nháp; mở, đóng và kết luận vẫn của Chair.
Code FE-11 nếu đã làm phải sửa phần kiểm quyền và test.

### 2. Chair giao quyền chủ toạ, Thư ký không được thay Chair — FE-06

| Chỗ bị ảnh hưởng | Hiện tại | Đề xuất |
|---|---|---|
| BR-126 | "The Secretary may replace the Chair…" khi `Published`/`InProgress` | **Bỏ quyền này của Secretary.** Chỉ Chair hiện hành tự chuyển vai trò Chair cho người khác |
| BR-169 | Thay Chair = Chair cũ thành Member, người mới thành Chair, cùng lúc | Giữ cơ chế đổi đồng thời; người thực hiện là **Chair hiện hành** |
| UC-28b *Quản lý vai trò & quyền bổ sung* | Secretary/Organizer đổi vai trò, gồm thay Chair | Tách thao tác thay Chair ra khỏi UC-28b |
| **UC mới — UC-73 Chuyển giao chủ toạ** *(Transfer Chair Role)* | — | Actor: Chair. `<<extend>>` UC-27. Chọn một người nội bộ đang tham dự (không phải Guest, không phải Secretary hiện hành); sau khi chuyển, Chair cũ thành Member và có thể rời phòng (UC-38) |

**Cần hỏi lại PO:** (a) Chair cũ sau khi giao có bị loại khỏi cuộc họp hay vẫn là Member? (b) Chỉ chuyển giao được khi
`InProgress`, hay cả `Published`? (c) Trước `Published`, Organizer/Secretary còn được gán Chair (UC-28a/b) không —
đề xuất: có, quy tắc "không thay Chair" chỉ áp dụng từ `Published`. (d) Có cần nhánh khi Chair mất kết nối mà chưa kịp
giao không. Kiểm lại endpoint SCRUM-20 đang cho Secretary thay Chair.

### 3. Agenda bỏ thời gian, không chặn thứ tự, bớt BR — FE-05

Agenda đã có code (`AgendaItem.PlannedDuration`, `AgendaRules`, `AgendaService`, `AgendaEndpoints`, seed) nên đây là
thay đổi **code + migration mới**, không chỉ docs.

| BR | Hiện tại | Đề xuất |
|---|---|---|
| BR-66 | Title + **Planned Duration > 0** bắt buộc | Chỉ Title bắt buộc; bỏ Planned Duration |
| BR-70 | **Chỉ mục Pending đầu tiên** được bắt đầu | **Bỏ** — Chair chọn bất kỳ mục Pending nào |
| BR-72 | Ghi giờ bắt đầu/kết thúc thực tế, thời lượng, so với Planned Duration | Bỏ phần thời lượng; cân nhắc giữ giờ thực tế nếu biên bản cần, còn không thì bỏ |
| BR-67, BR-238, BR-240 | Chỉ sắp xếp mục Pending, chỉ đổi chỗ trong các mục Pending | Gộp thành một BR: sắp xếp tự do khi Meeting chưa `Ended` (hỏi PO có cho đổi chỗ mục đã Done không) |
| BR-68, BR-236, BR-237, BR-239, BR-241 | 5 BR tách từ BR-68 cũ về trường được sửa theo trạng thái | Gộp lại 1–2 BR |
| BR-71 | Tối đa một mục `InProgress` | Giữ — vẫn cần biết mục đang bàn |

UC giữ nguyên số (UC-23a–d, UC-24, UC-25, UC-26), chỉ sửa đặc tả. BR bỏ thì ghi "không còn dùng", **không tái cấp mã**.
Cần sửa thêm [SM-02 Agenda](../10-statemachine/README.md) nếu sơ đồ có điều kiện thứ tự, và sequence diagram agenda.

### 4. Edit & Submit Meeting Minutes — FE-12

Danh mục hiện tách **UC-60 Chỉnh biên bản** và **UC-61 Gửi ký biên bản**. Đề xuất gom thành một UC:

- **UC-60 Chỉnh & gửi ký biên bản** *(Edit & Submit Meeting Minutes)* — UC-60a Chỉnh, UC-60b Gửi ký. Actor: Secretary; Email / SMTP.
- **UC-61** ghi "không còn dùng — gộp vào UC-60b", giữ số, không tái cấp.

Kiểm lại Report 3 bản DOCX: nếu ở đó đang ghi tên *Review & Submit Minutes for Signing* thì đổi theo. FE-12 vẫn là
Placeholder nên chỉ sửa docs. Cùng lúc nên giải quyết góp ý hội đồng: **ai chỉ định người ký** (đề xuất: Secretary chọn
người ký trong UC-60b) và **bước ban hành** sau UC-64.

### 5. Người nói bật mic, hệ thống ghi nhận lời nói — FE-08 / FE-10

**Hiện tại:** không có UC nào. FE-10 chỉ xử lý **file audio upload sau khi `Ended`** (UC-44 → UC-47); speaker trong
transcript do AI tách rồi Secretary gán tên tay. UC-39a *Micro* chỉ bật/tắt mic.

**Đề xuất UC mới:**

- **UC-74 Ghi nhận lời nói trong phòng họp** *(Capture Live Speech Transcript)* — khi một người bật mic và nói trong phòng
  họp online (`InProgress`), hệ thống chuyển giọng nói thành văn bản và **gắn với người đang nói theo track audio của
  họ**. Actor: User đang trong phòng; AI / Speech Provider. Quan hệ: độc lập khi đã kết nối.
- **UC-75 Bật / tắt ghi nhận lời nói** *(Start / Stop Live Transcription)* — Chair hoặc Secretary. Bắt buộc có bước
  thông báo và xin đồng ý trước khi ghi.
- Sửa UC-45 *Xem transcript có speaker*: nguồn transcript gồm cả bản ghi trực tiếp, không chỉ file upload.

Cách này xác định người nói **mà không cần voiceprint** (mỗi người một track trong LiveKit — xem
[ADR-0010](../05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md)), nên không đụng ranh giới "không voiceprint"
trong [ERD](../02-design/02-erd.md). Họp trực tiếp chung một micro vẫn phải gán tay. Cần ADR mới vì đổi phạm vi FE-10
(realtime) và kéo theo nghĩa vụ NĐ 13/2023/NĐ-CP: đồng ý ghi âm, mục đích, thời hạn lưu, quyền xoá — ghi rõ trong đặc tả,
không lưu lặng lẽ. Cần thêm bảng chi phí AI (góp ý hội đồng mục 2).

### 6. Giơ tay phát biểu — FE-08

**Hiện tại:** đã có ở **màn hình** (Report 3: nút "Giơ tay", hàng chờ "XIN PHÁT BIỂU"), **dữ liệu** (bảng speaking request),
**trạng thái** ([SM-07 SpeakingRequest](../10-statemachine/README.md), bản Draft) và [ADR-0011](../05-decisions/0011-tinh-nang-hop-la-panel-dung-chung.md),
nhưng **không có UC riêng** — đang nằm lẫn trong UC-39/UC-40/UC-41 (Placeholder).

**Đề xuất UC mới:**

- **UC-76 Giơ tay / hạ tay xin phát biểu** *(Raise / Lower Hand)* — User đang trong phòng, gồm Guest. Độc lập khi đã kết nối.
- **UC-77 Xử lý lượt xin phát biểu** *(Manage Speaking Requests)* — Chair: cho phát biểu, kết thúc lượt, bỏ qua. `<<extend>>` UC-41.

BR cần viết: thứ tự hàng chờ theo giờ giơ tay; mỗi người tối đa một yêu cầu đang chờ; yêu cầu tự huỷ khi rời phòng hoặc
cuộc họp kết thúc; có gắn với mục agenda hiện tại không. Sau đó chốt SM-07 khỏi trạng thái Draft.

### 7. Biểu quyết gắn với tài liệu, phải đọc xong mới được bỏ phiếu — FE-11 / FE-07

**Hiện tại mâu thuẫn:** BR-285 "Vote thuộc một Meeting, không gắn Agenda Item" (ADR-0019/ADR-0020). Gắn **tài liệu** không trái
trực tiếp BR-285 nhưng là thay đổi lớn của FE-11 đã đặc tả, nên cần ADR mới.

| Chỗ bị ảnh hưởng | Đề xuất |
|---|---|
| UC-53 | Thêm bước chọn một hoặc nhiều tài liệu của cuộc họp làm tài liệu của biểu quyết |
| UC-55 *Cast Vote* | Thêm điều kiện: người bỏ phiếu đã **xác nhận đọc** mọi tài liệu gắn với Vote |
| UC-32c *Xem trước / tải về* | Ghi nhận đã đọc |
| **UC mới — UC-78 Xác nhận đã đọc tài liệu** *(Acknowledge Document Read)* | Actor: người đủ điều kiện bỏ phiếu, gồm Guest. `<<extend>>` UC-32 |
| ERD | Bảng nối Vote ↔ MeetingDocument; bảng ghi nhận đọc (ai, tài liệu nào, lúc nào) |
| BR mới | Không bỏ phiếu khi chưa đọc đủ; danh sách tài liệu chốt khi mở Vote; gỡ tài liệu khỏi cuộc họp khi đang gắn Vote Open thì xử lý thế nào |

**Cần hỏi lại PO:** "đọc xong" đo bằng gì — (a) đã mở tài liệu, (b) cuộn tới trang cuối, (c) tự tích "Tôi đã đọc".
Đề xuất (c) kèm (a): hệ thống không chứng minh được người dùng thật sự đọc, nên chỉ ghi nhận **xác nhận** và giờ xác nhận.
Tài liệu `Secret` không tải về được thì đọc qua xem trước. Phiếu bí mật (BR-259) vẫn không được lộ ai chọn gì — ghi nhận
đọc tách khỏi phiếu.

---

## Tổng hợp UC

| Loại | UC | Ghi chú |
|---|---|---|
| Sửa actor / đặc tả | UC-53, UC-52 | Secretary tạo vote (ý 1) |
| Sửa đặc tả | UC-28b | Bỏ thao tác Secretary thay Chair (ý 2) |
| Sửa đặc tả | UC-23a–d, UC-24, UC-25 | Bỏ thời gian, bỏ chặn thứ tự (ý 3) |
| Gộp | UC-60 + UC-61 → UC-60 (60a, 60b); UC-61 không còn dùng | Edit & Submit (ý 4) |
| Sửa đặc tả | UC-45, UC-39a | Nguồn transcript trực tiếp (ý 5) |
| Sửa đặc tả | UC-53, UC-55, UC-32c | Vote gắn tài liệu (ý 7) |
| **Thêm** | UC-73 Chuyển giao chủ toạ | Ý 2 |
| **Thêm** | UC-74 Ghi nhận lời nói trong phòng họp, UC-75 Bật/tắt ghi nhận | Ý 5 |
| **Thêm** | UC-76 Giơ tay / hạ tay, UC-77 Xử lý lượt xin phát biểu | Ý 6 |
| **Thêm** | UC-78 Xác nhận đã đọc tài liệu | Ý 7 |

Số UC-73 → UC-78 là **đề xuất**, chỉ chốt khi cập nhật Master Use Case Inventory. Baseline sẽ thành 78 UC (UC-61 vẫn
tính số nhưng ghi không còn dùng).

## ADR cần viết

- Ghi nhận lời nói trực tiếp trong phòng họp online, xác định người nói theo track (ý 5) — đổi phạm vi FE-10.
- Biểu quyết gắn tài liệu và điều kiện đã đọc (ý 7) — bổ sung ADR-0019/ADR-0020.
- Chair tự chuyển giao, Secretary không thay Chair (ý 2) — nếu đổi quyết định FE-06 ngày 12/10/2026.

## Việc tiếp theo

| # | Việc | Người | Hạn |
|---|---|---|---|
| 1 | Gửi PO các câu hỏi "Cần hỏi lại PO" ở ý 1, 2, 3, 7 | KhanhPG | _(điền)_ |
| 2 | Tạo ticket Jira cho từng ý | _(điền)_ | _(điền)_ |
| 3 | Viết 3 ADR ở trên | _(điền)_ | _(điền)_ |
| 4 | Cập nhật Master Use Case Inventory, BR, Report 3 sau khi PO chốt | BA | _(điền)_ |
| 5 | Migration + sửa code agenda (ý 3), kiểm quyền vote (ý 1), thay Chair (ý 2) | BE | _(điền)_ |
