# Workbook kiểm thử Report 5.x

Nguồn local của các workbook kiểm thử, đẩy lên Drive bằng `python scripts/push_reports.py <số>`:

| File trong thư mục này | File trên Drive | Khóa |
|---|---|---|
| `Report5.0_Test_Documentation.docx` | `Report-5.0_Test Documentation.docx` | `5.0` |
| `Report5.2_Integration_Test.xlsx` | `Report-5.2_Integration Test.xlsx` | `5.2` |

Tên local và tên trên Drive khác nhau, nên đích được xác định bằng file ID trong
`scripts/push_reports.py`, không theo tên file. Report 5.1, 5.3, 5.4 và 5.5 mới chỉ có trên Drive,
chưa có bản nguồn trong repo.

Quy tắc sửa, đồng bộ và phát hành nằm ở [README của 08-reports](../README.md).
