# Noti.MeetingStarted — Unit (FE-13, NOTI)

Description: Meeting-started notification: recipients and payload.

Precondition: Meeting just started.

## NotificationService.QueueMeetingStartedAsync

Layer: Application · `backend/TrustCabinet.Application/Notifications/NotificationService.cs:15`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting "Họp giao ban" started by the Chair; history event H1; three other invited participants. | meetingId M1; title "Họp giao ban"; historyId H1; occurredAt 2026-11-18T02:00Z; recipients [U1, U2, U3]. | Return: No result. Three notifications are saved in one commit.<br>Exception: N/A<br>Log Message: Each notification: type MeetingStarted, title "Cuộc họp đã bắt đầu", body "Cuộc họp “Họp giao ban” đã bắt đầu.", target Meeting M1, origin history H1, key "meeting-started:H1:<user id>", created at 02:00Z. | Normal | UC-21 · BR-164 · BR-322 |
| UTC002 | The recipient list contains U1 twice. | recipients [U1, U1, U2]. | Return: Two notifications (one per distinct user).<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-21 · BR-164 · BR-322 |
| UTC003 | No recipients (the Chair is the only invited participant). | recipients []. | Return: No notification and no database write.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-21 · BR-164 · BR-322 |
