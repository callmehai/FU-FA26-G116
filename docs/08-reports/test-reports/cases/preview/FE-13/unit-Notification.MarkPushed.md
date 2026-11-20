# Notification.MarkPushed — Unit (FE-13, NOTI)

Description: Pending push selection and marking pushed.

Precondition: Notifications with push state.

## NotificationService.MarkPushedAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:169`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Notification N1 has no push time. | notificationId N1; clock 2026-11-18T03:00Z. | Return: No result.<br>Exception: N/A<br>Log Message: N1's PushedAt is set to 03:00Z. | Normal | UC-66 |
| UTC002 | Notification N1 was already pushed at 02:00Z. | notificationId N1; clock 03:00Z. | Return: No result; PushedAt stays 02:00Z.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-66 |
| UTC003 | No notification with this id. | notificationId random. | Return: No result and no error.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-66 |
