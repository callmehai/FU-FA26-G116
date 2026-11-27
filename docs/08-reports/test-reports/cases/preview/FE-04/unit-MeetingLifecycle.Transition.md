# MeetingLifecycle.Transition — Unit (FE-04, MEET)

Description: Meeting start and end: chair-or-secretary start and chair-only end with roster checks and fan-out on end.

Precondition: Published meeting with one chair and at least one secretary for Start; InProgress meeting for End.

## MeetingLifecycleService.TransitionAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingLifecycleService.cs:34`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Meeting is Published with exactly one active Chair (the actor) and one active Secretary.<br>2. Two other Members are invited. | command: Start; actor: the Chair. | Return: Success(InProgress); ActualStartedAt = now; MSG110.<br>Exception: N/A<br>Log Message: Saved: status InProgress, history row Start (Published to InProgress) with the Chair as actor; after commit "meeting started" notification queued for the two Members and the Secretary but not for the starter. | Normal | UC-20 · BR-024 · MSG110 |
| UTC002 | 1. Meeting is InProgress; actor is the active Chair.<br>2. Meeting mode is Online. | command: End; actor: the Chair. | Return: Success(Ended); ActualEndedAt = now; MSG111.<br>Exception: N/A<br>Log Message: Saved: status Ended, history row End (InProgress to Ended); draft votes deleted; open speaking requests closed; after commit: agenda event "MeetingEnded" published, draft-deletion events sent to Chair and Secretary only, LiveKit room closed. | Normal | UC-21 · BR-024 · BR-025 · MSG111 |
| UTC003 | Meeting is InProgress, mode InPerson. | command: End; actor: the Chair. | Return: Success(Ended); MSG111.<br>Exception: N/A<br>Log Message: Same saved changes; no room closure because an in-person meeting has no online room. | Boundary | UC-21 · BR-024 · BR-025 · MSG111 |
| UTC004 | Meeting does not exist. | meetingId: unknown. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-20 · BR-024 |
| UTC005 | 1. Meeting is Published with exactly one active Chair and one active Secretary (the actor).<br>2. Chair is not the actor. | command: Start; actor: Secretary. | Return: Success(InProgress); ActualStartedAt = now; MSG110.<br>Exception: N/A<br>Log Message: Saved: status InProgress, history row Start (Published to InProgress) with the Secretary as actor; after commit "meeting started" notification queued for the Chair and the Members but not for the Secretary who started it. | Normal | UC-20 · BR-024 · MSG110 |
| UTC006 | Actor is the Chair on the roster but the Chair account is deactivated. | command: End; actor: deactivated Chair. | Return: Failure Forbidden (403); MSG114.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-21 · BR-024 · MSG114 |
