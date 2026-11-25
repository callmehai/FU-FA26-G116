# Sequence diagram — việc hệ thống tự chạy

Ba sơ đồ có actor **System**: không UC nào mô tả vì không có người dùng bấm (quy tắc "actor là người", [docs/08-reports/README.md](../../08-reports/README.md)).
Chúng là hình minh hoạ thuật toán ở **Report 4 phần IV "Other Design Specifications"**, không nằm trong bộ sequence diagram theo UC
và không có trang trong `TrustCabinet_Sequence_Diagrams.drawio` (chuyển ra ngày 25/11/2026, note 60 §3.5 mục 5).

| Nguồn `.txt` | Ảnh | Report 4 | Code |
|---|---|---|---|
| [SYS_ProcessTranscriptionQueue.txt](SYS_ProcessTranscriptionQueue.txt) | [SYS_ProcessTranscriptionQueue.png](SYS_ProcessTranscriptionQueue.png) | IV.1 Hàng đợi gỡ băng | `TranscriptionWorker`, `TranscriptionService.ProcessNextAsync` |
| [SYS_BuildCombinedRecording.txt](SYS_BuildCombinedRecording.txt) | [SYS_BuildCombinedRecording.png](SYS_BuildCombinedRecording.png) | IV.5 Bản ghi gộp | `CombinedRecordingWorker`, `CombinedRecordingService` |
| [SYS_AutoEndForgottenMeeting.txt](SYS_AutoEndForgottenMeeting.txt) | [SYS_AutoEndForgottenMeeting.png](SYS_AutoEndForgottenMeeting.png) | IV.6 Tự huỷ, tự kết thúc cuộc họp | `MeetingOverdueWorker`, `MeetingOverdueService` |

Tên cũ: `transcription-voice-id/UC54c_ProcessTranscriptionQueue.txt` (trang "UC-54c - … Process Transcription Queue (System)"),
`meeting-recording/SYS_BuildCombinedRecording.txt`, `meeting-management/SYS_AutoEndForgottenMeeting.txt` (hai trang này từng ghi
"DRAFT awaiting approval", đã chốt khi chuyển).

## Xuất ảnh

Ảnh là bản draw.io (chuẩn agenda), xuất không ghép vào file gộp rồi chuyển vào thư mục này:

```bash
python scripts/txt_to_sequence_drawio_png.py --no-splice --no-originals --no-compress \
  docs/06-sequencediagram/system-processes/SYS_ProcessTranscriptionQueue.txt
mv docs/06-sequencediagram/drawio-png/sys_processtranscriptionqueue.drawio.png \
  docs/06-sequencediagram/system-processes/SYS_ProcessTranscriptionQueue.png
```

Sửa `.txt` thì xuất lại ảnh và thay hình tương ứng trong Report 4 (DOCX), rồi chạy `scripts/check_docx.py`.
