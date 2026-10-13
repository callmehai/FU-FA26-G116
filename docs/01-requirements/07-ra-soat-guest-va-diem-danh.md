# Rà soát Guest, điểm danh và kết thúc cuộc họp — 13/10/2026

Các quyết định dưới đây gồm yêu cầu PO và lần chốt tiếp BR-122/End Meeting trong phiên làm việc, được đưa vào Report 3 DOCX trước rồi đồng bộ tài liệu. [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md) ghi rõ phần thay thế các ADR đã chấp nhận. Không suy ra API hay schema hiện tại đã đáp ứng.

## 1. Danh tính Guest

Giữ lời mời tên + email và OTP (UC-04, BR-153). Sau OTP, Guest tự nhập tên/danh xưng hiển thị và chức vụ: chưa chốt thêm field bắt buộc hoặc validation. Hai trường là tự khai. Email lời mời là địa chỉ đã xác minh, không bị đổi bởi form profile; tên ban đầu do Thư ký nhập vẫn được lưu riêng. Không chốt thêm gate hoàn tất profile hay luồng resume trong đợt này.

**Đồng bộ:** UC-04, BR-148/180, màn Guest Verification; [sequence UC-04](../06-sequencediagram/auth/README.md) đã bổ sung cho UC hiện có. Chi tiết schema chưa thiết kế trong đợt này.

## 2. Guest tham gia như Member

Micro/camera/chia sẻ màn hình/chat/xem người đang trong phòng, xem và bình luận tài liệu, phản hồi lời mời, bỏ phiếu và được điểm danh theo cùng quy tắc Member. Guest không thành tài khoản nội bộ, không làm Chair/Secretary, không được ủy quyền, không điều phối phòng (UC-41) và không có kho cá nhân.

Quyền bổ sung có thể mở rộng ngoài quyền role cho người nội bộ hoặc Guest; xem roster/phản hồi là hai ví dụ, không phải danh sách đóng. UC-40 xem người đang kết nối không cần hai quyền này; không cấp mặc định quyền xem toàn bộ roster/email cho mọi người trong phòng. Đây là cách áp dụng “giống Member” theo cơ chế quyền hiện tại.

**Đồng bộ:** BR-150/174–177/181, UC-27/28a/28b/30, UC-37–42, ma trận quyền và mô tả màn hình. Đổi Guest permissions không cho phép đổi Guest thành Chair/Secretary.

## 3. Tài liệu

Guest đã xác minh và còn quyền vào cuộc họp đọc toàn bộ tài liệu đã gắn vào cuộc họp theo Member; không tồn tại cờ “chia sẻ riêng cho Guest”. Guest chỉ đọc file qua tài liệu của cuộc họp, không mở kho của người tải lên. Guest bình luận bằng invitation identity và tên tự khai. Giữ quy tắc mức mật về tải xuống và ghi audit như Member. Tài liệu không có phiên bản ([ADR-0018](../05-decisions/0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md)).

**Đồng bộ:** UC-32a/b/c, UC-35, BR-181; màn tài liệu/preview/comments; ERD author identity cho Guest. Thay phần loại Guest khỏi quyền đọc ở ADR-0006 bằng ADR-0016.

## 4. Mời vào cuộc họp Mật/Tối mật

Form mời hiện cảnh báo Guest sẽ có quyền tham gia và tài liệu như Member. Không thêm checkbox bắt xác nhận hay luồng phê duyệt riêng. Vẫn lưu người mời, thời gian và mức mật. Không bỏ kiểm tra hiệu lực lời mời hoặc các quy tắc xử lý tài liệu/audio.

**Đồng bộ:** UC-28a AF-1, BR-182 và màn mời Guest; ADR-0016 thay yêu cầu xác nhận thêm của ADR-0005.

## 5. Các BR được sửa

