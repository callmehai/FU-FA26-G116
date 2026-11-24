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

## Tiến độ UI (5.2), cập nhật 2026-11-25 (đếm từ `../cases/FE-xx/ui.json`: 563 case, 563 Passed, 0 Failed, 0 Pending)

| Feature | Trạng thái | Passed / Failed / trống |
|---|---|---|
| FE-01 Authentication | Đã chạy | 26 / 0 / 0 |
| FE-02 User Management | Đã chạy | 33 / 0 / 0 |
| FE-03 Access Control | Đã chạy | 28 / 0 / 0 |
| FE-04 Meeting Management | Đã chạy | 44 / 0 / 0 |
| FE-05 Agenda | Đã chạy | 29 / 0 / 0 |
| FE-06 Participants | Đã chạy | 50 / 0 / 0 |
| FE-07 Documents | Đã chạy | 51 / 0 / 0 |
| FE-08 Online Room | Đã chạy | 54 / 0 / 0 |
| FE-09 Recording | Đã chạy xong 25/11 (4 case mic mới test tay, không ảnh) | 48 / 0 / 0 |
| FE-10 Transcription & Summary | Đã chạy | 64 / 0 / 0 |
| FE-11 Voting | Đã chạy | 21 / 0 / 0 |
| FE-12 Minutes | Đã chạy | 32 / 0 / 0 |
| FE-13 Notifications | Đã chạy | 24 / 0 / 0 |
| FE-15 Reporting | Đã chạy lại trên màn mới | 18 / 0 / 0 |
| FE-16 Calendar | Đã chạy | 41 / 0 / 0 |

FE-14 không có sheet (UC-69 đã bỏ). API (5.2.2): 15 Feature, 825 case, 825 Passed. Số chính xác đọc từ `../cases/FE-xx/api.json`.
