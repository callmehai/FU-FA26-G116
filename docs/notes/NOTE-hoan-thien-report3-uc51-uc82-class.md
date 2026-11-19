# Hoàn thiện Report 3: bỏ UC-51, đưa UC-82 vào FE-05, vẽ bổ sung class diagram

**Ngày:** 2026-11-19 · **Nhánh:** `docs/report3-uc51-bo-uc82-fe05-class` (xếp chồng lên `docs/ba-final-review-note54-ui-audit-br420`, PR #417 — nhánh sửa Report 3 gần nhất, commit `a9e07091` ngày 19/11/2026, chưa vào `main`)
**Trạng thái:** note việc cần làm đã chốt với nhau; **chưa sửa DOCX/diagram**, về nhà chạy agent tiếp.
**Nguồn chuẩn:** DOCX trong `docs/08-reports/source/` (không sửa tay `Report-3_*.md`, `05-use-case-specifications.md`, `06-business-rules.md` — sinh bằng `scripts/sync_reports.py`).

## 1. Quyết định đã chốt

| # | Quyết định | Hệ quả |
|---|---|---|
| Q1 | **UC-51 Công bố tóm tắt: bỏ.** Tạm giữ số UC-51, sau đánh số lại. | Inventory + spec + DOCX ghi “bỏ/retired”, không vẽ sequence/class, không code (code hiện cũng chưa có `/publish`). Lý do: UC-50 duyệt xong là mọi người đọc được rồi, UC-51 không còn việc gì. |
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
- [ ] UC-51: ghi bỏ/retired, tạm giữ số (ghi rõ “sau đánh số lại”).
- [ ] UC-82: nhập spec + BR-395→BR-403 + MSG415–MSG423 từ `NOTE-dac-ta-so-bo-scrum-30.md` vào FE-05; cập nhật UC-59 (BR-402) và UC-24 (AF-2).
- [ ] UC-84/85: nâng từ sơ bộ thành chính thức, đối chiếu code SCRUM-137/142.
- [ ] Sửa banner lỗi thời, cập nhật inventory trong DOCX, thêm 1 dòng Change Log.
- [ ] Chạy `python scripts/check_docx.py` → phải `OK`, rồi `python scripts/sync_reports.py --source <docx> --report 3`, rà `git diff`.

### B. Sequence (`docs/06-sequencediagram/`)
- [ ] UC-82: xuất PNG + thêm tab draw.io (hiện chỉ có `.txt`).
- [ ] UC-84a→f, UC-85a→c: đối chiếu code, gỡ nhãn “sơ bộ”, xuất lại PNG/tab nếu đổi.
- [ ] UC-79: kiểm khớp code Register to Speak hiện tại.
- [ ] Không vẽ UC-51.

### C. Class (`docs/07-classdiagram/`)
- [ ] Vẽ mới: FE-08 (UC-37/38/39a-d/40/41/42/78/79), FE-12 (UC-58/59/60/61/64/65/83), FE-15 (UC-70a/b), FE-16 (UC-84/85).
- [ ] Bổ sung: FE-05 thêm UC-82; FE-07 thêm UC-74; FE-10 thêm UC-47→50 và UC-86a→d.
- [ ] Mỗi diagram: source `.drawio` + PNG đồng bộ, validator 0 lỗi, render xem không cắt/chồng chữ.

### D. Inventory + UC diagram
- [ ] `00-master-use-case-inventory.md` (qua sync từ DOCX): UC-51 ghi bỏ, thêm hàng UC-82 vào FE-05.
- [ ] `14-uc-diagram/`: thêm UC-82 theo actor, gỡ UC-51.

## 4. Kế hoạch stacked PR

- Layer 1 (nhánh này, base là PR #417): note + DOCX UC-51/UC-82 + sync snapshot.
- Layer 2: sequence UC-82/84/85.
- Layer 3: class diagram bổ sung.
- `gh stack` chưa cài trên máy này — chạy `gh extension install github/gh-stack` rồi `gh stack link` khi đã có PR, hoặc báo để làm cùng.
- Không đẩy Drive (`push_reports.py`) khi chưa được yêu cầu. Không commit/push khi chưa được yêu cầu — file note này đang để uncommitted.
