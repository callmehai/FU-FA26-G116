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
