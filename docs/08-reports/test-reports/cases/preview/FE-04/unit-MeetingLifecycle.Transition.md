# MeetingLifecycle.Transition — Unit (FE-04, MEET)

Description: Meeting start and end: chair-only transitions with roster checks and fan-out on end.

Precondition: Published meeting with one chair and at least one secretary for Start; InProgress meeting for End.

## MeetingLifecycleService.TransitionAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingLifecycleService.cs:33`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting is Published with exactly one active Chair (the actor) and one active Secretary.<br>2. Two other Members are invited. | command: Start; actor: the Chair. | Return: Success(InProgress); ActualStartedAt = now; MSG110.<br>Exception: N/A<br>Log Message: Saved: status InProgress, history row Start (Published to InProgress) with the Chair as actor; after commit "meeting started" notification queued for the two Members and the Secretary but not for the Chair. | Normal | UC-21 · BR-127 · BR-130 · BR-131 · BR-164 · MSG110 |
| UTC002 | 1. Meeting is InProgress; actor is the active Chair.<br>2. Meeting mode is Online. | command: End; actor: the Chair. | Return: Success(Ended); ActualEndedAt = now; MSG111.<br>Exception: N/A<br>Log Message: Saved: status Ended, history row End (InProgress to Ended); draft votes deleted; open speaking requests closed; after commit: agenda event "MeetingEnded" published, draft-deletion events sent to Chair and Secretary only, LiveKit room closed. | Normal | UC-22 · BR-130 · BR-132 · BR-143 · MSG111 |
| UTC003 | Meeting is InProgress, mode InPerson. | command: End; actor: the Chair. | Return: Success(Ended); MSG111.<br>Exception: N/A<br>Log Message: Same saved changes; no room closure because an in-person meeting has no online room. | Boundary | UC-22 · BR-130 · BR-132 · MSG111 |
| UTC004 | Meeting is InProgress, mode Hybrid. | command: End; actor: the Chair. | Return: Success(Ended); MSG111.<br>Exception: N/A<br>Log Message: Room closure is requested after commit. | Boundary | UC-22 · BR-130 · BR-132 · BR-143 · MSG111 |
| UTC005 | Command carries an approval action. | command: Approve; actor: Chair. | Return: Failure InvalidRequest; MSG80; nothing changed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · MSG80 |
| UTC006 | Start command carries a reason. | command: Start with Reason "x". | Return: Failure InvalidRequest; MSG80.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · MSG80 |
| UTC007 | Start command carries an approver id or an organizer id. | command: Start with AssignedApproverId set. | Return: Failure InvalidRequest; MSG80.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · MSG80 |
| UTC008 | Meeting does not exist. | meetingId: unknown. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-127 · BR-130 · BR-131 |
| UTC009 | Actor has no read access to the meeting. | actor: stranger. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-29 |
| UTC010 | Actor reads the meeting as Secretary. | command: Start; actor: Secretary. | Return: Failure Forbidden (403); MSG112.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-130 · MSG112 |
| UTC011 | Actor is the Chair on the roster but the Chair account is deactivated. | command: End; actor: deactivated Chair. | Return: Failure Forbidden (403); MSG114.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-22 · BR-130 · MSG114 |
| UTC012 | Actor is the meeting creator, not on the roster as Chair. | command: Start; actor: creator. | Return: Failure Forbidden; MSG112.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-130 · MSG112 |
| UTC013 | Meeting is Approved (not yet Published). | command: Start; actor: Chair. | Return: Failure StatusConflict (409); MSG112; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-131 · MSG112 |
| UTC014 | Meeting is already InProgress. | command: Start; actor: Chair. | Return: Failure StatusConflict; MSG112.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-131 · MSG112 |
| UTC015 | Published meeting with no active Secretary. | command: Start; actor: Chair. | Return: Failure StatusConflict; MSG112.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-127 · MSG112 |
| UTC016 | Published meeting with two active Chairs (actor is one of them). | command: Start; actor: Chair. | Return: Failure StatusConflict; MSG112.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-127 · MSG112 |
| UTC017 | Published meeting where the only Secretary is deactivated. | command: Start; actor: Chair. | Return: Failure StatusConflict; MSG112.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-21 · BR-127 · MSG112 |
| UTC018 | Meeting is Published. | command: End; actor: Chair. | Return: Failure StatusConflict; MSG114.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-22 · BR-132 · MSG114 |
| UTC019 | Meeting is InProgress but has no Secretary. | command: End; actor: Chair. | Return: Success(Ended); the roster is not checked when ending.<br>Exception: N/A<br>Log Message: Same saved changes as a normal end. | Boundary | UC-22 · BR-130 · BR-132 · MSG111 |
| UTC020 | The store fails while saving the start. | command: Start; actor: Chair. | Return: Failure SaveFailed (500); MSG115; no notification is sent.<br>Exception: N/A<br>Log Message: Error logged with the action and meeting id; transaction rolled back. | Abnormal | UC-21 · MSG115 |
| UTC021 | The store fails while saving the end. | command: End; actor: Chair. | Return: Failure SaveFailed; MSG116; no post-commit effect runs.<br>Exception: N/A<br>Log Message: Error logged; transaction rolled back. | Abnormal | UC-22 · MSG116 |
| UTC022 | Ending finds the agenda in a state that cannot be closed. | command: End; actor: Chair. | Return: Failure SaveFailed; MSG116.<br>Exception: N/A<br>Log Message: Error logged; transaction rolled back. | Abnormal | UC-22 · MSG116 |
| UTC023 | Meeting ended but the agenda event publication fails. | command: End; actor: Chair. | Return: Success(Ended); MSG111; the failure does not change the result.<br>Exception: N/A<br>Log Message: Warning logged for the failed post-commit effect; the other effects still run. | Boundary | UC-22 · BR-130 · BR-132 · MSG111 |
| UTC024 | Start commits but the notification insert fails. | command: Start; actor: Chair. | Return: Success(InProgress); MSG110.<br>Exception: N/A<br>Log Message: Warning logged; the meeting stays InProgress. | Boundary | UC-21 · BR-127 · BR-130 · BR-131 · BR-164 · MSG110 |
| UTC025 | The caller cancels the request. | cancellation token cancelled. | Return: No result; the cancellation is rethrown.<br>Exception: OperationCanceledException is propagated.<br>Log Message: N/A | Abnormal | UC-21 · BR-127 · BR-130 · BR-131 |
