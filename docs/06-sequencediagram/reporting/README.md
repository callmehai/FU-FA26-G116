# Báo cáo và thống kê — FE-15 (đã vào Report 3; đã có BE và FE)

Các sequence UC-79a, UC-79b tuân theo [convention chung](../README.md). Đặc tả UC-79a, UC-79b, BR-149–BR-152, BR-010 và
mã `MSG463`–`MSG466` đã vào Report 3 ngày 09/11/2026 (bản trích ở
[05-use-case-specifications.md](../../01-requirements/05-use-case-specifications.md)); lý do và chỗ lệch khỏi ticket ở
[NOTE-dac-ta-so-bo-scrum-38.md](../../notes/NOTE-dac-ta-so-bo-scrum-38.md) (SCRUM-38). UC-79 là UC gom, đặc tả nằm ở
các UC con.

| UC | Source | Ảnh |
|---|---|---|
| UC-79a View Reports & Statistics | [UC79a_ViewReportsAndStatistics.txt](UC79a_ViewReportsAndStatistics.txt) | [PNG](UC79a_ViewReportsAndStatistics.png) · [draw.io](../drawio-png/uc79a_viewreportsandstatistics.drawio.png) |
| UC-79b Export Report | [UC79b_ExportReport.txt](UC79b_ExportReport.txt) | [PNG](UC79b_ExportReport.png) · [draw.io](../drawio-png/uc79b_exportreport.drawio.png) |

- Actor là mọi người dùng nội bộ; số liệu chỉ đếm cuộc họp người xem đọc được, Quản trị cũng vậy (BR-149, BR-150).
- Kết quả chỉ có số đếm, tỷ lệ và trung bình — không tiêu đề, người dự hay nội dung cuộc họp (BR-152).
- UC-79b tính lại số liệu lúc xuất và ghi audit; không ghi được audit thì không trả tệp (BR-010).
- Đã xuất PNG và thêm 2 tab vào `TrustCabinet_Sequence_Diagrams.drawio` ngày 09/11/2026. Lifeline
  `:Report Controller` / `:Report Service` ứng với `ReportEndpoints` / `ReportService`; `:Meeting Repository` là truy
  vấn EF Core ngay trong `ReportService`, `:Audit Service` là `IAuditService`.

## Cập nhật theo kiến trúc

- **17/11/2026 — CA-19 (SCRUM-184):** service không còn gọi thẳng `:Database` (backend bốn project, [ADR-0064](../../05-decisions/0064-backend-bon-project-clean-architecture.md)); chèn lifeline `:Audit Log Repository` giữa `:Audit Service` và `:Database` ở UC-79b. Message tới `:Database` giữ nguyên nội dung; luồng, nhánh `MSG`/`BR` và actor không đổi.

## Xuất ảnh

Từ thư mục gốc repository, sau khi sửa `.txt` — chỉ truyền file đã sửa:

```bash
python scripts/export_sequence.py UC70a UC70b
```

Lệnh xuất cả ảnh sequencediagram.org cạnh `.txt` lẫn tab trong
`TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/`
(trên Windows đặt `PYTHONUTF8=1` để script in được tiếng Việt). Mở từng ảnh kiểm tra không cắt; xác nhận
file gộp chỉ đổi tab đã sửa.
