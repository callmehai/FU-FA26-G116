# Phòng họp trực tuyến — FE-08

Các sequence UC-43, UC-44, UC-45a → UC-45d, UC-46, UC-47, UC-48, UC-49, UC-50 tuân theo
[convention chung](../README.md). Đặc tả nằm ở
[Report 3, FE-08](../../08-reports/Report-3_Software_Requirement_Specification.md#8-fe-08--online-meeting-room-management);
số BR và MSG theo danh mục sau khi gộp `main` 29/10/2026 (BR-100–BR-106; MSG117, MSG118,
MSG123, MSG124, MSG280 → MSG284, MSG328 → MSG343, MSG350).

- Phòng chỉ mở khi cuộc họp `InProgress`. Trước giờ họp màn chờ hiện MSG117 và tự mở khi cuộc họp bắt đầu (UC-43 AF-3);
  cuộc họp đã kết thúc hiện MSG331, đã huỷ hiện MSG350.
- Lifeline `:Room Service` là dịch vụ phòng họp mà UC nhắc tới; sơ đồ không vẽ tên sản phẩm hay giao thức bên dưới (convention §10).
  Ngoại lệ là UC-43: theo review của GVHD trên Report 4 (25/11/2026), sơ đồ vẽ rõ `:LiveKit Gateway` ký access token
  (JWT, chỉ vào đúng phòng này), trình duyệt mở WebSocket báo hiệu `/rtc` tới `:Room Service (LiveKit SFU)`, trao đổi
  SDP và ICE, và webhook `participant_joined` để backend ghi phiên vào phòng (`CallRoomService.JoinAsync`,
  `LiveKitGateway.CreateJoinTicket`, `HandleWebhookAsync`).
- Micro, camera, chia sẻ màn hình, chọn thiết bị, chat và cửa sổ nổi chạy ở trình duyệt, nói chuyện thẳng với
  dịch vụ phòng họp nên không có controller hay database; UC-43, UC-47 đi qua backend để kiểm quyền.
- **Guest** là actor trong đặc tả (UC-43 AF-2) nhưng chưa được cài: chờ xác thực Guest (SCRUM-25).
- **UC-50** là yêu cầu chưa cài (mới có bảng `speaking_requests`); sơ đồ vẽ luồng chính theo đặc tả.
- UC-43 EX-7 (phòng đủ 50 người, NFR) chưa có mã MSG nên sơ đồ ghi ở bước kết nối.
- Ghi âm (FE-09) không vẽ ở đây; UC-43, UC-45a chỉ nhắc tới.

| UC | File |
|---|---|
| UC-43 Join Meeting Room | `UC43_JoinMeetingRoom` |
| UC-44 Leave Meeting Room | `UC44_LeaveMeetingRoom` |
| UC-45a Manage Microphone | `UC45a_ManageMicrophone` |
| UC-45b Manage Camera | `UC45b_ManageCamera` |
| UC-45c Manage Screen Sharing | `UC45c_ManageScreenSharing` |
| UC-45d Choose Audio and Video Devices | `UC45d_ChooseAudioAndVideoDevices` |
| UC-46 View Meeting Room Participants | `UC46_ViewMeetingRoomParticipants` |
| UC-47 Moderate Meeting Room | `UC47_ModerateMeetingRoom` |
| UC-48 Use In-room Chat | `UC48_UseInRoomChat` |
| UC-49 Keep Meeting in a Floating Window | `UC49_FloatingWindow` |
| UC-50 Register to Speak | `UC50_RegisterToSpeak` |

## Xuất ảnh

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/online-meeting-room
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/online-meeting-room/UC*.txt
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
