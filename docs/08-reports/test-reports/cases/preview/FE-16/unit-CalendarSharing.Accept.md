# CalendarSharing.Accept — Unit (FE-16, CAL)

Description: Answering a share request: accept and decline with state rules.

Precondition: A pending share request.

## CalendarSharingService.AcceptAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/CalendarSharingService.cs:149`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: An; shareId; values: level null, start null, end null. | Return: Success Accepted (MSG436); share Effective with Level2, 2026-11-20 to 2026-12-20, DecidedAt now.<br>Exception: N/A<br>Log Message: Notification CalendarShareAccepted queued for Binh; audit calendar_share.accepted with Requested and Granted snapshots; both accounts locked and re-read. | Normal | UC-84c · BR-414 · BR-431 · MSG436 |
| UTC002 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: An; values: level "Level2"; start 2026-11-20; end 2026-12-20. | Return: Success Accepted (MSG436); accepted exactly as asked.<br>Exception: N/A<br>Log Message: Notification queued; audit calendar_share.accepted. | Boundary | UC-84c · BR-431 · MSG436 |
| UTC003 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: An; values: end 2026-12-21. | Return: Failure InvalidValues (MSG443); the end is later than asked; share stays Pending.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84c · BR-431 · MSG443 |
| UTC004 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Share Declined (closed). | actor: An; values empty. | Return: Failure NotAllowed (MSG450).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84c · MSG450 |
| UTC005 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20.<br>3. Binh (the sender) is deactivated. | actor: An; values empty. | Return: Failure SenderDeactivated (MSG468); the share stays Pending and can only be declined.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84c · MSG468 |
| UTC006 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18; Binh asked for Level2 until 2026-12-30 (pending change). | actor: An; values: level null, start null, end null. | Return: Success Updated (MSG442); share Level2, end 2026-12-30; pending change cleared.<br>Exception: N/A<br>Log Message: Notification CalendarShareUpdated queued for Binh; audit calendar_share.change_accepted with Before and After. | Normal | UC-84c · BR-431 · MSG442 |
