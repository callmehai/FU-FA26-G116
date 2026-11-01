# ADR-0044: Bản gỡ băng mic phòng là bản sơ bộ khi đang họp, chuyển chữ lại cả bản khi kết thúc

- **Trạng thái:** Đề xuất — SCRUM-29, SCRUM-31, PO 01/11/2026
- **Ngày:** 2026-11-01
- **Người quyết định:** Hải (PO)
- **Thay:** điểm "cả bản được chuyển chữ lại sau mỗi lượt" của [ADR-0041](0041-mic-phong-ghi-tung-luot.md)
- **Khác:** AI-eCabinet `origin/main` bba2097f, `modules/recordings/internal/merge.go` (`mergeLive` xếp một job
  chuyển chữ cả tệp sau mỗi lượt) và `modules/transcripts/internal/corrections.go` (`correct` cho sửa khi
  `in_progress`)

## Bối cảnh

Theo ADR-0041, như AI-eCabinet, mỗi lần Thư ký tắt mic phòng thì lượt vừa ghi được nối vào bản ghi của cuộc họp, rồi
**cả bản** được chuyển chữ lại. Ba hệ quả, Khánh nêu ở BA Final Review (01/11/2026) và Hải gặp trên server test:

- Chữ đã sửa và tên đã gán của các lượt trước **mất** sau mỗi lượt mới: lần chạy mới thay toàn bộ câu và người nói.
  eCab cũng vậy: tên người nói gắn với phiên bản bản gỡ băng, phiên bản mới không mang theo.
- Mỗi lượt làm lại cả cuộc họp từ đầu: lượt thứ mười chờ chuyển chữ cả mười lượt, máy 2 vCPU chậm dần.
- Trong lúc họp, Thư ký sửa chữ hay gán tên đều là công cốc.

## Quyết định

1. **Đang họp: chỉ đẩy lượt mới vào.**
   - Tắt mic khi cuộc họp `InProgress`: chỉ phần âm thanh của lượt mới (và các lượt còn chờ trước nó) được cắt ra
     thành một tệp WAV riêng để chuyển chữ (`transcripts.append_from_ms`).
   - Câu của lượt mới thêm vào sau các câu đã có. Nhãn người nói đánh số tiếp (`SPEAKER_02`… sau `SPEAKER_01`), vì
     mỗi lần chạy tách giọng riêng.
   - Voice ID vẫn gợi ý tên cho từng giọng mới. Hai giọng được gợi ý cùng một người thì gom lại như trước.
2. **Đang họp: bản sơ bộ, không sửa.**
   - Bản gỡ băng mic phòng của cuộc họp `InProgress` có `provisional: true`, `canEdit: false`.
   - Sửa chữ hoặc gán người nói trả `409 MSG382`.
   - FE hiện nhãn "Bản sơ bộ" và một dòng: "Họp xong máy chuyển chữ lại cả bản, khi đó mới sửa chữ và gán người nói."
   - FE cũng ẩn nút xoá lượt trên bản sơ bộ (PO 01/11/2026), vì nút đi theo `canEdit`. API vẫn cho xoá lượt khi
     mic không ghi; Thư ký xoá lượt sau khi họp xong.
3. **Kết thúc họp: chuyển chữ lại cả bản.**
   - Chủ toạ kết thúc họp: trong cùng transaction, bản gỡ băng của bản ghi phòng có từ hai lượt trở lên trở về
     `Pending`, chạy cả tệp. Tách người nói trên toàn bộ cuộc họp, nên một người nói ở nhiều lượt thành một giọng.
   - Bản ghi một lượt đã được chuyển chữ cả tệp, không chạy lại.
   - Từ đó Thư ký sửa chữ, gán tên như với tệp tải lên.
4. **Không xoá bản ghi phòng khi mic đang ghi.**
   - Xoá bản ghi phòng hoặc xoá một lượt trong lúc mic phòng đang ghi lượt khác trả `409 MSG381`. Lượt đang ghi sắp
     được nối vào chính tệp đó.
   - eCab cũng chặn "Giữ bản này" khi còn bản đang thu hoặc đang bóc băng (`keep.go`).
5. **Bản ghi đã xoá ghi người xoá và lý do.**
   - `recordings.deleted_by_id` và `deletion_reason` (`Erased`: Thư ký xoá; `Expired`: hết hạn lưu trữ, không có người
     xoá). Trước đây chỉ có `deleted_at`, người xoá chỉ nằm trong nhật ký kiểm toán.
   - Danh sách bản ghi trả `deletedByName`, `deletionReason`. FE vẫn không hiện bản đã xoá (PO 29/10/2026).
   - eCab xoá hẳn dòng bản ghi, không lưu lý do. TrustCabinet giữ dòng làm bằng chứng đã xoá theo yêu cầu.

## Hệ quả

- Chữ Thư ký sửa và tên đã gán không còn bị lần chạy sau ghi đè, vì không ai sửa trước khi bản cuối có.
- Mỗi lượt trong lúc họp chỉ tốn thời gian chuyển chữ của chính nó.
- Bản sơ bộ tách giọng theo từng lượt, nên một người có thể thành "Người nói 1" ở lượt đầu và "Người nói 3" ở lượt
  sau. Bản cuối sau khi kết thúc họp gom lại.
- Kết thúc họp làm máy chuyển chữ bận lâu hơn một lượt, vì chạy cả cuộc họp. Chưa đo trên máy 2 vCPU của server test
  (SCRUM-128); FE ước lượng bằng ¼ độ dài bản ghi.
- Lượt bị server tự lưu sau khi cuộc họp đã kết thúc (`Recordings:IdleTakeHours`) làm cả bản chuyển chữ lại, như cũ.
- Một migration: `transcripts.append_from_ms`, `recordings.deleted_by_id`, `recordings.deletion_reason`. Dữ liệu cũ
  không cần sửa: `NULL` nghĩa là chuyển chữ cả bản, và bản đã xoá trước đây không có lý do.
- BA cập nhật UC-44a–e, UC-45c, BR-345/347/348/360 ở Final Review.

## Nghĩa vụ theo Luật 91/2025/QH15

Không đổi so với ADR-0041: mục đích, thời hạn lưu và thông báo trước lượt đầu giữ nguyên. Tệp WAV cắt ra để chuyển
chữ là bản tạm trên server, bị xoá ngay khi chuyển chữ xong hoặc lỗi.
