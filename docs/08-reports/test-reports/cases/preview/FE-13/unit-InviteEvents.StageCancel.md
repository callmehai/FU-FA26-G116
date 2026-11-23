# InviteEvents.StageCancel — Unit (FE-13, NOTI)

Description: Staging invitation emails for publication, cancellation and rescheduling.

Precondition: Meeting with participants and guests.

## MeetingInvitationEvents.StageCancellationAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingInvitationEvents.cs:35`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | A Published meeting with Organizer-Chair An, Member Bình and one active Guest; An cancels it. | meeting; historyId H1; actorId An; reason "Hoãn do lịch bận". | Return: No result. Three Cancelled dispatches staged, all with event id H1 and deduplication key roster:H1:Cancelled:<owner id>.<br>Exception: N/A<br>Log Message: An in-app notification "Cuộc họp đã bị huỷ" (body: Cuộc họp “<title>” đã bị huỷ. Lý do: Hoãn do lịch bận) is staged for Bình only; none for An (the actor) and none for the Guest. The dispatch payload carries the reason. | Normal | UC-75 · BR-300 · BR-322 |
| UTC002 | A meeting with only the actor as participant. | actorId: the only participant. | Return: One dispatch for the actor, no in-app notification.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-75 · BR-322 |
| UTC003 | A meeting with no active participants and no Guests. | meeting. | Return: Nothing is staged.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-75 · BR-300 |
