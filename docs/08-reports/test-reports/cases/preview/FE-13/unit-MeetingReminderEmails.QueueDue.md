# MeetingReminderEmails.QueueDue — Unit (FE-13, NOTI)

Description: Queuing reminder emails for meetings starting soon.

Precondition: Meetings about to start; clock replaced.

## MeetingReminderEmails.QueueDueAsync

Layer: Application · `backend/TrustCabinet.Application/Email/MeetingReminderEmails.cs:31`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Clock 2026-11-18T03:00Z.<br>2. No Published meeting starts within the next 15 minutes. | No input. | Return: 0; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Normal | BR-303 |
| UTC002 | Published in-person meeting "Họp giao ban" starts at 03:10Z; organizer An (an@example.vn); participants Bình (Chair) and Cường (Member); invitations were sent days ago. | Clock 03:00Z; application base URL https://tc.example.vn/. | Return: 2 (one reminder per participant).<br>Exception: N/A<br>Log Message: Two EmailMessage rows queued, subject "Cuộc họp sắp bắt đầu: Họp giao ban", deduplication key "email-starting-soon:<meetingId>:<start unix seconds>:<participant id>"; the text names the start time, place and the link https://tc.example.vn/mgmt/meetings/<meetingId>; the HTML shows the role (Chủ toạ / Thành viên), the chair and the organizer contact "An (an@example.vn)"; one commit. | Normal | BR-303 |
| UTC003 | An online meeting held in the TrustCabinet room (Mode Online, no external link). | Clock 03:00Z. | Return: The button and the link point to /mgmt/meetings/<meetingId>/room, labelled "Vào phòng họp".<br>Exception: N/A<br>Log Message: N/A | Normal | BR-303 |
| UTC004 | An online meeting with an external link, or a hybrid meeting with an external link. | Clock 03:00Z. | Return: The button is "Xem cuộc họp" pointing to /mgmt/meetings/<meetingId>.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC005 | A hybrid meeting with no external link. | Clock 03:00Z. | Return: The button is "Vào phòng họp" (a meeting that is not in person and has no link is entered from the app).<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC006 | Participant Bình was invited at 02:59Z, inside the 15-minute window before the 03:10Z start. | Clock 03:00Z. | Return: No email for Bình; the other participants still get theirs.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC007 | Participant invited exactly at 02:55Z, which is 15 minutes before a 03:10Z start. | Clock 03:00Z. | Return: No email: an invitation created at the boundary counts as late.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC008 | Participant invited at 02:54:59Z, one second before the boundary. | Clock 03:00Z. | Return: An email is queued.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC009 | All reminder keys for this meeting and start time are already stored. | Clock 03:00Z. | Return: 0; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC010 | The meeting was reminded for 03:10Z, then rescheduled to 03:12Z; the new key holds the new start. | Clock 03:00Z. | Return: One email per participant for the new time.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-15 · BR-303 · BR-76 |
| UTC011 | The same email key is produced twice in one run (two reads of the same participant). | Clock 03:00Z. | Return: Only one email is queued for that key.<br>Exception: N/A<br>Log Message: N/A | Abnormal | BR-303 |
| UTC012 | A Guest invitation on the meeting; the link provider prepares the link https://tc.example.vn/guest/verify?t=abc. | Clock 03:00Z. | Return: An email to the Guest: button "Xác minh email để vào cuộc họp" with that link; no place and no role; the contact is the organizer's name only.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-04 · BR-303 |
| UTC013 | A Guest invitation; the link provider returns Unavailable. | Clock 03:00Z. | Return: No email for the Guest and the key is not recorded, so the next cycle tries again; participants are unaffected.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-303 |
| UTC014 | A Guest invitation; the link provider returns Invalid. | Clock 03:00Z. | Return: No email for the Guest.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-04 · BR-303 |
| UTC015 | A Guest invited at 02:59Z (inside the window). | Clock 03:00Z. | Return: No email for the Guest and the link provider is not asked.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-04 · BR-303 |
| UTC016 | A Guest whose reminder key was already stored. | Clock 03:00Z. | Return: No email; the link provider is not asked.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-04 · BR-303 |
| UTC017 | Two meetings start within the window, each with 2 participants. | Clock 03:00Z. | Return: 4.<br>Exception: N/A<br>Log Message: N/A | Normal | BR-303 |
| UTC018 | The meeting has no active Chair. | Clock 03:00Z. | Return: Reminders are still queued; the HTML has no "Chủ toạ" row.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC019 | The meeting title contains a line break and is 320 characters long. | Clock 03:00Z. | Return: The subject has the line break replaced by a space and is at most 300 characters.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC020 | The subject's 300th character would split a surrogate pair. | Clock 03:00Z. | Return: The subject is cut to 299 characters.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
| UTC021 | Only deduplicated work: every recipient is skipped. | Clock 03:00Z. | Return: 0; no save is made when nothing was queued.<br>Exception: N/A<br>Log Message: N/A | Boundary | BR-303 |
