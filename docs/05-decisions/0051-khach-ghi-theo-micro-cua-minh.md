# ADR-0051: Khách trong phòng họp trực tuyến cũng được ghi theo micro của mình

- **Trạng thái:** Đề xuất — SCRUM-129, PO 01/11/2026
- **Ngày:** 2026-11-03
- **Người quyết định:** Hải (PO)
- **Bổ sung cho:** [ADR-0040](0040-cach-ghi-am-theo-hinh-thuc-hop.md) mục 3 (họp trực tuyến ghi theo micro từng
  người), trước đây chỉ áp dụng cho người có tài khoản.

## Bối cảnh

ADR-0040 cho mỗi người họp trực tuyến tự ghi micro của mình, gửi từng câu lên, tên người nói là chủ micro. Khi đó chỉ
người có tài khoản gửi được câu: `speech_clips.speaker_id` là khoá ngoại tới `users`, endpoint chỉ nhận token nội bộ.
Khách (UC-04, SCRUM-116) vào phòng họp bằng phiên Khách và vé LiveKit (SCRUM-59, SCRUM-127) nên người khác nghe được
họ, nhưng lời họ không có trong bản ghi. Biên bản thiếu phần phát biểu của Khách.

Khách không có dòng `users` hay `meeting_participants` (ADR-0016, SCRUM-116), và máy so giọng không bao giờ tự gán
một Khách (ADR-0045). Cần một cách gắn câu cho Khách mà không biến Khách thành tài khoản và không đoán bằng giọng.

## Quyết định

1. **Một câu thuộc đúng một người nói:** hoặc một thành viên (`speaker_id`), hoặc một lời mời Khách
   (`guest_invitation_id`, khoá ngoại tới `meeting_guest_invitations`). Ràng buộc `ck_speech_clips_one_speaker` buộc
   có đúng một trong hai. Câu gắn với **lời mời**, không với phiên: Khách xác minh lại (phiên mới) vẫn là cùng người
   nói, và câu còn sau khi lời mời bị thu hồi.
2. **Tên hiển thị** của câu Khách là tên Khách tự khai, chưa có thì tên được mời, như bản gỡ băng đã làm. Câu có cờ
   `speakerIsGuest`; màn bản ghi hiện nhãn "Khách" cạnh tên, tệp `.txt` ghi "Tên (Khách)".
3. **Ai gửi được:** token Khách gửi câu vào đúng cuộc họp của token, khi phiên còn hiệu lực, lời mời chưa thu hồi,
   Khách đã khai tên (như mọi quyền khác của Khách), và cuộc họp đang `InProgress` theo hình thức `Online` hoặc
   `Hybrid`. Server đọc lại phiên, lời mời và cuộc họp ở mỗi câu.
4. **Không ghi trước khi báo:** server chỉ nhận câu của phiên Khách đã có dòng `call.guest_admitted` (bằng chứng xác
   nhận thông báo ghi âm ở màn chờ, SCRUM-59). Chưa có thì `400 MSG380`, như khi xin vào phòng mà chưa xác nhận.
   Trình duyệt chỉ bật bộ cắt câu trong phòng, tức sau màn chờ, và tắt mic là không gửi gì, như thành viên.
5. **Khách chỉ thấy câu của mình.** `GET …/speech-clips` bằng token Khách trả các câu của lời mời đó, không có dấu
   lọt tiếng (dấu đó nêu tên và câu của người khác), không sửa, loại hay xuất được. Khách nghe lại và xoá câu của mình
   khi phiên còn; câu của người khác trả `404`. Sửa chữ, loại câu, xuất `.txt`, bản ghi phòng và bản gỡ băng vẫn chỉ
   cho thành viên (`403` với token Khách).
6. **Xoá:** câu Khách xoá theo cùng hạn với bản ghi (`Recordings:RetentionDays`, `RecordingRetentionWorker`). Thu hồi
   lời mời không xoá câu đã nói. Hết phiên (cuộc họp kết thúc, thu hồi, đăng xuất) Khách không tự xoá được nữa; Thư ký
   xoá theo yêu cầu của Khách, như Thư ký vẫn xoá được mọi câu.

## Nghĩa vụ theo Luật 91/2025/QH15

- **Căn cứ và thông báo:** như ADR-0040, âm thanh hội nghị được ghi khi đã báo trước (Điều 32 khoản 1 điểm b, khoản
  2). Khách là người ngoài cơ quan, nên thông báo phải được **xác nhận** (ô đánh dấu ở màn chờ) và lưu làm bằng chứng
  kèm IP trước khi có vé vào phòng; server từ chối câu của phiên không có bằng chứng đó.
- **Lưu trong thời gian cần thiết, xoá an toàn** (Điều 32 khoản 4, Điều 14): chữ và âm thanh câu của Khách xoá cùng
  hạn với bản ghi. Khách rút lại được câu của mình trong lúc họp; sau đó yêu cầu Thư ký xoá.
- **Không sinh trắc học:** câu được gắn theo người gửi, không so giọng; không tạo vector giọng nào cho Khách.
  Voice ID vẫn chỉ cho thành viên đã đồng ý riêng (ADR-0027).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Gắn câu với `guest_sessions` | Đúng người đang gửi | Khách xác minh lại có phiên mới, thành hai "người nói"; phiên là bằng chứng truy cập, không phải danh tính | Lời mời mới là danh tính của Khách trong cuộc họp |
| Kiểm Khách có mặt trong phòng (`call_participants`) | Chặt hơn | Thành viên không bị kiểm như thế; webhook LiveKit đến trễ thì câu đầu bị từ chối, trình duyệt bỏ câu | Giữ cùng luật với thành viên; bằng chứng thông báo là điều kiện thêm cho Khách |
| Nhận câu muộn đến 7 ngày như thành viên | Không mất câu khi mất mạng lúc cuối | Phiên Khách hết khi cuộc họp kết thúc, không còn ai để xác thực | Khách chỉ gửi khi cuộc họp đang diễn ra |

## Hệ quả

- Biên bản có lời Khách dưới tên Khách, không phải gán tay.
- `speech_clips.speaker_id` thành nullable; mọi chỗ đọc tên người nói dùng tên thành viên hoặc tên Khách.
- Câu Khách giữ trên máy chưa gửi được thì mất khi cuộc họp kết thúc (server trả `404`, trình duyệt bỏ câu). Trình
  duyệt ghi chủ của từng câu đang chờ (`user:{id}` / `guest:{invitationId}`), nên một máy dùng chung không gửi câu của
  người này bằng phiên của người kia.
- Khách không nhận sự kiện hub `speechChanged`; trang của Khách đọc lại danh sách của mình sau mỗi câu đã lưu.
