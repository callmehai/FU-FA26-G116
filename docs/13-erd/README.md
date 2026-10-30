# 13 · ERD

Ba cách trình bày mô hình dữ liệu của TrustCabinet. Đây không phải ba bản phải giữ giống nhau bằng tay:
`muc-tieu` và `chen` cùng mô tả **mô hình đích**, còn `hien-tai` là ảnh chụp schema đang chạy.

| Thư mục | Nội dung | Dùng ở đâu | Đẩy Drive |
|---|---|---|---|
| [muc-tieu/](muc-tieu/README.md) | ERD mục tiêu dạng bảng, 30 thực thể, có thuộc tính | Nguồn chính của [02-erd.md](../02-design/02-erd.md) và Report 3 §I.3.2 | Có |
| [chen/](chen/README.md) | ERD mục tiêu dạng Chen, không thuộc tính, 30 thực thể / 52 quan hệ | Góc nhìn thực thể–quan hệ | Có |
| [hien-tai/](hien-tai/README.md) | Snapshot schema hiện tại, sinh từ database | Đối chiếu code và migration; không phải nguồn yêu cầu | Không |

## Bản nào là nguồn?

- Câu hỏi **mô hình đích gồm thực thể và quan hệ nào** → `muc-tieu` và `chen`. Hai bản mô tả cùng một mô
  hình; sửa bản này thì rà bản kia.
- Câu hỏi **database đang có gì** → `hien-tai`, sau khi migration đã chạy và snapshot được sinh lại.
- [02-erd.md](../02-design/02-erd.md) là nguồn văn bản: tên thực thể, thuộc tính, quan hệ và trạng thái
  triển khai. Khi văn bản và hình lệch nhau, sửa hình theo văn bản, không ngược lại.
- Thứ tự nguồn sự thật của dự án vẫn là: requirement/UC/BR mới nhất → ADR đã chốt → code hiện tại.

## Quy tắc sửa

- Sửa file `.drawio`, xuất lại ảnh cùng tên; không sửa tay file ảnh.
- Tuân theo [DRAWIO_EDITING_RULES.md](../../DRAWIO_EDITING_RULES.md) và
  [drawio-skill](../../.agents/skills/drawio-skill/SKILL.md).
- Đẩy `muc-tieu` và `chen` lên Drive theo [DIAGRAM_DRIVE_SYNC.md](../DIAGRAM_DRIVE_SYNC.md). `hien-tai`
  nằm ngoài phạm vi đồng bộ.
- `hien-tai` chỉ được sinh lại sau khi migration chạy; không chỉnh sửa file trong thư mục đó.
