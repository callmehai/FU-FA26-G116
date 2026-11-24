# Sequence Diagram — Gỡ băng, nhận diện người nói và dự thảo AI (FE-10)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-10
(UC-54a → UC-54c, UC-55a → UC-55d, UC-58 → UC-61, UC-56a → UC-56f), đã đối chiếu với code ngày 30/10/2026 và vẽ theo đặc tả **đã sửa** ở
[plan 19 §12](../../notes/ban-giao-ba-final-review/19-plan-fe-09-fe-10-ghi-am-go-bang-voice-id.md#12-review-lượt-13-30102026--việc-phải-sửa);
UC-58 → UC-61 thêm ngày 05/11/2026 theo PR #260. UC-51 cũ (công bố tóm tắt) chưa có code nên không có sơ đồ.

| Use Case | Source |
|---|---|
| UC-54a View Recording Transcript | [UC54a_ViewRecordingTranscript.txt](UC54a_ViewRecordingTranscript.txt) |
| UC-54b Export Meeting Transcript | [UC54b_ExportMeetingTranscript.txt](UC54b_ExportMeetingTranscript.txt) |
| UC-54c Transcribe Meeting Recording — Thư ký yêu cầu | [UC54c_TranscribeMeetingRecording.txt](UC54c_TranscribeMeetingRecording.txt) |
| UC-54c Transcribe Meeting Recording — hệ thống xử lý hàng chờ | [UC54c_ProcessTranscriptionQueue.txt](UC54c_ProcessTranscriptionQueue.txt) |
| UC-55a Edit Transcript Text | [UC55a_EditTranscriptText.txt](UC55a_EditTranscriptText.txt) |
| UC-55b Assign Speaker | [UC55b_AssignSpeaker.txt](UC55b_AssignSpeaker.txt) |
| UC-55c Exclude or Restore a Sentence | [UC55c_ExcludeOrRestoreSentence.txt](UC55c_ExcludeOrRestoreSentence.txt) |
| UC-55d Delete a Sentence | [UC55d_DeleteSentence.txt](UC55d_DeleteSentence.txt) |
| UC-58 Generate / Regenerate Meeting Summary | [UC58_GenerateMeetingSummary.txt](UC58_GenerateMeetingSummary.txt) |
| UC-59 View Meeting Summary | [UC59_ViewMeetingSummary.txt](UC59_ViewMeetingSummary.txt) |
| UC-60 Edit Meeting Summary | [UC60_EditMeetingSummary.txt](UC60_EditMeetingSummary.txt) |
| UC-61 Approve Meeting Summary | [UC61_ApproveMeetingSummary.txt](UC61_ApproveMeetingSummary.txt) |
| UC-56a View My Voice ID | [UC56a_ViewMyVoiceId.txt](UC56a_ViewMyVoiceId.txt) |
| UC-56b Give Voice ID Consent | [UC56b_GiveVoiceIdConsent.txt](UC56b_GiveVoiceIdConsent.txt) |
| UC-56c Record Voice Sample | [UC56c_RecordVoiceSample.txt](UC56c_RecordVoiceSample.txt) |
| UC-56d Play Voice Sample | [UC56d_PlayVoiceSample.txt](UC56d_PlayVoiceSample.txt) |
| UC-56e Delete Voice Sample | [UC56e_DeleteVoiceSample.txt](UC56e_DeleteVoiceSample.txt) |
| UC-56f Withdraw Voice ID Consent | [UC56f_WithdrawVoiceIdConsent.txt](UC56f_WithdrawVoiceIdConsent.txt) |
| UC-57a View Voice Collection | [UC57a_ViewVoiceCollection.txt](UC57a_ViewVoiceCollection.txt) |
| UC-57b Record Voice Sample for Attendee | [UC57b_RecordVoiceSampleForAttendee.txt](UC57b_RecordVoiceSampleForAttendee.txt) |
| UC-57c Play Collected Voice Sample | [UC57c_PlayCollectedVoiceSample.txt](UC57c_PlayCollectedVoiceSample.txt) |
| UC-57d Delete Collected Voice Sample | [UC57d_DeleteCollectedVoiceSample.txt](UC57d_DeleteCollectedVoiceSample.txt) |

Quy ước riêng của nhóm này:

- Lifeline theo code: `:Transcript Controller` / `:Voice ID Controller` là `TranscriptEndpoints` / `VoiceIdEndpoints`;
  `:Transcription Service`, `:Voice ID Service` là `ITranscriptionService`, `IVoiceIdService`; tên thao tác Controller →
  Service lấy đúng tên interface, bỏ `Async` (`get`, `retry`, `export`, `editSegment`, `assignSpeaker`, `processNext`,
  `grant`, `withdraw`, `addSample`, `deleteSample`, `sampleAudio`).
- `:Speech Recognition Service` là dịch vụ nhận dạng giọng nói chạy trong hệ thống (ADR-0026, BR-121): code là
  `ISpeechEngine` (gỡ băng) và `IVoiceEmbedder` (giọng mẫu), cùng một worker. `:Speaker Matcher` là lớp tĩnh `SpeakerMatcher`.
- **UC-54c có hai sơ đồ** (quyết định D7): Thư ký yêu cầu chuyển chữ (lần đầu, sau lỗi, hoặc làm lại bản đã xong), và
  một sơ đồ hệ thống — `TranscriptionWorker` lấy bản chờ lâu nhất trong hàng, chuyển chữ, gợi ý người nói, đánh dấu
  `Completed` hoặc `Failed` kèm lý do (EX-3). Đây là ngoại lệ của quy ước "một request Actor", như UC-04.
- UC-54b, UC-55a mỗi UC phủ cả bản ghi phòng (`:Transcript Controller`) và bản ghi theo micro (`:Speech Clip Controller`),
  vẽ bằng `alt` ở đầu sơ đồ.
- UC-55b AF-1 (gán cả nhóm "cùng giọng") là một vòng `loop`: màn hình gọi `assignSpeaker` cho từng giọng trong nhóm.
- UC-55b AF-2 (đổi người nói của **một câu**) là `opt` riêng: `assignSegmentSpeaker(segmentId, userId | guestId | label, actorId)`,
  tìm hoặc tạo giọng `SPEAKER_NN`, chuyển câu, tính lại thời lượng, ẩn giọng hết câu, trả `MSG407` (SCRUM-35, PR #265).
- Gợi ý người nói chỉ Thư ký thấy (BR-120, BR-123); giọng khớp rõ — từ ngưỡng tự gán (mặc định 0,6, bằng ngưỡng gợi ý) trở lên và người đó đã
  đồng ý thông báo 1.2 — thì máy **tự điền tên** và Thư ký vẫn đổi hoặc bỏ được (ADR-0045); máy không bao giờ gán Khách.
  Rút đồng ý xoá mọi giọng mẫu, mọi gợi ý **và tên máy đã điền** trong một giao dịch, tên Thư ký đã gán giữ nguyên
  (BR-117). Xoá một giọng mẫu (UC-56e) **không** tính lại hồ sơ giọng: không có hồ sơ
  gộp, mỗi mẫu một đặc trưng, gợi ý lấy mẫu giống nhất.
- Người dùng không thuộc thành phần hiện tại nhận câu trả lời "không tìm thấy" (không có mã MSG); sơ đồ vẽ nhánh này không
  có số EX. Voice ID chỉ đọc/ghi của chính người gọi: mẫu của người khác trả lời như không tồn tại.
- Nhánh `alt` **không có số EX** là hành vi code có nhưng đặc tả UC chưa ghi: người không còn trong thành phần (trả lời "không
  tìm thấy"); UC-54c người không phải Thư ký (MSG295); UC-55b, UC-55c lỗi lưu (MSG304). BA bổ sung EX vào đặc tả hoặc bỏ nhánh khi duyệt.
- `ref` sang UC khác viết thành self-message `ref UC-…` để file draw.io cũng hiện.

## Cập nhật theo kiến trúc

- **17/11/2026 — CA-19 (SCRUM-184):** service không còn gọi thẳng `:Database` (backend bốn project, [ADR-0064](../../05-decisions/0064-backend-bon-project-clean-architecture.md)); chèn lifeline `:Meeting Summary Repository` (`IMeetingSummaryRepository`) ở UC-58 → UC-61: `findByMeeting`, `findWithPeople`, `findNextQueued`, `claim`, `storeDraft`, `markFailed`, `saveEdit`, `markReviewed`. Message tới `:Database` giữ nguyên nội dung; luồng, nhánh `MSG`/`BR` và actor không đổi.

## Xuất ảnh

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/transcription-voice-id
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/transcription-voice-id/UC*.txt
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
