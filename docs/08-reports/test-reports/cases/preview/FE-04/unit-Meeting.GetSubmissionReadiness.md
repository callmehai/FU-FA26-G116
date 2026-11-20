# Meeting.GetSubmissionReadiness — Unit (FE-04, MEET)

Description: Submission readiness check: ownership, capability, status and stored-information completeness.

Precondition: Meeting rows seeded; actor with known capability and relationship.

## MeetingService.GetSubmissionReadinessAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:373`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting is Draft created by the actor.<br>2. Actor holds MeetingCreator; stored information is complete. | id: meeting; actorId: organizer. | Return: Outcome Success; status Draft; errors empty; Ready true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-18 · BR-33 |
| UTC002 | Draft meeting with an empty stored title. | id: meeting; actorId: organizer. | Return: Outcome Success; errors { title }; Ready false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-33 |
| UTC003 | Rejected meeting created by the actor with complete information. | actorId: organizer. | Return: Outcome Success; status Rejected; Ready true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-18 · BR-33 |
| UTC004 | No meeting exists with the id. | id: unknown. | Return: Outcome NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-33 |
| UTC005 | Meeting exists but the actor has no relationship to it. | actorId: stranger. | Return: Outcome NotFound; existence is hidden.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-29 |
| UTC006 | Actor is the Chair or approver, not the creator. | actorId: Chair. | Return: Outcome Forbidden with the meeting status; MSG85.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-29 · MSG85 |
| UTC007 | Actor created the meeting but lost MeetingCreator. | actorId: organizer without capability. | Return: Outcome Forbidden; MSG85.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-29 · MSG85 |
| UTC008 | Meeting is WaitingApproval. | actorId: organizer. | Return: Outcome StatusConflict with status WaitingApproval; MSG41.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-18 · BR-33 · MSG41 |
| UTC009 | Meeting is Approved. | actorId: organizer. | Return: Outcome StatusConflict; MSG41.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-18 · BR-33 · MSG41 |
| UTC010 | The store fails while reading. | actorId: organizer. | Return: Outcome ReadFailed; MSG136.<br>Exception: PersistenceException is caught inside the service.<br>Log Message: Error logged with the meeting id. | Abnormal | UC-18 · MSG136 |
