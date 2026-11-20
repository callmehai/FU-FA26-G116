# InviteEvents.StageReschedule — Unit (FE-13, NOTI)

Description: Staging invitation emails for publication, cancellation and rescheduling.

Precondition: Meeting with participants and guests.

## MeetingInvitationEvents.StageRescheduleInvitationsAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingInvitationEvents.cs:47`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A Published meeting with 2 active participants and 1 Guest, moved from 09:00 to 14:00 on the same day. | meeting (new time 14:00-15:30); historyId H2; previous (09:00, 10:30). | Return: No result. Three Rescheduled dispatches staged with key roster:H2:Rescheduled:<owner id>.<br>Exception: N/A<br>Log Message: Each payload holds PreviousStartsAt 09:00 and PreviousEndsAt 10:30. No in-app notification is staged here; the caller adds it, leaving out the organizer who moved the meeting. | Normal | UC-15 · BR-76 · BR-322 |
| UTC002 | A meeting with no active participants and no Guests. | meeting. | Return: Nothing is staged.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-15 · BR-76 |
