# 01 · Yêu cầu (Requirements) — TrustCabinet

Thư mục lưu trữ tài liệu yêu cầu nghiệp vụ và đặc tả Use Case cho hệ thống họp không giấy tờ **TrustCabinet**.

Nguồn chuẩn là ba báo cáo DOCX trên Google Drive (Report 1, 2, 3), được đồng bộ sang
[08-reports/](../08-reports/README.md). Các file ở đây trích và sắp xếp lại nội dung đó để dev và tester tra cứu nhanh:

- Mọi chức năng, luồng nghiệp vụ và quy tắc kiểm thử bám theo các đặc tả tại đây.
- Khi Report 3 thay đổi, sửa DOCX local trước, sinh snapshot bằng `scripts/sync_reports.py`, rồi đồng bộ lại thư mục này trong **cùng commit**. Quy tắc snapshot Report 1–7 nằm ở [08-reports/README.md](../08-reports/README.md).

---

## 1. Cấu trúc tài liệu

- [00-master-use-case-inventory.md](00-master-use-case-inventory.md) — Danh mục tổng thể: 15 Feature (FE-01 → FE-15),
  72 Use Case (UC-01 → UC-72), actor, quan hệ `<<extend>>`/`<<include>>`, **bảng đổi số UC cũ → mới** sau đợt gom UC
  ngày 10/10/2026.
- [05-use-case-specifications.md](05-use-case-specifications.md) — Đặc tả Use Case đồng bộ từ Report 3 §II (UC đã đặc tả
  và UC placeholder), có link tới sequence diagram tương ứng. FE-11 đã có đặc tả UC-52a, UC-52b,
  UC-53–UC-57 theo quyết định 14/10/2026; các sequence của FE-11 chưa được tạo trong đợt đặc tả này.
- [06-business-rules.md](06-business-rules.md) — Danh mục Business Rules đồng bộ từ Report 3 §V.1 (267 BR đang dùng trong
  dải BR-01 → BR-319, trong đó BR-315 → BR-319 thêm và đồng bộ Report 3 ngày 22/10/2026; danh sách BR ngừng dùng ở cuối file). FE-07/FE-11 đã đồng bộ ngày 15/10/2026,
  gồm UC-33b/33d theo ADR-0020; các phần màn hình chờ cập nhật theo [note bàn giao](../notes/README.md).
- Mã thông báo `MSGnn`: [03-api/00-danh-muc-ma-thong-bao.md](../03-api/00-danh-muc-ma-thong-bao.md) là nguồn chuẩn duy nhất.

Yêu cầu phi chức năng, phạm vi MoSCoW và dữ liệu cá nhân hiện nằm trong Report 1 và Report 3 §IV; chưa tách file riêng.

---

## 2. Quy ước viết Use Case Specification (Business-Oriented)

Đặc tả Use Case phải phản ánh **góc nhìn nghiệp vụ (Business Perspective)** của Actor và Hệ thống, phục vụ việc giao tiếp rõ ràng giữa Product Owner / BA, Khách hàng, Developer và Tester.

### 2.1. Nguyên tắc cốt lõi: Tập trung nghiệp vụ, không kỹ thuật
- **Nghiệp vụ thuần túy**: Mô tả hành động của Actor (nhập liệu, chọn, xác nhận) và phản hồi của Hệ thống (kiểm tra điều kiện, ghi nhận thay đổi, thông báo kết quả).
- **Business Rules dùng chung**: Định nghĩa BR tập trung tại [06-business-rules.md](06-business-rules.md). UC Specification và Sequence Diagram chỉ dẫn chiếu đúng ID (`BR-XX`) ở bước áp dụng; không chép lại một phiên bản BR riêng.
- **Tuyệt đối KHÔNG đưa chi tiết kỹ thuật vào Use Case Spec**:
  - Không nhắc đến HTTP method, URL endpoint, HTTP status code (`GET /api/meetings`, `200 OK`, `400 Bad Request`, `500 Internal Server Error`).
  - Không nhắc đến Database table, SQL query, ORM (`SELECT`, `SaveChanges()`, `Foreign Key`).
  - Không nhắc đến kỹ thuật mạng, giao thức hay thư viện (`SignalR hub`, `LiveKit token`, `JWT claims`, `Redis cache`, `bcrypt`, `middleware`).
  - *Lưu ý*: Các chi tiết kỹ thuật trên thuộc phạm vi của [02-design/](../02-design/), [03-api/](../03-api/) và [06-sequencediagram/](../06-sequencediagram/).

