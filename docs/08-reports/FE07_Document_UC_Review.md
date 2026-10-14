# FE-07 — Hồ sơ review chéo đặc tả Document

Ngày cập nhật: **14/10/2026**. Trạng thái: **bản đặc tả để review**, chưa xác nhận toàn bộ UC và chưa chứng minh chức năng đã triển khai.

File này chứa đầy đủ **toàn bộ 14 UC chi tiết của FE07**, các Business Rules liên quan và toàn bộ MSG dùng trong 14 UC. Nội dung UC, BR và MSG được trích nguyên văn từ Report 3 DOCX local; các flow được tách khỏi bảng để dễ đọc.

Nguồn đối chiếu:

- [Report 3 DOCX](source/Report_3_Software_Requirement_Specification.docx) và [Report 3 Markdown](Report-3_Software_Requirement_Specification.md).
- [Danh mục BR](../01-requirements/06-business-rules.md) và [danh mục MSG](../03-api/00-danh-muc-ma-thong-bao.md).
- [Quy ước đặc tả](../01-requirements/README.md), [ADR-0018](../05-decisions/0018-tai-lieu-khong-phien-ban-chi-giu-tham-chieu.md), [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md) và phần quy tắc đọc còn hiệu lực của [ADR-0006](../05-decisions/0006-kho-tai-lieu-ca-nhan-va-quyen-doc.md).

## Nguyên tắc viết BR, MSG và thuật ngữ

- Mỗi BR diễn đạt một nguyên tắc nghiệp vụ độc lập, ngắn và rõ.
- Nội dung Rule Statement không chứa mã BR khác, mã feature, tên bảng dữ liệu hay chi tiết triển khai. Dẫn chiếu và phạm vi được đặt tại UC hoặc cột riêng.
- MSG trong đặc tả viết bằng **tiếng Anh**; mã và câu chữ phải khớp danh mục.
- Quyền xem tài liệu bao gồm quyền bình luận; không có quyền bình luận riêng.
- Actor chỉ dùng **Guest**, không thêm “verified” hay “external” vào tên actor. Xác minh email và lời mời còn hiệu lực là Preconditions.
- **Participant** là người dùng nội bộ được mời vào cuộc họp; tên gọi này không yêu cầu người đó đã vào phòng họp hoặc được ghi nhận có mặt.
- Không đặc tả hành động Cancel và không có Cancellation Flow trong 14 UC này.

## Cơ sở nghiệp vụ đã chốt trước vòng review này

Các quyết định dưới đây là cơ sở của bản đặc tả hiện tại. Checklist ở phần sau dùng để xác nhận hoặc điều chỉnh trong vòng review mới; câu hỏi được đưa vào checklist không tự hủy quyết định đã chốt.

1. Organizer, Chair và Secretary được đọc tài liệu trước công bố. Participant và Guest chỉ đọc sau công bố. Vai trò Approver tự nó không cấp quyền đọc; người đồng thời giữ quan hệ hợp lệ khác đọc theo quan hệ đó.
2. Chair/Secretary được thêm tài liệu ở mọi trạng thái trước khi cuộc họp kết thúc; từ Ended trở đi không được thêm.
3. Được thêm/gỡ tài liệu của mục nghị sự Pending hoặc InProgress; không được thêm/gỡ với mục Done. Khi đã có biên bản, không được gỡ tài liệu cuộc họp.
4. Thư mục tự tạo dùng tên cuộc họp và ngày upload theo giờ Việt Nam. Thư mục hiện có tiếp tục được dùng khi đổi tên/di chuyển. Nếu đã bị xóa, lần upload tiếp theo tạo thư mục mới tại gốc kho, dùng ngày upload mới.
5. Tải từ máy vào cuộc họp chỉ thành công khi cả tài liệu cuộc họp và tài liệu kho cá nhân được tạo. Nếu một bên thất bại, không tạo tài liệu mới ở cả hai bên.
6. Sau khi gỡ khỏi cuộc họp và không còn nơi sử dụng, file gốc có thể được cleanup. Lịch sử giữ metadata và comments, không bảo đảm đọc lại nội dung file đã cleanup. Chưa chốt chức năng xem lịch sử, ai được xem hoặc xem ở đâu.
7. File đã tải lên không có phiên bản và không đổi nội dung. Muốn thay nội dung thì tải file mới, thêm vào cuộc họp và gỡ tài liệu cũ khi được phép.
8. Cùng một file đã tải lên chỉ được gắn một lần đang hiệu lực trong một cuộc họp. Hai lần upload riêng là hai tài liệu khác nhau, kể cả cùng tên hoặc nội dung.
9. Quyền bổ sung của Guest không được vượt các giới hạn về vai trò, ủy quyền, điều hành phòng họp, kho cá nhân hoặc tải tài liệu của cuộc họp Secret.

## Kết quả xử lý review

| Vấn đề                              | Cách xử lý                                                                                          |
| ----------------------------------- | --------------------------------------------------------------------------------------------------- |
| Cancel nằm trong EX                 | Bỏ các nhánh Cancel; không thêm Cancellation Flow.                                                  |
| Cleanup và lịch sử                  | Tách quy tắc giữ comments, quy tắc cleanup và quy tắc giữ metadata/comments sau cleanup.            |
| Quyền bổ sung của Guest             | Tách nguyên tắc cấp quyền và nguyên tắc giới hạn cứng; quyền bổ sung không thể ghi đè các giới hạn. |
| Thư mục tự tạo bị xóa               | Tạo thư mục mới ở gốc kho khi upload lần tiếp theo.                                                 |
| Tiêu chí trùng tài liệu             | Dùng cùng file đã tải lên; không so trùng bằng tên hoặc nội dung.                                   |
| Danh sách rỗng                      | Step 4 cho phép đọc kết quả rỗng, đổi bộ lọc hoặc kết thúc xem.                                     |
| Participant / Member                | Dùng Participant thống nhất trong phần đặc tả và BR liên quan này.                                  |
| Cuộc họp không khả dụng / mất quyền | Tách nhánh cuộc họp không khả dụng dùng MSG174; giữ MSG162/MSG169 cho mất quyền thêm/gỡ.            |

## Phạm vi review và các điểm còn cần chốt

Tất cả 14 UC chi tiết của FE07 đã có đầy đủ Actor, Description, Preconditions, Postconditions, Main Flow, AF và EX để review. Quy tắc tên tài liệu không trống đã được bỏ. PO đã chốt: ai được xem tài liệu thì được bình luận, không có quyền bình luận riêng. Giới hạn độ dài tên/bình luận, hạn mức kho cá nhân và giới hạn trạng thái cuộc họp khi bình luận chưa được quyết định mới; không tự đặt các giá trị này trong đặc tả.

Danh mục đầy đủ các quyền bổ sung và grant matrix của Guest vẫn cần được đặc tả ở phần quản lý quyền cuộc họp. Các giới hạn cứng trong hồ sơ này đã rõ và luôn có hiệu lực.

## Checklist xác nhận nghiệp vụ với PO

**Nhóm A: 0/9 câu hỏi đã được trả lời trong vòng review này.** Một số câu hỏi xác nhận lại quyết định đã có; mỗi mục ghi rõ cơ sở hiện tại để PO quyết định giữ nguyên hay thay đổi.

PO ghi câu trả lời ở từng mục. Nếu thay đổi quyết định cũ, ghi rõ phạm vi áp dụng và thời điểm hiệu lực. Chỉ cập nhật BR, UC và MSG sau khi quyết định hoặc đề xuất sửa được duyệt.

### Nhóm A — Chín câu hỏi cần xác nhận

- [ ] **A01 — Meeting Approver có cần đọc tài liệu đính kèm để thực hiện phê duyệt không?**
  - Liên quan: BR-234, BR-198, BR-233; UC-19, UC-32a, UC-32c, UC-35.
  - Hiện tại: Approver không được đọc chỉ nhờ vai trò Approver; đây là quyết định đã chốt. Người đồng thời là Organizer, Chair hoặc Secretary đọc theo quan hệ đó.
  - Cần xác nhận: giữ chính sách hiện tại hay cấp quyền cho người đang được giao duyệt trong WaitingApproval? Nếu cấp, quyền đọc kết thúc khi nào hoặc khi đổi người duyệt?
  - Lưu ý: quy tắc hiện tại cho phép ai xem được tài liệu thì bình luận được. Mở quyền xem cho Approver cũng ảnh hưởng UC-35.
  - **PO chốt:** …

- [ ] **A02 — Có cho bình luận sau khi Meeting đã Ended, sau khi biên bản phát hành hoặc sau khi lưu trữ không?**
  - Liên quan: BR-249; UC-35.
  - Hiện tại: quyền xem tài liệu bao gồm quyền bình luận; chưa có giới hạn riêng theo các mốc kết thúc/phát hành/lưu trữ.
  - Cần xác nhận: tiếp tục bình luận khi còn quyền xem, hay khóa bình luận ở một mốc cụ thể? Nếu khóa, chọn rõ mốc và quy định việc đọc các bình luận cũ.
  - Lưu ý: vòng đời Meeting hiện kết thúc ở Ended; biên bản có vòng đời riêng. `MinutesIssued`/`Archived` trong checklist là mốc nghiệp vụ cần làm rõ, không mặc nhiên là trạng thái Meeting hiện có.
  - **PO chốt:** …

- [ ] **A05 — Từ chối lời mời có giữ nguyên quyền đọc tài liệu không?**
  - Liên quan: BR-235, BR-181; UC-29, UC-32a, UC-32c, UC-35 và luồng vào phòng họp.
  - Hiện tại: từ chối tham dự không tự thu hồi quyền đọc. ADR-0006 có quy tắc này cho Participant; ADR-0016 cho Guest quyền tham gia và đọc tài liệu như Participant.
  - Cần xác nhận riêng cho Participant và Guest: giữ quyền hay mất quyền khi Declined? Nếu Guest bị mất quyền, phạm vi gồm tài liệu, bình luận, phòng họp hay toàn bộ quyền truy cập Meeting? Nếu đổi phản hồi sau đó, quyền có được khôi phục không?
  - **PO chốt:** …

