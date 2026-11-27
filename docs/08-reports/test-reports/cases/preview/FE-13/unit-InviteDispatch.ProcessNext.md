# InviteDispatch.ProcessNext — Unit (FE-13, NOTI)

Description: Invitation dispatch worker: pick the next staged event and send.

Precondition: Staged invitation events.

## MeetingInvitationDispatchProcessor.ProcessNextAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingInvitationDispatchProcessor.cs:21`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Email delivery is disabled in configuration (Enabled false). | No input. | Return: 0. The dispatch repository is not read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-19 · UC-22 |
| UTC002 | Three Pending dispatches are due; each can be turned into an email. | Clock 2026-11-18T03:00Z. | Return: 3 (the sum of the emails queued by the three dispatches).<br>Exception: N/A<br>Log Message: Three emails are queued and the three dispatches become Materialized. | Normal | UC-19 · UC-22 |
| UTC003 | Three dispatches are due: one is turned into an email, one is already Materialized by another worker, one is suppressed because the invitee is no longer on the roster. | Clock 2026-11-18T03:00Z. | Return: 1: only the dispatch that produced an email counts.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-19 · UC-22 |
| UTC004 | Exactly 50 dispatches are due. | Clock 2026-11-18T03:00Z. | Return: 50 (the whole batch is processed).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-19 · UC-22 |
