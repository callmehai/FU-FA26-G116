# ADR-0040: Cách ghi âm suy ra từ hình thức họp: trực tuyến ghi theo micro từng người, trực tiếp ghi bằng mic phòng

- **Trạng thái:** Đề xuất — SCRUM-109, PO 29/10/2026
- **Ngày:** 2026-10-29
- **Người quyết định:** Hải (PO)
- **Thay:**
  - điểm 1 (chọn `PerMic`/`Room`) và điểm 5 (công tắc "Ghi lời tôi", mỗi người một máy khi họp trực tiếp) của
    [ADR-0029](0029-ghi-am-theo-micro-tung-nguoi.md);
  - [ADR-0030](0030-ghi-ca-cuoc-goi-tu-may-thu-ky.md) (Thư ký ghi trộn cả cuộc gọi);
  - phần "Đồng ý" của [ADR-0039](0039-luu-am-thanh-tung-cau-de-nghe-lai.md) (người nói tự bật micro ở ô micro nổi).
- **Chốt với nhóm:** 📌・note-chung, 29/10/2026

## Bối cảnh

Từ ADR-0029, Thư ký phải chọn trước "Micro từng người" hay "Thu chung cả phòng". AI-eCabinet chỉ có phòng họp thật,
nên cách "mỗi người một laptop" của nó sinh ra tiếng lọt giữa các máy ngồi cạnh nhau (ADR-0038). TrustCabinet có
thêm phòng gọi trực tuyến, nên câu hỏi đúng là: **trong một cuộc họp, tiếng của mỗi người đi qua đâu**, và có mấy
đường tiếng.

| Đường tiếng | Một đường chứa | Các đường nghe lẫn nhau | Cách xử lý |
|---|---|---|---|
| Mic riêng của người họp trực tuyến | 1 người | Không: mỗi người một nơi | Biết chắc ai nói theo tài khoản |
| Mic chung của phòng (micro phòng, laptop Thư ký) | Nhiều người | Chỉ có một đường | Tách người nói, gợi ý tên bằng Voice ID (ADR-0027) |
| Nhiều laptop trong cùng một phòng (cách của eCab) | Mỗi máy chủ yếu 1 người | Có | Phải chống tiếng lọt (ADR-0038) |

Họp kết hợp thực tế dùng một máy phòng họp nối với loa và micro của phòng, vào cuộc gọi như một người: người trực
tuyến nghe phòng qua máy đó, phòng nghe người trực tuyến qua loa. Mọi câu nói đều đi qua cuộc gọi đúng một lần.

## Quyết định

1. **Hình thức họp quyết định cách ghi**, Thư ký không chọn nữa.

   | Hình thức | Mic | Ghi thế nào |
   |---|---|---|
   | Trực tuyến | N mic, mỗi mic 1 người | Mỗi người tự ghi micro của mình trong phòng gọi, gửi từng câu |
   | Trực tiếp | 1 mic phòng | Thư ký ghi cả phòng, hoặc tải tệp lên sau. Chuyển chữ cả tệp, tách người nói |
   | Kết hợp | 1 mic phòng + N mic | Cả hai |

   - Bỏ cột `meetings.capture_mode`, bỏ `captureMode` khỏi API tạo, sửa và xem cuộc họp, bỏ
     `PUT /api/meetings/{id}/capture-mode`. `MSG324`–`MSG327` ngừng dùng.
   - Hình thức họp đã khoá khi cuộc họp được công bố, nên cách ghi cũng không đổi giữa chừng.
2. **Bỏ ghi theo từng laptop trong phòng họp.** Họp trực tiếp chỉ dùng một mic phòng.
3. **Họp trực tuyến: nút mic của cuộc gọi là công tắc duy nhất.**
   - Mic bật thì người khác nghe và câu được ghi; tắt mic là không ghi. Bỏ nút "Ghi lời tôi / Dừng ghi".
   - Bỏ ô "Lời nói" trong phòng gọi: đang họp không ai đọc. Chip "Đang ghi lời bạn" mở "Ghi âm của tôi", từ đó mở
     cả bản ghi ở tab mới.
   - Bỏ ghi trộn cả cuộc gọi từ máy Thư ký (ADR-0030): lời từng người đã nằm trên mic của họ. `MSG344` ngừng dùng.
   - Server từ chối bản ghi phòng của cuộc họp trực tuyến (`MSG358`).
4. **Họp trực tiếp: thẻ ghi âm nổi của Thư ký** trên trang cuộc họp, ở góc mọi tab trong khi họp: Ghi (kèm hộp xác
   nhận đã báo mọi người), Tạm dừng, Dừng và lưu, thời lượng, mức âm. Trang giữ bộ ghi, nên đổi tab không dừng ghi.
   Rời trang khi đang ghi thì phần đã ghi được lưu như khi bấm Dừng.
5. **Họp kết hợp:** người trực tuyến như mục 3. Phần phòng họp do máy phòng họp ghi, chọn "Máy phòng họp" khi vào
   cuộc gọi; phần này làm ở thay đổi sau.

## Nghĩa vụ theo Luật 91/2025/QH15

- **Căn cứ ghi âm:** âm thanh thu từ hội nghị, hội thảo được ghi mà không cần sự đồng ý của từng người, miễn không
  làm tổn hại danh dự, uy tín của họ (Điều 32 khoản 1 điểm b). Họp nội bộ thì căn cứ là hoạt động của cơ quan nhà
  nước hoặc thoả thuận với người lao động (Điều 19 khoản 1 điểm c, d).
- **Thông báo trước khi ghi** (Điều 32 khoản 2):
  - Màn chờ của phòng gọi ghi "Cuộc họp được ghi âm để lập biên bản · tự xoá sau 180 ngày" trước khi vào phòng.
  - Thư ký phải xác nhận đã báo người dự trước khi ghi cả phòng.
- **Lưu trong thời gian cần thiết** (Điều 32 khoản 4, Điều 3 khoản 3), rồi xoá an toàn (Điều 14): bản ghi và từng
  câu tự xoá sau `Recordings:RetentionDays` (180 ngày).
- **Voice ID** vẫn là dữ liệu sinh trắc học, cần đồng ý riêng của từng người (Điều 31, ADR-0027). Ghi âm cuộc họp
  không thay được sự đồng ý đó.

## Hệ quả

- Người dự không phải chọn hay bật gì ngoài mic của cuộc gọi.
- Tên người nói trong phòng họp chỉ đúng khi người đó đã đăng ký Voice ID; ai chưa đăng ký thì Thư ký gán tay.
- Dò tiếng lọt (ADR-0038) vẫn giữ. Hai người họp trực tuyến ngồi chung một văn phòng vẫn lọt tiếng của nhau.
- Một cuộc họp trực tuyến đã có bản ghi phòng từ trước thay đổi này vẫn hiện bản ghi đó, chỉ không thêm được.
- Chưa làm trong thay đổi này:
  - đẩy bản ghi phòng lên từng phần trong khi ghi; hiện vẫn tải lên một lần khi dừng;
  - chế độ "Máy phòng họp", và ghép phần phòng họp với câu trực tuyến thành một dòng thời gian.
