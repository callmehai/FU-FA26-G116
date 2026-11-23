# MeetingAudience.CanUserJoin — Unit (FE-04, MEET)

Description: User room join: an active user may join a meeting they may read.

Precondition: Meeting and participants seeded; user account state known.

## MeetingAudience.CanUserJoinAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingAudience.cs:30`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. User account is active.<br>2. Meeting is Published and the user is an active Member. | meetingId: Published meeting; userId: active Member. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-37 · BR-29 · BR-154 |
| UTC002 | 1. User account is deactivated.<br>2. User is the Chair of the meeting. | meetingId: meeting; userId: deactivated Chair. | Return: false; the readability lookup is not reached.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-29 · BR-154 |
| UTC003 | 1. User account is active.<br>2. Meeting is Draft and the user is only a Member (not readable). | meetingId: Draft meeting; userId: active Member. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-37 · BR-29 · BR-154 |
| UTC004 | 1. User account is active.<br>2. Meeting was Cancelled after being Published; user is a current Member. | meetingId: meeting Cancelled from Published; userId: active Member. | Return: true; the invitees may still open a called-off meeting.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-37 · BR-29 · BR-300 |