---

### 2.2. Luồng sự kiện chính (Main Flow / Basic Flow)
- Là kịch bản lý tưởng (happy path) mà Actor thực hiện để đạt được mục tiêu nghiệp vụ nhanh nhất mà không gặp lỗi.
- Mỗi bước là một tương tác rõ ràng giữa Actor và Hệ thống.
- Kết thúc bằng việc hoàn thành mục tiêu Use Case và thỏa mãn **Success Postconditions**.

---

### 2.3. Luồng sự kiện thay thế (Alternative Flow - AF)
- **Định nghĩa**: Alternative Flow là một **con đường khác với Main Flow nhưng vẫn đạt mục tiêu nghiệp vụ và Success Postcondition** của Use Case.
- **Quy tắc viết AF**:
  - Nêu rõ điểm rẽ từ bước nào của Main Flow.
  - Nếu luồng tiếp tục quay lại Main Flow, phải kết thúc bằng:
    > *"The flow resumes at Step X of the Main Flow."*
  - **Khi nào dùng AF**:
    - Trường hợp có nhiều cách lựa chọn hợp lệ để hoàn thành mục tiêu (ví dụ: Tạo cuộc họp từ bản nháp vs. Tạo cuộc họp mới trực tiếp).
    - Với UC dạng danh sách (View List): Thao tác **Search / Filter** là Alternative Flow khi người dùng tìm kiếm theo tiêu chí khác nhau để xem danh sách.
    - Kết quả tìm kiếm rỗng (*"No matching results"*): Hệ thống xử lý đúng yêu cầu và vẫn đạt mục tiêu tra cứu danh sách.
  - **Khi nào KHÔNG dùng AF**:
    - Không dùng AF cho trường hợp: người dùng hủy thao tác, không lưu được, lỗi kết nối, dữ liệu không hợp lệ, không đủ quyền hạn.
    - Các thao tác UI nhỏ (sắp xếp cột `Sort`, chuyển trang `Pagination`, mở/đóng accordion) không làm thay đổi bản chất kịch bản nghiệp vụ thì mô tả trong Business Rules hoặc UI Behavior, không tách thành AF.

---

### 2.4. Luồng ngoại lệ / Lỗi (Exception Flow - EX)
- **Định nghĩa**: Exception Flow được kích hoạt khi **có lỗi, vi phạm quy tắc nghiệp vụ (Business Rules) hoặc điều kiện không hợp lệ** khiến Main Flow không thể hoàn thành bình thường.
- **Phân loại EX**:
  1. **Recoverable Exception (Có thể sửa và tiếp tục)**:
     - Xảy ra khi dữ liệu nhập không hợp lệ hoặc thiếu thông tin mà người dùng có thể điều chỉnh lại ngay.
     - Sau khi hệ thống thông báo lỗi, nếu cho phép người dùng điều chỉnh, kết thúc bằng:
       > *"The flow resumes at Step X of the Main Flow."*
  2. **Fatal / Terminated Exception (Dừng luồng / Thất bại)**:
     - Xảy ra khi điều kiện nghiệp vụ bị từ chối hoàn toàn (ví dụ: cuộc họp không còn ở trạng thái cho phép, không có quyền thực hiện, cuộc họp đã kết thúc).
     - Luôn mô tả **kết quả nghiệp vụ (Business Outcome)** trước (ví dụ: *"No meeting is created."*, *"The meeting status remains unchanged."*), sau đó kết thúc bằng:
       > *"The use case ends."*
- **Quy tắc thông báo**: Mọi Exception Flow phải dẫn chiếu đúng **Mã thông báo hệ thống (`MSGxx`)** kèm thông điệp đầy đủ theo danh mục [00-danh-muc-ma-thong-bao.md](../03-api/00-danh-muc-ma-thong-bao.md).

---

### 2.5. Bảng tổng kết quy tắc kiểm tra nhanh

| Tiêu chí kiểm tra | Phân loại luồng | Kết thúc luồng bằng câu |
|---|---|---|
| Nhánh rẽ **vẫn đạt** mục tiêu nghiệp vụ và Success Postcondition | **Alternative Flow (AF)** | *"The flow resumes at Step X of the Main Flow."* hoặc hoàn tất mục tiêu |
| Nhánh rẽ **không đạt** mục tiêu vì dữ liệu không hợp lệ (người dùng sửa lại được) | **Exception Flow (EX - Recoverable)** | *"The flow resumes at Step X of the Main Flow."* |
| Nhánh rẽ **không đạt** mục tiêu vì vi phạm quy tắc / điều kiện tiên quyết | **Exception Flow (EX - Terminated)** | *"[Business outcome]. The use case ends."* |
| Thao tác UI thuần túy (Sort, Next page, Resize...) | **Business Rule / UI behavior** | Không tách thành flow riêng |

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

