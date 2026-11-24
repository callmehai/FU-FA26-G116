# Báo cáo — nhóm con-lai

## Tóm tắt
- 22 diagram trong 5 thư mục; đã sửa 18 file `.txt` (thêm nhãn EX, bỏ nhánh hạ tầng, sửa thanh activation).
- UC-04 chuyển sang `con-lai-ba-chon.md` (không sửa `.txt`).
- `check_sequence_lifelines.py` báo `0 of 22 diagrams flagged`.
- Đã xuất lại 18 ảnh seqorg (Playwright).

## Bảng UC đã sửa

| UC | Dòng trước | Dòng sau | EX trước | EX sau |
|---|---:|---:|---:|---:|
| UC-02 | 54 | 50 | 0* | 0* |
| UC-03 | 16 | 11 | 0 | 0 |
| UC-06a | 53 | 53 | 0* | 2 |
| UC-06b | 55 | 55 | 0* | 1 |
| UC-06c | 159 | 159 | 1 | 1 |
| UC-07 | 121 | 93 | 3 | 2 |
| UC-08 | 133 | 156 | 2 | 2 |
| UC-11 | 105 | 137 | 0* | 1 |
| UC-12a | 62 | 57 | 0* | 2 |
| UC-12b | 80 | 109 | 0* | 3 |
| UC-12c | 69 | 83 | 0* | 2 |
| UC-13 | 62 | 74 | 3 | 2 |
| UC-66 | 37 | 38 | 1 | 0 |
| UC-67a | 51 | 68 | 2 | 1 |
| UC-67b | 47 | 48 | 1 | 0 |
| UC-68 | 85 | 110 | 4 | 3 |
| UC-70a | 84 | 88 | 3 | 2 |
| UC-70b | 80 | 89 | 4 | 2 |

\* Trước đây nhánh lỗi chưa có nhãn `- EX-n`; đã thêm nhãn theo spec.

## UC chuyển sang ba-chon
- UC-04 (9 EX nghiệp vụ).

## Kiểm tra
- `check_sequence_lifelines.py` → 0 of 22 diagrams flagged.
- grep infra phrases → chỉ còn UC-04 (ba-chon, không sửa).
- Đã mở ≥ 3 PNG (UC07, UC08, UC70a) kiểm tra bằng mắt.