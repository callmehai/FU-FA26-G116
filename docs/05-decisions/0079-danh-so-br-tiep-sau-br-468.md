# ADR-0079: Đánh số BR gộp tiếp sau BR-468

- **Trạng thái:** Đề xuất — 2026-11-23
- **Ngày:** 2026-11-23
- **Người quyết định:** chờ PO/leader duyệt
- **Liên quan:** [ADR-0064](0064-backend-bon-project-clean-architecture.md), kế hoạch gộp Business Rules `docs/01-requirements/08-ke-hoach-gop-business-rules.md`

## Bối cảnh

Kế hoạch gộp Business Rules viết lại toàn bộ bộ BR từ 406 về 153 BR. Ban đầu các BR mới được đánh
`BR-001…BR-153`. Các mã này trùng với BR cũ đã cấp (`BR-001…BR-468`), trong đó `trace_br.py`
chuẩn hoá `BR-013` thành `BR-13`, nên ADR, ghi chú và test cũ sẽ trỏ sai BR.

## Quyết định

Bộ BR gộp **không** đánh số lại từ `BR-001`. Các BR mới được đánh tiếp sau mã BR cũ lớn nhất
(`BR-468`), tức `BR-469…BR-621`. Toàn bộ ID cũ (`BR-001…BR-468`) ngừng dùng và chỉ tra qua
bảng ánh xạ ở mục 4, 5 của kế hoạch gộp. Không dùng tiền tố khác.

Câu "ngoại lệ so với quy ước không đánh số lại" bị bỏ; đây là đánh số mới theo dải riêng, không
ghi đè mã cũ.

## Hệ quả

- `trace_br.py` và các script đọc BR không còn nhầm `BR-013` với `BR-13` vì mã mới đều 3 chữ số
  và nằm ngoài dải cũ.
- ADR, notes, test-reports giữ nguyên; tra cứu bằng bảng ánh xạ.
- BR ngừng dùng (8 BR) giữ mã cũ trong bảng retired, không ánh xạ sang dải mới.
"