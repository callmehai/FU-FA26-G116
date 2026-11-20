# Notification.GetTarget — Unit (FE-13, NOTI)

Description: Marking notifications read and resolving a notification target.

Precondition: Notifications of two users.

## NotificationService.GetTargetAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:75`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | U1 has notification N1 targeting Meeting M1 (MeetingId M1) and U1 can still access M1. | userId U1; notificationId N1. | Return: NotificationTarget("Meeting", M1).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-68 |
| UTC002 | N1 as above, but U1 lost access to M1 (removed from the roster). | userId U1; notificationId N1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-68 |
| UTC003 | Notification N2 belongs to U2. | userId U1; notificationId N2. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-68 |
| UTC004 | No notification with this id. | userId U1; notificationId random. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-68 |
| UTC005 | A notification with TargetType Meeting whose MeetingId (M1) differs from its TargetId (M2). | userId U1. | Return: null: the target is not opened.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-68 |
| UTC006 | A notification of a removed participant: TargetType and TargetId are null. | userId U1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-68 |
| UTC007 | A Meeting notification whose TargetId is null. | userId U1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-68 |
| UTC008 | A calendar-share notification; U1 is the owner or the viewer of the share. | userId U1. | Return: NotificationTarget("CalendarShare", shareId).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-68 |
| UTC009 | A calendar-share notification; the share no longer involves U1 or no longer exists. | userId U1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-68 |
| UTC010 | A calendar-share notification with a null TargetId. | userId U1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-68 |
| UTC011 | A voice-sample notification; the sample S1 still exists and belongs to U1. | userId U1. | Return: NotificationTarget("VoiceSample", S1).<br>Exception: N/A<br>Log Message: N/A | Normal | UC-68 |
| UTC012 | A voice-sample notification; the sample was deleted or belongs to someone else. | userId U1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-68 |
| UTC013 | A voice-sample notification with a null TargetId. | userId U1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-68 |
| UTC014 | A notification with an unsupported TargetType "Document". | userId U1. | Return: null.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-68 |
