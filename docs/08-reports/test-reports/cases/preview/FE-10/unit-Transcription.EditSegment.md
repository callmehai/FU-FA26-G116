# Transcription.EditSegment — Unit (FE-10, TRANS)

Description: Editing a segment and assigning speakers.

Precondition: Transcript with segments; editor actor.

## TranscriptionService.EditSegmentAsync

Layer: Application · `backend/TrustCabinet.Application/Transcription/TranscriptionService.cs:154`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Transcript segment exists; actor is the current secretary of the meeting.<br>2. New text differs from the stored text. | segmentId: an existing segment; actorId: the current secretary; text: corrected sentence. | Return: Success with the segment id, the trimmed text and the edit time (MSG302 "Đã lưu đoạn gỡ băng.").<br>Exception: N/A<br>Log Message: Saved: segment text, editor and edit time; audit row records the corrected segment. | Normal | UC-46a · BR-359 · MSG302 |
| UTC002 | Transcript segment exists; actor is the current secretary. | segmentId: an existing segment; actorId: the current secretary; text: blank. | Return: Failure Invalid (MSG303 "Nội dung đoạn gỡ băng không hợp lệ."); errors[text] = ["Nội dung đoạn phải có từ 1 đến 8000 ký tự."]; nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46a · BR-359 · MSG303 |
| UTC003 | Transcript segment exists; actor is the current secretary. | segmentId: an existing segment; actorId: the current secretary; text: exactly 8000 characters. | Return: Success (MSG302); text at the limit is accepted.<br>Exception: N/A<br>Log Message: Saved: segment text, editor and edit time; audit row records the corrected segment. | Boundary | UC-46a · BR-359 · MSG302 |
| UTC004 | Transcript segment exists; actor takes part in the meeting but is not the secretary. | segmentId: an existing segment; actorId: a member who is not the secretary; text: corrected sentence. | Return: Failure Forbidden (MSG295 "Chỉ Thư ký cuộc họp được quản lý bản ghi."); nothing is saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-46a · BR-359 · MSG295 |
