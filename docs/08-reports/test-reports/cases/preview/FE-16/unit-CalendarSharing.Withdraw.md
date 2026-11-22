# CalendarSharing.Withdraw — Unit (FE-16, CAL)

Description: Changing, withdrawing and ending a share.

Precondition: An open share.

## CalendarSharingService.WithdrawAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/CalendarSharingService.cs:314`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: Binh (sender); shareId. | Return: Success Withdrawn (MSG438); share status Withdrawn, ClosedAt now, ClosedById Binh.<br>Exception: N/A<br>Log Message: Notification CalendarShareWithdrawn queued for An; audit calendar_share.withdrawn. | Normal | UC-84e · MSG438 |
| UTC002 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: An (the asked side). | Return: Failure NotAllowed (MSG450); only the sender withdraws.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84e · MSG450 |
| UTC003 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: a third user. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84e |
| UTC004 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20.<br>3. The database fails while saving. | actor: Binh. | Return: Failure SaveFailed (MSG447); share stays Pending.<br>Exception: N/A<br>Log Message: Unit of work changes discarded; error logged. | Abnormal | UC-84e · MSG447 |