- [ ] **A06 — Có cho gỡ tài liệu sau Ended nhưng trước khi tạo Meeting Minutes không?**
  - Liên quan: BR-185, BR-184, BR-196; UC-33c.
  - Hiện tại: gỡ bị chặn khi đã có biên bản hoặc tài liệu liên quan mục nghị sự Done; chưa có quy tắc riêng chặn gỡ chỉ vì Meeting đã Ended.
  - Cần xác nhận: khi chưa có biên bản và không liên quan mục Done, Chair/Secretary còn được gỡ sau Ended không? Nếu không, bổ sung mốc khóa rõ ràng.
  - **PO chốt:** …

- [ ] **A07 — Người dùng có cần mở hoặc tải tài liệu trực tiếp từ Personal Library không?**
  - Liên quan: UC-34; BR-242; liên hệ A03 và A04.
  - Hiện tại: UC-34 mô tả duyệt danh sách, tìm kiếm/lọc và xem thông tin sử dụng. UC-32c chỉ mô tả tài liệu của Meeting.
  - Cần xác nhận: kho cá nhân có chức năng preview, download hay cả hai? Nếu có, chốt quyền và hạn chế mức mật, rồi quyết định đặc tả trong UC-34 hay một UC riêng.
  - **PO chốt:** …

- [ ] **A08 — Có yêu cầu lưu file gốc để kiểm toán, thay vì chỉ giữ metadata/comments không?**
  - Liên quan: BR-232, BR-212, BR-213; UC-33c, UC-71a; liên hệ B04.
  - Hiện tại: PO đã chọn giữ metadata/comments; file gốc được cleanup khi không còn nơi sử dụng. Chưa có yêu cầu giữ file gốc để đọc lại lịch sử.
  - Cần xác nhận lại: giữ quyết định hiện tại hay yêu cầu giữ file gốc? Nếu phải giữ, chốt phạm vi, thời hạn, người được truy cập và điều kiện được xóa; không tự đặt các giá trị này.
  - **PO chốt:** …

- [ ] **A09 — Có cần giới hạn tổng dung lượng kho, độ dài comment và tên tài liệu không?**
  - Liên quan: UC-35, UC-71b, UC-36.
  - Hiện tại: giới hạn 50 MB là theo từng file; tổng dung lượng kho và độ dài tên/comment chưa chốt. Quy tắc tên tài liệu không trống đã bị bỏ.
  - Cần xác nhận riêng từng giới hạn: có cần không, giá trị và đơn vị nếu có, cách xử lý khi vượt giới hạn. Không phục hồi BR đã bỏ hoặc tự điền số liệu.
  - **PO chốt:** …

### Nhóm B — Bốn câu hỏi bổ sung từ báo cáo review

**0/4 câu hỏi đã được trả lời trong vòng review này.** Đây là các điểm bổ sung; không phải quyết định đã được áp dụng.

- [ ] **B01 — Có khóa hoàn toàn Title/Description khi Agenda Item đã Done không?**
  - Liên quan: BR-68, BR-74; UC-23b, thuộc FE05.
  - Hiện tại: khi Meeting còn cho phép quản lý Agenda, mục InProgress hoặc Done vẫn sửa được Title/Description.
  - Cần chốt: giữ khả năng sửa hai trường này hay khóa mục Done? Đây là đổi nghiệp vụ và có ảnh hưởng code hiện tại.
  - **PO chốt:** …

- [ ] **B02 — Một tài liệu trong Meeting có cần liên kết với nhiều Agenda Item không?**
  - Liên quan: BR-193; UC-33a, UC-32a và các UC quản lý Agenda.
  - Hiện tại: cùng file chỉ có một đính kèm đang hiệu lực trong một Meeting; dòng đính kèm hiện mô tả một Agenda Item tùy chọn.
  - Đề xuất để PO cân nhắc: giữ một tài liệu trong danh sách Meeting nhưng cho liên kết tới nhiều mục, thay vì upload lặp hoặc tạo nhiều bản đính kèm trùng.
  - **PO chốt:** …

- [ ] **B03 — Tên thư mục có cần duy nhất trong cùng thư mục cha không?**
  - Liên quan: BR-221; UC-72a, UC-72b, UC-71c, UC-72c.
  - Hiện tại: ADR-0018 cho phép tên thư mục không duy nhất.
  - Cần chốt: tiếp tục cho phép trùng tên hay yêu cầu duy nhất trong cùng cha? Nếu giữ trùng tên, chốt thông tin giúp người dùng phân biệt khi chọn thư mục đích. Đường dẫn riêng không phân biệt được hai thư mục trùng tên trong cùng một cha.
  - **PO chốt:** …

- [ ] **B04 — Có cần chức năng xem lịch sử bình luận của tài liệu đã gỡ không; ai xem và xem ở đâu?**
  - Liên quan: BR-211, BR-212, BR-232; UC-33c; liên hệ A08.
  - Hiện tại: đã chốt giữ dữ liệu làm bằng chứng, nhưng chưa chốt chức năng, vai trò hoặc màn hình xem lịch sử.
  - Cần chốt: chỉ giữ dữ liệu hay cung cấp chức năng xem lại? Nếu cần xem, xác định người được xem, phạm vi Meeting và vị trí truy cập. Không tự thêm actor thanh tra/kiểm toán hoặc mặc nhiên giữ file gốc.
  - **PO chốt:** …

### Nhóm C — Sáu đề xuất chỉnh tài liệu cần duyệt

**0/6 đề xuất đã được duyệt trong vòng review này.** Các đề xuất dưới đây đang chờ duyệt; câu chữ BR/UC hiện tại vẫn được giữ ở các phần sau.

| Duyệt | Mã  | Đề xuất                                                                                                 | Phạm vi và lưu ý                                                                                                                                     |
| ----- | --- | ------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| [ ]   | C01 | Đồng bộ mô tả actor/màn hình và luồng duyệt về đúng quyền đọc của Approver.                             | Chốt A01 trước. UC duyệt là UC-19; UC-20 là Publish Meeting. Không tự mở quyền chỉ vì mô tả cũ cho phép xem documents.                               |
| [ ]   | C02 | Viết BR-208 ngắn: “Changing document content requires uploading a new file.”                            | Tránh hiểu có chức năng Replace độc lập. Việc thêm/gỡ và giới hạn của từng thao tác nằm tại UC-33a/UC-33c; không chèn mã BR khác vào Rule Statement. |
| [ ]   | C03 | Thu hẹp Applies To của BR-68 về UC-23b và sửa dẫn chiếu các UC tạo/xóa/đổi thứ tự theo đúng BR đã tách. | Chỉnh phạm vi/dẫn chiếu; không tự đổi quyền sửa mục Done trong B01.                                                                                  |
| [ ]   | C04 | Làm rõ BR-239: xóa mục không đánh lại vị trí lưu của các mục còn lại.                                   | Giữ hành vi hiện tại. Code và test có xử lý các vị trí không liên tục; chưa có căn cứ đổi reorder chỉ vì vị trí có khoảng trống.                     |
| [ ]   | C05 | Viết BR-223 rõ: “A folder can be deleted only when it contains no documents and no subfolders.”         | UC-72d đã kiểm tra cả hai điều kiện; đây là làm rõ câu chữ.                                                                                          |
| [ ]   | C06 | Bổ sung điều kiện qua thời gian chờ sau upload vào quy tắc cleanup BR-213.                              | Điều kiện có trong ADR-0018. Giá trị thời gian chờ chưa chốt; không tự điền và không đổi quyết định lưu file gốc trong A08.                          |

**PO/BA ghi nhận duyệt nhóm C:** …

Tên thư mục tự tạo hiện dùng ngày upload theo `dd/mm/yyyy` như quyết định đã có. Chưa có căn cứ phải đổi sang `dd-mm-yyyy` chỉ vì ký tự `/` trong một tên hiển thị; không đưa thay đổi này thành yêu cầu bắt buộc của vòng review.

### Ghi nhận kết quả review

| Mục | Quyết định hoặc nội dung duyệt | Người xác nhận | Ngày | BR/UC/MSG cần cập nhật |
| --- | ------------------------------ | -------------- | ---- | ---------------------- |
| …   | …                              | …              | …    | …                      |

## Gợi ý review lần tiếp theo

- Mỗi BR có đúng một nguyên tắc, không chép lại quy tắc khác hoặc đưa chi tiết triển khai vào câu chữ.
- Actor, Preconditions, Postconditions và các bước kiểm tra có khớp các quyết định PO ở trên không.
- AF có đạt mục tiêu; EX có nói rõ kết quả dữ liệu và cách kết thúc/tiếp tục không.
- Có xử lý thay đổi quyền, trạng thái cuộc họp, trạng thái mục nghị sự hoặc biên bản trong lúc người dùng thao tác không.
- Mỗi MSG có đúng tình huống, đúng câu chữ và đúng điểm sử dụng trong UC không.
- Phát hiện cần chỉ rõ UC/BR/MSG, mâu thuẫn hoặc nhánh thiếu, cùng đề xuất chỉnh sửa. Nếu muốn thay quyết định PO, ghi rõ đó là đề xuất đổi nghiệp vụ.

## Quy ước viết Use Case Specification (Business-Oriented)

Đặc tả Use Case phải phản ánh **góc nhìn nghiệp vụ (Business Perspective)** của Actor và Hệ thống, phục vụ việc giao tiếp rõ ràng giữa Product Owner / BA, Khách hàng, Developer và Tester.

### Nguyên tắc cốt lõi: Tập trung nghiệp vụ, không kỹ thuật

