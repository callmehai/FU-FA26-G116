# ADR-0016: Khách đã xác minh email tham gia như Thành viên

- **Trạng thái:** Đã chấp nhận theo quyết định PO trong phiên làm việc ngày 13/10/2026
- **Ngày:** 2026-10-13
- **Người quyết định:** PO
- **Thay thế:** ADR-0005 về xác minh và quyền Guest, xác nhận thêm khi mời vào cuộc họp mật; phần loại Guest khỏi quyền đọc tài liệu của ADR-0006; phần token Guest chỉ nghe và chưa cho xem người trong phòng của ADR-0010; phần loại Guest khỏi biểu quyết của ADR-0014. Các phần khác của các ADR sau vẫn áp dụng.

## Bối cảnh

Guest là người ngoài được mời dự một cuộc họp, không phải người chỉ vào nghe. Quyền chỉ nghe trong ADR-0005 và các lệnh cấm tài liệu/biểu quyết không còn phù hợp với quyết định PO. Report 3 cũng đã bỏ việc bắt điểm danh đủ trước khi kết thúc cuộc họp (BR-179).

## Quyết định

1. Thư ký mời theo tên và email. Guest xác minh quyền kiểm soát email bằng OTP, sau đó tự nhập tên/danh xưng hiển thị và chức vụ. Chỉ email đã được xác minh; tên/chức vụ là tự khai, không được trình bày như danh tính đã xác thực. Không chốt thêm field bắt buộc, validation hay schema trong đợt này.
2. Trong đúng cuộc họp được mời, Guest dùng micro, camera, chia sẻ màn hình, chat, xem người đang kết nối; xem tài liệu và phiên bản có sẵn qua cuộc họp, bình luận, phản hồi lời mời và bỏ phiếu như Member. Guest được Thư ký điểm danh như Member.
3. Thư ký có thể cấp thêm quyền trong cuộc họp cho người đã có role, bổ sung vào quyền mặc định của role; Guest được cấp như Member. Xem roster và phản hồi lời mời là ví dụ, không giới hạn chỉ hai quyền này. Catalogue quyền và ma trận quyền được cấp theo role chưa chốt. UC-40 là danh sách người đang trong phòng, khác UC-27 là roster lời mời. Không cấp vai trò Chair/Secretary, quyền được ủy quyền, điều phối phòng hay kho cá nhân cho Guest.
4. Guest đọc toàn bộ tài liệu đã gắn vào cuộc họp theo quy tắc của Member. Không thêm cờ hoặc luồng “chia sẻ riêng cho Guest”. Quy tắc tải xuống theo mức mật áp dụng giống nhau; không mở kho cá nhân hoặc phiên bản chưa được cung cấp qua cuộc họp.
5. Mời vào cuộc họp Mật/Tối mật chỉ có cảnh báo trong form bình thường, không thêm bước xác nhận hay luồng phê duyệt. Vẫn ghi người mời, thời gian và mức mật để truy vết.
6. BR-172 cho lưu điểm danh một hoặc nhiều người được chọn. Người không gửi giữ nguyên giá trị đã lưu hoặc Chưa điểm danh. Một submission vẫn lưu nguyên khối để tránh mất dữ liệu. BR-179/MSG155 ngừng dùng; không tái cấp. BR-170 (chỉ sửa trong InProgress), BR-159 (đóng phiếu trước khi kết thúc) và BR-122 giữ nguyên; BR-122 đang được phân tích, chưa có quyết định sửa.

## Hệ quả

- Report 3 DOCX, Markdown, các đặc tả UC đang có, BR và sequence hiện có phản ánh cùng quyết định này. UC placeholder vẫn là placeholder; không viết đặc tả mới hoặc quyết định chi tiết schema trong đợt này. Đây là yêu cầu đích, không khẳng định API/DB đã thực thi.
- Profile, quyền bổ sung, bình luận và phiếu Guest tham chiếu lời mời theo cuộc họp, không dựa vào tên tự khai hoặc lượt kết nối. Rejoin không sinh quyền bỏ phiếu lần hai. Thu hồi chặn hành động mới, giữ lịch sử đã ghi nhận.
- Receipt phiếu và lựa chọn phiếu lưu cùng nhau. Phiếu kín không lưu liên kết danh tính Guest hoặc User với lựa chọn. Quyền đọc, thời hạn và thu hồi được kiểm tra ở mỗi request; Member/Guest không được vượt quy tắc trạng thái cuộc họp hay phiếu.
- Token/phân quyền phòng cho Guest phải cho phép publish media và chat theo cùng quy tắc Member; không áp dụng `canPublish = false`, `canPublishData = false` chỉ vì là Guest như ADR-0010 cũ. Danh sách người đang kết nối không cần ẩn danh hóa riêng khi có Guest; profile vẫn được ghi rõ là tự khai.
- Quyết định này không bỏ yêu cầu về thông báo/đồng ý xử lý audio, mục đích sử dụng, thời hạn lưu giữ và xóa dữ liệu. Các nghĩa vụ hiện có đối với audio của Guest được giữ như với người nội bộ; việc lưu profile không tự tạo bằng chứng đồng ý ghi âm.
- Jira SCRUM-20 (roster/lời mời), SCRUM-26 (điểm danh), SCRUM-22 (tài liệu) và các ticket Guest/room/voting cần đối chiếu khi triển khai. Phiên sửa tài liệu này không đăng tin hoặc sửa Jira.
