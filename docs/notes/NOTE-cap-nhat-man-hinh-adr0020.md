# Bàn giao phần màn hình Report 3 theo ADR-0020

**Ngày:** 15/10/2026. **Nguồn:** [ADR-0020](../05-decisions/0020-tai-lieu-theo-quyen-xem-cuoc-hop.md).

Đã cập nhật UC Specification, Business Rules và System Messages trong
[Report 3 DOCX local](../08-reports/source/Report_3_Software_Requirement_Specification.docx),
[bản trích UC](../01-requirements/05-use-case-specifications.md),
[danh mục BR](../01-requirements/06-business-rules.md) và
[danh mục MSG](../03-api/00-danh-muc-ma-thong-bao.md).
UC-33b được tái cấp cho **gắn tài liệu cuộc họp vào một mục nghị sự**; UC-33d là **bỏ gắn khỏi một mục**.
UC-32b vẫn ngừng dùng. UC/BR/MSG, actor, danh mục UC và mô tả dữ liệu đích đã được đồng bộ ngày
15/10/2026, gồm ma trận quyền. Phần đặc tả màn hình, ảnh wireframe và screen inventory vẫn pending;
không dùng các phần cũ đó làm nguồn thay thế UC/BR mới.

## Các màn hình cần cập nhật

| Vị trí trong phần III — Functional Requirements | Nội dung cần sửa | Đối chiếu |
|---|---|---|
| 7.3 — Meeting Detail – Documents Tab | Ai hiện có quyền xem cuộc họp thì đọc được tài liệu, gồm cả Approver khi được xem cuộc họp; không có quyền đọc tài liệu riêng. Hiển thị nhiều mục gắn với một tài liệu; không lặp tài liệu khi lọc theo một mục. Tài liệu chung là tài liệu không gắn mục nào. | UC-32a/c; BR-275, BR-277 |
| 7.3 — Các thao tác tài liệu | Tách rõ Thêm vào cuộc họp, Gắn vào mục, Gỡ khỏi cuộc họp, Bỏ gắn khỏi một mục. Chair/Secretary đang có quyền xem cuộc họp thực hiện trước khi họp kết thúc. Khi kết thúc, khoá cả bốn thao tác, kể cả chưa có biên bản. Mục Pending, InProgress hay Done không khoá thao tác liên kết. | UC-33a/b/c/d; BR-184/185/195/279 |
| 7.3 — Xác nhận gỡ/bỏ gắn | Gỡ khỏi cuộc họp làm tài liệu biến mất khỏi mọi mục của cuộc họp; bình luận giữ làm lịch sử. Bỏ gắn một mục vẫn giữ tài liệu ở cuộc họp và các mục còn lại; nếu hết liên kết thì thành tài liệu chung. Không dùng cùng nhãn hoặc lời xác nhận cho hai thao tác. | UC-33c/d; BR-280/281 |
| 7.4 — Add Document to Meeting Dialog | Phân biệt thêm tài liệu vào cuộc họp (UC-33a) với gắn tài liệu đã có vào mục (UC-33b). Hai thao tác có thể được bố trí trong cùng hộp thoại; ADR không bắt buộc tách màn hình. Nếu cùng hộp thoại, thể hiện rõ kết quả của từng thao tác, không tự coi cả hai là một lần lưu chung. Gắn nhiều mục bằng cùng một tài liệu, không tải lại file. Kiểm tra cuộc họp chưa kết thúc khi xác nhận lưu. | UC-33a/b; BR-277/278 |
| 7.4 — Thêm nhiều tài liệu | Giữ chọn nhiều từ kho và chọn nhiều file từ máy, nút “Thêm n tài liệu”. UC-33a đã bổ sung xử lý cả nhóm: kiểm tra từng tài liệu; có file không hợp lệ, tài liệu đã gắn, mất quyền hoặc không lưu được thì không thêm tài liệu nào của nhóm. Chỉ rõ file/tài liệu lỗi để người dùng loại hoặc chọn lại. Chỉ báo thành công và cập nhật số đã thêm khi toàn bộ nhóm thành công. | UC-33a; BR-286; MSG161/164/165/166/249 |
| 7.1/7.2 — Upload trong kho cá nhân | Cho chọn nhiều PDF/DOCX trong một lần, tối đa 50 MB **mỗi file**. Tất cả vào vị trí đang chọn; một file lỗi thì không thêm tài liệu nào của nhóm. Hiển thị các file lỗi, giữ lựa chọn để người dùng điều chỉnh; không báo thành công một phần. Một file vẫn dùng cùng luồng. | UC-36; BR-187/214/215/286; MSG164/177/178 |
| 7.4 — Tải từ máy và thông báo thư mục | Thư mục tự tạo mang tên `<tên cuộc họp> – dd/mm/yyyy`, lấy **ngày bắt đầu dự kiến của cuộc họp theo giờ Việt Nam**, kể cả khi tạo lại thư mục đã bị xoá. Không lấy ngày tải lên hoặc ngày bắt đầu thực tế. Thư mục đang tồn tại vẫn được dùng lại dù đã đổi tên/chuyển chỗ. | UC-33a AF-1; BR-192/226/227/228; ADR-0020 |
| 7.2 — Personal Document Library; 7.3/7.5 — Tên tài liệu cuộc họp | Đổi tên trong kho chỉ đổi tên ở kho. Tên trong cuộc họp được chép khi thêm và giữ nguyên khi tên kho thay đổi. | UC-71b; BR-276 |
| 7.5 — Document Viewer – Preview & Comments | Quyền đọc đi theo quyền xem cuộc họp; người đọc được bình luận trước khi kết thúc họp. Sau khi kết thúc, tài liệu và bình luận chỉ đọc; ẩn/khoá gửi bình luận và xử lý MSG251 nếu cuộc họp kết thúc trong lúc đang viết. Giữ hạn chế tải tài liệu Mật. | UC-32c/35; BR-188/249/275/282 |
| 5.2 — Add / Edit Agenda Item Drawer | Cho sửa liên kết tài liệu cả khi mục đang bàn hoặc đã bàn xong, cho tới khi cuộc họp kết thúc. Giữ khoá Planned Duration và Presenter của mục InProgress/Done; không áp khoá này sang tài liệu. | UC-23b; BR-68/185/278/279; MSG250 |
| 5.3 — Meeting Facilitation | Đổi “Create a vote for this item” và mô tả mở phiếu gắn mục hiện tại thành chuẩn bị/mở phiếu thuộc cuộc họp. Không tự gắn phiếu vào mục đang bàn. | ADR-0019/0020; UC-53/54 |
| 11.2 — Create & Open Vote Dialog; I.5.1 — Screen Inventory, dòng 11.2 | Bỏ trường, lời mô tả và giá trị mặc định gắn phiếu với Agenda Item. Phiếu thuộc cuộc họp. Đối chiếu thêm [note FE-11 hiện có](NOTE-cap-nhat-man-hinh-fe11.md) về chuẩn bị nháp ở Approved/Published/InProgress và mở phiếu khi InProgress. | UC-53/54 |

