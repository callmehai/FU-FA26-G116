# Bàn giao cập nhật màn hình theo Report 3 ngày 18/10/2026

**Ngày:** 18/10/2026. **Người cập nhật UC Spec:** KhanhPG. **Người phụ trách sửa màn hình:** LocHD (Hoàng Đức Lộc).
**Ticket:** [SCRUM-100](https://sep490g116.atlassian.net/browse/SCRUM-100).

Đợt 18/10/2026 chỉ sửa UC list, UC Specification, Business Rules và System Messages trong Report 3 DOCX local, rồi sinh lại
[Markdown](../08-reports/Report-3_Software_Requirement_Specification.md) bằng `scripts/sync_reports.py`. Phần màn hình
(Report 3 §III, wireframe v5, ảnh `docs/wireframe/screens/`) **giữ nguyên**, người phụ trách màn hình cập nhật sau.
Khi màn hình cũ và UC Spec mới khác nhau thì theo UC Spec.

Nguồn quyết định: [trao đổi PO 16/10/2026](../04-meetings/2026-10-16-trao-doi-po.md) và
[góp ý hội đồng lần 1](NOTE-gop-y-hoi-dong-lan-1.md).

## Màn cần sửa

| Màn (wireframe · Report 3 §III) | UC / BR | Cần sửa |
|---|---|---|
| **5.2** Thêm / sửa mục nghị sự · §III 5.2 | UC-23a, UC-23b, UC-33b, UC-33d; BR-66, BR-68, BR-304 | Bỏ ô "Thời lượng (phút)" và dòng giờ dự kiến tự tính. Sửa được Nội dung, Người trình bày, Ghi chú ở mọi trạng thái của mục. "Tài liệu của mục" đổi thành nút **+ Thêm tài liệu** mở menu 3 lựa chọn: *Tải tệp lên từ máy* · *Chọn từ tài liệu cuộc họp* · *Chọn từ Tài liệu của tôi*. Mỗi tài liệu trong danh sách có nút gỡ (UC-33d AF-1). Bỏ checkbox "Mục này cần biểu quyết", vì vote không gắn mục nghị sự (BR-285) |
| **5.1** Tab Nội dung cuộc họp · §III 5.1 | UC-23d, UC-26; BR-238 | Bỏ cột/nhãn thời lượng. Kéo thả sắp xếp được mọi mục, cả mục InProgress và Done |
| **5.1 / 5.3** Kết luận của mục · §III 5.1, 5.3 | UC-77, UC-26; BR-309 → BR-311 | Mục InProgress / Done có nút **Ghi kết luận** cho Chủ trì và Thư ký (ô nhập tối đa 2.000 ký tự, MSG271 → MSG274). Danh sách nghị sự hiện kết luận kèm người lưu và thời điểm. Sau khi sinh nháp biên bản thì chỉ đọc |
| **5.2** Người trình bày · §III 5.2 | UC-23a, UC-23b, UC-28c; BR-306 → BR-308 | Dropdown "Người trình bày" chỉ liệt kê thành phần tham dự và Guest hiện tại; sửa được ở mọi trạng thái của mục. Ghi chú: người được giao nhận thông báo khi cuộc họp đã công bố; bị gỡ khỏi cuộc họp thì mục về "Chưa có người trình bày" |
| **5.3** Điều hành cuộc họp · §III 5.3 | UC-24, UC-25; BR-70, BR-72 | Nút "Bắt đầu" có ở **mọi** mục Pending, không chỉ mục đầu tiên. Bỏ so sánh với thời lượng dự kiến. Bỏ MSG141 và MSG144 khỏi ghi chú |
| **11.1** Tab Biểu quyết · §III 11.1 | UC-52a; BR-253 | Thư ký cũng thấy nháp, có nút Tạo / Sửa / Xoá nháp |
| **11.2** Tạo biểu quyết · §III 11.2 | UC-53; BR-160 | Form chỉ có nội dung, phương án, hình thức, vai trò (không chọn tài liệu trong form). Khi người mở form là Thư ký: chỉ có "Lưu nháp", không có nút "Mở biểu quyết" |
| **7.3** Tab Tài liệu (nhóm "Biểu quyết: …") · §III 7.3 | UC-76a, UC-76b; BR-287, BR-288, BR-304, BR-305 | Mỗi vote Nháp có nút ghim mở menu 3 lựa chọn (*Tải tệp lên từ máy* · *Chọn từ tài liệu cuộc họp* · *Chọn từ Tài liệu của tôi*) và nút gỡ từng tài liệu. Vote đã mở thì khoá, không thêm/gỡ được (MSG213) |
| **11.2b** Xác nhận mở biểu quyết | UC-54; BR-288 | Hiện danh sách tài liệu của vote (chốt khi mở) |
| **11.3 / 11.3b** Bỏ phiếu · §III 11.3 | UC-55 (PRE-5, EX-8), UC-74; BR-289, BR-292 | Danh sách tài liệu của vote, mỗi tài liệu có trạng thái *Đã đọc / Chưa đọc*. Nút "Gửi phiếu" bị mờ khi còn tài liệu chưa đọc. Thông báo MSG252 |
| **7.5** Xem tài liệu · §III 7.5 | UC-32c, UC-74; BR-290, BR-291 | Khi người dùng cuộn tới trang cuối của một tài liệu thuộc vote, hiện checkbox **"Tôi đã đọc tài liệu này"**. Chưa tới trang cuối thì checkbox mờ (MSG254). Đã xác nhận thì hiện "Đã đọc lúc hh:mm" |
| **6.1** Tab Thành phần tham dự · §III 6.1 | UC-73, UC-28b; BR-126, BR-294 → BR-296 | Chair có nút **Giao chủ toạ** mở dialog chọn người nhận (không có Guest và Thư ký). Từ Published, Thư ký không còn lựa chọn đổi Chair (MSG256) |
| **8.3** Panel người trong phòng · §III 8.3 | UC-73 | Nên có thêm lối vào "Giao chủ toạ" cho Chair, để Chair giao xong rồi rời phòng |
| **4.5** Chi tiết cuộc họp · §III 4.5 | UC-16, UC-75; BR-297 → BR-300 | Organizer có nút **Huỷ cuộc họp** khi Draft → Published, mở dialog nhập lý do bắt buộc (MSG262). Cuộc họp đã huỷ: badge *Đã huỷ*, hiện lý do, người huỷ, thời điểm; mọi nút sửa bị ẩn |
| **4.3** Danh sách cuộc họp · §III 4.3, 4.1, 4.2 | UC-17; BR-297 | Bộ lọc trạng thái có *Đã huỷ*; `StatusBadge` thêm trạng thái Cancelled |

Màn cũ dùng chữ "Thời lượng" hoặc gắn biểu quyết vào mục nghị sự thì rà lại hết, không chỉ các màn trong bảng.

## Cách làm

1. Ra wireframe v6 theo [docs/wireframe/README.md](../wireframe/README.md): sửa HTML → extract → `mk_plugin.js 5 6` →
   chạy thử plugin → chụp lại ảnh các màn đã đổi. Không sửa file v5.
2. Sửa Report 3 §III (mô tả màn, bảng trường) và thay ảnh tương ứng trong DOCX local, rồi sync Markdown bằng script.
3. Mã thông báo lấy từ [catalogue](../03-api/00-danh-muc-ma-thong-bao.md); MSG252 → MSG264 là mã mới của đợt này.
   MSG141, MSG144, MSG250 đã ngừng dùng trong đặc tả.