- **Nghiệp vụ thuần túy**: Mô tả hành động của Actor (nhập liệu, chọn, xác nhận) và phản hồi của Hệ thống (kiểm tra điều kiện, ghi nhận thay đổi, thông báo kết quả).
- **Business Rules dùng chung**: Định nghĩa BR tập trung tại [06-business-rules.md](../01-requirements/06-business-rules.md). UC Specification và Sequence Diagram chỉ dẫn chiếu đúng ID (`BR-XX`) ở bước áp dụng; không chép lại một phiên bản BR riêng.
- **Tuyệt đối KHÔNG đưa chi tiết kỹ thuật vào Use Case Spec**:
  - Không nhắc đến HTTP method, URL endpoint, HTTP status code (`GET /api/meetings`, `200 OK`, `400 Bad Request`, `500 Internal Server Error`).
  - Không nhắc đến Database table, SQL query, ORM (`SELECT`, `SaveChanges()`, `Foreign Key`).
  - Không nhắc đến kỹ thuật mạng, giao thức hay thư viện (`SignalR hub`, `LiveKit token`, `JWT claims`, `Redis cache`, `bcrypt`, `middleware`).
  - _Lưu ý_: Các chi tiết kỹ thuật trên thuộc phạm vi của [02-design/](../02-design/), [03-api/](../03-api/) và [06-sequencediagram/](../06-sequencediagram/).

---

### Luồng sự kiện chính (Main Flow / Basic Flow)

- Là kịch bản lý tưởng (happy path) mà Actor thực hiện để đạt được mục tiêu nghiệp vụ nhanh nhất mà không gặp lỗi.
- Mỗi bước là một tương tác rõ ràng giữa Actor và Hệ thống.
- Kết thúc bằng việc hoàn thành mục tiêu Use Case và thỏa mãn **Success Postconditions**.

---

### Luồng sự kiện thay thế (Alternative Flow - AF)

- **Định nghĩa**: Alternative Flow là một **con đường khác với Main Flow nhưng vẫn đạt mục tiêu nghiệp vụ và Success Postcondition** của Use Case.
- **Quy tắc viết AF**:
  - Nêu rõ điểm rẽ từ bước nào của Main Flow.
  - Nếu luồng tiếp tục quay lại Main Flow, phải kết thúc bằng:
    > _"The flow resumes at Step X of the Main Flow."_
  - **Khi nào dùng AF**:
    - Trường hợp có nhiều cách lựa chọn hợp lệ để hoàn thành mục tiêu (ví dụ: Tạo cuộc họp từ bản nháp vs. Tạo cuộc họp mới trực tiếp).
    - Với UC dạng danh sách (View List): Thao tác **Search / Filter** là Alternative Flow khi người dùng tìm kiếm theo tiêu chí khác nhau để xem danh sách.
    - Kết quả tìm kiếm rỗng (_"No matching results"_): Hệ thống xử lý đúng yêu cầu và vẫn đạt mục tiêu tra cứu danh sách.
  - **Khi nào KHÔNG dùng AF**:
    - Không dùng AF cho trường hợp: người dùng hủy thao tác, không lưu được, lỗi kết nối, dữ liệu không hợp lệ, không đủ quyền hạn.
    - Các thao tác UI nhỏ (sắp xếp cột `Sort`, chuyển trang `Pagination`, mở/đóng accordion) không làm thay đổi bản chất kịch bản nghiệp vụ thì mô tả trong Business Rules hoặc UI Behavior, không tách thành AF.

---

### Luồng ngoại lệ / Lỗi (Exception Flow - EX)

- **Định nghĩa**: Exception Flow được kích hoạt khi **có lỗi, vi phạm quy tắc nghiệp vụ (Business Rules) hoặc điều kiện không hợp lệ** khiến Main Flow không thể hoàn thành bình thường.
- **Phân loại EX**:
  1. **Recoverable Exception (Có thể sửa và tiếp tục)**:
     - Xảy ra khi dữ liệu nhập không hợp lệ hoặc thiếu thông tin mà người dùng có thể điều chỉnh lại ngay.
     - Sau khi hệ thống thông báo lỗi, nếu cho phép người dùng điều chỉnh, kết thúc bằng:
       > _"The flow resumes at Step X of the Main Flow."_
  2. **Fatal / Terminated Exception (Dừng luồng / Thất bại)**:
     - Xảy ra khi điều kiện nghiệp vụ bị từ chối hoàn toàn (ví dụ: cuộc họp không còn ở trạng thái cho phép, không có quyền thực hiện, cuộc họp đã kết thúc).
     - Luôn mô tả **kết quả nghiệp vụ (Business Outcome)** trước (ví dụ: _"No meeting is created."_, _"The meeting status remains unchanged."_), sau đó kết thúc bằng:
       > _"The use case ends."_
- **Quy tắc thông báo**: Mọi Exception Flow phải dẫn chiếu đúng **Mã thông báo hệ thống (`MSGxx`)** kèm thông điệp đầy đủ theo danh mục [00-danh-muc-ma-thong-bao.md](../03-api/00-danh-muc-ma-thong-bao.md).

---

### Bảng tổng kết quy tắc kiểm tra nhanh

| Tiêu chí kiểm tra                                                                 | Phân loại luồng                       | Kết thúc luồng bằng câu                                                 |
| --------------------------------------------------------------------------------- | ------------------------------------- | ----------------------------------------------------------------------- |
| Nhánh rẽ **vẫn đạt** mục tiêu nghiệp vụ và Success Postcondition                  | **Alternative Flow (AF)**             | _"The flow resumes at Step X of the Main Flow."_ hoặc hoàn tất mục tiêu |
| Nhánh rẽ **không đạt** mục tiêu vì dữ liệu không hợp lệ (người dùng sửa lại được) | **Exception Flow (EX - Recoverable)** | _"The flow resumes at Step X of the Main Flow."_                        |
| Nhánh rẽ **không đạt** mục tiêu vì vi phạm quy tắc / điều kiện tiên quyết         | **Exception Flow (EX - Terminated)**  | _"[Business outcome]. The use case ends."_                              |
| Thao tác UI thuần túy (Sort, Next page, Resize...)                                | **Business Rule / UI behavior**       | Không tách thành flow riêng                                             |

---

### 2.6. Quy tắc viết Business Rule (BR)

- Một BR chỉ diễn đạt **một nguyên tắc nghiệp vụ độc lập**. Tách các điều kiện, quyền, giới hạn và kết quả có thể áp dụng riêng thành các BR riêng.
- Viết ngắn, trực tiếp và dễ kiểm tra: nêu đối tượng, điều kiện nếu có và điều được phép, bắt buộc hoặc bị cấm. Không kể lại toàn bộ flow.
- Nội dung Rule Statement không chứa mã BR khác, mã feature, mã MSG, tên bảng dữ liệu hoặc chi tiết triển khai. Phạm vi UC, nguồn quyết định và liên hệ giữa các quy tắc nằm ở cột riêng hoặc tại bước áp dụng trong UC.
- Tra danh mục trước khi thêm: dùng lại quy tắc cùng nghĩa, không tạo BR trùng. Khi tách BR lớn, giữ mã cũ cho nguyên tắc chính và cấp mã mới cho các nguyên tắc được tách; không renumber hoặc tái cấp mã đã dùng.
- Quy tắc từ ADR đã được chấp nhận cần được diễn đạt thành BR nghiệp vụ; ghi ADR tại cột nguồn, không đưa nội dung kỹ thuật của ADR vào Rule Statement.
- Phân biệt quy tắc đã chốt và quy tắc đề xuất. Không biến giả định, giá trị giới hạn chưa rõ hoặc quyết định còn thiếu thành quy tắc đã được PO thông qua.
- Dùng tên actor chuẩn: `Guest`, không dùng `verified Guest` hoặc `external Guest` làm tên actor. Xác minh email và hiệu lực lời mời là điều kiện nghiệp vụ. `Participant` không có nghĩa là người đó đã tham dự hoặc được ghi nhận có mặt.

Ví dụ Rule Statement: **“Only an empty folder can be deleted.”** Phạm vi `UC-72d` và nguồn quyết định được ghi ở cột riêng.

### 2.7. Quy tắc viết System Message (MSG)

- Thông điệp trong UC Specification và danh mục phục vụ đặc tả được viết bằng **tiếng Anh**. Chỉ đổi ngôn ngữ khi có yêu cầu; thay đổi đặc tả không tự động thay đổi chuỗi UI/API đã triển khai.
- Một mã MSG diễn đạt một tình huống. Dùng chung mã khi cùng tình huống xuất hiện ở nhiều UC; không dùng cùng mã cho lỗi có nghĩa khác nhau chỉ vì câu chữ gần giống.
- Viết ngắn và cụ thể: nói kết quả hoặc vấn đề người dùng cần hiểu; thêm cách sửa/thử lại khi có ích. Không đưa tên exception, endpoint, trạng thái HTTP hoặc chi tiết lưu trữ vào thông điệp.
- Thông điệp thất bại phải đúng với kết quả dữ liệu: không báo thành công khi chỉ hoàn tất một phần; nếu không có thay đổi nào được lưu, nói rõ khi người dùng cần biết.
- Không tiết lộ sự tồn tại của tài nguyên mà người dùng không được truy cập. Phân biệt lỗi mất quyền với tài nguyên không khả dụng chỉ khi việc phân biệt không làm lộ dữ liệu.
- Tra [danh mục MSG](../03-api/00-danh-muc-ma-thong-bao.md) trước khi cấp mã. Dùng dải `MSGnn` chung cho dự án, cập nhật “Mã tiếp theo”, không renumber hoặc tái dùng mã đã nghỉ.
- Trong UC, ghi đủ **mã và nguyên văn thông điệp** tại đúng bước thành công hoặc lỗi. Không để mã tạm hoặc dùng câu chữ khác với danh mục.
- Mã mới cho đặc tả phải ghi trạng thái **Dành sẵn, chưa triển khai**. Đồng bộ danh mục MSG, phụ lục Report 3 và các UC dùng mã đó trong cùng đợt sửa.
- Không tạo MSG cho thao tác đóng/hủy giao diện không được đặc tả, hoặc cho mỗi bước nhỏ không cần phản hồi riêng.

Ví dụ: **`MSGnn – “Only an empty folder can be deleted.”`** Mã thực tế phải được cấp từ danh mục chung.

---

## Rà soát nguồn và các BR cần làm rõ thêm

Các điểm dưới đây được ghi nhận để PO/BA xem xét; nội dung các BR này chưa bị đổi trong đợt sửa này.

