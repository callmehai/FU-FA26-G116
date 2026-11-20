# Hoàn thiện Report 3: bỏ UC-51, đưa UC-82 vào FE-05, vẽ bổ sung class diagram

**Ngày:** 2026-11-19 · **Nhánh:** `docs/report3-uc51-bo-uc82-fe05-class` (PR #419 → `main`; #417 đã vào `main` ngày 19/11/2026, merge `bd954709`)
**Trạng thái:** đã sửa DOCX + sync snapshot + class diagram và xử lý review PR #419 ngày 20/11/2026; còn chờ Hải re-review.
**Nguồn chuẩn:** DOCX trong `docs/08-reports/source/` (không sửa tay `Report-3_*.md`, `05-use-case-specifications.md`, `06-business-rules.md` — sinh bằng `scripts/sync_reports.py`).

## 1. Quyết định đã chốt

| # | Quyết định | Hệ quả |
|---|---|---|
| Q1 | **UC-51 Công bố tóm tắt: bỏ.** Tạm giữ số UC-51, sau đánh số lại. **PO Hải chốt ngày 19/11/2026** (note này §1, thảo luận với leader); không có ticket Jira riêng cho UC-51 — ticket gần nhất là **SCRUM-36** (UC-47→UC-51), nơi UC-51 vẫn ghi “chưa làm”. | Inventory + spec + DOCX ghi “bỏ/retired”, không vẽ sequence/class, không code (code hiện cũng chưa có `/publish`). Lý do: UC-50 duyệt xong là mọi người đọc được rồi, UC-51 không còn việc gì. |
| Q2 | **UC-82 Ghi chú cuộc họp: vào FE-05** Agenda Management, `<<extend>>` UC-26. | Spec lấy từ `NOTE-dac-ta-so-bo-scrum-30.md` (BR-395→BR-403, MSG415–MSG423); sequence đã có `.txt` ở `agenda-management/`; class vẽ cùng FE-05; inventory thêm hàng UC-82. |

## 2. Bảng rà soát đã chốt (19/11/2026)

| Nhóm | Thiếu / lệch |
|---|---|
| Đặc tả | UC-51 placeholder; UC-82 vắng mặt (banner `05-...md:11` ghi “UC-82, UC-83 chưa vào Report 3” đã lỗi thời vì UC-83 đã có). Còn lại đủ. |
| Sequence | Thiếu hẳn UC-51. Sơ bộ 10: UC-82, UC-84a→f, UC-85a→c (có `.txt`; UC-82 chưa PNG/tab; README calendar ghi “chưa có code” đã lỗi thời). Còn lại đủ. |
| Class | Thiếu toàn FE: FE-08, FE-12, FE-15, FE-16. Thiếu một phần: FE-07 thiếu UC-74; FE-10 thiếu UC-47→50 và UC-86a→d (UC-51 bỏ thì không vẽ). UC-82 vẽ cùng FE-05. |
| Code | Đủ hết trừ UC-51 chưa có (đúng vì bỏ). UC-82/84/85/86/79/70 đã có code. UC-09/62/63/69 bỏ — không làm. |

## 3. Việc cần làm (về nhà chạy agent)

### A. DOCX Report 3 (`Report_3_Software_Requirement_Specification.docx`)
- [x] UC-51: ghi bỏ/retired, tạm giữ số (ghi rõ “sau đánh số lại”).
- [x] UC-82: nhập spec + BR (free numbers 397–403, 459, 461) + MSG415–MSG423 vào FE-05; cập nhật UC-59 (BR-402) và UC-24 (AF-2).
- [x] UC-84/85: headings/spec đã có đầy đủ; không còn nhãn sơ bộ trong DOCX.
- [x] Sửa banner lỗi thời (05-use-case-specifications.md), cập nhật inventory, thêm 1 dòng Change Log.
- [x] Chạy `python scripts/check_docx.py` → OK, rồi `python scripts/sync_reports.py --source <docx> --report 3`, rà `git diff`.

### B. Sequence (`docs/06-sequencediagram/`)
- [x] UC-82: xuất PNG + thêm tab draw.io (splice vào combined, PNG drawio-png).
- [x] UC-84a→f, UC-85a→c: tabs đã có trong combined drawio; không còn nhãn “sơ bộ”.
- [x] UC-79: tab đã có; kiểm khớp code (không thay đổi).
- [x] Không vẽ UC-51.

### C. Class (`docs/07-classdiagram/`)
- [x] Vẽ mới: FE-08, FE-12, FE-15, FE-16 (source `.drawio` + PNG, validator 0 lỗi).
- [x] Bổ sung: FE-05 thêm UC-82; FE-07 thêm UC-74; FE-10 thêm UC-47→50 và UC-86a→d.
- [x] Mỗi diagram đã làm: source `.drawio` + PNG đồng bộ, validator 0 lỗi.

### D. Inventory + UC diagram
- [x] `00-master-use-case-inventory.md`: UC-51 ghi bỏ/retired, thêm hàng UC-82 vào FE-05.
- [x] `14-uc-diagram/`: thêm node "Take Meeting Notes" (UC-82) vào secretary page, gỡ "Publish Meeting Summary" (UC-51).

## 4. Kết quả và việc còn lại

- PR #419 mở từ nhánh này → `main`. #417 đã vào `main` (`bd954709`) nên nhánh chỉ còn hai commit của #419.
- Review PR #419 ngày 20/11/2026 (`CHANGES_REQUESTED`) đã xử lý: xem note 56 (bản #419). Trong đó có
  **đánh lại BR ghi chú** theo số chính thức trong Report 3 — bảng đối chiếu số tạm → số chính thức:

  | Số tạm trong contract/catalogue cũ | Số chính thức | Nghĩa |
  |---|---|---|
  | BR-396 | BR-398 | Mốc khoá ghi chú |
  | BR-397 | BR-399 | Ghi chú bám mục Chủ toạ đang bàn |
  | BR-398 | BR-400 | Mỗi người một ghi chú mỗi mục + một ghi chú chung |
  | BR-399 | BR-401 | Chống ghi đè bản cũ |
  | BR-400 | BR-403 | Tối đa 20.000 ký tự |
  | BR-401 | BR-459 | Dấu đầu dòng Ý kiến/Kết luận/Nhiệm vụ |
  | BR-403 | BR-461 | Nháp giữ trên máy khi mất mạng |
- DOCX UC-51 đã có bảng retired đầy đủ (Description + N/A cho các flow) để `05-use-case-specifications.md`
  không còn dòng “Placeholder only … added later”; inventory dòng UC-51 cũng đã đổi.
- Không đẩy Drive (`push_reports.py`) khi chưa được yêu cầu. Không commit/push khi chưa được yêu cầu.
