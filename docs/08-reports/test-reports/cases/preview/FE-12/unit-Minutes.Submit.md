# Minutes.Submit — Unit (FE-12, MIN)

Description: Minutes approval workflow: submit, approve (issue), return, correct.

Precondition: Minutes in the needed status; approver and secretary actors.

## MinutesService.SubmitAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:206`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Ended meeting; actor is the current Secretary.<br>2. Draft revision with UpdatedAt T; the caller read it at T. | minutesId: the Draft revision; actor: the current Secretary; loadedAt: T. | Return: Status WaitingApproval with SubmittedBy = the Secretary and SubmittedAt = now; 200 MSG424.<br>Exception: N/A<br>Log Message: Saved: status, submitter and times on the revision; notification "Biên bản chờ duyệt" to the current Chair ("Thư ký đã gửi biên bản cuộc họp "…" để Chủ toạ duyệt."); realtime minutes-changed push to the meeting's readers. | Normal | UC-72 · BR-135, BR-136 · MSG424 |
| UTC002 | 1. Ended meeting; actor is the current Chair.<br>2. Draft revision with UpdatedAt T. | minutesId: the Draft revision; actor: the current Chair; loadedAt: T. | Return: Failure NotSecretary; 403 MSG425; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72 · BR-135, BR-136 · MSG425 |
| UTC003 | 1. Meeting still InProgress; actor is the current Secretary.<br>2. Draft revision with UpdatedAt T. | minutesId: the Draft revision; actor: the current Secretary; loadedAt: T. | Return: Failure MeetingNotEnded; 409 MSG426; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72 · BR-135, BR-136 · MSG426 |
| UTC004 | 1. Ended meeting; actor is the current Secretary.<br>2. Draft revision read at T, then saved by someone else. | minutesId: the Draft revision; actor: the current Secretary; loadedAt: T. | Return: Failure Stale; 409 MSG401; status unchanged.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-72 · UC-71 · MSG401 |
