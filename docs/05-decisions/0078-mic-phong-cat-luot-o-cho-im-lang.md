# ADR-0078: Mic phòng cắt lượt ở chỗ im lặng

- **Trạng thái:** Đề xuất — buổi họp thật 22/11/2026
- **Ngày:** 2026-11-23
- **Người quyết định:** trưởng nhóm (chọn phương án tốt nhất), chờ PO duyệt
- **Thay:** cách cắt cứng đúng 5 phút ở điểm 5 của [ADR-0072](0072-hop-ket-hop-mot-cong-tac-mic-phong.md). Phần còn lại
  của điểm 5 giữ nguyên: mic còn bật thì tự lưu lượt và mở lượt sau, không hỏi lại xác nhận, không mất tiếng.
- **Liên quan:** [ADR-0041](0041-mic-phong-ghi-tung-luot.md) (mic phòng ghi từng lượt, giới hạn kích thước một lượt)

## Bối cảnh

ADR-0072 cho mic phòng tự lưu lượt khi lượt đủ đúng 5 phút tiếng (`TAKE_SPLIT_SECONDS`). Ở buổi họp thật 22/11 một
lần cắt rơi vào giữa câu: nửa câu ở cuối lượt trước, nửa câu ở đầu lượt sau. Máy chuyển chữ nhận từng lượt riêng, nên
cả hai nửa đều ra chữ sai.

Hai cách đã cân nhắc:

| Cách | Được | Mất |
|---|---|---|
| Hai lượt chồng nhau khoảng 30 giây | Câu bị cắt có đủ ở lượt sau | Phải sửa server (một cuộc họp chỉ có một lượt mở), và câu trong đoạn chồng ra **hai lần** trong bản gỡ băng, phải tìm cách bỏ bớt |
| **Cắt ở chỗ im lặng** | Không đổi server, không chồng, không lặp chữ | Lượt dài hơn 5 phút một chút, tới 6 phút |

Trưởng nhóm chọn cách tốt nhất: cắt ở chỗ im lặng.

## Quyết định

1. **Đủ 5 phút thì chờ chỗ ngừng nói rồi mới cắt.** Lượt chưa đủ 5 phút thì không cắt, kể cả khi phòng im. Từ phút thứ 5,
   phòng im liền **600 ms** là cắt ngay ở đó: lượt trước kết thúc trong khoảng lặng, lượt sau bắt đầu từ khoảng lặng đó.
   600 ms dài hơn chỗ ngắt giữa các từ (thường dưới 300 ms) và ngắn hơn chỗ dừng giữa hai câu.
2. **Không có chỗ im thì vẫn cắt ở 6 phút.** Phòng nói liền không nghỉ thêm 60 giây (`TAKE_MAX_EXTRA_SECONDS`) thì cắt
   như trước, để một lượt không bao giờ dài quá 6 phút. Gần chạm giới hạn kích thước (ADR-0041) thì vẫn cắt ngay như cũ.
3. **"Im" so với tiếng nền của chính phòng đó, không phải một mức cố định.**
   - Đo độ lớn (RMS, dB) của từng khối tiếng mic gửi lên, khoảng 43 ms một khối.
   - Tiếng nền là mức của 10% khối nhỏ nhất trong 30 giây gần nhất, tính lại mỗi giây.
   - Một khối là "im" khi nhỏ hơn tiếng nền **+ 9 dB** (giống ngưỡng bộ cắt câu của micro từng người, SCRUM-109), nhưng
     ngưỡng luôn nằm trong khoảng **-55 dB đến -35 dB**: dưới -55 dB lúc nào cũng là im (phòng trống sau khi trình duyệt
     lọc ồn), trên -35 dB không bao giờ là im (phòng đang nói to).
   - Phòng có máy lạnh kêu đều thì tiếng máy lạnh là tiếng nền: chỗ chỉ còn tiếng máy lạnh vẫn tính là im.
4. **Đo trên chính các mẫu tiếng đang ghi vào lượt, không thêm `AnalyserNode` hay `getUserMedia` thứ hai.** Mic phòng đã
   có một `AudioWorklet` chuyển từng khối tiếng cho trang để ghi; bộ đo độ lớn đọc ngay các khối đó. Cách này hơn
   `AnalyserNode` ở hai điểm: không cần hẹn giờ đọc định kỳ (trình duyệt làm chậm hẹn giờ ở tab chạy nền, ví dụ trang
   cuộc họp khi Thư ký đang ở tab phòng họp), và chỗ im đo được nằm đúng trên dòng thời gian của lượt, nên chỗ cắt chính
   là chỗ im.
5. **Giữ mọi hành vi khác của ADR-0072.** Tắt mic, rời trang, cuộc họp kết thúc thì lưu lượt đang ghi ngay, không chờ chỗ
   im. Đi giữa trang cuộc họp và phòng họp trong một tab không tắt mic, và bộ đo đi theo bộ ghi mic phòng
   (`RoomMicProvider`), nên không bắt đầu đo lại. Đổi micro trong phòng họp cũng không đặt lại.

Hằng số nằm trong `frontend/src/features/recordings/takeRoller.ts`; quyết định cắt là hàm thuần `shouldRollNow`, có
unit test trong `frontend/tests/unit/takeRoller.test.ts`.

## Nghĩa vụ theo Luật 91/2025/QH15

Không đổi gì: tiếng được ghi, mục đích, mốc đồng ý và thời hạn lưu giữ như ADR-0041 và ADR-0072. Bộ đo chỉ tính độ lớn
trong bộ nhớ trình duyệt, không lưu, không gửi đi.

## Hệ quả

- Lượt mic phòng dài khoảng 5 đến 6 phút thay vì đúng 5 phút, và kết thúc ở chỗ ngừng nói. Vạch "Lượt N" trên tab Bản ghi
  vì thế không còn cách đều nhau đúng 5 phút.
- Không đổi endpoint, cơ sở dữ liệu hay backend; không có đoạn chồng nên không có câu lặp.
- Phòng nói liền 6 phút không nghỉ thì vẫn có thể cắt giữa câu, như trước nhưng hiếm hơn nhiều.
- Muốn thử nhanh khi phát triển: `localStorage['tc:roomMicSplit'] = '10,5'` (chỉ ở `npm run dev`) cho lượt cắt từ giây
  thứ 10 ở chỗ im, chậm nhất giây thứ 15. Bản build không có đoạn này.