## 3. Khung mẫu đặc tả Use Case (Template chuẩn)

```markdown
### UC-[XX]: [Tên Use Case Tiếng Anh] ([Tên Use Case Tiếng Việt])

- **Actor chính (Primary Actor)**: [Tên Actor thực hiện theo Report 3, ví dụ: Meeting Organizer / Secretary / Chair]
- **Mô tả (Description)**: [Tóm tắt ngắn gọn mục tiêu nghiệp vụ của Use Case]
- **Điều kiện tiên quyết (Preconditions)**:
  - [Điều kiện 1: Đã đăng nhập và có quyền tương ứng...]
  - [Điều kiện 2: Trạng thái của đối tượng nghiệp vụ cần có trước khi thực hiện...]
- **Điều kiện sau (Postconditions)**:
  - [Kết quả đạt được khi thành công: Dữ liệu được tạo/cập nhật, trạng thái mới, thông báo...]
- **Sơ đồ liên quan (Related Diagrams)**:
  - Sequence Diagram: [Link đến file sơ đồ tương ứng]

#### Luồng sự kiện chính (Main Flow):
1. Actor chọn thực hiện hành động [X] trên màn hình [Y].
2. Hệ thống hiển thị giao diện nhập liệu / yêu cầu xác nhận.
3. Actor cung cấp các thông tin cần thiết và xác nhận thực hiện.
4. Hệ thống kiểm tra tính hợp lệ của thông tin và các quy tắc nghiệp vụ (BR-[XX]).
5. Hệ thống ghi nhận dữ liệu mới và cập nhật trạng thái tương ứng.
6. Hệ thống hiển thị thông báo thành công: `MSGxx - [Nội dung thông báo thành công]`.

#### Luồng sự kiện thay thế (Alternative Flows):
- **AF-1: [Tên kịch bản thay thế đạt mục tiêu]**
  1. Tại bước [X] của Main Flow, Actor chọn [phương thức thay thế]...
  2. Hệ thống xử lý theo phương thức thay thế...
  3. The flow resumes at Step [Y] of the Main Flow.

#### Luồng ngoại lệ / Lỗi (Exception Flows):
- **EX-1: [Tình huống dữ liệu không hợp lệ / Vi phạm quy tắc nghiệp vụ]**
  1. Tại bước [X] của Main Flow, hệ thống phát hiện [điều kiện lỗi theo BR-XX].
  2. Hệ thống hiển thị thông báo lỗi: `MSGxx - [Nội dung thông báo lỗi]`.
  3. The flow resumes at Step [X] of the Main Flow (hoặc: [Business outcome]. The use case ends).

#### Quy tắc nghiệp vụ liên quan (Business Rules):
- **BR-[XX]**: [Nội dung quy tắc nghiệp vụ cần tuân thủ]
```

---

## 4. Checklist kiểm duyệt Use Case Specification

- [ ] **Góc nhìn nghiệp vụ**: Không chứa HTTP method, HTTP code, SQL, DB table hay thư viện kỹ thuật.
- [ ] **Khớp Actor**: Đúng Primary Actor được định nghĩa trong Report 3.
- [ ] **Tiền điều kiện & Hậu điều kiện**: Nêu rõ trạng thái trước và sau khi Use Case hoàn thành.
- [ ] **Phân định AF vs EX**: Mọi AF đều đạt success postcondition; mọi lỗi/hủy/từ chối đều là EX.
- [ ] **Mã thông báo (MSG)**: Mọi thông báo lỗi và thành công đều có mã `MSGxx` và thông điệp chuẩn xác từ `docs/03-api/00-danh-muc-ma-thong-bao.md`.
- [ ] **Quy tắc nghiệp vụ (BR)**: Đánh mã `BR-XX` rõ ràng và được dẫn chiếu tại các bước kiểm tra tương ứng.

- [07-ra-soat-guest-va-diem-danh.md](07-ra-soat-guest-va-diem-danh.md) — quyết định Guest 13/10/2026 và phân tích BR-122 chưa chốt.
