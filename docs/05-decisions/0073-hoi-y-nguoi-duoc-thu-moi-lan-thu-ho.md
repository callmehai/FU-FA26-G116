# ADR-0073: Thu giọng mẫu hộ — hỏi ý người được thu ở mỗi lần thu, một mức đồng ý cho mọi mẫu thu hộ

- **Trạng thái:** Đề xuất — chờ PO xác nhận
- **Ngày:** 2026-11-22
- **Người quyết định:** Hải (theo báo lỗi của Lộc 21/11/2026)
- **Bổ sung:** [ADR-0060](0060-thu-ky-thu-voice-id-ho.md) (Thư ký thu Voice ID hộ), [ADR-0071](0071-thu-giong-mau-ho-bat-ky-luc-nao.md)
  (thu hộ bất kỳ lúc nào). Thay một điểm của ADR-0071: "người đã đồng ý 1.3 vẫn được Thư ký thu trong cuộc họp như cũ".
- **Liên quan:** [ADR-0027](0027-voice-id-co-dong-y.md), [ADR-0049](0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md)

## Bối cảnh

Lộc báo (21/11/2026, kênh báo lỗi): *Thư ký thu hộ giọng mẫu cho người dùng trong cuộc họp từ màn hình cuộc họp thì bỏ qua
được phần hỏi quyền thu âm giọng.*

Tái hiện được trên dữ liệu thật, cùng một người, cùng một người thu:

1. Người được thu đã bật Voice ID trước chiều 21/11, tức đồng ý thông báo **1.3**.
2. Ở *Quản trị → Người dùng → nút micro*, hộp thoại báo "Người này chưa đồng ý điều khoản Voice ID mới", không có nút Thu;
   gọi thẳng `POST /api/users/{userId}/voice-collection/samples` trả `409 MSG475`.
3. Ở *chi tiết cuộc họp → Thành phần tham dự / Bản ghi → Giọng mẫu cho mic phòng*, cùng người đó hiện "đã đồng ý", có nút
   **Thu**; `POST /api/meetings/{id}/voice-collection/{userId}/samples` qua bước kiểm đồng ý, tới máy nhận dạng và lưu mẫu
   (`201 MSG473`; máy local không có speech worker thì dừng ở `503 MSG312`, sau bước kiểm đồng ý).

Nguyên nhân: hai đường thu dùng hai mức đồng ý khác nhau. Đường trong cuộc họp nhận 1.3 (ADR-0060), đường theo người đòi
1.4 (ADR-0071). Với người thu, màn hình cuộc họp là cách đi vòng qua bước "đồng ý điều khoản mới" mà màn Người dùng bắt.

Còn một lỗ thứ hai, sâu hơn: ở cả hai đường, không có bước nào hỏi người được thu **ngay lúc thu**. Đồng ý Voice ID là đồng ý
thường trực, có thể đã cho từ lâu. Người thu bấm Thu là ghi, người được thu chỉ biết khi nhận thông báo sau đó.

## Quyết định

1. **Một mức đồng ý cho mọi mẫu thu hộ: thông báo 1.4 trở lên**, trên cả đường trong cuộc họp lẫn đường theo người
   (`VoiceIdRules.ProxyNoticeVersion`). Người còn đồng ý 1.3 hiện "Chưa đồng ý" ở cả hai màn, không có nút Thu, API trả
   `409 MSG475`. Họ thấy lời mời "Đồng ý điều khoản mới" ở trang *Giọng nói của tôi*, bấm một lần là xong, như mọi lần lên
   phiên bản thông báo. Trang *Giọng nói của tôi* của họ cũng báo "chưa cho người khác thu hộ" theo cùng mức này.
2. **Hỏi ý người được thu ở mỗi lần thu.** Hộp thoại thu giọng hộ có ô "Đã hỏi, *tên người* đồng ý cho thu giọng mẫu lần
   này". Chưa tick thì không bấm được Bắt đầu ghi, Ghi lại hay Lưu. Ô tick đi kèm tệp trong trường `personAgreed=true`.
