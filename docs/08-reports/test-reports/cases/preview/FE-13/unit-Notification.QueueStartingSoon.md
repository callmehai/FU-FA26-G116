# Notification.QueueStartingSoon — Unit (FE-13, NOTI)

Description: Starting-soon reminders: one notification per active participant inside the 15-minute window.

Precondition: Published meetings starting within 15 minutes with active participants.

## NotificationService.QueueStartingSoonAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:119`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Published meeting M1 "Họp giao ban" starts at 03:10Z with two active participants U1 and U2. | Clock 03:00Z. | Return: 2.<br>Exception: N/A<br>Log Message: Two notifications: type MeetingStartingSoon, title "Cuộc họp sắp bắt đầu", body "Cuộc họp "Họp giao ban" sẽ bắt đầu trong vòng 15 phút.", target Meeting M1, key "meeting-starting-soon:M1:<start as unix seconds>:<user id>"; one commit. | Normal | BR-303 |
| UTC002 | Meeting M1 starts exactly 15 minutes ahead, at 03:15Z. | Clock 03:00Z. | Return: The participants are reminded: a start at the cutoff is inside the window (2 for 2 participants).<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC003 | Meeting M1 starts at 03:15:01Z, one second beyond the window. | Clock 03:00Z. | Return: 0.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC004 | The same user appears twice for the same meeting in the upcoming list. | Clock 03:00Z. | Return: 1: the second row has the same key and is skipped.<br>Exception: N/A<br>Log Message: N/A | Abnormal | BR-303 |
