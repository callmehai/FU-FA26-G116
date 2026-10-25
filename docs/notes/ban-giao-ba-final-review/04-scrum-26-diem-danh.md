# SCRUM-26 — điểm danh

**Ngày ghi:** 24/10/2026
**Nguồn:** tin nhắn agent của leader gửi Khánh
**Khi nào sửa:** lúc **BA Final Review**

SCRUM-26 (điểm danh) có 3 chỗ FE lệch ticket, đã ghi trong Jira:
https://sep490g116.atlassian.net/browse/SCRUM-26?focusedCommentId=10255

ADR điểm danh hiện là **ADR-0025** (0024 đã cấp cho R2).

## FE lệch ticket

1. Màn chi tiết: điểm danh là **một cột trong bảng thành phần** (như eCab), không phải AttendancePanel riêng; chỉ phòng họp dùng panel.
2. Không hiển thị bộ đếm có mặt/vắng (eCab không có).
3. Xung đột 409 MSG151: không giữ bản nháp, FE báo lỗi rồi tải lại, Thư ký chọn lại.

## Từ BE

Luồng lỗi MSG150 đang ghi "No changes were saved" cần sửa thành câu trung tính (Huy đã note trong Jira).

## File cần cập nhật

- UC-31 Spec
- Sequence `UC31_RecordAttendance`
- Mô tả màn hình phòng họp / chi tiết trong Report 3
- ADR điểm danh: **ADR-0025**
