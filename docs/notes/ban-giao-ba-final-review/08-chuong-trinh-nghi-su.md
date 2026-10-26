# Chương trình nghị sự — ghi chú thay đổi cho BA Final Review

**Ngày ghi:** 24/10/2026
**Nguồn:** tin nhắn agent của leader gửi Khánh
**Khi nào sửa:** lúc **BA Final Review**

## 1. Agenda Item (SCRUM-98)

- Bỏ thời lượng dự kiến khỏi Agenda Item.
- Thư ký có thể sửa các trường lập kế hoạch ở mọi trạng thái của item.
- Chỉ item Pending mới được xoá.
- Có thể sắp xếp toàn bộ item, kể cả item Done.
- Chủ toạ có thể bắt đầu một item Pending bất kỳ, nhưng mỗi lúc chỉ có tối đa một item InProgress.
- Hệ thống vẫn ghi thời điểm bắt đầu và kết thúc thực tế, không tính thời lượng.

## 2. Người trình bày

- Chọn một thành viên nội bộ đang tham gia, hoặc một khách mời có lời mời còn hiệu lực.
- Không nhập tên tự do làm phân công hiện hành.
- Khi người nội bộ bị gỡ hoặc lời mời khách bị thu hồi, phân công được xoá.
- Khi phân công trong lúc cuộc họp Published hoặc InProgress, hệ thống gửi thông báo trong ứng dụng cho thành viên nội bộ, hoặc email cho khách.
- BA cần xác nhận đây là quy tắc nghiệp vụ cần đưa vào đặc tả.

## 3. Tài liệu agenda

- Một tài liệu có thể liên kết với nhiều Agenda Item.
- Khi sửa một item, chỉ các liên kết của item đó thay đổi.
- BA cần đối chiếu quy tắc này với ADR-0020 và đặc tả UC-23.

## 4. Kết luận Agenda Item (UC-77)

- Chủ toạ hoặc Thư ký có thể ghi và cập nhật kết luận, tối đa 2.000 ký tự, khi Meeting InProgress hoặc Ended và item InProgress hoặc Done.
- Không cho sửa sau khi biên bản cuộc họp đã tồn tại.
- Hệ thống lưu người cập nhật và thời điểm.
- Các mã MSG271–MSG274 đã được dành cho luồng này trong catalogue.

## 5. Điểm cần chốt

- SCRUM-98 còn ghi câu hỏi PO về việc đổi thứ tự item Done. Code hiện cho phép.
- Cần làm rõ “hoàn thành mục lúc nào cũng được” có cho phép hoàn thành item Pending mà không bắt đầu trước không. Code hiện chỉ hoàn thành item InProgress.

> **Đã chốt 25/10/2026 (PO):** cả hai điểm trên đã có đáp án, xem [11 §3](11-quy-tac-thong-bao-agenda-po-25-10.md#3-scrum-98-agenda-pr-135-po-chốt-2510).
