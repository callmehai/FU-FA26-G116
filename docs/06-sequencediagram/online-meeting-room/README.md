# Phòng họp trực tuyến — FE-08

Các sequence UC-37, UC-38, UC-39a → UC-39d, UC-40, UC-41, UC-42, UC-78 và UC-79 tuân theo
[convention chung](../README.md). Đặc tả nằm ở
[Report 3, FE-08](../../08-reports/Report-3_Software_Requirement_Specification.md#8-fe-08--online-meeting-room-management);
số BR và MSG theo danh mục sau khi gộp `main` 29/10/2026 (BR-323 → BR-332, BR-336 → BR-339; MSG117, MSG118,
MSG123, MSG124, MSG280 → MSG284, MSG328 → MSG343, MSG350).

- Phòng chỉ mở khi cuộc họp `InProgress`. Trước giờ họp màn chờ hiện MSG117 và tự mở khi cuộc họp bắt đầu (UC-37 AF-3);
  cuộc họp đã kết thúc hiện MSG331, đã huỷ hiện MSG350.
- Lifeline `:Room Service` là dịch vụ phòng họp mà UC nhắc tới; sơ đồ không vẽ tên sản phẩm hay giao thức bên dưới (convention §10).
- Micro, camera, chia sẻ màn hình, chọn thiết bị, chat và cửa sổ nổi chạy ở trình duyệt, nói chuyện thẳng với
  dịch vụ phòng họp nên không có controller hay database; UC-37 và UC-41 đi qua backend để kiểm quyền.
- **Guest** là actor trong đặc tả (UC-37 AF-2) nhưng chưa được cài: chờ xác thực Guest (SCRUM-25).
- **UC-79** là yêu cầu chưa cài (mới có bảng `speaking_requests`); sơ đồ vẽ luồng chính theo đặc tả.
- UC-37 EX-7 (phòng đủ 50 người, BR-336) chưa có mã MSG nên sơ đồ ghi ở bước kết nối.
- Ghi âm (FE-09) không vẽ ở đây; UC-37 và UC-39a chỉ nhắc tới.

| UC | File |
|---|---|
| UC-37 Join Meeting Room | `UC37_JoinMeetingRoom` |
| UC-38 Leave Meeting Room | `UC38_LeaveMeetingRoom` |
| UC-39a Manage Microphone | `UC39a_ManageMicrophone` |
| UC-39b Manage Camera | `UC39b_ManageCamera` |
| UC-39c Manage Screen Sharing | `UC39c_ManageScreenSharing` |
| UC-39d Choose Audio and Video Devices | `UC39d_ChooseAudioAndVideoDevices` |
| UC-40 View Meeting Room Participants | `UC40_ViewMeetingRoomParticipants` |
| UC-41 Moderate Meeting Room | `UC41_ModerateMeetingRoom` |
| UC-42 Use In-room Chat | `UC42_UseInRoomChat` |
| UC-78 Keep Meeting in a Floating Window | `UC78_FloatingWindow` |
| UC-79 Register to Speak | `UC79_RegisterToSpeak` |

## Xuất ảnh

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/online-meeting-room
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/online-meeting-room/UC*.txt
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