3. **Server chặn, không chỉ giao diện.** Mẫu thu hộ thiếu `personAgreed=true` (thiếu trường, `false`, giá trị khác) bị trả
   `400 MSG483` (`errors.personAgreed`), trước khi tệp tới máy nhận dạng, không lưu gì, không gửi thông báo. Kiểm sau cuộc
   họp (`404`/`409 MSG477`) và đồng ý thường trực (`409 MSG475`), trước giới hạn số mẫu (`409 MSG476`).
4. **Ghi lại bằng chứng.** Dòng nhật ký `voice.sample_added` của mẫu thu hộ có `personAgreed: true` cùng người thu
   (`actorId`). Thông báo `VoiceSampleCollected` gửi chủ mẫu thêm câu "Người thu đã xác nhận bạn đồng ý cho lần thu này. Nếu
   bạn không đồng ý, hãy xoá mẫu ở trang Giọng nói của tôi." Người được thu là người biết xác nhận đó có đúng không, và xoá
   được ngay.
5. Thông báo đồng ý (ô "Ai thu" ở trang *Giọng nói của tôi*) thêm: người thu hỏi ý tôi trước mỗi lần thu. Đây là thêm một
   bảo đảm cho người được thu, không mở rộng việc xử lý, nên **không** lên phiên bản thông báo và không ai phải đồng ý lại
   vì điểm này.

Không thêm bảng hay cột: bằng chứng của từng lần hỏi nằm ở nhật ký kiểm toán, vốn chỉ thêm, không sửa.

## Nghĩa vụ theo Luật 91/2025/QH15 và Nghị định 356/2025/NĐ-CP

- **Đồng ý riêng, của chính chủ, đúng mục đích đã đọc** (Luật, Điều 9): đồng ý thường trực vẫn chỉ do chính người đó cho, ở
  trang của họ; không ai đồng ý thay được. Mọi mẫu thu hộ giờ cùng đòi thông báo 1.4, thông báo nói rõ ai được thu.
- **Im lặng không phải đồng ý** (Luật, Điều 9 khoản 4 điểm d): người thu phải hỏi và tick cho từng lần thu; không có ô tick
  sẵn. Đây là xác nhận của người thu, không thay đồng ý của chủ mẫu; vì vậy chủ mẫu được báo ngay và xoá được.
- **Bảo mật dữ liệu nhạy cảm** (Luật, Điều 31 khoản 4): mẫu bị từ chối không tới máy nhận dạng, không được lưu dưới dạng nào.
- **Rút và xoá**: không đổi. Rút đồng ý hoặc bị vô hiệu hoá tài khoản thì xoá ngay mọi giọng mẫu, cả mẫu thu hộ
  ([ADR-0049](0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md)). Chủ mẫu xoá từng mẫu thu hộ bất cứ lúc nào.

## Hệ quả

- Người đã đồng ý 1.3 mà chưa đồng ý lại thì không ai thu hộ được, kể cả Thư ký trong cuộc họp. Trên server test, đó là mọi
  người bật Voice ID trước chiều 21/11.
- Thêm `MSG483`. Không đổi `MSG475` (cùng tình huống: chưa đồng ý điều khoản mới), chỉ đổi điều kiện của đường trong cuộc họp.
- `POST …/voice-collection/{userId}/samples` và `POST /api/users/{userId}/voice-collection/samples` có thêm trường
  `personAgreed`; client cũ không gửi trường này bị `400 MSG483`.
- Phương án mạnh hơn, chưa làm: người được thu tự bấm đồng ý cho lần thu trên máy của họ (thông báo "X muốn thu giọng mẫu
  của bạn — Đồng ý?"). Cần trạng thái chờ và đẩy thông báo hai chiều; để PO quyết nếu muốn.
