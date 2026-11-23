# Summary.Request — Unit (FE-10, TRANS)

Description: Requesting an AI summary draft: meeting status, existing draft and queueing.

Precondition: Meeting with a transcript.

## SummaryService.RequestAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/SummaryService.cs:81`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting is Ended; actor is its current chair.<br>2. The AI server is configured for the meeting sensitivity; no draft exists yet. | meetingId: an Ended meeting; actorId: its current chair. | Return: Success with the fresh state (MSG384 "Đã gửi yêu cầu. Dự thảo sẽ tự hiện khi AI viết xong."); a Queued draft row is stored with the requester and time.<br>Exception: N/A<br>Log Message: Saved: Queued draft row with requester and request time. | Normal | UC-47 · BR-374 · BR-375 · MSG384 · Deviation: drafts are Ended-only since 518e9ce2 (20/11); PRE updated from InProgress |
| UTC002 | Meeting is Ended; actor takes part but is neither chair nor secretary. | meetingId: an Ended meeting; actorId: an ordinary member. | Return: Failure Forbidden (MSG385 "Chỉ Chủ toạ hoặc Thư ký được tạo, sửa và duyệt dự thảo."); nothing is queued.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · BR-374 · MSG385 · Deviation: drafts are Ended-only since 518e9ce2 (20/11); PRE updated from InProgress |
| UTC003 | Meeting is still Draft; actor is its current chair. | meetingId: a Draft meeting; actorId: its current chair. | Return: Failure WrongMeetingStatus (MSG386 "Chỉ tạo dự thảo khi cuộc họp đã kết thúc."); nothing is queued.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · BR-375 · MSG386 · Deviation: drafts are Ended-only since 518e9ce2 (20/11); PRE updated from InProgress |
| UTC004 | 1. Meeting is Ended; actor is its current chair.<br>2. A draft is already Queued. | meetingId: a meeting with a Queued draft; actorId: its current chair. | Return: Failure Busy (MSG387 "AI đang viết dự thảo, vui lòng chờ."); the waiting draft is untouched.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · MSG387 · Deviation: drafts are Ended-only since 518e9ce2 (20/11); PRE updated from InProgress |
| UTC005 | 1. Meeting is Ended; actor is its current chair.<br>2. No AI server is configured. | meetingId: an Ended meeting; actorId: its current chair. | Return: Failure AiUnavailable (MSG394 "Máy AI chưa sẵn sàng. Liên hệ quản trị hệ thống."); nothing is queued, as the request would only fail later.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-47 · BR-381 · MSG394 · Deviation: drafts are Ended-only since 518e9ce2 (20/11); PRE updated from InProgress |
