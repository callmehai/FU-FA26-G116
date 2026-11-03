# PR #212 — Màn Khách, trang cuộc họp và tài liệu của Khách (SCRUM-121, 122, 123): việc BA Final Review

**Người ghi:** KhanhPG (BA), 02/11/2026. **Nguồn:** [PR #212](https://github.com/callmehai/FU-FA26-G116/pull/212) mục
"Deviations from the docs"; comment Jira của Lộc ngày 01/11/2026:
[SCRUM-121](https://sep490g116.atlassian.net/browse/SCRUM-121?focusedCommentId=10389) (6 điểm lệch),
[SCRUM-123](https://sep490g116.atlassian.net/browse/SCRUM-123?focusedCommentId=10390) (mức độ mật),
[SCRUM-122](https://sep490g116.atlassian.net/browse/SCRUM-122?focusedCommentId=10391) (E2E, 403 thay vì 404).
Contract: [16-truy-cap-khach-uc04.md](../../03-api/16-truy-cap-khach-uc04.md) §8, kết quả E2E
[16-truy-cap-khach-uc04-ket-qua-kiem-thu.md](../../03-api/16-truy-cap-khach-uc04-ket-qua-kiem-thu.md). Note này gom việc BA, không thay
Jira hay đặc tả. Phần UC-04/BR/MSG của BE nằm ở [note 21](21-scrum-116-truy-cap-khach-uc04.md).

> **Tiến độ 02/11/2026: CHƯA LÀM GÌ.** Mới ghi note.

## 1. Trạng thái

- PR #212 **đã merge vào `main`** (01/11/2026), cùng stack với #213 và #215 ([note 24](24-pr213-215-scrum-59-127-khach-phong-hop.md)).
- Không thêm endpoint, không thêm mã MSG. FE dùng MSG126, MSG371–MSG379 đã có.
- Màn 1.3 giờ đã có trên UI: đặc tả màn hình còn trống ở note 21 (§3, ô "CHƯA LÀM") **làm được rồi**, chụp từ `/guest/{id}`.

## 2. Đã làm (tóm tắt để viết đặc tả màn hình)

- Route công khai `/guest/{invitationId}#token=…`, ngoài phiên nội bộ và sidebar (BR-156). Token đọc từ fragment rồi xoá khỏi thanh địa chỉ.
- **Màn 1.3:** tên cuộc họp, email bị che, 6 ô mã (chỉ số, tự nhảy ô, dán cả mã), nút Xác minh, "Gửi lại mã" đếm ngược theo giây BE trả
  (MSG373, MSG379), "Mã có hiệu lực trong 15 phút". Sai mã MSG126; sai 5 lần MSG372 khoá nút Xác minh; EX-1 MSG371; EX-3 MSG374.
  Có thông báo ghi âm và phạm vi Khách.
- **Form hồ sơ** (UC-04 bước 5): tên hiển thị bắt buộc (mặc định tên được mời), chức vụ; MSG377 theo từng ô.
- **Trang cuộc họp của Khách:** thanh trên (logo, tên + chức vụ tự khai, "Khách", nút Thoát); tên, trạng thái, giờ; câu hỏi tham dự
  (UC-29); tab Nội dung, Tài liệu, Biểu quyết, Thành phần tham dự. Dòng của chính Khách trong danh sách thành phần có "(Tôi)".
- **Tài liệu (SCRUM-123):** mọi tài liệu của cuộc họp, xem và tải về; trình xem ở `/guest/{id}/documents/{docId}`; không có Góp ý (từ PR #224, 02/11/2026, Khách góp ý và trả lời được — [note 27](27-pr224-tai-lieu-sau-e2e.md));
  biểu quyết có tài liệu đính kèm: đọc tới cuối → "Xác nhận đã đọc" (UC-74) → bỏ phiếu.
- **ALT-1:** còn phiên thì mở lại link vào thẳng, không gửi mã mới. Token Khách lưu riêng trong `localStorage` theo lời mời.
- Kiểm lại phiên mỗi 20 giây, khi focus và sau mọi 404. Hết phiên: gỡ dữ liệu cuộc họp khỏi màn hình. Lời mời còn hiệu lực → về màn
  nhập mã kèm MSG378; không còn → "Cuộc họp đã kết thúc hoặc lời mời đã bị thu hồi".

## 3. Điểm khác tài liệu, và chỗ phải sửa

| # | Khác gì | Nguồn | Chỗ BA sửa |
|---|---|---|---|
| 1 | Màn 6.4 (phản hồi tham dự) **không tách riêng cho Khách**: câu hỏi nằm đầu trang cuộc họp của Khách, giống trang chi tiết 4.5b của người nội bộ | 121 #1 | UC-29: điểm vào của Khách; Report 3 §III màn 6.4 ghi Khách trả lời trên trang cuộc họp |
| 2 | Trang Khách **không có chip mức độ mật, hình thức, địa điểm**: `GuestMeetingView` chỉ có id, tên, trạng thái, giờ. Cuộc họp Mật vẫn hiện nút Tải về, bấm thì BE trả 403/MSG159 | 121 #2, 123 #1 | **Đề xuất BE** thêm `sensitivity` (và mode/location nếu cần) vào view Khách — chưa có ticket, hỏi Hải/Lộc. Trong lúc chờ: UC-32c ghi Khách thấy nút Tải về nhưng bị từ chối MSG159 với cuộc họp Mật (BR-188 vẫn đúng) |
| 3 | Màn kết thúc ghi **"Cuộc họp đã kết thúc hoặc lời mời đã bị thu hồi"** + nội dung MSG371, vì BE cố ý không nói lý do (EX-1). Trình duyệt chưa từng có phiên mở link hết hiệu lực thì thấy "Không mở được lời mời". Nội dung/mã chính thức **vẫn chờ chốt** | 121 #3 | UC-04 EX-1: ghi hai màn này. Hỏi PO có cần mã riêng không (FE #215 xác nhận không cần MSG381) |
| 4 | Dòng "TrustCabinet mời bạn họp" chữ thường cỡ nhỏ, **không viết hoa** như mô tả màn 1.3 (tránh kiểu eyebrow) | 121 #4 | Đặc tả màn 1.3: sửa mô tả |
| 5 | Có nút **Thoát** cho Khách (`DELETE /api/guest-access/session`, chỉ kết thúc phiên đó); tài liệu chưa nhắc | 121 #5 | UC-04: thêm AF/bước Khách tự kết thúc phiên (khớp điểm 6 ở note 21: đăng xuất chỉ kết thúc phiên đó) |
| 6 | **`GET /api/meetings/{id}` trả 403 cho Khách**, không phải 404 như mục 8 ticket SCRUM-122. Endpoint chỉ cho người nội bộ; Khách nhận 403 với mọi id, mọi trạng thái, nên không lộ cuộc họp nào tồn tại | 122 | Sửa kỳ vọng trong ticket SCRUM-122 và test case của Tester. Không đổi code. Khớp bảng 403/404 trong `AGENTS.md` (giấu sự tồn tại của dòng dữ liệu, 403 ở đây giống nhau cho mọi id) |
| 7 | Wireframe chưa cập nhật trong PR | 121 #6 | Làm cùng đặc tả màn hình ([plan 06](06-plan-dac-ta-man-hinh.md)) |

Ghi nhận thêm (không phải lệch docs): gửi OTP chạy đồng bộ qua SMTP, local mất 5–8 giây.

## 4. Việc BA, theo thứ tự

- [ ] UC-04: AF Khách tự Thoát (điểm 5); EX-1 hai màn kết thúc (điểm 3).
- [ ] UC-29: Khách trả lời ngay trên trang cuộc họp (điểm 1).
- [ ] UC-32a/UC-32c: Khách xem mọi tài liệu và tải về (PO 30/10/2026, comment SCRUM-123 10349); góp ý được từ PR #224 (UC-35 đã sửa, [note 27](27-pr224-tai-lieu-sau-e2e.md)); cuộc họp Mật bị MSG159
      (điểm 2). Kiểm tra trùng việc với note 21 §3 dòng UC-32a/32c đã làm.
- [ ] Hỏi Hải/Lộc: thêm `sensitivity` vào `GuestMeetingView` (điểm 2); mã/nội dung màn kết thúc (điểm 3).
- [ ] Sửa kỳ vọng SCRUM-122 mục 8: 403 (điểm 6), báo Tester.
- [ ] **Đặc tả màn hình** 1.3, form hồ sơ, trang cuộc họp Khách, trình xem tài liệu Khách, màn kết thúc: chụp từ UI thật, cập nhật
      wireframe (điểm 4, 7). Đánh dấu xong ô "CHƯA LÀM" ở note 21 khi làm.
- [ ] Sync Report 3, Change Log.