| BR     | Phát hiện                                                                                                                                 | Nguồn đối chiếu        | Hướng xử lý đề xuất                                                                         |
| ------ | ----------------------------------------------------------------------------------------------------------------------------------------- | ---------------------- | ------------------------------------------------------------------------------------------- |
| BR-213 | Quy tắc cleanup đã rút mất điều kiện file phải qua thời gian chờ sau upload, có nguy cơ được hiểu là xóa ngay khi chưa có nơi tham chiếu. | ADR-0018, quyết định 7 | Bổ sung điều kiện qua thời gian chờ; độ dài thời gian chờ vẫn để quyết định khi triển khai. |

BR-229 có căn cứ từ quyết định mỗi lần upload tạo một file riêng trong ADR-0018. BR-251 áp dụng quy tắc bảo mật hiện có về không lộ dữ liệu cuộc họp ngoài quyền đọc; không phải một quyền mới. Tên thư mục không trống, thư mục tối đa năm cấp, chỉ xóa thư mục rỗng và giới hạn PDF/DOCX 50 MB đều có quy định trực tiếp trong ADR-0018.

## UI Behavior — Document Viewer

- PDF preview displays the document page by page.
- DOCX preview displays a continuous scrollable document.
- These are presentation behaviors, not Business Rules. The storage-reference decision remains in ADR-0018/design.

## Business Rules liên quan

| BR-ID  | Rule Statement                                                                                                          | Applies To                                                                                                   | Rationale                                                        | Enforced By                                          |
| ------ | ----------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------- | ---------------------------------------------------- |
| BR-68  | For an InProgress or Done Agenda Item, only Title and Description may be edited as planning fields.                     | UC-23a, UC-23b, UC-23c, UC-23d                                                                               | PO 14/10/2026 — atomic rule review                               |                                                      |
| BR-150 | A Guest cannot hold an internal Meeting Role, including Chair or Secretary.                                             | UC-28a, UC-37                                                                                                | PO 14/10/2026 — atomic rule review                               |                                                      |
| BR-153 | An external Guest must verify control of the invited email before Guest access is established.                          | UC-29, UC-37                                                                                                 |                                                                  |                                                      |
| BR-154 | External Guest access is limited to the invited Meeting.                                                                | UC-29, UC-37                                                                                                 |                                                                  |                                                      |
| BR-155 | External Guest access expires according to the configured validity period.                                              | UC-29, UC-37                                                                                                 |                                                                  |                                                      |
| BR-157 | Revoking an external Guest invitation immediately invalidates access granted by that invitation.                        | UC-28c, UC-29, UC-37                                                                                         |                                                                  |                                                      |
| BR-181 | A Guest has the same participation rights as a Participant within the invited Meeting.                                  | UC-04, UC-27, UC-28a, UC-28b, UC-29, UC-30, UC-31, UC-32a, UC-32c, UC-35, UC-37–UC-42, UC-52a, UC-52b, UC-55 | PO 14/10/2026 — atomic rule review                               | Guest participation / Meeting Participant Management |
| BR-183 | An uploaded file's content cannot be changed.                                                                           | UC-32a, UC-32c, UC-33a, UC-33c, UC-34, UC-36, UC-71                                                          | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-184 | Only the current Chair or Secretary may add or remove documents in their Meeting.                                       | UC-33a, UC-33c                                                                                               | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-185 | A Meeting document cannot be removed once Meeting Minutes exist.                                                        | UC-33c                                                                                                       | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-186 | Deleting a personal-library document does not remove the document from Meetings.                                        | UC-33c, UC-71a                                                                                               | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-187 | Only PDF and DOCX documents may be uploaded.                                                                            | UC-33a, UC-36                                                                                                | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-188 | Documents of a Secret Meeting may be read online but not downloaded.                                                    | UC-32c                                                                                                       | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-191 | Personal-library folders may be nested at most five levels deep.                                                        | UC-34, UC-36, UC-71b, UC-71c, UC-72a, UC-72b, UC-72c, UC-72d                                                 | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-192 | An automatically created Meeting folder is named “<Meeting title> - dd/mm/yyyy”, using the upload date in Vietnam time. | UC-33a                                                                                                       | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-193 | The same uploaded file may have only one active attachment in a Meeting.                                                | UC-33a, UC-33c                                                                                               | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-194 | A direct upload creates both the Meeting document and the personal-library document, or creates neither.                | UC-33a                                                                                                       | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-195 | Documents may be added in any Meeting state before the Meeting ends.                                                    | UC-33a                                                                                                       | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-196 | Documents cannot be added to or removed from a completed Agenda Item.                                                   | UC-33a, UC-33c                                                                                               | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-197 | Each Meeting-document addition or removal records who performed it and when.                                            | UC-33a, UC-33c                                                                                               | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-198 | Before publication, only the Meeting Organizer and the current Chair or Secretary may read Meeting documents.           | UC-32a, UC-32c                                                                                               | PO 14/10/2026 — atomic rule review                               | Document Management                                  |
| BR-199 | A Secretary may grant a Guest the additional Meeting permissions available to an internal Participant.                  | UC-28b, UC-32a, UC-32c                                                                                       | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026               | Document / Guest access                              |
| BR-200 | Additional permissions cannot override Guest restrictions or the prohibition on downloading Secret Meeting documents.   | UC-28b, UC-32a, UC-32c                                                                                       | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026               | Document / Guest access                              |
| BR-201 | A Guest receives no system Role.                                                                                        | UC-04, UC-28a                                                                                                | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026               | Document / Guest access                              |
| BR-202 | A Guest cannot receive delegated authority.                                                                             | UC-28b                                                                                                       | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026               | Document / Guest access                              |
| BR-203 | A Guest cannot moderate a Meeting room.                                                                                 | UC-41                                                                                                        | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026               | Document / Guest access                              |
| BR-204 | A Guest cannot use a personal document library.                                                                         | UC-34, UC-36, UC-71, UC-72                                                                                   | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026               | Document / Guest access                              |
| BR-205 | Meeting documents have no separate sharing option for Guests.                                                           | UC-32a, UC-32c, UC-33a                                                                                       | ADR-0016 / PO 13/10/2026 — cross-review 14/10/2026               | Document / Guest access                              |
| BR-206 | Uploaded documents have no versions.                                                                                    | UC-32a, UC-32c, UC-33a, UC-33c, UC-36                                                                        | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-208 | Changing document content requires uploading a new file and replacing the old Meeting document.                         | UC-33a, UC-33c, UC-36                                                                                        | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-209 | A Meeting document must come from the actor's own personal library or computer.                                         | UC-33a                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-210 | A file uploaded directly into a Meeting is also added to the uploader's personal library.                               | UC-33a                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-211 | A removed document is excluded from the Meeting's current document list.                                                | UC-32a, UC-33c                                                                                               | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-212 | Removing a Meeting document preserves its comments as history.                                                          | UC-33c                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-213 | A file is deleted by periodic cleanup when no personal-library document or active Meeting document uses it.             | UC-33c, UC-71a                                                                                               | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-214 | An uploaded document must not exceed 50 MB.                                                                             | UC-33a, UC-36                                                                                                | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-215 | An uploaded document's type is determined from its actual content.                                                      | UC-33a, UC-36                                                                                                | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-216 | Uploaded files retain their original format and content.                                                                | UC-33a, UC-36                                                                                                | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-219 | A personal-library folder belongs to one user.                                                                          | UC-34, UC-72                                                                                                 | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-220 | A folder name must not be empty.                                                                                        | UC-72a, UC-72b                                                                                               | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-221 | Folder names need not be unique.                                                                                        | UC-72a, UC-72b                                                                                               | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-222 | A folder cannot be moved into itself or one of its descendants.                                                         | UC-72c                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-223 | Only an empty folder can be deleted.                                                                                    | UC-72d                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-224 | A user may rename documents in their own personal library.                                                              | UC-71b                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-225 | A user may move personal-library documents only between their own folders or the library root.                          | UC-71c                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-226 | The first direct upload creates a personal-library folder for that user and Meeting.                                    | UC-33a                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-227 | Later uploads to the same Meeting reuse its existing personal-library folder, even after it is renamed or moved.        | UC-33a                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-228 | If the Meeting's personal-library folder was deleted, the next direct upload creates a new folder at the library root.  | UC-33a                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-229 | Separate uploads are different documents, even when their names or contents match.                                      | UC-33a, UC-36                                                                                                | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-231 | Removing a Meeting document does not remove it from personal libraries or other Meetings.                               | UC-33c                                                                                                       | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-232 | File cleanup preserves document metadata and comments, but does not retain the file content for historical reading.     | UC-33c, UC-71a                                                                                               | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-233 | A current Participant or Guest may read Meeting documents only after the Meeting is published.                          | UC-32a, UC-32c                                                                                               | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-234 | Meeting Approver status alone grants no document-reading access.                                                        | UC-32a, UC-32c                                                                                               | ADR-0018 / PO 14/10/2026 — atomic rule review                    | Document / Guest access                              |
| BR-235 | Declining a Meeting invitation does not revoke document-reading access.                                                 | UC-32a, UC-32c                                                                                               | ADR-0006 — cross-review 14/10/2026                               | Document / Guest access                              |
| BR-236 | All planning fields of a Pending Agenda Item may be edited.                                                             | UC-23b                                                                                                       | Split from former BR-68 — no change to agenda field restrictions | Agenda Management                                    |
| BR-237 | Only Pending Agenda Items may be deleted.                                                                               | UC-23c                                                                                                       | Split from former BR-68 — no change to agenda field restrictions | Agenda Management                                    |
| BR-238 | Only Pending Agenda Items may be reordered.                                                                             | UC-23d                                                                                                       | Split from former BR-68 — no change to agenda field restrictions | Agenda Management                                    |
| BR-239 | Deleting an Agenda Item preserves the positions of the remaining Items.                                                 | UC-23c                                                                                                       | Split from former BR-68 — no change to agenda field restrictions | Agenda Management                                    |
| BR-240 | Reordering exchanges only the existing positions of Pending Agenda Items.                                               | UC-23d                                                                                                       | Split from former BR-68 — no change to agenda field restrictions | Agenda Management                                    |
| BR-241 | Agenda planning changes preserve execution status, actual timestamps and the Meeting lifecycle.                         | UC-23b, UC-23c, UC-23d                                                                                       | Split from former BR-68 — no change to agenda field restrictions | Agenda Management                                    |
| BR-242 | A user may access only their own personal-library documents and folders.                                                | UC-34, UC-36, UC-71a–UC-71c, UC-72a–UC-72d                                                                   | ADR-0006 / ADR-0018                                              | Document Management                                  |
| BR-245 | Moving a personal-library document does not change its Meeting attachments.                                             | UC-71c                                                                                                       | ADR-0018                                                         | Document Management                                  |
| BR-246 | A comment must contain non-whitespace characters.                                                                       | UC-35                                                                                                        | Report 3 screen 7.5 — required comment text                      | Document Management                                  |
| BR-248 | A DOCX comment has no page reference.                                                                                   | UC-35                                                                                                        | ADR-0018                                                         | Document Management                                  |
| BR-249 | Anyone who can view a Meeting document may comment on it.                                                               | UC-35                                                                                                        | PO decision — no separate commenting permission                  | Document Management                                  |
| BR-250 | Each comment records its author and posting time.                                                                       | UC-35                                                                                                        | Report 3 screen 7.5                                              | Document Management                                  |
| BR-251 | Personal-library usage details show only Meetings the user may access.                                                  | UC-34                                                                                                        | Existing resource confidentiality rule                           | Document Management                                  |

