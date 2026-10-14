# Đồng bộ báo cáo

## Quy tắc nguồn và đích

- DOCX trong `source/` là bản local chuẩn để đồng bộ Report 1–3 ra Markdown.
- Markdown chỉ được sinh từ DOCX local bằng `scripts/sync_reports.py`. Không tải DOCX từ Drive để tạo hoặc cập nhật Markdown.
- Khi được yêu cầu đưa report lên Drive, dùng DOCX local tương ứng để **thay nội dung file Drive hiện có tại chỗ**. Không tạo bản sao, không xóa/tạo lại file và không đổi tên hoặc di chuyển file; phải giữ nguyên Drive file ID và link.
- Chỉ thay file đúng theo bảng link chuẩn bên dưới. Trước khi ghi, xác nhận tên file và ID từ Drive khớp với bảng; sau khi ghi, kiểm tra cùng ID/link vẫn mở được và kích thước/thời gian cập nhật phản ánh bản mới.
- Việc thay DOCX trên Drive không cập nhật Markdown. Việc tạo Markdown không đọc hay ghi Drive.

## Đồng bộ Markdown từ DOCX local

Đồng bộ các DOCX có sẵn trong `source/`:

```powershell
python scripts/sync_reports.py --source docs/08-reports/source
git diff -- docs/08-reports
```

Chỉ đồng bộ Report 3:

```powershell
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3
git diff -- docs/08-reports/Report-3_Software_Requirement_Specification.md
```

Script nhận tên `Report-1.docx` và `Report_1_Project_Introduction.docx` (tương tự Report 2, 3), chỉ đọc file DOCX trực tiếp trong thư mục nguồn, và báo lỗi nếu có nhiều file cùng số report. Có thể dùng `--output-dir <thư-mục>` để xem thử Markdown ở nơi khác. Script chuyển đổi văn bản, tiêu đề, danh sách và bảng; ảnh, định dạng ký tự và bố cục trang không được thể hiện đầy đủ trong Markdown. Report 3 được giữ trong một snapshot Markdown tổng.

## Link Drive chuẩn Report 1–7

Các link dưới đây là đích cố định. Khi cập nhật report trên Drive, thay nội dung đúng file ở link tương ứng, giữ nguyên file ID và URL.

| Report | Tên file trên Drive | Link Drive |
|---|---|---|
| 1 | `Report_1_Project_Introduction.docx` | [Report 1 – Project Introduction](https://docs.google.com/document/d/15ncrJ-7eInb0vLIOXmRfZsugiPkunGW6/edit) |
| 2 | `Report_2_Project_Management_Plan.docx` | [Report 2 – Project Management Plan](https://docs.google.com/document/d/1oyefDV6vVOBY0qtO4sCQVMcoj3tDHKqo/edit) |
| 3 | `Report_3_Software_Requirement_Specification.docx` | [Report 3 – Software Requirement Specification](https://docs.google.com/document/d/1JNNCwt0Yc_KLS9dPB6wtP8HW6x7D1fWl/edit) |
| 4 | `Report-4_Software Design Specification.docx` | [Report 4 – Software Design Specification](https://docs.google.com/document/d/1vPEW9IKH_5GZmkLOz9lkYRZdKHp-cKzd/edit) |
| 5 | `Report-5.0_Test Documentation.docx` | [Report 5.0 – Test Documentation](https://docs.google.com/document/d/1hokAKFTmcJAmdnrJuKBfN4Z1zptNJSIy/edit) |
| 6 | `Report-6_Software User Guides.docx` | [Report 6 – Software User Guides](https://docs.google.com/document/d/1d_z9XEz_rOqqop6W0Et_Q0kywj492EHG/edit) |
| 7 | `Report-7_Final Project Document.docx` | [Report 7 – Final Project Document](https://docs.google.com/document/d/1jXB0xKyxTkIQ7X_qodNX7H0MWYnGo8Fi/edit) |

Report 5 còn có các workbook kiểm thử riêng (Unit Test, Integration Test, System Test và Acceptance Test); chúng không phải file đích của DOCX Report 5.0.

Thư mục Drive chứa các report: [SEP490-G116 Reports](https://drive.google.com/drive/folders/19wdKDAYViFGn4nlrmCnyNJkDQ0IOw-Z7?usp=drive_link).

**Lần đồng bộ Markdown gần nhất:** 10/10/2026, từ bản DOCX sau đợt gom UC (70 UC, FE-01 → FE-15, bỏ Meeting Session). Các file trong [01-requirements/](../01-requirements/README.md) đã trích lại UC, BR theo bản này.

> **Mục "V.2 System Messages" của Report 3 không phải nguồn chuẩn.** Mã thông báo chuẩn duy nhất là [03-api/00-danh-muc-ma-thong-bao.md](../03-api/00-danh-muc-ma-thong-bao.md); khi hai bên lệch nhau, catalogue thắng.
