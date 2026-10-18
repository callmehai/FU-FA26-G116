# Góp ý của hội đồng — lần review 1

> **Ghi nhận ngày 18/10/2026.** Ngày review: _(điền ngày hội đồng review)_.
> **Trạng thái 18/10/2026: ĐÃ ÁP DỤNG MỘT PHẦN vào Report 3 local** (chưa commit, chưa lên Drive). Ticket
> Jira đã tạo ngày 18/10/2026: SCRUM-99 (huỷ / quá hạn), SCRUM-100 (ADR, chi phí AI, Report 3 còn sót),
> SCRUM-101 → SCRUM-104 (đặc tả FE-08/09/10/12). Chưa có ADR hay code. Mục quá hạn (BR-301, BR-302) dùng giá trị mặc định, còn chờ PO xác nhận.

| Mục | Kết quả | Còn lại |
|---|---|---|
| 1, 3. Xác định người nói / hướng AI | Chọn hướng A: gắn người nói theo track mic họp online, không Voice ID. Chưa sửa Report 3 vì FE-08/FE-10 còn placeholder | ADR; [note UC chưa đặc tả](NOTE-uc-chua-dac-ta-cho-sua.md) |
| 2. Chi phí AI | Chưa làm | Bảng chi phí |
| 4.1. Huỷ / quá hạn | Thêm **UC-75 Cancel Meeting**; sửa UC-16, UC-17; BR-297 → BR-302; MSG261 → MSG264 | PO xác nhận mốc 24 giờ / 4 giờ; entity, job, code; ADR |
| 4.2. Người ký, ban hành | Chưa sửa: FE-12 còn placeholder | Note UC chưa đặc tả |
| 4.3. Uỷ quyền, nhắc lịch | BR-202 ngừng dùng; thêm BR-303 nhắc lịch (theo code hiện tại) | Chú thích Permission Matrix còn chữ "delegated" |
| 4.4. NĐ 13/2023, chữ ký số | Chưa sửa: FE-09/FE-12 còn placeholder | Note UC chưa đặc tả |

Note gồm hai phần cho mỗi góp ý: **nguyên văn ý hội đồng** (tóm lại) và **hiện trạng trong repo** — để thấy
góp ý chạm vào chỗ nào và có mâu thuẫn với quyết định đã chốt hay không.

---

## 1. Chưa xác định được ai là người phát biểu (quan trọng nhất)

**Hội đồng:** với file âm thanh, hệ thống chưa biết ai nói; nội dung biên bản chưa gắn với từng người nói.

**Hiện trạng:**

- FE-10 (UC-44 → UC-47): AI chỉ tách người nói **trong một bản ghi** (`SpeakerLabel` = "Người nói 1, 2, 3…"),
  sau đó Secretary **gán tên tay** (`SpeakerUserId`) — xem [ERD](../02-design/02-erd.md).
- Voiceprint đang **bị loại khỏi phạm vi có chủ đích**: [ERD — Ranh giới đã chốt](../02-design/02-erd.md),
  mục "Không làm" trong [README](../../README.md), và có test tự động chặn thêm cột kiểu voiceprint. Lý do:
  dữ liệu sinh trắc học theo Nghị định 13/2023/NĐ-CP.

→ Góp ý này **mâu thuẫn trực tiếp** với phạm vi hiện tại. Muốn đổi phải có ADR mới, không sửa lặng lẽ.

## 2. Tính khả thi

**Hội đồng:**

- Áp dụng cho họp online khả thi hơn, nhưng khó cạnh tranh với công cụ có sẵn (đã tích hợp ghi âm + chuyển
  giọng nói thành văn bản).
- Nhóm phải **tính rõ chi phí AI** (speech-to-text, nhận dạng người nói, tóm tắt) để chứng minh khả thi về
  kinh tế khi triển khai thật.

**Hiện trạng:** chưa có tài liệu nào ước tính chi phí AI. Phòng họp online đã chọn LiveKit SFU
([ADR-0010](../05-decisions/0010-phong-hop-truc-tuyen-dung-livekit-sfu.md)).

**Cần làm:** bảng chi phí theo kịch bản (VD: số cuộc họp/tháng × thời lượng trung bình × đơn giá từng
dịch vụ), so sánh ít nhất 2 nhà cung cấp và phương án tự host. Ghi rõ nguồn và ngày lấy đơn giá.

## 3. Định hướng đề xuất

**Hội đồng đưa hai hướng:**

- **Có AI:** nhận dạng giọng từng người và chuyển thành văn bản **ngay trong khi họp** — cần Voice ID cho từng
  người, từ đó tách và nhận diện nhiều người nói trong cùng một file (speaker diarization).
- **Thay thế:** nếu không giải được, bỏ phần AI, tập trung vào quản lý biên bản họp.

**Các phương án nhóm có thể cân nhắc khi trình PO** (chưa chốt):

