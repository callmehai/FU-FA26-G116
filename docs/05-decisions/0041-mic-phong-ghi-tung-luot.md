# ADR-0041: Mic phòng ghi từng lượt, đẩy lên từng phần trong khi ghi, nối thành một bản ghi của cuộc họp

- **Trạng thái:** Đề xuất — SCRUM-29, PO 29/10/2026
- **Ngày:** 2026-10-29
- **Người quyết định:** Hải (PO)
- **Thay:** điểm 4 của [ADR-0040](0040-cach-ghi-am-theo-hinh-thuc-hop.md) ở hai chỗ: Tạm dừng / Ghi tiếp / Dừng và
  lưu thành một công tắc bật/tắt mic; tải cả tệp lên khi dừng thành đẩy từng phần trong khi ghi.
- **Theo:** AI-eCabinet `origin/main` 9554adec: `modules/recordings/internal/service.go` (Begin, AppendPart,
  Complete), `merge.go` (nối lượt vào bản ghi của cuộc họp), `clients/web/.../room/live/tape.ts`, `tapeStore.ts`,
  `roomMic.ts`, `useRoomMic.ts`

## Bối cảnh

Ở ADR-0040, thẻ ghi âm của Thư ký giữ cả bản ghi trong trình duyệt. Thư ký bấm "Dừng và lưu" thì mới tải cả tệp lên,
mỗi lần dừng thành một bản ghi riêng. Có ba vấn đề:

- Laptop sập hoặc tab bị đóng giữa buổi là mất cả buổi.
- Một cuộc họp có mấy bản ghi rời, mỗi bản một bản gỡ băng.
- Chưa dừng thì chưa có chữ để đọc, chưa có tiếng để nghe.

AI-eCabinet giải quyết bằng mic phòng:

- trình duyệt đẩy tiếng lên từng phần trong khi ghi;
- tắt mic là một lượt ghi, được nối vào bản ghi duy nhất của cuộc họp;
- cả bản được chuyển chữ lại sau mỗi lượt.

Bản mới nhất của eCab đã bỏ chữ chạy khi đang ghi (commit e10184b1: "drop the room mic's live captions from the UI").
Chữ có sau mỗi lần tắt mic.

## Quyết định

1. **Một công tắc.**
   - Bật mic phòng là mở một lượt ghi. Tắt mic là lưu lượt đó.
   - Không còn Tạm dừng và Dừng và lưu: tắt rồi bật lại đã là tạm dừng.
2. **Đẩy lên từng phần trong khi ghi.**
   - Trình duyệt ghi 16 kHz mono 16-bit PCM, mở đầu bằng header WAV chưa biết độ dài.
   - Tiếng được gửi thành từng phần đúng `Recordings:RoomPartBytes` byte (mặc định 320 000 byte, tức 10 giây); phần
     cuối ngắn hơn.
   - Vị trí của phần trong tệp suy ra từ số thứ tự. Gửi lại một phần sau khi mất câu trả lời chỉ ghi đè đúng chỗ đó.
   - Server từ chối phần để lại khoảng trống, phần sai kích thước, và phần 1 không phải WAV 16 kHz mono 16-bit
     (`MSG361`).
3. **Giữ trên máy cho tới khi server nhận.**
   - Phần chưa gửi nằm trong IndexedDB của trình duyệt, mỗi cuộc họp một lượt.
   - Mở lại trang thì thẻ mic hỏi: **Ghi tiếp** vào lượt đó, hay **Lưu phần đã ghi**.
   - Mỗi trình duyệt chỉ một tab ghi (Web Lock). Server cũng chỉ cho một lượt mở mỗi cuộc họp (index duy nhất,
     `MSG360`).
4. **Nối thành một bản ghi.**
   - Lượt đầu của cuộc họp thành bản ghi phòng.
   - Lượt sau được nối vào bản ghi đó: bỏ header của lượt, ghi lại độ dài thật vào header của cả tệp.
   - Mỗi lượt được ghi lại trong cột `recordings.takes`: bắt đầu ở đâu trong tệp, dài bao nhiêu, nói lúc nào. Nhờ đó bản
     gỡ băng hiện giờ thực, và có vạch "Lượt 2 · 10:05–10:32" giữa các lượt.
   - Bản ghi đầy giới hạn (`Recordings:MaxUploadMegabytes`) thì lượt tiếp theo mở bản ghi mới. Thẻ mic tự sang lượt
     mới khi gần chạm giới hạn.
