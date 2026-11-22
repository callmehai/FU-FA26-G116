# Minutes.Return — Unit (FE-12, MIN)

Description: Minutes approval workflow: submit, approve (issue), return, correct.

Precondition: Minutes in the needed status; approver and secretary actors.

## MinutesService.ReturnAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:253`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Ended meeting; actor is the current Chair.<br>2. Revision WaitingApproval with UpdatedAt T; the caller read it at T. | minutesId: the waiting revision; actor: the current Chair; reason: "Bổ sung kết luận của mục 2."; loadedAt: T. | Return: Status Rejected with the reason, ReturnedBy = the Chair and ReturnedAt = now; 200 MSG430.<br>Exception: N/A<br>Log Message: Saved: status, reason, returner and times; notification "Biên bản bị trả lại" to the current Secretary ("Chủ toạ đã trả lại biên bản cuộc họp "…" Xem lý do trên tab Biên bản."); realtime minutes-changed push to the meeting's readers. | Normal | UC-65 · BR-406 · BR-407 · MSG430 · Deviation: success also emits a realtime minutes-changed push (working-tree change). |
| UTC002 | 1. Ended meeting; actor is the current Secretary.<br>2. Revision WaitingApproval with UpdatedAt T. | minutesId: the waiting revision; actor: the current Secretary; reason: "Bổ sung kết luận."; loadedAt: T. | Return: Failure NotChair; 403 MSG428; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-65 · BR-406 · MSG428 |
| UTC003 | 1. Ended meeting; actor is the current Chair.<br>2. Revision still Draft. | minutesId: the Draft revision; actor: the current Chair; reason: "Bổ sung kết luận."; loadedAt: T. | Return: Failure NotWaiting; 409 MSG429; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-65 · MSG429 |
| UTC004 | 1. Ended meeting; actor is the current Chair.<br>2. Revision WaitingApproval with UpdatedAt T. | minutesId: the waiting revision; actor: the current Chair; reason: "   " (only spaces); loadedAt: T. | Return: Failure ReasonInvalid; 400 MSG431; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-65 · BR-407 · MSG431 |
| UTC005 | 1. Ended meeting; actor is the current Chair.<br>2. Revision WaitingApproval with UpdatedAt T. | minutesId: the waiting revision; actor: the current Chair; reason: 2001 characters; loadedAt: T. | Return: Failure ReasonInvalid; 400 MSG431; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-65 · BR-407 · MSG431 |
| UTC006 | 1. Ended meeting; actor is the current Chair.<br>2. WaitingApproval revision read at T, then saved by someone else. | minutesId: the waiting revision; actor: the current Chair; reason: "Bổ sung kết luận."; loadedAt: T. | Return: Failure Stale; 409 MSG401; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-65 · BR-389 · MSG401 |