| Phương án | Ý chính | Đánh đổi |
|---|---|---|
| A. Gắn người nói theo **track âm thanh** của họp online | Trong LiveKit mỗi người tham dự có track audio riêng, nên biết ai nói mà **không cần voiceprint** | Chỉ áp dụng cho họp online; họp trực tiếp chung một micro vẫn phải gán tay |
| B. Voice ID (đăng ký giọng mẫu) + diarization | Đúng ý hội đồng, áp dụng cả file ghi âm họp trực tiếp | Dữ liệu sinh trắc học: cần đồng ý riêng, mục đích, thời hạn lưu, quyền xoá theo NĐ 13/2023; đảo ngược quyết định phạm vi hiện tại; tốn công và chi phí |
| C. Bỏ AI, tập trung quản lý biên bản | Giảm rủi ro kỹ thuật và pháp lý | Mất điểm khác biệt chính của sản phẩm |

Dù chọn A hay B, phần "trong khi họp" (realtime) là yêu cầu mới — hiện FE-10 xử lý audio **sau** khi họp.

## 4. Hạn chế về tài liệu

### 4.1. Không huỷ được cuộc họp, không có quy tắc cho cuộc họp quá hạn

**Hội đồng:** cuộc họp chỉ đi một chiều `Draft → Published → InProgress → Ended`; không có đường dừng một
cuộc họp không còn tổ chức, và hệ thống không tự xử lý khi không ai bấm Start/End.

**Hiện trạng:** `MeetingStatus` trong code chỉ có `Draft, WaitingApproval, Rejected, Approved, Published,
InProgress, Ended` — không có `Cancelled`. [Vòng đời cuộc họp](../02-design/01-vong-doi-cuoc-hop.md) và
[SM-01](../10-statemachine/README.md) không có nhánh huỷ hay quá hạn.

**Cần chốt:** trạng thái `Cancelled` (từ trạng thái nào, ai được huỷ, có lý do bắt buộc, có báo người được
mời); quy tắc quá hạn (VD: `Published` quá giờ kết thúc dự kiến X giờ mà chưa Start thì xử lý gì;
`InProgress` quá Y giờ chưa End thì tự kết thúc hay chỉ nhắc). Thêm trạng thái = migration mới + cập nhật
`MeetingStatusHistory`.

### 4.2. Không quy định ai chỉ định người ký biên bản, thiếu bước ban hành

**Hiện trạng:** FE-12 có actor "Người ký được chỉ định" (UC-62, UC-65) nhưng không UC nào chỉ định người
ký; UC-64 *Finalize* là niêm phong, chưa có bước **ban hành** (phát hành biên bản tới người liên quan).
Toàn bộ FE-12 vẫn là Placeholder — xem
[Master Use Case Inventory](../01-requirements/00-master-use-case-inventory.md).

### 4.3. Uỷ quyền dự họp có dữ liệu nhưng không có nghiệp vụ; nhắc lịch chưa đặc tả

**Hiện trạng:**

- Uỷ quyền đã **bỏ** theo quyết định D4 (phản hồi lời mời chỉ còn `Accepted`/`Declined`, BR-105), nhưng
  còn sót dấu vết — VD `BR-202` "Guest cannot receive delegated authority" trong
  [Business Rules](../01-requirements/06-business-rules.md). Cần rà sót Report 3, BR, ERD, SM để xoá phần
  còn sót, **hoặc** nếu PO muốn giữ uỷ quyền thì phải viết UC/BR đầy đủ.
- Nhắc lịch: ERD chỉ nhắc tới khoá chống trùng `meeting-starting-soon:<meeting-id>:<user-id>`; không có BR
  nào quy định **nhắc lúc nào, nhắc ai, qua kênh nào** (in-app / email).

### 4.4. Thiếu nghiệp vụ lưu trữ – xoá dữ liệu theo NĐ 13/2023; chữ ký số chưa rõ cơ chế

**Hội đồng:** NFR cam kết tuân thủ NĐ 13/2023 và có chữ ký số, nhưng không UC, BR hay job nào thực hiện.

**Hiện trạng:** bảng `recordings` đã có `ConsentObtainedAt`, `ConsentPurpose`, `RetainUntil`, `DeletedAt`
([ERD](../02-design/02-erd.md)), nhưng chưa có UC xin đồng ý, UC/job xoá khi tới hạn, UC xử lý yêu cầu xoá
của chủ thể dữ liệu. Ký HSM thật nằm ngoài phạm vi; chưa tài liệu nào nói rõ "ký điện tử" trong FE-12 là
cơ chế gì (xác nhận trong hệ thống, hash + timestamp, hay chứng thư số).

---

## Việc cần làm tiếp

| # | Việc | Phụ thuộc |
|---|---|---|
| 1 | PO chốt hướng AI (A / B / C ở mục 3), viết ADR | Bảng chi phí ở #2 |
| 2 | Lập bảng ước tính chi phí AI | — |
| 3 | Bổ sung trạng thái huỷ + quy tắc quá hạn vào vòng đời, SM-01, BR, Report 3 | PO chốt |
| 4 | Đặc tả chỉ định người ký và bước ban hành trong FE-12 | — |
| 5 | Rà sót dấu vết uỷ quyền; viết BR nhắc lịch | PO chốt có giữ uỷ quyền không |
| 6 | Viết UC/BR/job lưu trữ – xoá theo NĐ 13/2023; mô tả cơ chế chữ ký | — |

Mỗi việc cần một ticket Jira (project `SCRUM`); note này không thay ticket.
