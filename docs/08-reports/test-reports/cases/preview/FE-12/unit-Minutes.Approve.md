# Minutes.Approve — Unit (FE-12, MIN)

Description: Minutes approval workflow: submit, approve (issue), return, correct.

Precondition: Minutes in the needed status; approver and secretary actors.

## MinutesService.ApproveAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:211`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Ended meeting; actor is the current Chair.<br>2. Revision WaitingApproval with UpdatedAt T; the caller read it at T.<br>3. Meeting participants and organizer accounts are active. | minutesId: the waiting revision; actor: the current Chair; loadedAt: T. | Return: Status Issued with ApprovedBy = the Chair and ApprovedAt = IssuedAt = now; 200 MSG427.<br>Exception: N/A<br>Log Message: Saved: status, approver and issue times; audit row for the issue (actor = the Chair, details carry meeting, revision and replaced-revision ids); notification "Biên bản đã ban hành" to every reader except the Chair ("Biên bản cuộc họp "…" đã được ban hành."); issue emails queued for the same readers. | Normal | UC-64 · BR-408 · BR-411 · BR-446 · MSG427 |
| UTC002 | 1. Ended meeting; actor is the current Chair.<br>2. WaitingApproval correction whose SupersedesId points at revision 1 (Issued); the caller read it at T. | minutesId: the waiting correction; actor: the current Chair; loadedAt: T. | Return: Status Issued; revision 1 becomes Superseded and stays readable; 200 MSG427 with the correction message.<br>Exception: N/A<br>Log Message: Saved: the correction Issued, the earlier revision Superseded; audit row; notifications and emails carry "Bản đính chính biên bản cuộc họp "…" đã được ban hành." | Normal | UC-64 · UC-83 · BR-409 · MSG427 |
| UTC003 | 1. Revision WaitingApproval.<br>2. Actor reads no meeting. | minutesId: the waiting revision; actor: the stranger; loadedAt: T. | Return: Failure NotFound; 404 with no message code; status unchanged and nobody notified.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-64 |
| UTC004 | 1. Ended meeting; actor is the current Secretary.<br>2. Revision WaitingApproval with UpdatedAt T. | minutesId: the waiting revision; actor: the current Secretary; loadedAt: T. | Return: Failure NotChair; 403 MSG428; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-64 · BR-406 · MSG428 |
| UTC005 | 1. Ended meeting; actor is the current Chair.<br>2. Revision still Draft. | minutesId: the Draft revision; actor: the current Chair; loadedAt: T. | Return: Failure NotWaiting; 409 MSG429; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-64 · MSG429 |
| UTC006 | 1. Ended meeting; actor is the current Chair.<br>2. WaitingApproval revision read at T, then saved by someone else. | minutesId: the waiting revision; actor: the current Chair; loadedAt: T. | Return: Failure Stale; 409 MSG401; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-64 · BR-389 · MSG401 |
| UTC007 | 1. Ended meeting; actor is the current Chair; WaitingApproval revision read at T.<br>2. The database fails while saving the transition. | minutesId: the waiting revision; actor: the current Chair; loadedAt: T. | Return: Failure SaveFailed; 503 MSG404; status unchanged and nobody notified.<br>Exception: N/A<br>Log Message: Error logged with the action and minutes id. | Abnormal | UC-64 · MSG404 |
