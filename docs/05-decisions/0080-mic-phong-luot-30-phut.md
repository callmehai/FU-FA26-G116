# ADR-0080: Mic phòng lưu lượt khoảng 30 phút một lần

- **Trạng thái:** Chấp nhận — Lộc chốt 23/11/2026
- **Ngày:** 2026-11-23
- **Người quyết định:** Lộc
- **Thay:** mốc 5 phút (tối đa 6 phút) của [ADR-0078](0078-mic-phong-cat-luot-o-cho-im-lang.md). Cách cắt ở chỗ im lặng
  của ADR-0078 giữ nguyên, chỉ đổi mốc.
- **Liên quan:** [ADR-0041](0041-mic-phong-ghi-tung-luot.md), [ADR-0072](0072-hop-ket-hop-mot-cong-tac-mic-phong.md)

## Bối cảnh

Mỗi lần mic phòng lưu một lượt, server chuyển chữ lại **cả** bản ghi phòng từ đầu (ADR-0041), và lượt mới huỷ lần chạy
cũ chưa xong. Đo trên server ngày 22–23/11: bản ghi 80 phút cần khoảng 25 phút để chuyển chữ cả bản, lâu hơn nhiều so
với 5 phút giữa hai lượt. Phần lớn thời gian họp vì thế là các lần chạy đè nhau: người dùng không thấy chữ mới, server
vẫn bận.

Đã cân nhắc: (A) không chuyển chữ trong lúc họp, chỉ chạy một lần khi kết thúc; (B) mỗi lượt chỉ chuyển chữ phần mới,
tách người nói một lần cuối buổi. Lộc chọn giữ cách hiện tại, chỉ nâng mốc lượt lên 30 phút: không đổi backend, ít lần
chạy lại hơn sáu lần.

## Quyết định

1. **Đủ 30 phút thì chờ chỗ ngừng nói rồi cắt** (`TAKE_SPLIT_SECONDS = 30 * 60`), cùng cách đo im lặng của ADR-0078.
2. **Không có chỗ im thì cắt ở 31 phút** (`TAKE_MAX_EXTRA_SECONDS` giữ 60 giây). Gần chạm giới hạn kích thước thì vẫn
   cắt ngay như cũ.
3. Tắt mic, rời trang, cuộc họp kết thúc thì lưu lượt ngay như trước, không chờ đủ 30 phút.

## Nghĩa vụ theo Luật 91/2025/QH15

Không đổi gì: tiếng được ghi, mục đích, mốc đồng ý và thời hạn lưu giữ như ADR-0041 và ADR-0072.

## Hệ quả

- Họp 80 phút: chuyển chữ lại khoảng 3 lần thay vì 16 lần.
- Trong lúc họp, chữ của mic phòng chỉ hiện sau mỗi 30 phút (hoặc khi Thư ký tắt mic). Muốn xem sớm thì tắt rồi bật lại
  mic: lượt đang ghi được lưu và chuyển chữ ngay.
- Một lượt 30 phút khoảng 58 MB (WAV 16 kHz mono), vẫn dưới giới hạn 300 MB; bản ghi phòng vẫn chạm 300 MB sau khoảng
  2 giờ 43 phút và lượt sau mở bản ghi phòng mới như cũ.
- Không đổi endpoint, cơ sở dữ liệu hay backend; tooltip đồng hồ mic tự đọc "khoảng 30 phút".
