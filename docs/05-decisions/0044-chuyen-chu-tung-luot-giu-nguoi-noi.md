# ADR-0044: Mic phòng chỉ chuyển chữ lượt mới, nhận lại người nói của lượt trước

- **Trạng thái:** Đề xuất — SCRUM-29, SCRUM-31, SCRUM-35, PO 01/11/2026
- **Ngày:** 2026-11-01
- **Người quyết định:** Hải (PO)
- **Thay:** điểm "cả bản được chuyển chữ lại sau mỗi lượt" của [ADR-0041](0041-mic-phong-ghi-tung-luot.md)
- **Khác:** AI-eCabinet `origin/main` bba2097f. `modules/recordings/internal/merge.go` (`mergeLive`) xếp một job chuyển
  chữ cả tệp sau mỗi lượt. `platform/jobs/transcribe.go` (`addCurrentVersion`) ghi kết quả thành phiên bản bản gỡ băng
  mới. `modules/transcripts/internal/corrections.go` (`correct`) cho sửa khi `in_progress`, nhưng chữ sửa nằm lại ở
  phiên bản cũ.

## Bối cảnh

Theo ADR-0041, như AI-eCabinet, mỗi lần Thư ký tắt mic phòng thì lượt vừa ghi được nối vào bản ghi của cuộc họp, rồi
**cả bản** được chuyển chữ lại. Khánh nêu ba hệ quả ở BA Final Review (01/11/2026), Hải cũng gặp trên server test:

- Chữ đã sửa và tên đã gán của các lượt trước **mất** sau mỗi lượt mới, vì lần chạy mới thay toàn bộ câu và người nói.
  eCab cũng vậy, và không báo gì.
- Mỗi lượt làm lại cả cuộc họp từ đầu: lượt thứ mười chờ chuyển chữ cả mười lượt, máy 2 vCPU chậm dần.
- Trong lúc họp, Thư ký sửa chữ hay gán tên đều là công cốc.

Chỉ chuyển chữ lượt mới thì giải quyết được ba điều trên. Nhưng mỗi lần chạy, máy tách người nói từ đầu, không biết
"Người nói 2" của lượt trước. Cùng một người sẽ thành người mới ở mỗi lượt, và tên đã gán không theo sang.

## Quyết định

1. **Chỉ chuyển chữ lượt mới.**
   - Tắt mic: chỉ phần tiếng của lượt mới (và các lượt còn chờ trước nó) được gửi đi chuyển chữ
     (`transcripts.append_from_ms`). Câu của lượt mới thêm vào sau các câu đã có, câu cũ không bị đụng tới.
   - Áp dụng cả khi cuộc họp đã kết thúc (lượt bị server tự lưu sau `Recordings:IdleTakeHours`). Kết thúc họp không
     chuyển chữ lại gì.
   - Bản gỡ băng chưa từng xong (lỗi, chưa chạy) thì chuyển chữ cả bản như trước. Thư ký vẫn tự bấm chuyển chữ lại cả
     bản được, kèm cảnh báo mất chữ đã sửa (BR-360).
2. **Nhận lại người nói bằng đoạn mẫu, không lưu giọng.**
   - Trước tiếng của lượt mới, server ghép một đoạn mẫu của từng người nói đã có: tối đa 6 giây, lấy từ **cuối** các câu
     dài nhất của người đó. Lấy phần cuối vì mốc kết thúc của câu là chỗ lời dừng, còn mốc bắt đầu có thể lùi vào câu
     trước. Giữa các đoạn có 1,5 giây im lặng. Người nói tổng cộng dưới 1,5 giây thì bỏ qua, vì quá ít để phân biệt.
   - Worker tách người nói trên cả tệp ghép. Một giọng của lần chạy giữ ít nhất một nửa đoạn mẫu của "Người nói 2"
     (và nhiều hơn mọi người khác) thì chính là Người nói 2: câu của nó mang nhãn `SPEAKER_01`, nên tên Thư ký đã gán
     theo sang. Giọng khác là người mới, đánh số liền sau người cuối (`SPEAKER_03`…), không bỏ số.
   - Câu worker nghe trong phần mẫu bị bỏ. Tệp ghép là bản tạm, bị xoá ngay khi chạy xong hoặc lỗi.
   - Không vector giọng nói nào được lưu hay so ở bước này: worker tự tách người nói, server chỉ đọc kết quả. Đây
     không phải Voice ID. Voice ID vẫn chỉ gợi ý tên cho người đã đăng ký mẫu giọng và đồng ý (ADR-0027).
   - Ngưỡng một nửa là để chặt: không chắc thì cho số mới. Tách thừa thì Thư ký gán cùng một tên là gộp; gộp nhầm hai
     người thì khó thấy hơn.
