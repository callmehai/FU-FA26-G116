# ADR-0032: Trạng thái Cancelled, huỷ cuộc họp và tự huỷ khi quá hạn

- **Trạng thái:** Đề xuất — SCRUM-99, PO 27/10/2026
- **Ngày:** 2026-10-27
- **Người quyết định:** PO (Hải)

## Bối cảnh

Hội đồng review lần 1 (mục 4.1) chỉ ra hai lỗ hổng của vòng đời cuộc họp:

- **Không có cách nói "cuộc họp này không diễn ra nữa".** Người tổ chức chỉ có thể để nó nằm đó hoặc dời lịch mãi.
- **Cuộc họp bị quên vẫn "Đã công bố" mãi mãi.** Nó vẫn nằm trên lịch của mọi người, dù giờ họp đã qua từ lâu.

Report 3 đã đặc tả UC-75 Cancel Meeting và BR-297 → BR-302. BR-301 và BR-302 còn chờ PO chốt mốc giờ.

## Quyết định

1. **Thêm trạng thái cuối `Cancelled`**, chỉ tới được từ `Draft`, `WaitingApproval`, `Rejected`, `Approved`, `Published`. Cuộc họp
   đã bắt đầu thì kết thúc bằng End, không huỷ. `Cancelled` không sửa, gửi duyệt, công bố, dời lịch hay bắt đầu được (BR-299):
   các rule hiện có đều liệt kê trạng thái được phép, nên trạng thái mới tự bị chặn.
2. **Huỷ đi qua `POST /transitions` với `action: Cancel`**, giống Reject: bắt buộc lý do, tối đa 1.000 ký tự. Chỉ Organizer được
   huỷ. Lý do, người huỷ, thời điểm và trạng thái trước khi huỷ được lưu trên `meetings` (để hiển thị và để lọc quyền đọc bằng SQL),
   đồng thời ghi một dòng history `Cancel`.
3. **Ai được báo:** huỷ sau khi công bố thì mọi người được mời nhận thông báo trong app và email kèm lý do (BR-300). Email kèm
   `.ics` `METHOD:CANCEL` cùng UID với thư mời, để lịch của người nhận tự gỡ sự kiện. Huỷ khi đang chờ duyệt thì báo người duyệt.
   Huỷ trước đó thì không báo ai, vì chưa ai được mời. Mọi thông báo được xếp trong cùng transaction và gửi sau đó, nên lỗi gửi
   không làm mất việc huỷ.
4. **Quyền đọc:** thành viên và Chair đọc được cuộc họp `Cancelled` nếu nó đã được công bố trước khi huỷ
   (`CancelledFromStatus = Published`), vì họ đã nhận thông báo kèm liên kết. Cuộc họp huỷ khi chưa công bố vẫn ẩn với họ.
5. **Tự huỷ (PO 27/10/2026):**
   - mọi cuộc họp chưa bắt đầu, quá **24 giờ sau giờ bắt đầu dự kiến** (`Meetings:AutoCancelAfterHours`), sẽ bị hệ thống huỷ;
   - job chạy 15 phút một lần, mỗi cuộc họp một transaction, kiểm lại dưới khoá dòng;
   - history `AutoCancel` có `ActorId = null` (cột thành nullable), hiển thị là "Hệ thống";
   - chỉ báo Organizer: giờ họp đã qua, báo cả đoàn là thừa;
   - muốn giữ cuộc họp thì Thư ký dời lịch trước mốc đó.
6. **Bỏ BR-302** (tự `Ended` cuộc họp đang diễn ra sau 4 giờ): người dùng tự kết thúc.

## Khác với đặc tả

- BR-301 tính 24 giờ **từ giờ kết thúc** và chỉ áp cho `Published`. PO chốt **tính từ giờ bắt đầu** và áp cho mọi trạng thái
  trước khi bắt đầu, vì một bản nháp hay một cuộc họp chờ duyệt đã quá ngày cũng không còn ý nghĩa.
- Thêm `MSG345` (lý do quá dài), tương ứng với `MSG84` của Reject. UC-75 chỉ có EX-1 cho trường hợp lý do bị bỏ trống.

## Hệ quả

- Lần deploy đầu tiên sẽ huỷ cùng lúc mọi cuộc họp cũ chưa bắt đầu trên server test. Trên DB máy dev, con số là 50 cuộc họp,
  phần lớn là dữ liệu `[Test FE]`.
- Rollback migration `MeetingCancellation` bị chặn khi đã có cuộc họp `Cancelled` hoặc history do hệ thống ghi, để không bịa ra
  người thực hiện hay trạng thái.
- Lịch ẩn cuộc họp đã huỷ theo mặc định. Bộ lọc lịch có nhóm "Đã huỷ" riêng, tên cuộc họp hiển thị gạch ngang.
