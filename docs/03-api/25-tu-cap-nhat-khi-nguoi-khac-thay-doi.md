# Tự cập nhật khi người khác thay đổi

Màn hình đang mở tự đọc lại khi **người khác** đổi dữ liệu, không cần bấm tải lại. Có ba đường, từ nhanh tới chậm:

1. **Event trên hub `/hubs/meetings`** cho các màn hình của một cuộc họp (trang chi tiết, các tab, trình xem tài liệu,
   phòng họp).
2. **Thông báo realtime** trên hub `/hubs/notifications` (`notificationReceived`, [06](06-thong-bao-trong-ung-dung.md)):
   màn hình nào liên quan thì đọc lại khi có thông báo hợp loại.
3. **Quay lại tab** (cửa sổ được focus hoặc tab hiện lại), cách lần đọc trước ít nhất 30 giây: bắt những thay đổi không
   gửi thông báo cho người đang xem.

Mọi event và thông báo chỉ là **tín hiệu "đọc lại"**. FE gọi lại endpoint của màn hình, và endpoint đó kiểm quyền lại như
mọi lần. Event không mang dữ liệu mà người nhận không được xem.

## Event mới trên `/hubs/meetings`

Cả ba có cùng payload `{ "eventId", "meetingId", "occurredAt" }` (`MeetingRereadChange`). `eventId` riêng cho mỗi lần
phát, FE bỏ event trùng theo nó. Event phát **sau commit**. Push lỗi hoặc chậm quá 2 giây chỉ ghi log, không làm thao tác
thất bại. Thao tác bị từ chối (4xx, 5xx) không phát gì.

| Event | Phát khi | Người nhận | Màn hình nghe |
|---|---|---|---|
| `meetingChanged` | Mọi bước workflow thành công qua `POST /api/meetings/{id}/transitions`: gửi duyệt, duyệt, từ chối, về nháp, công bố, huỷ, bắt đầu, kết thúc, chuyển người tổ chức. `PUT /api/meetings/{id}` thành công (sửa, dời lịch). Hệ thống tự huỷ cuộc họp quá hạn (BR-301) và tự kết thúc cuộc họp bỏ quên (ADR-0062) | Mọi người đang đọc được cuộc họp đã `JoinMeeting`, **và** người gắn với cuộc họp vừa mất quyền đọc (thành phần bị gỡ, cuộc họp huỷ trước khi công bố). Người sau chỉ có trong nhóm nếu đã join lúc còn quyền; họ chỉ nhận id, lần đọc lại trả `404` và trang hiện "Không tìm thấy" | Trang chi tiết cuộc họp: đọc lại cuộc họp (header, trạng thái, thanh duyệt) và lịch sử |
| `minutesChanged` | Dự thảo AI: xin viết (`Queued`), AI bắt đầu (`Running`), viết xong (`Ready`) hoặc lỗi (`Failed`), sửa, duyệt dự thảo. Biên bản: tạo hoặc tạo lại, lưu, gửi duyệt, duyệt và ban hành, trả lại, đính chính | Mọi người đang đọc được cuộc họp đã join. Bản nháp vẫn chỉ Chủ toạ, Thư ký đọc được: người khác đọc lại và nhận đúng phần của mình (BR-386, BR-412) | Tab Biên bản: đọc lại cả thẻ Dự thảo và thẻ Biên bản. Dự thảo chuyển `Queued` thì thẻ tự bật vòng hỏi 5 giây như khi chính mình bấm |
| `commentsChanged` | Gửi góp ý hoặc trả lời thành công trên bất kỳ tài liệu nào của cuộc họp (`POST …/documents/{id}/comments`) | Mọi người đang đọc được cuộc họp đã join, kể cả Khách đang có phiên | Panel Góp ý đang mở (đọc lại danh sách của tài liệu mình), danh sách tài liệu (số góp ý) |

`commentsChanged` **không** nói tài liệu nào: id của một tài liệu nháp không tới người không được xem nháp. Panel đang mở
tự đọc lại tài liệu của mình. Nút "Làm mới" trong panel Góp ý đã bỏ vì thừa: panel cũng đọc lại sau mỗi lần kết nối hub
lại.

Khách đang có phiên và đã join cũng nhận ba event này (chỉ id). Trang Khách hiện chỉ nghe `participantsChanged`; panel
Góp ý trong trình xem tài liệu của Khách dùng chung `CommentsPanel` nên tự đọc lại theo `commentsChanged`.

## Màn hình đọc lại theo thông báo hoặc khi quay lại tab

| Màn hình | Thông báo làm đọc lại | Quay lại tab (≥ 30 giây) |
|---|---|---|
| Danh sách cuộc họp và các thẻ thống kê | mọi thông báo có `meetingId` | có, giữ bộ lọc, sắp xếp, trang; bảng mờ đi trong lúc tải như khi lọc |
| Lịch làm việc | mọi thông báo có `meetingId` | có, giữ khoảng ngày đang xem |
| Ô xem nhanh trên lịch | thông báo của đúng cuộc họp đó | — |
| Trang Voice ID | `VoiceSampleCollected` | — |
| Tìm giờ họp (danh sách người đã chia sẻ) | `CalendarShare*` | — |
| Người dùng | — | có, trừ khi đang mở hộp thoại |
| Vai trò và ma trận quyền | — | có, trừ khi còn dòng đang sửa chưa lưu, đang lưu hoặc mở hộp thoại |
| Cấu hình AI | — | có, trừ khi biểu mẫu đang sửa, đang lưu hoặc đang thử kết nối |
| Nhật ký kiểm toán, Báo cáo thống kê | — | có |
| Kho tài liệu cá nhân, Kho của tài khoản (quản trị) | — | có; kho cá nhân trừ khi đang mở hộp thoại |

Thay đổi không gửi thông báo cho người đang xem (huỷ cuộc họp nháp, sửa cuộc họp chưa công bố) hiện ở danh sách và lịch
khi người đó quay lại tab.

## Code

- Backend: `IMeetingChangePublisher` (Domain), `MeetingChangePublisher` (Web). Nhóm người nhận dùng
  `AgendaChangePublisher.ReaderGroupsAsync`, `includeFormer: true` cho `meetingChanged`.
- Frontend: `useMeetingEvents` (event hub), `useNotificationRefresh` (thông báo), `lib/useRefetchOnFocus` (quay lại tab).