| BR | Vấn đề trước đây | Quy tắc sau quyết định |
|---|---|---|
| 172 | Một người chưa chọn khiến cả danh sách không lưu được | Lưu một/nhiều người được gửi; người khác giữ nguyên. Submission vẫn nguyên khối; chỉ recheck người được gửi, authority và trạng thái |
| 174, 176 | Guest không được cấp quyền xem roster/phản hồi | Guest được cấp thêm quyền như Member, không giới hạn hai quyền xem; tách invited roster khỏi connected room list |
| 160, 165 | Guest bị cấm bỏ phiếu tuyệt đối | Guest đủ điều kiện và còn quyền vào được bỏ một phiếu trong InProgress/Open như Member; tên hiển thị/rejoin không tạo voter mới |
| 148, 150, 175, 177 | Các BR liên đới vẫn dùng Guest identity/quyền cố định | Đồng bộ profile tự khai, người nhận grant là Guest invitation và thu hồi quyền khi revoke; giữ bằng chứng lịch sử |
| 179 | Điểm danh thiếu chặn kết thúc | Ngừng dùng cùng MSG155. UC-22 bỏ bước/nhánh kiểm tra đủ điểm danh |
| 180–182 | Chưa có BR tập trung cho các quyết định mới | Thêm profile tự khai, quyền Member và cảnh báo khi mời vào cuộc họp mật |

## 6. BR-122 — phân tích và quyết định đã chốt

**Quy tắc trước khi sửa:** sau khi gán Thư ký đầu tiên, cuộc họp luôn phải giữ ít nhất một Thư ký hiện hành. UC-28b/28c từ chối đổi vai trò hoặc bỏ người Thư ký cuối cùng ở mọi trạng thái cho phép quản lý roster.

**Tình huống gây vướng:** Organizer gán nhầm A làm Thư ký cho bản Draft. Muốn bỏ A rồi chọn B nhưng không thể bỏ A trước, vì BR-122. Người dùng phải tìm B và gán B trước, trong khi cuộc họp chưa phát hành. BR-125 đã kiểm tra có Thư ký lúc công bố, BR-127 kiểm tra lúc bắt đầu; vì vậy ràng buộc “luôn có từ lần gán đầu” ở giai đoạn chuẩn bị lặp lại gate muộn hơn và khóa một trạng thái tạm thời hợp lệ.

**Lý do phải sửa đồng bộ quyền phục hồi:** trước lần chốt tiếp, Organizer chỉ được gán Thư ký đầu tiên (BR-78/79). Nếu người Thư ký duy nhất tự bỏ mình hoặc bị đổi vai trò, sau đó ai được gán lại? Chỉ bỏ BR-122 nhưng giữ nguyên điều kiện “đầu tiên” có thể khiến Draft không có ai được quản lý roster. Ngoài ra, cuộc họp đã Published/InProgress cần người chịu trách nhiệm chuẩn bị, điểm danh và biên bản; bỏ người cuối cùng mà không có người thay có thể làm gián đoạn các thao tác đó. Đổi BR-122 cần chốt đồng thời quyền phục hồi, không chỉ bỏ một câu.

**Phương án PO đã chốt:** Draft/Rejected/Approved cho phép tạm không có Thư ký; Organizer được gán Thư ký khi hiện không có ai, không chỉ ở lần gán đầu trong lịch sử. Published/InProgress giữ ít nhất một Thư ký hoặc có thao tác thay người cùng lần lưu. WaitingApproval vẫn khóa thay đổi roster theo BR-80. Tiếp tục kiểm tra BR-125/127 khi công bố/bắt đầu. Nếu muốn cho Published tạm không có Thư ký, phải chốt riêng người có quyền phục hồi và hành vi các thao tác phụ thuộc.

**Đã áp dụng:** BR-78/79/122, UC-28a/b/c và các sequence đã đồng bộ. MSG109 chỉ chặn mất vai trò bắt buộc ở Published/InProgress; Draft/Rejected/Approved được tạm thiếu Thư ký. Organizer phục hồi khi không còn Thư ký hiện hành.

