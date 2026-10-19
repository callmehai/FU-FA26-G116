# Ảnh sequence từ Draw.io

Thư mục này chứa một ảnh `.drawio.png` cho mỗi tab Use Case trong
[`TrustCabinet_Sequence_Diagrams.drawio`](../TrustCabinet_Sequence_Diagrams.drawio). Mỗi ảnh được
xuất từ tab tương ứng và giữ dữ liệu Draw.io nhúng để có thể mở lại bằng diagrams.net.

Chạy từ thư mục gốc repo để đồng bộ toàn bộ ảnh:

```powershell
python scripts/sync_sequence_drawio_png.py
```

Cần cài Draw.io CLI. Nếu executable chưa có trong `PATH`, đặt biến môi trường
`DRAWIO_EXECUTABLE` trỏ tới file `draw.io.exe`/`drawio` trước khi chạy. Script đọc page ID và thứ tự
tab từ file gốc, bỏ qua tab index, xuất ảnh ở scale 2, rồi xóa các ảnh `.drawio.png` cũ không còn
tab UC tương ứng. Không chỉnh sửa ảnh trong thư mục này; hãy sửa tab trong file Draw.io gộp rồi chạy
lại script.

## Ảnh đang chờ xuất lại (19/10/2026)

Đợt đồng bộ theo Report 3 ngày 18/10/2026 đã sửa source `.txt`, ảnh SequenceDiagram.org và tab trong file Draw.io gộp,
nhưng **chưa** xuất lại ảnh trong thư mục này. Draw.io CLI 31.7.0 trên máy hiện tại render dở các trang lớn (từ khoảng
29 triệu pixel ở scale 2 trở lên, phần dưới bị trắng) và mất khoảng 3 phút cho mỗi trang. Các ảnh sau còn là bản cũ
hoặc chưa có, cần chạy lại `python scripts/sync_sequence_drawio_png.py` và kiểm tra ảnh không bị cắt:

- Ảnh cũ, tab đã đổi: UC-16, UC-17, UC-22, UC-23a, UC-23b, UC-23c, UC-23d, UC-24, UC-25, UC-26, UC-28b, UC-28c.
- Chưa có ảnh, tab mới: UC-73, UC-74, UC-75, UC-76a, UC-76b, UC-77.

Trong lúc chờ, nguồn đúng là file `.txt` và ảnh `.png` cùng tên trong thư mục của từng feature.
