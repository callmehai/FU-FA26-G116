# Summary.Review — Unit (FE-10, TRANS)

Description: Editing and reviewing a summary draft.

Precondition: Summary draft exists.

## SummaryService.ReviewAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/SummaryService.cs:161`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A Ready draft exists; actor is the current secretary. | meetingId: a meeting with a Ready draft; actorId: its current secretary. | Return: Success with the fresh state (MSG391 "Đã duyệt dự thảo."); the draft is marked reviewed with reviewer and time, and other readers may see it from now on.<br>Exception: N/A<br>Log Message: Saved: reviewer and review time on the draft. | Normal | UC-50 · BR-374 · MSG391 |
| UTC002 | No draft exists yet; actor is the current secretary. | meetingId: a meeting without a draft; actorId: its current secretary. | Return: Failure NotDrafted (MSG390); nothing is marked.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-50 · MSG390 |
| UTC003 | A draft is Queued on the worker; actor is the current secretary. | meetingId: a meeting with a Queued draft; actorId: its current secretary. | Return: Failure Busy (MSG387); the mark would end up on a text nobody has read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-50 · MSG387 |
| UTC004 | A Ready draft exists; actor takes part but is neither chair nor secretary. | meetingId: a meeting with a Ready draft; actorId: an ordinary member. | Return: Failure Forbidden (MSG385); nothing is marked.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-50 · BR-374 · MSG385 |
