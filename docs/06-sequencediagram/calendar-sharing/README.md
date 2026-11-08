# Chia sẻ lịch và tìm giờ họp — FE-16 (sơ bộ, chưa có code)

Các sequence UC-84a → UC-84e, UC-85a → UC-85c tuân theo [convention chung](../README.md). Đặc tả sơ bộ nằm tại
[NOTE-dac-ta-so-bo-scrum-137-142.md](../../notes/NOTE-dac-ta-so-bo-scrum-137-142.md) (SCRUM-142 chia sẻ lịch,
SCRUM-137 tìm giờ họp), quyết định phạm vi ở [ADR-0058](../../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md); hai UC cha UC-84/UC-85 là UC gom, đặc tả nằm ở các UC con.

- Chỉ hiện giờ bận/rảnh: không tên cuộc họp, địa điểm, người dự hay mức mật (BR-427); bước lọc trong UC-85a/UC-85c
  chỉ giữ "meetings at shared levels", không nhắc gì khác.
- Khung trùng họp Mật vẫn cho tạo nháp; kiểm lại khi tạo dùng đúng dữ liệu đã chia sẻ (BR-428, D12).
- UC-84a/UC-84b/UC-84d kết thúc ở bước gửi; câu trả lời (chấp nhận/từ chối, duyệt/từ chối thay đổi) là UC-84c.
  UC-85b `<<extend>>` UC-85a (từ một khung đã tìm mới tạo nháp).
- Số hiệu dựa trên PR #279 merge trước (UC-83, BR-404 → BR-413, `MSG424`–`MSG434`); nếu nhánh này merge
  trước thì #279 dời số. Số UC-86..UC-90 trả lại, không dùng.

## Xuất ảnh

Từ thư mục gốc repository:

```bash
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/calendar-sharing/*.txt
```

Lệnh ghép tab vào `TrustCabinet_Sequence_Diagrams.drawio` và xuất ảnh vào `drawio-png/`. Mở từng ảnh kiểm tra
không cắt; chạy validate của drawio-skill trên file gộp, xác nhận chỉ thêm tab mới.
