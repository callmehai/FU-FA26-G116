# MeetingDetails.From — Unit (FE-04, MEET)

Description: Meeting detail projection: the detail fields built from a meeting for a viewer.

Precondition: Meeting with participants.

## MeetingDetails.From

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingContracts.cs:54`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Draft meeting with a stale AssignedApproverId. | meeting: Draft, AssignedApproverId set; actorId: creator. | Return: Details with AssignedApproverId null (hidden before submission).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-29 |
| UTC002 | WaitingApproval meeting with an approver. | meeting: WaitingApproval; actorId: creator. | Return: Details expose AssignedApproverId.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · BR-29 |
| UTC003 | Published meeting with a Chair, two Secretaries named "Bình" and "An", and the actor is a Member who has not answered. | actorId: Member. | Return: Chair set; Secretaries ordered by name (An, Bình); MyMeetingRole "Member"; MyResponse null; CanRespond true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · UC-29 · BR-29 |
| UTC004 | Actor is the Chair of a Published meeting. | actorId: Chair. | Return: MyMeetingRole "Chair"; MyResponse null; CanRespond false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · UC-29 |
| UTC005 | Actor is a Member who accepted. | actorId: Member with response Accepted. | Return: MyResponse "Accepted"; CanRespond false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · UC-29 |
| UTC006 | Actor has no participant row. | actorId: the creator who is not on the roster. | Return: MyMeetingRole, MyResponse null; CanRespond false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-29 |
| UTC007 | No actor supplied. | actorId: null. | Return: MyMeetingRole, MyResponse null; CanRespond false.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-29 |
| UTC008 | The actor Member row is delegated. | actorId: delegating Member (Delegated). | Return: MyMeetingRole null; the row is not current.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · UC-29 |
| UTC009 | Meeting is Cancelled with a stored cancellation time and canceller. | meeting: Cancelled; CancelledAt set; CancelledBy "Lan"; reason "Hoãn". | Return: Cancellation { time, CancelledById, CancelledByName "Lan", Reason "Hoãn" }.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-16 · UC-75 · BR-298 |
| UTC010 | Meeting was cancelled by the system after the 24-hour rule. | CancelledById: null; CancelledBy: null. | Return: Cancellation.CancelledByName is "Hệ thống".<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · UC-75 · BR-301 |
| UTC011 | Meeting is Cancelled but has no cancellation time recorded. | CancelledAt: null. | Return: Cancellation null.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-299 |
| UTC012 | Meeting is not Cancelled but has an old CancelledAt value. | Status: Published; CancelledAt set. | Return: Cancellation null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-16 · BR-299 |
| UTC013 | Meeting has an inactive Chair row (replaced) next to the current Chair. | two Chair rows, one not Current. | Return: Chair is the current one only.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-16 · BR-124 |
