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
| [0005](0005-khach-ngoai-tham-du-bang-link-moi.md) | Khách ngoài hệ thống tham dự bằng link mời | Thay thế về quyền/xác minh bởi ADR-0016 |
| [0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md) | Kho tài liệu cá nhân và quy tắc đọc tài liệu | Đã chấp nhận — phần loại Guest thay thế bởi ADR-0016; phần phiên bản thay thế bởi ADR-0018; quy tắc đọc tài liệu cuộc họp thay thế bởi ADR-0020 |
| [0007](0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) | Ma trận phân quyền sửa được lúc chạy — bỏ `HasData` | Đã chấp nhận |
| [0008](0008-vai-tro-la-du-lieu.md) | Vai trò là dữ liệu, không phải enum | Đã chấp nhận |
| [0009](0009-tu-lien-ket-google-lan-dau-theo-email.md) | Tự liên kết tài khoản Google ở lần đăng nhập đầu, theo email đã xác minh | Đề xuất |
| [0010](0010-phong-hop-truc-tuyen-dung-livekit-sfu.md) | Phòng họp trực tuyến dùng LiveKit (SFU) tự host | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013; quyền Guest thay thế bởi ADR-0016 |
| [0011](0011-tinh-nang-hop-la-panel-dung-chung.md) | Tính năng dùng trong lúc họp là panel dùng chung cho màn chi tiết và phòng họp | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013 |
| [0012](0012-giao-dien-hanh-chinh-sidebar-doc.md) | Giao diện phong cách hành chính, một sidebar dọc | Đề xuất |
| [0013](0013-mot-cuoc-hop-la-mot-buoi-hop.md) | Một cuộc họp là một buổi họp — bỏ Meeting Session | Đã chấp nhận |
| [0014](0014-bieu-quyet-chi-trong-cuoc-hop.md) | Biểu quyết chỉ diễn ra trong cuộc họp, Chủ toạ ghi kết luận | Đã chấp nhận — phần loại Guest thay thế bởi ADR-0016, gate End Meeting thay thế bởi ADR-0017, chuẩn bị Vote nháp thay thế bởi ADR-0019 |
| [0015](0015-server-test-tren-lightsail.md) | Server test chung chạy Docker Compose trên một máy Lightsail, ở chế độ Development, tự deploy từ `main` | Đề xuất |
| [0016](0016-khach-tham-gia-nhu-thanh-vien.md) | Guest xác minh email tham gia như Member; điểm danh theo người được chọn | Đã chấp nhận — PO 13/10/2026; phần giữ BR-122/159 tại mục 6 được thay thế bởi ADR-0017 |
| [0017](0017-ket-thuc-hop-khong-phu-thuoc-diem-danh-bieu-quyet.md) | End Meeting độc lập với Vote/điểm danh; phục hồi Thư ký theo trạng thái | Đã chấp nhận — PO 13/10/2026 |
| [0018](0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md) | Tài liệu không có phiên bản — kho cá nhân và cuộc họp chỉ giữ tham chiếu tới file | Đã chấp nhận — PO 13/10/2026; phần khoá gỡ, AgendaItemId và số UC-33b thay thế bởi ADR-0020 |
| [0019](0019-chuan-bi-bieu-quyet-truoc-khi-hop.md) | Chuẩn bị biểu quyết trước khi họp và chốt điều kiện mở phiếu | Đã chấp nhận — xác nhận người dùng 14/10/2026 |
| [0020](0020-tai-lieu-theo-quyen-xem-cuoc-hop.md) | Tài liệu đi theo quyền xem cuộc họp; biểu quyết không gắn mục nghị sự | Đã chấp nhận — PO 15/10/2026 |
| [0021](0021-vong-doi-thanh-phan-va-y-dinh-gui-thu.md) | Lịch sử thành phần và ý định gửi thư cùng transaction | Đề xuất — implementation SCRUM-20 |
