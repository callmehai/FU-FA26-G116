# Meeting.CheckUpdate — Unit (FE-04, MEET)

Description: Update pre-check: editable statuses and organizer ownership before editing.

Precondition: Meeting in a known status; organizer actor.

## MeetingService.CheckUpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:182`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting is Draft and owned by the actor. | id: meeting; actorId: organizer. | Return: Success with the meeting details.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · BR-27 · BR-28 |
| UTC002 | Meeting is WaitingApproval. | status: WaitingApproval. | Return: Failure NotEditable; MSG33.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-34 · MSG33 |
| UTC003 | Meeting is owned by someone else or does not exist. | actorId: not the creator. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-29 |
