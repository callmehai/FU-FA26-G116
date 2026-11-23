# Nguồn report

**Đây là chỗ sửa nội dung Report 1–7.** Các file `.docx` trong thư mục này là nguồn local chuẩn.
Các `../Report-*.md` chỉ là snapshot — không sửa chúng; sau khi sửa DOCX, chạy
`python scripts/sync_reports.py` (xem [README của 08-reports](../README.md)).

Thư mục `Meeting_Minutes/` giữ biên bản họp từng buổi, mỗi buổi một DOCX. Đẩy cả thư mục lên Drive
bằng `python scripts/push_reports.py minutes` — script tạo file còn thiếu, thay file đã lệch, không xóa
file chỉ có trên Drive. Workbook kiểm thử Report 5.x nằm ở [../test-reports/](../test-reports/README.md).

Quy tắc sửa DOCX, đồng bộ Markdown và thay file Google Drive nằm ở
[README của 08-reports](../README.md). Đọc file đó trước khi sửa nguồn trong thư mục này.

`Report-7_Final Project Document.docx` là mẫu của trường, chỉ tổng hợp lại từ Report 1–6. Ảnh (context, BF, ERD, UC, screen flow,
kiến trúc, package, class, sequence) được chèn bằng `python scripts/fill_report7.py --write` (chạy lại được, dựng lại đúng các mục đó
từ `docs/`); phần chữ và bảng điền tay hoặc ở lượt sau.

PPTX là file nguồn trình bày, không phải snapshot Markdown.
