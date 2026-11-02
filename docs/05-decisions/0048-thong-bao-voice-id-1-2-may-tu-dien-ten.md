# ADR-0048: Thông báo đồng ý Voice ID lên 1.2; máy chỉ tự điền tên người đã đồng ý bản này

- **Trạng thái:** Đề xuất — SCRUM-35, PO 02/11/2026
- **Ngày:** 2026-11-02
- **Người quyết định:** Hải (PO)
- **Bổ sung:** [ADR-0045](0045-may-tu-gan-ten-khi-giong-khop-ro.md) (máy tự gán tên khi giọng khớp rõ)

## Bối cảnh

ADR-0045 cho máy tự điền tên người nói khi điểm khớp giọng từ 0,75 trở lên. Nhưng thông báo đồng ý
Voice ID đang hiện (1.1) vẫn ghi "Máy chỉ gợi ý, Thư ký quyết định tên", và mục đích lưu trong
`voice_consents` là "Gợi ý tên người phát biểu…". Người đã đồng ý bản 1.1 chưa từng được báo rằng máy
sẽ tự điền tên họ.

Theo Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15, đồng ý xử lý dữ liệu sinh trắc học chỉ có giá trị cho
đúng những gì người đó đã được báo. Tự điền tên là một cách xử lý mới, nên cần đồng ý mới.

## Quyết định

1. **Thông báo lên 1.2.**
   - Mục "Dùng ở đâu" ghi: máy gợi ý tên người nói, và tự điền tên khi giọng khớp rõ; Thư ký xem và
     sửa được.
   - Mục "Quyền của bạn" ghi: rút lại thì giọng mẫu, mọi gợi ý và tên máy đã điền bị xoá ngay.
   - Mục đích đổi thành "Nhận ra người phát biểu trong bản gỡ băng của các cuộc họp tôi tham dự (gợi ý
     tên, tự điền tên khi giọng khớp rõ), để Thư ký lập biên bản."
2. **Máy chỉ tự điền tên người có đồng ý từ bản 1.2 trở lên** (`VoiceIdRules.AutoAssignNoticeVersion`).
   Người còn ở bản 1.1 hay 1.0 vẫn được gợi ý như họ đã đồng ý, không bị tự điền.
3. **Người đồng ý bản cũ thấy lời mời đồng ý điều khoản mới** ở trang "Giọng nói của tôi". Lời mời
   liệt kê đúng những gì đã đổi kể từ bản họ đồng ý. Đồng ý thì gia hạn ngay dòng đồng ý cũ: giọng
   mẫu giữ nguyên, nhật ký ghi `voice.consent_renewed` kèm bản cũ và bản mới, như ADR-0042.

## Hệ quả

- Những người đã có giọng mẫu trên server test sẽ chưa được tự điền tên, cho tới khi họ bấm "Đồng ý
  điều khoản mới".
- Lần chuyển chữ lại sau khi gia hạn mới tự điền tên. Bản gỡ băng đã có giữ nguyên, vì tên chỉ được
  gán lúc chuyển chữ (ADR-0041).
- Bản sau này đổi câu chữ thì lại tăng phiên bản. So phiên bản bằng `CompareOrdinal`, như
  `AudioNoticeVersion`, nên phiên bản phải giữ dạng một chữ số sau dấu chấm (1.2, 1.3…) hoặc phải đổi
  cách so.
