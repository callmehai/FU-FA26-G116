# Kế hoạch triển khai: Xử lý Mục 3.2 FE-07 (Kho tài liệu và tài liệu cuộc họp) — BA Final Review

**Mục tiêu:** Hoàn thiện toàn bộ các hạng mục BA Final Review còn lại của Feature FE-07 (SCRUM-21, 22, 23, 58, 107, 110, 111, 112, 113) theo đúng ghi chú mục 3.2 trong [note 14](14-ghi-chu-nhan-27-28-10-2026-phan-loai-theo-fe.md), đưa tài liệu đặc tả (UC Specs, Report 3), ma trận quyền (Permission Matrix) và sơ đồ (Sequence, Class Diagrams) khớp 100% với mã nguồn as-built đã merge trên `main`.

**Nhánh git:** `docs/ba-final-review-fe-07` (đã tạo từ `origin/main`).

---

## Bản đồ tệp tin tác động (File Changes Map)

- **Đặc tả Use Case & Business Rules:**
  - `docs/01-requirements/05-use-case-specifications.md`
  - `docs/01-requirements/00-master-use-case-inventory.md`
- **Báo cáo SRS (Report 3):**
  - `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`
  - `docs/08-reports/Report-3_Software_Requirement_Specification.md` (sinh tự động qua script)
- **Sơ đồ trình tự (Sequence Diagrams):**
  - `docs/06-sequencediagram/document-management/*.txt` (PlantUML)
  - `docs/06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio`
  - `docs/06-sequencediagram/drawio-png/` (xuất ảnh tương ứng)
- **Sơ đồ lớp (Class Diagram):**
  - `docs/07-classdiagram/feature-07-document-management-class-diagram.drawio`
  - `docs/07-classdiagram/feature-07-document-management-class-diagram.drawio.png`
- **Ghi chú & Tài liệu đối chiếu:**
  - `docs/notes/ban-giao-ba-final-review/14-ghi-chu-nhan-27-28-10-2026-phan-loai-theo-fe.md`
  - `docs/notes/ban-giao-ba-final-review/15-plan-fe-07-ba-final-review.md` (file này)

---

## Phân rã chi tiết các Task

### Task 1: Bổ sung Exception Flows (EX) cho `MSG347`, `MSG348`, `MSG349` vào Đặc tả UC & Report 3

**Phạm vi nghiệp vụ:**
1. **`MSG347` (400 - Tên tài liệu không hợp lệ):**
   - Áp dụng: UC-71b (Đổi tên tài liệu cá nhân).
   - Tình huống: Tên tài liệu sau khi cắt khoảng trắng bị trống, hoặc chỉ chứa phần mở rộng (ví dụ `.pdf`), chứa ký tự điều khiển, vượt quá 255 ký tự, hoặc thay đổi phần mở rộng file ban đầu.
   - Nội dung thông báo: *"Tên tài liệu không hợp lệ. Tên không được trống và phải giữ phần đuôi .pdf hoặc .docx của tệp."*
2. **`MSG348` (400 - Nội dung vượt quá độ dài cho phép):**
   - Áp dụng UC-72a (Tạo thư mục mới) & UC-72b (Đổi tên thư mục): Tên thư mục vượt quá 400 ký tự.
   - Áp dụng UC-35 (Góp ý tài liệu): Nội dung góp ý hoặc phản hồi vượt quá 4.000 ký tự.
   - Nội dung thông báo: *"Nội dung vượt quá độ dài cho phép."*
3. **`MSG349` (409 - Cuộc họp đã huỷ):**
   - Áp dụng: Toàn bộ thao tác thay đổi tài liệu cuộc họp khi cuộc họp ở trạng thái `Cancelled` (BR-299):
     - UC-33a (Thêm tài liệu vào cuộc họp từ kho hoặc tải từ máy).
     - UC-33b (Gắn tài liệu vào mục nghị sự).
     - UC-33c (Gỡ tài liệu khỏi cuộc họp).
     - UC-33d (Gỡ tài liệu khỏi mục nghị sự).
     - UC-35 (Gửi góp ý hoặc phản hồi trên tài liệu).
   - Nội dung thông báo: *"Cuộc họp đã huỷ nên không thay đổi được nữa."*

**Các bước thực hiện:**
- [ ] **Step 1:** Cập nhật bảng Exception Flows trong `docs/01-requirements/05-use-case-specifications.md` tại các mục UC-71b, UC-72a, UC-72b, UC-35, UC-33a, UC-33b, UC-33c, UC-33d.
- [ ] **Step 2:** Cập nhật bảng EX tương ứng trong `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`.
- [ ] **Step 3:** Đồng bộ Report 3 ra Markdown: `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`.
- [ ] **Step 4: Verification:** Chạy test `dotnet test tests/TrustCabinet.Web.Tests --filter MessageCodeCatalogueTests` để kiểm tra tính nhất quán mã lỗi giữa code, catalogue và tài liệu.

---

### Task 2: Cập nhật đặc tả chi tiết UC-34b (Quản trị viên đọc kho tài khoản khoá) theo PR #174

