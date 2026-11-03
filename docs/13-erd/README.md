# 13 · ERD

Hai cách trình bày mô hình dữ liệu của TrustCabinet: `muc-tieu` (dạng bảng) và `chen` (dạng Chen). Hai bản
cùng mô tả **mô hình đích**; sửa bản này thì rà bản kia.

| Thư mục | Nội dung | Dùng ở đâu | Đẩy Drive |
|---|---|---|---|
| [muc-tieu/](muc-tieu/README.md) | ERD mục tiêu dạng bảng, 35 thực thể, có thuộc tính | Nguồn chính của [02-erd.md](../02-design/02-erd.md) và Report 3 §I.3.2 | Có |
| [chen/](chen/README.md) | ERD mục tiêu dạng Chen, không thuộc tính, 32 thực thể / 53 quan hệ | Góc nhìn thực thể–quan hệ | Có |

## Bản nào là nguồn?

- Câu hỏi **mô hình đích gồm thực thể và quan hệ nào** → `muc-tieu` và `chen`. Hai bản mô tả cùng một mô
  hình; sửa bản này thì rà bản kia.
- [02-erd.md](../02-design/02-erd.md) là nguồn văn bản: tên thực thể, thuộc tính, quan hệ và trạng thái
  triển khai. Khi văn bản và hình lệch nhau, sửa hình theo văn bản, không ngược lại.
- Thứ tự nguồn sự thật của dự án vẫn là: requirement/UC/BR mới nhất → ADR đã chốt → code hiện tại.
- Ảnh chụp schema đang chạy (`hien-tai`) đã bỏ ngày 03/11/2026: đó là bản sinh tự động, cũ 5 migration và
  trùng việc với migration + `02-erd.md`. Cần đối chiếu schema thì đọc migration hoặc snapshot EF.

## Quy tắc sửa

- Sửa file `.drawio`, xuất lại ảnh cùng tên; không sửa tay file ảnh.
- Tuân theo [DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md) và
  [drawio-skill](../../.agents/skills/drawio-skill/SKILL.md).
- Đẩy `muc-tieu` và `chen` lên Drive theo [DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md).
