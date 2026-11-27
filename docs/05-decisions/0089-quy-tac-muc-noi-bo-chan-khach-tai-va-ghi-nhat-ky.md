# ADR-0089: Mức Nội bộ chặn Khách tải tài liệu và ghi nhật ký khi tải tài liệu, biên bản

- **Trạng thái:** Đề xuất — Hải 27/11/2026
- **Ngày:** 2026-11-27
- **Người quyết định:** Hải (PO)
- **Liên quan:** [ADR-0057](0057-ba-muc-mat-nhu-ai-ecabinet.md) (3 mức mật),
  [ADR-0058](0058-chia-se-lich-va-tim-gio-hop.md) (chia sẻ lịch 2 mốc),
  [ADR-0088](0088-bo-cau-hinh-ai-chi-dung-may-ai-cua-nhom.md) (bỏ đường API AI ngoài)

## Bối cảnh

Sau khi [ADR-0088](0088-bo-cau-hinh-ai-chi-dung-may-ai-cua-nhom.md) bỏ đường gọi API AI bên ngoài, mọi cuộc họp ở cả ba
mức (`Normal`, `Internal`, `Secret`) đều chạy dự thảo bằng máy AI của nhóm. Khi đó giữa **Thường (`Normal`)** và
**Nội bộ (`Internal`)** chỉ còn một khác biệt duy nhất là phạm vi chia sẻ lịch bận/rảnh (Mốc 1 và Mốc 2, ADR-0058),
dễ gây câu hỏi vì sao phải tách hai mức nếu cơ chế bảo vệ tài liệu và truy vết giống hệt nhau.

## Quyết định

Bổ sung hai quy tắc nghiệp vụ cho cuộc họp mức **Nội bộ (`Internal`)**, tạo thành bậc thang bảo mật 3 cấp rõ ràng:

1. **Khách mời (Guest) không được tải tài liệu của cuộc họp Nội bộ về máy (chỉ xem trực tuyến).**
   - Khách có lời mời hợp lệ vẫn đọc tài liệu cuộc họp `Internal` trực tuyến và góp ý như trước (khi cuộc họp `Published`
     hoặc `InProgress`), nhưng nút tải bị ẩn và API tải về (`?download=true`) từ chối với mã `403 MSG485`
     (*"Khách mời chỉ được xem trực tuyến, không được tải tài liệu của cuộc họp Nội bộ."*).
   - Thành viên nội bộ của cuộc họp `Internal` vẫn được tải tài liệu về máy.
2. **Ghi nhật ký kiểm toán (`AuditLog`) khi thành viên nội bộ tải tài liệu hoặc biên bản của cuộc họp Nội bộ.**
   - Tải tài liệu cuộc họp `Internal` (`UC-35b`) ghi sự kiện `document.downloaded` trước khi trả tệp.
   - Tải biên bản Word/PDF cuộc họp `Internal` (`UC-69`, `UC-71`) ghi sự kiện `minutes.downloaded` trước khi trả tệp.
   - *(Ghi âm `UC-51c` và bản gỡ băng `UC-54b` vốn đã ghi nhật ký khi tải và chỉ dành cho Chủ toạ/Thư ký, không cho Khách.)*

Bảng tổng hợp 3 mức mật sau quyết định này:

| Quy tắc nghiệp vụ | Thường (`Normal`) | Nội bộ (`Internal`) | Mật (`Secret`) |
|---|---|---|---|
| Chia sẻ lịch bận/rảnh | Mốc 1 trở lên | Mốc 2 | Không bao giờ chia sẻ |
| Thành viên nội bộ tải tài liệu, biên bản | Được tải (không ghi nhật ký) | Được tải + **ghi nhật ký** | Cấm tải (`MSG159`, `MSG405`) |
| Khách mời (Guest) tải tài liệu | Được tải | **Cấm tải (`MSG485`)**, chỉ xem trực tuyến | Cấm tải (`MSG159`), chỉ xem trực tuyến |
| Ghi nhật ký xem tài liệu (`document.viewed`) | Không | Không | Có (mỗi lượt xem) |
| Ẩn khỏi kho khi Quản trị xem tài khoản đã khoá | Không | Không | Có |
| Cảnh báo khi mời Khách | Không | Không | Có |

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Kết luận |
|---|---|---|---|
| Chỉ giải thích bằng câu chữ về chia sẻ lịch | Không phải sửa tài liệu thiết kế hay BR | Hai mức Thường và Nội bộ quá gần nhau về bảo mật tài liệu | Bỏ |
| Thêm cả chặn Khách tải lẫn ghi nhật ký khi tải cho mức Nội bộ | Đúng nghĩa "Nội bộ" (Khách không mang tệp ra ngoài; nội bộ tải được nhưng có truy vết), không cần đổi schema DB | Cần cập nhật đặc tả UC-35b, UC-69, UC-71, BR-010/055/081, MSG485 và test report | **Chọn** |

## Hệ quả

- Cấp mã mới `MSG485` (`403`) cho tình huống Khách yêu cầu tải tài liệu cuộc họp Nội bộ.
- Cập nhật `BR-010`, `BR-055`, `BR-081`, `UC-35b`, `UC-69`, `UC-71`, đặc tả màn hình FE-07, mục IV Report 3, sequence
  diagram (`UC-35b`, `UC-69`) và Report 1, 2, 4, 5.
- Bảng `AuditLog` đã lưu chuỗi `Action` và JSON `Details`, không cần migration cơ sở dữ liệu.

