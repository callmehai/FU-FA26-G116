# Notification.QueueStartingSoon — Unit (FE-13, NOTI)

Description: Starting-soon reminders: one notification per active participant inside the 15-minute window.

Precondition: Published meetings starting within 15 minutes with active participants.

## NotificationService.QueueStartingSoonAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:119`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Clock 2026-11-18T03:00Z. No Published meeting starts in the next 15 minutes. | No input. | Return: 0; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Normal | BR-303 |
| UTC002 | Published meeting M1 "Họp giao ban" starts at 03:10Z with two active participants U1 and U2. | Clock 03:00Z. | Return: 2.<br>Exception: N/A<br>Log Message: Two notifications: type MeetingStartingSoon, title "Cuộc họp sắp bắt đầu", body "Cuộc họp "Họp giao ban" sẽ bắt đầu trong vòng 15 phút.", target Meeting M1, key "meeting-starting-soon:M1:<start as unix seconds>:<user id>"; one commit. | Normal | BR-303 |
| UTC003 | Meeting M1 starts exactly 15 minutes ahead, at 03:15Z. | Clock 03:00Z. | Return: The participants are reminded: a start at the cutoff is inside the window (2 for 2 participants).<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC004 | Meeting M1 starts at 03:15:01Z, one second beyond the window. | Clock 03:00Z. | Return: 0.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC005 | Meeting M1 starts exactly now, 03:00Z. | Clock 03:00Z. | Return: 0: a meeting that is starting at this instant is not 'upcoming'.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC006 | U1 was already reminded for M1 at the same planned start (the key exists); U2 was not. | Clock 03:00Z. | Return: 1 (only U2).<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC007 | Every participant was already reminded. | Clock 03:00Z. | Return: 0; no save.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC008 | M1 was reminded for 03:10Z, then rescheduled to 03:12Z the same day. | Clock 03:00Z. | Return: 2: the key holds the planned start, so the new time is reminded again.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · BR-303 · BR-76 |
| UTC009 | The same user appears twice for the same meeting in the upcoming list. | Clock 03:00Z. | Return: 1: the second row has the same key and is skipped.<br>Exception: N/A<br>Log Message: N/A | Abnormal | BR-303 |
| UTC010 | The meeting is Draft or Cancelled, or the participant is inactive. | Clock 03:00Z. | Return: 0: only active participants of Published meetings are returned.<br>Exception: N/A<br>Log Message: N/A | Abnormal | BR-303 |
