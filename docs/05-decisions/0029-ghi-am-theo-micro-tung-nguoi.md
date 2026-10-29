# ADR-0029: Ghi âm theo micro từng người, chỉ lưu chữ của từng câu

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-26
- **Người quyết định:** Hải (PO, FE), theo SCRUM-109

## Bối cảnh

Trước SCRUM-109, cuộc họp chỉ có một cách lấy lời nói: Thư ký ghi âm hoặc tải lên một bản ghi chung
cả phòng (SCRUM-29). Worker tách người nói rồi Thư ký gán tên, có gợi ý Voice ID (SCRUM-31, SCRUM-35).
Cách này phải đoán ai nói. Phòng ồn hoặc nhiều người nói chen nhau thì đoán sai.

AI-eCabinet có thêm cách thứ hai và đặt làm mặc định: **mỗi người tự thu micro của mình**. Câu nào thu
từ micro của ai thì người đó nói, không cần đoán. PO chốt làm theo eCab (26/10/2026), nhưng khác hai
chỗ:

- Chữ hiện **sau mỗi câu**. eCab giữ các câu trên máy tới khi người dùng tắt micro mới gửi.
- **Chủ tọa không bật micro người khác từ xa được.** eCab cho phép.

## Quyết định

1. **Cuộc họp có cách ghi âm** (`meetings.capture_mode`): `PerMic` (mặc định) hoặc `Room`.
   - Chọn ở form tạo/sửa, hoặc ở tab Bản ghi (Chủ tọa, Thư ký).
   - **Khoá khi cuộc họp bắt đầu**, như eCab. Lý do: người dự phải biết trước micro mình có được gỡ băng
     không, và bản gỡ băng không thể nửa theo micro, nửa thu chung.
   - Cuộc họp đã công bố đổi ở tab Bản ghi, không qua form dời lịch, để không ai nhận thông báo "dời lịch".
2. **Trình duyệt cắt câu theo âm lượng**, không dùng mô hình nhận dạng tiếng nói nào.
   - Lý do: CSP của server test chặn WebAssembly, và micro cá nhân đặt gần miệng thì "to hơn nền ồn" là đủ.
   - Thông số lấy theo eCab: cao hơn nền ồn 9 dB, có 300 ms âm đệm trước câu, bỏ đoạn dưới 400 ms,
     cắt ở 30–40 giây.
   - Riêng khoảng lặng kết thúc câu là 1,2 giây thay vì 5 giây, vì PO muốn chữ hiện theo từng câu.
   - Micro tắt tự tăng âm (AGC) như eCab, để micro người nói nhỏ không thu lên tiếng người ngồi cạnh.
3. **Không lưu âm thanh.**
   - Âm thanh một câu (WAV 16 kHz) chỉ nằm trong bộ nhớ của request.
   - Backend gửi worker tự chạy (ADR-0026, `diarize=false`), nhận chữ rồi bỏ. Tối đa 2 câu cùng lúc
     trên cả server.
   - Chỉ lưu chữ, lúc bắt đầu, thời lượng và chủ micro (`speech_clips`). Chữ xoá theo cùng hạn với bản
     ghi (`Recordings:RetentionDays`).
   - eCab lưu âm thanh từng câu để nghe lại và chạy lại. Ta đổi khả năng đó lấy ít dữ liệu cá nhân hơn.
4. **Gửi lại an toàn.** Mỗi câu có mã do trình duyệt sinh, gửi bằng `PUT …/speech-clips/{mã}`. Mất phản
   hồi thì gửi lại cùng mã, câu chỉ lưu một lần. Worker hỏng thì trình duyệt giữ câu trong bộ nhớ và gửi
   lại, giãn dần tới 30 giây.
5. **Chỉ chính người nói bật hoặc tắt micro của mình.**
   - Trong phòng họp: mặc định ghi khi micro cuộc gọi bật, có chip đỏ "Đang ghi lời bạn".
   - Trên trang cuộc họp (họp trực tiếp, mỗi người một máy): mặc định tắt.
   - Người nói rút lại được câu của mình. Thư ký sửa chữ và xoá câu.

## Hệ quả

- Không cần đoán người nói: tên trên mỗi câu là chủ tài khoản đang thu.
- Thêm một nguồn dữ liệu cá nhân là chữ của từng câu. Nó tuân theo cùng quy tắc với bản ghi: ai xem,
  hạn lưu, xoá.
- Câu đang chờ gửi nằm trong bộ nhớ tab, nên đóng tab là mất. eCab lưu vào IndexedDB, chưa làm ở đây.
- Chưa làm như eCab: một người chỉ thu từ một tab ("ghế micro"), đánh dấu tiếng lọt giữa các micro,
  nghe lại từng câu.
