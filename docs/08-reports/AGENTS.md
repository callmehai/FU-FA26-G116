# Quy tắc chỉnh sửa report DOCX

Áp dụng cho mọi DOCX trong `docs/08-reports/source/` và Markdown snapshot tương ứng.

## Xác nhận file và phạm vi

- Trước khi ghi, nêu rõ đường dẫn tuyệt đối của DOCX đích, report/UC/table hoặc trang cần sửa, và các đầu ra liên quan (Markdown, Drive). Nếu người dùng đã chỉ rõ chính xác, nhắc lại phạm vi rồi tiếp tục; nếu có nhiều bản có thể là nguồn chuẩn hoặc vị trí/phạm vi còn mơ hồ, hỏi và chờ xác nhận trước khi sửa.
- Kiểm tra `git status` và diff hiện tại trước khi sửa. Giữ nguyên thay đổi có sẵn của người dùng; không khôi phục, thay thế hoặc đồng bộ từ Drive khi chưa được yêu cầu.
- Chỉ sửa vùng đã xác nhận. Không tiện tay chuẩn hóa nội dung, style, bảng, ảnh hoặc các UC khác. Chỉ cập nhật Markdown hay thay file Drive khi yêu cầu bao gồm các đầu ra đó. Khi thay Drive, xác minh đúng file ID/link và giữ nguyên ID.

## Style UC Specification

- Với UC trong Report 3, dùng UC-23a thuộc FE-05 làm mẫu: giữ bố cục bảng hiện tại; các nhãn trường/mục và tiêu đề AF/EX theo đúng cách in đậm của mẫu; nội dung flow giữ regular. Chỉ dùng italic khi mẫu UC tương ứng có dùng; không tự thêm nhấn mạnh.
- Report 3 dùng Times New Roman xuyên suốt tài liệu. Khi chỉ sửa một UC, giữ nguyên các định dạng khác; chỉ chuẩn hóa font toàn file khi yêu cầu phạm vi toàn file.
- Alternative Flow có nội dung thì đặc tả các bước như mẫu; không có Alternative Flow thì ghi `N/A`. Exception Flow phải mô tả điều kiện phát sinh, kết quả dữ liệu/trạng thái, phản hồi hệ thống và cách kết thúc/tiếp tục use case đủ chi tiết.
- Với report khác Report 3, giữ style sẵn có của report đó; không áp style UC-23a hoặc Times New Roman sang tài liệu khác nếu chưa được yêu cầu.

## Bảo toàn DOCX

- Trước khi sửa, lập mốc kiểm tra cho nội dung ngoài phạm vi và cấu trúc file: văn bản, số lượng bảng/đoạn, section, header/footer, hình/shape, media và relationship. Tạo bản sao tạm ngoài thư mục report để có thể đối chiếu/khôi phục.
- Dùng cách chỉnh sửa giữ nguyên các phần DOCX không liên quan. Không lưu lại toàn bộ DOCX bằng thư viện chưa kiểm chứng khả năng giữ ảnh, shape, header/footer, field, comment và các thành phần OOXML khác.
- Sau khi sửa, so sánh nội dung ngoài phạm vi với mốc ban đầu; xác minh cấu trúc, relationship và media không đổi trừ phần được yêu cầu. Kiểm tra DOCX mở được và các ảnh vẫn hiện diện. Nếu phát hiện mất/thay đổi ngoài dự kiến, khôi phục từ bản tạm, chọn cách sửa khác và kiểm tra lại trước khi báo hoàn tất.
- Nếu đồng bộ Markdown được yêu cầu, chỉ chạy `scripts/sync_reports.py` với đúng DOCX và số report; rà diff để chắc chỉ report được yêu cầu thay đổi. Script Markdown không giữ bố cục trang, ảnh, font, bold hoặc italic của DOCX.

## Báo cáo hoàn tất

Nêu file và vùng đã sửa, các kiểm tra bảo toàn nội dung/ảnh đã chạy, kết quả đồng bộ Markdown/Drive (nếu được yêu cầu), và giới hạn kiểm tra hiển thị nếu không mở/render DOCX thực tế.
