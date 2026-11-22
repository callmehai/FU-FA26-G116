# Meeting.Create — Unit (FE-04, MEET)

Description: Meeting create and update validation: title, mode, timestamps and the Published reschedule lock.

Precondition: Organizer with meeting-creation capability; valid timestamps with offsets.

## MeetingService.CreateAsync

Layer: Application · `backend/TrustCabinet.Application/Meetings/MeetingService.cs:22`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Clock is 2026-11-18T08:00:00Z.<br>2. Chair and Secretary users exist and are active. | input: title "Họp giao ban", start 2026-11-19T02:00:00Z, end 03:00:00Z, InPerson; participants: Chair user A, Secretary user B. | Return: MeetingResult Success; meeting Draft, CreatedById = actor, no approver; participants A (Chair) and B (Secretary) each with response Pending.<br>Exception: N/A<br>Log Message: Saved: Meeting row, 2 participant rows, history row Create (from none to Draft) by the organizer. | Normal | UC-14 · BR-24 · BR-26 · BR-162 · BR-320 · MSG29 |
| UTC002 | Input has an empty title. | input: title "", rest valid; participants: valid. | Return: MeetingResult Invalid; errors { title }; MSG31; nothing saved.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-24 · MSG31 |
| UTC003 | Planned start is not after the clock. | start = now. | Return: MeetingResult Invalid; errors { plannedStartTime: "Thời gian bắt đầu phải sau thời điểm hiện tại." }; MSG31.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-14 · BR-162 · MSG31 |
| UTC004 | Participants list has 101 rows. | participants: 101 distinct active users, one Chair. | Return: InvalidParticipants; errors { participants }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-14 · BR-320 · MSG277 |
| UTC005 | No participant is Chair. | participants: [A Member, B Secretary]. | Return: InvalidParticipants; errors { participants: "Cuộc họp phải có đúng một Chủ toạ." }; MSG277.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-14 · BR-320 · MSG277 |
| UTC006 | The store fails while saving. | valid input and roster. | Return: MeetingResult SaveFailed; MSG35; the error is logged.<br>Exception: PersistenceException is caught inside the service.<br>Log Message: Error logged with the actor id; transaction rolled back. | Abnormal | UC-14 · MSG35 |