## System Messages liên quan

Các mã đang dùng dưới đây dành sẵn cho đặc tả và có thông điệp tiếng Anh. MSG182, MSG201 và MSG203 đã ngừng dùng; không tái cấp mã.

| Code   | Message Type             | Context                                                                                                | Message Content                                                                                  |
| ------ | ------------------------ | ------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| MSG156 | Form error               | UC-32a / EX-1; UC-32c / EX-1; UC-35 / EX-2                                                             | Meeting documents are not accessible.                                                            |
| MSG157 | Form error               | UC-32a / EX-2                                                                                          | Unable to load the document list. Please try again.                                              |
| MSG158 | Form error               | UC-32c / EX-2                                                                                          | Unable to display the document. Please try again.                                                |
| MSG159 | Form error               | UC-32c / EX-3                                                                                          | Secret Meeting documents can only be viewed online and cannot be downloaded.                     |
| MSG160 | Form error               | UC-32c / EX-4                                                                                          | Unable to download the document. Please try again.                                               |
| MSG161 | Toast                    | UC-33a / Normal Flow                                                                                   | Document added to the Meeting successfully.                                                      |
| MSG162 | Form error               | UC-33a / EX-1                                                                                          | You no longer have permission to add documents to this Meeting.                                  |
| MSG163 | Field error / Form error | UC-33a / EX-2                                                                                          | Select your own available document and, if applicable, an available Agenda Item in this Meeting. |
| MSG164 | Field error / Form error | UC-33a / EX-3; UC-36 / EX-1                                                                            | Select a valid PDF or DOCX file no larger than 50 MB.                                            |
| MSG165 | Form error               | UC-33a / EX-4                                                                                          | This document is already attached to the Meeting.                                                |
| MSG166 | Form error               | UC-33a / EX-5                                                                                          | Unable to add the document. No document was added. Please try again.                             |
| MSG167 | Toast                    | UC-33c / Normal Flow                                                                                   | Document removed from the Meeting successfully.                                                  |
| MSG168 | Form error               | UC-33c / EX-1                                                                                          | The document cannot be removed because Meeting Minutes already exist.                            |
| MSG169 | Form error               | UC-33c / EX-2                                                                                          | You no longer have permission to remove documents from this Meeting.                             |
| MSG170 | Form error               | UC-33c / EX-3                                                                                          | This document is no longer attached to the Meeting. Refresh the document list.                   |
| MSG171 | Form error               | UC-33c / EX-4                                                                                          | Unable to remove the document. It remains attached to the Meeting. Please try again.             |
| MSG172 | Form error               | UC-33a / EX-6                                                                                          | Documents cannot be added after the Meeting has ended.                                           |
| MSG173 | Form error               | UC-33a / EX-7; UC-33c / EX-5                                                                           | Documents cannot be added to or removed from a completed Agenda Item.                            |
| MSG174 | Form error               | UC-33a / EX-8; UC-33c / EX-6                                                                           | This Meeting is not accessible.                                                                  |
| MSG175 | Form error               | UC-34 / EX-2; UC-71a / EX-1; UC-71b / EX-1; UC-71c / EX-1; UC-72b / EX-1; UC-72c / EX-1; UC-72d / EX-1 | This personal-library item is not available.                                                     |
| MSG176 | Form error               | UC-34 / EX-1                                                                                           | Unable to load your personal document library. Please try again.                                 |
| MSG177 | Toast                    | UC-36 / Normal Flow                                                                                    | Document uploaded to your personal library successfully.                                         |
| MSG178 | Form error               | UC-36 / EX-3                                                                                           | Unable to upload the document. No document was added. Please try again.                          |
| MSG179 | Form error               | UC-36 / EX-2; UC-71c / EX-2; UC-72a / EX-2; UC-72c / EX-2                                              | The selected destination folder is not available in your personal library.                       |
| MSG180 | Toast                    | UC-71a / Normal Flow                                                                                   | Document deleted from your personal library successfully.                                        |
| MSG181 | Form error               | UC-71a / EX-2                                                                                          | Unable to delete the document. Your personal library remains unchanged.                          |
| MSG183 | Toast                    | UC-71b / Normal Flow                                                                                   | Document renamed successfully.                                                                   |
| MSG184 | Form error               | UC-71b / EX-2                                                                                          | Unable to rename the document. Its name remains unchanged.                                       |
| MSG185 | Toast                    | UC-71c / Normal Flow                                                                                   | Document moved successfully.                                                                     |
| MSG186 | Form error               | UC-71c / EX-3                                                                                          | Unable to move the document. Its location remains unchanged.                                     |
| MSG187 | Toast                    | UC-72a / Normal Flow                                                                                   | Folder created successfully.                                                                     |
| MSG188 | Form error               | UC-72a / EX-1; UC-72b / EX-2                                                                           | Enter a folder name.                                                                             |
| MSG189 | Form error               | UC-72a / EX-3; UC-72c / EX-4                                                                           | Folders cannot be nested more than five levels deep.                                             |
| MSG190 | Form error               | UC-72a / EX-4                                                                                          | Unable to create the folder. No folder was created.                                              |
| MSG191 | Toast                    | UC-72b / Normal Flow                                                                                   | Folder renamed successfully.                                                                     |
| MSG192 | Form error               | UC-72b / EX-3                                                                                          | Unable to rename the folder. Its name remains unchanged.                                         |
| MSG193 | Form error               | UC-72c / EX-3                                                                                          | A folder cannot be moved into itself or one of its descendants.                                  |
| MSG194 | Toast                    | UC-72c / Normal Flow                                                                                   | Folder moved successfully.                                                                       |
| MSG195 | Form error               | UC-72c / EX-5                                                                                          | Unable to move the folder. Its location remains unchanged.                                       |
| MSG196 | Form error               | UC-72d / EX-2                                                                                          | Only an empty folder can be deleted.                                                             |
| MSG197 | Toast                    | UC-72d / Normal Flow                                                                                   | Folder deleted successfully.                                                                     |
| MSG198 | Form error               | UC-72d / EX-3                                                                                          | Unable to delete the folder. The folder remains unchanged.                                       |
| MSG199 | Toast                    | UC-35 / Normal Flow                                                                                    | Comment added successfully.                                                                      |
| MSG200 | Form error               | UC-35 / EX-1                                                                                           | Enter a comment.                                                                                 |
| MSG202 | Form error               | UC-35 / EX-3                                                                                           | Unable to add the comment. No comment was saved. Please try again.                               |
| MSG204 | Form error               | UC-35 / EX-4                                                                                           | Unable to load the document comments. Please try again.                                          |

# Đặc tả đầy đủ 14 Use Case

## UC-32a – View Meeting Documents

**Primary Actors:** Meeting Organizer, Chair, Secretary, Participant, Guest

**Secondary Actors:** None

### Description

Allows an authorized user to browse the documents currently attached to one Meeting. Guest document access follows Participant rules. (BR-181, BR-205)

### Preconditions

● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.
● PRE-2: The selected Meeting exists and the user is currently entitled to read its documents. (BR-181, BR-198, BR-233, BR-234, BR-153, BR-155, BR-157)

### Postconditions

● POST-1: Only currently attached documents available to the user are displayed.
● POST-2: Meeting documents, personal libraries and Meeting status remain unchanged.

### Normal Flow

View Meeting Documents

1. The user opens the Documents tab of the selected Meeting.
2. The system checks the user's current Meeting-document access, including the current invitation for a Guest. Organizer, Chair and Secretary access is available before publication; Participant and Guest access starts with publication. Approver status alone grants no document-reading access. (BR-181, BR-198, BR-233, BR-234, BR-200)
3. The system displays currently attached documents with their names, file types, associated Agenda Items where present, the people who added them and comment counts. Removed documents are excluded. (BR-211)
4. The user reviews the displayed list or empty result, and may adjust the Agenda Item filter or finish viewing. Selecting a document opens UC-32c.

### Alternative Flows

**AF-1 – Filter by Agenda Item**

1. At Step 4, the user selects an Agenda Item.
2. The system displays only available documents associated with that item.
3. The flow resumes at Step 4 of the Main Flow.

**AF-2 – No documents to display**

1. At Step 3, the Meeting has no currently attached documents, or the filter in AF-1 has no matches.
2. The system displays an empty list and explains that no documents are available for the current selection.
3. The user may clear the filter. The flow resumes at Step 4 of the Main Flow.

### Exception Flows

**EX-1 – Meeting documents are not accessible**

1. At Step 2, the Meeting is unavailable or the user's document-reading access has ended, including removal of a participant or revocation or expiry of a Guest invitation.
2. No document names or contents are disclosed. No Meeting information is changed. The system displays MSG156 – “Meeting documents are not accessible.”
3. The use case ends.

**EX-2 – Document list cannot be loaded**

