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
| [0007](0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) | Ma trận phân quyền sửa được lúc chạy — bỏ `HasData` | Đã chấp nhận — phần huỷ phiên khi sửa ma trận thay thế bởi ADR-0022 |
| [0008](0008-vai-tro-la-du-lieu.md) | Vai trò là dữ liệu, không phải enum | Đã chấp nhận |
| [0009](0009-tu-lien-ket-google-lan-dau-theo-email.md) | Tự liên kết tài khoản Google ở lần đăng nhập đầu, theo email đã xác minh | Đề xuất |
| [0010](0010-phong-hop-truc-tuyen-dung-livekit-sfu.md) | Phòng họp trực tuyến dùng LiveKit (SFU) tự host | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013; quyền Guest thay thế bởi ADR-0016 |
| [0011](0011-tinh-nang-hop-la-panel-dung-chung.md) | Tính năng dùng trong lúc họp là panel dùng chung cho màn chi tiết và phòng họp | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013 |
| [0012](0012-giao-dien-hanh-chinh-sidebar-doc.md) | Giao diện phong cách hành chính, một sidebar dọc | Đề xuất — phần hình nhãn trạng thái và mức độ thay thế bởi ADR-0028 |
| [0013](0013-mot-cuoc-hop-la-mot-buoi-hop.md) | Một cuộc họp là một buổi họp — bỏ Meeting Session | Đã chấp nhận |
| [0014](0014-bieu-quyet-chi-trong-cuoc-hop.md) | Biểu quyết chỉ diễn ra trong cuộc họp, Chủ toạ ghi kết luận | Đã chấp nhận — phần loại Guest thay thế bởi ADR-0016, gate End Meeting thay thế bởi ADR-0017, chuẩn bị Vote nháp thay thế bởi ADR-0019 |
| [0015](0015-server-test-tren-lightsail.md) | Server test chung chạy Docker Compose trên một máy Lightsail, ở chế độ Development, tự deploy từ `main` | Đề xuất |
| [0016](0016-khach-tham-gia-nhu-thanh-vien.md) | Guest xác minh email tham gia như Member; điểm danh theo người được chọn | Đã chấp nhận — PO 13/10/2026; phần giữ BR-122/159 tại mục 6 được thay thế bởi ADR-0017 |
| [0017](0017-ket-thuc-hop-khong-phu-thuoc-diem-danh-bieu-quyet.md) | End Meeting độc lập với Vote/điểm danh; phục hồi Thư ký theo trạng thái | Đã chấp nhận — PO 13/10/2026 |
| [0018](0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md) | Tài liệu không có phiên bản — kho cá nhân và cuộc họp chỉ giữ tham chiếu tới file | Đã chấp nhận — PO 13/10/2026; phần khoá gỡ, AgendaItemId và số UC-33b thay thế bởi ADR-0020 |
| [0019](0019-chuan-bi-bieu-quyet-truoc-khi-hop.md) | Chuẩn bị biểu quyết trước khi họp và chốt điều kiện mở phiếu | Đã chấp nhận — xác nhận người dùng 14/10/2026 |
| [0020](0020-tai-lieu-theo-quyen-xem-cuoc-hop.md) | Tài liệu đi theo quyền xem cuộc họp; biểu quyết không gắn mục nghị sự | Đã chấp nhận — PO 15/10/2026 |
| [0021](0021-vong-doi-thanh-phan-va-y-dinh-gui-thu.md) | Lịch sử thành phần và ý định gửi thư cùng transaction | Đề xuất — implementation SCRUM-20 |
| [0022](0022-sua-quyen-vai-tro-khong-dang-xuat.md) | Sửa quyền của vai trò không đăng xuất ai — báo qua hub để trình duyệt tự refresh token | Đề xuất — SCRUM-105; phần đổi vai trò của một người thay thế bởi ADR-0023 |
| [0023](0023-doi-vai-tro-nguoi-dung-khong-dang-xuat.md) | Đổi vai trò của một người dùng cũng không đăng xuất — báo và áp dụng quyền mới ngay | Đề xuất — SCRUM-105, PO 22/10/2026 |
| [0024](0024-luu-file-tai-lieu-tren-cloudflare-r2.md) | Lưu file tài liệu trên Cloudflare R2, mọi byte đi qua backend | Đề xuất — SCRUM-107 |
| [0025](0025-version-rieng-cho-diem-danh.md) | Version riêng cho điểm danh; không dùng xmin toàn dòng | Đã chấp nhận trong phạm vi SCRUM-26, chờ team review branch |
| [0026](0026-tu-chay-nhan-dang-giong-noi.md) | Tự chạy nhận dạng giọng nói (chuyển thành chữ, tách người nói) trên một worker nội bộ | Đề xuất — SCRUM-31, nhận xét hội đồng 17/10/2026 |
| [0027](0027-voice-id-co-dong-y.md) | Voice ID có đồng ý — lưu giọng mẫu đã mã hoá, máy chỉ gợi ý tên, Thư ký quyết định | Đề xuất — SCRUM-35, nhận xét hội đồng 17/10/2026 |
| [0028](0028-trang-thai-la-icon-va-chu.md) | Trạng thái cuộc họp là icon và chữ, mức "Thường" không gắn chip | Đề xuất |
| [0029](0029-ghi-am-theo-micro-tung-nguoi.md) | Ghi âm theo micro từng người, chỉ lưu chữ của từng câu | Đề xuất — SCRUM-109 |
| [0030](0030-ghi-ca-cuoc-goi-tu-may-thu-ky.md) | Ghi cả cuộc gọi từ trình duyệt của Thư ký, báo đang ghi qua kênh dữ liệu của cuộc gọi | Đề xuất — SCRUM-44, PO 26/10/2026 |
| [0031](0031-gui-email-qua-resend.md) | Gửi email qua Resend, giữ hàng đợi trong app, nhận kết quả giao thư bằng webhook | Đề xuất — SCRUM-15, PO 27/10/2026 |
| [0032](0032-huy-cuoc-hop-va-tu-huy-khi-qua-han.md) | Trạng thái Cancelled, huỷ cuộc họp và tự huỷ khi quá 24 giờ sau giờ bắt đầu | Đề xuất — SCRUM-99, PO 27/10/2026 |
| [0033](0033-hang-doi-tai-len-o-trinh-duyet.md) | Hàng đợi tải lên ở trình duyệt, mỗi tệp một request, tự thử lại khi chắc chưa lưu | Đề xuất — SCRUM-58, PO 27/10/2026 |
