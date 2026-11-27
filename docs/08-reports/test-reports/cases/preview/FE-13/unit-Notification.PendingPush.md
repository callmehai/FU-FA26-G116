# Notification.PendingPush — Unit (FE-13, NOTI)

Description: Pending push selection and marking pushed.

Precondition: Notifications with push state.

## NotificationService.PendingPushAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:166`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Five notifications with no push time; the oldest were created 02:00Z, 02:01Z ... | limit 3. | Return: The three oldest unpushed notifications, oldest first.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-76 |
| UTC002 | Every notification was pushed. | limit 10. | Return: Empty list.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-76 |