1. At Step 3, the system cannot retrieve the available document list.
2. No Meeting information is changed. The system displays MSG157 – “Unable to load the document list. Please try again.”; the failed load is not presented as an empty list.
3. The use case ends.

## UC-32c – Preview Meeting Document

**Primary Actors:** Meeting Organizer, Chair, Secretary, Participant, Guest

**Secondary Actors:** None

### Description

Allows an authorized user to read a Meeting document online and download the same file when permitted. Comments are covered separately in UC-35. (BR-181, BR-188)

### Preconditions

● PRE-1: The internal user is signed in, or the Guest has a valid verified invitation for this Meeting.
● PRE-2: The selected document is currently attached to the Meeting and the user has current document-reading access. (BR-181, BR-211, BR-198, BR-233, BR-234, BR-153, BR-155, BR-157)

### Postconditions

● POST-1: The selected document is displayed for online reading; in AF-1, the user also receives the permitted download.
● POST-2: The file content, its Meeting association and its comments remain unchanged.

### Normal Flow

Preview Meeting Document

1. The user selects a document in the Meeting Documents tab or the documents of an Agenda Item.
2. The system checks current document-reading access and that the document is still attached to this Meeting. Organizer, Chair and Secretary may read before publication; Participant and Guest may read from publication onward; Approver status alone grants no reading access. (BR-181, BR-211, BR-198, BR-233, BR-234, BR-200)
3. The system opens the document viewer and displays the file name, its content and the Meeting sensitivity. A PDF is shown page by page and a DOCX as a continuous scrollable document.
4. The user reads the document. The system offers downloading only when the Meeting is not Secret. (BR-188)

### Alternative Flows

**AF-1 – Download the document**

1. At Step 4, the user chooses Download for a Meeting that is not Secret.
2. The system rechecks current access, that the document remains attached and the current Meeting sensitivity. (BR-181, BR-211, BR-188, BR-198, BR-233, BR-200)
3. The system provides the same uploaded file to the user. (BR-183, BR-216)
4. The flow resumes at Step 4 of the Main Flow.

### Exception Flows

**EX-1 – Document is no longer accessible**

1. At Step 2, or during the access check in AF-1, the document was removed or the user's Meeting-document access is no longer valid.
2. No further document content or download is provided. The file and Meeting remain unchanged. The system displays MSG156 – “Meeting documents are not accessible.”
3. The use case ends.

**EX-2 – Preview cannot be displayed**

1. At Step 3, the system cannot display the selected file.
2. The document remains attached and its content is unchanged. The system displays MSG158 – “Unable to display the document. Please try again.”
3. The use case ends.

**EX-3 – Download is prohibited**

1. During Step 2 of AF-1, the Meeting is Secret, including when its sensitivity changed after the viewer opened.
2. No download is provided. The document remains unchanged. The system displays MSG159 – “Secret Meeting documents can only be viewed online and cannot be downloaded.” (BR-188)
3. The flow resumes at Step 4 of the Main Flow for online reading.

**EX-4 – Download cannot be provided**

1. At Step 3 of AF-1, the system cannot provide the file.
2. The file and its Meeting association remain unchanged. The system displays MSG160 – “Unable to download the document. Please try again.”
3. The flow resumes at Step 4 of the Main Flow.

## UC-33a – Add Document to Meeting

**Primary Actors:** Chair, Secretary

**Secondary Actors:** None

### Description

Allows the current Chair or Secretary to add a document from their own personal library or computer to a Meeting, optionally associating it with an Agenda Item. A file is not revised in place: replacing content requires adding a new file and removing the old document through UC-33c. (BR-184, BR-206, BR-208)

### Preconditions

● PRE-1: The actor is signed in and is the current Chair or Secretary of the selected Meeting.
● PRE-2: The Meeting exists. (BR-184)
● PRE-3: The Meeting has not ended; any selected Agenda Item is Pending or InProgress. (BR-195, BR-196)

### Postconditions

● POST-1: The selected document appears in the Meeting Documents list with its optional Agenda Item, the actor and addition time.
● POST-2: A file selected from the personal library stays there unchanged; a file uploaded from the computer is also available in the actor's personal library. (BR-183, BR-210, BR-192, BR-194)
● POST-3: Meeting lifecycle status is unchanged.

### Normal Flow

Add Document from Personal Library

1. The Chair or Secretary opens Meeting Documents and chooses Add Document.
2. The system offers selection from the actor's personal library or upload from the computer.
3. The actor selects a document from their own library, optionally selects an Agenda Item of this Meeting and submits the addition.
4. The system rechecks the current Chair or Secretary assignment, ownership and availability of the selected document, that the Meeting has not ended, and that any selected Agenda Item is currently Pending or InProgress. A file already attached to this Meeting is not added a second time. (BR-184, BR-209, BR-193, BR-195, BR-196)
5. The system adds the document to the Meeting without changing the file or removing it from the personal library, and records the actor and addition time. (BR-183, BR-197)
6. The system updates the Meeting Documents list and displays MSG161 – “Document added to the Meeting successfully.”

### Alternative Flows

**AF-1 – Upload from the computer**

1. At Step 3, the actor chooses a file from the computer, optionally selects an Agenda Item and submits the addition.
2. The system checks that the actual file is PDF or DOCX and does not exceed 50 MB. (BR-187, BR-214, BR-215)
3. The system performs the current-authority, Meeting lifecycle and Agenda Item checks in Step 4. (BR-184, BR-195, BR-196)
4. The system adds the document to the Meeting and places it in the uploader's personal library, in the folder “<Meeting title> - dd/mm/yyyy” The date is the upload date in Vietnam time. The existing folder for that user and Meeting is reused even if renamed or moved. If it was deleted, a new folder is created at the library root using this upload date. Later uploads do not rename an existing folder. (BR-210, BR-192, BR-226, BR-227, BR-228)
5. Both additions must complete together; otherwise EX-5 applies. (BR-194) The flow resumes at Step 6 of the Main Flow.

### Exception Flows

**EX-1 – Actor is no longer authorized**

1. At Step 4, or the corresponding check in AF-1, the actor is no longer the current Chair or Secretary.
2. No document is added to the Meeting or personal library. Existing documents remain unchanged. The system displays MSG162 – “You no longer have permission to add documents to this Meeting.”
3. The use case ends.

**EX-2 – Selection is missing or no longer available**

1. At Step 4, no document was selected, the library document is unavailable or does not belong to the actor, or the selected Agenda Item is unavailable in this Meeting.
2. No document is added. The system identifies the invalid selection and displays MSG163 – “Select your own available document and, if applicable, an available Agenda Item in this Meeting.”
3. The flow resumes at Step 3 of the Main Flow.

**EX-3 – Uploaded file is invalid**

1. At Step 2 of AF-1, no file was selected, the actual file is not a valid PDF or DOCX, or it exceeds 50 MB. (BR-187, BR-214, BR-215)
2. No document is added to the Meeting or personal library. The system displays MSG164 – “Select a valid PDF or DOCX file no larger than 50 MB.”
3. The flow resumes at Step 1 of AF-1.

**EX-4 – Document is already attached**

1. At Step 4, the same previously uploaded file is already attached to this Meeting. A matching name or identical content in a separate upload is not a duplicate. (BR-193, BR-229)
2. No duplicate is added and existing documents remain unchanged. The system displays MSG165 – “This document is already attached to the Meeting.”
3. The flow resumes at Step 3 of the Main Flow.

**EX-5 – Addition cannot be completed**

1. At Step 5 or Step 4 of AF-1, the system cannot complete the addition.
2. No new Meeting document or personal-library entry is recorded (BR-194); existing documents remain unchanged. The system displays MSG166 – “Unable to add the document. No document was added. Please try again.”
3. The use case ends.

**EX-6 – Meeting has ended**

1. At Step 4, or the corresponding check in AF-1, the Meeting is Ended or in a subsequent lifecycle state, including when it ended after the Add Document dialog opened. (BR-195)
2. No document is added to the Meeting or personal library. Existing documents remain unchanged. The system displays MSG172 – “Documents cannot be added after the Meeting has ended.”
3. The use case ends.

**EX-7 – Agenda Item is completed**

1. At Step 4, or the corresponding check in AF-1, the selected Agenda Item is Done, including when it completed after selection. (BR-196)
2. No document is added to the Meeting or personal library. The system displays MSG173 – “Documents cannot be added to or removed from a completed Agenda Item.”
3. The actor may select a Pending or InProgress Item or omit the optional association. The flow resumes at Step 3 of the Main Flow, or Step 1 of AF-1 for a computer upload.

**EX-8 – Meeting is unavailable**

1. Before displaying or completing the addition, the selected Meeting cannot be accessed.
2. No document is added to the Meeting or personal library. The system displays MSG174 – “This Meeting is not accessible.” without disclosing whether the Meeting exists.
3. The use case ends.

## UC-33c – Remove Document from Meeting

**Primary Actors:** Chair, Secretary

**Secondary Actors:** None

### Description

Allows the current Chair or Secretary to remove a Meeting document when no Minutes exist and the document is not associated with a completed Agenda Item. (BR-184, BR-185, BR-196)

### Preconditions

● PRE-1: The actor is signed in and is the current Chair or Secretary of the Meeting. (BR-184)
● PRE-2: The selected document is currently attached to this Meeting.
● PRE-3: No Meeting Minutes exist for this Meeting. (BR-185)
● PRE-4: The document is not associated with a Done Agenda Item. (BR-196)

### Postconditions

● POST-1: The document no longer appears in this Meeting's current document list and is no longer available through this attachment.
● POST-2: Its comments remain as history; personal-library entries and attachments in other Meetings remain unchanged. (BR-212, BR-231, BR-197)
● POST-3: The actor and removal time are recorded. Meeting lifecycle status is unchanged.

### Normal Flow

Remove Document from Meeting

1. The Chair or Secretary selects an attached document and chooses Remove from Meeting.
2. The system displays a confirmation naming the document and explaining that it will leave this Meeting's list, while its comments remain as history and personal-library documents and other Meetings are unaffected.
3. The actor confirms removal.
4. The system rechecks the current Chair or Secretary assignment, that the document is still attached, that no Meeting Minutes exist, and that the document is not associated with a Done Agenda Item. Pending and InProgress Items do not block removal. (BR-184, BR-185, BR-196)
5. The system removes the document from this Meeting's current list, preserves its comments as history and records the actor and removal time. (BR-211, BR-212, BR-231, BR-197)
6. The system displays MSG167 – “Document removed from the Meeting successfully.”

