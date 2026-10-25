# Review PR #136 và Bổ sung BA Final Review (25/10/2026)

**Nguồn:** Feedback review PR #136 và chỉ đạo từ PO / nhóm qua TrustCabinet Bot (sau các PR đã merge: #133 SCRUM-69 Start/End, #137 Thông báo, #134 SCRUM-21 Tài liệu, #135 SCRUM-98 Agenda, #141 SCRUM-25 Agenda FE).  
**Nhánh áp dụng:** `docs/design-diagrams` (PR #136).

---

## I. Review PR #136 (BA Final Review lượt 1–4)

PR #136 nội dung chính đã khớp code: trả lời lời mời một lần + Chủ tọa không trả lời (`MSG154`/`285`/`286`), Thư ký chỉ thay Chủ tọa khi tài khoản Chủ tọa bị khóa (`MSG256`/`259`), tải tài liệu kiểm quyền trước khi kiểm tệp (`MSG164`/`174`/`288`), điểm danh (`MSG150`/`151`/`289`), danh sách mặc định "Đang diễn ra", mã HTTP các endpoint.

Các việc cần xử lý trước khi merge:

### A. Chặn merge

1. **CI đỏ — 3 test fail trong `MessageCodeCatalogueTests`**:
   - **Nguyên nhân**: Commit `4badff9` xoá `docs/assets/diagrams/meeting-management/{SubmitMeetingForApproval,ReviewMeetingApproval,PublishMeeting}.mmd`, nhưng test `Workflow_sequences_use_catalogued_codes_with_the_documented_status` đọc đúng 3 file này để kiểm mã HTTP + mã MSG.
   - **Cách sửa**: 
     - Khôi phục 3 file `.mmd` sang thư mục mới `docs/06-sequencediagram/meeting-management/`.
     - Cập nhật đường dẫn ở `tests/TrustCabinet.Web.Tests/MessageCodeCatalogueTests.cs:239` trỏ tới `docs/06-sequencediagram/meeting-management/`.
     - Chạy lại test `dotnet test --filter MessageCodeCatalogueTests`.

2. **Conflict 5 file với `main`**:
   - Đã xử lý xong khi merge `origin/main` vào `docs/design-diagrams`:
     - Danh mục mã: `MSG271`–`MSG274` đã triển khai (`SCRUM-98`), `MSG156`–`MSG160` đã triển khai (`SCRUM-21`), `MSG250` ngừng dùng.
     - 2 file skill trong `.agents/skills/` lấy theo `origin/main` (PR #145).

3. **Bộ test report của Trường Huy**:
   - Hai commit `6930076` (*doc test*) và `f33053e` (*Thêm bộ test report theo mẫu*) nằm chung trên PR này; khuyến nghị tách ra PR riêng cho test report nếu nhóm yêu cầu.

### B. Tài liệu ghi khác code hoặc tự mâu thuẫn cần sửa

1. **Dòng `MSG174` trong danh mục mã thông báo** (`docs/03-api/00-danh-muc-ma-thong-bao.md` ~dòng 500):
   - Đang ghi: `UC-33b/EX-7, UC-33c/EX-5, UC-33d/EX-7 "Đã triển khai SCRUM-107"` là sai vì gắn/gỡ/xoá tài liệu chưa có endpoint (`MSG167/169/170/171` chưa có backend).
   - **Sửa thành câu chuẩn**: `"UC-33a / EX-7 và UC-77 / EX-4 · Đã triển khai; UC-33b / EX-7, UC-33c / EX-5, UC-33d / EX-7 còn dành sẵn"`.

2. **`BR-115` (`docs/01-requirements/06-business-rules.md` ~dòng 112)**:
   - Đang ghi: "The Chair's response stays Pending" là sai vì đổi vai không reset câu trả lời, thành viên đã trả lời trước đó khi nhận vai Chủ tọa vẫn giữ câu trả lời cũ (API vẫn trả, chỉ FE ẩn đi).
   - **Sửa thành**: "Câu trả lời của Chủ tọa không được hiển thị" thay cho "luôn là Pending". Sửa câu tương tự ở mục I.3.2 Report 3 (`Participant.InvitationResponse`).

3. **`UC-28b` (`docs/01-requirements/05-use-case-specifications.md` ~dòng 775)**:
   - Đang ghi: "The Chair and the Secretary do not swap roles" không có điều kiện. Thực tế chỉ cấm sau khi công bố (`Published`/`InProgress`). Trước khi công bố vẫn chọn Thư ký làm Chủ tọa được.
   - **Sửa thành**: Thêm `"after publication"`.

4. **`UC-73` (`docs/01-requirements/05-use-case-specifications.md` ~dòng 854)**:
   - `PRE-3` bắt buộc "có ít nhất một người nhận hợp lệ", nhưng `AF-1` lại là nhánh "không có ai để giao" (`MSG258`).
   - **Sửa thành**: Bỏ `PRE-3` hoặc chuyển điều kiện vào `AF-1`.

5. **Sơ đồ tuần tự còn số EX cũ**:
   - `docs/06-sequencediagram/meeting-participant-management/UC31_RecordAttendance.txt:53`: "Secretary is no longer authorized - EX-2" → sửa thành `EX-3`.
   - `UC31_RecordAttendance.txt:78`: "Any attendance save fails - EX-3" → sửa thành `EX-5` (`MSG150`).
   - `docs/06-sequencediagram/meeting-participant-management/UC29_RespondToMeetingInvitation.txt:54`: "Save fails - EX-2" → sửa thành `EX-3`.
   - Xuất lại file PNG và cập nhật tab drawio tương ứng.

6. **Ghi nhận rà soát thêm**:
   - `UC33a_UploadDocumentsToMeeting.txt`: code kiểm body hỏng (`MSG164`) trước tổng dung lượng, chỉ kiểm `Content-Length` (`MSG288`) ở đầu. Sau lưu tệp có kiểm quyền lần 2 trong transaction.
   - `UC30_ViewParticipantResponseStatus.txt`: Service trả response thô, FE tự gắn nhãn và ẩn Chủ tọa.
   - `UC-28a/b/c`: Code cho phép cả Chủ tọa và Thư ký quản lý thành phần trước khi công bố (đặc tả ghi Thư ký).

---

## II. Bổ sung từ các PR đã merge (#133, #134, #135, #137, #141)

### 1. Quy tắc thông báo chung: Người thao tác không nhận thông báo về chính mình
- **Nguyên tắc**: Không ai nhận thông báo (in-app notification) về thao tác do chính mình thực hiện. Email vẫn gửi đầy đủ cho mọi người để cập nhật lịch.
  - `BR-164` (Bắt đầu họp): Thông báo cho người được mời, trừ Chủ tọa vừa bấm Bắt đầu.
  - `BR-76` (Dời lịch cuộc họp đã công bố): Thông báo cho người được mời, trừ người thực hiện dời lịch; email vẫn gửi tất cả.
  - `BR-92` (Đổi vai trò, thay Chủ tọa): Người tự thao tác không nhận thông báo; ví dụ Chủ tọa tự giao lại thì chỉ người nhận vai trò mới nhận thông báo. (Thư ký thay Chủ tọa vẫn báo cả 2).
  - `BR-307` (Giao người trình bày Agenda): Thư ký tự gán mình làm người trình bày thì không nhận thông báo.
- **Đề xuất**: Thêm một Business Rule chung (ví dụ `BR-332` hoặc tương đương) quy định nguyên tắc này thay vì sửa lẻ tẻ từng BR.
- **File cần cập nhật**: `docs/01-requirements/06-business-rules.md` (`BR-76`, `BR-92`, `BR-164`), `05-use-case-specifications.md` (`UC-15` AF-1, `UC-21` POST-3, `UC-28b`).

### 2. SCRUM-21 (Tài liệu cuộc họp) — Rủi ro đã biết của BR-188
- **Quy tắc**: Cuộc họp Mật (`Secret`) chỉ chặn nút "Tải về". Thao tác "Xem tài liệu" vẫn phải tải tệp gốc về trình duyệt để hiển thị, nên người dùng rành kỹ thuật vẫn có thể lưu tệp từ cache/network. Mỗi lượt xem đều có audit log.
- **Cập nhật**: Ghi nhận rủi ro kỹ thuật đã biết này vào `UC-32c` (Xem tài liệu) và `BR-188`.

### 3. SCRUM-98 (Chương trình nghị sự — PO chốt 25/10/2026)
- **Hoàn thành mục (`BR-72`)**: Phải bắt đầu mục (`InProgress`) rồi mới bấm kết thúc (`Done`) được. Chỉ được xoá mục chưa bắt đầu (`Pending`); mục đã bắt đầu hoặc đã xong không được xoá.
- **Sắp xếp mục (`BR-238`)**: Cho phép đổi chỗ cả các mục đã `Done`.
- **Người trình bày là Khách**: Khách dù đã từ chối lời mời (`Declined`) vẫn được chọn làm người trình bày và vẫn vào họp được (từ chối chỉ ẩn lịch cá nhân, không thu hồi quyền).
- **Thông báo (`BR-307`)**: Thư ký tự gán mình làm người trình bày thì không nhận thông báo. Email thông báo cho Khách phải có tên cuộc họp và thời gian bắt đầu.
- **File cần cập nhật**: `06-business-rules.md` (`BR-72`, `BR-307`), `05-use-case-specifications.md` (`UC-23a/b`, `UC-24`, `UC-77`).

### 4. SCRUM-25 (FE Chương trình nghị sự) — Lệch note màn hình 18/10
- Hộp thoại thêm/sửa mục (màn 5.2): Chưa có menu "+ Thêm tài liệu" 3 lựa chọn; hiện chỉ cho chọn trong tài liệu đã có của cuộc họp (tải lên ở tab Tài liệu - SCRUM-107; kho cá nhân chờ SCRUM-58).
- Panel trong phòng họp: Do bề ngang hẹp (360px) nên không có kéo thả sắp xếp mục; các thao tác khác giữ nguyên.
- **File liên quan**: Cập nhật mô tả màn hình Report 3 §III 5.1–5.3.
