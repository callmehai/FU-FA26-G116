# InviteEvents.StagePublish — Unit (FE-13, NOTI)

Description: Staging invitation emails for publication, cancellation and rescheduling.

Precondition: Meeting with participants and guests.

## MeetingInvitationEvents.StagePublicationInvitationsAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingInvitationEvents.cs:27`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A meeting with 2 active participants (Chair An, Member Bình), 1 inactive user, and 1 active Guest invitation. | meeting; now 2026-11-18T03:00Z. | Return: No result. Three dispatch rows are staged: one Invitation per active participant and one for the Guest.<br>Exception: N/A<br>Log Message: Each dispatch has the participant's initial delivery key (or the Guest's) as its deduplication key. No in-app notification is staged. | Normal | UC-19 · UC-22 |
| UTC002 | A meeting whose roster has the same user twice (two rows) and a participant who delegated their seat. | meeting; now 2026-11-18T03:00Z. | Return: One dispatch for that user; the delegated seat gets none.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-19 · UC-22 |
