# ADR-0079: Cho đánh số lại BR và UC, có bảng ánh xạ mã cũ sang mã mới

- **Trạng thái:** Chấp nhận — BA (Khánh) đề nghị 23/11/2026
- **Ngày:** 2026-11-23
- **Người quyết định:** BA, trưởng nhóm đồng ý cho bot sửa luật
- **Thay:** câu "không renumber hoặc tái cấp mã đã dùng" của Business Rule trong
  [docs/01-requirements/README.md §2.6](../01-requirements/README.md#26-quy-tắc-viết-business-rule-br). Mã MSG **không đổi**.

## Bối cảnh

Danh mục Business Rule đã lên 406 dòng, nhiều BR trùng ý hoặc chỉ là một bước của UC. Kế hoạch gộp BR (PR #490) muốn
đánh số lại từ BR-001, và sau đó đánh số lại UC cho liền mạch. Luật cũ cấm đánh số lại và cấm tái cấp mã đã dùng, vì
ADR, note và Jira đã trích mã cũ: một mã cũ được cấp lại cho nguyên tắc khác thì các chỗ trích đó trỏ sai mà không ai biết.

## Quyết định

1. **BR và UC được đánh số lại**, theo từng đợt có kế hoạch, không lắt nhắt từng mã.
2. **Mỗi đợt có một bảng ánh xạ cố định** `mã cũ → mã mới` (hoặc → "gộp vào …", → "bỏ"), đặt trong
   `docs/01-requirements/`. Bảng chỉ được thêm, không sửa dòng đã có, và được giữ mãi.
3. **Nơi trích mã cũ:** ADR đã chấp nhận và note cũ **không sửa** (ADR không được sửa); người đọc tra bảng ánh xạ.
   Tài liệu còn sống (UC Specification, Report, README, test case, kế hoạch QA, chú thích code) được cập nhật sang mã mới
   trong cùng đợt.
4. **Mã MSG giữ luật cũ:** không đánh số lại, không tái dùng mã đã nghỉ, vì FE và tester so khớp theo mã
   (`MessageCodeCatalogueTests`).

## Hệ quả

- Sau đợt đánh số lại, cùng một số (ví dụ BR-130) có hai nghĩa theo thời gian. Một mã trích ở ADR hay Jira cũ phải
  được hiểu theo bảng ánh xạ; mã trong tài liệu còn sống là mã mới.
- Script đọc BR (`scripts/trace_br.py`, `sync_requirement_extracts.py`, uc-map) phải được sửa trong cùng đợt đánh số
  lại để không gộp nhầm mã cũ và mã mới.
