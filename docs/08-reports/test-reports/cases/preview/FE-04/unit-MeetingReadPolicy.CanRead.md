# MeetingReadPolicy.CanRead — Unit (FE-04, MEET)

Description: Meeting read policy: who may read a meeting, its history and its rejection reason.

Precondition: Meeting with known creator, approver and participant list.

## MeetingReadPolicy.CanRead

Layer: Domain · `backend/TrustCabinet.Domain/Meetings/MeetingReadPolicy.cs:34`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting in Draft created by the user. | Meeting: Draft; userId: creator. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC002 | Meeting in WaitingApproval with an assigned approver. | userId: assigned approver. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC003 | Meeting in Draft with an assigned approver left from an earlier cycle. | userId: that approver. | Return: false; the approver reads only once the meeting is past Draft.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC004 | Draft meeting; user is the current Chair. | userId: Chair. | Return: true; a Chair reads at any status.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC005 | Approved meeting; user is the current Secretary. | userId: Secretary. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC006 | Approved meeting; user is only a Member. | userId: Member. | Return: false; a Member reads only from Published.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC007 | Published meeting; user is a Member. | userId: Member. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC008 | InProgress meeting; user is a Member. | userId: Member. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC009 | Ended meeting; user is a Member. | userId: Member. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC010 | Cancelled meeting that was Published before it was cancelled; user is a Member. | userId: Member; CancelledFromStatus: Published. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-29 · BR-300 |
| UTC011 | Cancelled meeting that was Approved before it was cancelled; user is a Member. | userId: Member; CancelledFromStatus: Approved. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-300 |
| UTC012 | Published meeting; the Member answered Delegated and was handed over to another user. | userId: the delegating Member. | Return: false; a delegated row is not a current participant.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC013 | Published meeting; the user holds an external Guest role row. | userId: guest row user. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC014 | Published meeting; the Member row was replaced (assignment status not Current). | userId: replaced Member. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-118 · BR-119 |
| UTC015 | Published meeting; user has no relationship to it. | userId: stranger. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 · BR-118 · BR-119 |
