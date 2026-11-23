# MeetingAudience.CanGuestJoin — Unit (FE-04, MEET)

Description: Guest room join: a live guest session may join its own open meeting.

Precondition: Guest sessions and meetings in known statuses seeded.

## MeetingAudience.CanGuestJoinAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingAudience.cs:35`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Guest session is valid and belongs to the meeting; guest invitation is readable.<br>2. Meeting is Published. | guest: session of this meeting; meetingId: the Published meeting. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 · BR-29 · BR-154 |
| UTC002 | Guest session has an empty SessionId (invalid). | guest: session with SessionId = empty Guid; meetingId: any meeting. | Return: false; the meeting is not loaded.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-29 · BR-154 |
| UTC003 | 1. Guest session valid and readable.<br>2. Meeting is Ended. | guest: valid session; meetingId: Ended meeting. | Return: false; only Published and InProgress meetings are open to guests.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-37 · BR-155 |
