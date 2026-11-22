# CalendarSharing.Change — Unit (FE-16, CAL)

Description: Changing, withdrawing and ending a share.

Precondition: An open share.

## CalendarSharingService.ChangeAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/CalendarSharingService.cs:258`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending. | actor: Binh (viewer); values: level "Level2"; end null. | Return: Success ChangeRequested (MSG454); share stays Effective at Level1; PendingLevel Level2, PendingEndDate 2026-12-18, PendingRequestedAt now.<br>Exception: N/A<br>Log Message: Notification CalendarShareChangeRequested queued for An; audit calendar_share.change_requested with Current and Asked. | Normal | UC-84d · BR-414 · MSG454 |
| UTC002 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending. | actor: Binh; values: level "Level2"; end 2026-12-25; start 2026-11-18. | Return: Success ChangeRequested (MSG454); both level and end asked; the start equal to the current start is allowed.<br>Exception: N/A<br>Log Message: Notification queued for An; audit calendar_share.change_requested. | Boundary | UC-84d · MSG454 |
| UTC003 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending. | actor: Binh; values: end 2027-11-17. | Return: Success ChangeRequested (MSG454); exactly 365 days from the start.<br>Exception: N/A<br>Log Message: Notification queued for An; audit calendar_share.change_requested. | Boundary | UC-84d · BR-431 · MSG454 |
| UTC004 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending. | actor: Binh; values: level null; end null. | Return: Failure InvalidValues (MSG443); nothing would change.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84d · MSG443 |
| UTC005 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18; Binh asked for Level2 until 2026-12-30 (pending change). | actor: Binh; values: end 2027-01-18. | Return: Failure NotAllowed (MSG450); only one pending change at a time.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84d · MSG450 |
| UTC006 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level2, 2026-11-10 to 2026-12-10 (start in the past). | actor: An (owner); values: level "Level1". | Return: Success Updated (MSG442); level lowered at once, the past start stays.<br>Exception: N/A<br>Log Message: Notification CalendarShareUpdated queued for Binh; audit calendar_share.updated with Before and After. | Normal | UC-84d · BR-414 · MSG442 |
| UTC007 | Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer. | actor: a third user; shareId of an Effective share. | Return: Failure NotFound (404).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84d |
| UTC008 | 1. Share between owner An and viewer Binh; today is 2026-11-18; An and Binh are active and Binh is a Meeting Organizer.<br>2. Effective share Level1, 2026-11-18 to 2026-12-18, nothing pending.<br>3. The database fails while saving. | actor: Binh; values: level "Level2". | Return: Failure SaveFailed (MSG447); no pending change is kept.<br>Exception: N/A<br>Log Message: Unit of work changes discarded; error logged. | Abnormal | UC-84d · MSG447 |
