# CalendarSharing.Request — Unit (FE-16, CAL)

Description: Requesting and directly sharing a calendar: validation, duplicates, notification.

Precondition: Two active users.

## CalendarSharingService.RequestAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/CalendarSharingService.cs:85`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is an active Meeting Organizer (the viewer); Binh is an active user (the owner).<br>3. No share exists between Binh and An. | actor: An; ownerId: Binh; level: "Level1"; start: 2026-11-18; end: 2026-12-18. | Return: Share created in status Pending, InitiatedBy Viewer, owner Binh, viewer An; Success Requested (MSG435); share view returned.<br>Exception: N/A<br>Log Message: Share saved; notification CalendarShareRequested queued for Binh ("Yêu cầu xem lịch"); audit action calendar_share.requested by An with owner, viewer, level and dates. | Normal | UC-84a · BR-423 · BR-431 · MSG435 |
| UTC002 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is an active Meeting Organizer (the viewer); Binh is an active user (the owner).<br>3. No share exists. | actor: An; ownerId: Binh; level: "Level2"; start: 2026-11-18; end: 2027-11-17. | Return: Success Requested (MSG435); Level2, exactly 365 days.<br>Exception: N/A<br>Log Message: Share saved; notification queued for Binh; audit calendar_share.requested. | Boundary | UC-84a · BR-414 · BR-431 · MSG435 |
| UTC003 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is not in the user list (unknown actor). | actor: an unknown id; ownerId: Binh. | Return: Failure NotOrganizer (MSG448); nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84a · BR-430 · MSG448 |
| UTC004 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is an active Meeting Organizer (the viewer); Binh is an active user (the owner). | actor: An; ownerId: null. | Return: Failure NotFound (404, no code); nothing disclosed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84a · BR-430 |
| UTC005 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is an active Meeting Organizer (the viewer); Binh is an active user (the owner).<br>3. A Pending share Binh -> An already exists. | actor: An; ownerId: Binh; valid values. | Return: Failure AlreadyShared (MSG449); nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84a · BR-423 · MSG449 |
| UTC006 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is an active Meeting Organizer (the viewer); Binh is an active user (the owner).<br>3. A Declined share Binh -> An exists (closed). | actor: An; ownerId: Binh; valid values. | Return: Success Requested (MSG435); closed shares do not block a new one.<br>Exception: N/A<br>Log Message: New share saved; notification queued for Binh; audit calendar_share.requested. | Boundary | UC-84a · BR-423 · MSG435 |
| UTC007 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is an active Meeting Organizer (the viewer); Binh is an active user (the owner). | actor: An; ownerId: Binh; level: null; start: 2026-11-18; end: 2026-12-18. | Return: Failure InvalidValues (MSG443).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84a · BR-414 · MSG443 |
| UTC008 | 1. Today is 2026-11-18 in Vietnam.<br>2. An is an active Meeting Organizer (the viewer); Binh is an active user (the owner).<br>3. The database fails while saving. | actor: An; ownerId: Binh; valid values. | Return: Failure SaveFailed (MSG447); no share, notification or audit row is kept.<br>Exception: N/A<br>Log Message: Unit of work changes discarded; error logged. | Abnormal | UC-84a · MSG447 |
