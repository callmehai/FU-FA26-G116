# FE-11 — Bản tiếng Việt để theo dõi và review UC, BR, MSG

**Ngày tổng hợp:** 15/10/2026

Nội dung dưới đây được diễn giải sang tiếng Việt đơn giản để tiện review. Mã UC/BR/MSG và ý nghiệp vụ được giữ nguyên. Đây là bản dịch tham khảo; nội dung chuẩn nằm trong các file nguồn liên kết bên dưới.

## Nguồn chuẩn

- [Đặc tả Use Case](../01-requirements/05-use-case-specifications.md#fe-11--voting-management)
- [Danh mục Business Rules](../01-requirements/06-business-rules.md)
- [Danh mục System Messages](../03-api/00-danh-muc-ma-thong-bao.md)
- [Report 3 Markdown](../08-reports/Report-3_Software_Requirement_Specification.md)
- [Ghi chú bàn giao màn hình FE-11](NOTE-cap-nhat-man-hinh-fe11.md)

## Phạm vi

- UC-52a, UC-52b, UC-53, UC-54, UC-55, UC-56, UC-57.
- BR-29, BR-159, BR-160, BR-161, BR-165, BR-181, BR-189, BR-190, BR-252–BR-274, BR-285.
- MSG205–MSG236; catalogue đánh dấu các mã là dành sẵn, chưa triển khai.
- BR-159 và BR-264 còn liên hệ UC-22/UC-59, nhưng ảnh hưởng đến biểu quyết nên vẫn được đưa vào.

## Đặc tả Use Case

## FE-11 — Quản lý biểu quyết

Chủ tọa hiện tại được chuẩn bị phiếu Nháp khi cuộc họp Đã duyệt, Đã công bố hoặc Đang diễn ra. Chỉ được mở phiếu và bỏ phiếu khi cuộc họp đang diễn ra. Mỗi cuộc họp có tối đa một phiếu đang mở; muốn mở phiếu phải có ít nhất một người đủ điều kiện. Kết thúc cuộc họp không tự đóng phiếu. Chủ tọa có thể đóng, tổng hợp kết quả và ghi kết luận khi cuộc họp đang diễn ra hoặc đã kết thúc. Mỗi phiếu thuộc về một cuộc họp, không gắn với mục nghị sự.

### UC-52a — Xem danh sách phiếu biểu quyết

- **Người thực hiện:** Quản trị viên hệ thống, Người tổ chức, Người duyệt, Chủ tọa, Thư ký, Người tham dự, Khách mời.
- **Điều kiện trước:** Người dùng nội bộ đã đăng nhập, hoặc Khách mời đã xác minh email và lời mời còn hiệu lực; người dùng có quyền xem phiếu. Chủ tọa hiện tại được vào phần chuẩn bị phiếu khi cuộc họp Đã duyệt, nhưng không được xem các hồ sơ bị hạn chế khác. (BR-29, BR-181, BR-274)
- **Kết quả:** Chỉ hiện phiếu được phép xem; không thay đổi phiếu, lá phiếu hay kết luận.
- **Luồng chính:** Mở khu vực Biểu quyết → hệ thống kiểm tra quyền hiện tại → liệt kê phiếu thuộc cuộc họp, chỉ hiện phiếu Nháp cho Chủ tọa hiện tại và chỉ đếm phiếu đang nhìn thấy → hiện nội dung, hình thức, trạng thái; phiếu đang mở hiện số người đã bỏ, phiếu đã đóng có kết quả/kết luận → chọn phiếu để sang UC-52b. (BR-253, BR-260, BR-285)
- **AF-1 — Không có phiếu được xem:** Nếu không có phiếu nào hiển thị, kể cả chỉ có Nháp bị ẩn, báo MSG207 (“Không có phiếu biểu quyết để hiển thị.”); không tiết lộ phiếu ẩn, kết thúc thành công.
- **EX-1 — Không có quyền/truy cập:** Cuộc họp không còn truy cập được, mất quyền hoặc lời mời Khách mời hết hạn/bị thu hồi. Báo MSG205 (“Không thể truy cập phiếu biểu quyết của cuộc họp.”), không tiết lộ dữ liệu; kết thúc.
- **EX-2 — Không tải được danh sách:** Quyền còn hợp lệ nhưng không lấy được danh sách. Báo MSG206 (“Không tải được danh sách phiếu biểu quyết. Vui lòng thử lại.”); không đổi dữ liệu, kết thúc.

### UC-52b — Xem trạng thái và kết quả phiếu

- **Người thực hiện:** Quản trị viên hệ thống, Người tổ chức, Người duyệt, Chủ tọa, Thư ký, Người tham dự, Khách mời.
- **Điều kiện trước:** Đã đăng nhập hoặc có lời mời Khách mời đã xác minh, còn hiệu lực; hiện có quyền xem phiếu; phiếu thuộc cuộc họp và được phép xem. Chỉ Chủ tọa hiện tại được xem phiếu Nháp. (BR-29, BR-181, BR-253, BR-274)
- **Kết quả:** Hiện đúng trạng thái và thông tin được phép xem; không đổi phiếu, lá phiếu hay kết luận.
- **Luồng chính — Phiếu đang mở:** Người dùng chọn phiếu → hệ thống kiểm tra quyền → hiện cuộc họp, nội dung, lựa chọn, công khai/kín, vai trò, giờ mở và tổng số người đủ điều kiện đã chốt → hiện số phiếu đã nhận trên tổng số đó. Khi còn mở, không hiện lựa chọn cá nhân hay kết quả theo phương án. Số người đã bỏ được cập nhật; phiếu kín không cho biết ai vừa bỏ hoặc chọn gì. Nếu cuộc họp đã kết thúc thì báo không nhận thêm phiếu nhưng vẫn cho xem số đã bỏ. (BR-165, BR-190, BR-259, BR-260, BR-285)
- **AF-1 — Xem phiếu Nháp:** Chỉ Chủ tọa hiện tại chọn được. Hiện nội dung, lựa chọn, hình thức và vai trò; chưa chốt người đủ điều kiện, chưa có phiếu. Không thay đổi dữ liệu. (BR-190, BR-253)
- **AF-2 — Xem phiếu Đã đóng:** Hiện giờ mở/đóng, số người đủ điều kiện, số phiếu đã nhận và số lượng theo phương án. Tỷ lệ tính trên số phiếu đã gửi; nếu chưa có phiếu thì hiện số 0, không hiện tỷ lệ. Phiếu công khai hiện lựa chọn từng người hoặc “chưa bỏ”; phiếu kín chỉ hiện tổng số. Hiện kết luận/ghi chú nếu đã ghi, nếu chưa thì nêu chưa có; hệ thống không tự suy kết luận. Không đổi dữ liệu. (BR-161, BR-259, BR-261, BR-263, BR-273)
- **EX-1 — Không được xem phiếu:** Phiếu/cuộc họp không truy cập được, mất quyền hoặc người xem không phải Chủ tọa hiện tại khi xem Nháp. Báo MSG205, không tiết lộ phiếu; kết thúc.
- **EX-2 — Không tải được trạng thái/kết quả:** Quyền còn hợp lệ nhưng không lấy được dữ liệu. Báo MSG208 (“Không tải được trạng thái hoặc kết quả phiếu. Vui lòng thử lại.”); dữ liệu không đổi, kết thúc.
### UC-53 — Tạo và cấu hình phiếu

- **Người thực hiện:** Chủ tọa.
- **Mô tả:** Chủ tọa hiện tại tạo, sửa hoặc xóa phiếu Nháp; hoặc chuyển thẳng sang mở phiếu. Được chuẩn bị Nháp khi cuộc họp Đã duyệt, Đã công bố hoặc Đang diễn ra. Phiếu thuộc cuộc họp, không gắn với mục nghị sự. (BR-285)
- **Điều kiện trước:** Chủ tọa đã đăng nhập, hiện có quyền; cuộc họp ở một trong ba trạng thái trên. Khi sửa/xóa, phiếu thuộc cuộc họp và đang là Nháp. (BR-189, BR-252, BR-254)
- **Kết quả:** Lưu tạo/cập nhật Nháp; xóa chỉ xóa Nháp đã chọn. Mở theo UC-54 chỉ ghi nhận khi mở thành công, không tạo riêng bản Nháp. Phiếu, lá phiếu và kết luận khác không đổi.
- **Luồng chính — Tạo Nháp:** Chủ tọa chọn Tạo phiếu → hệ thống kiểm tra quyền và trạng thái, mở biểu mẫu nội dung/lựa chọn/hình thức/nhóm vai trò. Không chọn mục nghị sự. Mặc định công khai, có Đồng ý/Không đồng ý/Bỏ phiếu trắng và chọn cả bốn vai trò → Chủ tọa nhập nội dung, lựa chọn, hình thức và vai trò → chọn Lưu nháp → hệ thống kiểm tra nội dung không trống, ít nhất hai lựa chọn khác nhau và không trống, hình thức hợp lệ, có ít nhất một vai trò → lưu Nháp, chưa mở phiếu và chưa chốt người đủ điều kiện → báo MSG210 (“Đã tạo phiếu nháp.”). (BR-190, BR-252, BR-253, BR-255, BR-256, BR-257, BR-272, BR-285)
- **AF-1 — Sửa Nháp:** Chủ tọa chọn Sửa → hệ thống xác nhận quyền, trạng thái và phiếu vẫn là Nháp → sửa thông tin, chọn Lưu → kiểm tra lại → cập nhật và báo MSG211 (“Đã cập nhật phiếu nháp.”). Không mở biểu quyết.
- **AF-2 — Xóa Nháp:** Chủ tọa chọn Xóa và xác nhận → hệ thống kiểm tra quyền, trạng thái và phiếu còn là Nháp → chỉ xóa phiếu đó, báo MSG212 (“Đã xóa phiếu nháp.”). Phiếu khác không đổi. (BR-189)
- **AF-3 — Mở ngay:** Từ biểu mẫu tạo/sửa, Chủ tọa chọn Mở phiếu khi cuộc họp đang diễn ra. Thông tin đã hiện nên thao tác là xác nhận mở; chuyển thẳng UC-54 AF-1, không lưu Nháp trước và không hỏi xác nhận lần nữa. Nếu mở lỗi, giữ nội dung trên biểu mẫu, không tạo Nháp mới và không sửa Nháp cũ.
- **EX-1 — Dữ liệu không hợp lệ:** Nội dung/lựa chọn/hình thức/vai trò sai. Báo MSG209 (“Thông tin phiếu chưa hợp lệ. Hãy kiểm tra các trường được đánh dấu.”), không lưu; Chủ tọa sửa rồi tiếp tục.
- **EX-2 — Không còn được quản lý Nháp:** Chủ tọa mất vai trò, cuộc họp sai trạng thái hoặc phiếu không còn là Nháp. Báo MSG213; không tạo/sửa/xóa, kết thúc.
- **EX-3 — Không tạo được:** Không lưu được Nháp. Báo MSG214; không có Nháp mới, kết thúc.
- **EX-4 — Không cập nhật được:** Nháp đã lưu giữ nguyên. Báo MSG215, kết thúc.
- **EX-5 — Không xóa được:** Nháp giữ nguyên. Báo MSG216, kết thúc.
- **EX-6 — Không truy cập được:** Cuộc họp/phiếu không truy cập được hoặc mất quyền; Nháp chỉ Chủ tọa hiện tại được xem. Báo MSG205, không tiết lộ và không đổi dữ liệu, kết thúc.

### UC-54 — Mở phiếu biểu quyết

- **Người thực hiện:** Chủ tọa.
- **Điều kiện trước:** Chủ tọa hiện tại có quyền; cuộc họp đang diễn ra; phiếu thuộc cuộc họp và là Nháp (hoặc mở từ biểu mẫu UC-53); chưa có phiếu nào khác đang mở. (BR-160, BR-266, BR-285)
- **Kết quả:** Phiếu Đang mở, có giờ mở; chốt danh sách và số người đủ điều kiện theo vai trò đã chọn. Người đủ điều kiện được gửi một phiếu khi cuộc họp đang diễn ra và phiếu còn mở. (BR-190)
- **Luồng chính:** Chủ tọa chọn Mở trên Nháp → hệ thống kiểm tra quyền và hiện thông tin để xác nhận → Chủ tọa xác nhận → hệ thống kiểm tra lại Chủ tọa, trạng thái cuộc họp, nội dung, lựa chọn, vai trò, phiếu vẫn là Nháp và không có phiếu khác đang mở → tìm người giữ vai trò được chọn, có quyền vào cuộc họp; Khách mời phải xác minh email và lời mời còn hiệu lực; phải có ít nhất một người, điểm danh không ảnh hưởng → mở phiếu, ghi giờ và chốt danh sách → báo MSG217 (“Đã mở biểu quyết.”). (BR-160, BR-190, BR-255–BR-257, BR-266, BR-267, BR-272, BR-285)
- **AF-1 — Mở từ biểu mẫu tạo/sửa:** Chọn Mở phiếu ngay trên biểu mẫu UC-53; không lặp lại bước xem và xác nhận. Hệ thống dùng dữ liệu biểu mẫu, kiểm tra như luồng chính. Nếu sửa, kiểm tra phiếu cũ vẫn thuộc cuộc họp và là Nháp. Chỉ ghi phiếu/dữ liệu sửa khi mở thành công; lỗi thì giữ nguyên Nháp. Tiếp tục ở bước ghi nhận mở. (BR-254, BR-285)
- **EX-1 — Cấu hình sai:** Báo MSG209; không mở, Nháp cũ giữ nguyên; quay lại UC-53 sửa.
- **EX-2 — Không còn được phép mở:** Chủ tọa không còn là người hiện tại, cuộc họp không còn đang diễn ra hoặc phiếu không còn là Nháp. Báo MSG218; dữ liệu giữ nguyên, kết thúc.
- **EX-3 — Đã có phiếu khác đang mở:** Báo MSG219; phiếu đang chọn không mở và phiếu kia không đổi, kết thúc. (BR-266)
- **EX-4 — Không có người đủ điều kiện:** Báo MSG220; không mở, Nháp giữ nguyên, kết thúc. (BR-267)
- **EX-5 — Không mở được:** Không ghi giờ mở/danh sách đủ điều kiện; không tạo phiếu mới hoặc sửa Nháp cũ. Báo MSG221, kết thúc.
- **EX-6 — Không truy cập được:** Báo MSG205, không tiết lộ phiếu bị ẩn và không đổi dữ liệu, kết thúc. (BR-253)
### UC-55 — Bỏ phiếu

- **Người thực hiện:** Chủ tọa, Thư ký, Người tham dự, Khách mời.
- **Điều kiện trước:** Đã đăng nhập hoặc là Khách mời có email đã xác minh/lời mời còn hiệu lực; có quyền vào cuộc họp và nằm trong danh sách đã chốt; cuộc họp đang diễn ra, phiếu đang mở; chưa bỏ phiếu trước đó. (BR-165, BR-190, BR-258, BR-268, BR-269)
- **Kết quả:** Ghi nhận một lựa chọn, tăng số người đã bỏ; phiếu không sửa/rút lại. Phiếu kín không nối danh tính với lựa chọn. (BR-259, BR-265)
- **Luồng chính:** Mở phiếu → hệ thống kiểm tra quyền, điều kiện, trạng thái và việc đã bỏ chưa; nếu được thì hiện nội dung/lựa chọn/hình thức và nhắc phiếu đã gửi không sửa được → chọn đúng một phương án và Gửi → hệ thống kiểm tra lại quyền, phương án thuộc phiếu, trạng thái cuộc họp/phiếu, danh sách đã chốt và chưa bỏ → ghi nhận, đánh dấu đã bỏ và cập nhật số lượng; phiếu kín không gắn người với lựa chọn → báo MSG227 (“Đã ghi nhận phiếu của bạn.”). Phiếu kín chỉ hiện xác nhận đã gửi, không hiện lựa chọn đã chọn. (BR-165, BR-258, BR-259, BR-265, BR-268, BR-269, BR-271)
- **AF:** Không có.
- **EX-1 — Chưa chọn phương án:** Không ghi phiếu; báo MSG222; quay lại chọn phương án. (BR-271)
- **EX-2 — Phương án không thuộc phiếu:** Không ghi và không tăng số lượng; báo MSG223; quay lại bước kiểm tra phiếu.
- **EX-3 — Không có trong danh sách:** Người dùng xem được phiếu nhưng không thuộc danh sách đã chốt; người được thêm sau khi mở không tự được thêm. Không ghi phiếu mới, phiếu cũ giữ nguyên; báo MSG224, kết thúc. (BR-268, BR-269)
- **EX-4 — Hết nhận phiếu:** Phiếu không còn mở hoặc cuộc họp không còn đang diễn ra, kể cả khi màn hình vẫn hiện Đang mở. Không ghi và không tăng số lượng; báo MSG225, kết thúc. (BR-165)
- **EX-5 — Đã bỏ phiếu:** Kể cả mở lại màn hình hoặc vào lại cuộc họp, không nhận phiếu thứ hai; phiếu cũ giữ nguyên. Báo MSG226. Với phiếu kín chỉ xác nhận đã gửi, không tiết lộ lựa chọn qua danh tính. Kết thúc.
- **EX-6 — Không ghi được:** Không nhận phiếu, không đánh dấu đã bỏ, không tăng số lượng. Báo MSG228, kết thúc.
- **EX-7 — Không truy cập được:** Báo MSG205; không tiết lộ phiếu bị ẩn và không đổi dữ liệu, kết thúc. (BR-253)

### UC-56 — Đóng phiếu và tổng hợp kết quả

- **Người thực hiện:** Chủ tọa.
- **Điều kiện trước:** Chủ tọa hiện tại có quyền; cuộc họp đang diễn ra hoặc đã kết thúc; phiếu thuộc cuộc họp và đang mở. (BR-270)
- **Kết quả:** Phiếu Đã đóng, ghi giờ đóng và kết quả; không nhận thêm phiếu, giữ nguyên phiếu đã gửi; không tự ghi kết luận. (BR-259, BR-263)
- **Luồng chính:** Chủ tọa chọn Đóng biểu quyết → hệ thống kiểm tra quyền, hiện số đã bỏ và hỏi xác nhận → Chủ tọa xác nhận → hệ thống kiểm tra lại quyền/trạng thái → ngừng nhận phiếu, ghi giờ đóng, đếm theo phương án. Tỷ lệ tính trên số phiếu đã gửi; nếu không có phiếu thì số lượng bằng 0, không hiện tỷ lệ → lưu trạng thái và kết quả, giữ kín phiếu kín → báo MSG229 và hiện kết quả theo UC-52b. Chủ tọa có thể sang UC-57 ghi kết luận. (BR-259, BR-261, BR-270)
- **AF-1 — Đóng phiếu sau khi họp kết thúc:** Nếu cuộc họp đã kết thúc nhưng phiếu vẫn mở, từ lúc kết thúc không nhận phiếu mới. Kết thúc cuộc họp không tự đóng phiếu và không đổi phiếu đã gửi. Tiếp tục luồng chính. (BR-159, BR-165)
- **EX-1 — Không còn được phép đóng:** Chủ tọa mất vai trò, trạng thái cuộc họp không hợp lệ hoặc phiếu không còn mở. Báo MSG230; dữ liệu/kết quả cũ giữ nguyên, kết thúc.
- **EX-2 — Không đóng hoặc đếm được:** Phiếu vẫn mở, không ghi giờ đóng/kết quả mới; giữ các phiếu đã gửi. Báo MSG231. Nếu cuộc họp đã kết thúc thì vẫn không nhận phiếu mới. Kết thúc.
- **EX-3 — Không truy cập được:** Báo MSG205; không tiết lộ dữ liệu ẩn, không đổi phiếu/lá phiếu/kết luận, kết thúc. (BR-253)

### UC-57 — Ghi kết luận biểu quyết

- **Người thực hiện:** Chủ tọa.
- **Điều kiện trước:** Chủ tọa hiện tại có quyền; cuộc họp đang diễn ra hoặc đã kết thúc; phiếu đã đóng và có kết quả; chưa ghi kết luận. (BR-161, BR-262)
- **Kết quả:** Lưu kết luận Đạt/Không đạt/Hoãn, ghi chú tùy chọn, người ghi và thời điểm. Sau đó không sửa/thay kết luận; kết quả và kết luận được đưa vào biên bản, phiếu đã gửi không đổi. (BR-264)
- **Luồng chính:** Chủ tọa xem kết quả phiếu đã đóng → hệ thống kiểm tra quyền và chưa có kết luận, cho chọn Đạt/Không đạt/Hoãn và nhập ghi chú tùy chọn → Chủ tọa chọn, xác nhận → hệ thống kiểm tra lại quyền, trạng thái, phiếu đã đóng/có kết quả, lựa chọn hợp lệ và chưa có kết luận → lưu thông tin người ghi/thời điểm, không đổi phiếu/kết quả → báo MSG235; kết luận chỉ xem được, dùng trong biên bản. (BR-161, BR-262–BR-264)
- **AF-1 — Ghi sau khi họp kết thúc:** Chủ tọa vẫn ghi kết luận cho phiếu đã đóng/tổng hợp. Không mở biểu quyết mới, không nhận phiếu mới; tiếp tục luồng chính.
- **EX-1 — Kết luận không hợp lệ:** Chưa chọn hoặc chọn ngoài Đạt/Không đạt/Hoãn. Không lưu; báo MSG232; quay lại chọn.
- **EX-2 — Đã có kết luận:** Giữ nguyên kết luận, ghi chú và kết quả; báo MSG233, kết thúc. (BR-262)
- **EX-3 — Không còn đủ điều kiện:** Không còn là Chủ tọa hiện tại, cuộc họp sai trạng thái hoặc phiếu chưa đóng/chưa có kết quả. Không lưu; báo MSG234, kết thúc. (BR-161)
- **EX-4 — Không ghi được:** Không lưu kết luận/ghi chú/người ghi/thời điểm; phiếu và kết quả giữ nguyên. Báo MSG236, kết thúc.
- **EX-5 — Không truy cập được:** Báo MSG205; không tiết lộ dữ liệu ẩn và không đổi dữ liệu, kết thúc. (BR-253)
## Business Rules

Các câu dưới đây được dịch từ danh mục BR để tiện review. Nội dung chuẩn nằm trong [danh mục BR](../01-requirements/06-business-rules.md).

| Đã rà soát | BR | Quy tắc tiếng Việt | UC áp dụng |
| --- | --- | --- | --- |
| [ ] | BR-29 | Chỉ người có quan hệ được phép với cuộc họp mới được vào: Người tổ chức, Người duyệt được phân công, Chủ tọa, Thư ký hoặc Người tham dự. Chỉ đăng nhập thôi chưa đủ quyền. | UC-16, UC-17, UC-26, UC-27, UC-30, UC-32a, UC-37, UC-68 |
| [ ] | BR-159 | Kết thúc cuộc họp không yêu cầu phải đóng phiếu và cũng không tự đóng phiếu; các lá phiếu đã gửi được giữ nguyên. | UC-22 |
| [ ] | BR-160 | Chỉ Chủ tọa hiện tại được mở phiếu Nháp, khi cuộc họp đang diễn ra. | UC-54 |
| [ ] | BR-161 | Chủ tọa hiện tại được ghi kết luận Đạt/Không đạt/Hoãn, kèm ghi chú tùy chọn, cho phiếu đã đóng và có kết quả khi cuộc họp đang diễn ra hoặc đã kết thúc. | UC-52b, UC-57 |
| [ ] | BR-165 | Chỉ được bỏ phiếu khi cuộc họp đang diễn ra, phiếu đang mở và người bỏ vẫn có quyền vào cuộc họp. | UC-55 |
| [ ] | BR-181 | Khách mời có quyền tham gia như Người tham dự trong cuộc họp mà họ được mời. | UC-04, UC-27, UC-28a, UC-28b, UC-29, UC-30, UC-31, UC-32a, UC-32c, UC-35, UC-37–UC-42, UC-52a, UC-52b, UC-55 |
| [ ] | BR-189 | Chủ tọa hiện tại được xóa phiếu Nháp khi cuộc họp Đã duyệt/Đã công bố/Đang diễn ra. Không được xóa phiếu Đang mở hoặc Đã đóng. | UC-53 |
| [ ] | BR-190 | Chốt danh sách và số người đủ điều kiện khi mở phiếu, theo vai trò đã chọn và quyền vào cuộc họp. Điểm danh không quyết định điều kiện bỏ phiếu. | UC-54, UC-55 |
| [ ] | BR-252 | Chỉ Chủ tọa hiện tại được tạo/cấu hình phiếu Nháp khi cuộc họp Đã duyệt/Đã công bố/Đang diễn ra. | UC-53 |
| [ ] | BR-253 | Chỉ Chủ tọa hiện tại được xem phiếu Nháp. | UC-52a, UC-52b, UC-53 |
| [ ] | BR-254 | Không sửa nội dung, lựa chọn, hình thức hoặc vai trò của phiếu sau khi phiếu đã mở/đóng. | UC-53 |
| [ ] | BR-255 | Nội dung phiếu không được trống. | UC-53, UC-54 |
| [ ] | BR-256 | Phiếu có ít nhất hai lựa chọn khác nhau và không trống. | UC-53, UC-54 |
| [ ] | BR-257 | Chủ tọa chọn một hoặc nhiều vai trò Chủ tọa, Thư ký, Thành viên, Khách mời; mặc định chọn cả bốn. | UC-53, UC-54 |
| [ ] | BR-258 | Mỗi người đủ điều kiện chỉ gửi tối đa một lá phiếu cho mỗi lượt biểu quyết. | UC-55 |
| [ ] | BR-259 | Phiếu kín ghi nhận người đã tham gia nhưng không nối họ với lựa chọn; chỉ công bố kết quả tổng hợp. | UC-52b, UC-55, UC-56 |
| [ ] | BR-260 | Khi phiếu đang mở, chỉ hiện số người đã bỏ; không hiện lựa chọn cá nhân hay kết quả theo phương án. | UC-52b |
| [ ] | BR-261 | Đóng phiếu thì đếm tất cả phiếu đã nhận theo phương án. Tỷ lệ tính trên số phiếu gửi; nếu chưa có phiếu thì số lượng bằng 0 và không hiện tỷ lệ. | UC-52b, UC-56 |
| [ ] | BR-262 | Không sửa hoặc thay kết luận và ghi chú đã ghi. | UC-57 |
| [ ] | BR-263 | Chủ tọa tự quyết định kết luận; hệ thống không suy kết luận từ kết quả kiểm phiếu. | UC-56, UC-57 |
| [ ] | BR-264 | Biên bản cuộc họp có kết quả kiểm phiếu và kết luận do Chủ tọa ghi. | UC-57, UC-59 |
| [ ] | BR-265 | Lá phiếu đã ghi không được sửa hoặc rút lại. | UC-55 |
| [ ] | BR-266 | Mỗi cuộc họp có tối đa một phiếu đang mở tại cùng thời điểm. | UC-54 |
| [ ] | BR-267 | Không mở phiếu nếu không có người đủ điều kiện. | UC-54 |
| [ ] | BR-268 | Đổi vai trò sau khi mở không đổi danh sách/số người đủ điều kiện đã chốt. | UC-54, UC-55 |
| [ ] | BR-269 | Mất quyền vào cuộc họp thì không gửi được phiếu mới; phiếu đã gửi không bị xóa. | UC-55 |
| [ ] | BR-270 | Chủ tọa hiện tại được đóng và đếm phiếu đang mở khi cuộc họp đang diễn ra hoặc đã kết thúc. | UC-56 |
| [ ] | BR-271 | Một lá phiếu chọn đúng một phương án thuộc phiếu đó. | UC-55 |
| [ ] | BR-272 | Mỗi phiếu dùng hình thức công khai hoặc kín. | UC-53, UC-54 |
| [ ] | BR-273 | Kết quả phiếu công khai đã đóng hiện lựa chọn của từng người đủ điều kiện hoặc cho biết họ chưa bỏ phiếu. | UC-52b |
| [ ] | BR-274 | Chủ tọa hiện tại được vào phần chuẩn bị phiếu của cuộc họp Đã duyệt; quyền này không mở quyền xem hồ sơ bị hạn chế khác. | UC-52a, UC-52b, UC-53 |
| [ ] | BR-285 | Mỗi phiếu thuộc một cuộc họp và không gắn với mục nghị sự. | UC-52a, UC-52b, UC-53, UC-54, UC-55, UC-56, UC-57 |

## System Messages

Bản dịch tiếng Việt để review. Catalogue chuẩn được liên kết ở đầu tài liệu.

| Đã rà soát | MSG | UC / luồng | Nội dung tiếng Việt · Trạng thái |
| --- | --- | --- | --- |
| [ ] | MSG205 | UC-52a/52b EX-1; UC-53/54 EX-6; UC-55 EX-7; UC-56 EX-3; UC-57 EX-5 · Dành sẵn | Không thể truy cập phiếu biểu quyết của cuộc họp. |
| [ ] | MSG206 | UC-52a EX-2 · Dành sẵn | Không tải được danh sách phiếu biểu quyết. Vui lòng thử lại. |
| [ ] | MSG207 | UC-52a AF-1 · Dành sẵn | Không có phiếu biểu quyết để hiển thị. |
| [ ] | MSG208 | UC-52b EX-2 · Dành sẵn | Không tải được trạng thái hoặc kết quả phiếu. Vui lòng thử lại. |
| [ ] | MSG209 | UC-53/54 · Dành sẵn | Thông tin phiếu chưa hợp lệ. Hãy kiểm tra các trường được đánh dấu. |
| [ ] | MSG210 | UC-53 · Dành sẵn | Đã tạo phiếu nháp. |
| [ ] | MSG211 | UC-53 AF-1 · Dành sẵn | Đã cập nhật phiếu nháp. |
| [ ] | MSG212 | UC-53 AF-2 · Dành sẵn | Đã xóa phiếu nháp. |
| [ ] | MSG213 | UC-53 EX-2 · Dành sẵn | Chỉ Chủ tọa hiện tại được quản lý phiếu Nháp khi cuộc họp Đã duyệt/Đã công bố/Đang diễn ra. |
| [ ] | MSG214 | UC-53 EX-3 · Dành sẵn | Không tạo được phiếu nháp. Phiếu chưa được tạo. Vui lòng thử lại. |
| [ ] | MSG215 | UC-53 EX-4 · Dành sẵn | Không cập nhật được phiếu nháp. Phiếu vẫn giữ nguyên. Vui lòng thử lại. |
| [ ] | MSG216 | UC-53 EX-5 · Dành sẵn | Không xóa được phiếu nháp. Phiếu vẫn giữ nguyên. Vui lòng thử lại. |
| [ ] | MSG217 | UC-54 · Dành sẵn | Đã mở biểu quyết. |
| [ ] | MSG218 | UC-54 EX-2 · Dành sẵn | Chỉ Chủ tọa hiện tại được mở phiếu Nháp khi cuộc họp đang diễn ra. |
| [ ] | MSG219 | UC-54 EX-3 · Dành sẵn | Cuộc họp đã có phiếu đang mở. Hãy đóng phiếu đó trước khi mở phiếu này. |
| [ ] | MSG220 | UC-54 EX-4 · Dành sẵn | Không có người đủ điều kiện theo vai trò đã chọn. Biểu quyết chưa được mở. |
| [ ] | MSG221 | UC-54 EX-5 · Dành sẵn | Không mở được biểu quyết. Phiếu chưa được mở. Vui lòng thử lại. |
| [ ] | MSG222 | UC-55 EX-1 · Dành sẵn | Hãy chọn một phương án trước khi gửi phiếu. |
| [ ] | MSG223 | UC-55 EX-2 · Dành sẵn | Phương án đã chọn không có trong phiếu này. |
| [ ] | MSG224 | UC-55 EX-3 · Dành sẵn | Bạn không đủ điều kiện bỏ phiếu này. |
| [ ] | MSG225 | UC-55 EX-4 · Dành sẵn | Phiếu này không còn nhận phiếu biểu quyết. |
| [ ] | MSG226 | UC-55 EX-5 · Dành sẵn | Phiếu của bạn đã được ghi nhận và không thể sửa hoặc rút lại. |
| [ ] | MSG227 | UC-55 · Dành sẵn | Đã ghi nhận phiếu của bạn. |
| [ ] | MSG228 | UC-55 EX-6 · Dành sẵn | Không ghi được phiếu. Chưa có phiếu nào được ghi nhận. Vui lòng thử lại. |
| [ ] | MSG229 | UC-56 · Dành sẵn | Đã đóng biểu quyết. Có thể xem kết quả. |
| [ ] | MSG230 | UC-56 EX-1 · Dành sẵn | Chỉ Chủ tọa hiện tại được đóng phiếu đang mở khi cuộc họp đang diễn ra hoặc đã kết thúc. |
| [ ] | MSG231 | UC-56 EX-2 · Dành sẵn | Không đóng được biểu quyết. Phiếu và các lá phiếu đã lưu vẫn giữ nguyên. Vui lòng thử lại. |
| [ ] | MSG232 | UC-57 EX-1 · Dành sẵn | Hãy chọn Đạt, Không đạt hoặc Hoãn. |
| [ ] | MSG233 | UC-57 EX-2 · Dành sẵn | Kết luận biểu quyết đã được ghi nhận và không thể thay đổi. |
| [ ] | MSG234 | UC-57 EX-3 · Dành sẵn | Chỉ Chủ tọa hiện tại được ghi kết luận cho phiếu đã đóng khi cuộc họp đang diễn ra hoặc đã kết thúc. |
| [ ] | MSG235 | UC-57 · Dành sẵn | Đã ghi nhận kết luận biểu quyết. |
| [ ] | MSG236 | UC-57 EX-4 · Dành sẵn | Không ghi được kết luận biểu quyết. Chưa có kết luận nào được lưu. Vui lòng thử lại. |

## Theo dõi review

- [ ] Đã rà soát bảy UC.
- [ ] Đã rà soát các BR trong phạm vi.
- [ ] Đã rà soát các MSG và đối chiếu với danh mục chuẩn.

### Ghi chú / quyết định cần theo dõi

_(Bổ sung trong quá trình review.)_
