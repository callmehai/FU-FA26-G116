# Rút gọn Exception Flow và Sequence Diagram

Ghi chú làm việc cho đợt rút gọn EX trong UC Specification (Report 3) và sequence diagram
(Report 4), theo yêu cầu của BA ngày 23/11/2026.

- [KE-HOACH.md](KE-HOACH.md) — kế hoạch, quy tắc và prompt cho agent.
- Mỗi nhóm tính năng sinh thêm `<nhom>-spec-patch.md`, `<nhom>-ba-chon.md` và
  `<nhom>-bao-cao.md` trong thư mục này khi làm.

Công cụ (chỉ đọc): [scripts/rutgon_ex_report.py](../../../scripts/rutgon_ex_report.py) báo EX nên giữ/bỏ;
[scripts/check_sequence_lifelines.py](../../../scripts/check_sequence_lifelines.py) bắt thanh activation bị cắt sớm.
Nguồn spec và sequence nằm ở [docs/01-requirements/](../../01-requirements/README.md) và
[docs/06-sequencediagram/](../../06-sequencediagram/README.md).
