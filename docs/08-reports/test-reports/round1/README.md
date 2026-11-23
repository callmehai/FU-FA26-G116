# Round 1 — tài liệu làm việc

Mọi thứ phục vụ chạy Round 1 của 5.1 (unit), 5.2 (UI) và 5.2.2 (API). Đây là tài liệu làm việc, không phải nguồn của workbook: nguồn là `../cases/`, quy tắc là [../AGENTS.md](../AGENTS.md).

| Đường dẫn | Nội dung |
|---|---|
| [checklist.md](checklist.md) | Điểm vào: tiến độ UI và API, việc theo thứ tự, người làm |
| [checklist-unit.md](checklist-unit.md) | Checklist riêng cho 5.1 (unit) |
| [handover.md](handover.md) | Bàn giao FE-09, 11, 13, 15 cho đồng đội: việc, cách làm, lỗi môi trường |
| [bugs.md](bugs.md) | Bug, nghi bug (cách tái hiện) và quyết định BA/PO của UI Round 1 |
| [plan-ui.md](plan-ui.md) | Phân việc chạy UI (5.2): nhóm A, AX, H |
| [plan-api.md](plan-api.md) | Phân việc chạy API (5.2.2) |
| [prompts.md](prompts.md) | Prompt giao agent và quy tắc chung |
| [ui-map/](ui-map/README.md) | Bảng ánh xạ AC của `tests/qa/plans/` sang case UI, mỗi Feature một file |
| [reports/](reports/README.md) | Báo cáo của agent (API theo nhóm Feature, `R1.md` cho UI) |

## Tiến độ UI (5.2), cập nhật 2026-11-22

| Feature | Trạng thái | Passed / Failed / trống |
|---|---|---|
| FE-01 Authentication | Đã chạy | 17 / 0 / 17 |
| FE-02 User Management | Đã chạy | 32 / 0 / 13 |
| FE-03 Access Control | Đã chạy | 41 / 0 / 4 |
| FE-04 Meeting Management | Đã chạy | 31 / 1 / 42 |
| FE-05 Agenda | Đã chạy | 38 / 0 / 9 |
| FE-06 Participants | Đã chạy | 79 / 0 / 11 |
| FE-07 Documents | Đã chạy | 72 / 0 / 29 |
| FE-08 Online Room | Đã chạy | 60 / 0 / 38 |
| FE-09 Recording | **Chưa chạy**, giao đồng đội | 0 / 0 / 69 |
| FE-10 Transcription & Summary | Đã chạy | 33 / 0 / 52 |
| FE-11 Voting | **Chưa chạy**, giao đồng đội | 0 / 0 / 23 |
| FE-12 Minutes | Đã chạy | 20 / 1 / 22 (+1 N/A) |
| FE-13 Notifications | **Chưa chạy**, giao đồng đội | 0 / 0 / 35 |
| FE-15 Reporting | **Chưa chạy**, giao đồng đội | 0 / 0 / 17 |
| FE-16 Calendar | Đã chạy | 63 / 0 / 5 |

FE-14 không có sheet (UC-69 đã bỏ). API (5.2.2): 15 Feature, 819 case (807 Passed, 12 Pending ở FE-10). Số chính xác đọc từ `../cases/FE-xx/api.json`.