## 7. Các điểm giữ nguyên hoặc cần quyết định tiếp

- BR-170: chỉ ghi/sửa điểm danh trong InProgress. Sau khi bỏ gate End Meeting, người Chưa điểm danh có thể vẫn còn khi cuộc họp kết thúc. Biên bản/báo cáo phải phân biệt Chưa điểm danh với Vắng; không tự chuyển họ thành Vắng. Việc cho bổ sung sau họp vẫn chưa được PO chốt.
- BR-159: PO đã bỏ gate Vote Open. Chair End Meeting không cần đóng Vote, không tự đóng Vote; sau Ended ngừng nhận phiếu và vẫn cho Chair đóng/kiểm phiếu cũ theo BR-160/165. MSG139 ngừng dùng.
- Chỉ chốt thông tin hiển thị tự khai sau OTP; chưa chốt field bắt buộc hoặc validation. Không coi profile là giấy tờ định danh.
- Các cơ chế đang có về eligible voters/phiếu kín/một phiếu không đổi được giữ nguyên; Guest được áp dụng cùng cơ chế, không bổ sung quorum hay hạn biểu quyết.
- Các hình mockup UI cũ là ảnh lịch sử nếu chưa dựng lại; mô tả màn hình và sequence mới là yêu cầu đích. Không khẳng định UI runtime đã đổi.

Quyết định chốt tiếp: [ADR-0017](../05-decisions/0017-ket-thuc-hop-khong-phu-thuoc-diem-danh-bieu-quyet.md). Điều kiện phân quyền Chair và Meeting InProgress vẫn là kiểm tra hợp lệ; không có gate hoàn tất điểm danh/biểu quyết ở End Meeting.

Phục hồi Thư ký có thể chọn một Member hiện hành đang hoạt động để đổi thành Secretary, không tạo participant trùng hoặc reset response/attendance.

## Thu gọn phạm vi theo yêu cầu PO

Chỉ sửa các UC spec đã có, BR, mô tả màn hình và sequence hiện có để phản ánh quyết định. 16 UC từng được agent tự mở rộng đã trả lại placeholder; bỏ sequence mới và mã MSG đi kèm. Không bổ sung thiết kế field/entity cho Guest profile, author/voter hoặc grants. Quyền hiệu lực = quyền mặc định của role + grants riêng trong Meeting; catalogue và grant matrix chưa chốt.

## Rà ba nhận xét của thành viên — 13/10/2026

1. UC-04 thực sự thiếu sequence sau khi thu gọn. Đã bổ sung source/PNG cho ba execution của chính UC đang có: cấp mã, xác minh email, hoàn tất/mở lại truy cập. UC-29 tiếp tục chỉ xử lý Accept/Decline, không thay thế UC-04.
2. ADR thay thế đã có ở local: ADR-0016 thay phần xác minh/quyền Guest của ADR-0005/0006/0010/0014; ADR-0017 chốt tiếp BR-122 và bỏ gate Vote trước End Meeting. Không sửa đè ADR-0005. Các file ADR mới cần được đưa vào cùng commit khi đẩy nhánh; chỉ sửa Report 3 không làm chúng xuất hiện trên remote.
3. Giữ UC-31 ở SCRUM-26, tách khỏi implementation SCRUM-20. SCRUM-20 phải bảo toàn response/attendance history khi xoá/revoke. Jira live đã xác nhận yêu cầu bảo toàn này, nhưng SCRUM-20 vẫn còn số UC/BR cũ và delegation/luôn giữ Secretary; SCRUM-26 vẫn còn self-attendance, Excused/Delegated và quorum. Đây là phần còn phải đồng bộ ticket với PO, không phải lý do mở rộng plan SCRUM-20. Lượt rà không sửa Jira hay code.
