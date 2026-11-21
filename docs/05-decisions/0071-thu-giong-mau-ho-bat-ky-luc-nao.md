# ADR-0071: Người có quyền "Thu giọng mẫu hộ" thu bất kỳ lúc nào, không cần cuộc họp — thông báo Voice ID 1.4

- **Trạng thái:** Đề xuất — PO 21/11/2026
- **Ngày:** 2026-11-21
- **Người quyết định:** PO
- **Bổ sung:** [ADR-0060](0060-thu-ky-thu-voice-id-ho.md) (Thư ký thu Voice ID hộ). Không thay quyết định nào của ADR-0060:
  đường thu trong cuộc họp giữ nguyên.
- **Liên quan:** [ADR-0027](0027-voice-id-co-dong-y.md), [ADR-0042](0042-luu-ban-ghi-giong-mau-de-nghe-lai.md),
  [ADR-0049](0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md)

## Bối cảnh

Theo ADR-0060, giọng mẫu thu hộ chỉ thu được trong một cuộc họp trực tiếp hoặc kết hợp, đang Đã công bố hoặc Đang họp, và
người thu phải là Thư ký của chính cuộc họp đó. Màn *Quản trị → Người dùng* có nút micro cho từng người, nhưng người có quyền
vẫn phải chọn một cuộc họp mình làm Thư ký có người đó. Người cần được thu mà chưa có cuộc họp nào như vậy thì không thu được,
dù họ đã đồng ý Voice ID.

PO (21/11/2026): người đã có quyền thu hộ thì thu được bất kỳ lúc nào, kể cả khi không có cuộc họp nào mà họ là Thư ký. Chỉ
chặn khi người được thu chưa đồng ý điều khoản ghi giọng.

## Quyết định

1. **Đường thu thứ hai, theo người.** Người giữ quyền `VoiceCollector` thu, nghe lại và xoá giọng mẫu thu hộ của **một người**
   ở `/api/users/{userId}/voice-collection`, không cần cuộc họp. Thiếu quyền: `403`, vì đây là đường dẫn cố định. Người được
   thu không tồn tại hoặc đã bị vô hiệu hoá: `404`.
2. **Thông báo Voice ID lên 1.4.** Câu mục đích thêm: giọng mẫu có thể do *người được quản trị viên giao quyền "Thu giọng mẫu
   hộ" thu bất kỳ lúc nào, không cần cuộc họp*. Đường thu theo người chỉ nhận người đã đồng ý **1.4 trở lên**. Người đã đồng ý
   1.3 vẫn được Thư ký thu trong cuộc họp như cũ. Họ thấy lời mời "Đồng ý điều khoản mới" một lần bấm, như các lần lên phiên
   bản trước. Trước khi họ đồng ý, đường thu theo người trả `409 MSG475`.
3. **Thấy mọi mẫu thu hộ của người đó.** Người có quyền thấy, nghe lại và xoá **mọi** mẫu thu hộ của người đó: mẫu thu trong
   cuộc họp và mẫu thu ngoài cuộc họp, của mình hay của người khác thu, kèm tên người thu. Mẫu người đó tự thu thì không thấy.
   Mỗi lần nghe ghi `voice.sample_played`.
4. **Giới hạn, mã hoá, thông báo, nhật ký như ADR-0060.** Tối đa 50 mẫu thu hộ cho một người, mỗi người thu tối đa 5. Mẫu mã
   hoá gắn với người được thu, nằm dưới đồng ý Voice ID của họ. Mỗi lần thu, chủ mẫu nhận thông báo `VoiceSampleCollected`.
   Thu và xoá ghi nhật ký với `scope: "proxy"`, `meetingId: null`.
5. **Dữ liệu.** Mẫu thu ngoài cuộc họp có `collected_by_id` mà không có `meeting_id`. Ràng buộc
   `ck_voice_samples_collected_in_meeting` (hai cột cùng có hoặc cùng không) đổi thành `ck_voice_samples_meeting_has_collector`
   (có `meeting_id` thì phải có `collected_by_id`), migration `VoiceIdAnytimeCollection`. Chủ mẫu thấy mẫu này ở tab
   *Thu hộ* với `meetingId`/`meetingTitle` là `null`.

## Nghĩa vụ theo Luật 91/2025/QH15

- **Đồng ý đúng mục đích đã đọc** (Điều 9): việc thu ngoài cuộc họp nằm trong câu thông báo 1.4. Người đồng ý bản cũ không bị
  thu theo cách mới cho tới khi tự đồng ý bản mới. Không ai đồng ý thay được.
- **Bảo mật dữ liệu nhạy cảm** (Điều 31 khoản 4): hai lớp chặn còn lại là quyền do quản trị viên cấp, kiểm lại trong cơ sở dữ
  liệu ở mỗi lần gọi, và đồng ý của chính chủ. Lớp "đúng Thư ký của cuộc họp" của ADR-0060 không áp dụng cho đường này. Bù lại:
  mỗi lần thu, chủ mẫu được báo; mỗi lần nghe, nhật ký ghi lại.
- **Rút và xoá**: không đổi. Rút đồng ý hoặc bị vô hiệu hoá tài khoản thì xoá ngay cả hai bộ mẫu, gồm cả mẫu thu ngoài cuộc họp.

## Hệ quả

- Quản trị viên nên cấp `VoiceCollector` cho ít vai trò hơn: quyền này giờ chạm được giọng mẫu thu hộ của mọi người dùng đã
  đồng ý, không còn gói trong cuộc họp.
- Không thêm mã thông báo: dùng lại `MSG473`–`MSG476` cho đúng các tình huống đó trên đường thu theo người.
- Người đã đồng ý 1.3 phải đồng ý lại một lần trước khi được thu ngoài cuộc họp.
