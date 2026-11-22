# CalendarSharing.End — Unit (FE-16, CAL)

Description: Changing, withdrawing and ending a share.

Precondition: An open share.

## CalendarSharingService.EndAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/CalendarSharingService.cs:344`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending. | actor: An (owner); shareId. | Return: Success Ended (MSG451); share status Ended, ClosedAt now, ClosedById An.<br>Exception: N/A<br>Log Message: Notification CalendarShareEnded queued for Binh; audit calendar_share.ended. | Normal | UC-84e · MSG451 |
| UTC002 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18; Binh asked for Level2 until 2026-12-30 (pending change). | actor: Binh. | Return: Success Ended (MSG451); the pending change is cleared with the share.<br>Exception: N/A<br>Log Message: Share closed with no pending change; notification queued for An; audit calendar_share.ended. | Boundary | UC-84e · MSG451 |
| UTC003 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Pending share started by Binh (viewer): Level2, 2026-11-20 to 2026-12-20. | actor: An. | Return: Failure NotAllowed (MSG450); a Pending share is declined or withdrawn, not ended.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84e · MSG450 |
| UTC004 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending. | actor: a third user. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84e |
| UTC005 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share whose end date is today 2026-11-18. | actor: An. | Return: Success Ended (MSG451); the last day still counts.<br>Exception: N/A<br>Log Message: Notification queued for Binh; audit calendar_share.ended. | Boundary | UC-84e · BR-425 · MSG451 |
| UTC006 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending.<br>3. The database fails while saving. | actor: An. | Return: Failure SaveFailed (MSG447); share stays Effective.<br>Exception: N/A<br>Log Message: Unit of work changes discarded; error logged. | Abnormal | UC-84e · MSG447 |
