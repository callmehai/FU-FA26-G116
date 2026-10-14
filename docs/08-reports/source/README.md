# Nguồn report

Thư mục lưu các file nguồn DOCX của Report 1–4 và file PPTX báo cáo tổng hợp. Quy tắc chọn nguồn,
đồng bộ Markdown và thay file Google Drive nằm trong [README của 08-reports](../README.md); quy tắc
bảo toàn DOCX khi chỉnh sửa nằm trong [AGENTS.md](../AGENTS.md).

- DOCX Report 1, 2 và 3 là nguồn local cho Markdown snapshot. Chỉ dùng `scripts/sync_reports.py` theo
  hướng dẫn của thư mục cha; việc đồng bộ Markdown chỉ đọc DOCX local.
- Report 4 và PPTX được giữ làm file nguồn/tham chiếu; script Markdown hiện chỉ xử lý Report 1–3.
- Không sửa file nguồn khi yêu cầu chỉ nhắm tới Markdown. Khi cần cập nhật file Drive, thay đúng file
  hiện có và giữ nguyên ID/link theo bảng trong README của thư mục cha.
