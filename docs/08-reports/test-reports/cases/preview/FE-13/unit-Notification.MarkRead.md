# Notification.MarkRead — Unit (FE-13, NOTI)

Description: Marking notifications read and resolving a notification target.

Precondition: Notifications of two users.

## NotificationService.MarkReadAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:56`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Notification N1 belongs to user U1 and is unread. | userId U1; notificationId N1; clock 2026-11-18T03:00Z. | Return: The notification, now with ReadAt 03:00Z.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-67a |
| UTC002 | Notification N2 belongs to user U2. | userId U1; notificationId N2. | Return: null; N2 is unchanged (another user's notification is not visible).<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-67a |
| UTC003 | No notification with this id. | userId U1; notificationId random. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-67a |
| UTC004 | Notification N1 is already read at 02:00Z. | userId U1; notificationId N1; clock 03:00Z. | Return: The notification; ReadAt stays 02:00Z.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-67a |
