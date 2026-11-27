# Meeting.Update — Unit (FE-04, MEET)

Description: Meeting update: validation, reschedule rules and invitation staging.

Precondition: Meeting in an editable status; organizer actor.

## MeetingService.UpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:207`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Draft meeting owned by the actor. | input: new title "Họp mới", same times. | Return: Success; title updated, status still Draft; Rescheduled false; MSG30.<br>Exception: N/A<br>Log Message: Saved: Meeting row with new title and UpdatedAt; no history row. | Normal | UC-14 · BR-014 · MSG30 |
| UTC002 | Meeting is WaitingApproval. | input: any. | Return: Failure NotEditable; MSG33; the input is not read or validated.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-018 · MSG33 |
| UTC003 | Draft meeting; title blank. | input: title "". | Return: Invalid; errors { title }; MSG31; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-013 · MSG31 |
| UTC004 | Published meeting; the planned start moves to a later time. | input: same title, mode, sensitivity, description; start +1 day, end +1 day. | Return: Success with Rescheduled true; MSG138; status stays Published.<br>Exception: N/A<br>Log Message: Saved: Meeting; history row Reschedule (Published to Published) by the organizer; invitation emails staged with previous times; notification "Cuộc họp đã dời lịch" for every current invitee except the organizer. | Normal | UC-14 · BR-014 · BR-015 · MSG138 |
