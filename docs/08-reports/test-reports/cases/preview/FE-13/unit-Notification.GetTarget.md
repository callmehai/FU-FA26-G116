# Notification.GetTarget — Unit (FE-13, NOTI)

Description: Marking notifications read and resolving a notification target.

Precondition: Notifications of two users.

## NotificationService.GetTargetAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:75`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | U1 has notification N1 targeting Meeting M1 (MeetingId M1) and U1 can still access M1. | userId U1; notificationId N1. | Return: NotificationTarget("Meeting", M1).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-78 |
| UTC002 | Notification N2 belongs to U2. | userId U1; notificationId N2. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-78 |
| UTC003 | A calendar-share notification; U1 is the owner or the viewer of the share. | userId U1. | Return: NotificationTarget("CalendarShare", shareId).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-78 |
| UTC004 | A voice-sample notification; the sample S1 still exists and belongs to U1. | userId U1. | Return: NotificationTarget("VoiceSample", S1).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-78 |
