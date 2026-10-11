# 05 · Quyết định kiến trúc (ADR)

Mỗi quyết định kỹ thuật một file `NNNN-tieu-de.md`, đánh số tăng dần, dùng
[mẫu sẵn](0000-template.md).

ADR đã viết thì **không sửa nội dung**. Đổi ý thì viết ADR mới và sửa trạng thái ADR cũ thành
*Thay thế bởi ADR-NNNN*. Mục đích là giữ lại lý do tại thời điểm quyết định — hữu ích khi bảo vệ
đồ án, vì hội đồng hay hỏi "sao chọn cái này".

## Danh sách

| # | Tiêu đề | Trạng thái |
|---|---|---|
| [0001](0001-tach-frontend-backend.md) | Tách frontend và backend thành hai thư mục | Đã chấp nhận |
| [0002](0002-khung-giao-dien-theo-ai-ecabinet.md) | Dựng khung giao diện bám theo AI-eCabinet | Thay thế một phần bởi ADR-0012 |
| [0003](0003-chon-postgresql-thay-cho-sql-server.md) | Chọn PostgreSQL thay cho SQL Server | Đã chấp nhận |
| [0004](0004-mot-to-chuc-khong-co-don-vi.md) | Một tổ chức, không có đơn vị trực thuộc | Đã chấp nhận |
| [0005](0005-khach-ngoai-tham-du-bang-link-moi.md) | Khách ngoài hệ thống tham dự bằng link mời | Đã chấp nhận |
| [0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md) | Kho tài liệu cá nhân và quy tắc đọc tài liệu | Đã chấp nhận |
| [0007](0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) | Ma trận phân quyền sửa được lúc chạy — bỏ `HasData` | Đã chấp nhận |
| [0008](0008-vai-tro-la-du-lieu.md) | Vai trò là dữ liệu, không phải enum | Đã chấp nhận |
| [0009](0009-tu-lien-ket-google-lan-dau-theo-email.md) | Tự liên kết tài khoản Google ở lần đăng nhập đầu, theo email đã xác minh | Đề xuất |
| [0010](0010-phong-hop-truc-tuyen-dung-livekit-sfu.md) | Phòng họp trực tuyến dùng LiveKit (SFU) tự host | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013 |
| [0011](0011-tinh-nang-hop-la-panel-dung-chung.md) | Tính năng dùng trong lúc họp là panel dùng chung cho màn chi tiết và phòng họp | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013 |
| [0012](0012-giao-dien-hanh-chinh-sidebar-doc.md) | Giao diện phong cách hành chính, một sidebar dọc | Đề xuất |
| [0013](0013-mot-cuoc-hop-la-mot-buoi-hop.md) | Một cuộc họp là một buổi họp — bỏ Meeting Session | Đã chấp nhận |
| [0014](0014-bieu-quyet-chi-trong-cuoc-hop.md) | Biểu quyết chỉ diễn ra trong cuộc họp, Chủ toạ ghi kết luận | Đã chấp nhận |
