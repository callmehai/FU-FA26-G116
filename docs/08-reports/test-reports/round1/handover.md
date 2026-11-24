# Bàn giao: chạy UI Round 1 cho FE-09, FE-11, FE-13, FE-15

Người nhận: thành viên được giao. Người bàn giao: Phạm Gia Khánh (đã chạy 11/15 Feature). Ngày: 2026-11-22.

## Việc cần làm

| Feature | Case UI | Plan Playwright | Bảng ánh xạ AC | Ghi chú |
|---|---:|---|---|---|
| FE-09 Recording | 69 | `tests/qa/plans/fe-09.mjs` (24 AC) | [ui-map/FE-09.md](ui-map/FE-09.md): 25 D, 22 P, 22 N | Nhiều case `N` cần hai phiên hoặc chèn lỗi (nhóm AX) |
| FE-11 Voting | 23 | `tests/qa/plans/fe-11.mjs` | [ui-map/FE-11.md](ui-map/FE-11.md): 8 D, 8 P, 7 N | |
| FE-13 Notifications | 35 | `tests/qa/plans/fe-13.mjs` | [ui-map/FE-13.md](ui-map/FE-13.md): 12 D, 3 P, 20 N | Email thật là nhóm H, làm tay |
| FE-15 Reporting | 17 | `tests/qa/plans/fe-15.mjs` | [ui-map/FE-15.md](ui-map/FE-15.md): 13 D, 3 P, 1 N | Ít việc nhất |

D = một AC phủ đủ, P = phủ một phần (viết thêm bước), N = chưa có AC (viết AC mới). Ý nghĩa nhóm A, AX, H: [plan-ui.md](plan-ui.md).

## Cách làm (theo thứ tự)

1. Đọc [../AGENTS.md](../AGENTS.md), [plan-ui.md](plan-ui.md) và skill `qa-ticket` (`.claude/skills/qa-ticket/SKILL.md`).
2. Dựng môi trường: `scripts/dev-up.ps1`, rồi chạy `cd tests/qa; node run.mjs plans/fe-09.mjs --only AC1 --base http://localhost:5173`. Mỗi AC một trình duyệt riêng, chạy cả desktop và phone (390px).
3. **Xem từng ảnh chụp bằng tay** trong `tests/qa/out/<ticket>/<giờ>-<commit>/` và đánh giá đúng/sai. Runner báo PASS chưa đủ.
4. Ghi kết quả vào `cases/FE-xx/ui.json`: `round1` (Passed, Failed, N/A hoặc để trống), `date1`, `tester1`, `note` kèm đường dẫn ảnh. Không gõ vào Excel.
5. Case chưa có AC thì viết thêm AC trong plan rồi chạy. Chưa chạy được thì **để trống**, không điền Passed.
6. Chạy `python scripts/test_reports/integration_cases.py check` (phải 0 lỗi). **Không chạy `build`**, Khánh sẽ chốt workbook.

## Case lệch sản phẩm thì làm gì

PO chốt 2026-11-22: **giữ app, sửa case theo app** (sửa `description`, `procedure`, `expected` trong JSON, thêm vào note dòng "Đã sửa case theo sản phẩm (ngày)"). Chỉ coi là bug khi app trái UC/BR, hoặc làm hỏng dữ liệu hay quyền. Bug và nghi bug ghi vào [bugs.md](bugs.md) kèm các bước tái hiện bằng tay. Mẫu: BUG-01 và SUS-01.

## Lỗi môi trường đã gặp

- Cổng 55432 bị Windows giữ: Postgres chạy ở 55620 qua `POSTGRES_PORT` và `ConnectionStrings__TrustCabinet` (`Port=55620`).
- Backend cần `TMP`/`TEMP` trỏ tới thư mục ghi được (ví dụ `%USERPROFILE%\AppData\LocalLow\TrustCabinetQA`), nếu không upload tệp lỗi 500 `UnauthorizedAccessException`.
- Vite chỉ mở ở `localhost` (không phải `127.0.0.1`).
- DB dùng lại có thể lệch `role_capabilities` so với seed (Thành viên không có quyền nào, Quản trị chỉ `AdminUsers`) làm AC quyền lỗi giả. Sau khi chạy FE-03, kiểm lại ma trận.
- Chi tiết: [bugs.md](bugs.md) mục 5.

## Còn lại cho Khánh

51 case nhóm H làm tay (Google thật, email thật, micro, giọng nói, kiểm hình ảnh) và `build` chốt workbook.