5. **Chuyển chữ lại cả bản sau mỗi lượt**, như eCab.
   - Người nói được tách thống nhất qua các lượt, bản sau cùng là bản chuẩn.
   - Lần chạy đang dở trên tệp ngắn hơn bị bỏ kết quả, cả bản chờ chạy lại.
   - Trong lúc đó tab Bản ghi vẫn hiện chữ của bản trước, kèm dòng "Đang chuyển chữ lại cả bản ghi".
6. **Ai cũng thấy phòng đang được ghi.**
   - Lượt đang mở hiện ở tab Bản ghi ("Mic phòng đang ghi") và thành chip "Đang ghi âm" cạnh tên cuộc họp.
   - Không ai nghe được lượt đang mở trước khi tắt mic.
   - Hub báo `RecordingsChanged` khi mở hoặc lưu lượt, lưu hoặc xoá tệp, chuyển chữ xong.
7. **Lượt bị bỏ dở.**
   - Thư ký bất kỳ của cuộc họp bấm "Lưu lượt đó" cho lượt mở trên máy khác.
   - Lượt không nhận được phần nào trong `Recordings:IdleTakeHours` (mặc định 12 giờ) thì server tự lưu.
   - Cuộc họp kết thúc khi mic đang bật thì thẻ mic tự tắt và lưu. Rời trang khi đang ghi cũng lưu.
   - Lượt mic chưa thu được tiếng nào thì bị bỏ, không lưu gì (`MSG365`).

## Nghĩa vụ theo Luật 91/2025/QH15

- **Thông báo trước khi ghi** (Điều 32 khoản 2):
  - Thư ký xác nhận đã báo người dự trước lượt đầu tiên của cuộc họp. Server từ chối lượt đầu chưa xác nhận
    (`MSG291`).
  - Các lượt sau nối vào cùng bản ghi, cùng mục đích và mốc đồng ý, nên không hỏi lại.
- **Lưu trong thời gian cần thiết** (Điều 32 khoản 4):
  - Lượt và bản ghi phòng có cùng `RetainUntil` theo `Recordings:RetentionDays`.
  - Tệp cũ sau khi nối và tệp của lượt bị bỏ được xoá ngay khi giao dịch đã ghi xong.
- **Phần còn trên máy** là dữ liệu cá nhân nằm ngoài server. Trình duyệt xoá phần đó ngay khi server nhận phần cuối,
  hoặc khi server báo lượt đã được lưu ở nơi khác (`MSG362`).
- Mở lượt, lưu lượt và việc server tự lưu lượt bỏ dở đều ghi nhật ký kiểm toán: `recording.take_started` và
  `recording.take_saved`, actor rỗng khi server tự lưu.

## Hệ quả

- Laptop sập giữa buổi chỉ mất phần chưa kịp lưu vào IndexedDB, tối đa khoảng 5 giây.
- Một cuộc họp trực tiếp có một bản ghi phòng và một bản gỡ băng đọc theo giờ thực.
- Worker chuyển chữ chạy lại cả bản sau mỗi lượt. Một buổi 2 giờ có nhiều lần tắt mic thì tốn máy hơn. Bù lại, người
  nói được đánh số thống nhất, và đó cũng là cách eCab làm.
- Chữ Thư ký sửa và tên đã gán cho người nói bị thay khi bản mới chạy xong. Nên sửa sau khi tắt mic lần cuối.
- Đang ghi thì chưa có chữ chạy. Muốn có thì phải dựng nhận dạng trực tiếp trên worker, để sau.
- Tải tệp lên (`POST …/recordings`) chỉ còn nhận nguồn `Upload`: mic phòng không đi đường này nữa.
- Chưa làm: chế độ "Máy phòng họp" cho họp kết hợp (ADR-0040 điểm 5).
