# ADR-0013: Một cuộc họp là một buổi họp — bỏ Meeting Session

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-10-09
- **Người quyết định:** PO (HaiTV), nhóm G116

## Bối cảnh

Report 3 (30/09 → 08/10/2026) thêm cấp **Meeting Session** dưới cuộc họp: một cuộc họp có nhiều phiên
(sáng / chiều), mỗi phiên có lịch và vòng đời riêng (`Planned → InProgress → Completed | Cancelled`), agenda
thuộc phiên, phòng họp chỉ mở khi có phiên `InProgress`. Code đã có bảng `meeting_sessions` (SCRUM-19,
SCRUM-70) và ADR-0010, ADR-0011 thiết kế quanh khái niệm này.

Khi vẽ lại Business Flow theo hệ thống tham chiếu AI-eCabinet, nhóm thấy họ **không có cấp nào dưới hay trên
cuộc họp**: chữ "phiên họp" trên giao diện của họ thực chất là **mục agenda**. Cấp Meeting Session sinh ra hai
vòng đời lồng nhau, 6 use case và hàng chục business rule, mã thông báo chỉ để xử lý những trường hợp như "giữa
hai phiên", "huỷ phiên cuối cùng" — phức tạp hơn nhu cầu thật của một đồ án và của tổ chức dùng hệ thống.

## Quyết định

Chúng tôi bỏ Meeting Session. **Một cuộc họp là một buổi họp.**

- `Meeting` tự mang lịch: `PlannedStartTime`, `PlannedEndTime` (bắt buộc, giờ kết thúc sau giờ bắt đầu, giờ
  bắt đầu ở tương lai khi tạo), `ActualStartedAt`, `ActualEndedAt` (hệ thống ghi khi Start / End), cùng
  `Mode`, `Location`, `OnlineLink` (tuỳ chọn, tối đa 1.000 ký tự).
- Vòng đời duy nhất: `Draft → WaitingApproval → Approved | Rejected → Published → InProgress → Ended`.
  Bỏ `MinutesIssued`, `Archived` khỏi trạng thái cuộc họp; trạng thái biên bản nằm ở `MeetingMinutes.Status`.
- Sau `Published`, Organizer chỉ được **dời lịch** (giờ, địa điểm, link) và hệ thống báo lại người được mời.
- `AgendaItem.MeetingId` trỏ thẳng vào cuộc họp. Phòng họp trực tuyến mở theo trạng thái cuộc họp
  (`InProgress`, hình thức Online / Hybrid).
- Danh mục UC được gom và đánh số lại (UC-01 → UC-70); 6 UC Meeting Session không có UC kế thừa ngoài phần lịch
  đã nhập vào UC-14 / UC-15.

Không đổi: `VotingSession` (một lượt biểu quyết), `CallSession` (một lượt mở phòng LiveKit), guest session,
phiên đăng nhập.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ Meeting Session như Report 3 cũ | Mô tả được cuộc họp kéo dài nhiều buổi | Hai vòng đời lồng nhau, nhiều UC/BR/MSG, phòng họp phải biết phiên | Phức tạp vượt nhu cầu; AI-eCabinet không có |
| Giữ bảng `meeting_sessions` nhưng ẩn khỏi nghiệp vụ (luôn một phiên) | Không phải migration ngay | Mô hình nói một đằng, dữ liệu một nẻo; mọi truy vấn lịch đi vòng | Nợ kỹ thuật không cần thiết |
| **Một cuộc họp = một buổi họp** | Một vòng đời, lịch nằm trên cuộc họp, khớp AI-eCabinet | Cuộc họp nhiều buổi phải tạo nhiều cuộc họp | **Chọn** |

## Hệ quả

- Code phải có **migration mới**: thêm cột lịch vào `meetings`, backfill từ `meeting_sessions` (giờ bắt đầu sớm
  nhất, giờ kết thúc muộn nhất trong các phiên không `Cancelled`), drop `meeting_sessions`, bỏ hai giá trị enum.
  Không sửa migration cũ đã lên `main`.
- Thay thế một phần **ADR-0010** (phòng mở theo phiên `InProgress`, cột `call_sessions.meeting_session_id`) và
  **ADR-0011** (dữ liệu panel theo phiên, planning / live context theo phiên). Hai ADR đó giữ nguyên nội dung;
  dòng trạng thái ghi phần bị thay thế.
- Tài liệu API trong `docs/03-api/` mô tả code đang chạy nên giữ banner cảnh báo tới khi có migration.
- Mã thông báo dành cho Meeting Session (`MSG48`–`MSG59`, `MSG77`, `MSG113`) ngừng dùng; cập nhật catalogue
  cùng commit với code.
- Một cuộc họp nhiều buổi được tổ chức thành nhiều cuộc họp riêng; báo cáo thống kê đếm theo cuộc họp.
