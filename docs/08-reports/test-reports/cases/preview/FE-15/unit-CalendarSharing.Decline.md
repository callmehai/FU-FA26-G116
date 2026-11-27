# CalendarSharing.Decline — Unit (FE-15, CAL)

Description: Answering a share request: accept and decline with state rules.

Precondition: A pending share request.

## CalendarSharingService.DeclineAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/CalendarSharingService.cs:229`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: An; shareId. | Return: Success Declined (MSG437); share status Declined, ClosedAt now, ClosedById An, DecidedAt now.<br>Exception: N/A<br>Log Message: Notification CalendarShareDeclined queued for Binh; audit calendar_share.declined. | Normal | UC-80c · MSG437 |
| UTC002 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20.<br>3. Binh (the sender) is deactivated. | actor: An. | Return: Success Declined (MSG437); a share whose sender was deactivated can still be declined.<br>Exception: N/A<br>Log Message: Share closed; notification queued; audit calendar_share.declined. | Boundary | UC-80c · MSG437 |
| UTC003 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18; Binh asked for Level2 until 2026-12-30 (pending change). | actor: An (owner). | Return: Success ChangeDeclined (MSG452); share stays Effective as before (Level1, end 2026-12-18); pending change cleared.<br>Exception: N/A<br>Log Message: Notification CalendarShareChangeDeclined queued for Binh; audit calendar_share.change_declined with the declined Level and end date. | Normal | UC-80c · MSG452 |
| UTC004 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share whose pending change is stale (pending end 2026-11-17). | actor: An. | Return: Success ChangeDeclined (MSG452); a stale change can still be declined.<br>Exception: N/A<br>Log Message: Pending change cleared; notification queued for Binh; audit calendar_share.change_declined. | Boundary | UC-80c · UC-80a · MSG452 |
| UTC005 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share, nothing pending. | actor: An. | Return: Failure NotAllowed (MSG450).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-80c · MSG450 |
