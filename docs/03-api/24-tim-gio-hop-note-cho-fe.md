# Tìm giờ họp — ghi chú cho FE (SCRUM-137)

**Ngày:** 11/11/2026 · **Người viết:** BE (Huy) · **Contract:** [24-tim-gio-hop.md](24-tim-gio-hop.md)

BE đã sẵn hai endpoint: `POST /api/calendar/slots` (UC-85a) và `POST /api/calendar/slots/draft` (UC-85b). Ghi chú này là những
điều người làm màn hình cần biết; quy tắc đầy đủ ở contract.

## 1. Màn hình và quyền

- Mục sidebar **"Tìm giờ họp"** chỉ hiện với capability `MeetingCreator` (AC ticket). Server vẫn kiểm lại và trả `403 MSG448`;
  route thiếu quyền render 404 như mọi route bị hạn chế.
- Màn có hai tab (PO D2, ADR-0058): **Tìm khung giờ** (API mới này) và **Lịch được chia sẻ** (UC-85c,
  `GET /api/calendar/shared-week`, đang nằm ở tab Lịch tuần của `/calendar/sharing`). Dùng lại component tuần đã có; chọn đặt tab ở
  đâu là việc của FE (ghi lại cho BA Final Review).
- Theo `AGENTS.md`: thêm `frontend/src/types/<tên>.ts` và `frontend/src/api/<tên>.ts` (gọi qua `api.post`), khớp lỗi bằng
  `isApiError` theo **mã**, không theo câu chữ. Đường dẫn tương đối `/api/...`.

## 2. Form tìm

- Chọn người: `GET /api/meetings/participant-options?q=&page=&pageSize=` (đã có). Tối đa **10** người, không gồm chính mình.
- Ô **"Tính cả tôi"** → `includeMe` (server mặc định `false`; FE chọn mặc định hiển thị).
- Mặc định như eCab: thời lượng 120 phút; từ hôm nay → thứ Sáu; T2–T6; 08:00–17:00; nghỉ trưa 12:00–13:00 (có công tắc tắt →
  `lunchBreak: false`, dùng cho cuộc họp dài hơn một buổi); "Cách cuộc họp khác" 30 phút (0–4 giờ); "Làm tròn giờ bắt đầu" 30 phút
  (5 phút–2 giờ). Thời lượng, đệm, bước là bội 5 phút. Khoảng ngày tối đa 31 ngày.
- Luôn hiện câu cố định (BR-428): `Chỉ tính lịch đã được chia sẻ. Người được mời vẫn có thể bận.`
- `400 MSG444` có `errors` theo tên trường (`durationMinutes`, `toDate`, `userIds[2]`…): tô đỏ đúng ô, hiện câu trong `errors`.

## 3. Kết quả

- `people[]` theo thứ tự: mình (nếu tick, `status = "Own"`), rồi người đã chọn. `status = "NotShared"` → hiện
  **"Chưa được chia sẻ lịch"** cạnh tên, kèm nút **"Xin xem lịch"** mở hộp thoại UC-84a sẵn có (`POST /api/calendar/shares`,
  chọn sẵn người đó). Người NotShared không rảnh cũng không bận.
- `days[].slots[]`: gom theo ngày. `busyUserIds` = bận, `unknownUserIds` = **chưa biết** (ngày ngoài khoảng chia sẻ) — không được vẽ
  như rảnh. Tên lấy từ `people` theo id. Không có tên cuộc họp nào để hiện (API không trả).
- `allFree = true` → mọi người được tính đều rảnh. `code = "MSG445"` → hiện câu `message` phía trên danh sách (thông tin, không
  phải lỗi), mỗi khung ghi ai bận / chưa biết.
- `days = []` và `code = null` → trạng thái trống: chưa ai chia sẻ lịch (mọi người `NotShared` và không tick "Tính cả tôi") hoặc
  không còn khung nào trong khoảng đã chọn.
- `truncated = true` → chỉ hiện 200 khung đầu; gợi ý thu hẹp khoảng ngày.

## 4. Tạo nháp từ một khung (UC-85b)

- Nút **"Tạo nháp"** trên khung → mở hộp thoại tạo cuộc họp hiện có, **khoá giờ** theo khung, điền sẵn danh sách: người đầu tiên
  = Chủ toạ, còn lại = Thành viên, mình = Thư ký (cho đổi thứ tự để chọn Chủ toạ; server lấy `userIds[0]` làm Chủ toạ).
- Gửi `POST /api/calendar/slots/draft` với các trường cuộc họp như `POST /api/meetings` (không có `participants`), cộng `userIds`,
  `includeMe`, `bufferMinutes` đúng như lúc tìm và `acceptedBusyUserIds = slot.busyUserIds ∪ slot.unknownUserIds`.
- `201 MSG29` → mở chi tiết cuộc họp `meeting.id` (cuộc họp ở Nháp, đi luồng duyệt bình thường).
- `409 MSG446` → báo câu đó, đóng hộp thoại, tìm lại.
- `400 MSG31` / `400 MSG277` → tô ô theo `errors` (vd. giờ bắt đầu đã qua kể từ lúc tìm → `plannedStartTime`).
- `500 MSG453` / `500 MSG35` → báo lỗi, cho thử lại.

## 5. Mã FE cần khớp

`MSG444`, `MSG445`, `MSG446`, `MSG448`, `MSG453`, `MSG29`, `MSG31`, `MSG35`, `MSG277` — đều đã có trong
[danh mục](00-danh-muc-ma-thong-bao.md). Không cần mã mới.
