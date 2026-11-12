# ADR-0060: Thư ký thu Voice ID hộ bằng micro phòng họp — bộ mẫu riêng, một lần đồng ý, quyền riêng

- **Trạng thái:** Đề xuất — PO (Hải) 12/11/2026. Sửa cùng ngày theo PO: một lần đồng ý cho cả hai bộ mẫu, bỏ đồng ý thu
  hộ riêng (migration `VoiceIdOneConsent`).
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

1. **Hai bộ giọng mẫu, một lần đồng ý.** Mỗi người có bộ **tự thu** (như cũ) và bộ **Thư ký thu hộ**, mỗi bộ tối đa
   5 mẫu. Cả hai nằm dưới **một** dòng `voice_consents` đang hiệu lực của người đó. Người dùng đồng ý **một lần** cho hệ
   thống nhận giọng, rút **một lần** là xoá cả hai bộ. Hai lần đồng ý cho hai cách ghi chỉ làm người dùng rối, và khi demo
   phải bấm hai nơi.
2. **Chính chủ tự đồng ý, câu thông báo nói rõ Thư ký thu hộ.** Thông báo Voice ID lên **1.3**: mục đích ghi rõ giọng mẫu
   do chính mình tự thu *hoặc do Thư ký cuộc họp mình dự thu bằng micro phòng họp*. Thư ký chỉ thu được người đã đồng ý
   theo thông báo 1.3 trở lên. Người đã đồng ý bản cũ thấy lời mời "Đồng ý điều khoản mới" (một lần bấm, giữ nguyên mẫu đã
   có, ADR-0042); trước đó Thư ký thu thì server từ chối (`MSG475`). Thư ký không đồng ý thay được.
3. **Quyền riêng cho Thư ký: `VoiceCollector`** ("Thu giọng mẫu hộ"), trong ma trận phân quyền, quản trị viên cấp cho
   vai trò mình tin. Vai trò Thư ký có sẵn quyền này, cả ở dữ liệu khởi tạo lẫn ở cơ sở dữ liệu đang chạy (migration
   `VoiceIdOneConsent` thêm một lần), nên server demo dùng được ngay. Quản trị viên gỡ đi thì không bị thêm lại. Thiếu
   quyền: `403`.
4. **Chỉ Thư ký của chính cuộc họp đó**, kiểm lại ở mỗi lần gọi: có quyền mà không phải Thư ký hiện tại của cuộc họp thì
   như cuộc họp không tồn tại (`404`). Thư ký bị thay giữa chừng mất quyền ngay.
5. **Chỉ cuộc họp có micro phòng** (trực tiếp, kết hợp), **không Mật**, chưa huỷ. Thu được khi cuộc họp **Đã công bố**
   hoặc **Đang họp**; sau đó vẫn xem, nghe, xoá được.
6. **Mẫu thu hộ thuộc về người được thu.** Mã hoá gắn với người đó như mẫu tự thu; ghi thêm ai thu (`collected_by_id`) và
   ở cuộc họp nào (`meeting_id`). Thư ký chỉ thấy, nghe lại, xoá mẫu thu ở cuộc họp của mình (thu lại = xoá rồi thu mới).
   Chủ mẫu thấy mọi mẫu thu hộ của mình, kèm cuộc họp và người thu, nghe lại và xoá được.
7. **Minh bạch.** Mỗi lần thu, chủ mẫu nhận thông báo. Mỗi lần Thư ký nghe lại một mẫu ghi nhật ký `voice.sample_played`.
   Thu và xoá mẫu thu hộ có nhật ký, `details.scope = "proxy"`; đồng ý và rút như Voice ID trước nay.
8. **Dùng để nhận người.** Máy so giọng với cả hai bộ, lấy mẫu khớp nhất. Mẫu thu hộ được **tự điền tên** khi giọng khớp
   rõ, như mẫu tự thu dưới thông báo 1.2 trở lên (ADR-0048); mẫu thu hộ luôn nằm dưới thông báo 1.3.
9. **Kết thúc.** Rút đồng ý xoá cả hai bộ, mọi gợi ý và tên máy tự điền trỏ tới người đó. Vô hiệu hoá tài khoản cũng vậy
   (ADR-0049).

## Nghĩa vụ theo Luật 91/2025/QH15

- **Đồng ý riêng, do chính chủ** (Điều 9): vẫn là đồng ý Voice ID riêng, tách khỏi đồng ý ghi âm; lưu phiên bản thông báo
  (1.3) và mục đích người đó đã đọc, trong đó có việc Thư ký thu hộ; ô đồng ý không đánh dấu sẵn.
- **Thông báo đủ** (Điều 9 khoản 2): thông báo Voice ID nêu mục đích, ai thu (tự thu, hoặc Thư ký cuộc họp mình dự bằng
  micro phòng họp), lưu gì, quyền của người dùng, bên kiểm soát.
- **Bảo mật dữ liệu nhạy cảm** (Điều 31 khoản 4): ba lớp chặn (quyền, đúng Thư ký của cuộc họp, đồng ý của chính chủ); mã hoá
  như cũ; mỗi lần người khác nghe đều ghi nhật ký.
- **Rút và xoá**: rút đồng ý xoá ngay mọi mẫu của cả hai bộ trong một transaction, dưới cùng khoá dòng mà việc thu mẫu và
  việc so giọng dùng.

## Hệ quả

- Thư ký có thể chuẩn bị trước buổi họp: mở tab *Bản ghi*, thu từng người bằng micro phòng. Họp trực tiếp nhận tên đúng hơn
  mà người dự không phải tự làm gì ngoài **một** lần bấm đồng ý Voice ID.
- Người họp trực tuyến không cần thu hộ: họ được ghi theo micro của chính mình nên đã biết ai nói (ADR-0040).
- Thêm một quyền vào ma trận; mã `MSG473`–`MSG477` (`MSG471`, `MSG472` của đồng ý thu hộ riêng đã ngừng dùng); một loại
  thông báo, một hành động nhật ký; migration `VoiceIdProxyCollection` (cột `collected_by_id`, `meeting_id`) và
  `VoiceIdOneConsent` (bỏ cột `scope`, đóng các đồng ý thu hộ riêng và xoá mẫu dưới chúng, cấp quyền cho vai trò Thư ký).
- Chưa làm: chọn thiết bị micro trong hộp thoại thu (đang dùng micro mặc định của trình duyệt).