### Alternative Flows

N/A

### Exception Flows

**EX-1 – Meeting Minutes already exist**

1. At Step 4, Meeting Minutes exist, including Minutes created after the confirmation was displayed. (BR-185)
2. The document remains attached and its comments remain unchanged. The system displays MSG168 – “The document cannot be removed because Meeting Minutes already exist.”
3. The use case ends.

**EX-2 – Actor is no longer authorized**

1. At Step 4, the actor is no longer the current Chair or Secretary. (BR-184)
2. No document is removed and comments remain unchanged. The system displays MSG169 – “You no longer have permission to remove documents from this Meeting.”
3. The use case ends.

**EX-3 – Document is no longer attached**

1. At Step 4, the selected document is no longer currently attached to this Meeting.
2. No further removal or comment change is made. The system displays MSG170 – “This document is no longer attached to the Meeting. Refresh the document list.”
3. The use case ends; the actor may refresh the list through UC-32a.

**EX-4 – Removal cannot be completed**

1. At Step 5, the system cannot complete the removal.
2. The document remains attached and its comments remain unchanged; no successful removal is recorded. The system displays MSG171 – “Unable to remove the document. It remains attached to the Meeting. Please try again.”
3. The use case ends.

**EX-5 – Agenda Item is completed**

1. At Step 4, the document is associated with a Done Agenda Item, including when the Item completed after the confirmation was displayed. (BR-196)
2. The document remains attached and its comments remain unchanged. The system displays MSG173 – “Documents cannot be added to or removed from a completed Agenda Item.”
3. The use case ends.

**EX-6 – Meeting is unavailable**

1. Before displaying or completing the removal, the selected Meeting cannot be accessed.
2. No document is removed and comments remain unchanged. The system displays MSG174 – “This Meeting is not accessible.” without disclosing whether the Meeting exists.
3. The use case ends.

## UC-34 – View Personal Document Library

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows an internal user to browse their own folders and documents and see permitted Meeting-usage details.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)

### Postconditions

● POST-1: The user sees only their own library items and permitted Meeting-usage details.
● POST-2: Documents, folders and Meeting attachments remain unchanged.

### Normal Flow

View Personal Document Library

1. The user opens Personal Document Library.
2. The system checks personal-library access. (BR-204, BR-242)
3. The system displays the user's folders and documents with names, file types and permitted Meeting-usage details. Other users' items and unauthorized Meeting details are excluded. (BR-219, BR-242, BR-251)
4. The user selects a folder or the library root.
5. The system displays the documents at that location. The user may browse the list or empty result, change the location or finish viewing.

### Alternative Flows

**AF-1 – Search or filter documents**

1. At Step 5, the user enters a document-name search or selects a file-type filter.
2. The system applies the criteria within the user's own displayed library location.
3. The flow resumes at Step 5 of the Main Flow.

**AF-2 – Empty library or no matching documents**

1. At Step 3 or Step 5, no documents match the location and criteria.
2. The system displays an empty result without treating it as a loading failure.
3. The flow resumes at Step 5 of the Main Flow.

### Exception Flows

**EX-1 – Library cannot be loaded**

1. At Step 2, Step 3 or Step 5, the system cannot retrieve the library.
2. No library or Meeting information changes. The system displays MSG176 – “Unable to load your personal document library. Please try again.”.
3. The use case ends.

**EX-2 – Selected folder is unavailable**

1. At Step 4, the folder was deleted or is no longer available in the user's library.
2. No information changes. The system displays MSG175 – “This personal-library item is not available.”.
3. The user returns to the library root. The flow resumes at Step 4 of the Main Flow.

## UC-35 – Comment on Meeting Document

**Primary Actors:** Meeting Organizer, Chair, Secretary, Participant, Guest

**Secondary Actors:** None

### Description

Allows anyone who can view a Meeting document to read and add comments. No separate commenting permission is required. PDF comments may include a page reference; DOCX comments have no page reference.

### Preconditions

● PRE-1: The internal user is signed in, or the Guest has verified the invited email and has a current valid invitation. (BR-153, BR-155, BR-157)
● PRE-2: The document is currently attached and the user has current read access. The same document-viewing access permits commenting; no separate commenting permission is required. (BR-198, BR-233, BR-234, BR-249)

### Postconditions

● POST-1: A comment is recorded with its author and posting time; a PDF comment may include a page reference and a DOCX comment has no page reference. (BR-248, BR-250)
● POST-2: The comment list and count are refreshed. Document content and Meeting status remain unchanged.

### Normal Flow

Comment on Meeting Document

1. The user opens the Meeting document viewer and its comments.
2. The system checks current document access and displays existing comments with author and posting time. (BR-249, BR-250)
3. The user enters a comment and chooses Send.
4. The system rechecks current read access, that the document remains attached, that the comment is not blank. (BR-246, BR-249)
5. The system saves the comment, author and posting time, with an optional page reference for PDF and no page reference for DOCX. (BR-248, BR-250)
6. The system refreshes the comments and count and displays MSG199 – “Comment added successfully.”.

### Alternative Flows

**AF-1 – Include a PDF page reference**

1. At Step 3, the user comments on a page being viewed in a PDF.
2. The comment includes that page reference; a page reference is not required for a document-wide comment.
3. The flow resumes at Step 4 of the Main Flow.

### Exception Flows

**EX-1 – Comment is empty**

1. At Step 4, the text is blank or contains only whitespace. (BR-246)
2. No comment is saved. The system displays MSG200 – “Enter a comment.”.
3. The flow resumes at Step 3 of the Main Flow.

**EX-2 – Document is no longer available**

1. At Step 2 or Step 4, the document is no longer attached, or the user can no longer view it, including after Guest invitation revocation or expiry.
2. No comment is saved. The system displays MSG156 – “Meeting documents are not accessible.”.
3. The use case ends.

**EX-3 – Operation cannot be completed**

1. At Step 5, the system cannot complete the operation.
2. No new comment is saved; existing comments remain unchanged. The system displays MSG202 – “Unable to add the comment. No comment was saved. Please try again.”.
3. The use case ends.

**EX-4 – Comments cannot be loaded**

1. At Step 2, current document access is valid but the system cannot retrieve comments.
2. No comment or document changes. The system displays MSG204 – “Unable to load the document comments. Please try again.”.
3. The use case ends.

## UC-36 – Upload Document to Personal Library

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows an internal user to upload one PDF or DOCX to the selected location in their personal library. Uploading does not attach it to a Meeting.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)

### Postconditions

● POST-1: A new personal-library document is available at the selected location.
● POST-2: The uploaded file retains its content and format; existing documents and Meeting attachments remain unchanged. (BR-183, BR-216, BR-229)

### Normal Flow

Upload Document to Personal Library

1. The user opens their library root or a folder and chooses Upload.
2. The user selects one file from the computer and submits it.
3. The system checks library access, the destination ownership and availability, and that the actual file is PDF or DOCX and no larger than 50 MB. (BR-187, BR-214, BR-215, BR-242)
4. The system adds a new document at the selected location with the file's name and original content. Separate uploads remain separate documents. (BR-183, BR-216, BR-229)
5. The system refreshes the library and displays MSG177 – “Document uploaded to your personal library successfully.”.

### Alternative Flows

N/A

### Exception Flows

**EX-1 – File is invalid**

1. At Step 3, no file is selected, the actual content is not a valid PDF or DOCX, or the file exceeds 50 MB. (BR-187, BR-214, BR-215)
2. No library document is created. The system displays MSG164 – “Select a valid PDF or DOCX file no larger than 50 MB.”.
3. The flow resumes at Step 2 of the Main Flow.

**EX-2 – Destination is unavailable**

1. At Step 3, the selected folder was deleted or does not belong to the user. (BR-242)
2. No library document is created. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.
3. The user selects an available folder or the library root. The flow resumes at Step 1 of the Main Flow.

**EX-3 – Operation cannot be completed**

1. At Step 4, the system cannot complete the operation.
2. No new library document is created; existing documents and Meeting attachments remain unchanged. The system displays MSG178 – “Unable to upload the document. No document was added. Please try again.”.
3. The use case ends.

## UC-71a – Delete Document from Personal Library

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows an internal user to delete a document from their personal library without removing documents already attached to Meetings.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)
● PRE-3: The selected document is in the user's library.

### Postconditions

● POST-1: The document no longer appears in the personal library.
● POST-2: Existing Meeting attachments remain readable; eventual cleanup applies only when no active use remains. Historical metadata and comments are retained. (BR-186, BR-213, BR-232)

### Normal Flow

Delete Document from Personal Library

1. The user selects a personal-library document and chooses Delete.
2. The system requests confirmation, naming the document and explaining that deletion affects the personal library only.
3. The user confirms deletion.
4. The system rechecks ownership and that the library document still exists. (BR-242)
5. The system deletes the personal-library document without removing Meeting attachments. (BR-186)
6. The system refreshes the library and displays MSG180 – “Document deleted from your personal library successfully.”.

### Alternative Flows

N/A

### Exception Flows

**EX-1 – Document is unavailable**

1. At Step 4, the selected document is missing or no longer belongs to the user.
2. No library document is deleted; Meeting documents remain unchanged. The system displays MSG175 – “This personal-library item is not available.”.
3. The use case ends.

**EX-2 – Operation cannot be completed**

1. At Step 5, the system cannot complete the operation.
2. The library document remains available and Meeting documents remain unchanged. The system displays MSG181 – “Unable to delete the document. Your personal library remains unchanged.”.
3. The use case ends.

## UC-71b – Rename Document

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows the owner to rename a personal-library document without changing its file content.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)
● PRE-3: The document exists in the user's library.

### Postconditions

● POST-1: The document is displayed under the new name in the personal library.
● POST-2: File content and library location remain unchanged. (BR-183)

### Normal Flow

