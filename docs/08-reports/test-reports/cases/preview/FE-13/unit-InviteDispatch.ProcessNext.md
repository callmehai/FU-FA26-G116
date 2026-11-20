# InviteDispatch.ProcessNext — Unit (FE-13, NOTI)

Description: Invitation dispatch worker: pick the next staged event and send.

Precondition: Staged invitation events.

## MeetingInvitationDispatchProcessor.ProcessNextAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingInvitationDispatchProcessor.cs:21`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Email delivery is disabled in configuration (Enabled false). | No input. | Return: 0. The dispatch repository is not read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-20 · BR-300 |
| UTC002 | 1. Email delivery enabled.<br>2. No dispatch is due. | Clock 2026-11-18T03:00Z. | Return: 0.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-20 · BR-300 |
| UTC003 | Three Pending dispatches are due; each can be turned into an email. | Clock 2026-11-18T03:00Z. | Return: 3 (the sum of the emails queued by the three dispatches).<br>Exception: N/A<br>Log Message: Three emails are queued and the three dispatches become Materialized. | Normal | UC-20 · BR-300 |
| UTC004 | Three dispatches are due: one is turned into an email, one is already Materialized by another worker, one is suppressed because the invitee is no longer on the roster. | Clock 2026-11-18T03:00Z. | Return: 1: only the dispatch that produced an email counts.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-20 · BR-300 |
| UTC005 | Exactly 50 dispatches are due. | Clock 2026-11-18T03:00Z. | Return: 50 (the whole batch is processed).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-20 · BR-300 |
| UTC006 | 51 dispatches are due. | Clock 2026-11-18T03:00Z. | Return: 50: one batch lists at most 50 due ids, the 51st waits for the next cycle.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-20 · BR-300 |
| UTC007 | Two due dispatches; the first one's materialization throws. | Clock 2026-11-18T03:00Z. | Return: 1: the failure is recorded for the first dispatch and the second one is still processed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-20 · BR-300 |
