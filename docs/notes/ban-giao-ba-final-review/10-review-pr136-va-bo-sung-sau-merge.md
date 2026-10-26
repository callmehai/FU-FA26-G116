# Review PR #136 và bổ sung cho BA Final Review (lượt 5)

**Ngày ghi:** 25/10/2026  
**Nguồn:** Review PR #136 và tin nhắn bot tổng hợp từ các PR đã merge (#133, #134, #135, #137, #141)  
**Khi nào sửa:** lúc **BA Final Review** (lượt 5)

PR: https://github.com/callmehai/TrustCabinet/pull/136

Phần chính của PR #136 đã khớp code. Dưới đây là các việc cần xử lý trước khi merge PR #136 và các quy tắc mới bổ sung từ PO/team cần đưa vào tài liệu.

## 1. Chặn merge PR #136 (Cần xử lý ngay)

1. **CI đỏ — 3 test fail trong `MessageCodeCatalogueTests`**:
   - Commit `4badff9` xoá `docs/assets/diagrams/meeting-management/{SubmitMeetingForApproval,ReviewMeetingApproval,PublishMeeting}.mmd`, nhưng test `Workflow_sequences_use_catalogued_codes_with_the_documented_status` đọc đúng 3 file này để kiểm mã HTTP + mã MSG.
   - **Cách sửa**: Chuyển 3 file `.mmd` sang `docs/06-sequencediagram/meeting-management/` và đổi đường dẫn ở `tests/TrustCabinet.Web.Tests/MessageCodeCatalogueTests.cs:239` (1 dòng: `"docs", "06-sequencediagram", "meeting-management"`). Chạy `dotnet test --filter MessageCodeCatalogueTests`.
2. **Conflict 5 file với `main`**:
   - Đã merge `origin/main` vào `docs/design-diagrams` sau các PR #133, #134, #135, #137, #145:
     - Danh mục: `MSG156`–`MSG160` (SCRUM-21), `MSG271`–`MSG274` (SCRUM-98) đã triển khai, `MSG250` ngừng dùng → lấy theo bản `main`.
     - 2 file skill trong `.agents/skills/` lấy theo `main`.
3. **Bộ test report của Trường Huy**:
   - Hai commit `6930076` (*doc test*) và `f33053e` (*Thêm bộ test report theo mẫu*) nằm chung PR #136. Cần tách sang PR riêng nếu nhóm yêu cầu.

## 2. Tài liệu ghi khác code hoặc tự mâu thuẫn — cần BA cập nhật

1. **Dòng `MSG174` (bảng tiếng Anh, ~dòng 500)**:
   - Ghi `UC-33b/EX-7, UC-33c/EX-5, UC-33d/EX-7 "Đã triển khai SCRUM-107"` là sai (chưa có endpoint gắn/gỡ/xoá).
   - Sửa thành: `"UC-33a / EX-7 và UC-77 / EX-4 · Đã triển khai; UC-33b / EX-7, UC-33c / EX-5, UC-33d / EX-7 còn dành sẵn"`.
2. **`BR-115` (`06-business-rules.md` ~dòng 112)**:
   - Ghi *"The Chair's response stays Pending"* là sai vì đổi vai không reset câu trả lời, thành viên đã trả lời khi nhận vai Chủ toạ vẫn giữ câu trả lời cũ (API vẫn trả, FE chỉ ẩn đi).
   - Sửa thành: *"Câu trả lời của Chủ tọa không được hiển thị"* thay cho *"luôn là Pending"*. Sửa câu tương tự ở mục I.3.2 Report 3 (`Participant.InvitationResponse`).
3. **`UC-28b` (`05-use-case-specifications.md` ~dòng 775)**:
   - Ghi *"The Chair and the Secretary do not swap roles"* không có điều kiện. Thực tế chỉ cấm sau công bố (`Published`/`InProgress`).
   - Sửa thành: Thêm *"after publication"*.
4. **`UC-73` (`05-use-case-specifications.md` ~dòng 854)**:
   - `PRE-3` bắt buộc *"có ít nhất một người nhận hợp lệ"*, mâu thuẫn với `AF-1` là nhánh *"không có ai để giao"* (`MSG258`).
   - Sửa thành: Bỏ `PRE-3` hoặc chuyển ý đó vào `AF-1`.
5. **Sơ đồ tuần tự còn số EX cũ**:
   - `UC31_RecordAttendance.txt:53`: `EX-2` → sửa thành `EX-3`.
   - `UC31_RecordAttendance.txt:78`: `EX-3` → sửa thành `EX-5` (`MSG150`).
   - `UC29_RespondToMeetingInvitation.txt:54`: `EX-2` → sửa thành `EX-3`.
   - Xuất lại PNG và cập nhật tab drawio tương ứng.
