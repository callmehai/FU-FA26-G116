# Meeting.Update — Unit (FE-04, MEET)

Description: Meeting update: validation, reschedule rules and invitation staging.

Precondition: Meeting in an editable status; organizer actor.

## MeetingService.UpdateAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:207`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Draft meeting owned by the actor. | input: new title "Họp mới", same times. | Return: Success; title updated, status still Draft; Rescheduled false; MSG30.<br>Exception: N/A<br>Log Message: Saved: Meeting row with new title and UpdatedAt; no history row. | Normal | UC-15 · BR-27 · BR-28 · MSG30 |
| UTC002 | Draft meeting; input identical to the stored values. | input equals stored. | Return: Success; nothing is saved; MSG30.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-15 · BR-27 · BR-28 · MSG30 |
| UTC003 | Draft meeting whose planned start is already in the past. | input: start yesterday, end later. | Return: Success; the past start is not checked when editing a Draft.<br>Exception: N/A<br>Log Message: Saved: Meeting row. | Boundary | UC-15 · BR-27 · BR-75 · MSG30 |
| UTC004 | Rejected meeting. | input: new description. | Return: Success; status remains Rejected; MSG30.<br>Exception: N/A<br>Log Message: Saved: Meeting row. | Normal | UC-15 · BR-27 · BR-28 · MSG30 |
| UTC005 | Meeting does not exist or is not owned by the actor. | id: unknown. | Return: Failure NotFound (404); the input factory is not called.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-29 |
| UTC006 | Meeting is WaitingApproval. | input: any. | Return: Failure NotEditable; MSG33; the input is not read or validated.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-34 · MSG33 |
| UTC007 | Meeting is Cancelled. | input: any. | Return: Failure NotEditable; MSG33.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-299 · MSG33 |
| UTC008 | Draft meeting; title blank. | input: title "". | Return: Invalid; errors { title }; MSG31; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-24 · MSG31 |
| UTC009 | Published meeting; the planned start moves to a later time. | input: same title, mode, sensitivity, description; start +1 day, end +1 day. | Return: Success with Rescheduled true; MSG138; status stays Published.<br>Exception: N/A<br>Log Message: Saved: Meeting; history row Reschedule (Published to Published) by the organizer; invitation emails staged with previous times; notification "Cuộc họp đã dời lịch" for every current invitee except the organizer. | Normal | UC-15 · BR-27 · BR-75 · BR-76 · BR-158 · MSG138 |
| UTC010 | Published meeting; only the location changes. | input: location "Phòng 502" instead of "Phòng 301". | Return: Success with Rescheduled true; MSG138.<br>Exception: N/A<br>Log Message: Saved: Reschedule history row; invitee notifications and emails staged. | Boundary | UC-15 · BR-76 · BR-158 · MSG138 |
| UTC011 | Published meeting; the new start is not after now. | input: start = now. | Return: Invalid; errors { plannedStartTime }; MSG31.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-15 · BR-75 · MSG31 |
| UTC012 | Published meeting; the title is changed. | input: title differs. | Return: Invalid; errors { title: "Cuộc họp đã công bố, chỉ được dời thời gian, địa điểm và link họp." }; MSG31.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-158 · MSG31 |
| UTC013 | Published meeting; mode, sensitivity and description are all changed. | input: different mode, sensitivity, description. | Return: Invalid; errors for mode, sensitivity and description with the same lock message; MSG31.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-15 · BR-158 · MSG31 |
| UTC014 | Published meeting; the input equals the stored data. | input identical. | Return: Success; Rescheduled false; no history row, no notification; MSG30.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-15 · BR-76 · MSG30 |
| UTC015 | Published online meeting; the stored location is the legacy text "Trực tuyến" and the form sends an empty location. | mode Online; times unchanged. | Return: Success; Rescheduled false; nothing is saved or notified, because location is ignored for Online meetings.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-15 · BR-76 · MSG30 |
| UTC016 | The store fails while saving. | valid input on a Draft. | Return: Failure SaveFailed; MSG36; the error is logged.<br>Exception: PersistenceException is caught inside the service.<br>Log Message: Error logged with meeting and actor ids; transaction rolled back. | Abnormal | UC-15 · MSG36 |
