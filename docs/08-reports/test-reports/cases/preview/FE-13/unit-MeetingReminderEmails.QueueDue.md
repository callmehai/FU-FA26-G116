# MeetingReminderEmails.QueueDue — Unit (FE-13, NOTI)

Description: Queuing reminder emails for meetings starting soon.

Precondition: Meetings about to start; clock replaced.

## MeetingReminderEmails.QueueDueAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingReminderEmails.cs:31`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Published in-person meeting "Họp giao ban" starts at 03:10Z; organizer An (an@example.vn); participants Bình (Chair) and Cường (Member); invitations were sent days ago. | Clock 03:00Z; application base URL https://tc.example.vn/. | Return: 2 (one reminder per participant).<br>Exception: N/A<br>Log Message: Two EmailMessage rows queued, subject "Cuộc họp sắp bắt đầu: Họp giao ban", deduplication key "email-starting-soon:<meetingId>:<start unix seconds>:<participant id>"; the text names the start time, place and the link https://tc.example.vn/mgmt/meetings/<meetingId>; the HTML shows the role (Chủ toạ / Thành viên), the chair and the organizer contact "An (an@example.vn)"; one commit. | Normal | BR-303 |
| UTC002 | Participant invited exactly at 02:55Z, which is 15 minutes before a 03:10Z start. | Clock 03:00Z. | Return: No email: an invitation created at the boundary counts as late.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC003 | All reminder keys for this meeting and start time are already stored. | Clock 03:00Z. | Return: 0; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC004 | The same email key is produced twice in one run (two reads of the same participant). | Clock 03:00Z. | Return: Only one email is queued for that key.<br>Exception: N/A<br>Log Message: N/A | Abnormal | BR-303 |
| UTC005 | A Guest invitation; the link provider returns Unavailable. | Clock 03:00Z. | Return: No email for the Guest and the key is not recorded, so the next cycle tries again; participants are unaffected.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-303 |
| UTC006 | The meeting title contains a line break and is 320 characters long. | Clock 03:00Z. | Return: The subject has the line break replaced by a space and is at most 300 characters.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
