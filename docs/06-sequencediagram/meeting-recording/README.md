# Sequence Diagram — Ghi âm cuộc họp (FE-09)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-09 Meeting Recording
(UC-43a → UC-43c, UC-44a → UC-44e, UC-80), đã đối chiếu với code ngày 30/10/2026 và vẽ theo đặc tả **đã sửa** ở
[plan 19 §12](../../notes/ban-giao-ba-final-review/19-plan-fe-09-fe-10-ghi-am-go-bang-voice-id.md#12-review-lượt-13-30102026--việc-phải-sửa).
Luồng gửi từng câu khi bật mic của mình là một phần của UC-39a (AF-2, quyết định D9) nên nằm ở
[`online-meeting-room/UC39a_RecordSpeechWhileMicrophoneOn.txt`](../online-meeting-room/UC39a_RecordSpeechWhileMicrophoneOn.txt).

| Use Case | Source |
|---|---|
| UC-43a View Meeting Recordings | [UC43a_ViewMeetingRecordings.txt](UC43a_ViewMeetingRecordings.txt) |
| UC-43b Play Meeting Recording | [UC43b_PlayMeetingRecording.txt](UC43b_PlayMeetingRecording.txt) |
| UC-43c Download Meeting Recording | [UC43c_DownloadMeetingRecording.txt](UC43c_DownloadMeetingRecording.txt) |
| UC-44a Start Room Recording | [UC44a_StartRoomRecording.txt](UC44a_StartRoomRecording.txt) |
| UC-44b Stop and Save Room Recording | [UC44b_StopAndSaveRoomRecording.txt](UC44b_StopAndSaveRoomRecording.txt) |
| UC-44c Upload Meeting Audio File | [UC44c_UploadMeetingAudioFile.txt](UC44c_UploadMeetingAudioFile.txt) |
| UC-44d Delete Recording Take | [UC44d_DeleteRecordingTake.txt](UC44d_DeleteRecordingTake.txt) |
| UC-44e Erase Meeting Recording | [UC44e_EraseMeetingRecording.txt](UC44e_EraseMeetingRecording.txt) |
| UC-80 Manage My Recorded Speech | [UC80_ManageMyRecordedSpeech.txt](UC80_ManageMyRecordedSpeech.txt) |

Quy ước riêng của nhóm này:

- Lifeline theo code: `:Recording Controller` / `:Speech Clip Controller` là `RecordingEndpoints` / `SpeechClipEndpoints`;
  `:Recording Service`, `:Room Take Service`, `:Speech Clip Service` là `IRecordingService`, `IRoomTakeService`,
  `ISpeechClipService`; tên thao tác Controller → Service lấy đúng tên interface, bỏ `Async` và `CancellationToken`
  (`begin`, `append`, `complete`, `deleteTake`, `list`, `playback`, `download`, `erase`, `delete`…).
- `:Meeting Repository` đọc cuộc họp kèm thành phần hiện tại; việc kiểm quyền (BR-341, BR-350, BR-351, BR-370) là self-call
  của service vì code kiểm bằng `RecordingRules` / `SpeechClipRules`, không có lớp policy riêng.
- `:Speech Clip Repository` là tầng lưu câu nói logic: code `SpeechClipService` dùng thẳng `TrustCabinetDbContext.SpeechClips`,
  không có interface repository (ghi ở [07-classdiagram §6g](../../07-classdiagram/README.md)).
- `:Playback Link Signer` gộp `IRecordingPlaybackSigner` (bản ghi) và `ISpeechClipAudioSigner` (một câu); trình duyệt
  phát âm thanh qua link ký có hạn, nên sơ đồ vẽ bước tải âm thanh từ `:Recording Storage`.
- Người dùng không thuộc thành phần hiện tại nhận câu trả lời "không tìm thấy" (không có mã MSG), như UC-37 EX-4;
  đặc tả ghi nhánh này ở UC-43a EX-3 và UC-43b EX-2; ở các UC khác sơ đồ vẽ nhánh này không có số EX.
- **Guest** không ghi được theo micro và không mở được bản ghi (BR-370, D6 — NEEDS REVIEW PO): mọi route chặn bằng
  tài khoản nội bộ đang hoạt động.
- Việc hệ thống tự chạy không có request của actor nên **không vẽ thành sơ đồ riêng**: UC-44b AF-2 (lượt không nhận tiếng
  12 giờ được tự lưu — `IRoomTakeService.completeIdle()` chạy đúng các bước lưu của UC-44b) và UC-44e AF-1 (hết 180 ngày —
  `IRecordingService.eraseExpired()` chạy đúng các bước xoá của UC-44e, lý do `retention`, không có actor).
- UC-44a AF-1 (mở lại tab đã đóng → *Ghi tiếp* tiếp tục vòng gửi phần, *Lưu phần đã ghi* là UC-44b) là lựa chọn thứ hai của
  actor nên không vẽ; UC-44a AF-3 (gần giới hạn dung lượng lượt → tự lưu và mở lượt mới) vẽ bằng `ref UC-44b`.
- UC-80 bắt đầu ở thao tác xác nhận xoá câu của mình; xem danh sách câu là UC-43a (lọc theo người nói, kèm câu còn chờ
  gửi trên máy), nghe lại là UC-43b.
- Nhánh `alt` **không có số EX** là hành vi code có nhưng đặc tả UC chưa ghi: người không còn trong thành phần (trả lời "không
  tìm thấy"); UC-44d lỗi lưu (MSG298). BA bổ sung EX vào đặc tả hoặc bỏ nhánh khi duyệt.
- `ref` sang UC khác viết thành self-message `ref UC-…` để file draw.io (dựng qua Mermaid, không có `ref`) cũng hiện.

## Xuất ảnh

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/meeting-recording
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/meeting-recording/UC*.txt
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