**Phạm vi nghiệp vụ (theo màn hình đã triển khai ở PR #174 và ADR-0035):**
- Bước 1 (Màn hình Quản lý Người dùng): Mỗi dòng tài khoản có nút *"Xem kho tài liệu"*. Nếu tài khoản đang ở trạng thái hoạt động (`Active`), nút vẫn hiển thị nhưng bị vô hiệu hoá (`disabled`) kèm tooltip lý do (tương tự như nút Đặt lại mật khẩu).
- Màn hình Kho tài liệu của tài khoản khoá: Không hỗ trợ công cụ tìm kiếm hoặc lọc như kho của chính mình (chỉ duyệt cây thư mục).
- Xử lý trạng thái tài khoản thay đổi: Nếu tài khoản được kích hoạt lại trong lúc Quản trị viên đang xem -> hệ thống từ chối truy cập với `409 MSG175` và tự động điều hướng quay lại màn hình Quản lý người dùng.
- Thư mục không tồn tại hoặc thuộc cuộc họp bảo mật: Nếu truy cập vào thư mục con đã bị xóa hoặc thuộc cuộc họp mức `Confidential`/`Secret` -> hệ thống tự động quay về gốc kho tài liệu của người dùng đó (không bổ sung mã lỗi mới).

**Các bước thực hiện:**
- [ ] **Step 1:** Cập nhật luồng chính (Main Flow bước 1) và các luồng ngoại lệ/ghi chú của UC-34b trong `docs/01-requirements/05-use-case-specifications.md`.
- [ ] **Step 2:** Cập nhật UC-34b trong `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx`.
- [ ] **Step 3:** Đồng bộ Report 3: `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`.
- [ ] **Step 4: Verification:** So sánh git diff của `05-use-case-specifications.md` và `Report-3_Software_Requirement_Specification.md`.

---

### Task 3: Cập nhật Permission Matrix cho quyền đọc kho tài khoản khoá

**Phạm vi nghiệp vụ:**
- Thêm quyền đọc kho tài liệu của tài khoản đã khoá:
  - Chỉ dành cho vai trò Quản trị viên hệ thống (`SystemAdmin` / capability `AdminUsers`).
  - Phục vụ bàn giao công việc khi nhân sự nghỉ việc hoặc bị khóa tài khoản.
  - Mọi thao tác truy cập danh sách và tải file đều ghi nhận nhật ký kiểm toán (Audit Log, ADR-0035).

**Các bước thực hiện:**
- [ ] **Step 1:** Cập nhật bảng Permission Matrix trong `docs/01-requirements/00-master-use-case-inventory.md` (nếu có bảng tổng hợp quyền).
- [ ] **Step 2:** Cập nhật bảng Permission Matrix trong `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx` (mục II.3 hoặc bảng phân quyền tương ứng).
- [ ] **Step 3:** Chạy `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`.
- [ ] **Step 4: Verification:** Kiểm tra diff bảng phân quyền trong Report 3 Markdown.

---

### Task 4: Chuyển đổi Sequence Diagrams FE-07 từ PROPOSED sang As-Built

**Các điểm điều chỉnh theo code thực tế đã merge:**
1. **Đổi tên và di chuyển (UC-71b, UC-71c, UC-72b, UC-72c):**
   - Đổi tên: Sử dụng `PUT /api/documents/{id}/name` và `PUT /api/folders/{id}/name` (hai endpoint PUT riêng biệt, không dùng `PATCH`).
   - Di chuyển: Sử dụng `PUT /api/documents/{id}/location` và `PUT /api/folders/{id}/location`.
2. **Loại bỏ endpoint `GET /api/folders`:**
   - Cây thư mục được trả về tích hợp trực tiếp trong response của `GET /api/library`.
3. **UC-33b (Gắn tài liệu vào mục nghị sự):**
   - Tách thành 3 endpoint riêng biệt theo 3 nguồn dữ liệu:
     - Chọn từ tài liệu đã có trong cuộc họp: `POST /api/meetings/{id}/agenda/{itemId}/meeting-documents`
     - Chọn từ kho tài liệu cá nhân: `POST /api/meetings/{id}/agenda/{itemId}/documents/library`
     - Tải tệp mới từ máy tính: `POST /api/meetings/{id}/agenda/{itemId}/upload`
   - UC-33b AF-1: Xác thực quyền và trạng thái cuộc họp trước khi đọc stream tệp multipart (giống UC-33a AF-1).
   - Danh sách tài liệu chọn rỗng -> trả `400 MSG170`.
4. **Mở tệp trong kho:**
   - Bổ sung bước gọi `GET /api/documents/{id}/content` để đọc nội dung tệp lưu trữ trong kho cá nhân.
5. **Realtime event:**
   - Bổ sung sự kiện SignalR `documentsChanged` được phát tới nhóm cuộc họp sau khi danh sách tài liệu thay đổi.
6. **API Chương trình nghị sự hiện hữu:**
   - Ghi chú rõ `PUT /api/meetings/{id}/agenda/{itemId}` vẫn hỗ trợ nhận `documentIds` (chức năng tiện ích cho Thư ký).

**Các bước thực hiện:**
- [ ] **Step 1:** Rà soát và cập nhật nội dung các file PlantUML `.txt` trong `docs/06-sequencediagram/document-management/` (`UC71b_RenameLibraryDocument.txt`, `UC71c_MoveLibraryDocument.txt`, `UC72b_RenameFolder.txt`, `UC72c_MoveFolder.txt`, `UC33b_AttachDocumentsToAgendaItem.txt`, `UC34_ViewPersonalLibrary.txt`, v.v.).
- [ ] **Step 2:** Cập nhật các tab tương ứng trong file Draw.io gộp `docs/06-sequencediagram/TrustCabinet_Sequence_Diagrams.drawio`.
- [ ] **Step 3:** Kiểm tra và xuất ảnh với tỷ lệ scale thích hợp cho 4 trang lớn theo `docs/06-sequencediagram/drawio-png/README.md`.
- [ ] **Step 4: Verification:** Kiểm tra tính toàn vẹn của các tab trong `TrustCabinet_Sequence_Diagrams.drawio` và các ảnh PNG.

---

### Task 5: Cập nhật Class Diagram FE-07 theo As-Built

**Phạm vi điều chỉnh:**
- Cập nhật sơ đồ lớp `docs/07-classdiagram/feature-07-document-management-class-diagram.drawio` để phản ánh đúng cấu trúc lớp và endpoint backend đã merge:
  - `PersonalLibraryEndpoints`: các endpoint `/api/library`, `/api/folders`, `/api/documents/{id}/name`, `/api/documents/{id}/location`, `/api/documents/{id}/content`.
  - `MeetingDocumentEndpoints` & `MeetingDocumentChangeEndpoints`: các endpoint quản lý tài liệu cuộc họp, gắn/gỡ mục nghị sự.
  - `DocumentCommentEndpoints`: góp ý tài liệu và phản hồi.
  - DTOs, request models và failure results tương ứng.

**Các bước thực hiện:**
- [ ] **Step 1:** Chỉnh sửa file `docs/07-classdiagram/feature-07-document-management-class-diagram.drawio` tuân thủ nguyên tắc [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md).
- [ ] **Step 2:** Xuất lại file ảnh `feature-07-document-management-class-diagram.drawio.png`.
- [ ] **Step 3: Verification:** Kiểm tra sơ đồ lớp bằng hình ảnh và định dạng XML.

---

### Task 6: Chuẩn hoá thuật ngữ "submission" & ghi nhận câu hỏi mở PO

**Phạm vi thực hiện:**
1. **Chuẩn hoá thuật ngữ "submission":**
   - Rà soát các dòng EX trong UC-33a, UC-33b, UC-36: chuyển đổi các câu còn dùng từ "submission" sang cách diễn đạt chuẩn: mỗi tệp tải lên là một lượt xử lý riêng biệt trong hàng đợi (theo ADR-0033).
2. **Ghi nhận câu hỏi mở PO (SCRUM-111, Lộc):**
   - Vấn đề: Quản trị viên khi xem kho tài khoản khoá có thấy tệp và `meetingId` của cuộc họp mức "Thường" mà Quản trị viên không tham gia hay không?
   - Hiện trạng code: Đang lọc ẩn `Confidential` và `Secret`; các cuộc họp mức Thường vẫn hiển thị.
   - Ghi nhận trạng thái as-built này vào note bàn giao và ghi chú rõ đang chờ phản hồi từ PO.
3. **Cập nhật Note 14:**
   - Cập nhật tiến độ của Mục 3.2 trong [note 14](14-ghi-chu-nhan-27-28-10-2026-phan-loai-theo-fe.md) và bảng tổng hợp danh sách việc cần làm.

**Các bước thực hiện:**
- [ ] **Step 1:** Rà soát và chỉnh sửa câu chữ "submission" trong `05-use-case-specifications.md` và Report 3.
- [ ] **Step 2:** Cập nhật trạng thái câu hỏi PO và tiến độ trong `14-ghi-chu-nhan-27-28-10-2026-phan-loai-theo-fe.md`.
- [ ] **Step 3: Verification:** Kiểm tra lại toàn bộ diff của các file tài liệu.

---

## Tiêu chí hoàn thành (Definition of Done)

1. Toàn bộ các mã `MSG347`, `MSG348`, `MSG349` đã có EX đầy đủ trong cả Đặc tả UC và Report 3 (khớp với code và catalogue).
2. UC-34b và Permission Matrix phản ánh chính xác các quyết định thiết kế và UI thực tế của PR #174, ADR-0035.
3. Sequence Diagrams và Class Diagram FE-07 được chuyển sang as-built, không còn các chi tiết giả định của bản PROPOSED cũ.
4. Lệnh kiểm tra danh mục mã thông báo `dotnet test --filter MessageCodeCatalogueTests` chạy thành công (Passed: 65, Failed: 0).
5. File Report 3 Markdown được đồng bộ hoàn toàn khớp với bản DOCX nguồn thông qua script `sync_reports.py`.
6. Mọi thay đổi được giữ nguyên trên nhánh `docs/ba-final-review-fe-07` sẵn sàng cho việc review và tạo Pull Request.
