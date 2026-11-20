# VoiceId.AddSample — Unit (FE-10, TRANS)

Description: Voice samples: add under consent, delete, read audio.

Precondition: User with consent; speech worker replaced.

## VoiceIdService.AddSampleAsync

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceIdService.cs:106`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. User consented to notice "1.3" and keeps two own samples.<br>2. Audio holds 20 seconds of one clear voice. | actorId: a consented user; audio: a clear voice recording; contentType: audio/mpeg. | Return: Success with the new sample (MSG307 "Đã lưu giọng mẫu."); only the encrypted vector is kept for matching, the recording under a consent that says so.<br>Exception: N/A<br>Log Message: Saved: encrypted sample with the model and speech length; audit row records the new sample. | Normal | UC-81c · BR-363 · BR-369 · MSG307 · Pending: needs the live speech worker to embed a clear voice sample. |
| UTC002 | User never consented. | actorId: a user without consent; audio: a clear voice recording; contentType: audio/mpeg. | Return: Failure NoConsent (MSG308 "Cần đồng ý nhận diện giọng nói trước khi ghi giọng mẫu."); the audio never goes to the worker.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81c · BR-361 · MSG308 |
| UTC003 | User withdraws consent after the first check, before anything is kept. | actorId: a user who withdraws mid-way; audio: a clear voice recording; contentType: audio/mpeg. | Return: Failure NoConsent (MSG308); nothing is kept after the person said no.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81c · BR-365 · MSG308 · Pending: needs a real concurrent consent withdrawal between the two reads. |
| UTC004 | User consented and already keeps 5 own samples. | actorId: a user with 5 own samples; audio: a clear voice recording; contentType: audio/mpeg. | Return: Failure LimitReached (MSG310 "Đã đủ 5 giọng mẫu. Xoá bớt rồi ghi lại."); nothing is kept.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81c · BR-363 · MSG310 |
| UTC005 | User consented; audio holds 3 seconds of speech. | actorId: a consented user; audio: a 3-second recording; contentType: audio/mpeg. | Return: Failure Rejected (MSG309); errors[file] = ["Giọng mẫu quá ngắn — cần ít nhất 6 giây có tiếng nói. Hãy đọc hết đoạn văn mẫu."]; nothing is kept.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81c · BR-363 · MSG309 · Pending: needs the live speech worker to judge speech length. |
| UTC006 | User consented; the speech worker is down. | actorId: a consented user; audio: a clear voice recording; contentType: audio/mpeg. | Return: Failure EngineUnavailable (MSG312 "Máy chủ nhận dạng giọng nói chưa sẵn sàng. Vui lòng thử lại sau."); nothing is kept.<br>Exception: N/A<br>Log Message: Warning logged with the user id. | Abnormal | UC-81c · MSG312 · Pending: needs a live speech worker in a down state. |
| UTC007 | User consented and keeps 4 own samples. | actorId: a user with 4 own samples; audio: a clear voice recording; contentType: audio/mpeg. | Return: Success (MSG307); the 5th sample is stored, a 6th would be refused.<br>Exception: N/A<br>Log Message: Saved: encrypted sample; audit row records the new sample. | Boundary | UC-81c · BR-363 · MSG307 · Pending: needs the live speech worker to embed the fifth sample. |
