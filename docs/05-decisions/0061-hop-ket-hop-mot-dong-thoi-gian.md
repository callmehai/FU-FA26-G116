# ADR-0061: Họp kết hợp: phòng ghi trực tiếp bằng mic phòng, người trực tuyến ghi theo micro của mình, một dòng thời gian, một bản ghi tổng hợp

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
5. **Bản ghi tổng hợp: N mic trực tuyến + 1 mic phòng thành một tệp nghe lại** (PO 13/11/2026). Họp kết hợp kết thúc rồi,
   server tự trộn bản ghi mic phòng (đặt từng lượt theo giờ bắt đầu thật) với mọi câu theo micro của người trực tuyến (đặt
   theo giờ nói) thành **một tệp WAV**, nằm trong danh sách Bản ghi với nhãn **Tổng hợp**: nghe bằng thanh phát như mọi bản
   ghi, Chủ toạ và Thư ký tải về được (như ADR-0040, không cho họp Mật).
   - Hai người nói cùng lúc thì nghe cùng lúc: chấp nhận chồng tiếng. Lời người trực tuyến nghe hơi vọng, vì mic phòng cũng
     thu tiếng loa phát lời họ.
   - Câu đã bị loại khỏi biên bản không vào bản tổng hợp.
   - Dựng khi nguồn đã ổn: không còn lượt mic phòng đang ghi, và không có gì mới trong 1 phút. Nguồn đổi về sau (câu gửi
     muộn, câu bị loại, lượt bị cắt) thì tự dựng lại, giữ nguyên dòng bản ghi. Thư ký xoá thì không dựng lại.
   - Chỉ để nghe: không gỡ băng lại (`POST …/transcribe` trả `409 MSG479`), vì lời đã có từ mic phòng và mic từng người.
   - Trộn ngay trong backend (mọi nguồn đều là PCM 16 kHz mono), theo khối 10 giây: không cần worker nhận dạng giọng, không
     nạp cả buổi họp vào bộ nhớ. Khoảng lặng giữa các lượt giữ nguyên, để vị trí trên thanh phát khớp giờ họp.

## Hệ quả

- Bản dự thảo biên bản họp kết hợp đọc đúng thứ tự và không lặp lời người trực tuyến.
- Mất mic phòng giữa buổi thì không bù bằng tệp ghi ngoài được. Bù lại, ADR-0041 đã giữ phần chưa gửi trên máy và ghi tiếp được.
- Tab Bản ghi vẫn hiện bản gỡ băng của mic phòng nguyên vẹn, kể cả đoạn vọng: Thư ký xem được đủ, chỉ bản dự thảo bỏ.
- Nhận đúng tên người ngồi ở phòng dựa vào Voice ID, tốt nhất là giọng mẫu Thư ký thu bằng chính mic phòng (ADR-0060).
- Bản tổng hợp tốn chỗ như bản ghi mic phòng (khoảng 115 MB mỗi giờ) và tự xoá cùng thời hạn lưu trữ.
- Họp trực tiếp chỉ có một nguồn (mic phòng, hoặc tệp tải lên), họp trực tuyến đã nghe được cả buổi trên một thanh theo micro
  từng người (ADR-0039), nên không cần bản tổng hợp.
