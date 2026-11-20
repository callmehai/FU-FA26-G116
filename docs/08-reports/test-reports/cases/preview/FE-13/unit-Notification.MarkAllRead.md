# Notification.MarkAllRead — Unit (FE-13, NOTI)

Description: Marking notifications read and resolving a notification target.

Precondition: Notifications of two users.

## NotificationService.MarkAllReadAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:71`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | User U1 has 3 unread and 2 read notifications; U2 has 1 unread. | userId U1; clock 2026-11-18T03:00Z. | Return: 3.<br>Exception: N/A<br>Log Message: U1's three unread get ReadAt 03:00Z; the read ones and U2's notification are untouched. | Normal | UC-67b |
| UTC002 | User U1 has no unread notification. | userId U1. | Return: 0.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-67b |
| UTC003 | User with no notification at all. | userId U3. | Return: 0.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-67b |
