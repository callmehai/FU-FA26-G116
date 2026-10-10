# Đồng bộ báo cáo

Google Drive là nguồn gốc. Tải bản DOCX mới nhất của từng report về thư mục `source/`, đặt tên như bảng dưới, rồi chạy từ thư mục gốc repo:

```powershell
python scripts/sync_reports.py
git diff -- docs/08-reports
```

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
