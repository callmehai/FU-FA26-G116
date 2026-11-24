# MeetingReadPolicy.CanRead — Unit (FE-04, MEET)

Description: Meeting read policy: who may read a meeting, its history and its rejection reason.

Precondition: Meeting with known creator, approver and participant list.

## MeetingReadPolicy.CanRead

Layer: Domain · `backend/TrustCabinet.Domain/Meetings/MeetingReadPolicy.cs:34`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting in Draft created by the user. | Meeting: Draft; userId: creator. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC002 | Draft meeting; user is the current Chair. | userId: Chair. | Return: true; a Chair reads at any status.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC003 | Published meeting; user is a Member. | userId: Member. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC004 | Approved meeting; user is only a Member. | userId: Member. | Return: false; a Member reads only from Published.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC005 | Cancelled meeting that was Published before it was cancelled; user is a Member. | userId: Member; CancelledFromStatus: Published. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-29 · BR-300 |