Khi cập nhật màn hình, đối chiếu các trạng thái thành công/lỗi với MSG238–MSG251 (**Dành sẵn,
chưa triển khai**); không dùng MSG163, MSG168, MSG173 đã ngừng dùng. MSG141 trong contract đã triển
khai được giữ nguyên; đặc tả UC-23b dùng MSG250 cho giới hạn Planned Duration/Presenter mới.

**Sai khác số màn cần lưu ý:** ADR-0020 nhắc “6.2” trong nhóm màn biểu quyết. Bản Report 3 local
hiện tại có III.6.2 là **Add Participant Dialog**, không có mô tả gắn Vote với mục nghị sự.
Không suy đoán đây là màn biểu quyết và không yêu cầu sửa màn này chỉ vì số được nêu trong ADR.
Người phụ trách đối chiếu lại số trên bộ wireframe khi cập nhật ảnh. Các ảnh chưa được render/soi
trực quan trong đợt sửa đặc tả này.

## Trạng thái đồng bộ ngoài đặc tả màn hình

- **Đã đồng bộ mô tả dữ liệu đích trong Report 3:** Vote không còn `AgendaItemId`; MeetingDocument
  dùng quan hệ `MeetingDocumentAgendaItem` nhiều–nhiều trong cùng Meeting. Tên được chép khi thêm;
  thêm lại tạo lần thêm mới, giữ bình luận cũ trong lịch sử. Tác giả bình luận có thể là User hoặc
  danh tính lời mời Guest. Đây là mô tả đích để dựng class diagram, chưa phải thay đổi database/code.
- **Đã đồng bộ danh mục UC:** có UC-33a/b/c/d, ghi rõ tái cấp UC-33b và UC-32b vẫn bỏ; FE-07 không
  còn ghi Placeholder cho các UC đã đặc tả. Actor UC-35 gồm mọi actor có quyền đọc tương ứng.
- **Đã đồng bộ ma trận quyền:** quyền đọc theo quyền xem Meeting, bốn thao tác Chair/Secretary
  trước khi kết thúc; người đọc được bình luận trước khi kết thúc, gồm System Admin và Approver
  khi được đọc tài liệu. Người sửa màn hình áp dụng các quyền này lên nút và trạng thái hiển thị.
