# 01 · Yêu cầu (Requirements) — TrustCabinet

Thư mục lưu trữ tài liệu yêu cầu nghiệp vụ và đặc tả Use Case cho hệ thống họp không giấy tờ **TrustCabinet**.

Đây là **Single Source of Truth** về mặt nghiệp vụ của dự án:
- Mọi chức năng, luồng nghiệp vụ và quy tắc kiểm thử đều bám sát theo các đặc tả tại đây.
- Khung phạm vi hiển thị trên landing page ([frontend/src/pages/scopeData.ts](../../frontend/src/pages/scopeData.ts)) phải đồng bộ với tài liệu này.

---

## 1. Cấu trúc tài liệu

- [05-use-case-specifications.md](05-use-case-specifications.md) — Đặc tả Use Case UC-01 → UC-52; UC-01 → UC-37 và BR-01 → BR-77 từ Report 3, F08/GOF09 và các phần liên quan đã được bổ sung từ Report 3 source-of-truth.
- [06-business-rules.md](06-business-rules.md) — Danh mục tập trung BR-01 → BR-103; UC Specification và Sequence Diagram dẫn chiếu mã tại đây.
- `01-pham-vi.md` — Danh sách phạm vi chức năng theo mô hình MoSCoW.
- `02-user-stories.md` — Danh sách User Story và Acceptance Criteria (AC).
- `03-yeu-cau-phi-chuc-nang.md` — Yêu cầu phi chức năng (NFR): hiệu năng, bảo mật, tải người dùng đồng thời.
- `04-du-lieu-ca-nhan.md` — Dữ liệu cá nhân và cam kết bảo vệ dữ liệu theo Nghị định 13/2023/NĐ-CP (ghi âm, biên bản, danh tính).

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
     - Xảy ra khi điều kiện nghiệp vụ bị từ chối hoàn toàn (ví dụ: Cuộc họp đã bị hủy, không có quyền thực hiện, phiên họp đã kết thúc).
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
