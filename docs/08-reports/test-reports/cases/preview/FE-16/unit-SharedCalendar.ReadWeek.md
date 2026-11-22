# SharedCalendar.ReadWeek — Unit (FE-16, CAL)

Description: Reading a shared calendar week and the busy time behind it.

Precondition: An open share.

## SharedCalendarService.ReadWeekAsync

Layer: Application · `backend/TrustCabinet.Application/CalendarSharing/SharedCalendarService.cs:28`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active Meeting Organizer; An is an active user.<br>3. Effective Level1 share from An to Binh, 2026-11-09 to 2026-12-09.<br>4. An chairs an Approved Normal meeting on Monday 2026-11-16 from 09:00 to 11:00. | actor: Binh; week: null (the current week). | Return: Week 2026-11-16 (Monday) to 2026-11-22 (Sunday), dayStart "00:00", dayEnd "24:00"; people [An with Level1 and its dates]; Monday known true with busy ["2026-11-16T09:00:00+07:00" to "2026-11-16T11:00:00+07:00"]; the other six days known true with no busy. Nothing about any meeting is disclosed.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-85c · BR-419 · BR-420 · Deviation: code now shows the whole week (Monday to Sunday) and the whole day (00:00–24:00), not Monday to Friday 08:00–17:00 |
| UTC002 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active Meeting Organizer; An is an active user.<br>3. Share from An to Binh is Stopped in the database (Level1, 2026-11-09 to 2026-12-09).<br>4. An is busy every weekday. | actor: Binh; week: null (the current week). | Return: People [An with status Stopped, Level1 and its dates, days empty]; no busy is shown. The screen shows "Chia sẻ lịch đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị vô hiệu hoá. Hãy xin chia sẻ mới."<br>Exception: N/A<br>Log Message: N/A | Normal | UC-85c · BR-424 · MSG439 |
| UTC003 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active user without the Meeting Organizer right. | actor: Binh; week: null. | Return: Failure NotOrganizer; the endpoint answers 403 MSG448 ("Bạn cần quyền tạo cuộc họp để thực hiện thao tác này."); nothing is read.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-85c · BR-430 · MSG448 |
| UTC004 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active Meeting Organizer.<br>3. The database fails while reading the shares and the busy time. | actor: Binh; week: null. | Return: Failure ReadFailed; the endpoint answers 500 MSG453 ("Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại."); nothing is returned.<br>Exception: N/A<br>Log Message: Error logged with the actor id. | Abnormal | UC-85c · MSG453 |
| UTC005 | 1. Today is 2026-11-18 in Vietnam.<br>2. Binh is an active Meeting Organizer; An is an active user with an Effective share covering November. | actor: Binh; week: 2026-11-22 (a Sunday). | Return: Week 2026-11-16 (Monday) to 2026-11-22 (Sunday); a Sunday belongs to the week starting the Monday before.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-85c · Deviation: week now ends Sunday 2026-11-22, not Friday 2026-11-20 |
