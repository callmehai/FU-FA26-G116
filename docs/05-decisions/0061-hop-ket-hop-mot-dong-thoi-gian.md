# ADR-0061: Họp kết hợp: phòng ghi trực tiếp bằng mic phòng, người trực tuyến ghi theo micro của mình, một dòng thời gian

- **Trạng thái:** Đề xuất — PO (Hải) 12/11/2026
- **Ngày:** 2026-11-12
- **Người quyết định:** Hải (PO), theo note của BA (Khánh) ở 📌・note-chung ngày 12/11/2026
- **Thay:** điểm 5 của [ADR-0040](0040-cach-ghi-am-theo-hinh-thuc-hop.md) (chế độ "Máy phòng họp" làm sau): không làm chế
  độ đó.
- **Liên quan:** [ADR-0041](0041-mic-phong-ghi-tung-luot.md) (mic phòng ghi từng lượt, biết giờ thực),
  [ADR-0038](0038-danh-dau-tieng-lot-giua-cac-micro.md) (tiếng lọt giữa các mic),
  [ADR-0060](0060-thu-ky-thu-voice-id-ho.md) (Thư ký thu giọng mẫu hộ bằng micro phòng)

## Bối cảnh

Họp kết hợp có hai nhóm người: ngồi ở phòng họp và vào từ xa. ADR-0040 đã cho mỗi nhóm một đường ghi: người trực tuyến ghi
theo micro của mình trong phòng gọi, phòng họp do Thư ký ghi bằng mic phòng. Còn hai chỗ hở:

- Thư ký có thể **tải tệp ghi âm lên** thay cho mic phòng. Tệp không biết từng câu được nói lúc mấy giờ, nên lời trong tệp
  không xếp được xen với câu của người trực tuyến: bản dự thảo AI đọc sai thứ tự ai nói trước, ai nói sau.
- Phòng họp nghe người trực tuyến qua **loa**, nên mic phòng ghi lại lời họ lần thứ hai. Bản dự thảo đọc cùng một câu hai lần,
  lần sau dưới tên "Người nói N" hoặc tên đoán sai.

PO chốt (12/11/2026): người họp trực tiếp thì Thư ký **thu trực tiếp** bằng micro chuyên dụng của phòng (không tải tệp lên);
người họp trực tuyến nói qua hệ thống, ghi theo micro của chính mình, nên biết chắc ai nói lúc nào.

## Quyết định

1. **Họp kết hợp không nhận tệp tải lên.** `POST /api/meetings/{id}/recordings` trả `409 MSG478`; `canUpload` luôn `false`.
   Phòng họp chỉ ghi bằng mic phòng (ADR-0041): mỗi lượt biết giờ bắt đầu, nên mỗi đoạn gỡ băng có giờ thực.
   Họp trực tiếp vẫn tải tệp lên được như cũ.
2. **Một dòng thời gian.** Câu theo micro của người trực tuyến (tên theo tài khoản) và đoạn gỡ băng của mic phòng (tên theo
   Voice ID hoặc Thư ký gán) được xếp theo giờ thực vào cùng một dòng, như bản dự thảo AI vẫn đọc.
3. **Bỏ tiếng vọng.** Đoạn mic phòng nói **cùng lúc** (lệch tối đa 2 giây) với một câu của người trực tuyến và có **từ 60% số
   từ** trùng câu đó là tiếng loa phòng phát lại: bỏ khỏi dòng thời gian của bản dự thảo. Ai ngồi ở phòng nói chen cùng lúc
   thì giữ, vì lời khác nhau. So từ bỏ dấu, chữ hoa và dấu câu, vì hai lần gỡ băng cùng một giọng hiếm khi khớp từng dấu.
4. Không làm chế độ "Máy phòng họp" của ADR-0040 điểm 5: máy Thư ký ghi mic phòng là đủ.

## Hệ quả

- Bản dự thảo biên bản họp kết hợp đọc đúng thứ tự và không lặp lời người trực tuyến.
- Mất mic phòng giữa buổi thì không bù bằng tệp ghi ngoài được. Bù lại, ADR-0041 đã giữ phần chưa gửi trên máy và ghi tiếp được.
- Tab Bản ghi vẫn hiện bản gỡ băng của mic phòng nguyên vẹn, kể cả đoạn vọng: Thư ký xem được đủ, chỉ bản dự thảo bỏ.
- Nhận đúng tên người ngồi ở phòng dựa vào Voice ID, tốt nhất là giọng mẫu Thư ký thu bằng chính mic phòng (ADR-0060).
