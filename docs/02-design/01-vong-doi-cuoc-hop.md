# Vòng đời cuộc họp

Trạng thái của một cuộc họp, theo đúng thứ tự nó đi qua. Đây là xương sống của toàn bộ nghiệp
vụ — gần như mọi màn hình đều đọc hoặc thay đổi trạng thái này.

Tham chiếu từ hệ thống AI-eCabinet (xem [ADR-0002](../05-decisions/0002-khung-giao-dien-theo-ai-ecabinet.md)).
Mã trạng thái giữ nguyên tiếng Anh để dùng trực tiếp trong code; nhãn tiếng Việt chỉ dùng khi hiển thị.

| Mã | Nhãn hiển thị | Ý nghĩa |
|---|---|---|
| `Draft` | Nháp | Thư ký đang soạn, chưa ai khác thấy |
| `WaitingApproval` | Chờ duyệt | Đã gửi, đang đợi người có quyền duyệt |
| `Rejected` | Bị từ chối | Organizer được sửa; chuyển về `Draft` trước khi gửi duyệt lại |
| `Approved` | Đã duyệt | Được chấp thuận, chưa thông báo cho thành phần tham dự |
| `Published` | Đã công bố | Thành phần tham dự nhìn thấy; thư mời đã xếp hàng để gửi bất đồng bộ |
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

Organizer được sửa ở `Draft` hoặc `Rejected`. Để gửi duyệt lại sau từ chối, phải thực hiện
`ReturnToDraft` trước; assignment cũ được xóa và lịch sử/lý do từ chối được giữ nguyên.

Khi gửi duyệt theo UC-15, người tổ chức phải chọn một tài khoản đang hoạt động có capability
`MeetingApprover`, khác chính Organizer. Trường `AssignedApproverId` để trống trong bản nháp cho
tới khi gửi duyệt.

Khi `WaitingApproval` thiếu người duyệt, gán chính Organizer, hoặc người duyệt inactive/mất
`MeetingApprover`, Organizer active có `MeetingCreator` được `ReturnToDraft` để chọn lại người duyệt.
Không cho rút khi người duyệt khác Organizer vẫn hợp lệ.

`Published` xác nhận thư mời đã xếp hàng, chưa xác nhận email đã được giao. Lỗi gửi không đổi
trạng thái cuộc họp. Chi tiết quyền, người nhận và các transition:
[API quản lý cuộc họp](../03-api/05-quan-ly-cuoc-hop.md).

## Vì sao cần ghi lại

- **Phân quyền bám theo trạng thái.** Sửa nội dung chỉ được phép ở `Draft` và `Rejected`. Biểu
  quyết chỉ mở khi `InProgress`. Biên bản chỉ sinh được sau `Ended`.
- **Lịch sử trạng thái là bằng chứng.** Màn chi tiết hiển thị timeline ai chuyển trạng thái, lúc
  nào. Đây là dữ liệu kiểm toán, không phải log kỹ thuật — không được xoá hay sửa.
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