6. **Điểm rà soát thêm**:
   - `UC33a_UploadDocumentsToMeeting.txt`: code kiểm body hỏng (`MSG164`) trước tổng dung lượng (`MSG288`). Sau khi lưu tệp có kiểm quyền lần 2 trong transaction.
   - `UC30_ViewParticipantResponseStatus.txt`: Service trả response thô, FE tự gắn nhãn và ẩn Chủ toạ.
   - `UC-28a/b/c`: Code cho phép cả Chủ toạ và Thư ký quản lý thành phần trước công bố.

## 3. Bổ sung từ các PR đã merge (#133, #134, #135, #137, #141)

1. **Quy tắc mới: Không ai nhận thông báo về thao tác của chính mình**:
   - Áp dụng cho mọi thông báo trong app (in-app notification). Email vẫn gửi cả người thao tác để lịch cập nhật.
   - `BR-164` (Bắt đầu họp): thông báo cho người được mời trừ Chủ toạ vừa bấm Bắt đầu.
   - `BR-76` (Dời lịch cuộc họp đã công bố): thông báo cho người được mời trừ người dời lịch; email dời lịch vẫn gửi mọi người.
   - `BR-92` (Đổi vai trò, thay Chủ toạ): người tự thao tác không nhận thông báo; ví dụ Chủ toạ tự giao lại vai trò thì chỉ người được giao nhận. Thư ký thay Chủ toạ vẫn báo cả hai.
   - `BR-307` (Giao người trình bày): không gửi khi người được giao là chính người thao tác (Thư ký tự gán mình). Email cho khách có tên cuộc họp và giờ bắt đầu.
   - **Đề xuất**: Thêm một BR chung (thay vì sửa lẻ từng BR) để các tính năng sau theo luôn.
   - **File cần cập nhật**: `06-business-rules.md` (`BR-76`, `BR-92`, `BR-164`, `BR-307`), `05-use-case-specifications.md` (`UC-15` AF-1, `UC-21` POST-3, `UC-28b`).
2. **SCRUM-21 (Tài liệu cuộc họp) — Rủi ro BR-188**:
   - Cuộc họp Mật (`Secret`) chỉ chặn nút Tải về, lần xem vẫn đưa tệp gốc tới trình duyệt nên người rành kỹ thuật vẫn lưu được; mỗi lần xem có ghi nhật ký kiểm toán.
   - Ghi vào `UC-32c` như rủi ro đã biết.
3. **SCRUM-98 (Agenda — PO chốt 25/10)**:
   - **Hoàn thành mục**: Phải bắt đầu rồi mới kết thúc được; chỉ xoá được mục chưa bắt đầu (đã bắt đầu hay đã xong thì không xoá) → sửa `BR-72`.
   - **Đổi chỗ mục đã Done**: Cho phép (giữ như đặc tả `BR-238`).
   - **Khách từ chối lời mời**: Vẫn được chọn làm người trình bày và vẫn vào họp được. Từ chối chỉ là trả lời trước + ẩn lịch cá nhân, không chặn quyền.
   - **File cần cập nhật**: `06-business-rules.md` (`BR-72`, `BR-307`), `05-use-case-specifications.md` (`UC-23a/b`, `UC-24`, `UC-77`).
4. **SCRUM-25 (FE Chương trình nghị sự) — Lệch note màn hình 18/10**:
   - Hộp thoại thêm/sửa mục (màn 5.2): Chưa có menu "+ Thêm tài liệu" 3 lựa chọn, chỉ chọn trong tài liệu đã tải lên của cuộc họp. Tải tệp lên ở tab Tài liệu (`SCRUM-107`), "Tài liệu của tôi" chờ `SCRUM-58`.
   - Panel trong phòng họp: Không có kéo thả sắp xếp (bề ngang 360px), các thao tác khác giữ nguyên.
   - **File liên quan**: Mô tả màn §III 5.1–5.3 trong Report 3.

## Nguồn khi sửa

- `docs/01-requirements/05-use-case-specifications.md`
- `docs/01-requirements/06-business-rules.md`
- `docs/03-api/00-danh-muc-ma-thong-bao.md`
- `docs/06-sequencediagram/meeting-participant-management/` (`UC29`, `UC31`)
- `docs/06-sequencediagram/meeting-management/` (`SubmitMeetingForApproval`, `ReviewMeetingApproval`, `PublishMeeting`)
- `tests/TrustCabinet.Web.Tests/MessageCodeCatalogueTests.cs`
- `docs/08-reports/Report-3_Software_Requirement_Specification.md`
