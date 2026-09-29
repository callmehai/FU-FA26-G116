# Vòng đời cuộc họp

Trạng thái của một cuộc họp, theo đúng thứ tự nó đi qua. Đây là xương sống của toàn bộ nghiệp
vụ — gần như mọi màn hình đều đọc hoặc thay đổi trạng thái này.

Tham chiếu từ hệ thống AI-eCabinet (xem [ADR-0002](../05-decisions/0002-khung-giao-dien-theo-ai-ecabinet.md)).
Mã trạng thái giữ nguyên tiếng Anh để dùng trực tiếp trong code; nhãn tiếng Việt chỉ dùng khi hiển thị.

| Mã | Nhãn hiển thị | Ý nghĩa |
|---|---|---|
| `Draft` | Nháp | Thư ký đang soạn, chưa ai khác thấy |
| `WaitingApproval` | Chờ duyệt | Đã gửi, đang đợi người có quyền duyệt |
| `Rejected` | Bị từ chối | Người duyệt trả lại; Organizer phải chuyển về `Draft` trước khi sửa |
| `Approved` | Đã duyệt | Được chấp thuận, chưa thông báo cho thành phần tham dự |
| `Published` | Đã công bố | Meeting đã công bố; invitation jobs đã xếp hàng; thành phần tham dự nhìn thấy. Email được gửi bất đồng bộ |
| `InProgress` | Đang diễn ra | Chủ toạ đã bắt đầu phiên họp |
| `Ended` | Đã kết thúc | Phiên họp đóng, chờ xử lý hậu kỳ (ASR, biên bản) |
| `MinutesIssued` | Đã ban hành biên bản | Biên bản đã được phê duyệt và chốt |
| `Archived` | Đã lưu trữ | Hồ sơ đóng, chỉ còn đọc |

```
Draft ──gửi duyệt──> WaitingApproval ──duyệt──> Approved ──công bố──> Published
  ^                        │                                              │
  │                        └──từ chối──> Rejected                         │
  └────Organizer đưa về nháp──────────────┘                           bắt đầu họp
                                                                          v
        Archived <──lưu trữ── MinutesIssued <──ban hành biên bản── Ended <┘
```

`Rejected` không được gửi thẳng sang `WaitingApproval`. Meeting Organizer phải thực hiện một
transition riêng `Rejected → Draft`; transition này ghi lịch sử, giữ nguyên lý do từ chối cũ và
không kích hoạt thư mời. Organizer sửa cuộc họp ở `Draft`, sau đó gửi lại theo đúng flow
`Draft → WaitingApproval` và có thể chọn lại một người duyệt đang hợp lệ.

Khi gửi duyệt theo UC-15, người tổ chức phải chọn một tài khoản đang hoạt động có capability
`MeetingApprover`. Trường `AssignedApproverId` được để trống trong bản nháp cho tới khi người duyệt
được chọn.

`Published` xác nhận transaction công bố đã lưu meeting, history và invitation jobs. Đây không phải
xác nhận email đã được giao: trạng thái gửi thực tế được theo dõi bằng `EmailStatus` và `SentAt`;
lỗi gửi hoặc retry không chuyển meeting khỏi `Published`.

## Vì sao cần ghi lại

- **Phân quyền bám theo trạng thái.** Sửa nội dung chỉ được phép ở `Draft`; meeting `Rejected` phải
  được Organizer đưa về `Draft` trước. Biểu
  quyết chỉ mở khi `InProgress`. Biên bản chỉ sinh được sau `Ended`.
- **Lịch sử trạng thái là bằng chứng.** Màn chi tiết hiển thị timeline ai chuyển trạng thái, lúc
  nào. Đây là dữ liệu kiểm toán, không phải log kỹ thuật — không được xoá hay sửa.
- **Quyền đọc lịch sử bám theo quan hệ hiện tại.** Meeting Organizer và người duyệt đang được gán
  xem được toàn bộ lifecycle history, gồm lý do từ chối. Người từng được gán trước đó không giữ
  quyền này chỉ vì assignment cũ vẫn xuất hiện trong lịch sử. Participant được xem meeting sau
  Published có thể xem timeline nhưng không được xem rejection reason.
- **Chair không phải quyền phê duyệt hay công bố.** Chair chỉ là quan hệ trong một cuộc họp.
  Approve/Reject thuộc người duyệt đang được gán và còn capability `MeetingApprover`; Publish thuộc
  Meeting Organizer. Một Chair chỉ làm được các action đó khi đồng thời có đúng quan hệ tương ứng.
- **Recipient được snapshot khi Publish (BR-54).** Chỉ participant row có user active và `Response` khác
  `Delegated` được đưa vào batch notification/invitation. Không tự thêm delegate chỉ vì
  `DelegatedToUserId` trỏ tới họ; participant row riêng của delegate vẫn được xét theo rule này.
  Thay đổi participants sau commit không sửa batch đã tạo.
- **Nhãn trong timeline khác nhãn trạng thái.** Timeline ghi *việc đã xảy ra* ("Đã gửi duyệt"),
  còn ô trạng thái ghi *cuộc họp đang là gì* ("Chờ duyệt"). Cùng một mã, hai cách diễn đạt.

## Mức độ mật

Độc lập với trạng thái, quyết định ai đọc được tài liệu và bản ghi.

| Mã | Nhãn | Ghi chú |
|---|---|---|
| `Normal` | Thường | Mặc định |
| `Internal` | Nội bộ | Trong nội bộ tổ chức |
| `Confidential` | Hạn chế | Chỉ thành phần tham dự |
| `Secret` | Mật | Hiển thị cảnh báo trên màn chi tiết; cân nhắc chặn tải về |

Hiện thực phía frontend: [types/meeting.ts](../../frontend/src/types/meeting.ts).
