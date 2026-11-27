# SharedCalendar.ReadWeek — Unit (FE-15, CAL)

Description: Reading a shared calendar week and the busy time behind it.

Precondition: An open share.

## SharedCalendarService.ReadWeekAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/SharedCalendarService.cs:28`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active Meeting Organizer; An is an active user.<br>3. Effective Level1 share from An to Binh, 2026-11-09 to 2026-12-09.<br>4. An chairs an Approved Normal meeting on Monday 2026-11-16 from 09:00 to 11:00. | actor: Binh; week: null (the current week). | Return: Week 2026-11-16 (Monday) to 2026-11-22 (Sunday), dayStart "00:00", dayEnd "24:00"; people [An with Level1 and its dates]; Monday known true with busy ["2026-11-16T09:00:00+07:00" to "2026-11-16T11:00:00+07:00"]; the other six days known true with no busy. Nothing about any meeting is disclosed.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-81c · BR-146, BR-147 |
| UTC002 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active Meeting Organizer; An is an active user.<br>3. Share from An to Binh is Stopped in the database (Level1, 2026-11-09 to 2026-12-09).<br>4. An is busy every weekday. | actor: Binh; week: null (the current week). | Return: People [An with status Stopped, Level1 and its dates, days empty]; no busy is shown. The screen shows "Chia sẻ lịch đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị vô hiệu hoá. Hãy xin chia sẻ mới."<br>Exception: N/A<br>Log Message: N/A | Normal | UC-81c · BR-142, BR-143 · MSG439 |
| UTC003 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active user without the Meeting Organizer right. | actor: Binh; week: null. | Return: Failure NotOrganizer; the endpoint answers 403 MSG448 ("Bạn cần quyền tạo cuộc họp để thực hiện thao tác này."); nothing is read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-81c · BR-140, BR-141 · MSG448 |
| UTC004 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active Meeting Organizer; An is an active user with an Effective share covering November. | actor: Binh; week: 2026-11-22 (a Sunday). | Return: Week 2026-11-16 (Monday) to 2026-11-22 (Sunday); a Sunday belongs to the week starting the Monday before.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-81c |