- **Hải — ảnh wireframe và mô tả màn hình:** cập nhật các vị trí ở bảng trên; giữ phiếu thuộc cuộc họp.
- **BE — SCRUM-28:** phần bỏ `AgendaItemId` của Vote là công việc triển khai đã được ADR giao;
  không có thay đổi code hoặc Jira trong đợt cập nhật tài liệu này.

Đây là note bàn giao các bề mặt còn lệch, không phải một quyết định nghiệp vụ mới. Chưa thay file trên Drive.

**Bổ sung theo xác nhận PO trong phiên ngày 15/10/2026:** khi xoá mục nghị sự, tài liệu vẫn nằm
trong cuộc họp và giữ các mục gắn khác; không còn mục gắn thì thành tài liệu chung (BR-283).
Khi thêm lại tài liệu đã gỡ, hiển thị tên tại lần thêm mới và không khôi phục bình luận cũ;
bình luận cũ vẫn giữ làm lịch sử (BR-276/284). Các nội dung này đã vào UC/BR; phần màn hình chờ cập nhật.

## Đối chiếu ảnh xác nhận PO — quyền đọc tài liệu

Ảnh PO yêu cầu: **Ai xem được cuộc họp thì đọc được tài liệu của cuộc họp đó; tài liệu không có
quyền đọc riêng.** Kết quả đối chiếu DOCX local ngày 15/10/2026:

- BR-198, BR-233, BR-234 không còn trong bảng BR đang dùng; các mã được giữ ở danh sách ngừng dùng
  để truy vết, không phải quy tắc còn hiệu lực.
- BR-275 là quy tắc thay thế duy nhất: **“Anyone who can view a Meeting can read its documents.”**
- UC-32a và UC-32c kiểm tra quyền xem Meeting cùng việc tài liệu còn trong Meeting; không kiểm tra
  một quyền đọc tài liệu riêng, không đặt thêm mốc Published riêng cho tài liệu và không loại Approver.
  Danh sách actor có System Admin; vai trò này vẫn phải có quyền xem Meeting tương ứng như mọi người khác.
- UC-35 dùng cùng quyền xem Meeting; bình luận sau Ended bị khoá theo xác nhận riêng của PO.
- Quy tắc chỉ xem trực tuyến đối với tài liệu Mật vẫn là hạn chế **tải xuống**, không phải quyền đọc riêng.

**Bàn giao phần màn hình:** ma trận quyền đã đồng bộ. Đối chiếu các dòng *Meeting document — View list* và
*Preview/Download* với chính dòng *Meeting — View details* cho từng actor, kể cả System Admin,
Approver và Guest. Thay cách diễn đạt “Member read rules” trong chú thích 13 và III.7.3 bằng
“có quyền xem cuộc họp được mời thì đọc được tài liệu của cuộc họp đó”; không thêm quyền cấp riêng,
checkbox chia sẻ tài liệu hoặc mốc công bố riêng. Chú thích phiên Guest vẫn giữ điều kiện xác minh
email và lời mời còn hiệu lực. III.7.4 dùng cùng cách diễn đạt trong *Access notice*.

Phần UC/BR và ma trận quyền đã đáp ứng ảnh PO. Mô tả màn hình và ảnh giữ nguyên trong DOCX để
người phụ trách cập nhật theo note; chưa xác nhận code/API thực thi quy tắc này.

**PO đã chốt ngày đặt tên thư mục:** dùng ngày bắt đầu **dự kiến** của cuộc họp vì có thể tải
tài liệu trước khi họp bắt đầu. Xem [ADR-0020](../05-decisions/0020-tai-lieu-theo-quyen-xem-cuoc-hop.md).
BR-192/228 và UC-33a đã cập nhật; điểm phân biệt ngày dự kiến/thực tế không còn để ngỏ.

**Thay đổi người dùng xác nhận trong vòng bàn giao:** hỗ trợ nhiều file trong cùng lần thêm/tải
ở UC-33a và UC-36. Cả nhóm thành công hoặc không thêm tài liệu nào (BR-286), khớp kết quả đã mô tả
trong màn 7.4 cũ. Đã cập nhật UC/BR/MSG và danh mục; phần màn hình chỉ cần đồng bộ diễn đạt và trạng
thái theo các hàng trên. Không còn đề xuất giới hạn một file mỗi lần. Không đặt thêm giới hạn tổng
số file hoặc tổng dung lượng của nhóm; giới hạn 50 MB áp dụng cho từng file như quy tắc hiện có.