3. **Sửa chữ và gán người nói ngay khi họp.**
   - Bản gỡ băng mic phòng sửa được cả khi cuộc họp `InProgress`, kể cả khi lượt mới đang được chuyển chữ
     (`appending: true`). FE hiện "Đang chuyển chữ lượt ghi mới…".
   - Trong khi đang chạy, FE ẩn nút xoá lượt và nút chuyển chữ lại. API vẫn cho xoá lượt khi mic không ghi.
4. **Gán người nói là Khách.**
   - Thư ký chọn được Khách được mời (lời mời chưa thu hồi) như một người của cuộc họp, với tên Khách tự khai, chưa
     khai thì tên lúc mời (`transcript_speakers.assigned_guest_id`, không đồng thời với `assigned_user_id`).
   - Khách không có giọng mẫu nên không bao giờ được gợi ý. Gán một lần thì lượt sau tự giữ tên, theo điểm 2.
5. **Không xoá bản ghi phòng khi mic đang ghi.**
   - Xoá bản ghi phòng hoặc xoá một lượt trong lúc mic phòng đang ghi lượt khác trả `409 MSG381`, vì lượt đang ghi sắp
     được nối vào chính tệp đó.
   - eCab cũng chặn "Giữ bản này" khi còn bản đang thu hoặc đang bóc băng (`keep.go`).
6. **Bản ghi đã xoá ghi người xoá và lý do.**
   - Thêm `recordings.deleted_by_id` và `deletion_reason`: `Erased` khi Thư ký xoá, `Expired` khi hết hạn lưu trữ
     (không có người xoá). Trước đây chỉ có `deleted_at`, người xoá chỉ nằm trong nhật ký kiểm toán.
   - Danh sách bản ghi trả `deletedByName`, `deletionReason`. FE vẫn không hiện bản đã xoá (PO 29/10/2026).
   - eCab xoá hẳn dòng bản ghi, không lưu lý do. TrustCabinet giữ dòng làm bằng chứng đã xoá theo yêu cầu.

## Hệ quả

- Chữ đã sửa và tên đã gán không còn bị lần chạy sau ghi đè.
- Mỗi lượt chỉ tốn thời gian chuyển chữ của chính nó, cộng tối đa 7,5 giây cho mỗi người đã nói (6 giây mẫu và 1,5 giây
  im lặng). Mười người nói thì thêm khoảng 75 giây âm thanh mỗi lượt. Chưa đo trên máy 2 vCPU của server test
  (SCRUM-128).
- Máy tách người nói vẫn có thể chia một người thành hai nhãn ở hai lượt, nhất là khi người đó nói rất ít ở lượt
  trước. Thư ký gán cùng một tên thì FE gộp lại. Hai người giọng rất giống nhau có thể bị gộp; Thư ký sửa bằng cách
  chuyển chữ lại cả bản.
- Chưa có số đo độ chính xác của việc nhận lại người nói trên giọng thật. Nên đo bằng bộ `workers/speech/eval/`.
- Hai migration: `RoomTranscriptAppendAndErasure` (`transcripts.append_from_ms`, `recordings.deleted_by_id`,
  `recordings.deletion_reason`) và `TranscriptSpeakerGuest` (`transcript_speakers.assigned_guest_id`). Dữ liệu cũ
  không cần sửa: `NULL` nghĩa là chuyển chữ cả bản, và bản đã xoá trước đây không có lý do.
- BA cập nhật UC-44a–e, UC-45c, UC-46a/b, BR-345/347/348/360 ở Final Review.

## Nghĩa vụ theo Luật 91/2025/QH15

Không đổi so với ADR-0041: mục đích, thời hạn lưu và thông báo trước lượt đầu giữ nguyên. Đoạn mẫu là tiếng của chính
bản ghi đó, gửi tới worker trong mạng nội bộ như tiếng của lượt mới. Không tạo ra hay lưu dữ liệu sinh trắc nào.
Tên Khách được gán là dữ liệu cá nhân của Khách trong bản ghi cuộc họp, bị xoá cùng bản ghi.