Rename Document

1. The user chooses Rename for a personal-library document.
2. The system displays its current name.
3. The user enters a new name and submits it.
4. The system rechecks ownership and document availability. (BR-242)
5. The system saves the new personal-library document name. File content remains unchanged. (BR-183, BR-224)
6. The system refreshes the displayed name and displays MSG183 – “Document renamed successfully.”.

### Alternative Flows

N/A

### Exception Flows

**EX-1 – Document is unavailable**

1. At Step 4, the selected document is missing or no longer belongs to the user.
2. No document name changes. The system displays MSG175 – “This personal-library item is not available.”.
3. The use case ends.

**EX-2 – Operation cannot be completed**

1. At Step 5, the system cannot complete the operation.
2. The previous name, content and Meeting attachments remain unchanged. The system displays MSG184 – “Unable to rename the document. Its name remains unchanged.”.
3. The use case ends.

## UC-71c – Move Document to Folder

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows the owner to move a personal-library document to another owned folder or the library root.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)
● PRE-3: The document exists in the user's library.

### Postconditions

● POST-1: The document is available at the selected library location.
● POST-2: Document name, file content and Meeting attachments remain unchanged. (BR-225, BR-245)

### Normal Flow

Move Document to Folder

1. The user chooses Move for a personal-library document.
2. The system displays destinations within the user's library.
3. The user selects an owned destination folder and confirms.
4. The system rechecks document ownership and availability and destination ownership and availability. (BR-225, BR-242)
5. The system moves only the library document to that location. (BR-245)
6. The system refreshes the library and displays MSG185 – “Document moved successfully.”.

### Alternative Flows

**AF-1 – Move to the library root**

1. At Step 3, the user selects the library root instead of a folder.
2. The move has no destination folder. (BR-225)
3. The flow resumes at Step 4 of the Main Flow.

### Exception Flows

**EX-1 – Document is unavailable**

1. At Step 4, the selected document is missing or no longer belongs to the user.
2. The document remains at its previous location. The system displays MSG175 – “This personal-library item is not available.”.
3. The use case ends.

**EX-2 – Destination is unavailable**

1. At Step 4, the destination was deleted or does not belong to the user. (BR-225, BR-242)
2. The document stays at its previous location. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.
3. The flow resumes at Step 3 of the Main Flow.

**EX-3 – Operation cannot be completed**

1. At Step 5, the system cannot complete the operation.
2. The document stays at its previous location and its Meeting attachments are unchanged. The system displays MSG186 – “Unable to move the document. Its location remains unchanged.”.
3. The use case ends.

## UC-72a – Create Folder

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows an internal user to create a folder at the library root or inside an owned folder.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)

### Postconditions

● POST-1: A new owned folder is available at the selected location.
● POST-2: Existing folders, documents and Meeting attachments remain unchanged.

### Normal Flow

Create Folder

1. The user selects the library root or an owned parent folder and chooses New Folder.
2. The user enters a folder name and submits it.
3. The system checks ownership, parent availability, a nonblank name and a resulting depth of at most five levels. Duplicate folder names are permitted. (BR-191, BR-219, BR-220, BR-221, BR-242)
4. The system creates the folder at that location.
5. The system refreshes the folder list and displays MSG187 – “Folder created successfully.”.

### Alternative Flows

N/A

### Exception Flows

**EX-1 – Folder name is blank**

1. At Step 3, the name is empty or contains only whitespace. (BR-220)
2. No folder is created. The system displays MSG188 – “Enter a folder name.”.
3. The flow resumes at Step 2 of the Main Flow.

**EX-2 – Parent is unavailable**

1. At Step 3, the selected parent was deleted or does not belong to the user. (BR-219, BR-242)
2. No folder is created. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.
3. The flow resumes at Step 1 of the Main Flow.

**EX-3 – Folder depth exceeds the limit**

1. At Step 3, the new folder would be deeper than level five. (BR-191)
2. No folder is created. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”.
3. The user selects a shallower parent. The flow resumes at Step 1 of the Main Flow.

**EX-4 – Operation cannot be completed**

1. At Step 4, the system cannot complete the operation.
2. No folder is created; existing library items remain unchanged. The system displays MSG190 – “Unable to create the folder. No folder was created.”.
3. The use case ends.

## UC-72b – Rename Folder

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows the owner to rename a folder without moving it or changing its contents.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)
● PRE-3: The folder exists in the user's library.

### Postconditions

● POST-1: The folder displays the new name.
● POST-2: Its location and contents remain unchanged; an automatic Meeting folder remains reusable. (BR-227)

### Normal Flow

Rename Folder

1. The user chooses Rename for an owned folder.
2. The system displays the current folder name.
3. The user enters a new name and submits it.
4. The system rechecks ownership, availability and a nonblank name; duplicate names are permitted. (BR-219, BR-220, BR-221, BR-242)
5. The system saves the new folder name without changing its contents or location.
6. The system refreshes the folder name and displays MSG191 – “Folder renamed successfully.”.

### Alternative Flows

N/A

### Exception Flows

**EX-1 – Folder is unavailable**

1. At Step 4, the selected folder is missing or no longer belongs to the user.
2. No folder name changes. The system displays MSG175 – “This personal-library item is not available.”.
3. The use case ends.

**EX-2 – Folder name is blank**

1. At Step 4, the name is empty or contains only whitespace. (BR-220)
2. The previous name remains. The system displays MSG188 – “Enter a folder name.”.
3. The flow resumes at Step 3 of the Main Flow.

**EX-3 – Operation cannot be completed**

1. At Step 5, the system cannot complete the operation.
2. The folder name, location and contents remain unchanged. The system displays MSG192 – “Unable to rename the folder. Its name remains unchanged.”.
3. The use case ends.

## UC-72c – Move Folder

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows the owner to move a folder with its contents to another owned folder or the library root.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)
● PRE-3: The folder exists in the user's library.

### Postconditions

● POST-1: The folder and its contents are available at the selected location, within five levels.
● POST-2: Folder and document names, file content and Meeting attachments remain unchanged. An automatic Meeting folder remains reusable. (BR-191, BR-227)

### Normal Flow

Move Folder

1. The user chooses Move for an owned folder.
2. The system displays destinations within the user's library.
3. The user selects an owned destination folder and confirms.
4. The system rechecks source and destination ownership and availability. The destination cannot be the source or its descendant, and the moved folder and every descendant must remain within five levels. (BR-191, BR-219, BR-222, BR-242)
5. The system moves the folder with its documents and subfolders, without changing their names or Meeting attachments.
6. The system refreshes the folder tree and displays MSG194 – “Folder moved successfully.”.

### Alternative Flows

**AF-1 – Move to the library root**

1. At Step 3, the user selects the library root instead of another folder.
2. The moved folder becomes a first-level folder; its descendants remain subject to the depth limit. (BR-191)
3. The flow resumes at Step 4 of the Main Flow.

### Exception Flows

**EX-1 – Folder is unavailable**

1. At Step 4, the selected folder is missing or no longer belongs to the user.
2. No folder or contained item moves. The system displays MSG175 – “This personal-library item is not available.”.
3. The use case ends.

**EX-2 – Destination is unavailable**

1. At Step 4, the destination was deleted or does not belong to the user. (BR-219, BR-242)
2. No folder or contained item moves. The system displays MSG179 – “The selected destination folder is not available in your personal library.”.
3. The flow resumes at Step 3 of the Main Flow.

**EX-3 – Destination creates a cycle**

1. At Step 4, the destination is the folder itself or one of its descendants. (BR-222)
2. No folder or contained item moves. The system displays MSG193 – “A folder cannot be moved into itself or one of its descendants.”.
3. The flow resumes at Step 3 of the Main Flow.

**EX-4 – Resulting depth exceeds the limit**

1. At Step 4, the moved folder or any descendant would be deeper than level five. (BR-191)
2. No folder or contained item moves. The system displays MSG189 – “Folders cannot be nested more than five levels deep.”.
3. The flow resumes at Step 3 of the Main Flow.

**EX-5 – Operation cannot be completed**

1. At Step 5, the system cannot complete the operation.
2. The folder and all contained items remain at their previous locations. The system displays MSG195 – “Unable to move the folder. Its location remains unchanged.”.
3. The use case ends.

## UC-72d – Delete Folder

**Primary Actors:** Internal user

**Secondary Actors:** None

### Description

Allows the owner to delete an empty personal-library folder. If an automatic Meeting folder is deleted, a later direct Meeting upload recreates it.

### Preconditions

● PRE-1: The internal user is signed in.
● PRE-2: The operation concerns the user's own personal library. Guest has no personal library. (BR-204, BR-242)
● PRE-3: The folder exists and contains no documents or subfolders. (BR-223)

### Postconditions

● POST-1: The empty folder no longer appears in the library.
● POST-2: Other folders, documents and Meeting attachments remain unchanged. Future recreation of an automatic Meeting folder follows its naming and location rules. (BR-192, BR-228)

### Normal Flow

Delete Folder

1. The user chooses Delete for an owned folder.
2. The system asks for confirmation and names the folder.
3. The user confirms deletion.
4. The system rechecks current ownership and availability and that the folder contains neither documents nor subfolders. (BR-219, BR-223, BR-242)
5. The system deletes only the empty folder.
6. The system refreshes the folder tree and displays MSG197 – “Folder deleted successfully.”.

### Alternative Flows

N/A

### Exception Flows

**EX-1 – Folder is unavailable**

1. At Step 4, the selected folder is missing or no longer belongs to the user.
2. No folder is deleted. The system displays MSG175 – “This personal-library item is not available.”.
3. The use case ends.

**EX-2 – Folder is no longer empty**

1. At Step 4, the folder contains a document or subfolder, including an item added after confirmation was displayed. (BR-223)
2. The folder and its contents remain unchanged. The system displays MSG196 – “Only an empty folder can be deleted.”.
3. The use case ends.

**EX-3 – Operation cannot be completed**

1. At Step 5, the system cannot complete the operation.
2. The folder and all other library items remain unchanged. The system displays MSG198 – “Unable to delete the folder. The folder remains unchanged.”.
3. The use case ends.
