# Báo cáo — nhóm lich-phieu

## Phạm vi

Thư mục: `calendar-sharing`, `voting-management`, `minutes-management`. Tổng 25 UC.

## Bảng tổng kết

| UC | Dòng trước | Dòng sau | EX trước | EX sau |
|---|---:|---:|---:|---:|
| UC-84a | 157 | 73 | 6 | 3 |
| UC-84b | 132 | 73 | 6 | 3 |
| UC-84c | 207 | 85 | 5 | 3 |
| UC-84d | 124 | 63 | 4 | 3 |
| UC-84e | 104 | 57 | 3 | 2 |
| UC-84f | 101 | 27 | 1 | 0 |
| UC-85a | 81 | 49 | 3 | 2 |
| UC-85b | 87 | 39 | 4 | 2 |
| UC-85c | 85 | 42 | 2 | 1 |
| UC-52a | 63 | 48 | 2 | 1 |
| UC-52b | 116 | 92 | 2 | 1 |
| UC-53 | 110 | 54 | 6 | 3 |
| UC-54 | 85 | 53 | 6 | 3 |
| UC-55 | 116 | 53 | 8 | 3 |
| UC-56 | 72 | 48 | 3 | 2 |
| UC-57 | 73 | 53 | 5 | 3 |
| UC-76a | 94 | 53 | 5 | 3 |
| UC-76b | 64 | 54 | 4 | 3 |
| UC-58 | 101 | 54 | 4 | 3 |
| UC-59 | 88 | 54 | 7 | 3 |
| UC-60 | 127 | 53 | 8 | 3 |
| UC-61 | 67 | 53 | 6 | 3 |
| UC-64 | 69 | 53 | 5 | 3 |
| UC-65 | 67 | 53 | 6 | 3 |
| UC-83 | 62 | 53 | 5 | 3 |

## UC chuyển sang ba-chon

Không có UC nào bị bỏ qua. Một số EX nghiệp vụ bị loại do vượt trần được liệt kê trong `lich-phieu-ba-chon.md` để BA xác nhận.

## Kiểm tra

- `check_sequence_lifelines.py` cho 3 thư mục: `0 of N diagrams flagged`.
- Không còn chuỗi cấm (`save failed`, `load failed`, `read failure`, `try again`) trong các `.txt`.
- Đã xuất lại 25 ảnh seqorg (Playwright) và kiểm tra mở được (xem danh sách file PNG).
- Đã mở kiểm tra bằng mắt ít nhất 3 PNG: UC84a, UC55, UC60 — thanh Controller và Service chạy suốt.

## Phạm vi file thay đổi

Chỉ các file `.txt`, `.png` cạnh chúng trong 3 thư mục, và 3 file ghi chú trong `docs/notes/rutgon-ex/`.
