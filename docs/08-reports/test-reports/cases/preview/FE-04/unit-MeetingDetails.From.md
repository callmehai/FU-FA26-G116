# MeetingDetails.From — Unit (FE-04, MEET)

Description: Meeting detail projection: the detail fields built from a meeting for a viewer.

Precondition: Meeting with participants.

## MeetingDetails.From

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingContracts.cs:54`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Draft meeting with a stale AssignedApproverId. | meeting: Draft, AssignedApproverId set; actorId: creator. | Return: Details with AssignedApproverId null (hidden before submission).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-016, BR-017 |
| UTC002 | Published meeting with a Chair, two Secretaries named "Bình" and "An", and the actor is a Member who has not answered. | actorId: Member. | Return: Chair set; Secretaries ordered by name (An, Bình); MyMeetingRole "Member"; MyResponse null; CanRespond true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · UC-31 · BR-016, BR-017 |
| UTC003 | Meeting is Cancelled with a stored cancellation time and canceller. | meeting: Cancelled; CancelledAt set; CancelledBy "Lan"; reason "Hoãn". | Return: Cancellation { time, CancelledById, CancelledByName "Lan", Reason "Hoãn" }.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · UC-22 |
| UTC004 | Meeting has an inactive Chair row (replaced) next to the current Chair. | two Chair rows, one not Current. | Return: Chair is the current one only.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · BR-021 |
