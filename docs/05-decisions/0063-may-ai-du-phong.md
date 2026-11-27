# ADR-0063: Máy AI local dự phòng cho dự thảo biên bản

- **Trạng thái:** Đề xuất — PO (Hải) 14/11/2026; điểm 5 (nút "Thử kết nối") thay thế bởi [ADR-0088](0088-bo-cau-hinh-ai-chi-dung-may-ai-cua-nhom.md)
- **Ngày:** 2026-11-15
- **Người quyết định:** Hải (PO)
- **Bổ sung:** [ADR-0054](0054-ai-du-thao-bien-ban-chay-tai-cho.md) (AI viết dự thảo chạy tại chỗ),
  [ADR-0056](0056-ai-du-thao-qua-api-cho-hop-thuong.md) (API cho cuộc họp Thường)

## Bối cảnh

Dự thảo biên bản của cuộc họp Nội bộ, Mật chỉ được viết bằng máy AI của nhóm (ADR-0054, ADR-0056): lời thoại không ra
khỏi Tailscale. Máy đó là một chiếc MacBook. Máy tắt, hết pin, ngủ hay rời Tailscale thì dự thảo của mọi cuộc họp Nội bộ,
Mật báo "Không kết nối được máy AI" cho tới khi máy bật lại. Hôm bảo vệ, chuyện đó không được xảy ra.

Thêm nữa, máy rời Tailscale không từ chối kết nối mà im lặng: server chờ tới hết thời hạn của cả dự thảo (15 phút) mới
báo lỗi.

## Quyết định

1. **Thêm một máy dự phòng** chạy cùng model, cũng của nhóm, cũng qua Tailscale: `Ai:BackupBaseUrl`
   (`AI_BACKUP_URL` trong `deploy/.env`). Để trống thì mọi thứ như cũ. Cùng `Ai:Model`, cùng `Ai:ApiKey`.
2. **Chỉ chuyển khi máy chính không kết nối được** (không nhận kết nối, hoặc không trả lời trong thời hạn). Máy chính trả
   lỗi (ví dụ 500, thiếu model) thì không chuyển: lỗi đó là do cấu hình, máy dự phòng cũng sẽ báo y như vậy hoặc che mất
   lỗi cần sửa.
3. **Chờ kết nối tối đa 10 giây** (`Ai:ConnectTimeoutSeconds`). Ollama đang chạy nhận kết nối ngay, nên 10 giây đủ
   cả khi Tailscale phải đi vòng qua relay.
4. **Máy chính không kết nối được thì 2 phút sau mới hỏi lại.** Trong 2 phút đó máy dự phòng được hỏi trước, nên một dự
   thảo dài nhiều mục không phải chờ 10 giây cho từng mục. Máy dự phòng cũng không kết nối được thì máy chính được hỏi
   tiếp; cả hai đều không được thì dự thảo báo "Không kết nối được máy AI chính lẫn máy dự phòng".
5. **Nút "Thử kết nối" ở trang Máy AI hỏi cả hai máy cùng lúc** và nói rõ máy nào trả lời, để quản trị viên biết trước
   buổi họp máy dự phòng có thật sự thay được không.

## Hệ quả

- Lời thoại cuộc họp Nội bộ, Mật vẫn không ra khỏi Tailscale: máy dự phòng cũng là máy của nhóm. Không trỏ
  `AI_BACKUP_URL` sang dịch vụ AI bên ngoài.
- Máy dự phòng nhận lời thoại cuộc họp như máy chính, nên chủ máy phải giữ máy như máy chính: chỉ Ollama nghe trên IP
  Tailscale, không mở ra mạng LAN.
- Biên bản ghi tên model như cũ; không ghi máy nào đã viết. Hai máy chạy cùng model nên kết quả cùng loại.
- Trạng thái "máy chính đang tắt" nằm trong bộ nhớ của app, khởi động lại app thì quên: lần gọi đầu sau đó lại thử máy
  chính trước.
