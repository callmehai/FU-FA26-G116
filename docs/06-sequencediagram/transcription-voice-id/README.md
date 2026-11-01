# Sequence Diagram — Gỡ băng và nhận diện người nói (FE-10)

Thư mục chứa source SequenceDiagram.org (`.txt`) và ảnh PNG cùng tên cho các UC thuộc FE-10
(UC-45a → UC-45c, UC-46a → UC-46d, UC-81a → UC-81f), đã đối chiếu với code ngày 30/10/2026 và vẽ theo đặc tả **đã sửa** ở
[plan 19 §12](../../notes/ban-giao-ba-final-review/19-plan-fe-09-fe-10-ghi-am-go-bang-voice-id.md#12-review-lượt-13-30102026--việc-phải-sửa).
UC-47 → UC-51 (tóm tắt) chưa có code nên không có sơ đồ.

| Use Case | Source |
|---|---|
| UC-45a View Recording Transcript | [UC45a_ViewRecordingTranscript.txt](UC45a_ViewRecordingTranscript.txt) |
| UC-45b Export Meeting Transcript | [UC45b_ExportMeetingTranscript.txt](UC45b_ExportMeetingTranscript.txt) |
| UC-45c Transcribe Meeting Recording — Thư ký yêu cầu | [UC45c_TranscribeMeetingRecording.txt](UC45c_TranscribeMeetingRecording.txt) |
| UC-45c Transcribe Meeting Recording — hệ thống xử lý hàng chờ | [UC45c_ProcessTranscriptionQueue.txt](UC45c_ProcessTranscriptionQueue.txt) |
| UC-46a Edit Transcript Text | [UC46a_EditTranscriptText.txt](UC46a_EditTranscriptText.txt) |
| UC-46b Assign Speaker | [UC46b_AssignSpeaker.txt](UC46b_AssignSpeaker.txt) |
| UC-46c Exclude or Restore a Sentence | [UC46c_ExcludeOrRestoreSentence.txt](UC46c_ExcludeOrRestoreSentence.txt) |
| UC-46d Delete a Sentence | [UC46d_DeleteSentence.txt](UC46d_DeleteSentence.txt) |
| UC-81a View My Voice ID | [UC81a_ViewMyVoiceId.txt](UC81a_ViewMyVoiceId.txt) |
| UC-81b Give Voice ID Consent | [UC81b_GiveVoiceIdConsent.txt](UC81b_GiveVoiceIdConsent.txt) |
| UC-81c Record Voice Sample | [UC81c_RecordVoiceSample.txt](UC81c_RecordVoiceSample.txt) |
| UC-81d Play Voice Sample | [UC81d_PlayVoiceSample.txt](UC81d_PlayVoiceSample.txt) |
| UC-81e Delete Voice Sample | [UC81e_DeleteVoiceSample.txt](UC81e_DeleteVoiceSample.txt) |
| UC-81f Withdraw Voice ID Consent | [UC81f_WithdrawVoiceIdConsent.txt](UC81f_WithdrawVoiceIdConsent.txt) |

Quy ước riêng của nhóm này:

- Lifeline theo code: `:Transcript Controller` / `:Voice ID Controller` là `TranscriptEndpoints` / `VoiceIdEndpoints`;
  `:Transcription Service`, `:Voice ID Service` là `ITranscriptionService`, `IVoiceIdService`; tên thao tác Controller →
  Service lấy đúng tên interface, bỏ `Async` (`get`, `retry`, `export`, `editSegment`, `assignSpeaker`, `processNext`,
  `grant`, `withdraw`, `addSample`, `deleteSample`, `sampleAudio`).
- `:Speech Recognition Service` là dịch vụ nhận dạng giọng nói chạy trong hệ thống (ADR-0026, BR-369): code là
  `ISpeechEngine` (gỡ băng) và `IVoiceEmbedder` (giọng mẫu), cùng một worker. `:Speaker Matcher` là lớp tĩnh `SpeakerMatcher`.
- **UC-45c có hai sơ đồ** (quyết định D7): Thư ký yêu cầu chuyển chữ (lần đầu, sau lỗi, hoặc làm lại bản đã xong), và
  một sơ đồ hệ thống — `TranscriptionWorker` lấy bản chờ lâu nhất trong hàng, chuyển chữ, gợi ý người nói, đánh dấu
  `Completed` hoặc `Failed` kèm lý do (EX-3). Đây là ngoại lệ của quy ước "một request Actor", như UC-04.
- UC-45b và UC-46a mỗi UC phủ cả bản ghi phòng (`:Transcript Controller`) và bản ghi theo micro (`:Speech Clip Controller`),
  vẽ bằng `alt` ở đầu sơ đồ.
- UC-46b AF-1 (gán cả nhóm "cùng giọng") là một vòng `loop`: màn hình gọi `assignSpeaker` cho từng giọng trong nhóm.
- Gợi ý người nói chỉ Thư ký thấy (BR-366) và không bao giờ tự gán tên; rút đồng ý xoá mọi giọng mẫu và mọi gợi ý trong một
  giao dịch, tên Thư ký đã gán giữ nguyên (BR-365). Xoá một giọng mẫu (UC-81e) **không** tính lại hồ sơ giọng: không có hồ sơ
  gộp, mỗi mẫu một đặc trưng, gợi ý lấy mẫu giống nhất.
- Người dùng không thuộc thành phần hiện tại nhận câu trả lời "không tìm thấy" (không có mã MSG); sơ đồ vẽ nhánh này không
  có số EX. Voice ID chỉ đọc/ghi của chính người gọi: mẫu của người khác trả lời như không tồn tại.
- Nhánh `alt` **không có số EX** là hành vi code có nhưng đặc tả UC chưa ghi: người không còn trong thành phần (trả lời "không
  tìm thấy"); UC-45c người không phải Thư ký (MSG295); UC-46b, UC-46c lỗi lưu (MSG304). BA bổ sung EX vào đặc tả hoặc bỏ nhánh khi duyệt.
- `ref` sang UC khác viết thành self-message `ref UC-…` để file draw.io cũng hiện.

## Xuất ảnh

```bash
node scripts/export-diagrams.js docs/06-sequencediagram/transcription-voice-id
python scripts/txt_to_sequence_drawio_png.py docs/06-sequencediagram/transcription-voice-id/UC*.txt
```

Giữ `.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio` và ảnh trong `drawio-png/` đồng bộ.
