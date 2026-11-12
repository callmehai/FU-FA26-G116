# ADR-0060: Thư ký thu Voice ID hộ bằng micro phòng họp — bộ mẫu và đồng ý riêng, quyền riêng

- **Trạng thái:** Đề xuất — PO (Hải) 12/11/2026
- **Ngày:** 2026-11-12
- **Người quyết định:** Hải (PO), theo note của BA (Khánh) ở 📌・note-chung ngày 12/11/2026
- **Bổ sung:** [ADR-0027](0027-voice-id-co-dong-y.md) (Voice ID có đồng ý). Không thay quyết định nào: mẫu tự thu giữ nguyên.
- **Liên quan:** SCRUM-35, [ADR-0040](0040-cach-ghi-am-theo-hinh-thuc-hop.md) (họp có phòng ghi bằng mic phòng),
  [ADR-0049](0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md) (vô hiệu hoá tài khoản kết thúc Voice ID),
  [ADR-0057](0057-ba-muc-mat-nhu-ai-ecabinet.md) (họp Mật không ghi âm)

## Bối cảnh

Họp trực tiếp và phần phòng họp của họp kết hợp được ghi bằng **một micro phòng** (ADR-0040). Thực tế đơn vị dùng một
micro chuyên dụng của phòng họp. Giọng mẫu mỗi người tự ghi trên laptop của mình (ADR-0027) đi qua micro khác, phòng khác,
nên khớp kém với giọng trong bản ghi phòng. Đòi mỗi người mang máy, tự ghi bằng micro riêng thì vướng phần cứng: nhiều người
không có micro tốt, hoặc không tự làm.

Cách thực tế: **Thư ký thu giọng mẫu hộ** từng người, bằng chính micro phòng họp. Nhưng như vậy Thư ký chạm vào **dữ liệu
sinh trắc học của người khác** (Luật 91/2025/QH15, Điều 31), nên phải có cách giữ an toàn.

## Quyết định

1. **Hai bộ giọng mẫu, hai đồng ý.** Mỗi người có bộ **tự thu** (như cũ) và bộ **Thư ký thu hộ**. Mỗi bộ nằm dưới một dòng
   `voice_consents` riêng (`scope`: `Self` / `Proxy`); mỗi người tối đa một dòng đang hiệu lực cho mỗi bộ. Mỗi bộ tối đa
   5 mẫu. Rút đồng ý bộ nào thì xoá mẫu bộ đó, bộ kia giữ nguyên.
2. **Chính chủ tự đồng ý.** Đồng ý thu hộ chỉ tạo được bằng `POST /api/me/voice/proxy-consent` của chính người đó, ở trang
   *Giọng nói của tôi*, tab *Thư ký thu hộ*. Thư ký không đồng ý thay được. Chưa đồng ý thì server từ chối thu (`MSG475`).
3. **Quyền riêng cho Thư ký: `VoiceCollector`** ("Thu giọng mẫu hộ"), trong ma trận phân quyền, quản trị viên cấp cho
   vai trò mình tin. Vai trò Thư ký mặc định có quyền này ở dữ liệu khởi tạo; server test đã có dữ liệu nên quản trị viên
   tự bật. Thiếu quyền: `403`.
4. **Chỉ Thư ký của chính cuộc họp đó**, kiểm lại ở mỗi lần gọi: có quyền mà không phải Thư ký hiện tại của cuộc họp thì
   như cuộc họp không tồn tại (`404`). Thư ký bị thay giữa chừng mất quyền ngay.
5. **Chỉ cuộc họp có micro phòng** (trực tiếp, kết hợp), **không Mật**, chưa huỷ. Thu được khi cuộc họp **Đã công bố**
   hoặc **Đang họp**; sau đó vẫn xem, nghe, xoá được.
6. **Mẫu thu hộ thuộc về người được thu.** Mã hoá gắn với người đó như mẫu tự thu; ghi thêm ai thu (`collected_by_id`) và
   ở cuộc họp nào (`meeting_id`). Thư ký chỉ thấy, nghe lại, xoá mẫu thu ở cuộc họp của mình (thu lại = xoá rồi thu mới).
   Chủ mẫu thấy mọi mẫu thu hộ của mình, kèm cuộc họp và người thu, nghe lại và xoá được.
7. **Minh bạch.** Mỗi lần thu, chủ mẫu nhận thông báo. Mỗi lần Thư ký nghe lại một mẫu ghi nhật ký `voice.sample_played`.
   Thu, xoá, đồng ý, rút đều có nhật ký, `details.scope = "proxy"`.
8. **Dùng để nhận người.** Máy so giọng với cả hai bộ, lấy mẫu khớp nhất. Mẫu thu hộ được **tự điền tên** khi giọng khớp
   rõ (thông báo thu hộ 1.0 đã nói rõ), như mẫu tự thu dưới thông báo 1.2 (ADR-0048).
9. **Kết thúc.** Rút đồng ý thu hộ hoặc tự thu đều bỏ mọi gợi ý và tên máy tự điền trỏ tới người đó; lần gỡ băng sau gợi ý
   lại từ bộ còn lại. Vô hiệu hoá tài khoản kết thúc cả hai bộ (ADR-0049).

## Nghĩa vụ theo Luật 91/2025/QH15

- **Đồng ý riêng, do chính chủ** (Điều 9): dòng `Proxy` riêng, lưu phiên bản thông báo và mục đích người đó đã đọc; ô đồng ý
  không đánh dấu sẵn.
- **Thông báo đủ** (Điều 9 khoản 2): tab *Thư ký thu hộ* nêu mục đích, ai thu, lưu gì, quyền của người dùng, bên kiểm soát.
- **Bảo mật dữ liệu nhạy cảm** (Điều 31 khoản 4): ba lớp chặn (quyền, đúng Thư ký của cuộc họp, đồng ý của chính chủ); mã hoá
  như cũ; mỗi lần người khác nghe đều ghi nhật ký.
- **Rút và xoá**: rút đồng ý thu hộ xoá ngay mọi mẫu thu hộ trong một transaction, dưới cùng khoá dòng mà việc thu mẫu và
  việc so giọng dùng.

## Hệ quả

- Thư ký có thể chuẩn bị trước buổi họp: mở tab *Bản ghi*, thu từng người bằng micro phòng. Họp trực tiếp nhận tên đúng hơn
  mà người dự không phải tự làm gì ngoài một lần bấm đồng ý.
- Người họp trực tuyến không cần thu hộ: họ được ghi theo micro của chính mình nên đã biết ai nói (ADR-0040).
- Thêm một quyền vào ma trận; mã `MSG471`–`MSG477`; một loại thông báo, một hành động nhật ký; migration
  `VoiceIdProxyCollection` (cột `scope`, `collected_by_id`, `meeting_id`, chỉ mục duy nhất theo người và bộ).
- Chưa làm: chọn thiết bị micro trong hộp thoại thu (đang dùng micro mặc định của trình duyệt).
