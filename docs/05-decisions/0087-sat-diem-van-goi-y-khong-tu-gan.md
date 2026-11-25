# ADR-0087: Giọng sát điểm giữa hai người vẫn được gợi ý, không tự gán

- **Trạng thái:** Chấp nhận — PO (Hải) 25/11/2026 (chọn cách B sau buổi test kết hợp)
- **Ngày:** 2026-11-25
- **Người quyết định:** Hải
- **Bổ sung:** [ADR-0045](0045-may-tu-gan-ten-khi-giong-khop-ro.md), [ADR-0074](0074-nguong-tach-nguoi-noi-va-voice-id-sau-buoi-thu-21-11.md)
  (giữ mọi ngưỡng, giữ `SuggestMargin` 0,05)

## Bối cảnh

Buổi test kết hợp 25/11/2026, hai bản ghi mic phòng có giọng không được gợi ý ai dù điểm đủ cao:

| Cuộc họp | Giọng | Người khớp nhất | Người xếp sau |
|---|---|---|---|
| 01a212dc | SPEAKER_00 | Trường Huy 0,612 | Khánh 0,594 |
| 01a21330 (1 phút 21) | Người nói 1, 28 giây | Khánh 0,726 | Hải 0,697 |
| 01a21330 | Người nói 2, 51 giây | Hải 0,612 | Khánh 0,604 |

Cả ba đều qua ngưỡng gợi ý 0,6 nhưng hơn người xếp sau dưới 0,05, nên trước đây bị bỏ hẳn: ô người nói không có
"Khớp giọng nhất", Thư ký phải tự đoán. Giọng mẫu của Hải và Khánh cùng được thu trên một máy, một chỗ ngồi (21/11), nên
nghe giống nhau; bản ghi ngắn càng làm hai điểm sát nhau.

Hạ `SuggestMargin` (0,05 → 0,02) thì máy sẽ tự gán cả các trường hợp này, nhưng chưa đo lại được (bộ VIVOS đã xoá), và
gán nhầm tên người vào biên bản tệ hơn để trống.

## Quyết định

1. Giữ `SuggestMargin` 0,05 cho việc **máy tự gán**.
2. Giọng mà người khớp nhất đạt `SuggestThreshold` nhưng hơn người xếp sau dưới mức đó vẫn được **gợi ý** người khớp nhất
   (`suggestedUserId`, "Khớp giọng nhất" trên ô người nói). Máy không tự gán tên: Thư ký xác nhận hoặc chọn người kia.
3. Mỗi người được gợi ý sát điểm cho tối đa một giọng, giọng điểm cao hơn trước; không gợi ý sát điểm một người đã được
   gợi ý chắc cho giọng khác.
4. Nhật ký server ghi gợi ý sát điểm là `(near tie)`.

API không đổi hình dạng. FE không đổi.

## Hệ quả

- Ba giọng trên được gợi ý: Khánh, Hải, Trường Huy. Thư ký bấm xác nhận một lần cho mỗi giọng thay vì chọn từ danh sách.
- Gợi ý sát điểm dễ sai người hơn gợi ý chắc: người xếp sau chỉ kém vài phần trăm. Vì không tự gán, tên sai chỉ vào
  biên bản khi Thư ký xác nhận.
- Gốc vấn đề vẫn là giọng mẫu: thu lại bằng đúng micro phòng họp, mỗi người ngồi đúng chỗ của mình, sẽ kéo hai điểm xa nhau.
