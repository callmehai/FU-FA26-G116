# Thay đổi để dành cho UC chưa đặc tả

> **Ghi ngày 18/10/2026 · KhanhPG.** Nguồn: [trao đổi PO 16/10/2026](../04-meetings/2026-10-16-trao-doi-po.md) và
> [góp ý hội đồng lần 1](NOTE-gop-y-hoi-dong-lan-1.md).
> Report 3 local ngày 18/10/2026 chỉ sửa UC đã có đặc tả, cùng UC list, Business Rules và System Messages.
> Các thay đổi dưới đây chạm vào UC còn là *Placeholder*, nên **chưa đưa vào Report 3**. Người nhận ticket của feature
> làm khi viết đặc tả cho feature đó.

## UC đang là placeholder

| Feature | Thay đổi khi đặc tả |
|---|---|
| FE-08 Phòng họp trực tuyến | Thêm UC **Raise / Lower Hand** (giơ tay / hạ tay xin phát biểu; actor là người đang trong phòng, gồm Guest). UC-41 thêm link sang UC đó; nút "Invite to speak" vẫn là của Chair. UC-37 thêm bước xin đồng ý ghi nhận lời nói. UC-39a: khi người đã đồng ý bật mic, hệ thống tự ghi tiếng nói và gắn với người nói theo track mic. Việc này hệ thống tự làm, **không tách thành UC riêng** |
| FE-09 Upload audio | Thêm UC **Delete Meeting Recording** (xoá audio + transcript theo yêu cầu, vẫn giữ dấu vết audit). UC-44: thời hạn lưu và tự xoá khi tới `RetainUntil` |
| FE-10 Transcript & tóm tắt | UC-45: nguồn transcript có cả bản ghi trực tiếp từ phòng họp online, không chỉ file upload. Họp trực tiếp vẫn upload rồi gán người nói bằng tay |
| FE-12 Biên bản | Gộp UC-60 + UC-61 thành **Edit & Submit Meeting Minutes** (UC-60a Chỉnh, UC-60b Gửi ký); UC-61 ghi không còn dùng. Bước gửi ký có chọn người ký, tối thiểu có Chair. UC-62 nói rõ cơ chế ký: xác thực lại + hash nội dung + thời điểm ký, không phải chứng thư CA. Thêm UC **Issue Meeting Minutes** sau UC-64. UC-59 (sinh nháp biên bản) lấy cả người trình bày và kết luận của từng mục nghị sự (BR-312, UC-77); sau khi sinh nháp, kết luận của mục chỉ còn đọc (BR-310) |

Số UC cho các UC mới chỉ cấp lúc đặc tả, tiếp sau số lớn nhất đang dùng (hiện là UC-77).

## Vùng Report 3 ngoài phạm vi đợt 18/10/2026

Đợt này chỉ sửa UC list, đặc tả UC, BR và System Messages. Các vùng sau còn ghi theo quy tắc cũ, cần sửa ở đợt khác:

- §III 5.2 / 5.3 (màn hình agenda): còn Planned Duration, MSG141, MSG144 và "bắt đầu mục Pending đầu tiên".
- §I.4.4 Permission Matrix: dòng Vote draft (Secretary), thay Chair, huỷ cuộc họp; chú thích Guest còn nhắc "delegated".
- §I.3.2 Entity: Meeting chưa có `Cancelled` và lý do huỷ; AgendaItem còn Planned Duration; chưa có bảng nối Vote ↔ tài liệu và bảng xác nhận đã đọc.
- §I.4.3 Use Case Diagram, §5.3 / §III.16 Job Inventory (job quá hạn BR-301, BR-302), §IV NFR 2.8 / Security.
- Change Log của Report 3.

## Code cần sửa theo

- Agenda (SCRUM-25): bỏ `PlannedDuration` (migration mới), bỏ khoá trường theo trạng thái, bỏ quy tắc mục Pending đầu tiên, sắp xếp ở mọi trạng thái.
- FE-06: chặn Secretary thay Chair từ `Published`; endpoint giao chủ toạ.
- FE-11: Secretary tạo nháp vote; vote gắn tài liệu; xác nhận đã đọc.
- FE-04: trạng thái `Cancelled`, job quá hạn.
