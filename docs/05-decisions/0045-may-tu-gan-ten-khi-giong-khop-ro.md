# ADR-0045: Máy tự gán tên người nói khi giọng khớp rõ; giọng thứ hai của cùng một người phải khớp cao hơn

- **Trạng thái:** Đề xuất — SCRUM-35, PO 01/11/2026
- **Ngày:** 2026-11-02
- **Người quyết định:** Hải (PO)
- **Thay:** câu "Chỉ Thư ký quyết định" trong [ADR-0027](0027-voice-id-co-dong-y.md), ở giọng khớp rõ. Giọng khớp
  vừa phải vẫn chỉ là gợi ý như cũ.
- **Khác AI-eCabinet:** eCab chỉ gợi ý (`VerdictSuggested`), không tự gán. Đây là yêu cầu riêng của PO.

## Bối cảnh

Hai việc báo trong kênh lỗi ngày 31/10 và 01/11/2026:

1. **Hai người bị gộp làm một.** Cuộc họp "[Test FE] Mic phòng – Trường Huy" có hai người nói. Máy tách
   đúng hai giọng, nhưng gợi ý cả hai là Trường Huy (0,755 và 0,645). FE gom giọng yếu vào giọng mạnh
   (`sameAsLabel`), nên tab Bản ghi chỉ còn một người nói. Ngưỡng "cùng giọng" (`SameVoiceThreshold`,
   0,7) không chặn được: đo lại trên server, hai người này giống nhau 0,745. Trong khi đó, một người
   thật bị tách làm hai (cuộc họp của Hải) chỉ giống nhau 0,777. Hai con số quá gần nhau nên không
   chỉnh được ngưỡng này. Thứ tách được hai trường hợp là điểm so với giọng mẫu: người lạ 0,645, người
   thật 0,771.
2. **Thư ký phải gán tay cả những giọng máy đã chắc chắn.** Giọng khớp 0,938 vẫn hiện "Người nói 3" cho
   tới khi Thư ký chọn tên. Sau khi chọn, tag "Khớp giọng nhất" cũng biến mất.

## Quyết định

1. **Giọng thứ hai của cùng một người phải đạt `VoiceId:SecondVoiceThreshold` (0,7)**, ngoài các điều
   kiện cũ. Giọng đầu tiên vẫn chỉ cần `SuggestThreshold` (0,6). Không đạt thì giọng đó giữ tên
   "Người nói N", không bị gom vào ai.
2. **Máy tự gán tên khi điểm ≥ `VoiceId:AutoAssignThreshold` (0,75).**
   - Tên do máy gán không có người gán (`assigned_by_id` để trống). API trả `autoAssigned: true` và nhật ký
     ghi `transcript.speaker_assigned` không có người làm, kèm `automatic: true` và điểm.
   - Thư ký vẫn đổi hoặc bỏ được tên đó. Đổi rồi thì tên là quyết định của Thư ký.
   - Máy không bao giờ gán Khách, vì Khách không có giọng mẫu.
   - Mỗi lần chuyển chữ lại, máy gán lại từ đầu, như mọi tên khác (ADR-0041).
3. **Tag "Khớp giọng nhất" luôn hiện** ở người máy gợi ý trong ô chọn tên, kể cả khi đã gán. Tag tô màu
   thương hiệu cho dễ thấy.

## Nghĩa vụ theo Luật 91/2025/QH15

- **Chỉ dựa trên đồng ý.** Máy chỉ so với giọng mẫu của thành phần cuộc họp đã đồng ý Voice ID, như cũ.
- **Rút đồng ý thì tên máy gán cũng mất.** Tên đó suy ra từ dữ liệu sinh trắc học. Khi một người rút
  đồng ý, các giọng máy tự gán là người đó trở về "Người nói N" cùng lúc với việc xoá gợi ý. Tên do Thư
  ký gán thì giữ lại, vì đó là quyết định của Thư ký về biên bản, không phải kết quả so giọng.
- **Không thêm dữ liệu mới.** Vector của từng giọng trong bản ghi vẫn không được lưu.

## Hệ quả

- Thư ký chỉ còn phải gán những giọng máy không chắc.
- Máy có thể gán sai người. Đo trên VIVOS ở ngưỡng 0,75: xem bảng trong `docs/03-api/13-nhan-dien-giong-noi.md`.
  Vì vậy tên máy gán có ghi chú khi rê chuột, và Thư ký kiểm tra trước khi duyệt biên bản.
- Một người bị tách làm hai mà giọng thứ hai khớp dưới 0,7 thì hiện thành hai người nói. Thư ký gán
  cùng một tên cho cả hai là gộp lại được, như trước.
