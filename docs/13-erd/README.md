# 13 · ERD

Hai cách trình bày mô hình dữ liệu của TrustCabinet: `muc-tieu` (dạng bảng, vẽ theo code) và `chen`
(dạng Chen, mô hình đích). Thêm hoặc bỏ thực thể thì rà cả hai.

| Thư mục | Nội dung | Dùng ở đâu | Đẩy Drive |
|---|---|---|---|
| [muc-tieu/](muc-tieu/README.md) | ERD dạng bảng vẽ lại theo code (04/11/2026): 41 bảng — 35 thực thể, 4 bảng nối, 2 bảng kỹ thuật — có thuộc tính | Report 3 §I.3.2; đối chiếu thuộc tính với [02-erd.md](../02-design/02-erd.md) | Có |
| [chen/](chen/README.md) | ERD mục tiêu dạng Chen, không thuộc tính, 35 thực thể / 59 quan hệ | Góc nhìn thực thể–quan hệ | Có |

## Bản nào là nguồn?

- Câu hỏi **schema thật đang có bảng, cột và khoá ngoại nào** → `muc-tieu`. Hình sinh từ snapshot EF bằng
  `scripts/draw_erd.py`; schema đổi thì chạy lại script, không kéo tay.
- Câu hỏi **mô hình đích gồm thực thể và quan hệ nào** → `chen` và [02-erd.md](../02-design/02-erd.md).
  `02-erd.md` là nguồn văn bản: tên thực thể, quan hệ, quy tắc và trạng thái triển khai. Khi Chen và văn
  bản lệch nhau, sửa hình theo văn bản.
- Thứ tự nguồn sự thật của dự án vẫn là: requirement/UC/BR mới nhất → ADR đã chốt → code hiện tại.
- Ảnh chụp schema đang chạy (`hien-tai`) đã bỏ ngày 03/11/2026 vì là bản sinh từ database, không ai sinh
  lại nên cũ 5 migration. `muc-tieu` từ 04/11/2026 thay vai trò đó, sinh từ snapshot trong repo.

## Quy tắc sửa

- `muc-tieu`: không sửa tay file `.drawio`; sửa lưới `GRID` trong `scripts/draw_erd.py` rồi sinh lại
  theo [muc-tieu/README.md](muc-tieu/README.md).
- `chen`: sửa file `.drawio`, xuất lại ảnh cùng tên; không sửa tay file ảnh.
- Tuân theo [DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md) và
  [drawio-skill](../../.agents/skills/drawio-skill/SKILL.md).
- Đẩy `muc-tieu` và `chen` lên Drive theo [DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md).
