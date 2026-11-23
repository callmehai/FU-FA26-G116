# Meeting.GetSubmissionReadiness — Unit (FE-04, MEET)

Description: Submission readiness check: ownership, capability, status and stored-information completeness.

Precondition: Meeting rows seeded; actor with known capability and relationship.

## MeetingService.GetSubmissionReadinessAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:375`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting is Draft created by the actor.<br>2. Actor holds MeetingCreator; stored information is complete. | id: meeting; actorId: organizer. | Return: Outcome Success; status Draft; errors empty; Ready true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-18 · BR-33 |
| UTC002 | Draft meeting with an empty stored title. | id: meeting; actorId: organizer. | Return: Outcome Success; errors { title }; Ready false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-33 |
| UTC003 | Actor is the Chair or approver, not the creator. | actorId: Chair. | Return: Outcome Forbidden with the meeting status; MSG85.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-29 · MSG85 |
| UTC004 | Meeting is WaitingApproval. | actorId: organizer. | Return: Outcome StatusConflict with status WaitingApproval; MSG41.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-33 · MSG41 |
