# Summary.Request — Unit (FE-10, TRANS)

Description: Requesting an AI summary draft: meeting status, existing draft and queueing.

Precondition: Meeting with a transcript.

## SummaryService.RequestAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/SummaryService.cs:64`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting is InProgress; actor is its current chair.<br>2. The AI server is configured for the meeting sensitivity; no draft exists yet. | meetingId: an InProgress meeting; actorId: its current chair. | Return: Success with the fresh state (MSG384 "Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong."); a Queued draft row is stored with the requester and time.<br>Exception: N/A<br>Log Message: Saved: Queued draft row with requester and request time. | Normal | UC-47 · BR-374 · BR-375 · MSG384 |
| UTC002 | 1. Meeting is Ended; actor is its current secretary.<br>2. A Failed draft from an earlier request exists. | meetingId: an Ended meeting with a Failed draft; actorId: its current secretary. | Return: Success (MSG384); the draft is Queued again and the old text stays readable until the new one lands.<br>Exception: N/A<br>Log Message: Saved: draft row back to Queued with the new requester and time. | Normal | UC-47 · BR-376 · MSG384 |
| UTC003 | Meeting is InProgress; actor takes part but is neither chair nor secretary. | meetingId: an InProgress meeting; actorId: an ordinary member. | Return: Failure Forbidden (MSG385 "Chỉ Chủ toạ hoặc Thư ký được tạo, sửa và duyệt dự thảo."); nothing is queued.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · BR-374 · MSG385 |
| UTC004 | Meeting is still Draft; actor is its current chair. | meetingId: a Draft meeting; actorId: its current chair. | Return: Failure WrongMeetingStatus (MSG386 "Chỉ tạo dự thảo khi cuộc họp đang diễn ra hoặc đã kết thúc."); nothing is queued.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · BR-375 · MSG386 |
| UTC005 | 1. Meeting is InProgress; actor is its current chair.<br>2. A draft is already Queued. | meetingId: a meeting with a Queued draft; actorId: its current chair. | Return: Failure Busy (MSG387 "AI đang viết dự thảo, vui lòng chờ."); the waiting draft is untouched.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · MSG387 |
| UTC006 | 1. Meeting is InProgress; actor is its current chair.<br>2. No AI server is configured. | meetingId: an InProgress meeting; actorId: its current chair. | Return: Failure AiUnavailable (MSG394 "Máy AI chưa sẵn sàng. Liên hệ quản trị hệ thống."); nothing is queued, as the request would only fail later.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · BR-381 · MSG394 |
| UTC007 | Actor may not read the meeting. | meetingId: a meeting the actor may not read; actorId: an outsider. | Return: Failure NotFound; answered as if the meeting does not exist.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 |
| UTC008 | Actor account is deactivated. | meetingId: an InProgress meeting; actorId: a deactivated account. | Return: Failure Inactive; nothing is queued.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 |
| UTC009 | 1. Meeting is InProgress; actor is its current chair.<br>2. Two requests arrive at the same time. | meetingId: an InProgress meeting without a draft; actorId: its current chair, asking twice at once. | Return: One request queues the draft (MSG384); the other reads Failure Busy (MSG387).<br>Exception: N/A<br>Log Message: Saved: one Queued draft row; warning logged with the meeting id for the concurrent request. | Abnormal | UC-47 · MSG387 |
| UTC010 | 1. Meeting is InProgress; actor is its current chair.<br>2. The store fails while saving the request. | meetingId: an InProgress meeting; actorId: its current chair. | Return: Failure SaveFailed (MSG393 "Không lưu được dự thảo. Vui lòng thử lại."); nothing is queued.<br>Exception: N/A<br>Log Message: Error logged with the meeting id. | Abnormal | UC-47 · MSG393 |
