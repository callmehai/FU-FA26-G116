# ADR-0014: Biểu quyết chỉ diễn ra trong cuộc họp, Chủ toạ ghi kết luận

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-10-09
- **Người quyết định:** PO (HaiTV), nhóm G116

## Bối cảnh

Report 1 và Report 3 bản trước mô tả hai kiểu biểu quyết: biểu quyết trực tiếp trong họp và biểu quyết
**time-boxed** trước hoặc sau họp, có **quorum**, kết quả đạt quorum thành một **Resolution bất biến**. Business
Flow cũ có "Shared Voting Sub-flow" dùng chung cho cả ba thời điểm. Entity `VotingSession` có `Kind`
(`Live | Asynchronous`), `OpensAt`, `ClosesAt` để phục vụ việc này.

AI-eCabinet biểu quyết ngay trong cuộc họp: chủ trì tạo, mở, mọi người bỏ phiếu, chủ trì đóng, rồi **ghi kết
luận** Thông qua / Không thông qua / Hoãn. Không có phiếu ngoài giờ họp, không quorum, không đối tượng nghị quyết
riêng. Biểu quyết ngoài giờ họp kéo theo lịch đóng phiếu, nhắc hạn, tính quorum trên điểm danh — nhiều việc mà
không có người dùng nào yêu cầu.

## Quyết định

Chúng tôi chỉ cho biểu quyết **khi cuộc họp `InProgress`**.

- Chair tạo / cấu hình và mở phiếu (UC-53, UC-54); Chair, Secretary, Member đang được phân công bỏ phiếu
  (UC-55), Guest không bỏ phiếu; Chair đóng (UC-56); hệ thống kiểm phiếu; Chair ghi kết luận
  `Passed | NotPassed | Deferred` kèm ghi chú (UC-57). BR-160, BR-161, BR-165.
- Không có biểu quyết trước / sau họp, không quorum, không entity Resolution. Biên bản lấy **kết quả kiểm phiếu và
  kết luận của Chair**.
- Không kết thúc được cuộc họp khi còn phiếu đang mở (BR-159).
- Tên entity `VotingSession` giữ nguyên; bỏ `Kind`, `OpensAt`, `ClosesAt`; thêm `OpenedAt`, `ClosedAt`,
  `ConclusionNote`, `ConcludedById`; `Conclusion` thành enum nullable.
- Feature đổi tên **FE-11 Voting Management**.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ biểu quyết time-boxed + quorum + Resolution | Phủ cả xin ý kiến ngoài giờ họp | Thêm lịch đóng phiếu, nhắc hạn, tính quorum, đối tượng bất biến riêng | Không ai yêu cầu; vượt phạm vi đồ án |
| Chỉ biểu quyết trong họp nhưng hệ thống tự kết luận theo đa số | Ít thao tác | Tự động hoá một quyết định chính trị; không có "Hoãn" | Kết luận là việc của Chủ toạ |
| **Biểu quyết trong họp, Chair ghi kết luận** | Khớp AI-eCabinet và BF-03; mô hình đơn giản | Không xin ý kiến ngoài giờ họp được | **Chọn** |

## Hệ quả

- Migration mới cho `voting_sessions` (bỏ / thêm cột như trên) khi làm FE-11.
- Business Flow BF-02 / BF-04 bỏ nhánh biểu quyết trước / sau họp; BF-03 có nhánh biểu quyết trong vòng agenda.
- `Attendance` không còn dùng để tính quorum; chỉ phục vụ biên bản và báo cáo.
- Phiếu kín vẫn tách `VoteReceipt` khỏi `VoteChoice` như [ERD](../02-design/02-erd.md#biểu-quyết-ẩn-danh--đọc-kỹ-chỗ-này).
