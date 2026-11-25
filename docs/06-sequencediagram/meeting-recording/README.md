# Sequence Diagram — Ghi âm cuộc họp (FE-09)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-09 Meeting Recording
(UC-51a → UC-51c, UC-52a, UC-52c → UC-52e, UC-53), đã đối chiếu với code ngày 30/10/2026 và vẽ theo đặc tả **đã sửa** ở
[plan 19 §12](../../notes/ban-giao-ba-final-review/19-plan-fe-09-fe-10-ghi-am-go-bang-voice-id.md#12-review-lượt-13-30102026--việc-phải-sửa).
Luồng gửi từng câu khi bật mic của mình là một phần của UC-45a (AF-2, quyết định D9) nên nằm ở
[`online-meeting-room/UC45a_RecordSpeechWhileMicrophoneOn.txt`](../online-meeting-room/UC45a_RecordSpeechWhileMicrophoneOn.txt).

| Use Case | Source |
|---|---|
| UC-51a View Meeting Recordings | [UC51a_ViewMeetingRecordings.txt](UC51a_ViewMeetingRecordings.txt) |
| UC-51b Play Meeting Recording | [UC51b_PlayMeetingRecording.txt](UC51b_PlayMeetingRecording.txt) |
| UC-51c Download Meeting Recording | [UC51c_DownloadMeetingRecording.txt](UC51c_DownloadMeetingRecording.txt) |
| UC-52a Turn Room Microphone On/Off | [UC52a_TurnRoomMicrophoneOnOff.txt](UC52a_TurnRoomMicrophoneOnOff.txt) |
| UC-52c Upload Meeting Audio File | [UC52c_UploadMeetingAudioFile.txt](UC52c_UploadMeetingAudioFile.txt) |
| UC-52d Delete Recording Take | [UC52d_DeleteRecordingTake.txt](UC52d_DeleteRecordingTake.txt) |
| UC-52e Erase Meeting Recording | [UC52e_EraseMeetingRecording.txt](UC52e_EraseMeetingRecording.txt) |
| UC-53 Manage My Recorded Speech | [UC53_ManageMyRecordedSpeech.txt](UC53_ManageMyRecordedSpeech.txt) |

Quy ước riêng của nhóm này:

- Lifeline theo code: `:Recording Controller` / `:Speech Clip Controller` là `RecordingEndpoints` / `SpeechClipEndpoints`;
  `:Recording Service`, `:Room Take Service`, `:Speech Clip Service` là `IRecordingService`, `IRoomTakeService`,
  `ISpeechClipService`; tên thao tác Controller → Service lấy đúng tên interface, bỏ `Async` và `CancellationToken`
  (`begin`, `append`, `complete`, `deleteTake`, `list`, `playback`, `download`, `erase`, `delete`…).
- `:Meeting Repository` đọc cuộc họp kèm thành phần hiện tại; việc kiểm quyền (BR-108, BR-111, BR-112) là self-call
  của service vì code kiểm bằng `RecordingRules` / `SpeechClipRules`, không có lớp policy riêng.
- `:Speech Clip Repository` là tầng lưu câu nói logic: code `SpeechClipService` dùng thẳng `TrustCabinetDbContext.SpeechClips`,
  không có interface repository (ghi ở [07-classdiagram §6g](../../07-classdiagram/README.md)).
- `:Playback Link Signer` gộp `IRecordingPlaybackSigner` (bản ghi) và `ISpeechClipAudioSigner` (một câu); trình duyệt
  phát âm thanh qua link ký có hạn, nên sơ đồ vẽ bước tải âm thanh từ `:Recording Storage`.
- Người dùng không thuộc thành phần hiện tại nhận câu trả lời "không tìm thấy" (không có mã MSG), như UC-43 EX-4;
  đặc tả ghi nhánh này ở UC-51a EX-3 và UC-51b EX-2; ở các UC khác sơ đồ vẽ nhánh này không có số EX.
- **Guest** không ghi được theo micro và không mở được bản ghi (BR-111, BR-112, D6 — NEEDS REVIEW PO): mọi route chặn bằng
  tài khoản nội bộ đang hoạt động.
- **UC-52a có hai request của actor** (bật mic phòng, rồi tắt mic phòng), ngoại lệ của quy ước "một request Actor" như UC-04:
  UC-52b "Stop and Save Room Recording" đã bỏ (note 60 §3.2, 25/11/2026), luồng lưu lượt của nó (MSG362, MSG363, MSG365,
  MSG295, lượt rỗng, lượt đầu hoặc bản ghi phòng đầy) gộp vào cuối UC-52a, sau vòng lặp "While the room microphone is on".
  Request thứ hai cũng là đường dừng lượt còn mở ở máy khác (AF-3).
- Việc hệ thống tự chạy không có request của actor nên **không vẽ ở đây**: server tự lưu lượt không nhận tiếng 12 giờ
  (`IRoomTakeService.completeIdle()`, chạy đúng các bước lưu cuối UC-52a), tự xoá bản ghi hết 180 ngày
  (`IRecordingService.eraseExpired()`, các bước xoá của UC-52e, lý do `retention`) và dựng bản ghi gộp. Thiết kế của chúng ở
  Report 4 phần IV; sơ đồ hệ thống ở [`system-processes/`](../system-processes/README.md).
- UC-52a AF-2 (mở lại tab đã đóng → *Ghi tiếp* tiếp tục vòng gửi phần, *Lưu phần đã ghi* đi vào bước tắt mic) là lựa chọn
  thứ hai của actor nên không vẽ riêng.
- UC-53 bắt đầu ở thao tác xác nhận xoá câu của mình; xem danh sách câu là UC-51a (lọc theo người nói, kèm câu còn chờ
  gửi trên máy), nghe lại là UC-51b.
- Nhánh `alt` **không có số EX** là hành vi code có nhưng đặc tả UC chưa ghi: người không còn trong thành phần (trả lời "không
  tìm thấy"); UC-52d lỗi lưu (MSG298). BA bổ sung EX vào đặc tả hoặc bỏ nhánh khi duyệt.
- `ref` sang UC khác viết thành self-message `ref UC-…` để file draw.io (dựng qua Mermaid, không có `ref`) cũng hiện.

## Cập nhật theo kiến trúc

- **17/11/2026 — CA-19 (SCRUM-184):** service không còn gọi thẳng `:Database` (backend bốn project, [ADR-0064](../../05-decisions/0064-backend-bon-project-clean-architecture.md)); chèn lifeline `:Audit Log Repository` giữa `:Audit Service` và `:Database` ở UC-51c. Message tới `:Database` giữ nguyên nội dung; luồng, nhánh `MSG`/`BR` và actor không đổi.

## Xuất ảnh

```bash
python scripts/export_sequence.py UC52a   # từng file đã sửa: tab drawio, drawio-png/ và ảnh sequencediagram.org
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
