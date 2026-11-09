# Báo cáo và thống kê — FE-15 (đã vào Report 3, chưa có code trên `main`)

Các sequence UC-70a, UC-70b tuân theo [convention chung](../README.md). Đặc tả UC-70a, UC-70b, BR-432 → BR-438 và
mã `MSG463`–`MSG466` đã vào Report 3 ngày 09/11/2026 (bản trích ở
[05-use-case-specifications.md](../../01-requirements/05-use-case-specifications.md)); lý do và chỗ lệch khỏi ticket ở
[NOTE-dac-ta-so-bo-scrum-38.md](../../notes/NOTE-dac-ta-so-bo-scrum-38.md) (SCRUM-38). UC-70 là UC gom, đặc tả nằm ở
các UC con.

| UC | Source | Ảnh |
|---|---|---|
| UC-70a View Reports & Statistics | [UC70a_ViewReportsAndStatistics.txt](UC70a_ViewReportsAndStatistics.txt) | [PNG](UC70a_ViewReportsAndStatistics.png) · [draw.io](../drawio-png/uc70a_viewreportsandstatistics.drawio.png) |
| UC-70b Export Report (`<<extend>>` UC-70a) | [UC70b_ExportReport.txt](UC70b_ExportReport.txt) | [PNG](UC70b_ExportReport.png) · [draw.io](../drawio-png/uc70b_exportreport.drawio.png) |

- Actor là mọi người dùng nội bộ; số liệu chỉ đếm cuộc họp người xem đọc được, Quản trị cũng vậy (BR-432).
- Kết quả chỉ có số đếm, tỷ lệ và trung bình — không tiêu đề, người dự hay nội dung cuộc họp (BR-437).
- UC-70b tính lại số liệu lúc xuất và ghi audit; không ghi được audit thì không trả tệp (BR-438).
- Đã xuất PNG và thêm 2 tab vào `TrustCabinet_Sequence_Diagrams.drawio` ngày 09/11/2026. Lifeline
  `:Report Controller` / `:Report Service` là tên dự kiến.

## Xuất ảnh

Từ thư mục gốc repository, sau khi sửa `.txt`:

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/reporting
```

```bash
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/reporting/*.txt
```

Lệnh thứ nhất xuất `.png` cạnh `.txt`; lệnh thứ hai ghép tab vào `TrustCabinet_Sequence_Diagrams.drawio` và xuất ảnh vào
`drawio-png/` (trên Windows đặt `PYTHONUTF8=1` để script in được tiếng Việt). Mở từng ảnh kiểm tra không cắt; xác nhận
file gộp chỉ thêm tab mới.
