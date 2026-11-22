# ADR-0077: Giữ phần Thư ký sửa tay khi chuyển chữ lại một bản ghi

- **Trạng thái:** Đề xuất — buổi họp thật 22/11/2026
- **Ngày:** 2026-11-22
- **Người quyết định:** chờ PO duyệt
- **Liên quan:** [ADR-0041](0041-mic-phong-ghi-tung-luot.md) (bản ghi mic phòng chuyển chữ lại cả tệp sau mỗi lượt),
  [ADR-0072](0072-hop-ket-hop-mot-cong-tac-mic-phong.md) (mic phòng tự lưu lượt mỗi 5 phút),
  [ADR-0074](0074-nguong-tach-nguoi-noi-va-voice-id-sau-buoi-thu-21-11.md) (máy tự gán tên),
  [ADR-0027](0027-voice-id-co-dong-y.md) (Voice ID có đồng ý)

## Bối cảnh

Chuyển chữ xong, `TranscriptionService` xoá mọi câu và mọi giọng của bản gỡ băng cũ rồi ghi lại từ kết quả mới. Bản ghi
mic phòng được chuyển chữ lại **cả tệp** sau mỗi lượt, và từ ADR-0072 mic phòng tự lưu lượt mỗi 5 phút. Vì vậy mọi việc
Thư ký sửa tay mất sau vài phút:

- tên gán cho một giọng (`PUT /api/recordings/{id}/speakers/{label}`),
- chữ sửa của một câu (`PUT /api/transcript-segments/{id}`),
- câu chuyển sang người khác (`PUT /api/transcript-segments/{id}/speaker`).

Buổi họp thật 22/11/2026: lúc 15:07 Thư ký gán tay giọng của người trình bày chính. Sau các lần chạy lại, giọng đó
(32 phút lời) lại thành "Người nói 1". Máy tách người nói đánh số giọng lại từ đầu mỗi lần, nên `SPEAKER_00` lần trước
có thể là `SPEAKER_02` lần này: không thể giữ tên theo nhãn.

## Quyết định

Trước khi xoá bản cũ, đọc lại câu và giọng cũ (không theo dõi entity), rồi đặt phần sửa tay lên kết quả mới. Mọi phép
khớp **chỉ dựa vào thời gian**, trên phần âm thanh cả hai lần cùng nghe; không đọc giọng nói, không dùng dữ liệu sinh
trắc. Luật nằm ở `Domain/Transcription/TranscriptCarry.cs`.

1. **Tên của giọng.** Với mỗi giọng mới, cộng thời gian nó trùng với từng giọng cũ trong vùng lần trước đã nghe (đến hết
   câu cũ cuối cùng). Giọng mới là giọng cũ `O` khi phần trùng với `O` **≥ 60%** lời của giọng mới trong vùng đó và
   **≥ 2 giây**. Câu Thư ký đã chuyển tay không tính cho giọng nào, vì nhãn của nó là ý Thư ký, không phải của máy.
   Nhiều giọng mới có thể cùng là một giọng cũ (máy tách một người thành hai): cả hai nhận tên.
2. **Chỉ mang tên do người gán** (`AssignedById` khác null), kể cả khi Thư ký đã bỏ gán. Tên đó thắng tên máy tự gán
   cho giọng ấy. Gợi ý của máy (`SuggestedUserId`) vẫn tính lại và vẫn hiện cho Thư ký. Tên máy tự gán thì tính lại từ
   giọng như trước, không mang sang.
3. **Rút đồng ý Voice ID.** Bất biến hiện có giữ nguyên: rút đồng ý xoá tên máy gán (`AssignedById` null), giữ tên Thư ký
   gán. Vì chỉ mang tên người gán, lần chạy sau không bao giờ đem lại tên máy đã gán cho người đã rút đồng ý. Người đã rút
   không còn giọng mẫu nên máy cũng không gán lại.
4. **Câu sửa chữ và câu chuyển người.** Câu cũ khớp câu mới khi cùng một lượt ghi (theo điểm giữa câu) và: hai đầu lệch
   nhau không quá **500 ms**, hoặc phần trùng **≥ 80%** câu dài hơn. Ghép một-một, cặp trùng nhiều nhất trước.
   - Câu sửa chữ: câu mới nhận chữ đã sửa, người sửa và giờ sửa. Mốc từng từ và độ chắc bị bỏ, như khi sửa tay.
   - Câu chuyển người: câu mới được chuyển lại. Nếu đích là một người do Thư ký gán tên thì câu vào giọng của người đó; chưa
     có giọng nào mang tên đó thì tạo giọng mới như khi chuyển tay. Nếu đích là giọng chưa có tên, hoặc tên do máy gán, thì
     câu vào giọng mới là giọng cũ đó, theo luật 1. Thời lượng đi theo câu. Câu được đánh dấu `SpeakerAssignedById`,
     `SpeakerAssignedAt` (cột mới) để lần sau còn chuyển lại.
   - Câu đã sửa hay đã chuyển luôn được giữ, kể cả khi ngắn: người đã đọc và thấy nó đáng sửa.
5. **Không khớp được thì không mất dấu.** Mỗi lần chạy có mang được gì hoặc có phần sửa không đặt được, nhật ký kiểm toán
   ghi một dòng `transcript.corrections_carried` (người thực hiện: hệ thống): danh sách tên đã mang, số câu sửa và số câu
   chuyển đã mang, và các câu sửa hay chuyển không khớp (giờ bắt đầu, kết thúc, ai sửa, lúc nào). **Không ghi chữ** vào
   nhật ký: nhật ký sống lâu hơn bản ghi, còn chữ là dữ liệu cá nhân phải xoá cùng bản ghi (Luật 91/2025/QH15).
6. Áp dụng cho **mọi** lần chạy lại: sau lượt mới của mic phòng và khi Thư ký bấm chuyển chữ lại.
7. **Xoá một lượt khi bản gỡ băng đang chờ chạy lại** giờ cũng cắt và dời câu cũ như khi bản đã xong, để lần chạy sau tìm
   thấy phần sửa tay đúng chỗ trên tệp ngắn hơn.

## Hệ quả

- Tên, chữ sửa và câu chuyển người còn lại qua các lượt 5 phút, miễn là máy còn cắt câu và tách giọng gần như lần trước.
  Phần trước của tệp không đổi nên thường là vậy.
- Khi máy cắt câu khác hẳn (tách một câu thành hai, gộp hai câu), chữ sửa của câu đó không đặt được. Nó không còn trên
  màn hình, chỉ còn dấu vết trong nhật ký. Muốn giữ cả chữ thì phải lưu bản sửa ở nơi riêng, ngoài bảng câu; chưa làm.
- Giọng mới chỉ trùng giọng cũ dưới 60% thì không nhận tên. Thư ký gán lại một lần.
- Thêm hai cột `transcript_segments.speaker_assigned_by_id`, `speaker_assigned_at` (migration
  `TranscriptSegmentSpeakerAssigned`). Câu đã chuyển trước bản này không có dấu, nên không được chuyển lại; không bù dữ
  liệu cũ.
