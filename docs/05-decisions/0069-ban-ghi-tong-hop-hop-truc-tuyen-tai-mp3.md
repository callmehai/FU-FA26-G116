# ADR-0069: Bản ghi tổng hợp cho họp trực tuyến, tải bản ghi dạng MP3

- **Trạng thái:** Đề xuất — PO (Hải) 20/11/2026
- **Ngày:** 2026-11-20
- **Người quyết định:** Hải (PO)
- **Liên quan:** [ADR-0040](0040-cach-ghi-am-theo-hinh-thuc-hop.md) (cách ghi theo hình thức họp),
  [ADR-0061](0061-hop-ket-hop-mot-dong-thoi-gian.md) điểm 5 (bản ghi tổng hợp của họp kết hợp),
  [ADR-0067](0067-don-du-lieu-dinh-ky.md) (bản tổng hợp đi theo nguồn), SCRUM-29

## Bối cảnh

Báo lỗi ngày 20/11/2026: "chưa có nút tải mp3 ghi âm xuống, mới chỉ có text". Lúc đó:

- **Họp trực tuyến** chỉ lưu từng câu theo micro của từng người. Không có tệp âm thanh của cả cuộc họp, nên Chủ toạ và Thư
  ký chỉ tải được lời nói dạng `.txt`.
- **Họp kết hợp** có bản ghi tổng hợp do server trộn sau khi họp xong, tải được nhưng là WAV 16 kHz: khoảng 115 MB mỗi giờ.
- Bản ghi mic phòng (WAV) và tệp tải lên tải về đúng tệp gốc.

## Quyết định

1. **Họp trực tuyến cũng có bản ghi tổng hợp.** Sau khi họp trực tuyến kết thúc, không Mật, server trộn mọi câu có tiếng
   (trừ câu đã loại khỏi biên bản) theo giờ thật, đúng cách đã làm cho họp kết hợp, chỉ là không có nguồn mic phòng. Bản
   ghi cũ của phòng (nếu có, từ trước ADR-0040) không được trộn vào. Bản này dựng khi có ít nhất một câu; mọi câu đã xoá
   hoặc đã loại thì bản tổng hợp bị xoá theo. Mọi quy tắc khác giữ nguyên: dựng lại khi nguồn đổi trong 8 ngày, Thư ký xoá
   thì không dựng lại, tiếng đã xoá không sống trong bản tổng hợp (ADR-0067), `retainUntil` là ngày sớm nhất của nguồn.
   Không có mic phòng để lấy đồng ý, mục đích và người tạo, nên bản của họp trực tuyến lấy: đồng ý là thông báo ghi âm mà
   mọi người xác nhận trong phòng họp trước khi micro được nghe (tính từ giờ bắt đầu họp), mục đích mặc định "Lập biên bản
   cuộc họp", người tạo là Thư ký (không có Thư ký thì người tạo cuộc họp).
2. **Tải bản ghi luôn ra MP3.** Mọi bản ghi Chủ toạ hoặc Thư ký tải về (tải lên, mic phòng, tổng hợp) đều là MP3 mono
   64 kbit/s (`Recordings:Mp3Kbps`), khoảng 29 MB mỗi giờ, tên tệp là tên bản ghi đổi đuôi thành `.mp3`. Tệp đã là MP3 thì
   gửi nguyên. Nghe trên trang không đổi: vẫn phát tệp đang lưu.
3. **Chuyển khi tải, không lưu bản thứ hai.** ffmpeg (`Recordings:FfmpegPath`, mặc định `ffmpeg` trên PATH) đọc tệp đang
   lưu và ghi MP3 thẳng vào response. Server đọc vài byte đầu trước khi trả header: ffmpeg không chạy được hay không ra
   gì thì trả `503 MSG482` thay vì một tệp hỏng; không có ffmpeg thì từ chối ngay lúc xin link (`GET …/download`), không
   ghi nhật ký tải. Lỗi giữa chừng thì cắt kết nối, trình duyệt báo tải lỗi chứ không giữ tệp cụt.
4. **Image cài ffmpeg.** Stage chạy của `Dockerfile` gốc cài `ffmpeg` bằng `apt-get install --no-install-recommends`.

## Lý do

- **Một tệp nghe lại được cả cuộc họp** là thứ người dùng cần để lưu hồ sơ; với họp trực tuyến, cách duy nhất có được nó
  là trộn câu của từng micro, mà phần trộn đã có sẵn cho họp kết hợp.
- **MP3 nhỏ hơn WAV khoảng 4 lần ở 64 kbit/s mono** (16 kHz PCM là 256 kbit/s) và mở được ở mọi máy, kể cả điện thoại.
- **Chuyển khi tải thay vì lưu sẵn MP3:** bản thứ hai là thêm một bản giọng nói của người khác phải theo đúng thời hạn và
  mọi đường xoá (ADR-0067), và phải dựng lại mỗi khi bản tổng hợp dựng lại. Chuyển khi tải thì chỉ có một bản để quản lý.
  Tải về là việc hiếm (chỉ Chủ toạ, Thư ký), nên tốn CPU lúc đó rẻ hơn giữ thêm dữ liệu.
- **Đưa tệp cho ffmpeg theo đường dẫn**, không qua pipe: m4a có chỉ mục ở cuối tệp thì không đọc qua pipe được.

## Hệ quả

- **CPU mỗi lần tải:** ffmpeg mã hoá MP3 nhanh hơn thời gian thực hàng trăm lần trên một nhân (một giờ họp vài chục giây),
  mỗi lượt tải một tiến trình. Không giới hạn số lượt tải song song; nếu thành vấn đề thì thêm hàng đợi.
- **Không tua hay tải tiếp được** khi tải MP3 đã chuyển: chưa biết độ dài trước nên không có `Content-Length` và `Range`.
- **Image nặng thêm** khoảng 420 MB trên đĩa (Ubuntu 24.04 kéo ~200 gói thư viện của ffmpeg; tải về khoảng 120 MB nén).
  Nếu cần nhẹ hơn, thay bằng một bản ffmpeg tĩnh chỉ có libmp3lame (vài chục MB) mà không đổi code.
- Server dev không có ffmpeg thì nút tải báo `MSG482`; test tích hợp dùng bộ chuyển giả, test của ffmpeg thật tự bỏ qua
  khi máy không có ffmpeg.
- **Khác yêu cầu cũ:** ADR-0061 điểm 5 và SCRUM-153 nói bản tổng hợp chỉ có cho họp kết hợp và chỉ để nghe; nay họp
  trực tuyến cũng có, và tải về là MP3 chứ không phải "tệp gốc" như SCRUM-29. BA cập nhật UC, BR trong lượt BA Final Review.
