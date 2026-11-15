# PO 14–15/11 — Bỏ ký số biên bản (UC-62, UC-63) và gom góp ý (SCRUM-106), sửa Report 3

**Người ghi:** KhánhPG/BA, 15/11/2026. **Nguồn:** Hải quyết định (nhắn 15/11/2026):
gỡ UC Ký biên bản và UC Xem trạng thái ký (UC-62, UC-63) cùng quy tắc liên quan vì bỏ ký số
kể cả mô phỏng; không có UC Thư ký gom góp ý, bỏ luôn nếu có nhắc; danh mục UC trong repo đã
đánh dấu bỏ. Trước đó PO 14/11/2026 đã chốt bỏ ký số biên bản kể cả mô phỏng (SCRUM-37, UC-62/63)
và bỏ Thư ký gom góp ý thành danh sách (SCRUM-106) — commit `83aa9ed1` (vào `main`).

**Quyết định 15/11/2026 (Khánh): xoá hẳn.** Gỡ UC-62/63 khỏi Report 3 và mọi chỗ SRS; BR-166 Retired (giữ số).
Ranh giới: code còn bảng `MinutesSignature` (`DbSet` trong `TrustCabinetDbContext`, entity + migration) nên
**không đụng code và ERD theo code** (02-erd mermaid, mục tiêu, Chen giữ nguyên bảng); chỉ dọn nội dung SRS
(Report 3, inventory, UC diagram, SM-13/14, prose 02-design) + câu chữ ERD nhắc BR-166/ký số.
Bỏ bảng khỏi code cần migration BE riêng — ticket riêng, không làm ở đây.

**Đã kiểm trùng:** không có note nào về sửa Report 3 theo quyết định này. `NOTE-dac-ta-so-bo-scrum-33.md`
chỉ ghi UC-62/63 "để dành cho ký số mô phỏng SCRUM-37" (trước khi bỏ); inventory đã đánh dấu Bỏ;
`05-use-case-specifications.md` / `06-business-rules.md` và snapshot Report 3 vẫn còn chỗ ký.

## 1. Hiện trạng trong repo (đã đối chiếu `main` sau merge 15/11/2026)

- **Đã xong:** `00-master-use-case-inventory.md` — UC-62, UC-63 gạch bỏ ("PO 14/11/2026, SCRUM-37 đóng"),
  dòng out-of-scope ghi ký số (kể cả mô phỏng) và gom góp ý (SCRUM-106 đóng); `AGENTS.md` + `README.md`
  ghi đúng hiện trạng (biên bản dừng ở Chủ tọa duyệt là ban hành — UC-64).
- **Còn phải sửa trong Report 3 (DOCX là nguồn, cấm sửa tay `.md`):**
  - Bảng danh mục UC (§I): hai dòng UC-62, UC-63 còn ghi "Placeholder … reserved for simulated
    digital signing (SCRUM-37)" (snapshot dòng 807–808).
  - Đặc tả UC-62 (§12.5), UC-63 (§12.6): còn là placeholder giữ chỗ cho SCRUM-37 (snapshot dòng 2376–2382).
  - `BR-166`: còn nhắc chu kỳ ký, liệt kê UC-62 trong Related UC (snapshot dòng 4662; bản trích
    `06-business-rules.md` dòng 154). Ký bỏ rồi thì quy tắc "request revision invalidates signatures…"
    không còn đối tượng áp dụng.
  - Các chỗ khác cùng họ ký trong Report 3 (rà khi sửa, chỉ chạm đúng phạm vi ký): điều kiện kết thúc
    BF-04 ("completed signing…"), thực thể 27 `MinutesSignature` + enum trạng thái `PendingSignature…`,
    ma trận phân quyền hai dòng "Submit for signing" / "View signature status & audit trail" (kèm chú thích ¹⁶),
    mô tả actor (Approver xem "signature status"; Secretary "finalizes the minutes for digital signing"),
    đặc tả màn hình họ ký (Sign Minutes, Signing Status & Finalization, Finalized Meeting Minutes),
    dòng Change Log 11/11/2026 còn ghi "UC-62 and UC-63 kept as placeholders for SCRUM-37".
  - **SCRUM-106:** không có UC/BR/MSG nào trong Report 3 (chưa từng đặc tả — xem
    `docs/notes/NOTE-tai-lieu-scrum-21-22-23.md` dòng 42: tách thành SCRUM-106, chưa có UC).
    Việc ở đây là rà soát: chỗ nào còn nhắc "Thư ký gom góp ý thành danh sách" thì gỡ; không có thì thôi,
    không đẻ thêm UC.

