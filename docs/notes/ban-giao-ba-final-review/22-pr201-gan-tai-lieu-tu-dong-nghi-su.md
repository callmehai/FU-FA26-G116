# PR #201 — Gắn tài liệu từ dòng mục nghị sự, trang đích sau đăng nhập: việc BA Final Review

**Người ghi:** KhanhPG (BA), 01/11/2026. **Nguồn:** mô tả và phần docs của
[PR #201](https://github.com/callmehai/FU-FA26-G116/pull/201) (mục "Notes for BA Final Review"). **Không có ticket Jira**: người dùng
yêu cầu trực tiếp. Note này gom việc BA, không thay đặc tả. Code là nguồn sự thật.

> **Tiến độ 01/11/2026: CHƯA LÀM GÌ.** Mới ghi note. Không có mục nào dưới đây đã sửa vào Report 3 hay sơ đồ. **Đặc tả màn hình cũng
> chưa làm** (điểm 2), tìm chữ "CHƯA LÀM" để thấy.

## 1. Trạng thái

- PR #201 đã có code, test (1717 pass) và Playwright. Trong PR, `docs/03-api/08-chuong-trinh-nghi-su.md` và
  `docs/03-api/11-tai-lieu-cuoc-hop.md` đã cập nhật. **Không** có ADR, **không** có mã MSG mới, **không** đổi phạm vi.
- Commit a745089 (ADR-0043 cũ, giữ tài liệu chung sau khi gắn) đã hoàn tác ở f0b5195. Hành vi "Tài liệu chung" khớp AI-eCabinet và khớp
  BR-280 / ADR-0020 hiện có: không đổi code cho phần này.
- Report 3 và sơ đồ sequence còn chưa theo kịp code ở 7 điểm dưới đây.

## 2. Bảy điểm khác tài liệu, và chỗ phải sửa

| # | Khác gì | Chỗ BA sửa |
|---|---|---|
| 1 | Form thêm/sửa mục nghị sự **không còn chọn tài liệu**; FE không gửi `documentIds` khi tạo hay sửa. Tài liệu gắn qua UC-33b (kẹp giấy trên dòng mục hoặc tab Tài liệu). API vẫn nhận `documentIds` | **UC-23a, UC-23b** (UC-23b bước 2 hiện ghi "document associations for editing" và bước 4 "any selected Meeting documents": bỏ chọn tài liệu khỏi form, trỏ sang UC-33b/33d). Sequence UC-23a/UC-23b: `createAgendaItem(..., documentIds)` và luồng sửa còn truyền `documentIds` (xem `docs/06-sequencediagram/README.md`); không sai nhưng không còn là đường đi của UI |
| 2 | **Điểm vào mới của UC-33b:** mỗi dòng mục (bản trang) có nút kẹp giấy, menu 3 nguồn: *Tải tệp lên từ máy* (cũng lưu vào kho người tải), *Chọn từ tài liệu cuộc họp* (mọi tài liệu của cuộc họp chưa gắn vào mục đó), *Chọn từ kho của tôi* | **UC-33b** Normal Flow/điểm vào. **CHƯA LÀM — đặc tả màn hình:** màn tab Nghị sự cần nút kẹp giấy và menu; làm qua wireframe ([plan 06](06-plan-dac-ta-man-hinh.md)). Chưa làm gì |
| 3 | `PUT /agenda/{itemId}`: thiếu hoặc null `documentIds` thì **giữ** liên kết; chỉ `[]` mới xoá (trước đây thiếu là xoá) | Không có UC riêng cho API. Ghi vào UC-23b: sửa mục không đụng liên kết tài liệu trừ qua UC-33b/33d. Kiểm tra UC-33d còn khớp |
| 4 | Cờ mới `actions.canLinkDocuments` trên mỗi mục nghị sự: true với Chủ toạ hoặc Thư ký hiện tại, đến khi cuộc họp `Ended` hoặc `Cancelled`. **Chủ toạ gắn được tài liệu từ tab Nghị sự dù không sửa được mục** | **UC-33b** Actor/PRE (Chủ toạ và Thư ký); đối chiếu BR gắn tài liệu (BR-277, BR-278, BR-279 trong UC-23b) xem có câu nào giới hạn Thư ký |
| 5 | **UC-01 / UC-02:** POST-3, POST-4 ghi "redirected to the appropriate authenticated page". Code: đăng nhập xong vào **lịch** (`/calendar`, "Lịch làm việc"), trước đây là danh sách cuộc họp. Ngoại lệ: mở link sâu khi chưa đăng nhập (ví dụ link cuộc họp trong thư mời) thì quay lại trang đó; tài khoản mới không mở được trang đó thì vào lịch (SCRUM-66); phiên hết hạn thì về trang đang xem; **đăng xuất chủ động thì lần đăng nhập sau luôn vào lịch**. `/` cũng mở lịch | **UC-01, UC-02** POST: gọi tên lịch. Có thể thêm AF cho link sâu. Kiểm tra UC-03 (đăng nhập Google) có cùng POST không |
| 6 | **Tài liệu chung (BR-280):** không đổi, khớp AI-eCabinet. Một tài liệu là "chung" chỉ khi **chưa gắn vào mục nghị sự nào**. Gắn vào mục thì chuyển khỏi nhóm chung; gỡ liên kết cuối thì quay lại. Tải thẳng vào mục chỉ nằm dưới mục đó. Người review từng ngạc nhiên | **BR-280** và **UC-33b**: nói rõ quy tắc này |
| 7 | Khác AI-eCabinet, **giữ có chủ ý**: (a) gắn trùng hoặc gỡ cái chưa gắn: AI-eCabinet trả 204 và bỏ qua, ta trả `409 MSG243` và `404 MSG246`, UI hiện thông báo dữ liệu cũ; (b) tài liệu chỉ gắn vào biểu quyết: AI-eCabinet cũng không tính là chung, ta chưa có liên kết biểu quyết | (a) UC-33b/33d EX: kiểm tra đã có MSG243/MSG246. (b) Chờ SCRUM-96 (VoteDocument): lúc đó BR-280 sửa thành "chưa gắn vào mục nghị sự nào và chưa gắn vào biểu quyết nào". Chưa sửa bây giờ |

## 3. Việc BA, theo thứ tự

- [ ] UC-23a, UC-23b: bỏ chọn tài liệu khỏi form (điểm 1), thêm câu giữ liên kết khi sửa (điểm 3).
- [ ] UC-33b: điểm vào kẹp giấy và 3 nguồn (điểm 2), Chủ toạ được gắn (điểm 4), quy tắc tài liệu chung (điểm 6).
- [ ] BR-280: câu "tài liệu chung = chưa gắn vào mục nào" (điểm 6). Phần "và biểu quyết" chờ SCRUM-96.
- [ ] UC-01, UC-02 POST (và UC-03 nếu cùng): trang đích sau đăng nhập là lịch (điểm 5).
- [ ] Sequence UC-23a, UC-23b: bỏ `documentIds` khỏi tham số hoặc chú thích "do UC-33b" (điểm 1).
- [ ] **CHƯA LÀM — ĐẶC TẢ MÀN HÌNH** (điểm 2): màn tab Nghị sự thêm nút kẹp giấy và menu 3 nguồn; hộp thoại "Sửa nội dung" bỏ danh sách tài liệu;
      hộp thoại thêm tài liệu có trạng thái "Đang tải…", lỗi + "Thử lại", "Cuộc họp chưa có tài liệu.". Làm qua wireframe, chưa làm gì.
- [ ] Sync Report 3: `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`.
- [ ] Change Log Report 3.
- [ ] Ghi kết quả vào Jira nếu có ticket; PR này không có ticket nên báo lại trong PR #201 hoặc cho Lộc.

## 4. Hỏi PO / Hải

- **Trang đích sau đăng nhập là lịch** (điểm 5): PO đã chốt chưa, hay chỉ là yêu cầu trực tiếp của người dùng. Nếu chưa chốt thì UC-01/02 ghi
  "NEEDS REVIEW PO".
- **Chủ toạ gắn được tài liệu** (điểm 4): có đúng ý PO không, vì Chủ toạ không sửa được mục.

## 5. Việc không phải của BA

- `editor-options` vẫn trả `documents` dù FE không đọc nữa (giữ để tương thích API).
- `SkillMirrorTests` fail trong PR là do skill cá nhân chỉ có trên máy tác giả, không phải lỗi docs.
