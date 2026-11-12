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
| [0006](0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md) | Kho tài liệu cá nhân và quy tắc đọc tài liệu | Đã chấp nhận — phần loại Guest thay thế bởi ADR-0016; phần phiên bản thay thế bởi ADR-0018; quy tắc đọc tài liệu cuộc họp thay thế bởi ADR-0020; hạn mức kho chốt bởi ADR-0034, quyền đọc kho tài khoản khoá chốt bởi ADR-0035 |
| [0007](0007-ma-tran-phan-quyen-sua-duoc-luc-chay.md) | Ma trận phân quyền sửa được lúc chạy — bỏ `HasData` | Đã chấp nhận — phần huỷ phiên khi sửa ma trận thay thế bởi ADR-0022 |
| [0008](0008-vai-tro-la-du-lieu.md) | Vai trò là dữ liệu, không phải enum | Đã chấp nhận |
| [0009](0009-tu-lien-ket-google-lan-dau-theo-email.md) | Tự liên kết tài khoản Google ở lần đăng nhập đầu, theo email đã xác minh | Đề xuất |
| [0010](0010-phong-hop-truc-tuyen-dung-livekit-sfu.md) | Phòng họp trực tuyến dùng LiveKit (SFU) tự host | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013; quyền Guest thay thế bởi ADR-0016 |
| [0011](0011-tinh-nang-hop-la-panel-dung-chung.md) | Tính năng dùng trong lúc họp là panel dùng chung cho màn chi tiết và phòng họp | Đã chấp nhận — phần Meeting Session thay thế bởi ADR-0013 |
| [0012](0012-giao-dien-hanh-chinh-sidebar-doc.md) | Giao diện phong cách hành chính, một sidebar dọc | Đề xuất — phần hình nhãn trạng thái và mức độ thay thế bởi ADR-0028; kích thước nút, ô nhập và bo góc thay thế bởi ADR-0043; nhóm Nghiệp vụ thay thế bởi ADR-0046 |
| [0013](0013-mot-cuoc-hop-la-mot-buoi-hop.md) | Một cuộc họp là một buổi họp — bỏ Meeting Session | Đã chấp nhận |
| [0014](0014-bieu-quyet-chi-trong-cuoc-hop.md) | Biểu quyết chỉ diễn ra trong cuộc họp, Chủ toạ ghi kết luận | Đã chấp nhận — phần loại Guest thay thế bởi ADR-0016, gate End Meeting thay thế bởi ADR-0017, chuẩn bị Vote nháp thay thế bởi ADR-0019 |
| [0015](0015-server-test-tren-lightsail.md) | Server test chung chạy Docker Compose trên một máy Lightsail, ở chế độ Development, tự deploy từ `main` | Đề xuất — phần chạy ở Development, tự migrate khi khởi động và không backup thay thế bởi ADR-0047 |
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
| [0027](0027-voice-id-co-dong-y.md) | Voice ID có đồng ý — lưu giọng mẫu đã mã hoá, máy chỉ gợi ý tên, Thư ký quyết định | Đề xuất — SCRUM-35, nhận xét hội đồng 17/10/2026; điểm 2 (không lưu âm thanh mẫu) thay thế bởi ADR-0042; "Thư ký quyết định" ở giọng khớp rõ thay thế bởi ADR-0045 |
| [0028](0028-trang-thai-la-icon-va-chu.md) | Trạng thái cuộc họp là icon và chữ, mức "Thường" không gắn chip | Đề xuất — chữ màu mực và bỏ "Thường" ở đầu trang chi tiết thay thế bởi ADR-0043 |
| [0029](0029-ghi-am-theo-micro-tung-nguoi.md) | Ghi âm theo micro từng người, chỉ lưu chữ của từng câu | Đề xuất — SCRUM-109; điểm 3 thay thế bởi ADR-0039, điểm 1 và 5 thay thế bởi ADR-0040 |
| [0030](0030-ghi-ca-cuoc-goi-tu-may-thu-ky.md) | Ghi cả cuộc gọi từ trình duyệt của Thư ký, báo đang ghi qua kênh dữ liệu của cuộc gọi | Thay thế bởi ADR-0040 |
| [0031](0031-gui-email-qua-resend.md) | Gửi email qua Resend, giữ hàng đợi trong app, nhận kết quả giao thư bằng webhook | Đề xuất — SCRUM-15, PO 27/10/2026 |
| [0032](0032-huy-cuoc-hop-va-tu-huy-khi-qua-han.md) | Trạng thái Cancelled, huỷ cuộc họp và tự huỷ khi quá 24 giờ sau giờ bắt đầu | Đề xuất — SCRUM-99, PO 27/10/2026 |
| [0033](0033-hang-doi-tai-len-o-trinh-duyet.md) | Hàng đợi tải lên ở trình duyệt, mỗi tệp một request, tự thử lại khi chắc chưa lưu | Đề xuất — SCRUM-58, PO 27/10/2026 |
| [0034](0034-han-muc-luu-tru-toan-he-thong.md) | Hạn mức lưu trữ tài liệu 10 GB cho cả hệ thống thay cho 1 GB mỗi người | Đề xuất — SCRUM-58, PO 27/10/2026 |
| [0035](0035-quan-tri-vien-doc-kho-tai-khoan-khoa.md) | Quản trị viên đọc kho của tài khoản đã khoá, có nhật ký kiểm toán, không thấy tệp cuộc họp Confidential/Secret | Đề xuất — SCRUM-58, PO 27/10/2026 |
| [0036](0036-may-chuyen-chu-tren-may-nhom-qua-tailscale.md) | Server test gọi máy chuyển chữ chạy trên máy của nhóm qua Tailscale, có khoá; bản ghi nằm trên volume | Đề xuất — SCRUM-31, PO 28/10/2026 |
| [0037](0037-them-dau-cau-bang-vibert-capu.md) | Thêm dấu câu và chữ hoa vào bản gỡ băng bằng viBERT-capu chạy trong worker | Đề xuất — SCRUM-31, PO 28/10/2026 |
| [0038](0038-danh-dau-tieng-lot-giua-cac-micro.md) | Bê từ AI-eCabinet: đánh dấu tiếng lọt giữa các micro riêng, loại câu khỏi bản ghi, giữ câu chưa gửi trên máy, mỗi người một tab ghi | Đề xuất — SCRUM-109, PO 28/10/2026 |
| [0039](0039-luu-am-thanh-tung-cau-de-nghe-lai.md) | Lưu âm thanh từng câu ghi theo micro để nghe lại, như AI-eCabinet; nghe qua link ký có hạn, xoá cùng chữ | Đề xuất — SCRUM-109, PO 29/10/2026; phần "Đồng ý" thay thế bởi ADR-0040 |
| [0040](0040-cach-ghi-am-theo-hinh-thuc-hop.md) | Cách ghi âm suy ra từ hình thức họp: trực tuyến ghi theo micro từng người, trực tiếp ghi bằng mic phòng; bỏ chọn cách ghi, bỏ ghi trộn cả cuộc gọi | Đề xuất — SCRUM-109, PO 29/10/2026; điểm 4 thay thế một phần bởi ADR-0041 |
| [0041](0041-mic-phong-ghi-tung-luot.md) | Mic phòng ghi từng lượt, đẩy lên từng phần trong khi ghi, nối thành một bản ghi của cuộc họp và chuyển chữ lại cả bản, như AI-eCabinet | Đề xuất — SCRUM-29, PO 29/10/2026 |
| [0042](0042-luu-ban-ghi-giong-mau-de-nghe-lai.md) | Lưu bản ghi giọng mẫu đã mã hoá để chính chủ nghe lại, như AI-eCabinet; thông báo đồng ý lên 1.1 | Đề xuất — SCRUM-35, PO 29/10/2026 |
| [0043](0043-thanh-phan-theo-ai-ecabinet-giu-sidebar.md) | Lấy một số thành phần của AI-eCabinet, giữ sidebar dọc và nhãn trạng thái cũ: kích thước control, nút gọi hành động có gợn sóng, trạng thái tô cả dòng, danh sách và hộp thoại tạo cuộc họp 2 cột | Đề xuất — PO 30/10/2026; nửa câu "Tìm giờ họp" thay thế bởi ADR-0058 |
| [0044](0044-server-kiem-cau-truc-va-dem-trang-pdf.md) | Server kiểm cấu trúc PDF và tự đếm số trang bằng PdfPig, không tin số trang trình duyệt gửi | Đề xuất — SCRUM-107, test E2E 02/11/2026 |
| [0045](0045-may-tu-gan-ten-khi-giong-khop-ro.md) | Máy tự gán tên người nói khi giọng khớp rõ; giọng thứ hai của cùng một người phải khớp cao hơn | Đề xuất — SCRUM-35, PO 01/11/2026 |
| [0046](0046-trang-ca-nhan-vao-menu-tai-khoan.md) | Trang cá nhân (Kho tài liệu, Giọng nói của tôi) vào menu tài khoản, lối tắt ghi giọng mẫu ở màn chờ vào phòng | Đề xuất — PO 02/11/2026 |
| [0047](0047-server-test-chay-staging-deploy-an-toan.md) | Server test chạy ở `Staging`; deploy migrate có chủ đích, backup database và tự quay lại khi health check đỏ | Đề xuất |
| [0048](0048-thong-bao-voice-id-1-2-may-tu-dien-ten.md) | Thông báo đồng ý Voice ID lên 1.2; máy chỉ tự điền tên người đã đồng ý bản này, người đồng ý bản cũ vẫn chỉ được gợi ý | Đề xuất — SCRUM-35, PO 02/11/2026 |
| [0049](0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md) | Vô hiệu hoá tài khoản kết thúc Voice ID; máy so giọng dưới khoá trên dòng đồng ý | Đề xuất — SCRUM-35, review 02/11/2026 |
| [0050](0050-giong-gan-trung-nhan-cung-ten.md) | Giọng chưa có tên giống giọng đầu của một người đã nhận ra từ 0,8 trở lên thì nhận cùng tên, bất kể điểm so với giọng mẫu | Đề xuất — SCRUM-35, PO 03/11/2026 |
| [0051](0051-khach-ghi-theo-micro-cua-minh.md) | Khách trong phòng họp trực tuyến cũng được ghi theo micro của mình: câu gắn với lời mời, chỉ nhận sau khi Khách xác nhận thông báo ghi âm, Khách chỉ thấy câu của mình | Đề xuất — SCRUM-129, PO 01/11/2026 |
| [0052](0052-chip-cung-bo-goc-giai-thich-sau-icon-i.md) | Mọi chip cùng bo góc 4px; câu giải thích nằm sau icon (i) (`InfoTip`), không giấu trong tooltip của chữ thường | Đề xuất — PO 03/11/2026 |
| [0053](0053-nut-tai-khoan-khong-to-sang.md) | Nút tài khoản ở chân sidebar không tô sáng khi đang ở trang cá nhân (thay một phần ADR-0046) | Đề xuất — PO 04/11/2026 |
| [0054](0054-ai-du-thao-bien-ban-chay-tai-cho.md) | Dự thảo và biên bản theo AI-eCabinet (một mẫu cố định); AI chạy tại chỗ qua API chuẩn OpenAI, lời thoại chia theo chương trình | Đề xuất — SCRUM-36, SCRUM-32, PO 04/11/2026; điểm 7 và phần trình soạn ở mục Hệ quả (biên bản là văn bản thuần) thay thế bởi ADR-0055 |
| [0055](0055-bien-ban-luu-html-soan-nhu-word.md) | Biên bản lưu HTML đã lọc theo danh sách cho phép, soạn bằng trình soạn thảo kiểu Word (TipTap); server lọc mọi lần ghi, dòng văn bản cũ đổi sang HTML khi đọc, Word và PDF đọc HTML | Đề xuất — SCRUM-32, PO 05/11/2026 |
| [0056](0056-ai-du-thao-qua-api-cho-hop-thuong.md) | Quản trị chọn máy AI viết dự thảo: máy local hoặc API chuẩn OpenAI (key lưu mã hoá); chỉ cuộc họp Thường gửi ra API, Nội bộ/Mật luôn ở máy local | Đề xuất — SCRUM-36, PO 06/11/2026 |
| [0057](0057-ba-muc-mat-nhu-ai-ecabinet.md) | Cuộc họp còn 3 mức mật như AI-eCabinet: Thường, Nội bộ, Mật; bỏ Hạn chế (họp đang Hạn chế thành Mật), luật "Hạn chế hoặc Mật" nay chỉ áp cho Mật, bỏ `document.downloaded` | Đề xuất — PO 07/11/2026 |
| [0058](0058-chia-se-lich-va-tim-gio-hop.md) | Có chia sẻ lịch (chỉ bận/rảnh, người có `MeetingCreator` xin, chủ lịch đồng ý; 2 mốc, họp Mật không bao giờ chia sẻ) và Tìm giờ họp theo số của eCab, gom thành FE-16; không có trợ lý AI | Đề xuất — SCRUM-142, SCRUM-137, PO 07/11/2026 |
| [0059](0059-giu-ba-project-backend-tach-application-sau-bao-cao.md) | Backend giữ ba project Web → Infrastructure → Domain đến buổi báo cáo giảng viên 13/11/2026; tài liệu vẽ đúng ba project; tách `TrustCabinet.Application` theo module ngay sau đó, không đổi API, `MSGnn` hay schema | Đề xuất — SCRUM-143 |
| [0060](0060-thu-ky-thu-voice-id-ho.md) | Thư ký thu Voice ID hộ bằng micro phòng họp: bộ mẫu riêng dưới một lần đồng ý Voice ID của chính chủ, quyền `VoiceCollector`, chỉ Thư ký hiện tại của chính cuộc họp trực tiếp/kết hợp không Mật; chủ mẫu được báo, nghe, xoá, rút | Đề xuất — SCRUM-35, PO 12/11/2026 |
| [0061](0061-hop-ket-hop-mot-dong-thoi-gian.md) | Họp kết hợp: phòng chỉ ghi trực tiếp bằng mic phòng (không nhận tệp tải lên), người trực tuyến ghi theo micro của mình, bản dự thảo đọc một dòng thời gian và bỏ tiếng loa phòng lặp lại lời người trực tuyến; họp xong server trộn mic phòng và mọi mic trực tuyến thành một bản ghi tổng hợp để nghe lại; không làm chế độ "Máy phòng họp" | Đề xuất — SCRUM-153, PO 12/11/2026 |
