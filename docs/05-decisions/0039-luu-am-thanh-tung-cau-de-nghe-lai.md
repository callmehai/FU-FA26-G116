# ADR-0039: Lưu âm thanh từng câu ghi theo micro để nghe lại, như AI-eCabinet

- **Trạng thái:** Đề xuất — SCRUM-109, PO 29/10/2026; phần "Đồng ý" thay thế bởi ADR-0040
- **Ngày:** 2026-10-29
- **Người quyết định:** Hải (PO)
- **Thay:** điểm 3 "Không lưu âm thanh" của [ADR-0029](0029-ghi-am-theo-micro-tung-nguoi.md), và câu "không lưu âm
  thanh nên không chạy lại được" của [ADR-0038](0038-danh-dau-tieng-lot-giua-cac-micro.md)
- **Theo:** AI-eCabinet `origin/main` 9554adec: `clients/web/.../detail/timeline/MeetingTimeline.tsx` (player chung,
  dạng sóng, nghe lại từng câu), `SpeechWidget.tsx` ("Ghi âm của tôi")

## Bối cảnh

ADR-0029 bỏ âm thanh của từng câu sau khi chuyển thành chữ, đổi khả năng nghe lại lấy ít dữ liệu cá nhân hơn. Khi
dựng lại tab Bản ghi theo giao diện eCab, PO chọn có đủ như eCab: Thư ký nghe lại câu để sửa chữ máy nghe sai, mọi
người nghe lại cả cuộc họp trên một player chung có dạng sóng. Không có âm thanh thì không làm được, và không đối
chiếu được chữ với tiếng khi duyệt biên bản.

## Quyết định

1. **Giữ âm thanh của câu có chữ.** Câu toàn im lặng vẫn không lưu gì.
   - Tệp nằm cùng kho với bản ghi phòng (`Recordings:StorageRoot`), tên là một id mới, không theo gì người gửi chọn.
   - Dòng `speech_clips` giữ `audio_path`, `audio_content_type`, và `peaks`: độ to mỗi 100 ms, 1 byte, để vẽ dạng sóng
     mà không tải âm thanh.
   - Ổ đĩa lỗi thì câu vẫn được lưu chữ, không có âm thanh. Lưu dòng lỗi thì xoá tệp vừa ghi, không để tệp mồ côi.
2. **Ai nghe được:** đúng người đọc được chữ.
   - Người trong danh sách cuộc họp nghe được mọi câu.
   - Câu đã bị loại khỏi bản ghi thì chỉ Chủ tọa và Thư ký nghe, như chữ của nó.
   - Không ai tải tệp của một câu về. Muốn có bản để mang đi thì dùng `.txt` (ADR-0038).
3. **Nghe qua link ký có hạn**, như bản ghi phòng.
   - `<audio>` không gửi được token, nên `GET /api/speech-clips/{id}/playback` kiểm quyền rồi trả link
     `/api/speech-clips/{id}/audio?expires=…&signature=…`, hết hạn sau `Recordings:PlaybackUrlMinutes`.
   - Khoá ký sinh từ khoá token với nhãn riêng (`trustcabinet/speech-clip-audio/v1`), nên link bản ghi phòng không mở
     được câu, và ngược lại.
4. **Xoá cùng chữ:**
   - người nói rút lại câu của mình, hoặc Thư ký xoá câu: xoá tệp sau khi xoá dòng;
   - hết hạn lưu trữ (`Recordings:RetentionDays`, 180 ngày): xoá dòng rồi xoá tệp.

## Nghĩa vụ theo Luật 91/2025/QH15

- **Đồng ý:** chỉ chính người nói bật micro của mình (ADR-0029, điểm 5), không ai bật hộ, kể cả Chủ tọa. Ô micro ghi rõ
  trước khi bật rằng chữ và âm thanh từng câu được lưu để nghe lại, ai nghe được, và xoá sau bao lâu. Tắt micro là
  ngừng thu ngay.
- **Mục đích:** soạn và đối chiếu biên bản cuộc họp. Không dùng âm thanh này để nhận ra giọng: không sinh vector giọng
  nào từ nó (Voice ID có đồng ý riêng, ADR-0027).
- **Xoá:** người nói xoá được câu của mình bất cứ lúc nào, và âm thanh đi cùng. Hết hạn thì tự xoá.
- **Tối thiểu:** chỉ giữ câu có chữ, đúng như trình duyệt cắt; không giữ tiếng im lặng giữa các câu.

## Hệ quả

- Thêm một nguồn dữ liệu cá nhân là tiếng nói của từng người. Nó theo đúng quy tắc của bản ghi phòng: ai nghe, hạn
  lưu, xoá.
- Dung lượng: WAV 16 kHz mono khoảng 32 KB mỗi giây nói. Một cuộc họp 2 giờ với khoảng 1 giờ có người nói tốn
  khoảng 115 MB trên đĩa.
- Có âm thanh rồi thì sau này chạy lại chuyển chữ cho một câu được (eCab có). Chưa làm trong thay đổi này.
- Câu đã lưu trước thay đổi này không có âm thanh: dòng vẫn hiện, không có nút nghe.
