# MeetingAudience.CanGuestJoin — Unit (FE-04, MEET)

Description: Guest room join: a live guest session may join its own open meeting.

Precondition: Guest sessions and meetings in known statuses seeded.

## MeetingAudience.CanGuestJoinAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingAudience.cs:35`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Guest session is valid and belongs to the meeting; guest invitation is readable.<br>2. Meeting is Published. | guest: session of this meeting; meetingId: the Published meeting. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 · BR-29 · BR-154 |
| UTC002 | Meeting is InProgress; guest session valid and readable. | guest: valid session; meetingId: InProgress meeting. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-37 · BR-29 · BR-154 |
| UTC003 | Guest session has an empty SessionId (invalid). | guest: session with SessionId = empty Guid; meetingId: any meeting. | Return: false; the meeting is not loaded.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-29 · BR-154 |
| UTC004 | Guest session belongs to another meeting. | guest: session of meeting A; meetingId: meeting B. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-29 · BR-154 |
| UTC005 | Guest invitation was revoked so the agenda guest-access check fails. | guest: valid session of this meeting; meetingId: the meeting. | Return: false; the meeting is not loaded.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-157 · BR-154 |
| UTC006 | 1. Guest session valid and readable.<br>2. Meeting no longer exists. | guest: valid session; meetingId: deleted meeting id. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-29 · BR-154 |
| UTC007 | 1. Guest session valid and readable.<br>2. Meeting is Ended. | guest: valid session; meetingId: Ended meeting. | Return: false; only Published and InProgress meetings are open to guests.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-37 · BR-155 |
| UTC008 | 1. Guest session valid and readable.<br>2. Meeting is Draft. | guest: valid session; meetingId: Draft meeting. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-29 · BR-154 |
