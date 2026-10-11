# Đồng bộ báo cáo

Có thể đồng bộ từ DOCX trong repo hoặc Google Drive. Mặc định script đọc thư mục `source/`; chỉ tải từ các link cấu hình khi thiếu report. Nhận cả tên `Report-1.docx` và `Report_1_Project_Introduction.docx` (tương tự Report 2, 3).

```powershell
python scripts/sync_reports.py
git diff -- docs/08-reports
```

Chọn nguồn cụ thể (đường dẫn tương đối tính từ gốc repo):

```powershell
python scripts/sync_reports.py --source docs/08-reports/source
python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3
python scripts/sync_reports.py --source "https://docs.google.com/document/d/FILE_ID/edit" --report 3
python scripts/sync_reports.py --source "https://drive.google.com/file/d/FILE_ID/view" --report 3
python scripts/sync_reports.py --source "https://drive.google.com/drive/folders/19wdKDAYViFGn4nlrmCnyNJkDQ0IOw-Z7?usp=drive_link"
```

Link file đơn cần `--report` để xác định Markdown đích. Thư mục local/Drive đồng bộ các report tìm thấy, không tự tải bù; thêm `--report 3` để chỉ lấy Report 3. Chỉ đọc file trực tiếp trong thư mục, không duyệt thư mục con. Nếu có hai file cùng số report, script báo lỗi để tránh chọn nhầm phiên bản.

Thư mục Drive và file riêng tư cần OAuth access token có quyền đọc Drive, đặt vào biến môi trường `GOOGLE_DRIVE_ACCESS_TOKEN` trước khi chạy. Token có thời hạn và không được ghi vào repo. Link Google Docs công khai dùng export DOCX; link DOCX công khai dùng download trực tiếp. Nếu Drive trả trang đăng nhập/xác nhận tải thay vì DOCX, script báo lỗi; dùng token cho trường hợp này. Google Docs export chịu giới hạn 10 MB của Drive API.

`--download` tải lại ba report từ các link cấu hình trong script. `--report3-source` vẫn được hỗ trợ cho lệnh cũ. Dùng `--output-dir <thư-mục>` để xem thử kết quả ở nơi khác. Script chuyển đổi toàn bộ nguồn đã chọn trước khi cập nhật Markdown; tải lỗi hoặc DOCX hỏng không ghi đè Markdown hiện có.

| Tên file trong `source/` | Tài liệu Drive | File Markdown được cập nhật |
|---|---|---|
| `Report-1.docx` | [Report 1 – Project Introduction](https://docs.google.com/document/d/1Lbw_A5CHPJzDa4kkLQFdjjn_c5KRIwOW/edit) | `Report-1_Project_Introduction.md` |
| `Report-2.docx` | [Report 2 – Project Management Plan](https://docs.google.com/document/d/1d4K3K3zcURku4SG84nXj7Zj6D-KiqGXa/edit) | `Report-2_Project_Management_Plan.md` |
| `Report-3.docx` | [Report 3 – Software Requirement Specification](https://docs.google.com/document/d/1rQuQxHWq4nJPJYMrrMlAZQ3P173PTXJz/edit) | `Report-3_Software_Requirement_Specification.md` |

Script dùng thư viện chuẩn của Python để lấy nội dung đoạn văn và bảng trong DOCX. Hình ảnh và bố cục trang không được xuất sang Markdown. Report 3 được giữ nguyên thành một file tổng; các tài liệu UC/BR/MSG riêng tiếp tục do agent cập nhật khi được giao.

**Lần đồng bộ gần nhất:** 10/10/2026, từ bản DOCX sau đợt gom UC (70 UC, FE-01 → FE-15, bỏ Meeting Session). Các file
trong [01-requirements/](../01-requirements/README.md) đã trích lại UC, BR theo bản này.

> **Mục "V.2 System Messages" của Report 3 không phải nguồn chuẩn.** Mã thông báo chuẩn duy nhất là
> [03-api/00-danh-muc-ma-thong-bao.md](../03-api/00-danh-muc-ma-thong-bao.md); khi hai bên lệch nhau, catalogue thắng.