## 2. Việc BA (sửa DOCX trong `docs/08-reports/source/`, sync một lần ở cuối)

Quy trình: sửa `Report_3_Software_Requirement_Specification.docx` → `python scripts/check_docx.py`
(phải `OK`, Report 3 thêm `--fix-fonts` về Times New Roman) → sync Report 3
(`python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`,
kéo theo bản trích UC/BR và `uc-map.html`) → rà `git diff` chỉ nằm trong vùng ký.

- [x] Gỡ hai dòng UC-62, UC-63 ở bảng danh mục UC, hoặc đánh dấu **Bỏ (PO 14/11/2026)** đồng bộ cách ghi
      inventory — hỏi leader một câu trước khi xóa hẳn (bảng danh mục có tiền lệ giữ dòng bỏ).
- [x] Gỡ hai mục đặc tả UC-62 (§12.5), UC-63 (§12.6); đánh lại số mục 12.x sau đó nếu còn liên tục.
- [x] `BR-166`: đánh dấu **Retired** (giữ số, không tái cấp), ghi câu trỏ thay thế ("Signing dropped PO
      14/11/2026; minutes are issued at Chair approval — UC-64"), gỡ UC-62 khỏi Related UC. Không dùng lại
      số BR-166 cho việc khác.
- [x] Rà các chỗ họ ký ở §1 (BF-04, entity 27 + enum, ma trận phân quyền, mô tả actor, đặc tả màn hình):
      gỡ hoặc viết lại theo "không ký — Chủ tọa duyệt là ban hành". Chỗ nào là màn hình chưa từng có
      (màn ký) thì gỡ cả màn; màn Finalized giữ lại nhưng bỏ câu ký/lịch sử ký.
- [x] Rà nhắc "gom góp ý / SCRUM-106": có thì gỡ, không có thì ghi một dòng xác nhận vào Change Log là đã rà.
- [x] Thêm một dòng Change Log (ngày sửa, SCRUM-37 + SCRUM-106, nguồn: quyết định Hải 15/11/2026).
- [x] Jira: comment kết quả vào **SCRUM-37** (đóng — bỏ ký số kể cả mô phỏng) và **SCRUM-106** (đóng —
      bỏ gom góp ý); hỏi leader trước nếu hai ticket này do người khác giữ.

Làm **sau note 46** (không tranh DOCX Report 3 với ai). Không đẩy Drive khi chưa được yêu cầu.

## 3. Đóng sổ — vào `main` 15/11/2026

- PR #351 (`d634ecd2`) đã merge: UC-62/63 gỡ hẳn (§12.5/12.6 + bảng danh mục, UC-64→12.5, UC-65→12.6, UC-83→12.7), BR-166 Retired, dọn SM-13/14 + UC diagram + prose + Change Log.
- PR #352 (`e7c3f295`) đã merge và **thay thế ranh giới "không đụng code" ở §trên**: bảng `minutes_signatures` xoá thật (migration `DropMinutesSignature`, bỏ entity/cấu hình/`DbSet`/`MeetingMinutes.Signatures`), ERD mục tiêu + Chen vẽ lại, Report 3 bỏ Entity 27 + viết lại Entity 26 theo luồng gửi duyệt → Chủ tọa duyệt là ban hành, BF-04 + sơ đồ ngữ cảnh đổi chữ ký thành duyệt.
- Jira SCRUM-37 + SCRUM-106 đã comment (PO 15/11/2026).
- Không còn việc BA cho bỏ ký số (xem note 49).
